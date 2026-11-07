using System;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Cameras
{
    /// <summary>
    /// 管理八档观察方位和行动者焦点。
    /// </summary>
    /// <remarks>
    /// <para>每段缓动保存三类值：start是固定起点，current是当前画面状态，target是最终目标。</para>
    /// <para>旋转、缩放和居中采用定时缓动，连续操作从当前画面接续；拖拽焦点立即跟手。</para>
    /// <para>相机位姿由焦点、观察角度和距离共同决定，不直接对世界位置做直线插值。</para>
    /// </remarks>
    public sealed class BattleCameraController : IDisposable
    {
        /// <summary>观察方位档数。</summary>
        private const int DIRECTION_COUNT = 8;
        /// <summary>相邻档位的水平角差（度）。</summary>
        private const float DIRECTION_ANGLE = 45f;
        /// <summary>受控的场景相机。</summary>
        private readonly Camera _camera;
        /// <summary>棋盘格子与世界坐标转换器。</summary>
        private readonly GridCoordinateConverter _coordinates;
        /// <summary>控制器接管前的世界位置，释放时恢复。</summary>
        private readonly Vector3 _originalPosition;
        /// <summary>控制器接管前的世界旋转，释放时恢复。</summary>
        private readonly Quaternion _originalRotation;
        /// <summary>装配时已校验的只读镜头配置。</summary>
        private readonly BattleCameraSettings _settings;
        /// <summary>焦点的世界XZ边界，Rect.y对应世界z；非画面边界。</summary>
        private readonly Rect _focusBounds;
        /// <summary>当前世界焦点，非相机位置。</summary>
        private Vector3 _currentFocus;
        /// <summary>本段缓动的世界焦点起点。</summary>
        private Vector3 _startFocus;
        /// <summary>
        /// 当前水平朝向角（Azimuth，度），对应Euler Y角；0°朝+Z，90°朝+X，非俯角。
        /// </summary>
        private float _currentAzimuth;
        /// <summary>本段缓动的水平起始角（度）。</summary>
        private float _startAzimuth;
        /// <summary>
        /// 目标水平角（度），不归一化到0～360°。
        /// </summary>
        private float _targetAzimuth;
        /// <summary>当前俯角（度），对应Euler X角，正值向下看。</summary>
        private float _currentElevation;
        /// <summary>本段缓动的起始俯角（度）。</summary>
        private float _startElevation;
        /// <summary>本段缓动已用时间（秒，非缩放时间）。</summary>
        private float _elapsed;
        /// <summary>当前相机到焦点的直线距离（世界单位）。</summary>
        private float _currentDistance;
        /// <summary>本段缓动的起始观察距离（世界单位）。</summary>
        private float _startDistance;
        /// <summary>上次同步的行动者。</summary>
        private CombatUnit _actor;
        /// <summary>上次同步时行动者的格子位置。</summary>
        private Vector2Int _actorPosition;

        /// <summary>目标观察档位（0～7），相对配置初始方位。</summary>
        public int DirectionIndex { get; private set; }
        /// <summary>目标世界焦点。</summary>
        public Vector3 Focus { get; private set; }
        /// <summary>目标观察距离（世界单位）。</summary>
        public float Distance { get; private set; }

        /// <summary>记录原位姿并初始化状态，不立即移动场景相机。</summary>
        public BattleCameraController(UnityEngine.Camera camera, GridCoordinateConverter coordinates,
            BattleCameraSettings settings, Rect focusBounds)
        {
            _camera = camera;
            _coordinates = coordinates;
            _settings = settings;
            _focusBounds = focusBounds;
            Distance = _currentDistance = settings.InitialDistance;
            _originalPosition = camera.transform.position;
            _originalRotation = camera.transform.rotation;
            _elapsed = settings.TransitionDuration;
            _currentAzimuth = camera.transform.eulerAngles.y;
            // 将Euler可能返回的350°转换为-10°这类有符号俯角，避免向配置俯角插值时绕远路。
            _currentElevation = Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            // 沿原朝向前进初始距离得到焦点；反算“焦点-朝向×距离”仍是原位置，接管时不会跳动。
            _currentFocus = camera.transform.position + camera.transform.forward * settings.InitialDistance;
            Focus = _currentFocus;
            // 初始目标用最短有符号角差建立，例如当前350°、配置0°时取360°而不是0°。
            _targetAzimuth = _currentAzimuth + Mathf.DeltaAngle(_currentAzimuth, settings.InitialAzimuth);
        }

        /// <summary>换人或角色移动时更新目标焦点并开始过渡；保留旋转和缩放目标。</summary>
        public void SynchronizeActor(CombatUnit actor)
        {
            if (ReferenceEquals(_actor, actor) && _actorPosition == actor.Position) return;

            _actor = actor;
            _actorPosition = actor.Position;
            Focus = _coordinates.GridToWorld(actor.Position);
            BeginTransition();
        }

        /// <summary>更新目标档位及水平角，从当前画面状态重新开始缓动。</summary>
        /// <param name="direction">-1为左旋，1为右旋，每次45°。</param>
        public void Rotate(int direction)
        {
            // 档位循环在0～7；先加总档数，使第0档左旋时仍得到非负余数7。
            DirectionIndex = (DirectionIndex + direction + DIRECTION_COUNT) % DIRECTION_COUNT;
            // 角度持续累加，例如315°继续转到360°而非归零，避免缓动反向绕圈。
            _targetAzimuth += direction * DIRECTION_ANGLE;
            // 例如0°→45°途中已到20°，再次右旋后目标为90°，从当前20°重新起步，不先跳到45°。
            BeginTransition();
        }

        /// <summary>把当前画面状态记为新起点并清零计时。</summary>
        private void BeginTransition()
        {
            // 调用方已更新目标；这里只做start=current，不做current=target，否则画面会瞬移。
            // 各通道共用计时，需一起捕获当前状态；未被本次输入修改的目标仍保留，继续向原目标推进。
            _startFocus = _currentFocus;
            _startAzimuth = _currentAzimuth;
            _startElevation = _currentElevation;
            _startDistance = _currentDistance;
            _elapsed = 0f;
        }

        /// <summary>按滚轮量更新目标距离并限幅；目标不变时不重启过渡。</summary>
        public void Zoom(float scrollDelta)
        {
            // 滚轮量×步长得到距离变化，正值拉近、负值拉远，再限制到允许范围。
            // 基于已有目标Distance累计，而非尚在缓动的_currentDistance，避免连续滚动丢失步数。
            float distance = Mathf.Clamp(Distance - scrollDelta * _settings.ZoomStep, _settings.MinDistance, _settings.MaxDistance);
            if (distance == Distance) return;
            Distance = distance;
            BeginTransition();
        }

        /// <summary>根据已用时间，从固定起点插值到目标，再应用相机位姿。</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_elapsed >= _settings.TransitionDuration) return;
            // 将累计时间封顶到总时长，确保最后一帧的时间比例恰好为1。
            _elapsed = Mathf.Min(_elapsed + unscaledDeltaTime, _settings.TransitionDuration);
            // u=已用时间/总时长；SmoothStep映射为s=3u²-2u³，使过渡起止速度为零。
            float progress = Mathf.SmoothStep(0f, 1f, _elapsed / _settings.TransitionDuration);
            // Lerp=start+(target-start)×s。没有新输入时start固定，不是每帧从current再靠近一部分。
            // s到1即准确到达目标；仅更新current，不反过来覆盖start或target。
            _currentFocus = Vector3.Lerp(_startFocus, Focus, progress);
            _currentAzimuth = Mathf.Lerp(_startAzimuth, _targetAzimuth, progress);
            _currentElevation = Mathf.Lerp(_startElevation, _settings.Elevation, progress);
            _currentDistance = Mathf.Lerp(_startDistance, Distance, progress);
            ApplyPose();
        }

        /// <summary>将鼠标位移投影到棋盘平面，立即平移并限幅焦点，不播放焦点缓动。</summary>
        public void Pan(Vector2 previousScreenPosition, Vector2 screenPosition)
        {
            // 用棋盘高度上的无限水平面投影，不受角色碰撞体或鼠标移到棋盘外影响。
            var plane = new Plane(Vector3.up, _coordinates.Origin);
            var previousRay = _camera.ScreenPointToRay(previousScreenPosition);
            var ray = _camera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(previousRay, out float previousDistance) || !plane.Raycast(ray, out float distance)) return;

            // 同一相机位姿下，旧、新屏幕点投影为P、Q；焦点加P-Q，让原来抓住的地图点跟随鼠标。
            var focus = _currentFocus + previousRay.GetPoint(previousDistance) - ray.GetPoint(distance);
            // 只限制焦点的世界XZ，Y固定为棋盘高度；Rect.y存的是世界z，不是高度。
            focus.x = Mathf.Clamp(focus.x, _focusBounds.xMin, _focusBounds.xMax);
            focus.y = _coordinates.Origin.y;
            focus.z = Mathf.Clamp(focus.z, _focusBounds.yMin, _focusBounds.yMax);
            // 将焦点的目标、当前值和起点统一为拖后位置，下帧Lerp两端相同，不会被旧焦点过渡拉回。
            // 不重置角度、距离和计时，尚未完成的旋转与缩放继续推进。
            Focus = _currentFocus = _startFocus = focus;
            ApplyPose();
        }

        /// <summary>将当前焦点、角度和距离换算为相机世界位置及朝向。</summary>
        private void ApplyPose()
        {
            // Euler的X为俯角、Y为水平角、Z为0（无侧倾）；旋转×forward得到世界朝向单位向量。
            var rotation = Quaternion.Euler(_currentElevation, _currentAzimuth, 0f);
            // 相机位置=焦点-朝向×距离，沿该朝向前进指定距离恰好到达焦点。
            // 例如水平角0°时，相机在焦点-Z侧向+Z看；角度插值后重建位置，避免直线插值切穿轨道。
            _camera.transform.SetPositionAndRotation(_currentFocus - rotation * Vector3.forward * _currentDistance, rotation);
        }

        /// <summary>相机仍存在时立即恢复接管前位姿，不播放返回缓动。</summary>
        public void Dispose()
        {
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
            }
        }
    }
}
