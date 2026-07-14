# CLAUDE_11 — 行为树可视化编辑器（GraphView）开发

> 日期：2026-07-14
> 前置：[CLAUDE_10_EditorPrinciples.md](CLAUDE_10_EditorPrinciples.md) | [CLAUDE_09](CLAUDE_09_BTNodeLibrary.md)

---

## 一、成果总览

从零搭建了一个基于 Unity GraphView 的行为树可视化编辑器。

### 编辑器文件（6 个）

```
Assets/Editor/BehaviorTree/
├── BehaviorTreeEditorWindow.cs    ← EditorWindow 入口 + 工具栏 + 保存
├── BehaviorTreeGraphView.cs       ← GraphView 画布 + 右键菜单 + 保存/加载
├── BTNodeView.cs                  ← 可视化节点（色条 + 文字 + 端口）
├── BTNodeFactory.cs               ← 反射扫描 [BTNode] 类型
├── BTInspectorView.cs             ← 选中节点参数编辑面板
└── BTNodeFactory.cs.meta
```

### 功能清单

| 功能 | 说明 |
|------|------|
| 双击 SO 打开 | `[OnOpenAsset]` → EditorWindow |
| 右键创建节点 | 反射扫描所有 `[BTNode]` 类型，按 Category 分组 |
| 端口拖拽连线 | `GetCompatiblePorts` 规则：不同节点、方向相反 |
| 保存 | 画布 → NodeEntry 列表 → SO + AssetDatabase.SaveAssets |
| 加载 | SO → 恢复节点位置 + 恢复连线 |
| 参数编辑 | 选中节点 → 右侧面板反射 Data struct 字段 |
| 关闭提示 | 有未保存修改时弹窗 |
| 画布操作 | 滚轮缩放、中键平移、Alt 框选 |

---

## 二、关键技术点

### 2.1 GraphView 的层级结构

```
EditorWindow
  └── rootVisualElement
        ├── Toolbar（保存按钮、资产名）
        └── 横向容器（contentRow）
              ├── GraphView（画布，flexGrow=1）
              └── BTInspectorView（右侧面板，width=220）
```

### 2.2 Node 的内部容器

GraphView 的 `Node` 自带四个容器：

```
Node
  ├── titleContainer    ← 标题栏（我们用作 4px 色条）
  ├── inputContainer    ← 左侧输入端口
  ├── mainContainer     ← 中间内容区（大字 + 小字）
  └── outputContainer   ← 右侧输出端口
```

`inputContainer` 和 `outputContainer` 的位置是 Node 的 USS 写死的（左右），不能改成上下。尝试过很多次，最终决定用左右布局，树从左往右流。

### 2.3 端口对齐的坑

想让 Input 和 Output 在节点内上下对齐 → 失败了很久。原因是：
- `Port.Capacity.Single` 和 `Port.Capacity.Multi` 内部渲染不同
- GraphView 的默认 USS 会覆盖手动设置的 width/height
- 不同容器（input/output）的参考坐标系不同

**最终方案**：回到 GraphView 默认的左右布局，Input 左 Output 右，不折腾自定义位置。

### 2.4 保存/加载的数据流

```
保存：BTNodeView → NodeEntry { Id, TypeName, Position, JsonData, ChildIds }
加载：NodeEntry → BTNodeView（Type.GetType → 找 BTNodeTypeInfo → new BTNodeView）
```

ChildIds 通过追踪 OutputPort 的 connections 得到。

### 2.5 参数编辑的数据流

```
创建节点 → 反射找 DataType → new DataObject（默认值）
选中 → 面板反射字段 → 生成 FloatField/TextField/Toggle → 修改
保存 → JsonUtility.ToJson(DataObject) → NodeEntry.JsonData
加载 → JsonUtility.FromJson → DataObject 恢复
```

---

## 三、踩过的坑

| 坑 | 原因 | 修法 |
|----|------|------|
| 双击 SO 打开提示"请打开资产" | `GetWindow` 触发 `CreateGUI` 时 `_treeAsset` 还是 null | 分开：CreateGUI 建骨架，设置 _treeAsset 后调 RefreshGraphArea |
| 没有网格 | 缺 `ContentZoomer` manipulator | `SetupZoom` 只设范围不管行为，需手动 `AddManipulator(new ContentZoomer())` |
| `Resources.Load` 样式表报错 | 路径不在 Resources 下 | 删掉，GridBackground 不需要 USS |
| `inputContainer` 端口位置锁死 | Node 的 USS 写死左右 | 放弃上下布局，用 GraphView 默认左右 |
| 节点缩成一团 | 没设最小宽度 | `style.minWidth = 140` |
| 右键菜单分类英文 | Category 值写的是 "Composite" 不是 "组合节点" | `TranslateCategory` 字典翻译第一段 |
| `object` 类型 Data 报错 | 无参节点用 `BTCondition<object>` 占位 | `GetDataType` 发现 T=object 时返回 null |
| `ObjectNames` 找不到 | 缺 `using UnityEditor` | Editor 脚本也需要手动 using |

---

## 四、编辑器架构三原则（来自 CLAUDE_10 基调）

1. **编辑器只管 NodeEntry 列表** — 不 new BTNode，不调 OnExecute
2. **TypeName 字符串是桥梁** — `typeof().FullName` 写入，`Type.GetType()` 读出
3. **连线 = ChildIds** — 画布上的线 = 父节点 ChildIds 的一个条目

---

## 五、待完善

- [ ] 子节点拖拽排序（Selector 的 Children 顺序目前 = 连线先后）
- [ ] 删除连线/节点的右键菜单
- [ ] 根节点标记（视觉上标出来哪个是 Root）
- [ ] 拖拽节点时自动更新 Position
- [ ] Decorator 只能连一根线的强力约束（目前只是提醒）
- [ ] 参数面板支持 Vector3 等复杂类型

---

## 六、已创建 / 修改的文件一览

### 新建
- `Assets/Editor/BehaviorTree/BehaviorTreeEditorWindow.cs`
- `Assets/Editor/BehaviorTree/BehaviorTreeGraphView.cs`
- `Assets/Editor/BehaviorTree/BTNodeView.cs`
- `Assets/Editor/BehaviorTree/BTNodeFactory.cs`
- `Assets/Editor/BehaviorTree/BTInspectorView.cs`
- `ClaudeLearn/CLAUDE_10_EditorPrinciples.md` （开发基调）
- `ClaudeLearn/CLAUDE_11_GraphViewEditor.md` （本文档）

### 修改
- 无（运行时文件全部未动）
