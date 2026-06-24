# Camera Shake + Enemy Lock-On System Plan

## Context

项目已具备完整双FSM（Locomotion + Action）和 Cinemachine 2.10.7，但缺少：
- 相机震屏反馈 — **打击感核心缺失**
- 顿帧（Hit Pause）— 卡肉感缺失
- 敌人锁定系统 — 无目标切换、无线框指示
- 命中检测 — Combo系统只有动画/音效/VFX，不产生具体命中判定

目标：用 Cinemachine 内置 Impulse 机制建立震屏 + 锁定系统，Combo 每段自动震屏，用 Capsule 做锁定测试。

**旧 Plan 删除：** 实施时先删除 `c:\Users\admin\.claude\plans\hashed-zooming-token.md`（其假设的 `相机震屏源.prefab` / `镜头打击感.prefab` 实际不存在）。

---

## 技术解释：Cinemachine Impulse 是什么？

**不需要额外下载任何插件。** Cinemachine Impulse 是 Cinemachine 2.10.7 内置功能，已在你的 `Packages/manifest.json` 中。

### "管线"（Pipeline）是什么意思？

指的是信号从产生到生效的**数据流路径**，不是额外组件：

```
① ImpulseSource（信号发生器）          ② Channel（通道）         ③ ImpulseListener（接收器）
┌──────────────────────┐         ┌──────────────┐         ┌──────────────────────────┐
│ 挂在一个 GameObject   │  广播   │ 频道号（如1） │  订阅   │ 挂在 VCam 上的扩展组件     │
│ GenerateImpulse()    │ ──────→ │ 允许多源多听  │ ──────→ │ 收到信号→对相机施加噪声     │
│ 代码里一行调用即可     │         │ 互不干扰       │         │ 产生抖动效果               │
└──────────────────────┘         └──────────────┘         └──────────────────────────┘
```

简单说就是：**代码说"抖" → ImpulseSource 发信号 → ImpulseListener 收到 → 相机抖。** 全部是 Cinemachine 自带的，你的项目里已经有这些类：
- `CinemachineImpulseSource` — 组件，挂 GameObject 上
- `CinemachineImpulseListener` — 组件，挂 VCam 上
- `NoiseSettings` — 资产，定义抖动波形（在 Project 右键 Create → Cinemachine → Noise Settings）

### 和手写震屏的区别

| 手写 | Cinemachine Impulse |
|------|---------------------|
| `cam.transform.position += Random.insideUnitSphere` | 信号驱动，自动衰减 |
| 每个相机要单独写代码 | Listener 自动接收，多相机自动同步 |
| 受 timeScale 影响（需要用 unscaledDeltaTime） | 有 `IgnoreTimeScale` 开关 |
| 抖动波形不可控 | NoiseSettings 可视化编辑频率/幅度 |

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│  CameraShake (Singleton)                                 │
│    持有 CinemachineImpulseSource ──→ Channel 1 ──→ Listeners│
│    + TriggerShake(force)    │                            │
│    + IgnoreTimeScale = true │ (shake不受顿帧影响)         │
├────────────────────────────┘                            │
│  HitPauseManager (Singleton)                             │
│    TimerManager.GetRealTimer() → 恢复 Time.timeScale     │
├──────────────────────────────────────────────────────────┤
│  LockOnManager (Singleton)                               │
│    Tab Toggle → Find nearest LockOnTarget                │
│    → VCam LookAt 切换到目标锁定点                          │
│    → PlayerController.HandleRotation 强制面向目标           │
│    → LockOnUI indicator 跟随目标屏幕坐标                    │
└──────────────────────────────────────────────────────────┘
```

**触发链：**
```
ATKingState.ComboNext()
  → CameraShake.TriggerShake(step.shakeForce)     // ImpulseSource → Listener → 相机抖
  → HitPauseManager.Trigger(pauseDuration, scale) // Time.timeScale → 0.05
  → CharacterAudio + CharacterVFX                 // (已有)
```

---

## FAQ：震屏和顿帧的关系

### 为什么相机抖动还需要调 `Time.timeScale`？

**震屏和顿帧是两个独立系统**，在 `ComboNext()` 里同时触发但互不依赖。这是动作游戏打击感的标准组合技：

```
击中敌人瞬间：
  ① CameraShake.TriggerShake(0.5f)    → 相机物理抖动（空间震动）
  ② HitPauseManager.Trigger(0.05s)     → 时间短暂冻结（顿帧/卡肉）
  ③ 音效 + VFX                        → （已有）
```

### 顿帧（Hit Stop / Impact Freeze）是什么？

以鬼泣/街霸为例：打到敌人的瞬间，**整个画面卡住 0.03~0.08 秒**，然后恢复。这种"卡肉感"让命中更有重量和质感。

实现方式：`Time.timeScale = 0.05`（游戏几乎暂停）→ 等待 N 毫秒 → `Time.timeScale = 1`（恢复正常）。

### 为什么必须用不受 timeScale 影响的计时器？

**这是关键约束。** 如果计时器本身受 timeScale 影响：

```
❌ 错误做法：
Time.timeScale = 0.05;                // 时间几乎暂停
yield return new WaitForSeconds(0.05f); // 这用 scaled time
// WaitForSeconds 内部: remaining -= Time.deltaTime
// deltaTime ≈ 0（因为 timeScale=0.05）→ 要等 1 秒才触发！永远恢复不了！
```

```
✅ 正确做法：
Time.timeScale = 0.05;
TimerManager.GetRealTimer(0.05f, () => Time.timeScale = 1f);
// GameTimer.Tick() 用 Time.unscaledDeltaTime → 不受 timeScale 影响
// 0.05 秒真实时间后正常恢复！
```

项目已有的 `TimerManager.GetRealTimer()` 内部使用 `Time.unscaledDeltaTime`，正是为这个场景准备的 — 它保证顿帧期间恢复回调能准时触发。

### 总结

| | 震屏（Camera Shake） | 顿帧（Hit Pause） |
|---|---|---|
| 做什么 | 相机空间抖动 | 游戏时间暂停 |
| 实现 | `CinemachineImpulseSource.GenerateImpulse()` | `Time.timeScale = 0.05` |
| 恢复 | Cinemachine 信号自动衰减 | `GetRealTimer` 到时恢复 |
| 持续 | 由 NoiseSettings 波形决定（~0.1-0.3s） | 由 hitPauseDuration 决定（~0.03-0.08s） |
| 独立？ | 各自独立调用，互不依赖 | 可以只震不卡，或只卡不震 |
| timeScale 影响？ | 需要 `ImpulseManager.IgnoreTimeScale=true` | 本身就是改 timeScale 的那个 |

---

## New Files (7 新建)

### 1. `Assets/Scripts/Manager/CameraShake.cs`
Singleton wrapping `CinemachineImpulseSource`，使用 `Singleton<T>` 基类。

```csharp
public class CameraShake : Singleton<CameraShake>
{
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    
    void Awake() {
        // 关键：顿帧期间 Impulse 不受 timeScale 影响
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
    }
    
    public void TriggerShake(float force) {
        _impulseSource?.GenerateImpulseWithForce(force);
    }
}
```

### 2. `Assets/Scripts/Manager/HitPauseManager.cs`
顿帧管理器，利用已有的 `TimerManager.GetRealTimer()`（不受 timeScale 影响）。

```csharp
public class HitPauseManager : Singleton<HitPauseManager>
{
    public void Trigger(float duration, float timeScale = 0.05f) {
        Time.timeScale = timeScale;
        TimerManager.Instance.GetRealTimer(duration, () => Time.timeScale = 1f);
    }
}
```

### 3. `Assets/Scripts/Manager/LockOnManager.cs`
锁定管理器，负责目标搜索/锁定/解除。

```csharp
public class LockOnManager : Singleton<LockOnManager>
{
    [Header("锁定参数")]
    [SerializeField] private float _lockRange = 15f;
    [SerializeField] private float _lockViewAngle = 60f;     // 半角
    [SerializeField] private float _loseRange = 20f;         // 脱锁距离
    [SerializeField] private float _loseAngle = 90f;         // 脱锁角度
    [Range(0, 1)] public float LookTargetBias = 0.7f;       // LookAt偏向目标的比例

    [Header("VCam 引用")]
    [SerializeField] private CinemachineVirtualCamera _vCam;
    [SerializeField] private Transform _playerTransform;

    private Transform _lookTarget; // 动态空物体，运行时创建

    public LockOnTarget CurrentTarget { get; private set; }
    public bool IsLockedOn => CurrentTarget != null;
    public event Action<LockOnTarget> OnLockOnChanged;

    void Update() {
        // 脱锁检测...
        // 更新 _lookTarget 位置（player↔target 插值）
        if (IsLockedOn) {
            _lookTarget.position = Vector3.Lerp(
                _playerTransform.position,
                CurrentTarget.GetLockOnPosition(),
                LookTargetBias
            );
        }
    }
}
```

**设计要点（绝区零风格）：**
- `FindObjectsOfType<LockOnTarget>()` 每次搜（测试阶段胶囊少，性能无影响）
- 评分：`distance + angle * 0.5f` — 优先近距离、正前方目标
- `Update()` 中检测脱锁距离/角度，超出自动 Unlock
- `LockOn()` → VCam.LookAt 切换到动态 `_lookTarget`（player 和 enemy 之间插值）
- `Unlock()` → VCam.LookAt 恢复为 player

### 绝区零风格锁定相机行为

**锁定后相机如何变化：**

```
锁定前                          锁定后
┌──────────────────┐           ┌──────────────────┐
│  相机在玩家后方     │           │  相机仍在玩家后方    │
│  看着玩家          │           │  但LookAt偏向敌人   │
│  玩家自由旋转      │           │  玩家强制面向敌人    │
│                  │           │                    │
│     📷           │           │     📷             │
│      \           │           │      \  👤→ 🎯     │
│       👤         │           │    👤→  🎯         │
│                  │           │   角色面向目标       │
└──────────────────┘           └──────────────────┘
```

**和 Dark Souls 类锁定的区别：**

| | 魂系锁定 | 绝区零风格（本项目） |
|---|---|---|
| 相机旋转 | 围绕敌人轨道旋转 | 保持在玩家身后，Follow 不动 |
| LookAt | 直接看敌人 | 在玩家和敌人之间插值（偏敌人） |
| 玩家朝向 | 始终面敌 | 始终面敌（相同） |
| VCam 切换 | 换专用锁定相机 | **不换相机**，只改 LookAt 目标 |
| 移动方式 | 侧移/后退相对敌人 | 自由移动，相机方向决定 |

**为什么不用双 VCam 切换：**
- 单 VCam + 动态 LookAt 过渡更平滑
- 绝区零锁定后相机仍跟随玩家，不是绕敌旋转
- 避免 Priority 切换的混合抖动

**实现关键：**
- 创建一个空 `_lookTarget` Transform，每帧 `Lerp(playerPos, targetPos, 0.7)`
- 锁定：`_vCam.LookAt = _lookTarget`
- 解锁：`_vCam.LookAt = _playerTransform`（恢复默认）
- PlayerController.HandleRotation 锁定态面向敌人

### 4. `Assets/Scripts/Enemy/LockOnTarget.cs`
标记组件，挂在任意敌人 GameObject 上。

```csharp
public class LockOnTarget : MonoBehaviour
{
    public Vector3 lockOnPointOffset = new Vector3(0, 1f, 0);
    public Vector3 GetLockOnPosition() => transform.position + lockOnPointOffset;
}
```

### 5. `Assets/Scripts/UI/LockOnUI.cs`
Canvas 上的锁定指示器（Image），挂在已有 ChatInputCanvas 或新建 Canvas。

```csharp
public class LockOnUI : MonoBehaviour
{
    [SerializeField] private Image _indicator;
    [SerializeField] private RectTransform _canvasRect;
    // OnLockOnChanged → 显隐 indicator
    // Update → 用 RectTransformUtility 转屏幕坐标 → anchoredPosition
    // 注意：目标在屏幕外时隐藏指示器
}
```

### 6. `Assets/Settings/CameraShakeProfile.asset` (NoiseSettings)
Unity Editor 创建资产：
- Position X/Y/Z: Perlin noise, Frequency ~15, Amplitude ~0.3
- Rotation: 0（保持相机朝向）

### 7. `Assets/Prefabs/Enemy_Capsule.prefab`
测试用胶囊敌人：
- Capsule (MeshFilter + MeshRenderer + CapsuleCollider, r=0.5, h=2)
- LockOnTarget 组件, offset=(0, 1, 0)

---

## Modified Files (5 修改)

### `ComboStepData.cs`
Add three fields（向后兼容，默认值 0）:
```csharp
[Header("震屏")]
[Range(0f, 2f)] public float shakeForce = 0f;
[Range(0f, 0.5f)] public float hitPauseDuration = 0f;
[Range(0f, 1f)] public float hitPauseScale = 0.05f;
```

### `ATKingState.cs` — ComboNext()
```csharp
// 现有音频播放之后加入：
var step = ResuableData.CurrentStep;
if (step.shakeForce > 0) CameraShake.Instance.TriggerShake(step.shakeForce);
if (step.hitPauseDuration > 0) HitPauseManager.Instance.Trigger(step.hitPauseDuration, step.hitPauseScale);
```

### `ActionNullState.cs` — OnFireStarted()
同样加入震屏+顿帧（第一击也要反馈）。

### `DashingState.cs` — Enter()
```csharp
CameraShake.Instance.TriggerShake(0.2f);  // 闪避轻震
```

### `PlayerController.cs`
- `Start()` 订阅 `Player/LockOn` 输入 → `LockOnManager.Instance.ToggleLockOn()`
- `HandleRotation()`: `IsLockedOn` 时面向 `CurrentTarget.GetLockOnPosition()`，否则保持现有相机相对转向
- `OnDestroy()` 取消订阅

---

## Scene / Prefab 改动

| 操作 | 说明 |
|------|------|
| 创建 GameObject "CameraShake" | 挂 `CameraShake` + `CinemachineImpulseSource`（Raw Signal=`CameraShakeProfile`, Channel=1, Default Velocity=(0,-1,0)） |
| 创建 GameObject "HitPauseManager" | 挂 `HitPauseManager` |
| 创建 GameObject "LockOnManager" | 挂 `LockOnManager`，拖 VCam + Player Transform 引用 |
| ThirdPersonCamera 加组件 | `CinemachineImpulseListener`（Channel=1, Gain=1, Apply After=Noise） |
| ChatInputCanvas 加子 Image | 挂 `LockOnUI` 脚本，指示器用简单圆点/十字 Sprite |
| 场景放 2-3 个 Enemy_Capsule | 分布在不同距离/角度（如正前方 5m，左侧 8m，右侧 6m） |
| 修改 `AnbiInput.inputactions` | Player map 加 `LockOn` Action（Button 类型），Keyboard/Tab 绑定 |
| 修改 `AnBiComboConfig.asset` | 5 段逐段递增 shakeForce (0.2→0.7), hitPause (0.03→0.08) |

**不需要：** 不需要新建 LockOnCamera VCam — 复用现有 ThirdPersonCamera，只切换 LookAt。

---

## Implementation Sequence

```
Phase 1 — 震屏管线（Cinemachine 内置，无额外插件）
  1. ComboStepData.cs 加字段
  2. CameraShake.cs + HitPauseManager.cs
  3. Editor: Project 右键→ Create → Cinemachine → Noise Settings 创建 CameraShakeProfile
  4. 场景: CameraShake/HitPauseManager GO；ThirdPersonCamera 加 ImpulseListener
  5. ATKingState / ActionNullState / DashingState 接入
  → 测试：进入游戏，攻击即可看到震屏

Phase 2 — 锁定系统（绝区零风格）
  6. LockOnTarget.cs + LockOnManager.cs + LockOnUI.cs
  7. Input 加 LockOn 绑定 Tab
  8. PlayerController 修改 HandleRotation + 输入订阅
  9. 场景: LockOnManager GO + Canvas 指示器 + Enemy_Capsule 实例
  → 测试：Tab 锁定/解锁，角色面向敌人，相机自动构图

Phase 3 — 数据配置
  10. AnBiComboConfig 逐段调 shakeForce/hitPause 参数
  11. 全面联调：连击中锁定→震屏+顿帧+锁定同时运作
```

---

## Verification

1. **震屏管线：** 按攻击 → 每击屏幕抖动，力度随连段递增（第1段轻 → 第5段重）；闪避 → 轻震
2. **顿帧：** 高连段（3-5段）有明显帧冻结/卡肉感，冻结后 timeScale 正确恢复为 1，无残留
3. **锁定目标：** Tab → 锁最近前方胶囊，UI 指示器出现并跟随目标屏幕位置
4. **锁定相机（绝区零风格）：** 锁定后相机仍在玩家身后，但镜头偏向敌人方向构图；角色自动面向敌人
5. **脱锁：** 跑远超过 20m / 转向超过 90° → 自动解锁；再按 Tab → 手动解锁
6. **边界情况：** 无敌人时 Tab 无反应（不报错）；锁定中敌人被删除自动 Unlock；锁定+连击同时运作
7. **Console：** 零 Error / NullReferenceException

## Key Considerations

- `CinemachineImpulseManager.Instance.IgnoreTimeScale = true` **必须设置**，否则顿帧期间震屏也变慢
- `CinemachineImpulseSource` 和 `CinemachineImpulseListener` 是 **Cinemachine 内置组件**，`using Cinemachine;` 即可，**无需任何额外下载**
- `GameTimer.Tick()` 用 `unscaledDeltaTime`，HitPauseManager 回调在顿帧期间正常触发恢复 timeScale
- `ComboStepData` 加字段向后兼容 — 现有 asset 默认值为 0，不改配置时不会震屏/顿帧
- LockOnManager 用 `FindObjectsOfType` 搜目标（测试阶段胶囊少，性能OK；后续换缓存/注册模式）
- 锁定相机复用现有 ThirdPersonCamera — **不新建 VCam**，只切换 `LookAt` 属性，过渡自然
- 旧 Plan `hashed-zooming-token.md` 需在实施前删除
