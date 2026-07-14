# CLAUDE_10 — 行为树可视化编辑器：开发基调

> 日期：2026-07-14
> 前置：[CLAUDE_07](CLAUDE_07_BehaviorTree.md) | [CLAUDE_08](CLAUDE_08_ReflectionAndSerialization.md) | [CLAUDE_09](CLAUDE_09_BTNodeLibrary.md)

---

## 开发者背景与协作方式

开发者是**初级 Unity 开发者，正在学习**。这个编辑器的开发也是一次学习过程。

**协作约定**：
- Claude **逐块生成代码**，每块代码附解释——这段代码干什么、为什么这样写、关键 API 的含义
- 每块代码完成后**停下来交流**，确认开发者理解了再继续
- Claude **记住开发者的反馈**——踩过的坑、纠正过的理解、偏好——写入 memory，下次对话自动加载
- 学习到一个阶段后，开发者可以让 Claude **生成阶段总结**（追加到 CLAUDE 学习文档）
- 目标不只是产出能跑的编辑器，更是理解 **Unity GraphView 编程模型**和**编辑器与运行时分离架构**

---

## 零、一句话定位

> **编辑器是 NodeEntry 列表的可视化编辑器。它不认识 BTNode，只认识 NodeEntry。**

编辑器 = 把 `List<NodeEntry>` 画成节点和连线，让策划/开发者拖拖连连就能产出 `.asset` 文件。运行时 `BuildTree()` 把这个列表反射成 `BTNode` 对象树。

---

## 一、架构全景图：三层单向依赖

```
┌─────────────────────────────────────────────────────────────────┐
│                    编辑器 (GraphView)                           │
│                                                                 │
│  画布上的每个节点    ←→    一个 NodeEntry                        │
│  画布上的每条连线    ←→    一个 ChildIds 条目                     │
│                                                                 │
│  职责：读写 BehaviorTreeSO._nodes (List<NodeEntry>)             │
│  它不知道 BTNode / BTAction / BTSelector 的存在                 │
│  它只知道：这个节点叫什么名字、在画布哪里、参数 JSON 是什么        │
└──────────────────────────┬──────────────────────────────────────┘
                           │ 写入 / 读取
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│              BehaviorTreeSO.asset (ScriptableObject)            │
│                                                                 │
│  _nodes: List<NodeEntry>     ← 扁平列表，这是唯一的数据源        │
│  _rootNodeId: string         ← 根节点 ID                        │
│  _blackboardAsset: BTBlackboardSO                               │
│                                                                 │
│  这是编辑器 ↔ 运行时之间的唯一契约                               │
└──────────────────────────┬──────────────────────────────────────┘
                           │ Awake 时读取（只读一次）
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│              运行时 (BehaviorTreeRunner)                         │
│                                                                 │
│  BuildTree():                                                   │
│    遍历 SO._nodes                                               │
│    → Type.GetType(TypeName)      反射创建 BTNode 对象            │
│    → DeserializeData(JsonData)   反序列化参数                    │
│    → WireChild(parent, child)    建立对象引用链                  │
│    → RootNode                    纯 C# 虚方法调用树              │
│                                                                 │
│  运行时完全不碰 SO（构建完成后），完全不碰编辑器                   │
└─────────────────────────────────────────────────────────────────┘
```

**核心原则**：三层之间只有数据传递，没有代码依赖。编辑器项目可以独立于运行时存在。

---

## 二、数据契约：NodeEntry 的每个字段

```csharp
[Serializable]
public class NodeEntry
{
    public string Id;              // 唯一标识（GUID）
    public string TypeName;        // 完整类型名，如 "AI.BehaviourTree.BTWait"
    public Vector2 Position;       // 编辑器画布坐标（x, y）
    public string JsonData;        // 节点参数 JSON，如 "{\"Duration\":2.0}"
    public List<string> ChildIds;  // 子节点 Id 列表（连线关系）
}
```

### 每个字段从哪来、到哪去：

| 字段 | 编辑器（写入） | 运行时（读取） |
|------|--------------|-------------|
| `Id` | 创建节点时生成 GUID | BuildTree 用作 `nodeMap` 的 key |
| `TypeName` | 用户从节点菜单选择 → `typeof(BTWait).FullName` | `Type.GetType()` 反射创建对象 |
| `Position` | 用户在画布上拖拽的位置 | **不用**（仅编辑器显示用） |
| `JsonData` | 用户编辑参数 → `JsonUtility.ToJson(Data)` | `DeserializeData()` → 填入 `Data` 字段 |
| `ChildIds` | 用户拖连线 → 写入父节点的 ChildIds | WireChild 遍历：parent.AddChild/SetChild |

### 关键理解：

- **编辑器不需要理解 ChildIds 里的节点是什么类型**。它只需要知道"节点 A 的第 2 个子节点是节点 B"。
- **运行时不需要 Position**。位置是纯视觉信息。
- **JsonData 是黑盒**。编辑器通过反射知道这个节点类型的 Data struct 有哪些字段，然后生成对应的编辑 UI。运行时反序列化后直接赋值。

---

## 三、节点类型体系：四类节点在编辑器中的表现

### 3.1 继承链回顾（来自 CLAUDE_07 + CLAUDE_09）

```
BTNode                          ← 所有节点的根
├── BTComposite                 ← 非泛型，多个子节点
│   ├── BTSelector              
│   └── BTSequence              
│
├── BTDecorator                 ← 非泛型，单个子节点
│   ├── BTInverter              ← 无参数，直接继承 BTDecorator
│   ├── BTSucceeder             ← 无参数，直接继承 BTDecorator
│   └── BTDecorator<T>          ← 泛型，有参数的装饰节点继承这个
│       ├── BTRepeater          
│       └── BTCooldown          
│
├── BTAction<T>                 ← 泛型动作叶子（0 个子节点）
│   ├── BTWait                  
│   ├── BTDebugLog              
│   ├── BTSetAnimatorTrigger    
│   ├── BTSetAnimatorBool       
│   ├── BTFaceTarget            
│   ├── BTFindNearestTarget     
│   ├── BTMoveTowards           
│   └── BTDestroySelf           ← CLAUDE_10 新增
│
└── BTCondition<T>              ← 泛型条件叶子（0 个子节点）
    ├── BTHasTarget             ← 无参数，BTCondition<object>
    ├── BTIsTargetInRange       
    ├── BTIsAnimationDone       
    ├── BTBlackboardCondition   
    └── BTIsRecentlyHit         ← CLAUDE_10 新增
```

### 3.2 编辑器如何区分四类节点

编辑器**不看类继承**，它通过反射检查：

| 检查方式 | 判断结果 | 连线规则 |
|---------|---------|---------|
| `typeof(BTComposite).IsAssignableFrom(type)` | 组合节点 | **多个**输入槽 + 多个输出槽 |
| `typeof(BTDecorator).IsAssignableFrom(type)` | 装饰节点 | **1 个**输入槽 + **1 个**输出槽 |
| `typeof(BTAction<>).IsAssignableFrom(type)` 或其泛型基类 | 动作叶子 | 多个输入槽 + **0 个**输出槽 |
| `typeof(BTCondition<>).IsAssignableFrom(type)` 或其泛型基类 | 条件叶子 | 多个输入槽 + **0 个**输出槽 |

实际上更简单——编辑器只需要知道**最多几个子节点**：

```
BTComposite:   ChildIds.Count = 0..N   （可以连任意多个子节点）
BTDecorator:   ChildIds.Count = 0..1   （最多 1 个子节点）
叶子节点:       ChildIds.Count = 0      （不能有子节点）
```

这个信息可以通过检查基类获得，也可以通过 `[BTNode]` attribute 额外标记。

### 3.3 编辑器中的视觉区分

| 节点类型 | 颜色 | 形状特征 |
|---------|------|---------|
| Composite（组合）| 蓝色调 | 底部有多个子节点槽 |
| Decorator（装饰）| 黄色调 | 底部只有 1 个子节点槽 |
| Action（动作）| 绿色调 | 底部无子节点槽 |
| Condition（条件）| 橙色调 | 底部无子节点槽 |

---

## 四、反射与属性发现：编辑器怎样找到所有节点类型

### 4.1 [BTNode] Attribute（已实现）

```csharp
// 每个节点类上贴的标签
[BTNode("等待", "Action/时间", "等待指定秒数后返回成功")]
public class BTWait : BTAction<WaitData> { }

// Attribute 定义
public class BTNodeAttribute : Attribute
{
    public string Name;        // "等待"
    public string Category;    // "Action/时间"
    public string Description; // 鼠标悬停提示
}
```

### 4.2 编辑器启动时的发现流程

```
1. TypeCache.GetTypesDerivedFrom<BTNode>()   ← Unity 编辑器 API
   或 AppDomain.CurrentDomain.GetAssemblies()
       .SelectMany(a => a.GetTypes())
       .Where(t => t.IsSubclassOf(typeof(BTNode)) && !t.IsAbstract)

2. 对每个找到的类型：
   - 读 [BTNode] attribute → 拿到 Name、Category、Description
   - 检查基类 → 判断是 Composite / Decorator / Action / Condition
   - 找到 Data 泛型参数 → 获取参数 struct 的字段列表
   - 注册到节点菜单

3. 节点菜单按 Category 分组：
   Composite/
   ├── Selector（选择节点）
   └── Sequence（顺序节点）
   Decorator/
   ├── 流程控制/
   │   ├── 重复 (Repeater)
   │   └── 冷却 (Cooldown)
   └── 结果修改/
       ├── 取反 (Inverter)
       └── 强制成功 (Succeeder)
   Action/
   ├── 时间/
   │   └── 等待 (Wait)
   ├── 动画/
   │   ├── 设置Trigger
   │   └── 设置Bool
   └── ...
   Condition/
   └── ...
```

### 4.3 新增节点零注册

开发者加一个新节点只需要：
1. 写 Data struct + BTNode 子类
2. 贴上 `[BTNode]` attribute
3. 编辑器**自动**发现，0 行注册代码

这就是反射的核心价值——CLAUDE_08 已经讲得很清楚。

---

## 五、数据-逻辑分离在编辑器中的体现

### 5.1 回顾模式（来自 CLAUDE_09）

```
struct XxxData          ← 数据层（纯字段，编辑器关注这个）
class BTXxx : BTAction<XxxData>  ← 逻辑层（OnExecute，运行时关注这个）
```

### 5.2 编辑器如何编辑参数

编辑器拿到节点类型后，找到泛型参数 `T`（即 Data struct 的类型），然后：

```
1. 反射获取 struct 的所有 public 字段
2. 根据字段类型生成编辑 UI：
   float     → FloatField
   string    → TextField
   bool      → Toggle
   enum      → EnumField / Popup
   Vector3   → Vector3Field
3. 编辑器修改值后 → JsonUtility.ToJson(Data) → 写回 NodeEntry.JsonData
```

**编辑器不实例化 BTNode**，它只：
- 实例化 Data struct（`new XxxData()`）
- 从 `NodeEntry.JsonData` 反序列化到 struct
- 让用户修改 struct 的字段
- 序列化写回 `NodeEntry.JsonData`

### 5.3 无参数节点

有些节点没有配置参数（如 `BTHasTarget : BTCondition<object>`、`BTInverter : BTDecorator`）。编辑器检查：
- 泛型参数是 `object`？→ 无参数面板
- 直接继承非泛型 `BTDecorator`？→ 无参数面板

---

## 六、连线规则

### 6.1 规则表

| 父节点类型 | 最多子节点数 | 视觉表现 |
|-----------|------------|---------|
| BTComposite | 无限 | 底部多个输出端口，可拖多条线 |
| BTDecorator | 1 | 底部 1 个输出端口，再拖线替换旧连线 |
| 叶子节点 | 0 | 底部没有输出端口 |

### 6.2 不能连的情况（编辑器阻止）

```
❌ 叶子节点 → 任何节点（叶子没有输出端口）
❌ Decorator → 第二个子节点（已有子节点时阻止新连线）
❌ 循环引用（A→B→C→A）
❌ 连向自己
❌ 同一个父节点连同一个子节点两次
```

### 6.3 连线存储

连线不是独立数据，而是存在父节点的 `ChildIds` 里：

```
画布上: [Selector] ──→ [Sequence A] ──→ [Wait]
         │
         └──→ [Sequence B] ──→ [Log]

SO 存储:
{ Id:"1", TypeName:"BTSelector", ChildIds:["2","5"] }
{ Id:"2", TypeName:"BTSequence",   ChildIds:["3"] }
{ Id:"3", TypeName:"BTWait",       ChildIds:[] }
{ Id:"5", TypeName:"BTSequence",   ChildIds:["6"] }
{ Id:"6", TypeName:"BTDebugLog",   ChildIds:[] }
```

**子节点的顺序 = ChildIds 列表的顺序。** 对 Selector 和 Sequence 来说，顺序就是优先级/执行顺序。编辑器需要支持在父节点下拖拽调整子节点顺序。

---

## 七、保存与加载

### 7.1 保存（编辑器 → SO）

```
用户点击 Save：
  1. 遍历画布上所有节点
  2. 对每个节点生成/更新 NodeEntry：
     - Id: 节点的 GUID（第一次保存时生成）
     - TypeName: typeof(节点类型).FullName
     - Position: 节点在画布上的坐标
     - JsonData: 参数面板的当前值序列化
     - ChildIds: 从连线数据中提取
  3. SO._nodes = 这个列表
  4. SO._rootNodeId = 标记为根节点的节点 ID
  5. EditorUtility.SetDirty(SO); AssetDatabase.SaveAssets();
```

### 7.2 加载（SO → 编辑器）

```
用户打开 SO 资产：
  1. 读取 SO._nodes
  2. 对每个 NodeEntry 创建 GraphView 节点：
     - 位置 = entry.Position
     - 标题 = 从 [BTNode] attribute 获取 Name
     - 参数面板 = 从 entry.JsonData 反序列化
  3. 遍历 ChildIds 创建连线
  4. 标记根节点（RootNodeId 匹配的节点）
```

---

## 八、关键约束（DON'T）

### 8.1 编辑器不允许做的事

| ❌ 禁止 | 原因 |
|--------|------|
| 在编辑器中 `new BTWait()` | 编辑器不实例化 BTNode，只操作 NodeEntry 数据 |
| 在编辑器中调用 `OnExecute()` | 编辑器不跑行为树逻辑 |
| 编辑器引用 `BehaviorTreeRunner` | 编辑器不依赖运行时 |
| 编辑器引用 `Blackboard` | 编辑器不依赖运行时 |
| 编辑器直接修改 `BTNode.Data` | Data 的修改路径是：编辑器 UI → NodeEntry.JsonData → 保存 SO → BuildTree → BTNode.Data |

### 8.2 运行时不允许做的事

| ❌ 禁止 | 原因 |
|--------|------|
| BuildTree 之后修改 SO | 运行时构建完就全走 C# 对象树 |
| 每 Tick 读 SO | 每 Tick 是纯 C# 虚方法调用，零 IO |
| 引用 GraphView / Editor 命名空间 | 运行时不能依赖 Editor 程序集 |

### 8.3 类型名约定

`NodeEntry.TypeName` 存的是 C# 完整类型名（`typeof(BTWait).FullName`），例如：
- ✅ `"AI.BehaviourTree.BTWait"`
- ❌ `"BTWait"` （短名可能冲突）
- ❌ `"等待"` （显示名，不是类型名）

如果在编辑器里改了命名空间/类名，旧的 SO 文件会因 `Type.GetType()` 返回 null 而丢失该节点。需要提供迁移机制或至少警告用户。

---

## 九、开发分步建议

### Phase 3a：最小可用编辑器
- 创建 `BehaviorTreeEditorWindow`（`EditorWindow`）
- 创建 `BehaviorTreeGraphView`（继承 `GraphView`）
- 创建 `BTNodeView`（继承 `Node`，显示节点标题）
- 支持：从菜单创建节点、拖拽位置、保存/加载 SO

### Phase 3b：连线系统
- 创建 Port（输入/输出端口）
- 支持拖拽连线（Edge）
- 连线规则校验（Decorator 只能连 1 个等）
- 保存/加载连线（ChildIds）

### Phase 3c：参数编辑
- Inspector 面板显示选中节点的参数
- 根据 Data struct 字段类型生成 UI
- 修改参数 → 实时更新 NodeEntry.JsonData

### Phase 3d：体验优化
- 节点搜索窗口（按 Category 分组 + 搜索栏）
- 右键菜单（复制/粘贴/删除）
- 根节点标记
- 断线/重连
- 子节点拖拽排序

---

## 十、关键文件索引

### 编辑器需要引用的已有文件

| 文件 | 编辑器用它做什么 |
|------|----------------|
| `BehaviorTreeSO.cs` | 读写 `_nodes` 列表、`RootNodeId` |
| `BTNodeAttribute.cs` | 反射获取节点的显示名、分类、描述 |
| `BTComposite.cs` | 判断节点是否组合类型（决定连线规则） |
| `BTDecorator.cs` | 判断节点是否装饰类型（决定连线规则） |
| `BTAction.cs` | 获取泛型 Data 类型（生成参数编辑 UI） |
| `BTCondition.cs` | 同上 |

### 编辑器不允许引用的文件

| 文件 | 原因 |
|------|------|
| `BehaviorTreeRunner.cs` | 运行时 |
| `Blackboard.cs` | 运行时 |
| 所有 `OnExecute()` 实现 | 运行时逻辑 |

---

## 十一、总结：记住这五句话

1. **编辑器只管 NodeEntry 列表。** 不知道 BTNode 的存在。
2. **TypeName 字符串是桥梁。** 编辑器通过 `typeof().FullName` 写入，运行时通过 `Type.GetType()` 读出。
3. **JsonData 是黑盒。** 编辑器通过反射 Data struct 的字段来编辑，反序列化后写回字符串。
4. **连线即 ChildIds。** 画布上的线 = 父节点 ChildIds 的一个条目。没有独立的"连线数据"。
5. **新增节点零注册。** `[BTNode]` attribute 贴上去，编辑器自动发现。这就是 CLAUDE_08 讲的反射价值。
