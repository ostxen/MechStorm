using System;
using System.Collections.Generic;
using MechStorm.Battle.Foundation;

namespace MechStorm.Battle.Spatial
{
    public sealed class SquareGrid
    {
        public int Width { get; }
        public int Height { get; }
        
        public SquareGrid(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be greater than zero.");
            }

            Width = width;
            Height = height;
        }
        
        // 四方向邻居偏移：上、下、左、右。
        private static readonly Vector2Int[] _fourDirections =
        {
            new(0, 1),
            new(0, -1),
            new(-1, 0),
            new(1, 0),
        };

        public bool IsInside(Vector2Int position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
        
        public int GetManhattanDistance(Vector2Int from, Vector2Int to)
        {
            return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        }
        
        // 返回只读列表语义，调用方只消费邻居结果，不应修改集合。
        public IReadOnlyList<Vector2Int> GetFourDirectionNeighbors(Vector2Int position)
        {
            var neighbors = new List<Vector2Int>(_fourDirections.Length);
            foreach (var direction in _fourDirections)
            {
                var neighbor = position + direction;
                if (IsInside(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        public IReadOnlyList<Vector2Int> GetReachablePositions(Vector2Int start, int range)
        {
            return GetReachablePositions(start, range, _ => false);
        }

        public IReadOnlyList<Vector2Int> GetReachablePositions(Vector2Int start, int range, Func<Vector2Int, bool> isPositionBlocked)
        {
            if (!IsInside(start))
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "Start position must be inside the grid.");
            }

            if (range < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "Range must be non-negative.");
            }

            if (isPositionBlocked == null)
            {
                throw new ArgumentNullException(nameof(isPositionBlocked));
            }
            
            // BFS 队列保存待扩展的格子；Queue 先进先出，保证先处理近距离格子。
            // visited 防止重复访问，distances 记录距离起点的步数。
            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var distances = new Dictionary<Vector2Int, int>();
            var reachablePositions = new List<Vector2Int>();
            
            // 起点距离为 0，默认也属于可达范围。
            queue.Enqueue(start);
            visited.Add(start);
            distances[start] = 0;
            reachablePositions.Add(start);
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (distances[current] >= range)
                {
                    continue;
                }

                // 从当前格子向四方向扩散一层，第一次访问到的格子就是最短步数。
                foreach (var direction in _fourDirections)
                {
                    var neighbor = current + direction;
                    
                    if (!IsInside(neighbor))
                    {
                        continue;
                    }

                    if (isPositionBlocked(neighbor))
                    {
                        continue;
                    }
                    
                    if (!visited.Add(neighbor))
                    {
                        continue;
                    }
                    
                    distances[neighbor] = distances[current] + 1;
                    reachablePositions.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return reachablePositions;
        }
    }
}