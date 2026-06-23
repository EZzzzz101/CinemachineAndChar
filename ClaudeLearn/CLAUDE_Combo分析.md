# Combo 系统分析 — 参考项目 vs 当前代码

## 参考项目（zzzdemo）的层次结构

```
Player (总入口，路由动画事件 → 状态机)
├── MovementStateMachine
└── ComboStateMachine
    ├── PlayerComboState (基类，统一输入 + 通用更新)
    │   ├── 订阅 L_AtK / FinishSkill / Execute 事件
    │   ├── Update(): characterCombo.UpdateComboAnimation() + UpdateEnemy()
    │   └── 持有 CharacterCombo 实例
    ├── PlayerNullState (空闲，等待攻击输入)
    │   └── Enter: ReSetComboInfo()
    ├── PlayerATKIngState (攻击执行中)
    │   ├── Update: UpdateAttackLookAtEnemy() + CheckMoveInterrupt()
    │   ├── OnAnimationExitEvent: 延迟 0.2s 后切回 NullState
    │   └── 对外接口: EnablePreInput / DisableLinkCombo / ATK / etc.
    └── CharacterCombo (业务层，独立于状态机)
        ├── LightComboInput() / DodgeComboInput() → combo 输入判断
        ├── UpdateComboAnimation() → 实际播放动画
        ├── CanBaseComboInput() → 条件判断（不能在被Hit/Parry/Skill时输入）
        ├── CheckCanLinkCombo() / CheckMoveInterrupt()
        └── ReSetComboInfo() → 重置 combo 状态
```

### 核心设计：状态层 vs 业务层分离

| 层 | 类 | 职责 |
|----|-----|------|
| 状态层 | PlayerComboState / PlayerATKIngState / PlayerNullState | 管理"当前处于什么状态"、输入订阅、动画事件路由 |
| 业务层 | CharacterCombo | 管理"怎么 combo"：索引、能否输入、播哪段动画、朝向敌人 |

**状态层调用业务层，业务层不碰状态机。** 这是关键。

---

## 你当前代码的现状

```
ActionStateMachine
├── ComboIndex 字段（放在状态机上了，不应在这里）
├── ActionNullState (空闲)
│   └── Enter: 订阅 Fire → CrossFade "Anbi_Normal_1"
└── ComboState (攻击中)
    ├── _hasBufferedInput / _comboAnims[]（硬编码在这里）
    ├── Enter: 订阅 Fire, 重置 buffer + ComboIndex=0
    ├── OnFireStarted: 检查 combo 窗口，标记 buffer
    ├── OnAnimationExitEvent: 有buffer→下一段CrossFade; 无→回Null
    └── Update: 空
```

### 当前代码的问题

1. **状态层和业务层混在一起**：`ComboState` 同时做状态管理（Enter/Exit/订阅）+ 业务逻辑（combo索引、动画名数组、buffer判断）
2. **ComboIndex 放在 ActionStateMachine 上**：这是业务数据，不属于状态机容器
3. **动画名硬编码**：`_comboAnims = { "Anbi_Normal_1", "Anbi_Normal_2", "Anbi_Normal_3" }` 写死在 ComboState 里，加招式要改代码
4. **没有 ComboState 基类**：ActionNullState 和 ComboState 各自实现 IState，共享逻辑（比如都订阅 Fire）没有提取
5. **外部无法控制 combo 行为**：比如"关闭连招窗口"、"允许移动打断"、"取消攻击冷却"这些操作没有入口

---

## 修改路线

### 第一步：创建 ComboState 基类

新建 `Assets/Scripts/Character/Core/Combo/States/ComboState.cs`（替换现有的），让它成为 Action 状态的统一基类：

- 持有 `ActionStateMachine Sm`、`PlayerController Owner`、`Animator`
- `Enter()` / `Exit()` 统一订阅/取消 `Player/Fire`
- `Update()` 空（子类覆写）
- 子类：`ActionNullState`、`ActionATKIngState`

### 第二步：拆分出 CharacterCombo 业务类

新建 `Assets/Scripts/Character/Core/Combo/CharacterCombo.cs`：

- 字段：`comboIndex`、`hasBufferedInput`、`comboAnims[]`、`inputWindowStart`
- 方法：
  - `LightComboInput()` — 接收攻击输入，判断是否允许
  - `UpdateComboAnimation()` — 实际执行 CrossFade
  - `CanBaseComboInput()` — 检查当前是否可以输入
  - `ReSetComboInfo()` — 重置 combo 状态
  - `CheckCanLinkCombo()` — 判断是否可以连下一段
- ComboState 通过调用 CharacterCombo 来完成业务，自己不存 combo 数据

### 第三步：重写 ActionNullState

- 继承 ComboState
- `Enter()`：`base.Enter()` + `characterCombo.ReSetComboInfo()`
- Fire 输入 → `characterCombo.LightComboInput()`
- 不再直接 CrossFade，交给 CharacterCombo 决定

### 第四步：重写 ComboState → ActionATKIngState

- 继承 ComboState
- `Enter()`：`base.Enter()`
- `Update()`：`characterCombo.UpdateComboAnimation()`
- `OnAnimationExitEvent()`：延迟判断 → 切回 NullState 或继续 combo
- 对外接口（供 Animation Event 调用）：
  - `EnablePreInput()` → `characterCombo.CanInput()`
  - `DisableLinkCombo()` → `characterCombo.DisConnectCombo()`
  - `ATK()` → `characterCombo.ATK()`

### 第五步：PlayerController 补充路由

已有 `OnAnimationTranslateEvent(AnimationEnterState)` 路由，Combo 部分 `case Atk → Action.OnAnimationTranslateEvent(ComboState)` 已写好。后续加上 `Action.OnAnimationExitEvent()` 即可（当前已有）。

---

## 对比总结

| | 当前你的代码 | 改完之后 |
|---|---|---|
| ComboIndex | 在 ActionStateMachine 上 | 在 CharacterCombo 里 |
| 动画名 | 硬编码在 ComboState | 可配置（后面可抽到 ScriptableObject） |
| 输入订阅 | 每个状态各自写 | ComboState 基类统一 |
| 业务逻辑 | 混在 ComboState | 独立到 CharacterCombo |
| 外部控制 | 无 | EnablePreInput / DisableLinkCombo 等接口 |
| 加新招式 | 改代码 | 改数据（或只改 CharacterCombo） |
