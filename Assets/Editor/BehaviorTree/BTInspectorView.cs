using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AI.BehaviourTree.Editor
{
    /// <summary>
    /// 节点参数编辑面板 — 选中节点时显示其 Data struct 的所有字段
    /// </summary>
    public class BTInspectorView : VisualElement
    {
        private BTNodeView _currentNode;

        public BTInspectorView()
        {
            style.width = 220;
            style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            style.paddingTop = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;

            ShowEmptyHint();
        }

        /// <summary>选中节点 → 刷新面板</summary>
        public void Show(BTNodeView nodeView)
        {
            _currentNode = nodeView;
            Clear();

            // ===== 标题：节点参数 =====
            var title = new Label("节点参数");
            title.style.color = Color.white;
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2;
            Add(title);

            // ===== 节点名（可编辑，同步到画布） =====
            var nameField = new TextField("名称");
            nameField.value = nodeView.DisplayName;
            nameField.style.marginBottom = 8;
            nameField.RegisterValueChangedCallback(evt =>
            {
                nodeView.SetDisplayName(evt.newValue);
                SetDirty();
            });
            Add(nameField);

            // ===== 描述文本 =====
            if (!string.IsNullOrEmpty(nodeView.Description))
            {
                var descLabel = new Label(nodeView.Description);
                descLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                descLabel.style.fontSize = 10;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                descLabel.style.marginBottom = 12;
                descLabel.style.paddingLeft = 0;
                descLabel.style.paddingRight = 0;
                Add(descLabel);
            }

            var nodeType = nodeView.NodeType;
            var dataType = nodeView.DataType;
            var dataObj = nodeView.DataObject;

            // ===== 加权随机节点：自动从子节点同步 Entries =====
            if (nodeType != null && nodeType.FullName == "AI.BehaviourTree.BTWeightedRandom"
                && dataObj is AI.BehaviourTree.WeightedRandomData wrData)
            {
                var connections = nodeView.OutputPort?.connections.ToList() ?? new();
                int childCount = connections.Count;
                if (wrData.Entries == null)
                    wrData.Entries = new List<AI.BehaviourTree.WeightedRandomData.WeightEntry>();
                while (wrData.Entries.Count < childCount)
                    wrData.Entries.Add(new AI.BehaviourTree.WeightedRandomData.WeightEntry { Weight = 0 });
                while (wrData.Entries.Count > childCount)
                    wrData.Entries.RemoveAt(wrData.Entries.Count - 1);
                // 从子节点名字自动填充 Label
                for (int i = 0; i < childCount; i++)
                {
                    var childView = connections[i].input.node as BTNodeView;
                    if (childView != null && string.IsNullOrEmpty(wrData.Entries[i].Label))
                        wrData.Entries[i].Label = childView.DisplayName;
                }
            }

            if (dataType == null || dataObj == null)
            {
                var hint = new Label("（无参数）");
                hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                Add(hint);
                return;
            }

            // 反射遍历 Data struct 的所有 public 字段
            var fields = nodeView.DataType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                Add(CreateFieldRow(field, nodeView.DataObject));
            }
        }

        /// <summary>取消选中</summary>
        public void ClearSelection()
        {
            _currentNode = null;
            Clear();
            ShowEmptyHint();
        }

        private void ShowEmptyHint()
        {
            var hint = new Label("选中节点\n编辑参数");
            hint.style.color = new Color(0.4f, 0.4f, 0.4f);
            hint.style.fontSize = 13;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 20;
            Add(hint);
        }

        /// <summary>为一个字段生成 Label + Input 行</summary>
        private VisualElement CreateFieldRow(FieldInfo field, object target)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 8;

            // 标签（字段名 + Tooltip）
            var label = new Label(ObjectNames.NicifyVariableName(field.Name));
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            label.style.fontSize = 10;
            // 读 [Tooltip] 属性 → 鼠标悬停字段名时显示
            var tooltipAttr = field.GetCustomAttribute<TooltipAttribute>();
            if (tooltipAttr != null)
                label.tooltip = tooltipAttr.tooltip;
            row.Add(label);

            // 输入控件 — 按类型选择
            if (field.FieldType == typeof(float))
            {
                var f = new FloatField();
                f.value = (float)field.GetValue(target);
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(target, evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }
            else if (field.FieldType == typeof(string))
            {
                var f = new TextField();
                f.value = (string)field.GetValue(target) ?? "";
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(target, evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }
            else if (field.FieldType == typeof(bool))
            {
                var f = new Toggle();
                f.value = (bool)field.GetValue(target);
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(target, evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }
            else if (field.FieldType == typeof(int))
            {
                var f = new IntegerField();
                f.value = (int)field.GetValue(target);
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(target, evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }
            else if (field.FieldType.IsEnum)
            {
                var f = new EnumField((Enum)field.GetValue(target));
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(target, evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }
            else if (field.FieldType.IsGenericType &&
                     field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                // List<T> 类型：遍历每个元素显示其字段
                var list = field.GetValue(target) as System.Collections.IList;
                if (list == null || list.Count == 0)
                {
                    var hint = new Label("（空列表，添加子节点后自动填充）");
                    hint.style.color = new Color(0.5f, 0.5f, 0.5f);
                    hint.style.fontSize = 10;
                    row.Add(hint);
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        var itemType = item.GetType();
                        var itemFields = itemType.GetFields(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance);

                        // 条目标题（序号 + Label 字段值）
                        var labelField = itemType.GetField("Label");
                        string itemTitle = labelField != null
                            ? (labelField.GetValue(item) as string ?? $"条目 {i}")
                            : $"条目 {i}";

                        var header = new Label($"  ─ {itemTitle}");
                        header.style.color = new Color(0.6f, 0.8f, 1f);
                        header.style.fontSize = 11;
                        header.style.marginTop = 2;
                        header.style.marginBottom = 2;
                        row.Add(header);

                        int ci = i; // 闭包捕获
                        foreach (var itemField in itemFields)
                        {
                            if (itemField.Name == "Label") continue; // Label 已显示在标题上
                            AddSubField(row, itemField, item, ci, list);
                        }
                    }
                }
            }
            else
            {
                // 不支持编辑的类型，只显示值
                var f = new Label(field.GetValue(target)?.ToString() ?? "null");
                f.style.color = Color.gray;
                row.Add(f);
            }

            return row;
        }

        /// <summary>为列表中的子字段生成输入控件</summary>
        private void AddSubField(VisualElement parent, System.Reflection.FieldInfo field,
            object item, int index, System.Collections.IList list)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 16;
            row.style.marginBottom = 2;

            var label = new Label(ObjectNames.NicifyVariableName(field.Name));
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.fontSize = 10;
            label.style.width = 50;
            row.Add(label);

            if (field.FieldType == typeof(float))
            {
                var f = new FloatField();
                f.value = (float)field.GetValue(item);
                int ci = index;
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(list[ci], evt.newValue);
                    SetDirty();
                    // 加权随机：自动计算最后一个 0 权重
                    if (_currentNode?.NodeType?.FullName == "AI.BehaviourTree.BTWeightedRandom")
                        AutoCalcWeights(list);
                });
                row.Add(f);
            }
            else if (field.FieldType == typeof(string))
            {
                var f = new TextField();
                f.value = (string)field.GetValue(item) ?? "";
                f.style.flexGrow = 1f;
                int ci = index;
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(list[ci], evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }
            else if (field.FieldType == typeof(bool))
            {
                var f = new Toggle();
                f.value = (bool)field.GetValue(item);
                int ci = index;
                f.RegisterValueChangedCallback(evt =>
                {
                    field.SetValue(list[ci], evt.newValue);
                    SetDirty();
                });
                row.Add(f);
            }

            parent.Add(row);
        }

        /// <summary>加权随机：最后一个条目始终自动 = 100 - 前面之和</summary>
        private void AutoCalcWeights(System.Collections.IList list)
        {
            if (list == null || list.Count < 2) return;

            // 前 N-1 项之和
            float sum = 0f;
            for (int i = 0; i < list.Count - 1; i++)
            {
                var entry = list[i];
                var wf = entry.GetType().GetField("Weight");
                if (wf == null) continue;
                sum += Mathf.Max(0f, (float)wf.GetValue(entry));
            }

            // 最后一项 = 100 - sum
            var last = list[list.Count - 1];
            var lastWeight = last.GetType().GetField("Weight");
            if (lastWeight != null)
                lastWeight.SetValue(last, Mathf.Max(0f, Mathf.Min(100f, 100f - sum)));

            // 刷新面板显示计算结果
            if (_currentNode != null)
                schedule.Execute(() => Show(_currentNode)).StartingIn(200);
        }

        private void SetDirty()
        {
            if (_currentNode?.GetContainingGraphView() is BehaviorTreeGraphView gv)
                gv.MarkDirty();
        }
    }

    /// <summary>BTNodeView 扩展：获取所属的 GraphView</summary>
    public static class BTNodeViewExtensions
    {
        public static BehaviorTreeGraphView GetContainingGraphView(this BTNodeView node)
        {
            // GraphElement 的 hierarchy 往上找，或通过 GetFirstAncestorOfType
            return node.GetFirstAncestorOfType<BehaviorTreeGraphView>();
        }
    }
}
