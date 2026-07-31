# CLAUDE_13 — 行为树 Animator 控制重构：状态型/行为型拆分 + Motor 执行层

> 日期：2026-07-31
> 前置：[CLAUDE_07_BehaviorTree.md](CLAUDE_07_BehaviorTree.md) | [monster-bt-design.md](monster-bt-design.md)

---

## 一、今日背景与目标

`BTSetAnimatorBool` 职责混乱，一个节点同时做四件事：

1. 设置 Animator Bool 参数
2. 等待动画结束
3. 判断动画生命周期
4. 接收动画退出信号

目标：**不推翻现有架构**，把 Animator 相关节点按生命周期类型拆开，并引入"决策层给标志、执行层每帧干活"的 Motor 分层，解决 BT tick（0.1s）与游戏帧生成不一致导致的怪物停顿。

## 二、核心设计原则（本次确立）

### 2.1 Animator 参数分两类，用不同节点处理

| 类型 | 例子 | 生命周期 | 用哪个节点 |
|------|------|---------|-----------|
| **状态型 Bool** | `IsMoving` / `IsGuard` / `IsCharging` | 进入 Set(true) → 离开 Set(false)，不等动画 | `BTSetAnimatorBool`（一帧完成） |
| **行为型动画** | `Attack` / `Dash` / `Hit` | 触发 → Running → 等动画退出 → Success | 专用节点（如 `BTDash`） |

行为型动画**不能** `SetTrigger → Success` 一帧完成，否则下一帧重扫又触发一次、动画重播。

### 2.2 决策层 / 执行层分离（Motor 分层）

```
行为树（tick 0.1s）: 只写标志 bool，如 bb["_faceTarget"] = true/false
        ↓
BossMotor（每帧 Update）: 读标志 + 读 target，平滑执行旋转
```

解决：行为树低频决策，Motor 高频执行，tick 间隙不再停顿。动画退出通知仍走 `BTAnimationExitNotifier → 黑板信号`。

## 三、改动清单

### 已应用

| 文件 | 改动 |
|------|------|
| `Actions(动作节点)/BTSetAnimatorBool.cs` | **精简**：删掉 `WaitForAnimExit` / `DoneSignalKey` 和整套等待逻辑，只保留 `SetBool → Success`（状态型） |
| `Actions(动作节点)/BTDash.cs` | **新建**：冲刺专用节点。OnEnter 清旧信号 → SetBool(true) 一次 → Running 等退出信号 → Success → OnExit SetBool(false)。加 `MaxDuration` 兜底超时 |
| `Actions(动作节点)/BTAnimationExitNotifier.cs` | **保留**：StateMachineBehaviour，OnStateExit 写 `bb[状态hash]=true` + 可选 ClearBoolName。只改注释 |
| `Actions(动作节点)/BTSetFaceTarget.cs` | **新建**：专用写 `_faceTarget` 标志的节点（键名写死，防敲错）。与 `BTFindNearestTarget`（写 target，时刻需要）职责分离 |
| `BossMotor.cs` | **新建**：每帧执行器。读 `_faceTarget`（开关）+ `target`（数据），Slerp 平滑面向 |
| `NewBehaviorTree.asset` | 冲刺节点从 `BTSetAnimatorBool+WaitForAnimExit` 换成 `BTDash`，`MaxDuration: 5` |

### 已删除

| 文件 | 原因 |
|------|------|
| `Actions(动作节点)/BTFaceTarget.cs` | tick 级旋转节点，被 `BossMotor` 每帧执行取代 |
| `Actions(动作节点)/BTSetBlackboardBool.cs` | 通用 bool 设置器，用户改要面向专用节点 |

### 未完成（下一步）

- **BlendTree 转向**：根据 Boss 朝向与目标方向的**有符号角度差**，写进 Animator 转向混合参数（`-1左 ~ 1右，0=正前方`）驱动转向动画。放 `BossMotor` 里每帧算（编辑中断，未落地）。

## 四、踩坑记录（重要学习）

### 4.1 动画没接上 → 节点永久卡死（常亮）

`BTDash` 靠 `OnStateExit` 写黑板信号判断完成。**AnyState→Dash 链条断开**时，Dash 状态进不去 → `OnStateExit` 永不触发 → 信号永不来 → 节点一直 Running（编辑器常亮）。

**解决**：加 `MaxDuration` 兜底，超时强制完成 + 打 Warning。超时走 Success（避免失败向上级联导致整条战斗分支失败）。

### 4.2 出口过渡不勾 Has Exit Time → 鬼畜闪灯

Dash 出口过渡无 Has Exit Time 且条件瞬间满足 → Dash 进入后立即退出 → 信号到达 → 节点 Success → 加权随机（权重 100）立刻重选 → 重进 → Running…… 每 tick 循环，节点亮暗闪烁。

**解决**：出口过渡勾 **Has Exit Time**，动画播完才退出。这是 bool 驱动 AnyState 的常规用法：进入保持 true、退出清除。

### 4.3 参数名尾随空格（还没核实的雷）

`Boss.controller` 里 dash 参数名是 `'dash '`（**带尾随空格**，YAML 用引号证实），而 notifier 的 `ClearBoolName: dash` 无空格 → 可能清不掉，Dash 退出后 bool 还是 true → AnyState 无限拉回。建议在 Animator 窗口重命名为 `dash`。

### 4.4 Dash 出口条件与战斗状态冲突

Dash 出口过渡是 `IsMoving==false` / `IsSolo==true`，而冲刺挂在设了 `IsMoving=true` 的战斗状态序列里 → Dash 退不出去。需加**无条件/退出时间**过渡。

### 4.5 黑板 SO 不是运行时白名单

`Blackboard` 是纯 `Dictionary<string,object>`，构造函数里 `BTBlackboardSO` 参数**根本没用到**。`Set/Get` 任意字符串键都能读写（`target`、`_hpRatio` 等 SO 里都没有）。SO 只做编辑器键下拉提示，运行时无约束。

## 五、当前黑板键清单

| 键 | 类型 | 写它的人 |
|---|---|---|
| `_owner` / `_transform` / `_animator` | 内置 | Blackboard 构造时 |
| `target` | Transform | `BTFindNearestTarget` |
| `_hpRatio` | float | `BossBrain` |
| `_timeSincePlayerAttack` | float | `BossBrain` |
| 状态名 hash | bool | `BTAnimationExitNotifier.OnStateExit` 退出信号 |
| `bt_active_anims` | HashSet\<int\> | `BTAnimationExitNotifier.OnStateEnter` |
| `_faceTarget` | bool | `BTSetFaceTarget` |
