# CLAUDE_02 — 动画驱动状态切换（2026-06-11）

> **⚠️ Claude：请先阅读所有 `CLAUDE*.md` 文件（按数字顺序），了解完整学习过程，不要重复已纠正过的错误。**

---

## 本次学习内容：动画驱动状态架构

### 问题起源
如何让代码响应"动画播完了"这个事件？不能靠计时器 — 动画时长会变。答案：**让动画自己通知代码**。

---

## 两条切换通路

状态切换分两种模式，都要在 Animator Controller 里挂 `StateMachineBehaviour`：

| 通路 | 脚本 | 挂在 | 触发 | 干什么 |
|------|------|------|------|--------|
| **动画进入** | `AnimationEnterBehaviour` | 动画 clip | `OnStateEnter` | 字符串/枚举 → 路由到 FSM，切状态 |
| **动画退出** | `AnimationExitBehaviour` | 动画 clip | `OnStateExit` | 通知当前状态"动画播完了"，状态自己决定去哪 |

**重要**：`OnStateExit` 只在动画状态**被切换走**时触发，不是"动画自然播完就触发"。所以 Animator Controller 里必须有一条过渡线（Has Exit Time）让动画播完后自动过渡到目标动画状态，OnStateExit 才会触发。

---

## 动画驱动的核心链路（B 方案：先播动画，后切状态）

```
按 Dash 键(Shift)
  → LocomotionState.OnDashStarted()
  → animator.CrossFadeInFixedTime("DashFront", 0.1f)   // ① 先播动画

Unity 开始播 DashFront 动画
  → AnimationEnterBehaviour.OnStateEnter()            // ② 动画通知：开始了
  → PlayerController.OnAnimationTranslateEvent(Dash)  // ③ 枚举路由
  → ChangeState(DashingState)                         // ④ 切状态（后切！）
  → DashingState.Enter()                              // ⑤ 只做数据逻辑

Unity 播完 DashFront → 过渡线(Has Exit Time) → 目标动画
  → AnimationExitBehaviour.OnStateExit()              // ⑥ 动画通知：结束了
  → PlayerController.OnAnimationExitEvent()
  → DashingState.OnAnimationExitEvent()
  → ChangeState(IdleState)                            // ⑦ 回 Idle
```

**关键理解**：不是"切状态→播动画"，而是"播动画→动画事件切状态"。动画是发令者，代码是响应者。

---

## 重点知识

### 1. CrossFade vs CrossFadeInFixedTime

| 方法 | 第二个参数含义 |
|------|----------------|
| `CrossFade("X", 0.15f)` | 归一化时间（当前动画时长的 15%） |
| `CrossFadeInFixedTime("X", 0.15f)` | 固定秒数（0.15 秒） |

**用 `CrossFadeInFixedTime`**，过渡时间精确可控。

### 2. 基类放公共输入，子类手动 base. 调用

基类 `LocomotionState.AddInputCallbacks()` 订阅 Dash，所有子类自带 Dash 能力。

**坑**：子类覆写 `AddInputCallbacks` 后，C# 多态会让 `Enter()` 里调的是子类版本。子类**必须**手动 `base.AddInputCallbacks()`，否则基类的 Dash 订阅就丢了：

```csharp
// IdleState / RunState / SprintState 都要写
protected override void AddInputCallbacks()
{
    base.AddInputCallbacks();    // ← 必须加！否则 Dash 不生效
    // 自己的订阅...
}
```

### 3. PlayerController 的路由角色

`PlayerController` 是动画层（字符串/枚举）和 FSM 层（IState 对象）之间的**翻译员**：

```
AnimationEnterBehaviour（枚举）→ PlayerController.OnAnimationTranslateEvent(枚举) → switch → Locomotion.OnAnimationTranslateEvent(IState实例)
```

枚举定义在 `AnimationEnterBehaviour` 内部，PlayerController 引用 `AnimationEnterBehaviour.AnimationEnterState`。

### 4. 过渡态不需要速度

DashingState 是过渡态，只播动画不做移动。`GetTargetSpeed()` 不需要覆写，基类默认返回 0。它只需要：
- `OnAnimationExitEvent()`：动画结束切到哪
- 可选覆写 `OnDashStarted()`：已经在 Dash 时禁止再触发 Dash

### 5. DashingState 被重命名为 Dash（输入名）

输入配置里 action 名是 `"Player/Dash"`（对应 Shift 键），不是 Rush。代码里方法也统一用 `OnDashStarted`。

---

## 待做事项（下个会话继续）

- [ ] 补全 IdleState / RunState / SprintState 的 `base.AddInputCallbacks()`
- [ ] 完成 AnimationExitBehaviour 的 Unity 挂载 + Animator Controller 过渡线
- [ ] 测试完整链路：Idle → Dash → DashingState → Idle
- [ ] DashingState.OnAnimationExitEvent 加方向判断（有方向→Sprint，无方向→Idle）
- [ ] RunState 改为 Dash→DashingState（不再直接切 SprintState）

---

## ⚠️ Claude 记住

**请阅读本文件所在目录下所有 `CLAUDE*.md` 文件（按数字顺序），了解项目架构和学习历史，不要重复犯已经纠正过的错误。**
