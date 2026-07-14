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

        /// <summary>是否有未保存的修改</summary>
        public bool IsDirty { get; internal set; }

        public BehaviorTreeGraphView()
        {
            // ========== 缩放 / 平移 / 框选 ==========
            this.AddManipulator(new ContentZoomer());
            this.SetupZoom(0.1f, 2.0f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // ========== 背景网格 ==========
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // ========== 记录右键位置 ==========
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1)
                    _lastRightClickPos = evt.localMousePosition;
            });

            // ========== 监听画布变化 ==========
            graphViewChanged += _ => { IsDirty = true; return _; };
        }

        /// <summary>
        /// 右键菜单 — 列出所有 [BTNode] 类型，按分类分组
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var allTypes = BTNodeFactory.GetAllNodeTypes();

            // 按 Category 分组，第一段翻译成中文
            // 如 "Action/时间" → "动作节点/时间"
            var groups = new Dictionary<string, List<BTNodeTypeInfo>>();
            foreach (var info in allTypes)
            {
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
            var nodeView = new BTNodeView(info.Type, info.Name, info.NodeCategory);
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
                }

                nodeEntries.Add(new BehaviorTreeSO.NodeEntry
                {
                    Id = nodeView.NodeId,
                    TypeName = nodeView.NodeType.FullName,
                    Position = nodeView.GetPosition().position,
                    JsonData = SerializeData(nodeView),
                    ChildIds = childIds
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

                var nodeView = new BTNodeView(typeInfo.Type, typeInfo.Name, typeInfo.NodeCategory);
                nodeView.NodeId = entry.Id;
                nodeView.SetPosition(new Rect(entry.Position, Vector2.zero));
                DeserializeData(nodeView, entry.JsonData);  // 恢复参数
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
    }
}
