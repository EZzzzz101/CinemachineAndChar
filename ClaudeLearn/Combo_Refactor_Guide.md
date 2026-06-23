# Combo 代码重构学习指南

## 目的

这个文档用于记录你当前项目里 `Combo` 代码和下载示例之间的差异，并给出一个清晰的改造方案。你可以带回家继续修改，并在后面与 AI 继续讨论。

## 当前项目结构（你的代码）

主要相关文件：
- `Assets/Scripts/Character/PlayerController.cs`
- `Assets/Scripts/Character/Core/Combo/ActionStateMachine.cs`
- `Assets/Scripts/Character/Core/Combo/States/ComboState.cs`
- `Assets/Scripts/Character/Core/Combo/States/ActionNullState.cs`

现状：
- `PlayerController` 持有一个 `ActionStateMachine`，并在 `Start()` 时进入 `ActionNullState`。
- `ActionNullState` 监听 `Player/Fire`，按下后直接播放 `Anbi_Normal_1` 动画。
- `ComboState` 既做“输入缓存”和“combo 切换”，也做“动画退出后的下一招播放”和“退出回空状态”。
- `ComboState` 内部硬编码了动画列表 `_comboAnims = { "Anbi_Normal_1", "Anbi_Normal_2", "Anbi_Normal_3" }`。

## 参考示例结构（下载示例代码）

主要参考点：
- `PlayerComboStateMachine`：状态机持有多个状态，攻击/空状态分离。
- `PlayerComboState`：combo 状态基类，统一负责输入注册、Update 调用、事件绑定。
- `PlayerATKIngState`：攻击中状态，负责攻击期间的行为和动画结束判断。
- `PlayerNullState`：攻击空闲状态，负责复位 combo、等待输入。
- `CharacterComboBase` / `CharacterCombo`：负责 combo 具体逻辑和数据（能否输入、连招、动画播放、当前索引、攻击类型等）。

## 核心区别

1. 责任分离
   - 你：`ComboState` 承担太多职责。
   - 他：状态机、状态、combo 逻辑三个层次分开。

2. 输入注册方式
   - 你：攻击输入直接写在 `ComboState` / `ActionNullState`。
   - 他：基类统一注册输入，状态只决定是否允许输入。

3. combo 数据管理
   - 你：`_comboAnims` 和 `ComboIndex` 直接写在状态里。
   - 他：使用 `ReusableData` / `ComboData`，动画名字、索引、是否可连招都在组合数据结构里管理。

4. 动画驱动流程
   - 你：`OnAnimationExitEvent()` 里直接判断并播放下一招。
   - 他：`Update()` 和 combo 逻辑里管理是否可以发动下一招，`OnAnimationExitEvent()` 只做状态切换。

5. 可扩展性
   - 你：现在是“基础连招播放逻辑”，但是如果要加技能、后摇、取消、移动打断就会很乱。
   - 他：已经考虑了“连招输入、重攻、技能、切换、移动中断”等更复杂逻辑。

## 建议改造方向

你作为学习者，改造目标应该是：
- 保持业务清晰
- 让逻辑职责分开
- 先实现最简单的“输入→攻击中→缓存下一招→回空状态”的流程
- 后面再扩展成数据驱动的 combo 系统

### 方案概览

1. 让 `ActionStateMachine` 持有两个状态：
   - `ActionNullState`：空闲等待攻击输入
   - `ComboState`：攻击中状态

2. 让 `ActionNullState` 只负责输入触发进入 combo
   - 不直接播放下一招动画
   - 而是切状态到 `ComboState`

3. 让 `ComboState` 负责：
   - `Enter()` 开始第一招动画
   - 记录 `ComboIndex`
   - 注册预输入监听
   - `OnAnimationExitEvent()` 判断是否继续下一招，或者切回 `ActionNullState`

4. 让 `OnAnimationTranslateEvent()` 仅做“动画事件到状态对象”的映射，不做 combo 决策。

5. 以后可继续拆分成：
   - `ActionComboState` 基类
   - `ActionAttackState` / `ActionNullState`
   - 独立 `ComboData` / `ComboRunner`

## 推荐修改步骤

下面是你回家后可以直接开始的步骤。

### 1. 备份当前文件

先备份 `Assets/Scripts/Character/Core/Combo/States/ComboState.cs` 和 `ActionNullState.cs`，然后再改。

### 2. 修改 `ActionNullState` 不直接播放动画

让它从“按键直接播放动画”改为“按键切换到攻击状态”：
- `OnFireStarted()` 改成 `Sm.ChangeState(Sm.ComboState)`
- 不要在这里调用 `Animator.CrossFadeInFixedTime`

### 3. 优化 `ComboState` Enter / Exit

`ComboState.Enter()` 负责：
- `_hasBufferedInput = false`
- `Sm.ComboIndex = 0`
- 启动第一招动画：`Owner.Animator.CrossFadeInFixedTime("Anbi_Normal_1", 0.111f)`
- 注册 `Player/Fire` 预输入监听

`ComboState.Exit()` 负责：
- 注销 `Player/Fire` 监听

### 4. 让 `ComboState` 只处理缓存和结束判断

`OnFireStarted()` 继续判断：
- 只有在预输入窗口内才记录 `true`
- 只有未到最后一招才记录

`OnAnimationExitEvent()` 继续判断：
- 如果有缓存，`Sm.ComboIndex++`，`CrossFadeInFixedTime` 下一招
- 如果没有缓存，则 `Sm.ComboIndex = 0`，`Sm.ChangeState(Sm.ActionNullState)`

这个流程已经是“个人版参考实现”，逻辑清晰，并且和参考示例的“攻击中状态 + 空闲状态”结构一致。

### 5. 如果想进一步贴近参考样本

后续可继续改：
- 增加一个 `ActionComboState` 基类，让 `ComboState` 继承它。
- 添加 `ActionAttackState` 和 `ActionNullState`，把 `ComboState` 改名为 `ActionAttackState`。
- 把 `_comboAnims` 改成一个可配置数组或 ScriptableObject 数据。
- 把 `ComboIndex`、`_hasBufferedInput`、`CanInput` 等数据抽到一个 combo 数据类里。

## 你现在可以直接做的最小改动

如果你现在只想先把结构理顺，最简单的改动是：
- `ActionNullState`：按键进入 `ComboState`
- `ComboState.Enter()`：开始第一招动画
- `ComboState.OnAnimationExitEvent()`：根据缓冲决定继续或回空

这会让你的逻辑和参考里面“空闲态触发攻击-攻击态处理连招”的思路对齐。

## 读这份文档的优先顺序

1. 先看“当前项目结构”和“参考示例结构”部分，理解两个实现层次差异。
2. 再看“建议改造方向”和“推荐修改步骤”，按步骤修改。
3. 这样你回家时直接按照文档改不会丢进度。

## 文件位置

文档已保存到：
- `D:\UnityProject\CinemachineAndChar\Combo_Refactor_Guide.md`

你可以回家继续打开这个文件，按顺序执行改造步骤。