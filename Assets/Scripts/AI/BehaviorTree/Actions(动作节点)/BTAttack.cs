using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct AttackData
    {
        [Tooltip("Animator 中驱动攻击的 Trigger 参数名（所有攻击共用一个，如 Attack）")]
        public string TriggerName;

        [Tooltip("Animator 中选攻击的 Int 参数名（1~N 对应第几个攻击，如 AttackIndex）")]
        public string IndexParamName;

        [Tooltip("各攻击状态短名（如 Attcak1~Attcak6），与 Animator 状态名一致；内部转 hash 匹配 BTAnimationExitNotifier 的退出信号")]
        public string[] StateNames;

        [Tooltip("兜底超时（秒）。超过该时间仍未等到动画退出信号则强制按完成处理，防止动画没接上时永久卡死。0 = 不启用")]
        public float MaxDuration;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：随机挑一个攻击动画播完返回 Success，并按 MonsterAttackConfigSO 结算伤害/音效/特效。
    /// 只用一个 Trigger + 一个 Int，不需要每个攻击各配一个 Bool：
    ///   OnEnter : 随机挑攻击 → SetInteger(AttackIndex, 挑中序号) → SetTrigger(Attack)
    ///              Trigger 触发后自动消耗；AttackIndex 下次进节点会被覆盖 ——
    ///              "最后一次进入谁就自动清理谁"，无残留、无 per-攻击 配置。
    ///   执行    : Running → 等 BTAnimationExitNotifier 对该状态的退出信号 → Success
    ///   伤害    : 读黑板的 MonsterAttackConfigSO，攻击动画播放进度依次越过该段配置的 hitTimes[]
    ///             （归一化时间，支持一段动画多段命中，如 [0.2,0.5,0.8] 打三下）逐次结算
    ///             （复用 AttackHitHelper，和玩家攻击同一套命中逻辑）；
    ///             时机判断整段失效时退出兜底补第一下，保证每次攻击至少造成一次伤害。
    ///   OnExit  : 把 AttackIndex 归零（清残留），防下次 Trigger 误匹配。
    /// </summary>
    [BTNode("攻击", "Action/动画", "随机挑一个攻击动画播完返回 Success；按 SO 的 hitTimes[] 在动画进度上逐段命中结算伤害")]
    public class BTAttack : BTAction<AttackData>
    {
        private int _picked = -1;       // 本次随机挑中的攻击下标
        private int _pickedHash;        // 挑中攻击状态的 shortNameHash（命中时机判断用）
        private bool _triggered;        // 本次进入是否已触发过
        private int _hitIndex;          // 已结算到第几击（支持多段命中）
        private bool _warnedNoConfig;   // 是否已警告过缺少攻击配置（只警告一次）
        private float _startTime;       // 进入时刻，用于超时兜底

        private MonsterAudio _audio;
        private GameObject _telegraphFx;   // 本次攻击的提示闪光实例

        public override void OnEnter(Blackboard bb)
        {
             _audio = bb.Get<MonsterAudio>("_audio");
            _triggered = false;
            _hitIndex = 0;
            _startTime = Time.time;
            _picked = (Data.StateNames == null || Data.StateNames.Length == 0)
                ? -1 : Random.Range(0, Data.StateNames.Length);
            _pickedHash = (_picked >= 0 && !string.IsNullOrEmpty(Data.StateNames[_picked]))
                ? Animator.StringToHash(Data.StateNames[_picked]) : 0;
            // 清掉该状态上次残留的退出信号，避免刚进入就误判完成
            if (_picked >= 0 && TryKey(Data.StateNames[_picked], out var k))
                bb.Set(k, false);

            // 记录本次攻击是否霸体（BossBrain 被打时据此决定是否打断）
            var step = GetStep(bb);
            bb.Set("_superArmor", step != null && step.isSuperArmor);
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null)
            {
                Debug.LogWarning("[BT] BTAttack: 黑板中没有 _animator");
                return BTResult.Failure;
            }
            if (_picked < 0)
            {
                Debug.LogWarning("[BT] BTAttack: 未配置攻击状态，按失败处理");
                return BTResult.Failure;
            }

            // 只触发一次：先选攻击（Int），再扣扳机（Trigger），并播起手音效
            if (!_triggered)
            {
                anim.SetInteger(Data.IndexParamName, _picked + 1);
                anim.SetTrigger(Data.TriggerName);
                _triggered = true;

                var step = GetStep(bb);
                if (step != null)
                {
                    // 起手音（播一次）
                    if (step.attackSound != null && _audio != null)
                        _audio.PlayComboSound(step.attackSound);
                    if (step.voiceClips != null && step.voiceClips.Length > 0 && _audio != null)
                        _audio.PlayComboVoice(step.voiceClips);
                }

                // 起手提示闪光（挂在锁定点骨骼上，跟随动画；第一击命中销毁）
                SpawnTelegraph(bb);
            }

            // 没填状态名 → 防止挂死，按完成处理
            if (!TryKey(Data.StateNames[_picked], out var signalKey))
            {
                Debug.LogWarning("[BT] BTAttack: StateNames 未填，按完成处理");
                return BTResult.Success;
            }

            // 多段命中：把播放进度已达到的命中点全部结算（过渡期源状态 hash 不匹配，不会误判）
            ProcessHits(bb, anim);

            // 动画已退出 → 完成
            if (bb.Get<bool>(signalKey))
            {
                // 兜底：时机判断整段没命中过时，退出补第一下，保证每次攻击至少造成一次伤害
                TryForceFirstHit(bb);
                return BTResult.Success;
            }

            // 兜底：动画没接上 / 退不出时，超过 MaxDuration 强制完成，避免永久卡死
            if (Data.MaxDuration > 0f && Time.time - _startTime >= Data.MaxDuration)
            {
                Debug.LogWarning($"[BT] BTAttack: 等待 {Data.StateNames[_picked]} 退出超时({Data.MaxDuration:F1}s)，按完成处理");
                TryForceFirstHit(bb);
                return BTResult.Success;
            }

            return BTResult.Running;
        }

        public override void OnExit(Blackboard bb)
        {
            DespawnTelegraph();   // 兜底清理提示闪光（没命中时也清掉）
            bb.Set("_superArmor", false);   // 攻击结束/被打断，解除霸体

            if (!_triggered) return;   // 未触发过（失败）不动 Animator

            Animator anim = bb.Get<Animator>("_animator");
            if (anim != null)
                anim.SetInteger(Data.IndexParamName, 0);   // 归零，防下次 Trigger 误匹配
        }

        /// <summary>多段命中：把攻击动画播放进度已经达到的命中点逐次结算（每段攻击可配多个 hitTimes）</summary>
        private void ProcessHits(Blackboard bb, Animator anim)
        {
            var step = GetStep(bb);
            if (step == null) return;

            var times = step.hitTimes;
            if (times == null || times.Length == 0) times = DefaultHitTimes;   // 未配置 → 单次 0.3

            // 时机门：必须在挑中的攻击状态内（过渡期源状态 hash 不匹配，不会误判）
            var info = anim != null ? anim.GetCurrentAnimatorStateInfo(0) : default;
            if (info.shortNameHash != _pickedHash) return;

            float t = info.normalizedTime;
            while (_hitIndex < times.Length && t >= times[_hitIndex])
            {
                // 挥空音：每个命中点播一次（不论是否打中）
                if (step.swingSound != null && _audio != null)
                    _audio.PlaySwingSound(step.swingSound, step.swingVolume, step.swingSpatialBlend);

                DealHit(bb, step, _hitIndex);
                _hitIndex++;

                // 多段命中：还有后续命中 → 重新起手预警（闪光+音效）；最后一段 → 关闭
                if (_hitIndex < times.Length)
                {
                    DespawnTelegraph();
                    SpawnTelegraph(bb);
                }
                else
                {
                    DespawnTelegraph();
                }
            }
        }

        /// <summary>兜底：时机判断整段失效时（如状态 hash 没匹配上），退出补第一下，保证至少一次伤害</summary>
        private void TryForceFirstHit(Blackboard bb)
        {
            if (_hitIndex > 0) return;
            var step = GetStep(bb);
            if (step == null) return;
            DealHit(bb, step, 0);
            _hitIndex = 1;
        }

        /// <summary>按 SO 段配置结算单次命中：复用 AttackHitHelper（和玩家 ATK 同一套命中逻辑）</summary>
        private void DealHit(Blackboard bb, MonsterAttackStepData step, int hitIndex)
        {
            Transform self = bb.Get<Transform>("_transform");
            if (self == null) return;

            var config = bb.Get<MonsterAttackConfigSO>("_attackConfig");
            int mask = config != null && config.targetLayer.value != 0
                ? config.targetLayer.value : Physics.AllLayers;

            Vector3 origin = self.position + Vector3.up * (step.attackUpOffset > 0f ? step.attackUpOffset : 1f);
            float range = step.attackRange > 0f ? step.attackRange : 2.5f;
            float angle = step.attackAngle > 0f ? step.attackAngle : 80f;

            // 只结算伤害；受击反馈（命中音/震屏/顿帧/特效/动画）由被击者自己播放（闪避时不播）
            AttackHitHelper.DealDamage(
                origin, self.forward, range, angle, mask,
                step.damage, self.gameObject, self);
        }

        /// <summary>起手生成提示闪光（挂在头部骨骼）+ 播预警音效；多段命中时每段重新触发</summary>
        private void SpawnTelegraph(Blackboard bb)
        {
            if (_telegraphFx != null) return;
            var config = bb.Get<MonsterAttackConfigSO>("_attackConfig");
            if (config == null) return;

            // 预警音效（所有攻击共用，起手/每段命中前播放）
            if (config.telegraphSound != null && _audio != null)
                _audio.PlayComboSound(config.telegraphSound);

            // 预警特效（挂在头部骨骼）
            if (config.telegraphVfxPrefab == null) return;

            Transform self = bb.Get<Transform>("_transform");
            if (self == null) return;

            var lockOn = self.GetComponent<LockOnTarget>();
            // 预警闪光挂在头部骨骼（自动找 Head，或手动拖 telegraphPoint）
            Transform anchor = lockOn != null ? lockOn.TelegraphPointTransform : self;
            if (anchor == self)
                Debug.LogWarning($"[BTAttack] {self.name} 预警闪光点未生效（LockOnTarget.telegraphPoint 为空且自动找骨失败）→ 特效生成在根节点(脚底)。请把头部骨骼拖到 LockOnTarget.telegraphPoint");
            _telegraphFx = VFXPool.Spawn(config.telegraphVfxPrefab, anchor.position, anchor.rotation, anchor, 0f);
            if (_telegraphFx == null) return;
            _telegraphFx.transform.localPosition = Vector3.zero;
        }

        private void DespawnTelegraph()
        {
            if (_telegraphFx == null) return;
            VFXPool.Despawn(_telegraphFx);
            _telegraphFx = null;
        }

        /// <summary>未配置 hitTimes 时的兜底：动画播到 30% 打一下</summary>
        private static readonly float[] DefaultHitTimes = { 0.3f };

        /// <summary>从黑板读 MonsterAttackConfigSO，取当前挑中攻击对应的段配置（索引对齐，越界钳到末尾）</summary>
        private MonsterAttackStepData GetStep(Blackboard bb)
        {
            var config = bb.Get<MonsterAttackConfigSO>("_attackConfig");
            var steps = config != null ? config.steps : null;
            if (steps == null || steps.Length == 0 || _picked < 0)
            {
                // 只警告一次：缺少配置时攻击只播动画不结算伤害，方便排查
                if (!_warnedNoConfig)
                {
                    _warnedNoConfig = true;
                    Debug.LogWarning("[BT] BTAttack: 黑板没有 MonsterAttackConfigSO(_attackConfig) 或 steps 为空，本次攻击只播动画不结算伤害");
                }
                return null;
            }
            return steps[Mathf.Min(_picked, steps.Length - 1)];
        }

        /// <summary>状态短名 → 信号键（= Animator.StringToHash，与退出脚本的 shortNameHash 一致）</summary>
        private bool TryKey(string s, out string key)        {
            if (string.IsNullOrEmpty(s))
            {
                key = null;
                return false;
            }
            key = Animator.StringToHash(s).ToString();
            return true;
        }
    }
}
