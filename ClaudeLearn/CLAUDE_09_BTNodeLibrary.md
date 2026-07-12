# CLAUDE_09 — 行为树节点库填充（Phase 2）

> 日期：2026-07-12
> 前置：[CLAUDE_07_BehaviorTree.md](CLAUDE_07_BehaviorTree.md) | [CLAUDE_08_ReflectionAndSerialization.md](CLAUDE_08_ReflectionAndSerialization.md)

---

## 一、Phase 1→Phase 2：我们在哪个位置

Phase 1 做完了**运行时核心引擎**——`BTNode` 基类 + `BTComposite`/`BTDecorator`/`BTAction<T>`/`BTCondition<T>` 四条继承链 + `BehaviorTreeSO` 序列化 + `BehaviorTreeRunner` 反射构建 + `Blackboard` 黑板。通过 `CreateTestTree()` 验证了完整链路能跑通。

Phase 2 的目标：**填充节点库，让行为树能驱动一个真实怪物**。

```
Phase 1: 引擎启动成功（一个 Sequence[Log→Wait→Log] 验证）
Phase 2: 装上轮子方向盘（条件/装饰/动作节点库）    ← 我们在这里
Phase 3: 视觉编辑器（GraphView）                   ← 未来
```

---

## 二、核心架构回顾

### 2.1 类继承金字塔

```
BTNode                          ← 所有节点的根：Execute() + OnExecute() + OnEnter/OnExit
├── BTComposite                 ← 非泛型，多个子节点，WireChild 通过 is 匹配
│   ├── BTSelector              ← 优先级决策（遇成功即停）
│   └── BTSequence              ← 顺序执行（遇失败即停）
│
├── BTDecorator                 ← 非泛型，单个子节点，WireChild 通过 is 匹配
│   ├── BTInverter              ← 取反（无参数，直接继承）
│   ├── BTSucceeder             ← 强制成功（无参数，直接继承）
│   └── BTDecorator<T>          ← 泛型版本（新加！有参数的装饰节点继承这个）
│       ├── BTRepeater          ← 重复执行
│       └── BTCooldown          ← 冷却控制
│
├── BTAction<T>                 ← 泛型动作叶子
│   ├── BTWait                  ← 等待 N 秒
│   └── BTDebugLog              ← 控制台打印
│
└── BTCondition<T>              ← 泛型条件叶子
    ├── BTHasTarget             ← 黑板中是否有目标
    ├── BTIsTargetInRange       ← 目标是否在范围内
    └── BTIsAnimationDone       ← 动画是否播完
```

### 2.2 为什么 BTDecorator<T> 是后来加的

`BTDecorator` 不是泛型——因为 `BehaviorTreeRunner.WireChild()` 需要用 `is BTDecorator` 匹配所有装饰节点。`is` 关键字只能匹配具体类型，不能匹配开放泛型 `BTDecorator<>`。

所以 **非泛型 `BTDecorator` 负责面向 `WireChild`**（提供 `SetChild` 方法和 `is` 匹配目标），**泛型 `BTDecorator<T>` 负责面向子类**（提供 `T Data` 和自动序列化）。

这两个类的职责分工是：
| 类 | 职责 | 谁用它 |
|----|------|-------|
| `BTDecorator` | 提供 `Child` 字段 + `SetChild()` + `is` 匹配 | `WireChild()` |
| `BTDecorator<T>` | 提供 `T Data` + 自动序列化 | `BTRepeater`、`BTCooldown` 等有参数的装饰节点 |

和 BTAction 的对比——BTAction 不需要非泛型版本，因为 `WireChild` 不碰叶子节点。

---

## 三、数据与逻辑分离模式（核心设计模式）

这是整个行为树框架里最重要的架构约定。每个叶子/装饰节点都遵循这个**两层结构**。

### 3.1 为什么拆成两层

Unity 的序列化能力有限。`NodeEntry` 是一个通用容器（存 `TypeName`、`JsonData`、`ChildIds`），所有节点类型共用。但每个节点需要的参数完全不同：

```
BTWait:        JsonData = "{\"Duration\":2.0}"        → WaitData struct
BTDebugLog:    JsonData = "{\"Message\":\"开始\"}"     → DebugLogData struct
BTIsTargetInRange: JsonData = "{\"Range\":2.0,\"TargetKey\":\"target\"}" → RangeData struct
BTRepeater:    JsonData = "{\"RepeatCount\":0}"        → RepeaterData struct
BTCooldown:    JsonData = "{\"CooldownTime\":2.0}"    → CooldownData struct
```

同一个 `JsonData` 字段，不同节点存完全不同的结构。Unity 不支持"根据 TypeName 决定怎么解析 JsonData"——必须运行时手动做。

### 3.2 模式模板

```csharp
// ========== 数据层：纯数据容器 ==========
[System.Serializable]                    // Unity 序列化必需
public struct XxxData                    // struct 不是 class（栈分配，零 GC）
{
    [Tooltip("参数说明")]                // 编辑器提示
    public float SomeValue;              // 只声明字段，不写方法
    public string SomeKey;
}

// ========== 逻辑层：行为实现 ==========
[BTNode("显示名", "Category/子类别", "描述")]  // 编辑器自动发现
public class BTXxx : BTAction<XxxData>   // 或 BTCondition 或 BTDecorator<T>
{
    // Data 字段由基类提供，类型 = XxxData

    public override void OnEnter(Blackboard bb)
    {
        // 可选：记录开始时间、重置计数器等
    }

    protected override BTResult OnExecute(Blackboard bb)
    {
        // 通过 Data.SomeValue 访问配置参数
        // 通过 bb.Get<T>("key") 读写黑板
        return BTResult.Success;
    }

    // OnExit / ResetNode 按需覆写
}
```

### 3.3 数据流全链路

```
1️⃣ 编辑/代码阶段：手动填 NodeEntry
   ┌─────────────────────────────────────────┐
   │ new NodeEntry {                         │
   │     Id = "3",                           │
   │     TypeName = typeof(BTWait).FullName, │  ← 编译时自动算类型名
   │     JsonData = "{\"Duration\":2.0}",    │  ← 参数写进 JSON 字符串
   │     ChildIds = new List<string>()       │
   │ }                                       │
   └─────────────────────────────────────────┘

2️⃣ 存储层：写入 BehaviorTreeSO.asset
   ┌─────────────────────────────────────────┐
   │ NodeEntry 存的是扁平列表                 │
   │ 父子关系靠 ChildIds 字符串引用           │
   │ Unity 序列化 List<NodeEntry> → 存盘      │
   └─────────────────────────────────────────┘

3️⃣ Awake 时 BuildTree()：JSON → C# 对象（只跑一次）
   ┌─────────────────────────────────────────┐
   │ Type.GetType("...BTWait")               │  ← 反射：字符串→Type
   │ Activator.CreateInstance(type)          │  ← 反射：Type→对象
   │ DeserializeData(entry.JsonData)         │  ← 反射：JSON→Data
   │   └→ JsonUtility.FromJson<WaitData>()   │
   │       └→ new WaitData{Duration=2.0}     │  ← 存入 Data 字段
   │ _nodeMap[entry.Id] = node               │
   └─────────────────────────────────────────┘

4️⃣ WireChild：建立对象引用链
   ┌─────────────────────────────────────────┐
   │ 遍历 ChildIds → _nodeMap 查出来          │
   │ WireChild(parent, child)                │
   │   → is BTComposite → AddChild()         │
   │   → is BTDecorator → SetChild()         │
   └─────────────────────────────────────────┘

5️⃣ 运行时 Tick：纯 C# 对象树（每 Tick 都跑）
   ┌─────────────────────────────────────────┐
   │ RootNode.Execute(Blackboard)            │  ← 纯虚方法调用
   │   → Selector.OnExecute → 遍历 Children  │  ← for 循环
   │     → Wait.OnExecute                    │  ← Data.Duration 是 C# 字段
   │       → Time.time - _startTime >= Data.Duration │  ← 纯值比较
   └─────────────────────────────────────────┘
```

---

## 四、三种基类的序列化机制对比

| 基类 | 有泛型 T | SerializeData | DeserializeData | Data 字段 | 子类需要写序列化吗 |
|------|---------|--------------|----------------|----------|------------------|
| `BTAction<T>` | ✅ | 基类自动 | 基类自动 | 基类自动 | ❌ 不需要 |
| `BTCondition<T>` | ✅ | 基类自动 | 基类自动 | 基类自动 | ❌ 不需要 |
| `BTDecorator<T>` | ✅ | 基类自动 | 基类自动 | 基类自动 | ❌ 不需要 |
| `BTDecorator` (非泛型) | ❌ | 不存在 | 不存在 | 不存在 | ✅ 子类手动（参考 BTRepeater 旧版） |

任何有配置参数的节点都应该继承泛型版本，避免重复写序列化样板代码。

---

## 五、本节课新增节点清单

### 5.1 条件节点（3 个）

| 节点 | 文件 | 数据层 | 核心逻辑 |
|------|------|-------|---------|
| `BTHasTarget` | `Conditions(条件节点)/BTHasTarget.cs` | 无（继承 `BTCondition<object>`） | `bb.Has("target")` |
| `BTIsTargetInRange` | `Conditions(条件节点)/BTIsTargetInRange.cs` | `RangeData { Range, TargetKey }` | `Vector3.Distance(self, target) <= Range` |
| `BTIsAnimationDone` | `Conditions(条件节点)/BTIsAnimationDone.cs` | `AnimationDoneData { StateName, Layer, Threshold }` | `normalizedTime >= Threshold && IsName(StateName)` |

**条件节点约定**：永远不返回 Running，立刻给出 Success 或 Failure。

### 5.2 装饰节点（3 个 + 1 个基类）

| 节点 | 文件 | 数据层 | 核心逻辑 |
|------|------|-------|---------|
| `BTDecorator<T>` | `Core/BTDecorator.cs` | — | 泛型装饰器基类，提供 T Data + 自动序列化 |
| `BTSucceeder` | `Decorators(装饰节点)/BTSucceeder.cs` | 无 | Success↔Success, Failure→Success, Running 透传 |
| `BTRepeater` | `Decorators(装饰节点)/BTRepeater.cs` | `RepeaterData { RepeatCount }` | 子节点完成后 Reset 再执行，0=无限 |
| `BTCooldown` | `Decorators(装饰节点)/BTCooldown.cs` | `CooldownData { CooldownTime }` | 子节点 Success 后 N 秒内拒绝执行 |

---

## 六、每个节点的设计要点与踩坑记录

### 6.1 BTHasTarget

- 没有配置参数 → 用 `BTCondition<object>` 作为占位，`object` 满足 `new()` 约束
- 条件节点只判断 Blackboard 键是否存在 → `bb.Has("target")`
- `"target"` 是一个约定字符串键名，和 `BTIsTargetInRange`、后续的 `BTFaceTarget`、`BTDetectTarget` 共用

### 6.2 BTIsTargetInRange

- `TargetKey` 字段允许自定义黑板键名，默认 `"target"`（用 `string.IsNullOrEmpty` 判断）
- 做了三层安全检查：`Has(key)` → `Get<Transform>(key) != null` → `_transform` 存在
- `_transform` 是 [Blackboard.cs](Assets/Scripts/AI/BehaviorTree/Core/Blackboard.cs) 构造器自动写入的内置键，无需手动设置

### 6.3 BTIsAnimationDone

- **必须先 `IsName()` 再 `normalizedTime`**：Animator 过渡期间 `GetCurrentAnimatorStateInfo` 可能返回过渡前的状态。不检查名字会在过渡帧误判
- `Threshold` 默认 0.95 不是 1.0：动画几乎不会精确达到 1.0（过渡/混合会截断），0.95 是业界惯例
- `Layer` 字段：怪物通常只用 Layer 0，保留字段为以后需要时不必改节点代码
- 这个节点是**动画驱动 AI 的关键桥梁**：玩家 FSM 用 AnimationEnter/ExitBehaviour 做动画驱动切换，怪物 BT 用这个条件节点知道"动画播完了"

### 6.4 BTDecorator\<T\>（泛型装饰器基类）

- 为什么现在才加：Phase 1 只有一个无参数的 BTInverter，不需要泛型
- `BTDecorator<T> : BTDecorator`：继承非泛型版本，保持 `is BTDecorator` 的匹配能力
- 代码完全照搬 `BTAction<T>` 的模式（对照 [BTAction.cs](Assets/Scripts/AI/BehaviorTree/Core/BTAction.cs)）

### 6.5 BTSucceeder

- 和 BTInverter 一个目录，都在 `Decorators(装饰节点)/`
- 5 行核心逻辑：`result == Running ? Running : Success`
- 使用场景：可选分支——"巡逻失败也无所谓，别影响上层决策"

### 6.6 BTRepeater

- `Child.ResetNode()` 是最关键的调用——不重置的话 Sequence 的 `_runningIndex` 停在末尾，下一轮不会从第一个子节点重新执行
- 重置调用链：`BTRepeater → Child.ResetNode() → (如果是 Sequence) → _runningIndex=0 → 递归 Children[i].ResetNode() → ...`
- 无限模式永远返回 `Running`——如果返回 Success，Selector 父节点会认为这个分支完成了，跳到下一条分支
- 失败也计数并重置——防止路点丢了之类的一次性失败导致节点永久卡死

### 6.7 BTCooldown

- **只有子节点 Success 才触发冷却**——Failure（条件不满足）和 Running（动画还在播）不触发
- `_lastSuccessTime` 初始值是 `float.MinValue` 而不是 `0`：用 0 的话，游戏开始前 2 秒内 `Time.time - 0 >= 2.0` 为 false，冷却白等
- `OnEnter` 不重置 `_lastSuccessTime`——冷却时间应该跨 Tick 保持。只在 `ResetNode()` 清空

---

## 七、设计模式小结

### 7.1 本次涉及的模式

| 模式 | 项目中的应用 | 好处 |
|------|------------|------|
| **模板方法** | `BTNode.Execute()` 骨架 + 子类 `OnExecute()` 填充 | 新节点只写业务，不管生命周期 |
| **组合模式** | `BTComposite` 把 `List<BTNode>` 当统一接口调用 `Execute()` | 父节点不区分子节点是叶子还是组合 |
| **装饰模式** | `BTDecorator` 包装一个子节点，修改返回值 | 行为过滤/"滤镜" |
| **数据-逻辑分离** | 每个叶子/装饰节点 = `[Serializable] struct` + `class : BTAction<T>` | 序列化逻辑和业务逻辑解耦 |

### 7.2 数据与逻辑分离的具体好处

1. **编辑器友好**：数据层 struct 的字段自动成为编辑器面板上的可配置项
2. **序列化干净**：`JsonUtility.ToJson(Data)` 一行搞定，不掺杂运行时状态
3. **代码可读**：打开一个节点脚本，先看 struct 知道参数，看 OnExecute 知道逻辑
4. **扩展零成本**：加一个新节点 = 写一个 struct + 写一个 OnExecute，无需改 Builder、Runner、SO

---

## 八、当前文件清单

```
Assets/Scripts/AI/BehaviorTree/
├── Core/
│   ├── BTNode.cs                    ← 抽象基类 + BTResult 枚举
│   ├── BTAction.cs                  ← 泛型动作基类（带序列化）
│   ├── BTCondition.cs               ← 泛型条件基类（带序列化）
│   ├── BTComposite.cs               ← 组合节点基类（Children + runningIndex）
│   ├── BTDecorator.cs               ← 装饰节点基类 NEW: + BTDecorator<T>
│   ├── BTNodeAttribute.cs           ← [BTNode] 自定义标签
│   ├── Blackboard.cs                ← 运行时键值存储
│   ├── BTBlackboardSO.cs            ← 黑板定义 ScriptableObject
│   ├── BehaviorTreeSO.cs            ← 行为树资产 ScriptableObject
│   └── BehaviorTreeRunner.cs        ← 执行器 MonoBehaviour
│
├── Composites(组合节点)/
│   ├── BTSelector.cs
│   └── BTSequence.cs
│
├── Decorators(装饰节点)/
│   ├── BTInverter.cs                ← Phase 1
│   ├── BTSucceeder.cs               ← NEW
│   ├── BTRepeater.cs                ← NEW
│   └── BTCooldown.cs                ← NEW
│
├── Actions(动作节点)/
│   ├── BTDebugLog.cs                ← Phase 1
│   └── BTWait.cs                    ← Phase 1
│
├── Conditions(条件节点)/
│   ├── BTHasTarget.cs               ← NEW
│   ├── BTIsTargetInRange.cs         ← NEW
│   └── BTIsAnimationDone.cs         ← NEW
│
└── Editor(编辑器相关)/              ← 空（Phase 3）
```

---

## 九、Blackboard 内置键约定

所有行为树节点通过以下约定键名访问项目系统：

| 键名 | 类型 | 谁写入 | 谁读取 | 说明 |
|------|------|-------|-------|------|
| `_owner` | GameObject | Blackboard 构造器 | 需要实例化/销毁的节点 | 挂载 BehaviorTreeRunner 的 GameObject |
| `_transform` | Transform | Blackboard 构造器 | BTIsTargetInRange, BTFaceTarget | owner.transform |
| `_animator` | Animator | Blackboard 构造器 | BTIsAnimationDone, BTSetAnimatorTrigger | GetComponent\<Animator\>() |
| `target` | Transform | BTDetectTarget（后续） | BTHasTarget, BTIsTargetInRange, BTFaceTarget | 当前追击/攻击目标 |
| `patrolPoints` | Transform[] | 用户手动 Set | 巡逻相关节点 | 巡逻路点列表 |
| `attackRange` | float | 用户手动 Set | BTIsTargetInRange | 攻击触发距离 |

**注意**：用户自定义键（target、patrolPoints 等）需要在 `BTBlackboardSO` 里定义 schema，但当前 `Blackboard` 构造器尚未实现 schema 驱动的自动初始化（待完善）。

---

## 十、Phase 2.5：TargetManager 重构

### 10.1 为什么做

`LockOnTarget.ActiveTargets` 静态列表职责混乱——同时服务"玩家锁定系统"和"怪物 AI 发现系统"。一条数据两条命，改一个影响另一个。

### 10.2 重构方案

```
LockOnTarget（只需管）→ "瞄我哪里"（锁定位移点）
Targetable（新组件）  → "我是谁"（阵营标记）
TargetManager（新单例）→ "所有人都在哪"（全局注册表 + 查询）
```

### 10.3 新增文件

| 文件 | 说明 |
|------|------|
| `Scripts/Enemy/Targetable.cs` | Team 枚举 + OnEnable/OnDisable 自动注册到 TargetManager |
| `Scripts/Manager/TargetManager.cs` | 单例 `Singleton<TargetManager>` + `FindNearest()` 查询 |

### 10.4 改造文件

| 文件 | 改动 |
|------|------|
| `LockOnTarget.cs` | 删 `ActiveTargets` 静态列表、OnEnable/OnDisable，只保留锁定位移 |
| `BTFindNearestTarget.cs` | 数据源从 `LockOnTarget.ActiveTargets` 切到 `TargetManager.Instance`，加阵营过滤 |
| `LockOnManager.cs` | `FindNearestTarget()` 数据源切到 `TargetManager`，**相机参数一行未动** |

---

## 十一、Phase 2.6：第一条怪物行为树（动画驱动）

### 11.1 踩过的坑

| 坑 | 原因 | 修法 |
|----|------|------|
| **怪物飞天** | 动画 clip 的 Root Motion Y 轴推了 transform.position | Y 轴 Bake Into Pose 勾上 |
| **怪物鬼畜** | 树里有 Repeater + BTWait，动画在 Walk/Idle 之间快速来回切 | 删 Repeater 和所有 Wait，让 Selector 每 Tick 自然重新评估 |
| **旋转一顿顿** | `RotateTowards` 固定速率 + Tick 离散导致跳跃式旋转 | 换成 `Quaternion.Slerp` 插值，每 Tick 平滑过渡 |
| **近距离旋转鬼畜** | Slerp 的 t 值恒定，近距离方向变化大导致抽搐 | 加距离衰减：`t *= min(distance/2, 1)` |
| **移动太慢** | `Time.deltaTime`(~0.016s) 远小于 Tick 间隔(0.1s)，位移量只有预期的 1/6 | 用时间戳记录实际 Tick 间隔：`dt = Time.time - _lastTickTime` |

### 11.2 最终行为树

```
Selector [
    ─── 分支1：在1.5m内 → 停 ───
    Sequence [ BTHasTarget → BTIsTargetInRange(1.5m) → BTSetAnimatorBool(false) ]
    ─── 分支2：有目标 → 转身 → 走 ───
    Sequence [ BTFindNearestTarget(15m) → BTFaceTarget(720°/s Slerp) → BTSetAnimatorBool(true) ]
    ─── 分支3：没目标 → 站 ───
    Sequence [ BTSetAnimatorBool(false) ]
]
```

### 11.3 关键设计决策

**移动不由代码推，由动画 Root Motion 驱动。** 行为树只负责三个控制信号：
- `BTFaceTarget` → 确保朝向对（Slerp + 距离衰减）
- `BTSetAnimatorBool("IsMoving", true/false)` → 控制走路/待机动画切换
- Animator 状态机 `Idle ← IsMoving → Walk` → Root Motion 自动位移

行为树的职责从"每帧算位置"降级为"每 Tick 设动画参数"，和 Animator 系统完美解耦。

---

## 十二、当前完整文件清单（更新后）

```
Assets/Scripts/AI/BehaviorTree/
├── Core/
│   ├── BTNode.cs                    ← 抽象基类 + BTResult
│   ├── BTAction.cs                  ← 泛型动作基类
│   ├── BTCondition.cs               ← 泛型条件基类
│   ├── BTComposite.cs               ← 组合节点基类
│   ├── BTDecorator.cs               ← 装饰节点基类 + BTDecorator<T>
│   ├── BTNodeAttribute.cs           ← [BTNode] 标签
│   ├── Blackboard.cs                ← 运行时键值存储
│   ├── BTBlackboardSO.cs            ← 黑板定义 SO
│   ├── BehaviorTreeSO.cs            ← 行为树资产 SO
│   └── BehaviorTreeRunner.cs        ← 执行器 + CreateTestTree(动画驱动)
│
├── Composites(组合节点)/
│   ├── BTSelector.cs
│   └── BTSequence.cs
│
├── Decorators(装饰节点)/
│   ├── BTInverter.cs
│   ├── BTSucceeder.cs
│   ├── BTRepeater.cs
│   └── BTCooldown.cs
│
├── Actions(动作节点)/
│   ├── BTDebugLog.cs
│   ├── BTWait.cs
│   ├── BTSetAnimatorTrigger.cs
│   ├── BTSetAnimatorBool.cs
│   ├── BTFaceTarget.cs              ← Slerp + 距离衰减
│   ├── BTFindNearestTarget.cs       ← 走 TargetManager
│   └── BTMoveTowards.cs             ← 备用（代码位移，当前树未用）
│
├── Conditions(条件节点)/
│   ├── BTHasTarget.cs
│   ├── BTIsTargetInRange.cs
│   ├── BTIsAnimationDone.cs
│   └── BTBlackboardCondition.cs
│
└── Editor(编辑器相关)/              ← 空（Phase 3）

Assets/Scripts/Enemy/
├── LockOnTarget.cs                  ← 精简：只保留锁定位移
└── Targetable.cs                    ← NEW：阵营标记 + 自动注册

Assets/Scripts/Manager/
└── TargetManager.cs                 ← NEW：单例 + 全局注册表 + 查询
```

---

## 十三、已知待实现

- [ ] `BTNavMeshMove` — NavMeshAgent 寻路版移动（替代 BTMoveTowards）
- [ ] `BTParallel` / `BTRandomSelector` — 组合节点
- [ ] GraphView 可视化编辑器（Phase 3）
- [ ] `Blackboard` 构造器从 BTBlackboardSO schema 初始化自定义键

---

## 十四、下一课预告

`CLAUDE_10`：战斗行为树 — 攻击/受击/死亡分支，BTCooldown + Trigger + AnimDone 实战联动
