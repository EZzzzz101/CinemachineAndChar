# CLAUDE_06 — 连招修复 + 锁定系统 + 攻击闪身

> 日期: 2026-06-26

---

## 一、连招系统修复

### 1.1 核心问题：缺少两段锁

对照 zzzdemo 源码发现，原项目只有 `canInput` 一把锁，导致 `EnablePreInput` 开在 ATK 之前时，手快玩家会跳过 ATK 直接切下一段。

**zzzdemo 的真实现流：**
```
① EnablePreInput  → canInput=true    "允许记录玩家按下"
   玩家按下        → hasATKCommand=true  "记录但不执行"
② ATK             → 伤害/震屏/卡肉   "本击一定打完"
③ CancelAttackColdTime → canATK=true "现在允许执行缓冲指令"
④ DisableLinkCombo    → 禁连招
⑤ EnableMoveInterrupt → 允许移动打断
```

**修复：新增 `canATK` 字段实现两段锁：**
- `canInput`（EnablePreInput 打开）→ 允许"记录"
- `canATK`（CancelAttackColdTime 打开）→ 允许"执行"
- `Update()` 检测 `canATK && hasBufferedInput` 才执行 `ComboNext()`

### 1.2 canLinkCombo 永久残存

`DisableLinkCombo()` 设为 false 后从未重置，第二轮连招被永久拒绝。

**修复：** 在 `ATKingState.Enter()` 中重置 `canLinkCombo = true`。每段进状态刷新连招许可。

### 1.3 _hasAdvancedCombo 竞态覆盖

`Enter()` 里 `_hasAdvancedCombo = false` 把 `ComboNext()` 刚设的 `true` 覆盖了，导致 `OnAnimationExitEvent` 读到 false → 错误回 ActionNull → 状态循环。

**修复：** 从 `Enter()` 中删除 `_hasAdvancedCombo = false`。该标记只由 `ComboNext()` 设 true，由 `OnAnimationExitEvent()` 消费为 false。

### 1.4 防循环守卫

`ActionNullState.OnAnimationTranslateEvent` 加 `if (!_isEntering) return;` 守卫，防止 Animator 过渡线意外触发 ATK 导致 FSM 死循环。

---

## 二、锁定系统（双 VCam + TargetGroup）

### 2.1 新建文件

| 文件 | 作用 |
|------|------|
| `Enemy/LockOnTarget.cs` | 敌人标记组件，定义锁定点偏移，OnEnable/OnDisable 注册到静态列表 |
| `Manager/LockOnManager.cs` | 锁定管理器，中键切换，双 VCam 方案，脱锁检测 |
| `UI/LockOnUI.cs` | Canvas 红点指示器，跟随锁定目标屏幕坐标 |

### 2.2 架构：双 VCam 方案

```
自由相机 FreeCam (POV, Priority=10)
    ↕ 自动克隆
索敌相机 LockOnCam (Composer, Priority=0/20, LookAt=TargetGroup)

锁定时：
  ① LockOnCam.pos/rot = FreeCam.pos/rot  ← 相同位置起步
  ② LockOnCam.Priority 0→20, FreeCam 10→10
  ③ CinemachineBrain 检测 Priority 变化
  ④ EaseInOut 0.25s 过渡（可配置）
  ⑤ Composer 偏头看 TargetGroup 中点（玩家↔敌人）
  
解锁时：
  ① FreeCam.pos/rot = LockOnCam.pos/rot ← 瞬移到索敌位置
  ② Priority 还原 → Cinemachine 过渡回 POV
  ③ 过渡平滑无跳变
```

### 2.3 TargetGroup

- 解锁时：仅含玩家 → 相机看玩家（与 POV 正常行为一致）
- 锁定时：玩家 + 敌人（权重 1:1）→ Composer 看中点

分组 Composer（GroupComposer）会强制推拉距离框住包围球过于激进，最终选用普通 Composer（只偏头看中点，不强制调整距离，体验自然）。

### 2.4 目标搜索

- 从角色位置向前搜索，Tag 过滤 `Enemy`
- 评分：距离 + 角度 × 0.5，优先正面近距离
- 使用静态 `LockOnTarget.ActiveTargets` 注册表，避免 `FindObjectsOfType`
- 脱锁仅按距离（默认 20m），不管角色朝向

### 2.5 POV 同步算法

解锁时将 FreeCam 位置/朝向直接瞬移到 LockOnCam 当前值，Cinemachine 从同一点出发 0.25s 过渡回 POV，避免大幅跳变。

---

## 三、攻击闪身 + 面向

### 3.1 PlayerController 新增

| 方法 | 作用 |
|------|------|
| `FaceEnemy()` | 锁定态瞬间面向敌人（`Quaternion.LookRotation`） |
| `FlashToEnemy()` | 锁定态 3m 内闪到敌人正前方 1.5m（直接设 `transform.position`） |

### 3.2 调用时机

`ActionNullState.OnFireStarted` 中，播攻击动画之前：
```
FaceEnemy() → FlashToEnemy() → 播动画
```

参数可调：`Flash Max Dist`（3m）、`Flash Target Dist`（1.5m）。

---

## 四、文件变更清单

### 新建
- `Assets/Scripts/Enemy/LockOnTarget.cs`
- `Assets/Scripts/Manager/LockOnManager.cs`
- `Assets/Scripts/UI/LockOnUI.cs`

### 修改
- `Assets/Scripts/Character/Core/Combo/ComboResuableData.cs` — 加 `canATK`
- `Assets/Scripts/Character/Core/Combo/States/ATKingState.cs` — 两段锁 + Enter/ComboNext 重构
- `Assets/Scripts/Character/Core/Combo/States/ActionNullState.cs` — 防循环守卫 + 闪身调用
- `Assets/Scripts/Character/PlayerController.cs` — FaceEnemy + FlashToEnemy

---

## 五、待实现

- CharacterController 挂载（防穿墙、重力、地面检测）
- 攻击检测（ATK 内距离/角度判定 → 命中扣血）
- EnemyHealth 敌人血量系统
- 受击反馈（敌人动画 / VFX）
