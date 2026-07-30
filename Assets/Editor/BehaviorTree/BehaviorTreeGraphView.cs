using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AI.BehaviourTree.Editor
{
    /// <summary>
    /// 行为树可视化画布 — 继承 Unity 的 GraphView
    /// 自带：滚轮缩放、中键平移、Alt+左键框选
    /// 我们需要加：右键创建节点、拖拽连线、保存/加载
    /// </summary>
    public class BehaviorTreeGraphView : GraphView
    {
        // 记录右键点击位置
        private Vector2 _lastRightClickPos;

        // 中键平移
        private Vector2 _panStart;
        private bool _isPanning;

        /// <summary>是否有未保存的修改</summary>
        public bool IsDirty { get; internal set; }

        // 根节点 ID（从 SO 加载时记录）
        private string _rootNodeId;

        // ===== 子图导航（文件夹节点钻取） =====
        private string _currentFolderId;               // 当前所在文件夹 ID，null=根视图
        private readonly Stack<FolderViewState> _folderStack = new();

        private struct FolderViewState
        {
            public string FolderId;
            public Vector3 ViewPosition;
            public Vector3 ViewScale;
        }

        /// <summary>导航发生变化时触发（用于更新面包屑）</summary>
        public event Action OnScopeChanged;

        public BehaviorTreeGraphView()
        {
            // ========== 缩放 / 框选 / 移节点 ==========
            this.AddManipulator(new ContentZoomer());
            this.SetupZoom(0.1f, 2.0f);
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // ========== 中键平移（不用 ContentDragger，它默认吞噬左键事件） ==========
            RegisterCallback<MouseDownEvent>(OnPanMouseDown);
            RegisterCallback<MouseMoveEvent>(OnPanMouseMove);
            RegisterCallback<MouseUpEvent>(OnPanMouseUp);

            // ========== 背景网格 ==========
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // 加载 USS 样式表让网格线更清晰
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/BehaviorTree/BehaviorTreeEditor.uss");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            // ========== 记录右键位置 ==========
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1)
                    _lastRightClickPos = evt.localMousePosition;
            });

            // ========== 复制 / 粘贴（Ctrl+C / Ctrl+V） ==========
            RegisterCallback<KeyDownEvent>(evt =>
            {
                bool ctrl = evt.ctrlKey || evt.commandKey;
                if (ctrl && evt.keyCode == KeyCode.C)
                {
                    CopySelectedNodes();
                    evt.StopPropagation();
                }
                else if (ctrl && evt.keyCode == KeyCode.V)
                {
                    PasteNodes();
                    evt.StopPropagation();
                }
            });

            // ========== 监听画布变化 ==========
            graphViewChanged += changes => { MarkDirty(); OnGraphChanged?.Invoke(); return changes; };

            // ========== 双击文件夹 → 进入子视图 ==========
            BTNodeView.OnSubTreeDoubleClick += EnterFolder;
        }

        /// <summary>标记为有修改</summary>
        public void MarkDirty()
        {
            if (_isLoading) return;
            IsDirty = true;
            OnDirty?.Invoke();
        }

        /// <summary>状态变脏时触发（EditorWindow 用来更新 hasUnsavedChanges）</summary>
        public event Action OnDirty;

        /// <summary>画布结构变化时触发（连线变动等，用于刷新 Inspector）</summary>
        public event Action OnGraphChanged;

        /// <summary>是否正在从 SO 加载（加载中不标记脏）</summary>
        private bool _isLoading;

        /// <summary>移除静态事件订阅，在 EditorWindow.OnDestroy 或移除 GraphView 时调用</summary>
        public void Cleanup()
        {
            BTNodeView.OnSubTreeDoubleClick -= EnterFolder;
        }

        // ========== 复制选中节点到剪贴板 ==========
        private void CopySelectedNodes()
        {
            var nodeViews = selection.OfType<BTNodeView>().ToList();
            if (nodeViews.Count == 0) return;

            var entries = new List<BehaviorTreeSO.NodeEntry>();
            foreach (var node in nodeViews)
            {
                // 收集子节点 ID（只保留也在选中列表里的子节点）
                var childIds = new List<string>();
                if (node.OutputPort != null)
                {
                    foreach (var edge in node.OutputPort.connections)
                    {
                        var childView = edge.input.node as BTNodeView;
                        if (childView != null && nodeViews.Contains(childView))
                            childIds.Add(childView.NodeId);
                    }
                }

                entries.Add(new BehaviorTreeSO.NodeEntry
                {
                    Id = node.NodeId,
                    TypeName = node.NodeType.FullName,
                    Position = node.GetPosition().position,
                    JsonData = SerializeData(node),
                    ChildIds = childIds,
                    CustomName = node.DisplayName
                });
            }
            _clipboard = "BTCP:" + JsonUtility.ToJson(new CopyData { Nodes = entries });
        }

        // ========== 从剪贴板粘贴节点 ==========
        private void PasteNodes()
        {
            if (string.IsNullOrEmpty(_clipboard) || !_clipboard.StartsWith("BTCP:")) return;

            string json = _clipboard.Substring("BTCP:".Length);
            var copyData = JsonUtility.FromJson<CopyData>(json);
            if (copyData?.Nodes == null || copyData.Nodes.Count == 0) return;

            var idMap = new Dictionary<string, string>();
            var pastedViews = new List<BTNodeView>();

            // 第一遍：创建节点
            foreach (var entry in copyData.Nodes)
            {
                var typeInfo = BTNodeFactory.GetAllNodeTypes()
                    .Find(t => t.Type.FullName == entry.TypeName);
                if (typeInfo == null) continue;

                string newId = System.Guid.NewGuid().ToString();
                idMap[entry.Id] = newId;

                string name = string.IsNullOrEmpty(entry.CustomName)
                    ? typeInfo.Name : entry.CustomName;

                var nodeView = new BTNodeView(
                    typeInfo.Type, name,
                    typeInfo.NodeCategory, typeInfo.Description ?? "");
                nodeView.NodeId = newId;
                nodeView.SetPosition(new Rect(entry.Position + new Vector2(30, 30), Vector2.zero));
                DeserializeData(nodeView, entry.JsonData);
                AddElement(nodeView);
                pastedViews.Add(nodeView);
            }

            // 第二遍：恢复节点间的内部连线（如果父子节点都被复制了）
            foreach (var entry in copyData.Nodes)
            {
                if (entry.ChildIds == null || entry.ChildIds.Count == 0) continue;
                if (!idMap.TryGetValue(entry.Id, out var newParentId)) continue;
                var parentView = pastedViews.Find(n => n.NodeId == newParentId);
                if (parentView?.OutputPort == null) continue;

                foreach (var childId in entry.ChildIds)
                {
                    if (!idMap.TryGetValue(childId, out var newChildId)) continue;
                    var childView = pastedViews.Find(n => n.NodeId == newChildId);
                    if (childView?.InputPort == null) continue;

                    var edge = parentView.OutputPort.ConnectTo(childView.InputPort);
                    AddElement(edge);
                }
            }

            // 选中粘贴出来的节点
            ClearSelection();
            foreach (var n in pastedViews) AddToSelection(n);
            IsDirty = true;
        }

        // ========== 中键平移画布 ==========
        private void OnPanMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 2) // 中键
            {
                _panStart = evt.mousePosition;
                _isPanning = true;
                evt.StopPropagation();
            }
        }

        private void OnPanMouseMove(MouseMoveEvent evt)
        {
            if (_isPanning)
            {
                Vector2 delta = evt.mousePosition - _panStart;
                _panStart = evt.mousePosition;
                // 移动内容容器实现画布平移
                contentViewContainer.transform.position += (Vector3)delta;
                evt.StopPropagation();
            }
        }

        private void OnPanMouseUp(MouseUpEvent evt)
        {
            if (evt.button == 2 && _isPanning)
            {
                _isPanning = false;
                evt.StopPropagation();
            }
        }

        /// <summary>定位到根节点</summary>
        public void FocusOnRoot()
        {
            if (string.IsNullOrEmpty(_rootNodeId)) return;

            var allNodes = nodes.ToList().OfType<BTNodeView>().ToList();
            var root = allNodes.Find(n => n.NodeId == _rootNodeId);
            if (root == null) return;

            // 把视图中心移到根节点位置
            var rootPos = root.GetPosition().center;
            var viewRect = contentViewContainer.layout;
            viewTransform.position = new Vector3(
                -rootPos.x + viewRect.width / 2f,
                -rootPos.y + viewRect.height / 2f,
                0);
            viewTransform.scale = Vector3.one;

            // 选中根节点
            ClearSelection();
            AddToSelection(root);
        }

        // ========== 子图导航（文件夹钻取） ==========

        /// <summary>双击文件夹节点时调用 — 进入子视图</summary>
        public void EnterFolder(BTNodeView folderNode)
        {
            // 保存当前视图状态
            _folderStack.Push(new FolderViewState
            {
                FolderId = _currentFolderId,
                ViewPosition = viewTransform.position,
                ViewScale = viewTransform.scale
            });

            _currentFolderId = folderNode.NodeId;
            ApplyScopeVisibility();
            FocusOnFolder(folderNode);

            // 选中文件夹节点自身，方便查看参数
            ClearSelection();
            AddToSelection(folderNode);

            OnScopeChanged?.Invoke();
        }

        /// <summary>返回上一层视图</summary>
        public void ExitFolder()
        {
            if (_folderStack.Count == 0) return;

            var prev = _folderStack.Pop();
            _currentFolderId = prev.FolderId;
            viewTransform.position = prev.ViewPosition;
            viewTransform.scale = prev.ViewScale;

            ApplyScopeVisibility();

            OnScopeChanged?.Invoke();
        }

        /// <summary>返回到根视图</summary>
        public void ExitToRoot()
        {
            if (_folderStack.Count == 0 && string.IsNullOrEmpty(_currentFolderId)) return;

            // 如果当前在根视图，不需要操作
            if (string.IsNullOrEmpty(_currentFolderId)) return;

            // 清空栈并回到根
            _folderStack.Clear();
            _currentFolderId = null;

            // 回到根视图的保存位置（如果没有保存过，用 FocusOnRoot）
            FocusOnRoot();

            ApplyScopeVisibility();
            OnScopeChanged?.Invoke();
        }

        /// <summary>回到指定层级的文件夹</summary>
        public void ExitToFolder(string folderId)
        {
            // 如果是当前视图，不做操作
            if (_currentFolderId == folderId) return;

            // 如果回到根
            if (string.IsNullOrEmpty(folderId))
            {
                ExitToRoot();
                return;
            }

            // 倒栈直到找到目标文件夹
            bool found = false;
            while (_folderStack.Count > 0)
            {
                var prev = _folderStack.Pop();
                if (prev.FolderId == folderId || _currentFolderId == folderId)
                {
                    found = true;
                    _currentFolderId = prev.FolderId;
                    viewTransform.position = prev.ViewPosition;
                    viewTransform.scale = prev.ViewScale;
                    break;
                }
                _currentFolderId = prev.FolderId;
            }

            if (!found) return;
            ApplyScopeVisibility();
            OnScopeChanged?.Invoke();
        }

        /// <summary>获取从根到当前文件夹的路径（用于面包屑）</summary>
        public List<FolderBreadcrumb> GetBreadcrumbs()
        {
            var list = new List<FolderBreadcrumb>();

            // 根目录
            list.Add(new FolderBreadcrumb { FolderId = null, DisplayName = "根目录" });

            if (_folderStack.Count == 0 && string.IsNullOrEmpty(_currentFolderId))
                return list;

            // 重建路径：从栈底（最早进入）到栈顶（最近进入）+ 当前文件夹
            var stackArray = _folderStack.ToArray();
            // stackArray[0] = 栈顶（最新），stackArray[^1] = 栈底（最早）
            var pathIds = new List<string>();
            for (int i = stackArray.Length - 1; i >= 0; i--)
                pathIds.Add(stackArray[i].FolderId);
            if (!string.IsNullOrEmpty(_currentFolderId))
                pathIds.Add(_currentFolderId);

            var allNodes = nodes.ToList().OfType<BTNodeView>().ToList();
            foreach (var id in pathIds)
            {
                if (id == null) continue;
                var node = allNodes.Find(n => n.NodeId == id);
                list.Add(new FolderBreadcrumb
                {
                    FolderId = id,
                    DisplayName = node != null ? node.DisplayName : "(未知)"
                });
            }

            return list;
        }

        /// <summary>根据当前 _currentFolderId 显示/隐藏节点</summary>
        private void ApplyScopeVisibility()
        {
            var allNodeViews = nodes.ToList().OfType<BTNodeView>().ToList();
            var visibleIds = GetVisibleNodeIds(allNodeViews);

            foreach (var node in allNodeViews)
            {
                bool visible = visibleIds.Contains(node.NodeId);
                node.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // 隐藏连接到不可见节点的边
            foreach (var edge in edges.ToList())
            {
                var inputNode = edge.input.node as BTNodeView;
                var outputNode = edge.output.node as BTNodeView;
                bool edgeVisible = (inputNode == null || visibleIds.Contains(inputNode.NodeId)) &&
                                   (outputNode == null || visibleIds.Contains(outputNode.NodeId));
                edge.style.display = edgeVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>收集当前作用域下应显示的节点 ID 集合</summary>
        private HashSet<string> GetVisibleNodeIds(List<BTNodeView> allNodeViews)
        {
            var ids = new HashSet<string>();

            if (string.IsNullOrEmpty(_currentFolderId))
            {
                // 根视图：显示所有节点
                foreach (var n in allNodeViews)
                    ids.Add(n.NodeId);
            }
            else
            {
                // 文件夹视图：显示文件夹节点本身 + 所有子孙节点
                ids.Add(_currentFolderId);
                var folderNode = allNodeViews.Find(n => n.NodeId == _currentFolderId);
                if (folderNode != null)
                    CollectDescendants(folderNode, allNodeViews, ids);
            }

            return ids;
        }

        /// <summary>递归收集 OutputPort 下游的所有子孙节点</summary>
        private static void CollectDescendants(BTNodeView parent, List<BTNodeView> allNodes, HashSet<string> ids)
        {
            if (parent.OutputPort == null) return;
            foreach (var edge in parent.OutputPort.connections)
            {
                var child = edge.input.node as BTNodeView;
                if (child != null && ids.Add(child.NodeId)) // Add 返回 true 表示之前不在集合里
                {
                    CollectDescendants(child, allNodes, ids); // 递归继续往下
                }
            }
        }

        /// <summary>定位视图到文件夹节点，并适当缩放</summary>
        private void FocusOnFolder(BTNodeView folderNode)
        {
            var allNodeViews = nodes.ToList().OfType<BTNodeView>().ToList();
            var visibleIds = GetVisibleNodeIds(allNodeViews);

            // 计算所有可见节点的包围盒
            var visibleNodes = allNodeViews.Where(n => visibleIds.Contains(n.NodeId)).ToList();
            if (visibleNodes.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var n in visibleNodes)
            {
                var rect = n.GetPosition();
                if (rect.x < minX) minX = rect.x;
                if (rect.y < minY) minY = rect.y;
                if (rect.xMax > maxX) maxX = rect.xMax;
                if (rect.yMax > maxY) maxY = rect.yMax;
            }

            float width = maxX - minX + 100;
            float height = maxY - minY + 100;

            var viewRect = contentViewContainer.layout;
            float scaleX = viewRect.width / width;
            float scaleY = viewRect.height / height;
            float scale = Mathf.Min(scaleX, scaleY, 1.0f); // 不放大，只缩小
            scale = Mathf.Max(scale, 0.3f); // 最小 0.3

            viewTransform.position = new Vector3(
                -minX + (viewRect.width - width * scale) / (2f * scale),
                -minY + (viewRect.height - height * scale) / (2f * scale),
                0);
            viewTransform.scale = new Vector3(scale, scale, 1);
        }

        /// <summary>当前是否在子视图中（非根视图）</summary>
        public bool IsInSubView => !string.IsNullOrEmpty(_currentFolderId);

        /// <summary>当前所在文件夹 ID</summary>
        public string CurrentFolderId => _currentFolderId;

        /// <summary>面包屑条目</summary>
        public class FolderBreadcrumb
        {
            public string FolderId;
            public string DisplayName;
        }

        /// <summary>
        /// 右键菜单 — 列出所有 [BTNode] 类型，按分类分组
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var allTypes = BTNodeFactory.GetAllNodeTypes();

            // 按 Category(类别) 分组，第一段翻译成中文
            // 如 "Action/时间" → "动作节点/时间"
            var groups = new Dictionary<string, List<BTNodeTypeInfo>>();
            foreach (var info in allTypes)
            {
                //取
                string group = TranslateCategory(info.Category);
                if (!groups.ContainsKey(group))
                    groups[group] = new List<BTNodeTypeInfo>();
                groups[group].Add(info);
            }

            // 生成菜单项
            foreach (var kvp in groups)
            {
                string groupName = kvp.Key;
                foreach (var info in kvp.Value)
                {
                    // 菜单路径：创建节点 / Action/时间 / 等待
                    string path = $"创建节点/{groupName}/{info.Name}";
                    var typeInfo = info;  // 闭包捕获
                    evt.menu.AppendAction(path, _ =>
                    {
                        CreateNodeAt(typeInfo, _lastRightClickPos);
                    });
                }
            }
        }

        /// <summary>
        /// 分类路径第一段翻中文，如 "Action/时间" → "动作节点/时间"
        /// </summary>
        private static string TranslateCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return "未分类";

            int slash = category.IndexOf('/');
            string first = slash > 0 ? category.Substring(0, slash) : category;
            string rest = slash > 0 ? category.Substring(slash) : "";

            first = first switch
            {
                "Composite"  => "组合节点",
                "Decorator"  => "装饰节点",
                "Action"     => "动作节点",
                "Condition"  => "条件节点",
                _            => first
            };

            return first + rest;
        }

        /// <summary>
        /// 连线兼容性规则 — 拖线时 Unity 调用这个方法过滤可连端口
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();

            // 遍历画布上所有端口
            foreach (var port in ports.ToList())
            {
                // 不能连自己
                if (port == startPort) continue;
                // 不能连同一个节点的端口
                if (port.node == startPort.node) continue;
                // 方向必须相反：Output → Input
                if (port.direction == startPort.direction) continue;

                compatible.Add(port);
            }

            return compatible;
        }

        /// <summary>
        /// 在指定位置创建一个 BTNodeView
        /// </summary>
        public void CreateNodeAt(BTNodeTypeInfo info, Vector2 screenPos)
        {
            Vector2 graphPos = contentViewContainer.WorldToLocal(screenPos);
            var nodeView = new BTNodeView(info.Type, info.Name, info.NodeCategory, info.Description);
            nodeView.SetPosition(new Rect(graphPos.x, graphPos.y, 0, 0));
            AddElement(nodeView);
        }

        #region Data 序列化辅助

        private static string SerializeData(BTNodeView nodeView)
        {
            if (nodeView.DataType == null || nodeView.DataObject == null)
                return "";
            return JsonUtility.ToJson(nodeView.DataObject);
        }

        private static void DeserializeData(BTNodeView nodeView, string json)
        {
            if (nodeView.DataType == null || string.IsNullOrEmpty(json))
                return;
            try
            {
                nodeView.DataObject = JsonUtility.FromJson(json, nodeView.DataType);
            }
            catch { /* JSON 格式不兼容时保留默认值 */ }
        }

        #endregion

        #region 保存 / 加载

        /// <summary>
        /// 画布 → SO：遍历所有节点和连线，写入 NodeEntry 列表
        /// </summary>
        public void SaveToSO(BehaviorTreeSO so)
        {
            var allNodeViews = nodes.ToList().OfType<BTNodeView>().ToList();
            var nodeEntries = new List<BehaviorTreeSO.NodeEntry>();

            foreach (var nodeView in allNodeViews)
            {
                // 从 Output 端口找出所有子节点
                var childIds = new List<string>();
                if (nodeView.OutputPort != null)
                {
                    foreach (var edge in nodeView.OutputPort.connections)
                    {
                        var childView = edge.input.node as BTNodeView;
                        if (childView != null)
                            childIds.Add(childView.NodeId);
                    }

                    // 按 Y 坐标排序（Y越小=越靠上=优先级越高）
                    childIds.Sort((a, b) =>
                    {
                        var viewA = allNodeViews.Find(n => n.NodeId == a);
                        var viewB = allNodeViews.Find(n => n.NodeId == b);
                        return viewA.GetPosition().y.CompareTo(viewB.GetPosition().y);
                    });
                }

                nodeEntries.Add(new BehaviorTreeSO.NodeEntry
                {
                    Id = nodeView.NodeId,
                    TypeName = nodeView.NodeType.FullName,
                    Position = nodeView.GetPosition().position,
                    JsonData = SerializeData(nodeView),
                    ChildIds = childIds,
                    CustomName = nodeView.DisplayName
                });
            }

            // 找根节点（没有被别人连的节点）
            string rootId = "";
            foreach (var nodeView in allNodeViews)
            {
                bool hasParent = false;
                if (nodeView.InputPort != null)
                {
                    hasParent = nodeView.InputPort.connections.Any();
                }
                if (!hasParent)
                {
                    rootId = nodeView.NodeId;
                    break;
                }
            }

            so.SetNodes(nodeEntries);
            so.RootNodeId = rootId;
            EditorUtility.SetDirty(so);
            IsDirty = false;
            Debug.Log($"[BT Editor] 保存完成：{nodeEntries.Count} 个节点，根节点 {rootId}");
        }

        /// <summary>
        /// SO → 画布：清空画布，从 NodeEntry 列表重建所有节点和连线
        /// </summary>
        public void LoadFromSO(BehaviorTreeSO so)
        {
            _isLoading = true;  // 加载中不触发自动保存

            // 清空
            DeleteElements(graphElements.ToList());

            // 重置子图导航状态
            _folderStack.Clear();
            _currentFolderId = null;

            _rootNodeId = so.RootNodeId;
            var nodeViewMap = new Dictionary<string, BTNodeView>();

            // 先建所有节点
            foreach (var entry in so.Nodes)
            {
                var typeInfo = BTNodeFactory.GetAllNodeTypes()
                    .Find(t => t.Type.FullName == entry.TypeName);

                if (typeInfo == null)
                {
                    Debug.LogWarning($"[BT Editor] 找不到类型: {entry.TypeName}");
                    continue;
                }

                var nodeView = new BTNodeView(typeInfo.Type, typeInfo.Name, typeInfo.NodeCategory, typeInfo.Description);
                nodeView.NodeId = entry.Id;
                nodeView.SetPosition(new Rect(entry.Position, Vector2.zero));
                DeserializeData(nodeView, entry.JsonData);  // 恢复参数
                // 如果有自定义名，覆盖默认标题
                if (!string.IsNullOrEmpty(entry.CustomName))
                    nodeView.SetDisplayName(entry.CustomName);
                AddElement(nodeView);
                nodeViewMap[entry.Id] = nodeView;
            }

            // 再连所有线
            foreach (var entry in so.Nodes)
            {
                if (entry.ChildIds == null || entry.ChildIds.Count == 0) continue;
                if (!nodeViewMap.TryGetValue(entry.Id, out var parentView)) continue;
                if (parentView.OutputPort == null) continue;

                foreach (var childId in entry.ChildIds)
                {
                    if (!nodeViewMap.TryGetValue(childId, out var childView)) continue;
                    if (childView.InputPort == null) continue;

                    var edge = parentView.OutputPort.ConnectTo(childView.InputPort);
                    AddElement(edge);
                }
            }

            _isLoading = false;  // 加载完成
        }

        #endregion

        #region 复制 / 粘贴

        /// <summary>剪贴板数据包装（JsonUtility 需要顶层类）</summary>
        [System.Serializable]
        private class CopyData
        {
            public List<BehaviorTreeSO.NodeEntry> Nodes;
        }

        private string _clipboard; // 复制/粘贴剪贴板

        #endregion
    }
}
