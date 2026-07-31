using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct SetFaceTargetData
    {
        [Tooltip("写入的面向状态：true = 开始面向角色，false = 停止面向")]
        public bool Value;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：写入"面向角色"标志，供 BossMotor 每帧读取并平滑转向。
    /// 只负责写开关（一帧完成），不负责转向——转向由 Motor 每帧执行。
    ///
    /// 与获取目标的 BTFindNearestTarget 分离：目标信息时刻需要，面向非时刻需要，
    /// 由行为树决定何时开/关。离开面向状态时记得写 false，否则 Boss 会一直面向。
    /// </summary>
    [BTNode("面向角色", "Action/移动", "写 _faceTarget 标志：true=面向角色，false=停止面向（转向由 Motor 每帧执行）")]
    public class BTSetFaceTarget : BTAction<SetFaceTargetData>
    {
        /// <summary>黑板面向标志键名（与 BossMotor.FaceTargetKey 默认值一致）</summary>
        public const string FaceTargetKey = "_faceTarget";

        protected override BTResult OnExecute(Blackboard bb)
        {
            bb.Set(FaceTargetKey, Data.Value);
            return BTResult.Success;
        }
    }
}
