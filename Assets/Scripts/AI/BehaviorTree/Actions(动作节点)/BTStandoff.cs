using UnityEngine;

// 供下方全局命名空间的 partial BossMotor 段解析 Blackboard 类型
using AI.BehaviourTree;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct StandoffData
    {
        [Tooltip("随机兜底时长下限(秒)，进入时取 [MinDuration, MaxDuration] 随机值")]
        public float MinDuration;

        [Tooltip("随机兜底时长上限(秒)")]
        public float MaxDuration;

        [Tooltip("受击即退出对峙（进对峙时记录 _hpRatio，血量下降=被打，重置状态）")]
        public bool ExitOnHit;

        [Tooltip("太近阈值：玩家距离 < 此值 → 后退主导")]
        public float BackAwayDist;

        [Tooltip("太远阈值：玩家距离 > 此值 → 前压；[BackAwayDist, KeepDist] 内横移绕圈")]
        public float KeepDist;

        [Tooltip("决策反应间隔(秒)：每过这么久才读一次玩家距离做决定，制造反应时间，避免鬼畜。<=0 用默认 0.4")]
        public float ReactInterval;

        [Tooltip("对峙状态 Bool 参数名(如 IsSolo)，进入置 true、退出置 false。留空用默认 IsSolo")]
        public string StateBoolName;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：对峙(持续绕圈周旋)
    ///   进入时随机横移方向 + 随机兜底时长；每 ReactInterval 按玩家距离带更新目标参数
    ///   (_standoffX/_standoffY 写黑板)，BossMotor partial 段每帧平滑驱动 2D 树。
    ///   太近后退 / 舒适带横移 / 太远前压。退出：随机时长到 / 受击(血量下降)。
    /// </summary>
    [BTNode("对峙", "Action/动画", "进入 MoveSlow 写 SpeedX/SpeedY：太近后退、舒适带横移、太远前压；随机时长/受击结束")]
    public class BTStandoff : BTAction<StandoffData>
    {
        public const string StandoffKey = "_standoff";  // int 0/1 开关
        public const string TargetXKey  = "_standoffX"; // float 目标 SpeedX
        public const string TargetYKey  = "_standoffY"; // float 目标 SpeedY

        // 动画参数名（与 MoveSlow 的 2D 树一致；如不同改这里）
        private const string XParam    = "SpeedX";
        private const string YParam    = "SpeedY";

        // 对峙状态标记 Bool 名（默认 IsSolo；留空用默认）
        private string BoolName => string.IsNullOrEmpty(Data.StateBoolName) ? "IsSolo" : Data.StateBoolName;

        private float _startTime;
        private float _endTime;        // 随机兜底结束时刻
        private float _enterHpRatio;   // 进对峙时血量，受击退出用
        private int _strafe;           // 本次横移方向 ±1
        private float _lastReactTime;

        public override void OnEnter(Blackboard bb)
        {
            _startTime = Time.time;
            float min = Mathf.Max(Data.MinDuration, 0f);
            float max = Data.MaxDuration > min ? Data.MaxDuration : min + 1f;
            _endTime = _startTime + Random.Range(min, max); // 随机兜底时长

            _strafe  = Random.value < 0.5f ? 1 : -1;
            _enterHpRatio = bb.Get<float>("_hpRatio");
            _lastReactTime = Time.time; // 先横移，过一个反应间隔再按距离调整

            bb.Set(StandoffKey, 1);
            bb.Set(TargetXKey, (float)_strafe);
            bb.Set(TargetYKey, 0f);

            Animator anim = bb.Get<Animator>("_animator");
            if (anim != null)
                anim.SetBool(BoolName, true); // 进入对峙：状态标记置 true
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            Transform self   = bb.Get<Transform>("_transform");
            Transform target = bb.Get<Transform>("target");
            if (self == null || target == null)
                return BTResult.Failure;

            // ── 退出 ──
            // 受击：血量比进入时下降 → 被打，退出（将来受击叶子以更高优先级接管）
            if (Data.ExitOnHit && bb.Get<float>("_hpRatio") < _enterHpRatio - 0.001f)
                return BTResult.Success;
            // 随机兜底时长
            if (Time.time >= _endTime)
                return BTResult.Success;

            // ── 决策反应：每 ReactInterval 读一次玩家距离，更新目标参数 ──
            float react = Data.ReactInterval > 0f ? Data.ReactInterval : 0.4f;
            if (Time.time - _lastReactTime >= react)
            {
                Vector3 toTarget = target.position - self.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                Vector2 t = ComputeTarget(dist);
                bb.Set(TargetXKey, t.x);
                bb.Set(TargetYKey, t.y);

                _lastReactTime = Time.time;
            }

            return BTResult.Running;
        }

        /// <summary>距离带 → 目标 2D 参数（后退主导 / 舒适带横移 / 前压）</summary>
        private Vector2 ComputeTarget(float dist)
        {
            float backAway = Data.BackAwayDist > 0f ? Data.BackAwayDist : 2.5f;
            float keep     = Data.KeepDist > backAway ? Data.KeepDist : backAway + 2.5f;

            if (dist < backAway) return new Vector2(_strafe * 0.2f, -0.9f); // 后退主导
            if (dist > keep)     return new Vector2(_strafe * 0.3f,  0.9f); // 前压
            return               new Vector2(_strafe, 0f);                 // 舒适带横移
        }

        public override void OnExit(Blackboard bb)
        {
            bb.Set(StandoffKey, 0);
            bb.Set(TargetXKey, 0f);
            bb.Set(TargetYKey, 0f);

            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null) return;
            anim.SetFloat(XParam, 0f);      // 2D 参数归零
            anim.SetFloat(YParam, 0f);
            anim.SetBool(BoolName, false);  // 退出对峙：状态标记置 false
        }
    }
}

// ========== 对峙专属 Motor 执行段（partial BossMotor，写在对峙脚本里保持清爽） ==========
// BossMotor 声明为 partial。对峙需要转向 + 平滑驱动 2D 参数，所以连上 BossMotor：
//   每帧读黑板 _standoff，非 0 就面向玩家（复用主体私有 RotateToTarget，幂等），
//   并把 2D 参数向黑板的"目标值"阻尼平滑写入 —— 配合节点的反应间隔形成"反应时间、不鬼畜"。
// 想加对峙专属逻辑（音效/粒子/渐入渐出）就在这个方法里加。
public partial class BossMotor
{
    [Header("对峙(周旋)")]
    [Tooltip("2D 参数平滑阻尼(秒)，SpeedX/SpeedY 渐变不跳变")]
    public float StandoffDampTime = 0.3f;

    private void UpdateStandoff(Blackboard bb)
    {
        if (bb.Get<int>(AI.BehaviourTree.BTStandoff.StandoffKey) == 0) return;

        RotateToTarget(bb); // 转向面向玩家

        if (_animator == null) return;
        float tx = bb.Get<float>(AI.BehaviourTree.BTStandoff.TargetXKey);
        float ty = bb.Get<float>(AI.BehaviourTree.BTStandoff.TargetYKey);
        float damp = StandoffDampTime > 0f ? StandoffDampTime : 0f;
        if (damp > 0f)
        {
            _animator.SetFloat("SpeedX", tx, damp, Time.deltaTime);
            _animator.SetFloat("SpeedY", ty, damp, Time.deltaTime);
        }
        else
        {
            _animator.SetFloat("SpeedX", tx);
            _animator.SetFloat("SpeedY", ty);
        }
        _animator.SetBool("IsMoving", true);
    }
}
