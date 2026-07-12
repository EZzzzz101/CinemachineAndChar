# 角色物理属性 + 战斗碰撞检测 — 实现计划

> 创建日期: 2026-06-27  
> 参考项目: `E:\zzzdemo-source-code-master\zzzdemo-source-code-master`

---

## 当前状态 vs 目标状态

| 方面 | 当前（CinemachineAndChar） | 目标（参考 zzzdemo） |
|------|--------------------------|---------------------|
| 移动 | `transform.position += Animator.deltaPosition` | `CharacterController.Move()` |
| 重力 | 无 | 自定义累积重力 -9m/s² |
| 地面检测 | 无 | `Physics.CheckSphere` |
| 命中检测 | ATK() 只震屏+顿帧 | Overlap 重叠查询（OverlapSphere + 角度过滤） |
| 敌人 | 只有 LockOnTarget，无血量 | 架势/HP 双层受击系统 |
| 伤害传递 | 无 | EventBus 事件驱动 |

---

## 命中检测方案：三种方式对比

Unity 里做命中检测有三种方式，都属于 **Physics Queries（物理查询）** 体系：

```
  Overlap 重叠查询              Cast 扫掠查询             Collision Callback 碰撞回调
  ┌──────────────────┐    ┌────────────────────┐    ┌────────────────────┐
  │  此刻此地，        │    │  从A到B这段路，      │    │  挂个Collider，     │
  │  谁跟我重叠？      │    │  碰到了谁？          │    │  等引擎通知我        │
  │                   │    │                    │    │                    │
  │     ╔═══╗         │    │  🗡️A═══════🗡️B     │    │   🗡️ 挥过去        │
  │     ║   ║         │    │  ═══扫掠路径═══▶    │    │    ═══▶            │
  │     ╚═══╝         │    │  ║        ║        │    │    等FixedUpdate    │
  │      👹 ←命中      │    │ 👹  👹  ←路径上    │    │    触发             │
  │                   │    │  两个都中           │    │    OnTriggerEnter   │
  └──────────────────┘    └────────────────────┘    └────────────────────┘
  查"此刻的一个点"           查"刚刚走过的整条线"         等引擎自动通知
```

| | Overlap | Cast | Collision Callback |
|---|---|---|---|
| **Unity API** | `Physics.OverlapSphere` `OverlapCapsule` `OverlapBox` | `Physics.SphereCast` `CapsuleCast` `BoxCast` `Raycast` | `OnTriggerEnter` `OnCollisionEnter` |
| **你控制时机** | ✅ 主动调用 | ✅ 主动调用 | ❌ 引擎决定 |
| **会丢刀** | 不会 | 不会 | 会（高速挥砍穿透） |
| **需要 Rigidbody** | 不需要 | 不需要 | 需要 |
| **查什么** | 一个位置 | 一段路径 | 持续区域 |

**本项目选用 Overlap**，理由：
- 动画 Keyframe Event 触发，时机精确
- 不需要 Rigidbody，跟 CharacterController 不冲突
- OverlapSphere 抓一圈 + 角度过滤出扇形 → 范围内全部命中

> 注意：Unity 没有内置"扇形 Overlap"，所以用 **OverlapSphere 全抓 → Vector3.Angle 过滤出锥形区域** 两步实现。

---

## Phase 1: 物理基础 — CharacterController + 重力 + 地面检测

**目标**: 将 `transform.position +=` 替换为 `CharacterController.Move()`，添加重力和地面检测

**修改文件**: `Assets/Scripts/Character/PlayerController.cs`

**参考代码**: `zzzdemo/Assets/Scripts/Character/Base/CharacterMoveControllerBase.cs`

### 步骤

- [ ] **1.1** 在 `安比.prefab` 上添加 `CharacterController` 组件  
  Height≈1.8, Radius≈0.35, Center(0, 0.9, 0), SlopeLimit=45, StepOffset=0.3

- [ ] **1.2** 在 PlayerController 中添加字段:
  ```csharp
  [Header("重力")]
  [SerializeField] private float characterGravity = -9f;
  [SerializeField] private float maxVerticalSpeed = 20f;
  [SerializeField] private float minVerticalSpeed = -3f;
  private float verticalSpeed;
  private Vector3 verticalVelocity;

  [Header("地面检测")]
  [SerializeField] private float groundDetectionRadius = 0.2f;
  [SerializeField] private float groundDetectionOffset = 0.1f;
  [SerializeField] private LayerMask whatIsGround;
  private bool isOnGround;
  private float fallOutDeltaTimer;
  private const float FallOutTimer = 0.2f; // 土狼时间

  [Header("斜坡")]
  [SerializeField] private float slopeDetectionLength = 1f;

  [Header("位移倍率")]
  [Range(0.2f, 100)][SerializeField] private float moveMult = 1f;
  [Range(0.2f, 60)][SerializeField] private float dodgeMult = 2f;

  private CharacterController _characterController;
  ```

- [ ] **1.3** `Awake()` 中添加:
  ```csharp
  _characterController = GetComponent<CharacterController>();
  fallOutDeltaTimer = FallOutTimer;
  ```

- [ ] **1.4** 修改 `OnAnimatorMove()`:
  ```csharp
  void OnAnimatorMove()
  {
      // 原来: transform.position += Animator.deltaPosition;
      Animator.ApplyBuiltinRootMotion();
      UpdateCharacterVelocity(Animator.deltaPosition);
  }
  ```

- [ ] **1.5** `Update()` 开头添加:
  ```csharp
  GroundDetection();
  UpdateCharacterGravity();
  UpdateVerticalVelocity();
  ```

- [ ] **1.6** 添加 5 个方法（从 zzzdemo 移植）:
  - `GroundDetection()` — `Physics.CheckSphere` 脚下检测
  - `UpdateCharacterGravity()` — 累积 `verticalSpeed += gravity * dt`，离地土狼时间 0.2s
  - `UpdateVerticalVelocity()` — `_characterController.Move(verticalVelocity * dt)`
  - `ResetVelocityOnSlope()` — `Physics.Raycast` 向下 + `Vector3.ProjectOnPlane`
  - `UpdateCharacterVelocity()` — `_characterController.Move(rootMotion * mult * dt)`

- [ ] **1.7** 创建 `Ground` Layer，场景地板设为此 Layer，PlayerController 设置 `whatIsGround`

---

## Phase 2: 修复 EventBus + 添加战斗数据

### 步骤

- [ ] **2.1** 修复 `EventBus.cs`  
  `private void Subscribe` → `public void Subscribe`（目前是 bug，外部无法订阅）

- [ ] **2.2** 在 `ComboStepData.cs` 添加字段:
  ```csharp
  [Header("伤害判定")]
  public float damage = 10f;                // 每次 ATK tick 伤害
  public float attackRange = 3f;            // OverlapSphere 半径
  public float attackAngle = 80f;           // 前方锥形角度（全角）
  public float attackUpOffset = 0.7f;       // 判定起点垂直偏移（胸口高度）
  public LayerMask enemyLayer;              // 敌人层
  ```

- [ ] **2.3** 新建 `Assets/Scripts/Character/Events/CombatEventData.cs`:
  ```csharp
  public class CombatEventData
  {
      public float damage;
      public Transform attacker;
      public Transform bearer;
  }
  ```

---

## Phase 3: ATK() 添加命中检测（Overlap 重叠查询）

**修改文件**: `Assets/Scripts/Character/Core/Combo/States/ATKingState.cs`

**方案**: OverlapSphere 抓范围内全部敌人 → Angle 过滤出前方锥形 → 全部命中

```
  攻击判定流程:

  Step 1: OverlapSphere              Step 2: 角度过滤
  ┌─────────────────┐                       ╱
  │    ╭─────╮      │                      ╱
  │   ╱       ╲    │                  ╱  ← 只保留锥内的
  │  │   👹👹   │  │                 ╱  ✅✅
  │  │  👤 →   │   │              👤 →
  │  │   👹     │   │                 ╲
  │   ╲       ╱    │                  ╲  ❌ ← 锥外的丢弃
  │    ╰─────╯     │                   ╲
  └─────────────────┘
  全部在范围内的: 3个                  命中: 2个
```

### 步骤

- [ ] **3.1** 添加 `DetectHits(step)` 方法:
  ```csharp
  private List<Transform> DetectHits(ComboStepData step)
  {
      var hits = new List<Transform>();
      Vector3 origin = Owner.transform.position + Vector3.up * step.attackUpOffset;

      // ① OverlapSphere — 球形抓全部
      Collider[] cols = Physics.OverlapSphere(origin, step.attackRange, step.enemyLayer);
      foreach (var col in cols)
      {
          Vector3 toTarget = col.transform.position - origin;
          toTarget.y = 0; // 忽略高度差

          // ② 角度过滤 — 只保留前方锥形内的
          float angle = Vector3.Angle(Owner.transform.forward, toTarget.normalized);
          if (angle <= step.attackAngle * 0.5f && toTarget.magnitude <= step.attackRange)
          {
              hits.Add(col.transform);
          }
      }
      return hits;
  }
  ```

- [ ] **3.2** 修改 `ATK()` 方法:
  ```csharp
  public void ATK()
  {
      var step = ResuableData.CurrentStep;
      int idx = ResuableData.currentATKIndex;

      // ① 震屏（保持不变）
      if (step.shakeForceList != null && idx < step.shakeForceList.Length)
      {
          float force = step.shakeForceList[idx];
          if (force > 0f) CameraShake.Instance.TriggerShake(force);
      }

      // ② 命中检测 — Overlap 重叠查询（新增）
      var hitTargets = DetectHits(step);
      foreach (var target in hitTargets)
      {
          EventBus.Instance.Emit("OnDamageDealt", new CombatEventData {
              damage = step.damage, attacker = Owner.transform, bearer = target
          });

          if (step.hitVfxPrefab != null)
          {
              Vector3 hitPos = (Owner.transform.position + target.position) * 0.5f + Vector3.up;
              Object.Instantiate(step.hitVfxPrefab, hitPos, Quaternion.identity);
          }
      }

      // ③ 顿帧（保持不变）
      if (step.hitPauseList != null && idx < step.hitPauseList.Length)
      {
          float duration = step.hitPauseList[idx];
          if (duration > 0f) HitPauseManager.Instance.Trigger(duration, step.hitPauseScale);
      }

      ResuableData.currentATKIndex++;
  }
  ```

---

## Phase 4: 敌人血量 + 受击系统

**新建文件**: `Assets/Scripts/Enemy/EnemyHealth.cs`

### 受击逻辑

```
  受到伤害
     ↓
  有架势(Posture > 0)？
   ├─ 是 → 扣架势 → 播放格挡动画(Parry)
   └─ 否 → 扣HP → 播放受击动画(Hit)
                ↓
              HP ≤ 0？
              └─ 是 → 死亡(Death) + 关闭 LockOnTarget
```

### 步骤

- [ ] **4.1** 创建 `EnemyHealth.cs`:
  ```csharp
  [RequireComponent(typeof(Animator))]
  public class EnemyHealth : MonoBehaviour
  {
      [Header("属性")]
      [SerializeField] private float maxHP = 100f;
      [SerializeField] private float maxPosture = 50f;
      [SerializeField] private float postureRecoverDelay = 3f;
      [SerializeField] private float postureRecoverRate = 10f;

      [Header("受击动画")]
      [SerializeField] private string lightHitAnim = "Hit";
      [SerializeField] private string parryAnim = "Parry";
      [SerializeField] private string deathAnim = "Death";
      [SerializeField] private float hitTransitionTime = 0.1f;

      private float _currentHP, _currentPosture, _postureRecoverTimer;
      private Animator _animator;
      private LockOnTarget _lockOnTarget;
      public bool IsDead { get; private set; }

      void Awake()
      {
          _animator = GetComponent<Animator>();
          _lockOnTarget = GetComponent<LockOnTarget>();
          _currentHP = maxHP;
          _currentPosture = maxPosture;
      }

      void OnEnable()  => EventBus.Instance.Subscribe("OnDamageDealt", OnDamageDealt);
      void OnDisable() => EventBus.Instance.Unsubscribe("OnDamageDealt", OnDamageDealt);

      void Update()
      {
          // 架势恢复
          if (_currentPosture < maxPosture)
          {
              _postureRecoverTimer += Time.deltaTime;
              if (_postureRecoverTimer >= postureRecoverDelay)
                  _currentPosture = Mathf.Min(
                      _currentPosture + postureRecoverRate * Time.deltaTime, maxPosture);
          }
      }

      private void OnDamageDealt(object eventDataObj)
      {
          if (eventDataObj is not CombatEventData data) return;
          if (data.bearer != transform) return;
          if (IsDead) return;

          _postureRecoverTimer = 0f;

          if (_currentPosture > 0)
          {
              _currentPosture -= data.damage;
              string anim = _currentPosture <= 0 ? lightHitAnim : parryAnim;
              _animator.CrossFadeInFixedTime(anim, hitTransitionTime);
          }
          else
          {
              _currentHP -= data.damage;
              _animator.CrossFadeInFixedTime(lightHitAnim, hitTransitionTime);
          }

          if (_currentHP <= 0) Die();
      }

      private void Die()
      {
          IsDead = true;
          _animator.CrossFadeInFixedTime(deathAnim, hitTransitionTime);
          if (_lockOnTarget != null) _lockOnTarget.enabled = false;
      }
  }
  ```

- [ ] **4.2** 在怪兽 prefab 上添加 EnemyHealth 组件

---

## Phase 5: 编辑器配置 + 测试

- [ ] **5.1** 创建 `Enemy` Layer，怪兽设置为此 Layer
- [ ] **5.2** `AnBiComboConfig.asset` — 5个 steps 设置 damage / attackRange / attackAngle / enemyLayer
- [ ] **5.3** 场景验证: 重力→锁定→攻击→Overlap命中→伤害事件→受击反应

---

## 文件变更清单

| 文件 | 操作 | Phase |
|------|------|-------|
| `Assets/Prefabs/安比.prefab` | Editor: 添加 CharacterController | 1 |
| `Assets/Scripts/Character/PlayerController.cs` | 修改: 重力+地面+CC移动 | 1 |
| `Assets/Scripts/Character/Events/EventBus.cs` | 修复: Subscribe 改 public | 2 |
| `Assets/Scripts/Character/Core/Combo/ComboStepData.cs` | 修改: 添加战斗字段 | 2 |
| `Assets/Scripts/Character/Events/CombatEventData.cs` | 新建 | 2 |
| `Assets/Scripts/Character/Core/Combo/States/ATKingState.cs` | 修改: ATK() 加 Overlap 命中检测 | 3 |
| `Assets/Scripts/Enemy/EnemyHealth.cs` | 新建 | 4 |
| 怪兽 prefab | Editor: 添加 EnemyHealth | 4 |
| `Assets/SO/AnBiComboConfig.asset` | Editor: 设置伤害值 | 5 |
| `Assets/Scenes/MyTest.unity` | Editor: Ground Layer | 5 |

---

## 验证 Checklist

- [ ] 角色移动 — 不会穿过地板，重力正常
- [ ] 锁定 — 中键锁定敌人正常
- [ ] 攻击命中 — OverlapSphere 范围内全部敌人命中（不只是锁定目标）
- [ ] 架势/HP — 架势归零后开始扣HP
- [ ] 受击动画 — 敌人播放 Parry → Hit → Death 动画
- [ ] 死亡 — LockOnTarget 自动关闭
