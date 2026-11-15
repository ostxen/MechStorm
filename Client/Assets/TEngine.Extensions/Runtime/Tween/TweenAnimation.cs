using System;
using LitMotion;
using LitMotion.Adapters;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TEngine.Extensions.Tween
{
    [Serializable]
    public sealed partial class TweenAnimation : IDisposable
    {
        public readonly struct TimelineEntry
        {
            public readonly int Index;
            public readonly string TargetName;
            public readonly TweenType Type;
            public readonly AddType Operation;
            public readonly double Start, ActiveStart, End;
            public readonly float Duration;
            public readonly int Loops;
            public readonly LoopType Loop;

            internal TimelineEntry(int index, SequenceAnimation item, double start, float duration, double length)
            {
                Index = index;
                TargetName = item.AnimationType == TweenType.Wait ? "等待" : item.Target.name;
                Type = item.AnimationType;
                Operation = item.AddType;
                Start = start;
                ActiveStart = start + (item.AnimationType == TweenType.Wait ? 0 : item.Delay);
                End = start + length;
                Duration = duration;
                Loops = item.AnimationType == TweenType.Wait ? 1 : item.Loops;
                Loop = item.AnimationType == TweenType.Wait ? LoopType.Restart : item.LoopType;
            }
        }

        public sealed class TimelineSnapshot
        {
            public System.Collections.Generic.IReadOnlyList<TimelineEntry> Entries { get; }
            public double Duration { get; }
            public float Delay { get; }
            public int Loops { get; }
            public LoopType Loop { get; }
            public LitMotion.Ease Ease { get; }
            public bool Reverse { get; }
            public bool Estimated { get; }
            // 根序列的 Duration 在 LitMotion 中转换为 float，保持相同精度。
            public double TotalDuration => Loops < 0 ? double.PositiveInfinity : Delay + (float)Duration * Loops;

            internal TimelineSnapshot(TimelineEntry[] entries, double duration, float delay, int loops,
                LoopType loop, LitMotion.Ease ease, bool reverse, bool estimated)
            {
                Entries = Array.AsReadOnly(entries);
                Duration = duration;
                Delay = delay;
                Loops = loops;
                Loop = loop;
                Ease = ease;
                Reverse = reverse;
                Estimated = estimated;
            }

            /// <summary>根动画经过时间映射到编排位置；保留缓动过冲及 Incremental 越界值。</summary>
            public double GetPosition(double elapsed)
            {
                var duration = (float)Duration;
                if (duration <= 0) return 0;
                var time = Math.Max(0, elapsed) - Delay;
                var completed = Math.Max(0, Math.Floor(time / duration));
                if (Loops >= 0) completed = Math.Min(completed, Loops);
                var t = Loops >= 0 && completed >= Loops ? 1.0 : Math.Max(0, Math.Min(1, (time - duration * completed) / duration));
                var eased = EaseUtility.Evaluate((float)t, Ease);
                if (Loop == LoopType.Yoyo && (completed + (int)t) % 2 == 1) eased = 1f - eased;
                else if (Loop == LoopType.Incremental)
                    return Duration * (EaseUtility.Evaluate(1f, Ease) * completed + EaseUtility.Evaluate((float)(t % 1), Ease));
                return Duration * eased;
            }
        }

        [HideInInspector, SerializeField] private SequenceAnimation[] m_Sequence = Array.Empty<SequenceAnimation>();
        [SerializeField] private bool m_PlayOnAwake;
        [SerializeField] private bool m_ResetOnAwake;
        public enum TriggerMode { Restart, IgnoreWhilePlaying, ContinueOrReverse }
        [SerializeField] private TriggerMode m_TriggerMode;
        [SerializeField, Min(0f)] private float m_Delay;
        [SerializeField] private LitMotion.Ease m_Ease = LitMotion.Ease.Linear;
        [SerializeField, Tooltip("-1 为无限循环，正整数为播放次数。")] private int m_Loops = 1;
        [SerializeField] private LoopType m_LoopType = LoopType.Restart;
        [SerializeField] private UpdateType m_UpdateType = UpdateType.Normal;
        [SerializeField] private bool m_IgnoreTimeScale = true;
        [SerializeField] private UnityEvent m_OnPlay = new UnityEvent();
        [SerializeField] private UnityEvent m_OnUpdate = new UnityEvent();
        [SerializeField] private UnityEvent m_OnComplete = new UnityEvent();

        private MotionHandle m_Motion;
        private bool m_SuppressCallbacks;
        private bool m_ReversibleMotion;
        private bool m_Backward;
        public bool IsBackward => m_ReversibleMotion && m_Backward;

        [NonSerialized] private MonoBehaviour m_Owner;
        [NonSerialized] private bool m_Initialized;
        public MotionHandle Handle => m_Motion;
        internal UpdateType UpdatePhase => m_UpdateType;

        /// <summary>在宿主 Awake 中调用；宿主 OnDisable 调用 DOKill，动态移除动画时调用 Dispose。</summary>
        public void Initialize(MonoBehaviour owner)
        {
            BindOwner(owner);
            if (!Application.isPlaying) return;
            if (m_Initialized) return;
            m_Initialized = true;
            TweenAnimationDriver.Register(this, owner);
            if (m_PlayOnAwake) DOPlay();
            else if (m_ResetOnAwake) ResetToFromValue();
        }

        internal void BindOwner(MonoBehaviour owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (m_Owner != null && m_Owner != owner)
                throw new InvalidOperationException("同一动画实例不能由多个组件共享。");
            m_Owner = owner;
        }

        /// <summary>停止并解除宿主注册；再次使用前重新 Initialize。</summary>
        public void Dispose()
        {
            DOKill();
            if (m_Owner != null) TweenAnimationDriver.Unregister(this, m_Owner);
            m_Owner = null;
            m_Initialized = false;
        }

        public void Play() => DOPlay();

        public MotionHandle DOPlay() => PlaySequence(false);

        public MotionHandle DORewind() => PlaySequence(true);

        /// <summary>供交互使用的触发入口；不改变 DOPlay 和方向 API 的既有语义。</summary>
        public MotionHandle Trigger()
        {
            if (m_TriggerMode == TriggerMode.IgnoreWhilePlaying && m_Motion.IsActive()
                && m_Motion.PlaybackSpeed != 0 && m_Motion.Time < m_Motion.TotalDuration
                && (!m_Backward || m_Motion.Time > 0)) return m_Motion;
            if (m_TriggerMode == TriggerMode.ContinueOrReverse)
                return m_ReversibleMotion && m_Motion.IsActive()
                    ? PlayDirection(!m_Backward) : PlayForward();
            return DOPlay();
        }

        public void SetState(bool expanded)
        {
            if (expanded) PlayForward();
            else PlayBackward();
        }

        public MotionHandle PlayForward() => PlayDirection(false);
        public MotionHandle PlayBackward() => PlayDirection(true);

        private MotionHandle PlayDirection(bool backward)
        {
            ValidateOrThrow();
            if (!m_ReversibleMotion || !m_Motion.IsActive())
            {
                PlaySequence(false);
                m_ReversibleMotion = true;
                m_Motion.Preserve();
                m_SuppressCallbacks = true;
                try { m_Motion.Time = backward ? (double.IsPositiveInfinity(m_Motion.TotalDuration)
                    ? m_Motion.Delay + m_Motion.Duration : m_Motion.TotalDuration) : 0; }
                finally { m_SuppressCallbacks = false; }
            }
            m_Backward = backward;
            // 已在对应端点时不重新采样或重建。
            m_Motion.PlaybackSpeed = backward
                ? (m_Motion.Time <= 0 ? 0 : -1)
                : (m_Motion.Time >= m_Motion.TotalDuration ? 0 : 1);
            return m_Motion;
        }

        public void MaintainReversibleEndpoint()
        {
            if (!m_ReversibleMotion || !m_Motion.IsActive()) return;
            var time = m_Motion.Time;
            if (time > 0 && time < m_Motion.TotalDuration) return;
            var end = time >= m_Motion.TotalDuration;
            if ((end && !m_Backward) || (!end && m_Backward))
            {
                m_Motion.PlaybackSpeed = 0;
                if (time < 0 || time > m_Motion.TotalDuration)
                {
                    m_SuppressCallbacks = true;
                    try { m_Motion.Time = end ? m_Motion.TotalDuration : 0; }
                    finally { m_SuppressCallbacks = false; }
                }
            }
        }

        /// <summary>
        /// 校验后替换动画列表并取消当前播放；调用 DOPlay 开始新列表。
        /// 复制数组容器，但动画项及其曲线、事件仍由调用方与动画共享，播放期间不要修改。
        /// </summary>
        public void SetAnimations(SequenceAnimation[] animations)
        {
            ValidateOrThrow(animations);
            var replacement = (SequenceAnimation[])animations.Clone();
            DOKill();
            m_Sequence = replacement;
        }

        public void DOComplete(bool withCallback = false)
        {
            if (m_Motion.IsPlaying())
            {
                if (m_Motion.Loops == -1)
                    throw new InvalidOperationException("无限循环没有完成终点，请调用 DOKill() 停止。");
                m_SuppressCallbacks = !withCallback;
                try
                {
                    m_Motion.Complete();
                }
                finally
                {
                    m_SuppressCallbacks = false;
                }
            }
        }

        public void DOKill()
        {
            var motion = m_Motion;
            m_Motion = MotionHandle.None;
            m_ReversibleMotion = false;
            m_Backward = false;
            // TryCancel 对已完成、已取消或已由目标销毁回调取消的句柄保持幂等。
            motion.TryCancel();
        }

        public static Type GetRequiredComponentType(TweenType animationType) =>
            SequenceAnimation.GetRequiredComponentType(animationType);

        /// <summary>返回首个配置错误，动画项编号从 1 开始；校验通过返回 null。</summary>
        public string GetValidationError() => GetValidationError(m_Sequence);

        /// <summary>当前端点采样得到的编排快照；不创建动画，也不修改目标。</summary>
        public TimelineSnapshot CaptureTimeline(bool reverse = false)
        {
            ValidateOrThrow();
            var entries = new TimelineEntry[m_Sequence.Length];
            double lastStart = 0, duration = 0;
            var estimated = false;
            for (var order = 0; order < entries.Length; order++)
            {
                var index = reverse ? entries.Length - 1 - order : order;
                var item = m_Sequence[index];
                var start = item.GetStartValue();
                var end = item.GetEndValue();
                if (reverse) (start, end) = (end, start);
                var itemDuration = item.GetDuration(start, end);
                // Chain 等待全部前项；Group 跟随最近项起点；Insert 是单轮绝对时间。
                var position = item.AddType == AddType.Join ? lastStart
                    : item.AddType == AddType.Insert ? item.InsertTime : duration;
                if (position > float.MaxValue) throw new InvalidOperationException("时间轴起点溢出。");
                // 后端 Insert 接受 float；快照使用同样的精度。
                position = (float)position;
                var length = item.AnimationType == TweenType.Wait ? itemDuration : item.Delay + itemDuration * item.Loops;
                entries[order] = new TimelineEntry(index, item, position, itemDuration, length);
                lastStart = position;
                duration = Math.Max(duration, position + length);
                estimated |= item.AnimationType != TweenType.Wait && item.SpeedBased;
            }
            if (duration > float.MaxValue || double.IsInfinity(duration) || double.IsNaN(duration))
                throw new InvalidOperationException("时间轴总时长溢出。");
            return new TimelineSnapshot(entries, duration, m_Delay, m_Loops, m_LoopType, m_Ease, reverse, estimated);
        }

        private string GetValidationError(SequenceAnimation[] animations)
        {
            var prefix = $"TweenAnimation '{(m_Owner != null ? m_Owner.name : "未绑定")}': ";
            if (animations == null || animations.Length == 0) return prefix + "动画列表为空。";
            if (float.IsNaN(m_Delay) || float.IsInfinity(m_Delay) || m_Delay < 0f)
                return prefix + "Delay 必须为有限非负数。";
            if (m_Loops != -1 && m_Loops < 1) return prefix + "全局 Loops 必须为 -1（无限循环）或正整数。";
            if (!Enum.IsDefined(typeof(LoopType), m_LoopType)) return prefix + "LoopType 无效。";
            if (!Enum.IsDefined(typeof(UpdateType), m_UpdateType)) return prefix + "UpdateType 无效。";
            if (!Enum.IsDefined(typeof(TriggerMode), m_TriggerMode)) return prefix + "TriggerMode 无效。";
            if (!Enum.IsDefined(typeof(LitMotion.Ease), m_Ease) || m_Ease == LitMotion.Ease.CustomAnimationCurve)
                return prefix + "Ease 必须为有效的内置缓动类型。";

            for (var i = 0; i < animations.Length; i++)
            {
                var error = animations[i] == null ? "动画项为空。" : animations[i].GetValidationError();
                if (error != null) return $"{prefix}第 {i + 1} 项: {error}";
            }

            return null;
        }

        private void ValidateOrThrow() => ValidateOrThrow(m_Sequence);

        private void ValidateOrThrow(SequenceAnimation[] animations)
        {
            var error = GetValidationError(animations);
            if (error != null) throw new InvalidOperationException(error);
        }

        public void ResetToFromValue()
        {
            ValidateOrThrow();
            var values = new Vector4[m_Sequence.Length];
            for (var i = 0; i < m_Sequence.Length; i++)
            {
                var item = m_Sequence[i];
                if (item.UseFromValue || item.UseFromTarget) values[i] = item.CaptureStartValue();
            }
            for (var i = 0; i < m_Sequence.Length; i++)
            {
                var item = m_Sequence[i];
                if (item.UseFromValue || item.UseFromTarget) item.ApplyValue(values[i]);
            }
        }

        private MotionHandle PlaySequence(bool reverse)
        {
            if (Application.isPlaying && (m_Owner == null || !m_Owner.isActiveAndEnabled))
                throw new InvalidOperationException("请先 Initialize 启用的宿主组件，再播放动画。");
            var timeline = CaptureTimeline(reverse);
            DOKill();

            var sequence = LSequence.Create();
            m_ReversibleMotion = !reverse;

            foreach (var entry in timeline.Entries)
            {
                var handle = CreateItemMotion(m_Sequence[entry.Index], reverse);
                sequence = sequence.Insert((float)entry.Start, handle);
            }

            var builder = sequence.Run(configuration =>
            {
                configuration = Configure(configuration, m_Ease, m_Loops, m_LoopType, m_Delay, m_UpdateType, m_IgnoreTimeScale);
                if (m_ReversibleMotion) configuration = configuration.WithDelay(m_Delay, skipValuesDuringDelay: false);
                configuration.WithOnComplete(() =>
                {
                    if (Application.isPlaying && !m_SuppressCallbacks && !m_Backward)
                    {
                        m_OnComplete?.Invoke();
                    }
                });
            });

            m_Motion = Application.isPlaying ? builder.AddTo(m_Owner.gameObject) : builder;
            if (Application.isPlaying)
            {
                var linkedTargets = new System.Collections.Generic.HashSet<GameObject>();
                foreach (var item in m_Sequence)
                {
                    if (item?.Target != null && linkedTargets.Add(item.Target.gameObject))
                        m_Motion.AddTo(item.Target.gameObject, LitMotion.LinkBehavior.CancelOnDestroy);
                }
            }
            if (m_ReversibleMotion) m_Motion.Preserve();
            if (Application.isPlaying) m_OnPlay?.Invoke();
            return m_Motion;
        }

#if UNITY_EDITOR
        /// <summary>仅供编辑器隔离预览单项，不应用序列设置、不触发业务事件；调用方负责取消和恢复属性。</summary>
        public MotionHandle CreateItemPreview(SequenceAnimation item)
        {
            if (Application.isPlaying) throw new InvalidOperationException("单项预览仅限编辑模式。");
            var error = item?.GetValidationError() ?? (item == null ? "动画项为空。" : null);
            if (error != null) throw new InvalidOperationException(error);
            return CreateItemMotion(item, false);
        }
#endif

        private MotionHandle CreateItemMotion(SequenceAnimation item, bool reverse)
        {
            if (item.AnimationType == TweenType.Wait)
                return LMotion.Create(0f, 1f, item.DurationOrSpeed).WithEase(LitMotion.Ease.Linear).Bind(_ => { });
            var start = item.GetStartValue();
            var end = item.GetEndValue();
            if (reverse)
            {
                (start, end) = (end, start);
            }

            var duration = item.GetDuration(start, end);
            var target = item.Target;
            var firstUpdate = true;
            Action onUpdate = () =>
            {
                if (!Application.isPlaying || m_SuppressCallbacks) return;
                if (firstUpdate)
                {
                    firstUpdate = false;
                    if (!m_Backward) item.OnPlay?.Invoke();
                }

                item.OnUpdate?.Invoke();
                m_OnUpdate?.Invoke();
            };

            switch (item.AnimationType)
            {
                case TweenType.Move:
                    return Build<Vector3, Vector3MotionAdapter>((Vector3)start, (Vector3)end, duration, item, onUpdate)
                        .Bind(v => { target.transform.position = Snap(v, item.Snapping); onUpdate(); });
                case TweenType.LocalMove:
                    return Build<Vector3, Vector3MotionAdapter>((Vector3)start, (Vector3)end, duration, item, onUpdate)
                        .Bind(v => { target.transform.localPosition = Snap(v, item.Snapping); onUpdate(); });
                case TweenType.Scale:
                    return Build<Vector3, Vector3MotionAdapter>((Vector3)start, (Vector3)end, duration, item, onUpdate)
                        .Bind(v => { target.transform.localScale = v; onUpdate(); });
                case TweenType.Rotation:
                    return Build<Vector3, Vector3MotionAdapter>((Vector3)start, (Vector3)end, duration, item, onUpdate)
                        .Bind(v => { target.transform.eulerAngles = v; onUpdate(); });
                case TweenType.LocalRotation:
                    return Build<Vector3, Vector3MotionAdapter>((Vector3)start, (Vector3)end, duration, item, onUpdate)
                        .Bind(v => { target.transform.localEulerAngles = v; onUpdate(); });
                case TweenType.AnchorPosition:
                    return Build<Vector2, Vector2MotionAdapter>((Vector2)start, (Vector2)end, duration, item, onUpdate)
                        .Bind(v => { ((RectTransform)target).anchoredPosition = Snap(v, item.Snapping); onUpdate(); });
                case TweenType.AnchorPosition3D:
                    return Build<Vector3, Vector3MotionAdapter>((Vector3)start, (Vector3)end, duration, item, onUpdate)
                        .Bind(v => { ((RectTransform)target).anchoredPosition3D = Snap(v, item.Snapping); onUpdate(); });
                case TweenType.SizeDelta:
                    return Build<Vector2, Vector2MotionAdapter>((Vector2)start, (Vector2)end, duration, item, onUpdate)
                        .Bind(v => { ((RectTransform)target).sizeDelta = Snap(v, item.Snapping); onUpdate(); });
                case TweenType.Color:
                    return Build<Color, ColorMotionAdapter>((Color)start, (Color)end, duration, item, onUpdate)
                        .Bind(v => { ((Graphic)target).color = v; onUpdate(); });
                case TweenType.Fade:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { var g = (Graphic)target; var c = g.color; c.a = v; g.color = c; onUpdate(); });
                case TweenType.CanvasGroupFade:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { ((CanvasGroup)target).alpha = v; onUpdate(); });
                case TweenType.FillAmount:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { ((Image)target).fillAmount = v; onUpdate(); });
                case TweenType.SliderValue:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { SetSliderValue((Slider)target, v); onUpdate(); });
                case TweenType.AnchorPositionX:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { var t = (RectTransform)target; var p = t.anchoredPosition; p.x = Snap(v, item.Snapping); t.anchoredPosition = p; onUpdate(); });
                case TweenType.AnchorPositionY:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { var t = (RectTransform)target; var p = t.anchoredPosition; p.y = Snap(v, item.Snapping); t.anchoredPosition = p; onUpdate(); });
                case TweenType.AnchorPositionZ:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { var t = (RectTransform)target; var p = t.anchoredPosition3D; p.z = Snap(v, item.Snapping); t.anchoredPosition3D = p; onUpdate(); });
                case TweenType.MoveX:
                case TweenType.MoveY:
                case TweenType.MoveZ:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { SetPositionAxis(target.transform, Snap(v, item.Snapping), item.AnimationType); onUpdate(); });
                case TweenType.LocalMoveX:
                case TweenType.LocalMoveY:
                case TweenType.LocalMoveZ:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { SetLocalAxis(target.transform, Snap(v, item.Snapping), item.AnimationType); onUpdate(); });
                case TweenType.ScaleX:
                case TweenType.ScaleY:
                case TweenType.ScaleZ:
                    return Build<float, FloatMotionAdapter>((float)start, (float)end, duration, item, onUpdate)
                        .Bind(v => { SetScaleAxis(target.transform, v, item.AnimationType); onUpdate(); });
                case TweenType.LayoutFlexibleSize:
                    return Build<Vector2, Vector2MotionAdapter>((Vector2)start, (Vector2)end, duration, item, onUpdate)
                        .Bind(v => { var t = (LayoutElement)target; t.flexibleWidth = v.x; t.flexibleHeight = v.y; onUpdate(); });
                case TweenType.LayoutMinSize:
                    return Build<Vector2, Vector2MotionAdapter>((Vector2)start, (Vector2)end, duration, item, onUpdate)
                        .Bind(v => { var t = (LayoutElement)target; t.minWidth = v.x; t.minHeight = v.y; onUpdate(); });
                case TweenType.LayoutPreferredSize:
                    return Build<Vector2, Vector2MotionAdapter>((Vector2)start, (Vector2)end, duration, item, onUpdate)
                        .Bind(v => { var t = (LayoutElement)target; t.preferredWidth = v.x; t.preferredHeight = v.y; onUpdate(); });
                default:
                    throw new InvalidOperationException($"TweenAnimation '{(m_Owner != null ? m_Owner.name : "未绑定")}': 不支持的 AnimationType {item.AnimationType}。");
            }
        }

        private MotionBuilder<T, NoOptions, TAdapter> Build<T, TAdapter>(
            T start, T end, float duration, SequenceAnimation item, Action onUpdate)
            where T : unmanaged
            where TAdapter : unmanaged, IMotionAdapter<T, NoOptions>
        {
            var builder = LMotion.Create<T, NoOptions, TAdapter>(start, end, duration)
                .WithDelay(item.Delay, skipValuesDuringDelay: !m_ReversibleMotion)
                .WithLoops(item.Loops, ToLitMotionLoop(item.LoopType))
                .WithOnComplete(() => { if (Application.isPlaying && !m_SuppressCallbacks && !m_Backward) item.OnComplete?.Invoke(); })
                .WithCancelOnError(true);

            builder = item.CustomEase
                ? builder.WithEase(item.EaseCurve)
                : builder.WithEase(ToLitMotionEase(item.Ease));
            if (Application.isPlaying)
            {
                builder = builder.WithScheduler(ToScheduler(item.UpdateType, item.IgnoreTimeScale));
            }
            return builder;
        }

        private static MotionBuilder<double, NoOptions, DoubleMotionAdapter> Configure(
            MotionBuilder<double, NoOptions, DoubleMotionAdapter> builder,
            LitMotion.Ease ease, int loops, LoopType loopType, float delay, UpdateType updateType, bool ignoreTimeScale)
        {
            builder = builder.WithEase(ToLitMotionEase(ease))
                .WithLoops(loops, ToLitMotionLoop(loopType))
                .WithDelay(delay);
            if (Application.isPlaying)
            {
                builder = builder.WithScheduler(ToScheduler(updateType, ignoreTimeScale));
            }
            return builder;
        }

        private static LitMotion.Ease ToLitMotionEase(LitMotion.Ease ease)
        {
            return ease;
        }

        internal static void SetSliderValue(Slider target, float value)
        {
            if (Application.isPlaying) target.value = value;
            else target.SetValueWithoutNotify(value);
        }

        private static LitMotion.LoopType ToLitMotionLoop(LoopType loopType)
        {
            return loopType switch
            {
                LoopType.Yoyo => LitMotion.LoopType.Flip,
                LoopType.Incremental => LitMotion.LoopType.Incremental,
                _ => LitMotion.LoopType.Restart
            };
        }

        private static IMotionScheduler ToScheduler(UpdateType updateType, bool ignoreTimeScale)
        {
            return updateType == UpdateType.Fixed
                ? MotionScheduler.FixedUpdate
                : updateType == UpdateType.Late
                    ? (ignoreTimeScale ? MotionScheduler.PreLateUpdateIgnoreTimeScale : MotionScheduler.PreLateUpdate)
                    : (ignoreTimeScale ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update);
        }

        internal static void SetAxis(ref Vector3 value, float component, TweenType type)
        {
            if (type == TweenType.MoveX) value.x = component;
            else if (type == TweenType.MoveY) value.y = component;
            else value.z = component;
        }

        private static void SetPositionAxis(Transform target, float value, TweenType type)
        {
            var position = target.position;
            SetAxis(ref position, value, type);
            target.position = position;
        }

        internal static void SetLocalAxis(Transform target, float value, TweenType type)
        {
            var position = target.localPosition;
            SetAxis(ref position, value, type == TweenType.LocalMoveX ? TweenType.MoveX :
                type == TweenType.LocalMoveY ? TweenType.MoveY : TweenType.MoveZ);
            target.localPosition = position;
        }

        internal static void SetScaleAxis(Transform target, float value, TweenType type)
        {
            var scale = target.localScale;
            SetAxis(ref scale, value, type == TweenType.ScaleX ? TweenType.MoveX :
                type == TweenType.ScaleY ? TweenType.MoveY : TweenType.MoveZ);
            target.localScale = scale;
        }

        private static float Snap(float value, bool snapping)
        {
            return snapping ? Mathf.Round(value) : value;
        }

        private static Vector2 Snap(Vector2 value, bool snapping)
        {
            return snapping ? new Vector2(Mathf.Round(value.x), Mathf.Round(value.y)) : value;
        }

        private static Vector3 Snap(Vector3 value, bool snapping)
        {
            return snapping ? new Vector3(Mathf.Round(value.x), Mathf.Round(value.y), Mathf.Round(value.z)) : value;
        }

    }
}
