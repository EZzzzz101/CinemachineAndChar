using System;
using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 行为树 ScriptableObject 资产
    /// 存储扁平节点列表 + 连线关系 + 根节点 ID
    /// 编辑器负责写，运行时 BehaviorTreeRunner.BuildTree() 负责读
    /// </summary>
    [CreateAssetMenu(menuName = "AI/Behavior Tree", fileName = "NewBehaviorTree")]
    public class BehaviorTreeSO : ScriptableObject
    {
        [SerializeField] private List<NodeEntry> _nodes = new();
        [SerializeField] private string _rootNodeId;
        [SerializeField] private BTBlackboardSO _blackboardAsset;

        public string Description;

        // ===== 编辑器 API =====
        public IReadOnlyList<NodeEntry> Nodes => _nodes;
        public string RootNodeId { get => _rootNodeId; set => _rootNodeId = value; }
        public BTBlackboardSO BlackboardAsset { get => _blackboardAsset; set => _blackboardAsset = value; }

        public void SetNodes(List<NodeEntry> nodes) => _nodes = nodes;
        public void AddNode(NodeEntry node) => _nodes.Add(node);
        public void RemoveNode(string id) => _nodes.RemoveAll(n => n.Id == id);
        public NodeEntry GetNode(string id) => _nodes.Find(n => n.Id == id);

        /// <summary>节点序列化条目 — SO 里的一行数据</summary>
        [Serializable]
        public class NodeEntry
        {
            public string Id;              // 唯一标识（GUID）
            public string TypeName;        // 完整类型名，BuildTree 时反射用
            public Vector2 Position;       // 编辑器画布坐标
            public string JsonData;        // 节点参数（JSON）
            public List<string> ChildIds;  // 子节点 Id 列表（连线关系）
        }
    }
}
