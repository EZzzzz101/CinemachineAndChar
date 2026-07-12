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

	#region 测试 — 怪物AI：动画驱动（Root Motion 位移）
	    // 树结构对照：
	    //
	    // Repeater(无限) [                              ← 顶层死循环
	    //     Selector [                                ← 优先级决策
	    //         ─── 分支1：已在范围 → 停步 ───
	    //         Sequence [
	    //             BTHasTarget
	    //             BTIsTargetInRange(1.5m)           ← 走到1.5m内了
	    //             BTSetAnimatorBool(false)          ← 停动画，站住
	    //             BTWait(0.3s)
	    //         ]
	    //         ─── 分支2：有目标 → 追击 ───
	    //         Sequence [
	    //             BTFindNearestTarget(15m)           ← 发现玩家 → 写入黑板
	    //             BTFaceTarget                       ← 转向，面朝玩家
	    //             BTSetAnimatorBool(true)            ← 播走路动画！Root Motion 推位置
	    //             BTWait(0.2s)                       ← 走0.2秒再重新判断
	    //         ]
	    //         ─── 分支3：没目标 → 发呆 ───
	    //         Sequence [
	    //             BTSetAnimatorBool(false)           ← 站住
	    //             BTWait(0.5s)
	    //         ]
	    //     ]
	    // ]
	    //
	    // 关键：没有 BTMoveTowards 代码位移 — 移动靠动画 clip 的 Root Motion

	    [ContextMenu("创建怪物行为树(动画驱动)")]
	    private void CreateTestTree()
	    {
	        var treeAsset = ScriptableObject.CreateInstance<BehaviorTreeSO>();
	        treeAsset.RootNodeId = "1";

	        treeAsset.SetNodes(new List<BehaviorTreeSO.NodeEntry>
	        {
	            new BehaviorTreeSO.NodeEntry {
	                Id = "1", TypeName = typeof(BTSelector).FullName,
	                Position = Vector2.zero, JsonData = "",
	                ChildIds = new List<string> { "2", "5", "8" }
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "2", TypeName = typeof(BTSequence).FullName,
	                Position = new Vector2(150, -80), JsonData = "",
	                ChildIds = new List<string> { "3", "4", "0a" }
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "3", TypeName = typeof(BTHasTarget).FullName,
	                Position = new Vector2(300, -110), JsonData = "",
	                ChildIds = new List<string>()
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "4", TypeName = typeof(BTIsTargetInRange).FullName,
	                Position = new Vector2(300, -80),
	                JsonData = "{\"Range\":1.5,\"TargetKey\":\"target\"}",
	                ChildIds = new List<string>()
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "0a", TypeName = typeof(BTSetAnimatorBool).FullName,
	                Position = new Vector2(300, -50),
	                JsonData = "{\"ParameterName\":\"IsMoving\",\"Value\":false}",
	                ChildIds = new List<string>()
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "5", TypeName = typeof(BTSequence).FullName,
	                Position = new Vector2(150, 0), JsonData = "",
	                ChildIds = new List<string> { "6", "7", "0b" }
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "6", TypeName = typeof(BTFindNearestTarget).FullName,
	                Position = new Vector2(300, 20),
	                JsonData = "{\"MaxRange\":15.0,\"TargetTeam\":0,\"TargetKey\":\"target\"}",
	                ChildIds = new List<string>()
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "7", TypeName = typeof(BTFaceTarget).FullName,
	                Position = new Vector2(300, 50),
	                JsonData = "{\"RotateSpeed\":720.0,\"TargetKey\":\"target\",\"AngleThreshold\":5.0}",
	                ChildIds = new List<string>()
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "0b", TypeName = typeof(BTSetAnimatorBool).FullName,
	                Position = new Vector2(300, 80),
	                JsonData = "{\"ParameterName\":\"IsMoving\",\"Value\":true}",
	                ChildIds = new List<string>()
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "8", TypeName = typeof(BTSequence).FullName,
	                Position = new Vector2(150, 80), JsonData = "",
	                ChildIds = new List<string> { "0c" }
	            },
	            new BehaviorTreeSO.NodeEntry {
	                Id = "0c", TypeName = typeof(BTSetAnimatorBool).FullName,
	                Position = new Vector2(300, 130),
	                JsonData = "{\"ParameterName\":\"IsMoving\",\"Value\":false}",
	                ChildIds = new List<string>()
	            }
	        });

	        _treeAsset = treeAsset;
	        Debug.Log("[BT] 怪物行为树已创建（11节点，动画驱动，无Wait无Repeater）");
	    }
	#endregion
    }
}