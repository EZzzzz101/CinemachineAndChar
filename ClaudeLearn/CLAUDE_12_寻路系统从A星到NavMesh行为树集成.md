# CLAUDE_12 — 寻路系统：从 A* 到 NavMesh 行为树集成

> 日期：2026-07-17
> 前置：[CLAUDE_07_BehaviorTree.md](CLAUDE_07_BehaviorTree.md) | [CLAUDE_09_BTNodeLibrary.md](CLAUDE_09_BTNodeLibrary.md)

---

## 一、今日目标总览

一天之内从零掌握游戏寻路的三个层次：

| 层次 | 内容 | 产出 | 时间 |
|------|------|------|------|
| 上午 | A* 算法理论 + 纯 C# 网格实现 | `AStarGrid.cs` 单元测试验证 | ~3.5h |
| 下午 | Unity NavMesh 烘焙 + `NavMesh.CalculatePath` 手动操控 | 场景有 NavMesh，路径跟随演示 | ~2.5h |
| 晚上 | BTNavMeshMove 节点 + 路径可视化 | 行为树可寻路移动，替换 BTMoveTowards | ~2.5h |

### 1.1 学习原则

**先理解再写代码**。每阶段先读理论或文档，再动手实现。不跳步骤。

### 1.2 涉及文件清单

| 文件 | 阶段 | 说明 |
|------|------|------|
| `Assets/Scripts/AI/Pathfinding/AStarGrid.cs` | 上午 | A* 网格寻路核心（纯 C# 类） |
| `Assets/Scripts/AI/Pathfinding/NavMeshPathDriver.cs` | 下午 | NavMesh 路径跟随演示组件 |
| `Assets/Scripts/AI/BehaviorTree/Actions(动作节点)/BTNavMeshMove.cs` | 晚上 | 行为树寻路节点 |
| `Assets/Scripts/AI/BehaviorTree/Actions(动作节点)/BTMoveTowards.cs` | 晚上 | 补充注释，与旧节点并存 |
| `com.unity.ai.navigation` | 下午 | Unity 包，NavMeshSurface 烘焙用 |

---

## 二、上午：A* 算法理论 + 纯 C# 实现（~3.5 小时）

### 2.1 为什么先学 A*

- NavMesh 寻路的底层仍然是 A\*，只是从网格（grid）换成了三角形图（triangle graph）
- 理解 A\* 的 Open/Closed 列表、G/H/F 估价函数、启发式选择，后面调试 NavMesh 路径时能理解 Unity 在做什么
- 纯 C# 实现可以在 Unity 之外独立测试，调试体验比嵌套在 MonoBehaviour 里好得多

### 2.2 A* 算法核心概念

```
OPEN = [start]          ← 待探索的节点
CLOSED = []             ← 已探索的节点

while OPEN not empty:
    current = OPEN 中 F 值最小的节点
    if current == goal:
        重建路径 → 返回

    OPEN 移除 current
    CLOSED 添加 current

    for each neighbor of current:
        if neighbor in CLOSED:
            continue                          ← 跳过已经探索过的

        g = current.g + cost(current, neighbor)  ← 起点到 neighbor 的实际代价
        h = heuristic(neighbor, goal)            ← 预估到终点的距离
        f = g + h

        if neighbor not in OPEN:
            OPEN 添加 neighbor
            neighbor.g = g;  neighbor.f = f;  neighbor.parent = current
        else if g < neighbor.g:
            更新 neighbor 的 g/f/parent        ← 找到了更短的路径
```

**三个核心计分值**：

| 概念 | 意义 | 公式 |
|------|------|------|
| G 值 | 起点到当前节点的实际代价 | `G = parent.G + moveCost` |
| H 值 | 当前节点到终点的预估代价（启发式） | 见下方三种选择 |
| F 值 | 总估价 = G + H，OPEN 排序依据 | `F = G + H` |

**三种启发式函数对比**：

| 启发式 | 公式 | 特点 | 适用场景 |
|--------|------|------|---------|
| Manhattan | `|dx| + |dy|` | 只允许上下左右移动 | 矩形网格，四方向移动 |
| Euclidean | `sqrt(dx² + dy²)` | 允许任意方向，最精确 | 连续空间估算 |
| Diagonal | `max(dx, dy) + (√2-1)*min(dx, dy)` | 允许对角线移动 | 八方向网格 |

**要点**：H 值 <= 实际距离时，A\* 保证找到最短路径（可采纳性，admissible）。H = 0 退化为 Dijkstra。H 过大可能找不到最短路径但搜索更快。

### 2.3 实现：AStarGrid.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace AI.Pathfinding
{
    /// <summary>网格中的一个节点</summary>
    public class AStarNode
    {
        public int X, Y;
        public bool Walkable = true;
        public float CostMultiplier = 1f;

        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public AStarNode Parent;
    }

    /// <summary>A* 网格寻路器（纯 C#，不依赖 MonoBehaviour）</summary>
    public class AStarGrid
    {
        private readonly AStarNode[,] _grid;
        private readonly int _width;
        private readonly int _height;

        // 四方向邻居偏移
        private static readonly Vector2Int[] _directions4 =
        {
            new Vector2Int(0, 1),  // 上
            new Vector2Int(0, -1), // 下
            new Vector2Int(-1, 0), // 左
            new Vector2Int(1, 0),  // 右
        };

        // 八方向：四个对角线
        private static readonly Vector2Int[] _directions8 =
        {
            new Vector2Int(0, 1),  new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(-1, 1), new Vector2Int(1, 1),
            new Vector2Int(-1, -1), new Vector2Int(1, -1),
        };

        public AStarGrid(int width, int height)
        {
            _width = width;
            _height = height;
            _grid = new AStarNode[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                _grid[x, y] = new AStarNode { X = x, Y = y };
            }
        }

        public void SetWalkable(int x, int y, bool walkable)
        {
            if (InBounds(x, y))
                _grid[x, y].Walkable = walkable;
        }

        /// <summary>核心 API：寻路，返回路径点列表（含起点和终点）</summary>
        public List<Vector2Int> FindPath(int startX, int startY, int endX, int endY)
        {
            if (!InBounds(startX, startY) || !InBounds(endX, endY))
                return new List<Vector2Int>();
            if (!_grid[startX, startY].Walkable || !_grid[endX, endY].Walkable)
                return new List<Vector2Int>();

            AStarNode start = _grid[startX, startY];
            AStarNode goal = _grid[endX, endY];

            // OPEN = 待探索，CLOSED = 已探索
            List<AStarNode> open = new List<AStarNode>();
            HashSet<AStarNode> closed = new HashSet<AStarNode>();

            // 重置节点状态（因为网格是复用的）
            ResetNodeState(start);
            start.GCost = 0;
            start.HCost = Heuristic(startX, startY, endX, endY);
            open.Add(start);

            while (open.Count > 0)
            {
                AStarNode current = FindLowestF(open);
                if (current == goal)
                    return ReconstructPath(current);

                open.Remove(current);
                closed.Add(current);

                foreach (AStarNode neighbor in GetNeighbors(current))
                {
                    if (!neighbor.Walkable || closed.Contains(neighbor))
                        continue;

                    int moveCost = IsDiagonal(current, neighbor) ? 14 : 10;
                    int tentativeG = current.GCost + moveCost;

                    if (!open.Contains(neighbor))
                    {
                        // 首次发现
                        neighbor.Parent = current;
                        neighbor.GCost = tentativeG;
                        neighbor.HCost = Heuristic(neighbor.X, neighbor.Y, endX, endY);
                        open.Add(neighbor);
                    }
                    else if (tentativeG < neighbor.GCost)
                    {
                        // 发现了更短路径
                        neighbor.Parent = current;
                        neighbor.GCost = tentativeG;
                        // HCost 不变
                    }
                }
            }

            return new List<Vector2Int>(); // OPEN 耗尽，无路可达
        }

        private AStarNode FindLowestF(List<AStarNode> nodes)
        {
            AStarNode best = nodes[0];
            for (int i = 1; i < nodes.Count; i++)
                if (nodes[i].FCost < best.FCost)
                    best = nodes[i];
            return best;
        }

        private List<AStarNode> GetNeighbors(AStarNode node)
        {
            List<AStarNode> neighbors = new List<AStarNode>(8);
            foreach (Vector2Int dir in _directions8)
            {
                int nx = node.X + dir.x;
                int ny = node.Y + dir.y;
                if (InBounds(nx, ny))
                    neighbors.Add(_grid[nx, ny]);
            }
            return neighbors;
        }

        private bool IsDiagonal(AStarNode from, AStarNode to)
            => from.X != to.X && from.Y != to.Y;

        private int Heuristic(int x1, int y1, int x2, int y2)
        {
            int dx = Mathf.Abs(x1 - x2);
            int dy = Mathf.Abs(y1 - y2);
            // Diagonal 距离：max(dx,dy)*10 + (√2-1)*min(dx,dy)*10 ≈ max(dx,dy)*10 + min(dx,dy)*4
            return Mathf.Max(dx, dy) * 10 + Mathf.Min(dx, dy) * 4;
        }

        private List<Vector2Int> ReconstructPath(AStarNode node)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            while (node != null)
            {
                path.Add(new Vector2Int(node.X, node.Y));
                node = node.Parent;
            }
            path.Reverse();
            return path;
        }

        private void ResetNodeState(AStarNode node)
        {
            // BFS 清空
            Queue<AStarNode> queue = new Queue<AStarNode>();
            HashSet<AStarNode> visited = new HashSet<AStarNode>();
            queue.Enqueue(node);
            visited.Add(node);

            while (queue.Count > 0)
            {
                AStarNode cur = queue.Dequeue();
                cur.GCost = 0;
                cur.HCost = 0;
                cur.Parent = null;

                foreach (AStarNode neighbor in GetNeighbors(cur))
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private bool InBounds(int x, int y)
            => x >= 0 && x < _width && y >= 0 && y < _height;
    }
}
```

### 2.4 验证测试

在 `AStarGrid.cs` 同一文件中追加或在单独的 `AStarTest.cs` 中写：

```csharp
#if UNITY_EDITOR
using UnityEditor;

public static class AStarTest
{
    [MenuItem("AI/Test A* Grid")]
    public static void TestAStar()
    {
        var grid = new AStarGrid(10, 10);

        // 设置一堵竖墙 (x=3, y=0~7)
        for (int y = 0; y <= 7; y++)
            grid.SetWalkable(3, y, false);

        // 从左侧 (1,5) 到右侧 (8,5)，应绕墙走
        var path = grid.FindPath(1, 5, 8, 5);

        string log = "路径: ";
        foreach (var p in path)
            log += $"({p.x},{p.y}) → ";
        Debug.Log(log + "End");

        Debug.Assert(path.Count > 0, "路径不应为空");
        Debug.Assert(path[path.Count - 1] == new Vector2Int(8, 5), "终点应为 (8,5)");

        // 验证没有穿过 x=3 的墙
        foreach (var p in path)
            Debug.Assert(p.x != 3 || (p.y < 0 || p.y > 7),
                $"路径不应穿过障碍物 ({p.x},{p.y})");

        Debug.Log("[A*] ✅ 测试通过！");
    }
}
#endif
```

**运行**：Unity Editor 菜单 → `AI` > `Test A* Grid`，Console 查看路径输出。

### 2.5 A* 实现注意事项（面试级理解）

| 问题 | 解答 |
|------|------|
| 为什么 OPEN 用 List 而不用 PriorityQueue？ | 教学目的。10×10 网格下 List 扫描性能瓶颈可忽略。大网格时可换 `C5.IntervalHeap` 或 `System.Collections.Generic.PriorityQueue`（.NET 6+）。 |
| G 值为什么用整数？ | 避免浮点比较误差，计算更快。直线 10，对角线 14（近似 10·√2）。 |
| 为什么 H 值要乘以 10？ | 与 G 值保持同一量纲。G 用 10/14 而 H 用浮点 sqrt 时，F 排序会因精度出现异常。 |
| Diagonal 启发式是否可采纳？ | 是。八方向下对角线距离 <= 实际路径代价，保证找到最短路径。 |
| 路径有锯齿怎么办？ | 网格路径天然问题（曼哈顿化）。用 Funnel 算法（String Pulling）做路径平滑，见 3.6 节。 |
| ResetNodeState 为什么用 BFS？ | 防止上一次寻路的 Parent/GCost 残留污染下一次结果。如果每次都 new 网格，不需要这个。 |

### 2.6 时间分配

| 时段 | 内容 | 耗时 |
|------|------|------|
| 09:00-09:30 | 阅读 A* 理论（2.1-2.2 节） | 30min |
| 09:30-10:30 | 编写 AStarGrid.cs | 60min |
| 10:30-11:00 | 编写测试 + 调试 | 30min |
| 11:00-11:30 | 理解启发式 vs 性能 | 30min |
| 11:30-12:00 | 整理笔记，过渡到 NavMesh | 30min |

**验证清单**：
- [ ] `AI/Test A* Grid` 菜单项可用
- [ ] 无障碍直路能找到最短路径
- [ ] 障碍物能正确绕行
- [ ] 无解时返回空列表（不崩溃）

---

## 三、下午：NavMesh 烘焙 + CalculatePath 手动操控（~2.5 小时）

### 3.1 从 Grid A* 到 NavMesh 的思维跃迁

```
Grid A*                       NavMesh
──────────────────────────────────────────────────
正方形网格                    三角形凸多边形（mesh）
节点 = 网格单元（离散）         节点 = 三角形面
边 = 四/八方向（有限）          边 = 共享边（任意方向）
障碍 = 标记不可通行            障碍 = 从 NavMesh 挖掉（镂空）
路径 = 网格坐标列表            路径 = 世界坐标 Vector3 序列
锯齿需要 Funnel 平滑          Unity 已做基础简化
```

**为什么 Unity 用 NavMesh 而不是 Grid？**

- 游戏场景是连续空间，不是网格。写实场景地形高低起伏，网格要么太粗（精度不够）要么太细（内存爆炸）。
- NavMesh 三角形数量远少于同精度网格：100m × 100m 平面，1m 精度网格需要 10,000 节点；NavMesh 只需 2 个三角形。
- `NavMesh.CalculatePath` 返回的 `corners` 已经是世界坐标 `Vector3[]`，可直接用于移动。

### 3.2 Unity NavMesh 体系概览

| 组件/API | 属于 | 说明 |
|---------|------|------|
| `UnityEngine.AI.NavMesh` | `com.unity.modules.ai`（内置） | 静态 API：SamplePosition, CalculatePath, Raycast |
| `UnityEngine.AI.NavMeshAgent` | 同上 | 全自动寻路组件（本项目不使用） |
| `UnityEngine.AI.NavMeshSurface` | `com.unity.ai.navigation`（需安装） | 运行时/编辑器烘焙组件 |
| `NavMeshBuilder` | 内置 | Editor 手动烘焙（Window > AI > Navigation） |

**本项目不使用 NavMeshAgent**，原因：
- 玩家角色是 Animator root motion，没有 CharacterController / Rigidbody
- 怪物如果也用 NavMeshAgent，位移控制权在 Agent，无法与 Animator 配合
- 怪物移动通过 `transform.position = Vector3.MoveTowards(...)` 直接设置位置
- 我们的方案：`NavMesh.CalculatePath` 算路径 → 自己插值移动，与现有模式一致

### 3.3 安装 NavMesh Surface

**操作步骤**：

1. Unity Editor 打开 `Window > Package Manager`
2. 点击 `+` → `Add package by name`，输入 `com.unity.ai.navigation`
3. 等待导入完成。包管理器会添加 `NavMeshSurface`、`NavMeshModifier`、`NavMeshModifierVolume` 等组件

### 3.4 在场景中烘焙 NavMesh

场景使用 `Assets/Scenes/MyTest.unity`。

1. 选择场景中所有地面 Mesh Renderer，Inspector 右上角 `Static` 下拉 → 勾选 `Navigation Static`
2. 对障碍物/墙壁同样勾选 `Navigation Static`
3. 新建空 GameObject → Add Component → `NavMeshSurface`
4. `NavMeshSurface` Inspector 上点击 `Bake`
5. Scene 视图出现蓝色 NavMesh 覆盖区域

**验证**：`Window > AI > Navigation` 打开 Navigation 窗口，切换到 `NavMesh` 面板查看三角形网格。

### 3.5 实现：NavMeshPathDriver.cs

```csharp
using UnityEngine;
using UnityEngine.AI;

namespace AI.Pathfinding
{
    /// <summary>
    /// NavMesh 路径跟随演示组件。
    /// 用于验证 NavMesh.CalculatePath + 手动路径插值。
    /// 后续被 BTNavMeshMove 替代。
    /// </summary>
    public class NavMeshPathDriver : MonoBehaviour
    {
        [Header("参数")]
        public Transform Target;
        public float MoveSpeed = 5f;
        public float StopDistance = 0.5f;
        public bool DrawPath = true;

        private NavMeshPath _path;
        private int _cornerIndex;
        private float _lastTickTime;

        void Awake()
        {
            _path = new NavMeshPath();
        }

        void Start()
        {
            RequestPath();
        }

        void Update()
        {
            if (Target == null || _path?.corners == null || _path.corners.Length == 0)
                return;

            if (DrawPath)
                DrawPathLines();

            if (_cornerIndex >= _path.corners.Length)
                return;

            Vector3 targetCorner = _path.corners[_cornerIndex];
            Vector3 selfPos = transform.position;
            targetCorner.y = selfPos.y;

            float dist = Vector3.Distance(selfPos, targetCorner);

            if (dist <= StopDistance)
            {
                _cornerIndex++;
                return;
            }

            // 与 BTMoveTowards 一致的 Tick-间隔移动
            float dt = Time.time - _lastTickTime;
            _lastTickTime = Time.time;
            transform.position = Vector3.MoveTowards(selfPos, targetCorner, MoveSpeed * dt);
        }

        public void RequestPath()
        {
            if (Target == null) return;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
                return;
            if (!NavMesh.SamplePosition(Target.position, out NavMeshHit endHit, 2f, NavMesh.AllAreas))
                return;

            bool found = NavMesh.CalculatePath(
                startHit.position, endHit.position, NavMesh.AllAreas, _path);
            _cornerIndex = found ? 0 : 0;
            _lastTickTime = Time.time;

            Debug.Log($"[NavMesh] 路径计算 {(found ? "成功" : "失败")}, "
                + $"{_path.corners?.Length ?? 0} 个拐点");
        }

        void OnDrawGizmos()
        {
            if (!DrawPath || _path?.corners == null) return;
            DrawPathLines();
        }

        private void DrawPathLines()
        {
            for (int i = 0; i < _path.corners.Length - 1; i++)
            {
                Vector3 from = _path.corners[i] + Vector3.up * 0.5f;
                Vector3 to   = _path.corners[i + 1] + Vector3.up * 0.5f;
                Debug.DrawLine(from, to, Color.cyan, 0f, false);
            }
        }
    }
}
```

**挂载验证**：

1. 场景中放 CubeA 作为行走者，挂 `NavMeshPathDriver`
2. 放 CubeB 作为目标，拖到 Target 槽
3. 运行，观察蓝色路径线 + CubeA 沿 NavMesh 移动
4. 运行时拖动目标位置，Observe 路径自动更新

### 3.6 路径平滑：Funnel 算法（String Pulling）

NavMesh.CalculatePath 返回的拐点已经是简化过的（去掉共线点），但窄通道（门框、走廊转角）仍有锯齿。

**Funnel 核心思想**（拉绳法）：

```
路径点序列: A → B → C → D → E

维护一个"漏斗"（左边界 L、右边界 R）。
从起点 A 开始，逐个推进顶点：
  推进到 C：左右边界更新
  推进到 D：漏斗"撑破"（新顶点超出当前漏斗开口）
            → 记录上一个顶点 B 为拐点
            → 从 B 重新开始漏斗

最终路径：A → B → D → E（去掉了多余的 C）
```

**实现时机**：Funnel 对 Grid A* 几乎是必须的；对 NavMesh 路径改善有限。理解概念即可，不强制实现。

**参考资料**：http://digestingduck.blogspot.com/2010/03/simple-stupid-funnel-algorithm.html（Mikko Mononen，Recast/Detour 作者）

### 3.7 NavMesh.CalculatePath vs NavMeshAgent 对比

| 维度 | NavMeshAgent | CalculatePath + 手动驱动 |
|------|-------------|------------------------|
| 路径计算 | 自动每帧更新 | 自己控制时机（如 0.5s 间隔） |
| 速度控制 | Agent.speed 属性 | 自己实现 MoveTowards |
| 避障 | 内置 RVO | 需要自己实现（后续方向） |
| 动画融合 | 需额外处理 Root Motion | 完全可控 |
| 斜坡处理 | 自动处理 | 需 SamplePosition 修正 |
| 本项目适用性 | 不适用（root motion 冲突） | 完全适用 |

### 3.8 时间分配

| 时段 | 内容 | 耗时 |
|------|------|------|
| 13:00-13:30 | 阅读 NavMesh 概念（3.1-3.2 节） | 30min |
| 13:30-14:00 | 安装包 + 场景烘焙 | 30min |
| 14:00-15:00 | 编写 NavMeshPathDriver + 验证 | 60min |
| 15:00-15:30 | Funnel 算法理解 | 30min |

**验证清单**：
- [ ] `com.unity.ai.navigation` 包已安装
- [ ] MyTest 场景已烘焙 NavMesh（Scene 视图蓝色覆盖）
- [ ] NavMeshPathDriver 可沿路径移动到目标
- [ ] Debug.DrawLine 显示路径拐点
- [ ] 调整目标位置，路径重新计算

---

## 四、晚上：BTNavMeshMove 节点 + 行为树集成（~2.5 小时）

### 4.1 设计原则

- 完全替换 `BTMoveTowards`，但保留相同的黑板接口（读取 `target` Transform）
- 内部使用 `NavMesh.CalculatePath` 计算路径
- 逐帧沿路径拐点 `Vector3.MoveTowards`，与现有移动模式一致
- 路径缓存 + 周期性重新计算（避免每帧 CalculatePath）
- `[BTNode]` 属性注册，编辑器可发现

### 4.2 实现：BTNavMeshMove.cs

```csharp
using UnityEngine;
using UnityEngine.AI;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct NavMeshMoveData
    {
        [Tooltip("移动速度（米/秒）")]
        public float Speed;

        [Tooltip("黑板中目标 Transform 的键名")]
        public string TargetKey;

        [Tooltip("到达此距离内停止移动")]
        public float StopDistance;

        [Tooltip("路径重新计算间隔（秒）")]
        public float ReplanInterval;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：通过 NavMesh.CalculatePath 寻路移动到目标。
    /// 替换 BTMoveTowards，支持绕行障碍物。
    /// </summary>
    [BTNode("寻路移动到目标", "Action/移动",
            "使用 NavMesh.CalculatePath 计算路径后移动，可绕过障碍物")]
    public class BTNavMeshMove : BTAction<NavMeshMoveData>
    {
        private NavMeshPath _path;
        private int _cornerIndex;
        private float _lastMoveTime;
        private float _lastReplanTime;

        private const float MinStopDist = 0.1f;
        private const float DefaultSpeed = 3f;
        private const float DefaultStopDist = 1.5f;
        private const float DefaultReplanInterval = 0.5f;

        public override void OnEnter(Blackboard bb)
        {
            _path = new NavMeshPath();
            _cornerIndex = 0;
            _lastMoveTime = Time.time;
            _lastReplanTime = -999f; // 强制第一帧重算
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            Transform self = bb.Get<Transform>("_transform");
            Transform target = GetTarget(bb);

            if (self == null || target == null)
                return BTResult.Failure;

            float speed = Data.Speed > 0f ? Data.Speed : DefaultSpeed;
            float stopDist = Data.StopDistance > MinStopDist
                ? Data.StopDistance : DefaultStopDist;
            float replanInterval = Data.ReplanInterval > 0f
                ? Data.ReplanInterval : DefaultReplanInterval;

            // === 直线距离达标即成功 ===
            Vector3 toTarget = target.position - self.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= stopDist)
                return BTResult.Success;

            // === 路径重算 ===
            bool needReplan = Time.time - _lastReplanTime >= replanInterval;
            if (_path.corners == null || _path.corners.Length == 0)
                needReplan = true;
            if (_cornerIndex >= (_path.corners?.Length ?? 0) - 1)
                needReplan = true;

            if (needReplan)
            {
                bool found = CalculatePath(self.position, target.position, _path);
                _lastReplanTime = Time.time;

                if (!found || _path.corners == null || _path.corners.Length == 0)
                    return BTResult.Failure; // 无法到达

                _cornerIndex = 0;
            }

            // === 路径跟随 ===
            if (_cornerIndex >= _path.corners.Length)
                return BTResult.Failure;

            Vector3 targetCorner = _path.corners[_cornerIndex];
            targetCorner.y = self.position.y;

            if (Vector3.Distance(self.position, targetCorner) <= stopDist * 0.5f)
            {
                _cornerIndex++;
                return BTResult.Running;
            }

            float dt = Time.time - _lastMoveTime;
            _lastMoveTime = Time.time;
            self.position = Vector3.MoveTowards(self.position, targetCorner, speed * dt);

            return BTResult.Running;
        }

        private bool CalculatePath(Vector3 start, Vector3 end, NavMeshPath path)
        {
            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(end, out NavMeshHit endHit, 2f, NavMesh.AllAreas))
                return false;

            return NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);
        }

        private Transform GetTarget(Blackboard bb)
        {
            string key = string.IsNullOrEmpty(Data.TargetKey) ? "target" : Data.TargetKey;
            return bb.Get<Transform>(key);
        }
    }
}
```

### 4.3 与 BTMoveTowards 的关键区别

| 维度 | BTMoveTowards（旧） | BTNavMeshMove（新） |
|------|-------------------|-------------------|
| 寻路 | 无，直线 Walk | NavMesh.CalculatePath |
| 障碍物 | 穿过一切 | 沿 NavMesh 绕行 |
| 路径重算 | 无 | 定期重算（默认 0.5s） |
| 路径缓存 | 无 | NavMeshPath + _cornerIndex |
| 无法到达 | 一直直线走 | 返回 Failure |
| SamplePosition | 无 | 有，防起终点不在 NavMesh 上 |

### 4.4 BTMoveTowards 是否删除？

**不建议删除。** 原因：

1. `NewBehaviorTree.asset` 未提交，可能已引用 BTMoveTowards，删除会导致 SO 加载报错
2. 简单空旷场景下 BTMoveTowards 零开销（无 GC Alloc、无路径计算）
3. 保留为"轻量级移动"选项，BTNavMeshMove 作为"智能移动"

在 `BTMoveTowards.cs` 类注释中补充：

```csharp
/// <summary>
/// 动作节点：向目标直线移动（Transform 直接位移，无寻路）。
/// 不经过障碍物绕行。如果需要 NavMesh 寻路请用 BTNavMeshMove 替代。
/// </summary>
```

### 4.5 示例行为树：怪物寻路攻击

```
Selector [
    Sequence [           // 攻击
        BTHasTarget
        BTIsTargetInRange (Range=2.0)
        BTFaceTarget
        BTSetAnimatorTrigger (TriggerName="Attack")
        BTWait (Duration=1.5)
    ]
    Sequence [           // 追击（寻路）
        BTHasTarget
        BTNavMeshMove (Speed=4, StopDistance=1.8, ReplanInterval=0.5)
    ]
    Sequence [           // 巡逻
        BTFindNearestTarget
        BTNavMeshMove (Speed=2, TargetKey="patrolPoint", StopDistance=0.5)
    ]
]
```

### 4.6 路径可视化

**方案一：Debug.DrawLine（编辑器调试用）**

在 BTNavMeshMove.OnExecute 中补充：

```csharp
#if UNITY_EDITOR
private void DrawPath()
{
    if (_path?.corners == null) return;
    for (int i = 0; i < _path.corners.Length - 1; i++)
    {
        Debug.DrawLine(
            _path.corners[i] + Vector3.up * 0.5f,
            _path.corners[i + 1] + Vector3.up * 0.5f,
            Color.cyan, 0f, false);
    }
}
#endif
```

**方案二：LineRenderer 世界空间路径**

在 `NavMeshMoveData` 中加可选 Prefab 引用，`OnEnter` 时实例化 LineRenderer，路径更新时 `SetPositions()`。`OnExit` 时销毁。

**方案三：小地图路径叠加（后续方向）**

需要一个现成的小地图系统（RenderTexture + RawImage）才能叠加。本日不做。

### 4.7 时间分配

| 时段 | 内容 | 耗时 |
|------|------|------|
| 19:00-19:30 | 阅读设计（4.1-4.3 节） | 30min |
| 19:30-20:30 | 编写 BTNavMeshMove.cs | 60min |
| 20:30-21:00 | 行为树 SO 中替换 + 场景验证 | 30min |
| 21:00-21:30 | 可选：LineRenderer 路径渲染 | 30min |
| 21:30-22:00 | 复盘收尾 | 30min |

**验证清单**：
- [ ] 怪物挂 BehaviorTreeRunner + 含 BTNavMeshMove 的 SO
- [ ] 运行时怪物沿 NavMesh 绕行障碍物
- [ ] 与旧 BTMoveTowards 行为一致（速度、停靠距离）
- [ ] 目标移动后路径自动重算
- [ ] 无法到达时返回 Failure（不原地抽搐）
- [ ] （可选）路径线显示

---

## 五、整体架构图

```
┌─────────────────────────────────────────────────────────────┐
│                   学习路线                                   │
│                                                             │
│  上午                        下午                  晚上      │
│  AStarGrid (纯C#)    →   NavMesh.CalculatePath   →   BTNavMeshMove  │
│  理解算法               理解工业级实现             行为树集成      │
│                                                             │
└─────────────────────────────────────────────────────────────┘

┌──────────┐    ┌──────────────────┐    ┌────────────────────┐
│ AStarGrid │    │ NavMeshPathDriver│    │   BTNavMeshMove    │
│           │    │                  │    │                    │
│ Grid A*   │    │ CalculatePath    │    │ BTAction<NavMesh..>│
│ 4/8 dirs  │    │ SamplePosition   │    │ OnEnter/OnExecute  │
│ Heuristic │    │ Path Follow      │    │ Replan Logic       │
│           │    │ DrawDebugLine    │    │ Path Caching       │
└──────────┘    └──────────────────┘    └────────────────────┘
      │                   │                       │
      │                   │                       │
      ▼                   ▼                       ▼
  学习算法          理解 Unity NavMesh       项目实际使用
  (理解原理)        (掌握工具)               (产出功能)
```

---

## 六、常见问题与调试

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| CalculatePath 返回 false | 起点/终点不在 NavMesh 上 | 调大 SamplePosition maxDistance，检查场景蓝色 NavMesh 覆盖 |
| 怪物穿过墙壁 | 行为树 SO 未更新节点 | 检查 TreeAsset 中是否还是 BTMoveTowards |
| 路径走到一半不动 | 拐点消耗完但未到达 | 检查 StopDistance 是否太小，或终点 SamplePosition 飘到意外位置 |
| 每帧卡顿 | CalculatePath 调用太频繁 | 检查 ReplanInterval（建议 ≥ 0.3s） |
| 路径拐点跳变 | SamplePosition 不同帧返回不同位置 | 缓存上一次 endHit，目标移动 < threshold 时复用路径 |
| 编辑器报错 "The type or namespace name 'NavMesh' does not exist" | 缺少 AI 模块引用 | 检查 manifest.json 中 `"com.unity.modules.ai": "1.0.0"` |
| Bake 按钮灰色 | 未标记 Navigation Static | 地面 GameObject → Static 下拉 → Navigation Static |

---

## 七、后续扩展方向

| 方向 | 说明 | 前置 |
|------|------|------|
| NavMesh 动态障碍 | NavMeshObstacle 组件，可移动的箱子/门实时更新 | NavMesh 烘焙完成 |
| RVO 避障 | 多个怪物之间互相避让 | BTNavMeshMove + 额外遮挡逻辑 |
| 分层寻路 | 大世界切分区块，区块间 Waypoint Graph，区块内 NavMesh | A* + NavMesh 基础 |
| 小地图系统 | RenderTexture + RawImage + 路径叠加 UI | 本文可选路径渲染 |
| Crowd 群体 | Flocking + NavMesh 路径约束 | RVO 基础 |
| NavMesh 动态更新 | 可破坏场景，打碎墙壁后重新烘焙 | NavMeshSurface 运行时烘焙 |

**小地图定位（用户可选需求）**：

路径可视化的自然延伸。核心思路：
1. 世界坐标 → 小地图 UV 坐标的变换（正交投影或相机矩阵）
2. 在 UI 层（RawImage）上叠加路径 LineRenderer
3. 需要先建立小地图系统（RenderTexture + 俯视相机 + Canvas RawImage）

---

## 八、今日学习自检清单

- [ ] 能手写 A\* 伪代码并解释 G/H/F 含义
- [ ] 能说出三种启发式函数及其适用场景
- [ ] AStarGrid 在 10×10 网格上正确绕行障碍物
- [ ] Unity NavMesh 已烘焙，Scene 视图显示蓝色覆盖
- [ ] 理解 `NavMesh.CalculatePath` 与 `NavMeshAgent` 的区别
- [ ] NavMeshPathDriver 可沿 NavMesh 路径移动
- [ ] 理解 SamplePosition 的必要性
- [ ] BTNavMeshMove 在行为树中正确驱动怪物移动
- [ ] 知道路径重算时机和间隔的配置意义
- [ ] （可选）路径线渲染可用

---

> **总结**：一天之内，从算法原理（A\*）到工业实现（NavMesh）再到项目集成（行为树节点），完成了寻路系统的完整闭环。三个代码文件对应三个学习层次，既可独立运行验证，也可串联使用。
