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

            //初始化网格
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
            //超范围
            if (!InBounds(startX, startY) || !InBounds(endX, endY))
                return new List<Vector2Int>();
            //起点终点不可走
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
            //设置起点为待探索点
            open.Add(start);

            //当存在待探索点
            while (open.Count > 0)
            {
                //F最小值为现在的点
                AStarNode current = FindLowestF(open);

                //现在的点是终点了
                if (current == goal)
                    return ReconstructPath(current);

                open.Remove(current);
                closed.Add(current);

                //遍历邻居
                foreach (AStarNode neighbor in GetNeighbors(current))
                {
                    if (!neighbor.Walkable || closed.Contains(neighbor))
                        continue;

                    int moveCost = IsDiagonal(current, neighbor) ? 14 : 10;
                    int tentativeG = current.GCost + moveCost;
                    
                    //新邻居
                    if (!open.Contains(neighbor))
                    {
                        // 首次发现
                        neighbor.Parent = current;
                        neighbor.GCost = tentativeG;
                        //启发式搜索
                        neighbor.HCost = Heuristic(neighbor.X, neighbor.Y, endX, endY);
                        open.Add(neighbor);
                    }
                    //发现某一点gone的最更短路径
                    else if (tentativeG < neighbor.GCost)
                    {
                        // 发现了更短路径
                        neighbor.Parent = current;
                        //更新gone长度
                        neighbor.GCost = tentativeG;
                        // HCost 不变
                    }
                }
            }

            return new List<Vector2Int>(); // OPEN 耗尽，无路可达
        }

        //找F最小值
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
        
        //是否走斜角
        private bool IsDiagonal(AStarNode from, AStarNode to)
            => from.X != to.X && from.Y != to.Y;

        //启发式搜索
        private int Heuristic(int x1, int y1, int x2, int y2)
        {
            int dx = Mathf.Abs(x1 - x2);
            int dy = Mathf.Abs(y1 - y2);
            // Diagonal 距离：max(dx,dy)*10 + (√2-1)*min(dx,dy)*10 ≈ max(dx,dy)*10 + min(dx,dy)*4
            return Mathf.Max(dx, dy) * 10 + Mathf.Min(dx, dy) * 4;
        }

        //取最后节点串联并倒序
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