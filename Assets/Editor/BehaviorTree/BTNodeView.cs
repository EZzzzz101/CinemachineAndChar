using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AI.BehaviourTree.Editor
{
    public enum BTNodeCategory
    {
        Composite,
        Decorator,
        Action,
        Condition
    }

    /// <summary>
    /// 行为树可视化节点（左→右流向）
    /// </summary>
    public class BTNodeView : Node
    {
        public System.Type NodeType { get; private set; }
        public string NodeId { get; set; }
        public string DisplayName { get; private set; }
        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }

        /// <summary>节点的参数数据对象（如 WaitData、CooldownData 实例）</summary>
        public object DataObject { get; set; }

        /// <summary>参数数据的 C# 类型（如 typeof(WaitData)），null = 无参数</summary>
        public System.Type DataType { get; private set; }

        /// <summary>节点描述文本</summary>
        public string Description { get; private set; }

        /// <summary>节点标题 Label，改标题时直接更新这个</summary>
        private Label _nameLabel;

        /// <summary>节点被选中时触发，参数面板监听此事件</summary>
        public static event Action<BTNodeView> OnNodeSelected;

        public BTNodeView(System.Type nodeType, string nodeName, BTNodeCategory category,
            string description = "", bool hasInput = true)
        {
            NodeType = nodeType;
            NodeId = System.Guid.NewGuid().ToString();
            DisplayName = nodeName;
            Description = description;
            Color color = GetColor(category);

            // ===== 初始化 Data 对象 =====
            DataType = GetDataType(nodeType);
            if (DataType != null)
                DataObject = Activator.CreateInstance(DataType);

            // ===== 顶部细色条 =====
            title = "";
            titleContainer.style.height = 4;
            titleContainer.style.backgroundColor = color;

            // ===== 大字 + 小字 =====
            _nameLabel = new Label(nodeName);
            _nameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            _nameLabel.style.fontSize = 14;
            _nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nameLabel.style.marginTop = 6;
            _nameLabel.style.marginLeft = 12;
            _nameLabel.style.marginRight = 12;
            _nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            mainContainer.Add(_nameLabel);

            var typeLabel = new Label(nodeType.Name);
            typeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            typeLabel.style.fontSize = 9;
            typeLabel.style.marginBottom = 4;
            typeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            mainContainer.Add(typeLabel);

            // ===== Input 端口（左边） =====
            if (hasInput)
            {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input,
                    Port.Capacity.Single, typeof(bool));
                InputPort.portName = "";
                inputContainer.Add(InputPort);
            }

            // ===== Output 端口（右边） =====
            if (category == BTNodeCategory.Composite)
            {
                OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output,
                    Port.Capacity.Multi, typeof(bool));
                OutputPort.portName = "";
                outputContainer.Add(OutputPort);
            }
            else if (category == BTNodeCategory.Decorator)
            {
                OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output,
                    Port.Capacity.Single, typeof(bool));
                OutputPort.portName = "";
                outputContainer.Add(OutputPort);
            }

            RefreshExpandedState();
            RefreshPorts();

            // ===== 双击节点 → 打开实现脚本 =====
            RegisterCallback<MouseDownEvent>(OnDoubleClick);
        }

        /// <summary>从泛型基类中找出 Data 类型，如 BTWait→WaitData</summary>
        private static System.Type GetDataType(System.Type nodeType)
        {
            var baseType = nodeType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType)
                {
                    var def = baseType.GetGenericTypeDefinition();
                    if (def == typeof(BTAction<>) || def == typeof(BTCondition<>) ||
                        def == typeof(BTDecorator<>))
                    {
                        var t = baseType.GetGenericArguments()[0];
                        // object 是占位类型，表示没有实际参数
                        if (t != typeof(object)) return t;
                        return null;
                    }
                }
                baseType = baseType.BaseType;
            }
            return null;  // 非泛型节点（如 BTInverter）
        }

        /// <summary>修改节点显示名（同步更新标题和属性）</summary>
        public void SetDisplayName(string newName)
        {
            DisplayName = newName;
            if (_nameLabel != null)
                _nameLabel.text = newName;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }

        private void OnDoubleClick(MouseDownEvent evt)
        {
            if (evt.clickCount >= 2)
            {
                OpenScript();
                evt.StopPropagation();
            }
        }

        /// <summary>在项目中搜索节点类型对应的脚本文件并打开</summary>
        private void OpenScript()
        {
            string typeName = NodeType.Name;
            //搜该名字MonoScript
            var guids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");

            foreach (var guid in guids)
            {
                //把找到的 guid 转成项目路径
                string path = AssetDatabase.GUIDToAssetPath(guid);
                //加载这个路径的脚本文件
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                //确认脚本里定义的类是不是要找的
                if (script != null && script.GetClass() == NodeType)
                {
                    //在 IDE 里打开它
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }

            Debug.LogWarning($"[BT Editor] 找不到节点脚本: {NodeType.FullName}");
        }

        private static Color GetColor(BTNodeCategory category)
        {
            return category switch
            {
                BTNodeCategory.Composite => new Color(0.3f, 0.5f, 0.8f),
                BTNodeCategory.Decorator => new Color(0.7f, 0.6f, 0.2f),
                BTNodeCategory.Action    => new Color(0.3f, 0.7f, 0.4f),
                BTNodeCategory.Condition => new Color(0.9f, 0.55f, 0.2f),
                _ => Color.gray
            };
        }
    }
}
