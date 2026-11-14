using System;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Board
{
    // Battle 的 Vector2Int 是平面格子坐标：X 映射 Unity x，Y 映射 Unity z；Unity y 只表示表现高度。
    public sealed class GridCoordinateConverter
    {
        // 单个网格在 Unity 世界中的尺寸；需要与棋盘渲染器使用同一个公共参数。
        public float CellSize { get; }

        // 整个棋盘左下角边界点在 Unity 世界中的坐标，不是 Grid(0,0) 的中心。
        public Vector3 Origin { get; }

        public GridCoordinateConverter() : this(1f, Vector3.zero)
        {
        }

        public GridCoordinateConverter(float cellSize, Vector3 origin)
        {
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be greater than zero.");
            }

            CellSize = cellSize;
            Origin = origin;
        }

        // 返回指定格子的中心点世界坐标。
        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            var x = Origin.x + (gridPosition.X + 0.5f) * CellSize;
            var z = Origin.z + (gridPosition.Y + 0.5f) * CellSize;
            return new Vector3(x, Origin.y, z);
        }

        // 返回世界坐标所在的格子坐标；落在边界上的点按 Floor 归入右侧或上侧格子。
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            var x = Mathf.FloorToInt((worldPosition.x - Origin.x) / CellSize);
            var y = Mathf.FloorToInt((worldPosition.z - Origin.z) / CellSize);
            return new Vector2Int(x, y);
        }
    }
}
