using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 行为树执行器 — 挂载在怪物 GameObject 上
    /// Awake 时从 SO 构建节点树，Update 时定时 Tick
    /// </summary>
    public class BehaviorTreeRunner : MonoBehaviour
    {
        [SerializeField]private BehaviorTreeSO _treeAsset; //行为树SO
        [SerializeField]private float _tickInterval =0.1f; //Tick间隔
        [SerializeField]private bool _tickEveryFrame =false;//按帧Tick

        //黑板数据
        public Blackboard Blackboard{get;private set;}
        //根节点
        public BTNode RootNode{get;private set;}
        //对外暴露的行为树SO
        public BehaviorTreeSO TreeAsset =>_treeAsset;

        private Dictionary<string,BTNode> _nodeMap;
        private float _tickTimer;

        void Awake()
        {
            if (_treeAsset != null)
                BuildTree();
        }

        void Update()
        {
            if(RootNode==null) return;
            if (_tickEveryFrame)
            {
                RootNode.Execute(Blackboard);
            }
            else
            {
                _tickTimer+=Time.deltaTime;
                if (_tickTimer >= _tickInterval)
                {
                    _tickTimer=0f;
                    RootNode.Execute(Blackboard);
                }
            }
        }
        void OnDestroy()
        {
            Blackboard?.Clear();
        }

        #region 从SO构建行为树
        public void BuildTree()
        {
             if (_treeAsset == null)
            {
                Debug.LogError($"[BT] {gameObject.name}: 没有指定 BehaviorTreeSO");
                return;
            }

            //1.初始化黑板
            Blackboard = new Blackboard(_treeAsset.BlackboardAsset,gameObject);

            //2.创建节点实例
            _nodeMap=new();
            foreach(var entry in _treeAsset.Nodes)
            {
                BTNode node = CreateNode(entry);
                if (node != null)
                {
                    //根据Guid确定节点
                    node.Guid =entry.Id;
                    _nodeMap[entry.Id] =node;
                }
            }

            //3.连线：建立父子关系
            foreach (var entry in _treeAsset.Nodes)
            {
                if(entry.ChildIds==null || entry.ChildIds.Count==0)
                    continue;
                if (!_nodeMap.TryGetValue(entry.Id, out var parent))
                    continue;
                // 遍历这个父节点的所有子节点 ID
                foreach (var childId in entry.ChildIds)
                {
                    if (_nodeMap.TryGetValue(childId, out var child))
                        WireChild(parent, child);   // 挂上去
                }
            }

            //4.确定根节点
            if (!string.IsNullOrEmpty(_treeAsset.RootNodeId))
            {
                _nodeMap.TryGetValue(_treeAsset.RootNodeId, out var root);
                RootNode = root;
            }
            Debug.Log($"[BT] {gameObject.name}: 构建完成，共 {_nodeMap.Count} 个节点");
        }
        #endregion

        /// <summary>
        /// 根据 TypeName 反射创建节点实例，并反序列化参数
        /// </summary>
        private BTNode CreateNode(BehaviorTreeSO.NodeEntry entry)
        {
            if(string.IsNullOrEmpty(entry.TypeName))
                return null;
            Type type = Type.GetType(entry.TypeName);
            if (type == null)
            {
                Debug.LogError($"[BT] 找不到类型: {entry.TypeName}");
                return null;
            }

            // 1.反射创建对象
            object tempObj = Activator.CreateInstance(type);
            // 2.安全转基类
            BTNode node = tempObj as BTNode;
            if(node==null) return null;

            // 反序列化参数（如果节点有 DeserializeData 方法）
            if (!string.IsNullOrEmpty(entry.JsonData))
            {
                var deserializeMethod = type.GetMethod("DeserializeData",
                    BindingFlags.Public | BindingFlags.Instance);
                deserializeMethod?.Invoke(node, new object[] { entry.JsonData });
            }

            return node;
        }

        /// <summary>
        /// 根据节点类型建立父子连线
        /// </summary>
        private void WireChild(BTNode parent,BTNode child)
        {
            //组合节点
            if (parent is BTComposite composite)
                composite.AddChild(child);
            //修饰节点
            else if (parent is BTDecorator decorator)
                decorator.SetChild(child);
            // BTAction / BTCondition 是叶子，不接受子节点，忽略
        }
    }
}
