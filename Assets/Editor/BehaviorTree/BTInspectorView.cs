using System;
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

            // 标题
            var title = new Label("节点参数");
            title.style.color = Color.white;
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            Add(title);

            if (nodeView.DataType == null || nodeView.DataObject == null)
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
            else
            {
                // 不支持编辑的类型，只显示值
                var f = new Label(field.GetValue(target)?.ToString() ?? "null");
                f.style.color = Color.gray;
                row.Add(f);
            }

            return row;
        }

        private void SetDirty()
        {
            if (_currentNode?.GetContainingGraphView() is BehaviorTreeGraphView gv)
                gv.IsDirty = true;
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
