---
name: monster-bt-design
description: 怪物行为树整体设计、架构决策和编辑器扩展
metadata:
  type: project
---

# 怪物行为树设计

## 架构定位

行为树是**决策层**，不是执行层。BT 负责"决定现在该干什么"，末端节点应尽可能只设参数（Trigger + 黑板值），由外部脚本（Controller）执行具体逻辑。

```
┌─ 决策层 ──────────────────────────┐
│  Behavior Tree                    │
│  条件节点 → 组合节点 → 动作节点     │
│  末端设 Trigger / 写黑板值          │
└──────────┬────────────────────────┘
           ↓ 参数
┌─ 执行层 ──────────────────────────┐
│  BossActionController / 其他脚本   │
│  读黑板参数，执行攻击/移动/闪避等    │
└───────────────────────────────────┘
```

两种末端方式并存：
- **设参数节点**：`BTSetAnimatorTrigger` + `BTSetBlackboard`，通知其他系统做事
- **直接动作节点**：`BTChaseTarget`、`BTWait` 等，BT 自己执行

## 怪物行为树设计图

```
Root (Selector)
│
├─── 【死亡】Sequence
│    ├── BTBlackboardCondition: _hpRatio <= 0
│    └── BTSetAnimatorTrigger: Death
│
├─── 【战斗模式】Sequence (有目标时)
│    ├── BTHasTarget
│    │
│    ├─── 【阶段分叉】Selector ← 从高到低匹配
│    │    │
│    │    ├─── 【Phase4 - 狂暴MAX】Sequence (HP < 25%)
│    │    │    ├── BTBlackboardCondition: _hpRatio < 0.25
│    │    │    ├── BTWeightedRandom ── 权重高：复杂攻击
│    │    │    │    ├── [权重4] Attack_D
│    │    │    │    ├── [权重3] Attack_C
│    │    │    │    ├── [权重2] Attack_B
│    │    │    │    └── [权重2] Attack_A
│    │    │    └── BTChaseTarget ── 追击/对峙
│    │    │
│    │    ├─── 【Phase3 - 狂暴】Sequence (HP < 50%)
│    │    │    ├── BTBlackboardCondition: _hpRatio < 0.5
│    │    │    ├── BTWeightedRandom
│    │    │    │    ├── [权重3] Attack_C
│    │    │    │    ├── [权重2] Attack_B
│    │    │    │    └── [权重1] Attack_A
│    │    │    └── BTCaseTarget 
│    │    │
│    │    ├─── 【Phase2】Sequence (HP < 75%)
│    │    │    ├── BTBlackboardCondition: _hpRatio < 0.75
│    │    │    ├─── Selector ← 对峙 or 攻击
│    │    │    │    ├── 【对峙】Sequence ← 玩家远+久未攻击
│    │    │    │    │    ├── BTBlackboardCondition: _timeSincePlayerAttack > 5
│    │    │    │    │    └── BTChaseTarget ← 绕圈周旋，不攻击
│    │    │    │    │
│    │    │    │    └── BTWeightedRandom
│    │    │    │         ├── [权重2] Attack_A
│    │    │    │         └── [权重1] Attack_B
│    │    │    └── BTChaseTarget
│    │    │
│    │    └─── 【Phase1】Sequence (HP >= 75%)
│    │         ├── BTBlackboardCondition: _hpRatio >= 0.75
│    │         ├─── Selector
│    │         │    ├── 【对峙】Sequence
│    │         │    │    ├── BTBlackboardCondition: _timeSincePlayerAttack > 5
│    │         │    │    └── BTChaseTarget
│    │         │    │
│    │         │    └── BTWeightedRandom
│    │         │         └── [权重1] Attack_A ← 只有一招
│    │         └── BTChaseTarget
│    │
│    └── BTFindNearestTarget ← 每轮刷新目标
│
├─── 【警戒】Sequence (发现玩家)
│    ├── BTIsTargetInRange: 15m (察觉距离)
│    ├── BTFindNearestTarget
│    └── BTSetAnimatorTrigger: Alert
│
└─── 【待机】Repeater (无限循环)
     └── BTSetAnimatorBool: IsMoving=false
```

### 已知问题

**警戒分支走不到**：`BTIsTargetInRange` 需要一个已有的 `target` 才能判断距离，但警戒是"发现"阶段，此时还没人设 target。应改成先 `BTFindNearestTarget`（找目标），找到后再设 Trigger。

## 编辑器扩展

### 文件夹节点（BTSubTree）

白色文件夹节点，双击**进入子视图**（类似 Animator BlendTree 的钻取）。

- 运行时：[BTSubTree.cs](Assets/Scripts/AI/BehaviorTree/Composites(组合节点)/BTSubTree.cs) — 继承 BTComposite，Sequence 语义
- 编辑器：白条 + 浅色背景 + `[+]` 标记
- 子图导航：`EnterFolder()` / `ExitFolder()` / 面包屑栏
- 文件：[BehaviorTreeGraphView.cs](Assets/Editor/BehaviorTree/BehaviorTreeGraphView.cs)
- 类别：`BTNodeCategory.SubTree`

### 运行时高亮（修复后）

从 Running 节点沿 `Parent` 链向上回溯，只高亮**当前执行路径**，不高亮一帧内完成的节点。

- `BTNode.Parent` — `WireChild()` 时建立父引用
- `GetRunningNodeIds()` — 找 IsRunning 节点 → 沿 Parent 回溯完整路径
- 编辑器 0.05s 轮询一次
- Animator 窗口一样的效果——只亮"当前正在播的"

### 文件夹图标

`BTNodeView.cs` 内添加 `[+]` 标记 + 浅色背景。

## 新增节点

| 节点 | 作用 |
|---|---|
| `BTSubTree` | 白色文件夹容器，双击钻入子视图 |
| `BTSetBlackboard` | 向黑板写入 float 值，用于"设参数"架构 |
| `BTSetAnimatorTrigger` | 已有，设动画 Trigger |
| `BTSetAnimatorBool` | 已有，设动画 Bool |
