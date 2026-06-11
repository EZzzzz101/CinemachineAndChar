# CinemachineAndChar — 角色控制系统

## 项目概要
Unity 项目，基于分层有限状态机（Hierarchical FSM）的角色控制系统，使用 Cinemachine + Unity Input System。

参考项目：`C:\Users\admin\Downloads\zzzdemo-source-code-master\zzzdemo-source-code-master\Assets\Scripts\FSM\`

## 架构

```
PlayerController (顶层 MonoBehaviour)
├── LocomotionStateMachine (移动层)
│   ├── IdleState ↔ RunState ↔ SprintState
│   └── DashingState (动画驱动过渡状态)
└── ActionStateMachine (攻击层，待做)
```

## 状态切换两种模式

1. **输入驱动**：`AddInputCallbacks` 订阅 Input System 事件 → 回调直接 `ChangeState`
   - Idle 订阅 Move.started → Run
   - Run 订阅 Move.canceled → Idle, Run.started → Sprint
   - Sprint 订阅 Move.canceled → Idle, Run.canceled → Run

2. **动画驱动**：`StateMachineBehaviour` 挂在动画 clip → `OnStateEnter/Exit` → 通知 `PlayerController` 路由
   - AnimationEnterBehaviour: 动画进入时 `PlayerController.OnAnimationTranslateEvent(string)`
   - AnimationExitBehaviour: 动画退出时 `PlayerController.OnAnimationExitEvent()`

## 关键文件

- `Assets/Scripts/Character/Core/IState.cs` — 状态接口（Enter/Exit/Update/HandInput/OnAnimationTranslateEvent/OnAnimationExitEvent）
- `Assets/Scripts/Character/Core/StateMachine.cs` — ChangeState 核心逻辑
- `Assets/Scripts/Character/Core/Locomotion/States/LocomotionState.cs` — 移动状态基类（_currentSpeed, _speedVelocity, GetTargetSpeed, Update平滑）
- `Assets/Scripts/Character/Core/Locomotion/States/IdleState.cs` — 输入驱动参考模板
- `Assets/Scripts/Character/Core/Locomotion/States/RunState.cs` — 需要加 Run.started → Sprint
- `Assets/Scripts/Character/Core/Locomotion/States/SprintState.cs` — 需要补 Move.canceled + Run.canceled
- `Assets/Scripts/Character/Core/Locomotion/LocomotionStateMachine.cs` — 需要注册 DashingState
- `Assets/Scripts/Character/PlayerController.cs` — 需要加 OnAnimationTranslateEvent(string) 路由方法
- `Assets/Scripts/Character/AnimationEnterBehaviour.cs` — StateMachineBehaviour，已创建
- `Assets/Input/AnbiInput.inputactions` — 输入配置（Fire=左键, Run=Shift, Move=WASD）

## 当前进度

- [x] FSM 基础框架
- [x] Idle ↔ Run 输入驱动切换
- [ ] Run/Sprint 拆分（Move.canceled 提取到 MovingState 基类）
- [ ] Dash 状态（动画驱动，Run→Dash动画→Sprint）
- [ ] ActionStateMachine（攻击/Combo）
- [ ] AnimationExitBehaviour 创建
- [ ] PlayerController 路由方法
- [ ] EventBus 待 UI 连接时使用

## 设计约定

- 每个状态在 `Enter` 时订阅输入，`Exit` 时取消订阅
- 速度通过 `GetTargetSpeed()` 虚方法声明，基类 `Update()` 统一 SmoothDamp
- `Player/Dash` = 左 Shift，`Player/Fire` = 鼠标左键，`Player/Move` = WASD
- **子类覆写虚方法后必须手动 `base.AddInputCallbacks()`**，否则基类的输入订阅丢失（C# 多态规则：调用虚方法时会走到子类版本，不会自动调基类版本）
- **学习过程记录在 CLAUDE_XX.md 文件中，每次重要学习都追加新文件，不覆盖旧文件**

## ⚠️ 每次对话开头必读提醒

**Claude：请阅读本文件所在目录下所有 `CLAUDE*.md` 文件（按数字顺序），了解项目架构和学习历史，不要重复犯已经纠正过的错误。**
