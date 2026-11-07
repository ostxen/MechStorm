using System;
using UnityEngine;

namespace MechStorm.Presentation.Cameras
{
    /// <summary>
    /// 镜头调参数据，运行时只读；不保存场景引用、棋盘边界或本场镜头状态。
    /// </summary>
    [Serializable]
    public sealed class BattleCameraSettings
    {
        /// <summary>
        /// 第0档的目标水平朝向（度，Euler Y角），0°朝+Z，90°朝+X；不限制在0～360°。
        /// </summary>
        [SerializeField] private float _initialAzimuth = 0f;

        /// <summary>
        /// 固定目标俯角（度，Euler X角），范围为0°～90°（不含端点），越大越接近俯视。
        /// </summary>
        [SerializeField] private float _elevation = 45f;

        /// <summary>
        /// 初始相机到焦点的直线距离（世界单位），须在最小与最大距离之间，含端点。
        /// </summary>
        [SerializeField] private float _initialDistance = 10f;

        /// <summary>
        /// 每段缓动的总时长（秒，非缩放时间），须大于0；不用于拖拽焦点。
        /// </summary>
        [SerializeField] private float _transitionDuration = 0.25f;

        /// <summary>
        /// 相机到焦点的最小直线距离（世界单位），须大于0。
        /// </summary>
        [SerializeField] private float _minDistance = 6f;

        /// <summary>
        /// 相机到焦点的最大直线距离（世界单位），须不小于最小距离。
        /// </summary>
        [SerializeField] private float _maxDistance = 18f;

        /// <summary>
        /// 每单位滚轮输入的距离变化步长（世界单位），须大于0。
        /// </summary>
        [SerializeField] private float _zoomStep = 1f;

        /// <summary>
        /// 相对鼠标按下位置的拖拽阈值（屏幕像素），须大于0；位移超过阈值才判为拖拽。
        /// </summary>
        [SerializeField] private float _dragThreshold = 8f;

        public float InitialAzimuth => _initialAzimuth;
        public float Elevation => _elevation;
        public float InitialDistance => _initialDistance;
        public float TransitionDuration => _transitionDuration;
        public float MinDistance => _minDistance;
        public float MaxDistance => _maxDistance;
        public float ZoomStep => _zoomStep;
        public float DragThreshold => _dragThreshold;

        // 代码侧创建参数；场景侧通过Inspector编辑序列化字段。
        public BattleCameraSettings(float initialAzimuth = 0f, float elevation = 45f, float initialDistance = 10f,
            float transitionDuration = 0.25f, float minDistance = 6f, float maxDistance = 18f,
            float zoomStep = 1f, float dragThreshold = 8f)
        {
            _initialAzimuth = initialAzimuth;
            _elevation = elevation;
            _initialDistance = initialDistance;
            _transitionDuration = transitionDuration;
            _minDistance = minDistance;
            _maxDistance = maxDistance;
            _zoomStep = zoomStep;
            _dragThreshold = dragThreshold;
        }

        // Runtime装配时校验一次。
        public void Validate()
        {
            if (!IsFinite(_initialAzimuth)) throw new ArgumentOutOfRangeException(nameof(InitialAzimuth));
            if (!IsFinite(_elevation) || _elevation <= 0f || _elevation >= 90f)
                throw new ArgumentOutOfRangeException(nameof(Elevation));
            if (!IsFinite(_minDistance) || _minDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(MinDistance));
            if (!IsFinite(_maxDistance) || _maxDistance < _minDistance) throw new ArgumentOutOfRangeException(nameof(MaxDistance));
            if (!IsFinite(_initialDistance) || _initialDistance < _minDistance || _initialDistance > _maxDistance)
                throw new ArgumentOutOfRangeException(nameof(InitialDistance));
            if (!IsFinite(_zoomStep) || _zoomStep <= 0f) throw new ArgumentOutOfRangeException(nameof(ZoomStep));
            if (!IsFinite(_transitionDuration) || _transitionDuration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(TransitionDuration));
            if (!IsFinite(_dragThreshold) || _dragThreshold <= 0f)
                throw new ArgumentOutOfRangeException(nameof(DragThreshold));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
