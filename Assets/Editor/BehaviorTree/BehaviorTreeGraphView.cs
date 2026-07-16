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
            graphViewChanged += _ => { IsDirty = true; return _; };
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
            // 清空
            DeleteElements(graphElements.ToList());

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
