using UnityEngine;

namespace TEngine
{
    public static partial class Utility
    {
        public static partial class Tween
        {
            public partial interface ITweenHelper
            {
                /// <summary>按控制点数组移动目标。</summary>
                public long MoveBezierPath(Transform target, Vector3[] path, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false);

                /// <summary>
                /// 按样条参数移动目标。样条坐标与目标位置必须处于同一坐标空间。
                /// </summary>
                public long MoveBezierPath(Transform target, BezierSpline path, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false,
                    bool orientToPath = false);

                /// <summary>根据起点、终点和弧线参数生成路径并移动目标。</summary>
                public long MoveBezierPath(Transform target, Vector3 start, Vector3 end, float duration,
                    float arcHeight = 2f, float sideOffset = 0f, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0,
                    bool useUnscaledTime = false, bool orientToPath = false);
            }
        }
    }
}
