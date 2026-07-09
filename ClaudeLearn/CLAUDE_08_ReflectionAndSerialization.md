# CLAUDE_08 — 反射、序列化与行为树开发笔记

> 日期：2026-07-09
> 前置：[CLAUDE_07_BehaviorTree.md](CLAUDE_07_BehaviorTree.md)

---

## 一、序列化 / 反序列化

### 是什么

```
序列化（保存）：
  C# 对象  ──→  字符串/二进制  ──→  磁盘文件
  WaitData { Duration: 2.0 }  ──→  "{\"Duration\":2.0}"  ──→  .asset

反序列化（加载）：
  磁盘文件  ──→  字符串/二进制  ──→  C# 对象
  .asset  ──→  "{\"Duration\":2.0}"  ──→  WaitData { Duration: 2.0 }
```

### Unity 自动帮你做 vs 需要自己做

| 场景 | 谁负责 | 原因 |
|------|-------|------|
| `ComboConfigSO` / `CharacterDataSO` | Unity 自动 | 字段类型编译时确定（`ComboStepData[]`），Unity 认识 |
| `BehaviorTreeSO._nodes` 列表 | Unity 自动 | 类型是 `List<NodeEntry>`，Unity 认识 |
| `NodeEntry.JsonData` 字符串 → `WaitData` | **你手动** | Unity 只知道它是 `string`，不知道里面存的是什么结构 |

### 为什么 JsonData 需要手动反序列化

`NodeEntry` 是一个**通用容器**，所有节点类型共用：

```csharp
// 同一个 JsonData 字段，不同节点存的是完全不同的结构
BTWait:        JsonData = "{\"Duration\":2.0}"
BTDebugLog:    JsonData = "{\"Message\":\"开始执行\"}"
BTFaceTarget:  JsonData = "{\"RotateSpeed\":720.0,\"TargetKey\":\"player\"}"
```

Unity 的序列化不支持"根据另一个字段的值决定怎么解析"——必须运行时自己处理。

### 我们的序列化 / 反序列化实现

```csharp
// 序列化：C# 对象 → JSON 字符串（存盘时用）
public string SerializeData() => JsonUtility.ToJson(Data);

// 反序列化：JSON 字符串 → C# 对象（加载时用）
public void DeserializeData(string json)
{
    if (!string.IsNullOrEmpty(json))
        Data = JsonUtility.FromJson<T>(json) ?? new T();
}
```

这两个方法只在 `BTAction<T>` 和 `BTCondition<T>` 里写了一次，所有叶子节点自动继承。

---

## 二、反射

### 是什么

**用字符串在运行时找到类型、创建对象、调用方法**——而不是编译时写死。

```csharp
// 平常写代码：编译时确定类型
var node = new BTWait();

// 反射：运行时根据字符串找类型
string typeName = "AI.BehaviourTree.BTWait";
Type type = Type.GetType(typeName);              // 字符串 → Type 对象
object obj = Activator.CreateInstance(type);      // Type 对象 → 创建实例
```

### 核心 API

| API | 作用 | 类比 |
|-----|------|------|
| `Type.GetType("全名")` | 字符串 → Type | 黄页：根据名字查号 |
| `Activator.CreateInstance(type)` | Type → new 实例 | 拨号：叫对方出来 |
| `type.GetMethod("方法名")` | Type → 找到方法 | 查手册：这机器怎么操作 |
| `method.Invoke(obj, args)` | 在 obj 上调用这个方法 | 动手：按按钮 |

### 在行为树里哪里用

**只在 `BuildTree()` 里用，Awake 时调用一次，不在 Update 里。零性能问题。**

```csharp
// BuildTree() 里的反射流程（每个节点跑一次）
Type type = Type.GetType(entry.TypeName);                    // 1. 字符串找类型
BTNode node = Activator.CreateInstance(type) as BTNode;      // 2. 创建对象
var method = type.GetMethod("DeserializeData");               // 3. 找反序列化方法
method.Invoke(node, new object[] { entry.JsonData });         // 4. 调用它
```

### 为什么用反射而不是 switch

不用反射就要手写几十个 case，每加一个新节点类型都要改 BuildTree。用反射，新节点零维护——TypeName 字符串存对了就行。

### 踩过的坑

1. **`BindingFlags` 枚举**：`GetMethod` 默认找 Static | Instance | Public，如果以后有重载需要指定参数类型数组
2. **`Activator.CreateInstance` 返回 `object`**：需要 `as BTNode` 转成基类
3. **`Invoke` 的参数**：`new object[] { arg }` 用 object 数组包起来，不是直接传

---

## 三、泛型基础

### `TData` 是什么

```csharp
public abstract class BTAction<TData> : BTNode where TData : new()
//                            ↑                       ↑
//                       类型占位符                 约束：TData 必须能 new()
{
    public TData Data = new TData();   // 泛型字段
}
```

- `TData` 就是 `T`，换了个名字，语义上表示"这是 Data 字段的类型"
- `where TData : new()` = "TData 必须有无参构造函数"，因为基类里要写 `new TData()`

### 能 new 和不能 new

| 能 new() | 不能 new() |
|----------|-----------|
| 所有 struct（int/float/Vector3…） | `string`（没有无参构造） |
| 有无参构造的 class | 抽象类、接口 |
| `WaitData`、`DebugLogData` 等自定义 struct | `BTNode`（抽象类） |

---

## 四、字典与 TryGetValue

### 基本用法

```csharp
Dictionary<string, BTNode> _nodeMap = new();

// 存
_nodeMap["abc"] = someNode;

// 取（安全版）
if (_nodeMap.TryGetValue("abc", out var node))
{
    // 找到了，node 是值
}
else
{
    // 没找到，跳过
}

// 取（不安全版——找不到会抛异常）
var node = _nodeMap["abc"];  // KeyNotFoundException
```

### TryGetValue 的 out 参数

`out var parent` 是输出参数——方法内部把结果写入 `parent`，调用方直接用，不用提前声明变量。

---

## 五、属性 vs 字段 vs `internal set`

### 自动属性

```csharp
// 自动属性（有 get/set）
public string Guid { get; internal set; }

// 等价于手写：
private string _guid;
public string Guid
{
    get { return _guid; }
    internal set { _guid = value; }
}
```

### `internal` 的含义

- `internal set` = 只有同程序集（同一个编译产物 dll）里的代码能赋值
- 当前项目没有 `.asmdef` 文件，全部代码在一个程序集里，所以项目内谁都能赋值
- 语义上表达"这个值由系统内部管理，外部只读"

### 属性 vs 字段

```csharp
public string RootNodeId { get; set; }   // 属性：以后可加逻辑
public string RootNodeId;                 // 字段：简单但改不了
```

外部使用一模一样，属性以后想加校验不需要改调用方。

### `IReadOnlyList` 包装

```csharp
public IReadOnlyList<NodeEntry> Nodes => _nodes;  // 外部只能读，不能 Clear/Add
```

比直接暴露 `List<T>` 安全。

---

## 六、Attribute（自定义标签）

### C# 的 Attribute 机制

```csharp
// 1. 定义标签（继承 Attribute）
public class BTNodeAttribute : Attribute
{
    public string Name;
    public string Category;
    public BTNodeAttribute(string name, string category) { ... }
}

// 2. 使用标签（Attribute 后缀可以省略）
[BTNode("等待", "Action/时间")]     // ← 等价于 [BTNodeAttribute(...)]
public class BTWait : BTAction<WaitData> { }

// 3. 编辑器里通过 TypeCache 扫描所有贴了 [BTNode] 的类
```

Unity 内置的 `[SerializeField]`、`[Serializable]`、`[CreateAssetMenu]` 都是同一个原理。

---

## 七、`Time.time` vs `Time.deltaTime`（计时方式）

### 踩坑记录

```csharp
// ❌ 用 deltaTime 累加 —— 精度受 Tick 频率影响
_elapsed += Time.deltaTime;

// ✅ 用时间戳比较 —— 与 Tick 间隔无关
_startTime = Time.time;
if (Time.time - _startTime >= duration) Success;
```

`+= Time.deltaTime` 只在 Tick 的瞬间加值。如果 Tick 间隔 0.1 秒，每 Tick 只加 0.016 秒（一帧的时间），而不是加 0.1 秒。**Wait 的实际等待时间 = 期望时间 × (DeltaTime / TickInterval)**，误差巨大。

**结论：需要精确计时的节点用时间戳比较，不要累加 deltaTime。**

---

## 八、核心链路回顾

Phase 1 验证通过的完整流程：

```
编辑/代码阶段：
  CreateTestTree() → ScriptableObject 内存中构造
    TypeName = typeof(BTWait).FullName        ← 编译时自动算类型名
    JsonData = "{\"Duration\":2.0}"           ← 参数写进 JSON 字符串

运行时：
  Awake → BuildTree()
    读 SO._nodes 列表
    → Type.GetType(TypeName)                   ← 反射找类型
    → Activator.CreateInstance                 ← 反射创建对象
    → GetMethod("DeserializeData").Invoke()    ← 反射反序列化参数
    → nodeMap[entry.Id] = node                 ← 存入字典
    → WireChild(parent, child)                 ← 建立父子连线
    → RootNode = nodeMap[RootNodeId]           ← 设置根节点

  Update（每 0.1 秒）:
    → RootNode.Execute(Blackboard)             ← 纯 C# 虚方法调用，零反射
      → Sequence.OnExecute                     ← for 循环推进子节点
        → Wait.OnExecute                       ← 时间戳比较
        → DebugLog.OnExecute                   ← 打印日志
```

---

## 九、代码文件结构

当前已完成的文件（17 个）：

```
Assets/Scripts/AI/BehaviorTree/
├── Core/
│   ├── BTNode.cs                抽象基类 + BTResult 枚举
│   ├── BTAction.cs              叶子-动作泛型基类（含序列化）
│   ├── BTCondition.cs           叶子-条件泛型基类（含序列化）
│   ├── BTComposite.cs           组合节点基类（Children + runningIndex）
│   ├── BTDecorator.cs           装饰节点基类（单个 Child）
│   ├── BTNodeAttribute.cs       [BTNode] 自定义标签
│   ├── Blackboard.cs            运行时键值存储
│   ├── BTBlackboardSO.cs        黑板定义 ScriptableObject + 枚举
│   ├── BehaviorTreeSO.cs        行为树资产 ScriptableObject + NodeEntry
│   └── BehaviorTreeRunner.cs    执行器 MonoBehaviour（BuildTree + Tick）
├── Composites(组合节点)/
│   ├── BTSequence.cs            顺序节点
│   └── BTSelector.cs            选择节点
├── Decorators(装饰节点)/
│   └── BTInverter.cs            取反节点
└── Actions(动作节点)/
    ├── BTDebugLog.cs            调试打印
    └── BTWait.cs                等待（时间戳比较）
```
