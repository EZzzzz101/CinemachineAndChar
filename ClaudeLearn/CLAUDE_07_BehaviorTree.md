# CLAUDE_07 — 行为树 (Behavior Tree) 学习文档

> 前置阅读：项目已有的 FSM 架构（[CLAUDE.md](CLAUDE.md)）、Combo 系统（[CLAUDE_Combo分析.md](CLAUDE_Combo分析.md)）

---

## 1. 为什么是行为树而不是状态机？

你的项目已经有成熟的 FSM（玩家角色用 `LocomotionStateMachine` + `ActionStateMachine`），但**怪物的控制逻辑和玩家完全不同**：

| 对比维度 | 玩家 FSM | 怪物行为树 |
|---------|---------|-----------|
| 驱动方式 | 玩家输入 → 状态切换 | 环境条件 → 行为决策 |
| 状态数量 | 少（Idle/Run/Sprint/Dash 等 5-7 个） | 多（巡逻/警戒/追击/攻击/受击/死亡…） |
| 状态切换 | 输入驱动（按键 → 切状态） | 条件驱动（距离近 → 攻击，距离远 → 追击） |
| 逻辑复杂度 | 线性为主 | 高度分支："如果 玩家在视野内 且 血量>50% 则 追击，否则 逃跑" |
| 可维护性 | 少量状态时清晰 | 状态一多就变成意大利面条 |

### 一句话总结

> **FSM** 是"你现在在什么状态，下一步能去哪"——适合玩家这种输入驱动的有限状态。
> **行为树** 是"现在该做什么，条件决定了优先级"——适合怪物这种条件驱动的多分支决策。

---

## 2. 行为树的核心概念

### 2.1 Tick 驱动模型

行为树**不是事件驱动**，而是**每帧（或每隔几帧）Tick 一次**。整个树从根节点开始遍历，每一帧根据当前条件重新决策。

```
Update()
  → 行为树.Tick()
    → 根节点.Execute(blackboard)
      → 子节点.Execute(blackboard)
        → ...
```

类比：相当于每 0.1 秒问一次"现在该干什么？"——而不是"当某事发生时才响应"。

### 2.2 每个节点返回三种结果之一

```csharp
public enum BTResult
{
    Success,   // 这个节点做完了 / 条件满足了
    Failure,   // 这个节点做不了 / 条件不满足
    Running    // 这个节点还在做，下一帧继续
}
```

这是行为树的灵魂：
- **Success 和 Failure** → 让父节点决定下一步做什么
- **Running** → 让节点保持控制权，下一帧继续

### 2.3 树的递归执行模式

```
父节点.Execute(blackboard)
  ├─ 如果是组合节点：逐个 Tick 子节点，根据返回值决定自己返回什么
  ├─ 如果是装饰节点：Tick 唯一子节点，可能修改其返回值
  ├─ 如果是动作节点：执行具体行为，返回 Success/Running/Failure
  └─ 如果是条件节点：判断条件，返回 Success/Failure（条件从不返回 Running）
```

---

## 3. 四大节点类型详解

### 3.1 组合节点 (Composite) — 有多个子节点

组合节点**不做事**，只决定**按什么顺序 / 什么逻辑**执行子节点。

---

#### Sequence（顺序节点）— 相当于 AND 逻辑

**执行逻辑**：从左到右依次执行子节点。**全部 Success 才返回 Success，任一 Failure 立即返回 Failure。**

```
Sequence ──→ [检查有目标?] ──→ [靠近目标] ──→ [播放攻击动画]
                 │                  │               │
               Success?            Success?         Success?
                 ↓ No              ↓ No             ↓ No
               Failure            Failure          Failure
```

**伪代码**：
```
for (i = runningIndex; i < children.Count; i++)
{
    result = children[i].Execute()
    if (result == Failure)   { runningIndex=0; return Failure }  // 一个失败全部失败
    if (result == Running)   { runningIndex=i; return Running }  // 停下来等它完成
}
runningIndex = 0; return Success  // 全部走完 → 成功
```

**关键：`runningIndex`** 记住上一次哪个子节点返回了 Running，下一帧从它继续，**不会重复执行已经成功的子节点**。

**使用场景**："先检查条件，再执行动作"——行为树里最常见的节点。

```
巡逻 = Sequence [ 没有目标? → 沿着路点走 ]
攻击 = Sequence [ 有目标? → 靠近目标 → 播放攻击动画 → 等待冷却 ]
```

---

#### Selector（选择节点）— 相当于 OR 逻辑

**执行逻辑**：从左到右依次执行。**任一 Success 立即返回 Success，全部 Failure 才返回 Failure。**

```
Selector ──→ [攻击?] ──→ [追击?] ──→ [巡逻?]
               │              │              │
             范围内?         看到?         默认
               ↓ Yes         ↓ Yes         ↓
             Success        Success       Success
```

**伪代码**：
```
for (i = runningIndex; i < children.Count; i++)
{
    result = children[i].Execute()
    if (result == Success)   { runningIndex=0; return Success }  // 一个成功 → 选这个
    if (result == Running)   { runningIndex=i; return Running }  // 停下来等它完成
}
runningIndex = 0; return Failure  // 全失败 → 没得选
```

**使用场景**：**优先级决策**。左边优先级最高，右边是 fallback。

```
怪物的顶层几乎一定是个 Selector：
Selector [
    死亡? → 播死亡动画          // 优先级最高：死了就别干别的
    受击? → 播放受击动画        // 第二优先级
    攻击范围? → 攻击            // 第三优先级
    追击范围? → 追击            // 第四优先级
    默认巡逻                     // 最低优先级：什么都没发生就巡逻
]
```

**为什么要用 Selector 而不是 if-else？**

因为 Selector 支持 Running 的中断恢复——如果「攻击」节点返回 Running（正在播动画），下一帧 Selector 会直接跳到攻击子节点继续等它完成，而不是从头检查条件跳到「追击」。

---

#### Parallel（并行节点）— 同时执行

**执行逻辑**：同时 Tick 所有子节点。可以配置策略：
- "全部 Success 才算 Success"
- "任意一个 Success 立即返回"

**使用场景**："一边走路一边播放动画"、"攻击的同时播放音效"。

---

#### RandomSelector（随机选择）

**执行逻辑**：每次打乱子节点顺序，然后按 Selector 逻辑执行。

**使用场景**：怪物有多个可用攻击时随机选一个——"有时候砍，有时候刺，有时候踢"。

---

### 3.2 装饰节点 (Decorator) — 只有 1 个子节点

装饰节点**包装**一个子节点，修改它的行为或返回值。像个滤镜。

---

#### Inverter（取反）

```
子节点返回 Success → 向上返回 Failure
子节点返回 Failure → 向上返回 Success
子节点返回 Running → 保持 Running（透传）
```

**使用场景**：
```
Sequence [
    Inverter [ 玩家在攻击范围内? ]   // ← 如果玩家"不"在攻击范围内
    追击玩家                          // ← 就去追击
]
```

---

#### Repeater（重复）

**执行逻辑**：重复执行子节点指定次数（或无限循环）。子节点 Success/Failure 后重新触发，子节点返回 Running 时等待。

**使用场景**：
```
Repeater(无限) [
    Sequence [
        巡逻路点A → 等待2秒 → 巡逻路点B → 等待2秒
    ]
]
```

---

#### Succeeder（强制成功）

**执行逻辑**：无论子节点返回什么，都替换为 Success（Running 除外）。

**使用场景**：装饰不重要的分支，确保它不会导致父节点失败。

---

#### Cooldown（冷却）

**执行逻辑**：子节点 Success 后开始计时，冷却期间直接返回 Failure。

**使用场景**：
```
Cooldown(3秒) [
    攻击
]
```
防止怪物每帧都攻击——攻击一次后必须等 3 秒。

---

### 3.3 动作节点 (Action) — 叶子节点，做事情

动作节点**没有子节点**，它的 `OnExecute()` 执行具体操作：

| 节点 | 做什么 | 典型返回值模式 |
|------|-------|--------------|
| `BTWait` | 等待 N 秒 | Running... → Success |
| `BTSetAnimatorTrigger` | 设置 Animator 的 Trigger 参数 | Success（一帧完成） |
| `BTPlayAnimation` | 播放动画并等待结束 | Running... → Success |
| `BTFaceTarget` | 旋转面向目标 | Running... → Success |
| `BTDebugLog` | 打印调试日志 | Success |

---

### 3.4 条件节点 (Condition) — 叶子节点，做判断

条件节点**没有子节点**，只判断条件并立即返回 Success/Failure（**永远不返回 Running**）：

| 节点 | 判断什么 |
|------|---------|
| `BTHasTarget` | Blackboard 里有没有设置 target？ |
| `BTIsTargetInRange` | 目标距离 < 配置的阈值？ |
| `BTIsHealthBelow` | 血量 < 某个百分比？ |
| `BTIsAnimationDone` | 当前动画的 normalizedTime >= 某个值？ |
| `BTBlackboardCondition` | 通用：比较黑板两个键的值（> < == !=） |

---

## 4. 完整行为树示例

一个怪物的完整行为树（伪代码形式）：

```
Selector [                              ← 顶层：优先级决策
    Sequence [                          ← 优先级1：死亡
        血量<=0?
        播放死亡动画
        等待5秒
        销毁自身
    ]
    Sequence [                          ← 优先级2：受击
        刚受到伤害?
        播放受击动画
        Wait(受击僵直时间)
    ]
    Sequence [                          ← 优先级3：攻击
        目标存在?
        目标在攻击范围2m内?
        Cooldown(2秒) [                 ← 攻击后冷却
            Sequence [
                面向目标
                播放攻击动画
            ]
        ]
    ]
    Sequence [                          ← 优先级4：追击
        目标存在?
        目标在视野范围15m内?
        Sequence [
            设置Animator Move=true
            面向目标
            移动到目标位置
        ]
    ]
    Sequence [                          ← 优先级5：巡逻（默认行为）
        Repeater(无限) [
            等待3秒                     ← 在每个路点停留
            随机选下一个路点
            设置Animator Move=true
            移动到路点
            等待2秒
            设置Animator Move=false
        ]
    ]
]
```

### 每帧 Tick 的执行过程（假设怪物在巡逻，远处出现了玩家）

**第 1 帧**：
```
根 Selector
  → 死亡? → Failure（血量>0）
  → 受击? → Failure（没受击）
  → 攻击? → 目标存在? Success → 目标在2m内? Failure（距离太远）→ Failure
  → 追击? → 目标存在? Success → 在15m内? Success → 开始追击（返回 Running）
  → 后面的巡逻不再检查
根 Selector 返回 Running
```

**第 2 帧 ~ 第 N 帧**：
```
根 Selector
  → 死亡? → Failure
  → 受击? → Failure
  → 攻击? → 目标在2m内? Failure（还没跑到）
  → 追击? → 继续执行（还在 Running）
根 Selector 返回 Running
```

**第 N+1 帧（跑到了攻击范围）**：
```
根 Selector
  → 死亡? → Failure
  → 受击? → Failure
  → 攻击? → 目标存在? Success → 目标在2m内? Success → 面向目标 → 播放攻击 → Running
  → （前面成功了，Selector 不往下走了）
根 Selector 返回 Running
```

**关键理解**：
- 每一帧都**从头评估条件**（死亡检测在第一帧就执行了，不是被跳过的）
- 但因为追击一直在 Running，Selector 会跳到追击继续执行——**不是重新从头执行**
- 当攻击条件满足时（距离够了），攻击的优先级更高（在追击左边），所以会自动「打断」追击切到攻击

**这就是行为树比 FSM 优雅的地方**：你不用手动管理「追击中 → 发现距离够了 → 切攻击」这种跨状态转换，树每一帧都评估优先级，自动处理条件变化。

---

## 5. Blackboard（黑板）是什么？

黑板是行为树的**共享内存**——节点之间不直接通信，而是通过黑板读写数据。

```
[条件节点: 目标在范围内?]  ←读取──  Blackboard  ──写入→ [动作节点: 锁定目标]
                               │
                        "target" = Player
                        "attackRange" = 2.0
                        "healthPercent" = 0.8
                        "_animator" = Animator组件
```

**为什么需要黑板**：
- 条件节点和动作节点之间传递数据（"我发现了谁"→"攻击谁"）
- 不同子树之间共享状态（"追击"子树和"攻击"子树都需要 target）
- 编辑器可视化配置（策划可以在面板上改 `attackRange` 值，不用改代码）

**自动绑定的内置键**（构造 Blackboard 时自动填入）：
| 键名 | 值 | 来源 |
|------|---|------|
| `_owner` | 怪物 GameObject | `BehaviorTreeRunner.gameObject` |
| `_transform` | 怪物 Transform | `gameObject.transform` |
| `_animator` | 怪物 Animator | `GetComponent<Animator>()` |

**用户自定义键**（在 BTBlackboardSO 里配置）：
| 键名 | 类型 | 用途 |
|------|------|------|
| `target` | Transform | 当前追击/攻击目标 |
| `patrolPoints` | Transform[] | 巡逻路点列表 |
| `attackRange` | float | 攻击触发距离 |
| `chaseRange` | float | 追击触发距离 |

---

## 6. 序列化/反序列化流程

行为树从编辑器到运行时的完整数据流：

```
┌─────────────────────────────────────────────────────────────────┐
│ 编辑器中 GraphView 画布上画的树                                     │
│                                                                 │
│   [Selector]                                         (蓝色)     │
│   ├── [Sequence: 攻击]                    (蓝色)                  │
│   │     ├── [BTHasTarget]                 (橙色)                  │
│   │     └── [BTIsTargetInRange]           (橙色)                  │
│   │           └── range=2.0                                         │
│   └── [Sequence: 巡逻]                    (蓝色)                  │
│         └── [BTWait]                      (绿色)                  │
│               └── Duration=3.0                                      │
│                                                                 │
└──────────────┬──────────────────────────────────────────────────┘
               │ 点击 Save → 遍历 GraphView 所有节点和连线
               ▼
┌─────────────────────────────────────────────────────────────────┐
│ BehaviorTreeSO.asset（ScriptableObject，存在 Assets/SO/ 下）      │
│                                                                 │
│ _nodes: [                                                        │
│   {                                                              │
│     Id: "a1b2c3",                                                │
│     TypeName: "AI.BehaviorTree.Composites.BTSelector",           │
│     Position: (0, 0),                                            │
│     JsonData: "",                                                │
│     ChildIds: ["d4e5f6", "g7h8i9"]                               │
│   },                                                              │
│   {                                                              │
│     Id: "d4e5f6",                                                │
│     TypeName: "AI.BehaviorTree.Composites.BTSequence",           │
│     Position: (150, 0),                                          │
│     JsonData: "",                                                │
│     ChildIds: ["j0k1l2", "m3n4o5"]                               │
│   },                                                              │
│   {                                                              │
│     Id: "j0k1l2",                                                │
│     TypeName: "AI.BehaviorTree.Conditions.BTHasTarget",          │
│     Position: (300, -50),                                        │
│     JsonData: "",                                                │
│     ChildIds: []        ← 条件节点没有子节点                        │
│   },                                                              │
│   {                                                              │
│     Id: "m3n4o5",                                                │
│     TypeName: "AI.BehaviorTree.Conditions.BTIsTargetInRange",    │
│     Position: (300, 50),                                         │
│     JsonData: "{\"Range\":2.0}",   ← 参数以 JSON 存储              │
│     ChildIds: []                                                  │
│   },                                                              │
│   ...                                                             │
│ ]                                                                │
│ _rootNodeId: "a1b2c3"                                            │
│ _blackboardAsset: 引用一个 BTBlackboardSO                         │
│                                                                 │
└──────────────┬──────────────────────────────────────────────────┘
               │ 运行时：Awake() 调用 BuildTree()
               ▼
┌─────────────────────────────────────────────────────────────────┐
│ BehaviorTreeRunner（MonoBehaviour，挂载在怪物 GameObject 上）        │
│                                                                 │
│ BuildTree() 只执行一次：                                           │
│   1. 读取 SO._nodes 列表                                          │
│   2. 遍历每个 NodeEntry：                                          │
│      - 根据 TypeName 反射创建实例：new BTHasTarget()              │
│      - 有 JsonData？→ DeserializeData(json) 填入 Data 字段       │
│      - 存入 nodeMap[entry.Id]                                     │
│   3. 遍历 ChildIds：                                              │
│      - parent.AddChild(nodeMap[childId])  建立引用关系            │
│   4. _rootNode = nodeMap[_rootNodeId]                             │
│                                                                 │
│ Update() 每帧执行：                                                │
│   _tickTimer += Time.deltaTime                                    │
│   if (_tickTimer >= 0.1f)    // 每 0.1 秒 Tick 一次               │
│       _rootNode.Execute(_blackboard)   ← 纯 C# 对象树递归调用     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 关键澄清

- **SO 只在 Awake() 时读一次**——用来构造运行时的 BTNode C# 对象树
- **运行时完全不碰 JSON**——每个节点的 Data 在 BuildTree 时反序列化一次，之后存在 C# 对象字段里
- **每帧 Tick 是纯 C# 调用**——遍历的是已经构建好的 BTNode 对象树，不是 SO，不涉及任何磁盘 IO 或 JSON 解析
- **SO 是纯粹的数据载体**——就像 `ComboConfigSO` 存连招配置一样，编辑器负责写，运行时负责读

---

## 7. 与项目现有系统的融合

| 现有系统 | 行为树如何访问 |
|---------|-------------|
| **Animator** | Blackboard 自动绑定 `_animator`，动作节点调用 `SetTrigger()` / `SetBool()` |
| **AnimationEnterBehaviour** | 如果怪物用 FSM + Animator Controller，BT 设 Trigger 后 Animator 驱动状态切换，和玩家完全一样 |
| **LockOnManager** | `LockOnManager.Instance` 直接访问，条件节点检测 `IsLockedOn` |
| **LockOnTarget** | 条件节点遍历 `LockOnTarget.ActiveTargets` 查找视野内目标 |
| **TimerManager** | BTWait 自维护计时器（零 GC 分配）；需要跨节点共享的计时器才用 TimerManager |
| **EventBus** | 动作节点通过 `EventBus.Instance.Emit()` 发事件给 UI/VFX/音频 |
| **HitPauseManager** | 受击节点调用 `HitPauseManager.Instance.Trigger()` |
| **现有 FSM 架构** | 可选——行为树不强制怪物用 FSM。简单怪物用 BT 直接驱动 Animator，复杂怪物可以让 BT 设置 Animator 参数 → FSM 响应 → AnimationEnterBehaviour 路由 |

---

## 8. 前置知识清单

在开始写代码之前，确保你理解了以下概念：

- [ ] **Tick 驱动**：树每帧/每几帧从根节点重新 Tick，不是事件驱动
- [ ] **三态返回值**：Success（做完了）、Failure（做不了）、Running（还在做）
- [ ] **Sequence**：AND 逻辑，全成功才成功，遇失败即停
- [ ] **Selector**：OR 逻辑，优先级决策，遇成功即停
- [ ] **runningIndex**：组合节点记住上次 Running 的子节点索引，下帧恢复
- [ ] **Decorator 包装模式**：修改唯一子节点的返回值或行为
- [ ] **条件节点永远不返回 Running**：判断是瞬时的
- [ ] **动作节点可以返回 Running**：等待动画/计时器完成
- [ ] **Blackboard 是共享内存**：节点之间通过键值对传递数据
- [ ] **SO 只是数据载体**：运行时是纯 C# 对象树，不涉及 JSON/磁盘 IO
- [ ] **`[BTNode]` Attribute 自动注册**：新增节点类型不需要手动注册到任何地方

---

## 9. 推荐学习资源

1. **BehaviorTree.CPP 文档**（行为树最经典的 C++ 实现，概念通用）: https://www.behaviortree.dev/
2. **Game AI Pro 第 2 章 — Behavior Trees**（Isla 2005 Halo 2 的行为树设计原文）
3. **Unity GraphView API**（编辑器部分需要的知识）: Unity 手册搜索 `GraphView`

---

> 下一步：吃透本文档后，可以开始 Phase 1（纯运行时，不需要编辑器）——只写 BTNode + Sequence + Selector + Wait + DebugLog，手动填 SO 数据验证 Tick 逻辑正确。
