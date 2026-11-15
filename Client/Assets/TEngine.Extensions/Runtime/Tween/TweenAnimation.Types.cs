using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TEngine.Extensions.Tween
{
    public sealed partial class TweenAnimation
    {
        public enum TweenType
        {
            Move, MoveX, MoveY, MoveZ, LocalMove, LocalMoveX, LocalMoveY, LocalMoveZ,
            Scale, ScaleX, ScaleY, ScaleZ, Rotation, LocalRotation,
            AnchorPosition, AnchorPositionX, AnchorPositionY, AnchorPositionZ, AnchorPosition3D,
            Color, Fade, CanvasGroupFade, FillAmount, LayoutFlexibleSize, LayoutMinSize,
            LayoutPreferredSize, SizeDelta, SliderValue, Wait
        }

        public enum AddType { Append, Join, Insert }
        public enum LoopType { Restart, Yoyo, Incremental }
        public enum UpdateType { Normal, Late, Fixed }

        [Serializable]
        public sealed class SequenceAnimation
        {
            public string Name;
            public AddType AddType;
            [Min(0f)] public float InsertTime;
            public TweenType AnimationType;
            public Component Target;
            public Vector4 ToValue;
            public bool Relative;
            public bool UseToTarget;
            public Component ToTarget;
            public bool UseFromValue;
            public Vector4 FromValue;
            public bool UseFromTarget;
            public Component FromTarget;
            public bool SpeedBased;
            [Min(0f)] public float DurationOrSpeed = 1f;
            [Min(0f)] public float Delay;
            public UpdateType UpdateType;
            public bool IgnoreTimeScale = true;
            public bool CustomEase;
            public bool Snapping;
            public AnimationCurve EaseCurve = AnimationCurve.Linear(0, 0, 1, 1);
            public LitMotion.Ease Ease = LitMotion.Ease.OutQuad;
            [Min(1)] public int Loops = 1;
            public LoopType LoopType;
            public UnityEvent OnPlay = new UnityEvent();
            public UnityEvent OnUpdate = new UnityEvent();
            public UnityEvent OnComplete = new UnityEvent();

            public static Type GetRequiredComponentType(TweenType animationType)
            {
                return animationType switch
                {
                    TweenType.Move or TweenType.MoveX or TweenType.MoveY or TweenType.MoveZ
                    or TweenType.LocalMove or TweenType.LocalMoveX or TweenType.LocalMoveY or TweenType.LocalMoveZ
                    or TweenType.Scale or TweenType.ScaleX or TweenType.ScaleY or TweenType.ScaleZ
                    or TweenType.Rotation or TweenType.LocalRotation => typeof(Transform),
                    TweenType.AnchorPosition or TweenType.AnchorPositionX or TweenType.AnchorPositionY
                    or TweenType.AnchorPositionZ or TweenType.AnchorPosition3D or TweenType.SizeDelta => typeof(RectTransform),
                    TweenType.Color or TweenType.Fade => typeof(Graphic),
                    TweenType.CanvasGroupFade => typeof(CanvasGroup),
                    TweenType.FillAmount => typeof(Image),
                    TweenType.LayoutFlexibleSize or TweenType.LayoutMinSize or TweenType.LayoutPreferredSize => typeof(LayoutElement),
                    TweenType.SliderValue => typeof(Slider),
                    _ => throw new ArgumentOutOfRangeException(nameof(animationType), animationType, "Unsupported TweenType.")
                };
            }

            private string GetTargetValidationError(Component component, string fieldName)
            {
                if (!Enum.IsDefined(typeof(TweenType), AnimationType))
                    return $"AnimationType 值 {AnimationType} 无效。";
                var required = GetRequiredComponentType(AnimationType);
                if (component == null)
                    return $"{fieldName} 缺失，{AnimationType} 必须引用 {required.Name}。";
                return required.IsInstanceOfType(component) ? null
                    : $"{fieldName} '{component.name}' 类型为 {component.GetType().Name}，{AnimationType} 必须引用 {required.Name}。";
            }

            public string GetValidationError()
            {
                if (AnimationType == TweenType.Wait)
                {
                    if (!Enum.IsDefined(typeof(AddType), AddType)) return "AddType 无效。";
                    if (AddType == AddType.Insert && (!IsFinite(InsertTime) || InsertTime < 0)) return "InsertTime 必须为有限非负数。";
                    if (!IsFinite(DurationOrSpeed) || DurationOrSpeed < 0) return "等待时长必须为有限非负数。";
                    return null;
                }
                var error = GetTargetValidationError(Target, nameof(Target));
                if (error != null) return error;
                if (UseFromTarget)
                {
                    error = GetTargetValidationError(FromTarget, nameof(FromTarget));
                    if (error != null) return error;
                }
                if (UseToTarget)
                {
                    error = GetTargetValidationError(ToTarget, nameof(ToTarget));
                    if (error != null) return error;
                }
                if (!Enum.IsDefined(typeof(AddType), AddType)) return "AddType 无效。";
                if (AddType == AddType.Insert && (!IsFinite(InsertTime) || InsertTime < 0f)) return "InsertTime 必须为有限非负数。";
                if (!Enum.IsDefined(typeof(UpdateType), UpdateType)) return "UpdateType 无效。";
                if (!Enum.IsDefined(typeof(LoopType), LoopType)) return "LoopType 无效。";
                if (!IsFinite(Delay) || Delay < 0f) return "Delay 必须为有限非负数。";
                if (!IsFinite(DurationOrSpeed) || (SpeedBased ? DurationOrSpeed <= 0f : DurationOrSpeed < 0f))
                    return SpeedBased ? "DurationOrSpeed（速度）必须为有限正数。" : "DurationOrSpeed（时长）必须为有限非负数。";
                if (Loops < 1) return "Loops 必须 >= 1。";
                if (CustomEase)
                {
                    if (EaseCurve == null || EaseCurve.length == 0) return "CustomEase 需要非空 EaseCurve。";
                    if (!Enum.IsDefined(typeof(WrapMode), EaseCurve.preWrapMode) || !Enum.IsDefined(typeof(WrapMode), EaseCurve.postWrapMode))
                        return "EaseCurve 的 WrapMode 无效。";
                    foreach (var key in EaseCurve.keys)
                    {
                        // Unity 使用无限切线表示阶跃插值。
                        if (!IsFinite(key.time) || !IsFinite(key.value)
                            || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent)
                            || !IsFinite(key.inWeight) || !IsFinite(key.outWeight))
                            return "EaseCurve 关键帧含无效数值。";
                    }
                }
                else if (!Enum.IsDefined(typeof(LitMotion.Ease), Ease) || Ease == LitMotion.Ease.CustomAnimationCurve)
                    return "Ease 无效；自定义曲线请启用 CustomEase 并设置 EaseCurve。";

                var start = GetStartValue();
                var end = GetEndValue();
                if (!IsFiniteValue(ToVector4(start))) return "起始属性值必须为有限数值。";
                if (!IsFiniteValue(ToVector4(end))) return "结束属性值必须为有限数值。";
                var duration = GetDuration(start, end);
                if (!IsFinite(duration) || duration < 0f) return "计算后的动画时长必须为有限非负数。";
                if (!IsFinite((duration + Delay) * Loops)) return "动画总时长溢出。";
                return null;
            }

            private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

            private static Vector4 ToVector4(object value)
            {
                if (value is Vector3 vector3) return vector3;
                if (value is Vector2 vector2) return new Vector4(vector2.x, vector2.y, 0f, 0f);
                if (value is Color color) return color;
                return new Vector4(Convert.ToSingle(value), 0f, 0f, 0f);
            }

            private bool IsFiniteValue(Vector4 value)
            {
                var resolved = ToVector4(ValueForType(value));
                return IsFinite(resolved.x) && IsFinite(resolved.y) && IsFinite(resolved.z) && IsFinite(resolved.w);
            }

            /// <summary>读取当前动画属性，标量存入 x 分量。</summary>
            public Vector4 CaptureCurrentValue()
            {
                return ToVector4(ReadComponentValue(Target));
            }

            internal object GetStartValue()
            {
                if (AnimationType == TweenType.Wait) return 0f;
                if (UseFromTarget) return ReadReferenceValue(FromTarget, nameof(FromTarget));
                return UseFromValue ? ValueForType(FromValue) : ReadComponentValue(Target);
            }

            internal object GetEndValue()
            {
                if (AnimationType == TweenType.Wait) return 1f;
                return UseToTarget
                    ? ReadReferenceValue(ToTarget, nameof(ToTarget))
                    : ValueForType(UsesRelativeEnd ? ToVector4(GetStartValue()) + ToValue : ToValue);
            }

            /// <summary>数值结束值相对于解析后的起始值作加法偏移；引用结束值始终为绝对端点。</summary>
            public bool UsesRelativeEnd => Relative && !UseToTarget && SupportsRelative(AnimationType);

            public static bool SupportsRelative(TweenType type) => type switch
            {
                TweenType.Move or TweenType.MoveX or TweenType.MoveY or TweenType.MoveZ
                or TweenType.LocalMove or TweenType.LocalMoveX or TweenType.LocalMoveY or TweenType.LocalMoveZ
                or TweenType.Scale or TweenType.ScaleX or TweenType.ScaleY or TweenType.ScaleZ
                or TweenType.Rotation or TweenType.LocalRotation
                or TweenType.AnchorPosition or TweenType.AnchorPositionX or TweenType.AnchorPositionY
                or TweenType.AnchorPositionZ or TweenType.AnchorPosition3D or TweenType.SizeDelta
                or TweenType.LayoutFlexibleSize or TweenType.LayoutMinSize or TweenType.LayoutPreferredSize => true,
                _ => false
            };

            internal Vector4 CaptureStartValue() => ToVector4(GetStartValue());

            /// <summary>读取指定端点，引用位置转换到动画属性坐标系；不修改目标。</summary>
            public Vector4 CaptureEndpointValue(bool to) => ToVector4(to ? GetEndValue() : GetStartValue());

            private object ReadReferenceValue(Component component, string fieldName)
            {
                var error = GetTargetValidationError(component, fieldName);
                if (error != null) throw new InvalidOperationException(error);

                switch (AnimationType)
                {
                    case TweenType.LocalMove:
                    case TweenType.LocalMoveX:
                    case TweenType.LocalMoveY:
                    case TweenType.LocalMoveZ:
                    case TweenType.AnchorPosition:
                    case TweenType.AnchorPositionX:
                    case TweenType.AnchorPositionY:
                    case TweenType.AnchorPositionZ:
                    case TweenType.AnchorPosition3D:
                        var parent = Target.transform.parent;
                        var position = parent == null ? component.transform.position
                            : parent.InverseTransformPoint(component.transform.position);
                        if (Target is RectTransform rect && AnimationType != TweenType.LocalMove
                            && AnimationType != TweenType.LocalMoveX && AnimationType != TweenType.LocalMoveY
                            && AnimationType != TweenType.LocalMoveZ && parent is RectTransform parentRect)
                        {
                            // anchoredPosition 的原点由被动画对象的锚点及 pivot 决定。
                            var anchor = rect.anchorMin + Vector2.Scale(rect.anchorMax - rect.anchorMin, rect.pivot);
                            var offset = parentRect.rect.min + Vector2.Scale(parentRect.rect.size, anchor);
                            position -= (Vector3)offset;
                        }
                        return AnimationType switch
                        {
                            TweenType.LocalMove or TweenType.AnchorPosition3D => position,
                            TweenType.AnchorPosition => (Vector2)position,
                            TweenType.LocalMoveX or TweenType.AnchorPositionX => position.x,
                            TweenType.LocalMoveY or TweenType.AnchorPositionY => position.y,
                            _ => position.z
                        };
                    default:
                        return ReadComponentValue(component);
                }
            }

            internal float GetDuration(object start, object end)
            {
                var duration = DurationOrSpeed;
                if (AnimationType == TweenType.Wait) return duration;
                if (!SpeedBased)
                {
                    return duration;
                }

                var distance = AnimationType switch
                {
                    TweenType.Move or TweenType.LocalMove or TweenType.AnchorPosition3D
                    or TweenType.Scale or TweenType.Rotation or TweenType.LocalRotation =>
                        Vector3.Distance((Vector3)start, (Vector3)end),
                    TweenType.AnchorPosition or TweenType.SizeDelta or TweenType.LayoutFlexibleSize
                    or TweenType.LayoutMinSize or TweenType.LayoutPreferredSize =>
                        Vector2.Distance((Vector2)start, (Vector2)end),
                    TweenType.Color => Vector4.Distance((Color)start, (Color)end),
                    _ => Mathf.Abs(Convert.ToSingle(end) - Convert.ToSingle(start))
                };

                return distance / duration;
            }

            public void ApplyValue(Vector4 value)
            {
                if (AnimationType == TweenType.Wait) return;
                var error = GetTargetValidationError(Target, nameof(Target));
                if (error != null) throw new InvalidOperationException(error);
                if (!IsFiniteValue(value)) throw new InvalidOperationException("应用的动画属性值必须为有限数值。");

                var resolved = ValueForType(value);
                switch (AnimationType)
                {
                    case TweenType.Move: Target.transform.position = (Vector3)resolved; break;
                    case TweenType.LocalMove: Target.transform.localPosition = (Vector3)resolved; break;
                    case TweenType.Scale: Target.transform.localScale = (Vector3)resolved; break;
                    case TweenType.Rotation: Target.transform.eulerAngles = (Vector3)resolved; break;
                    case TweenType.LocalRotation: Target.transform.localEulerAngles = (Vector3)resolved; break;
                    case TweenType.AnchorPosition: ((RectTransform)Target).anchoredPosition = (Vector2)resolved; break;
                    case TweenType.AnchorPosition3D: ((RectTransform)Target).anchoredPosition3D = (Vector3)resolved; break;
                    case TweenType.SizeDelta: ((RectTransform)Target).sizeDelta = (Vector2)resolved; break;
                    case TweenType.Color: ((Graphic)Target).color = (Color)resolved; break;
                    case TweenType.Fade:
                        var graphic = (Graphic)Target;
                        var color = graphic.color;
                        color.a = Convert.ToSingle(resolved);
                        graphic.color = color;
                        break;
                    case TweenType.CanvasGroupFade: ((CanvasGroup)Target).alpha = Convert.ToSingle(resolved); break;
                    case TweenType.FillAmount: ((Image)Target).fillAmount = Convert.ToSingle(resolved); break;
                    case TweenType.SliderValue: SetSliderValue((Slider)Target, Convert.ToSingle(resolved)); break;
                    case TweenType.LayoutFlexibleSize:
                        var flexible = (LayoutElement)Target;
                        flexible.flexibleWidth = value.x;
                        flexible.flexibleHeight = value.y;
                        break;
                    case TweenType.LayoutMinSize:
                        var minimum = (LayoutElement)Target;
                        minimum.minWidth = value.x;
                        minimum.minHeight = value.y;
                        break;
                    case TweenType.LayoutPreferredSize:
                        var preferred = (LayoutElement)Target;
                        preferred.preferredWidth = value.x;
                        preferred.preferredHeight = value.y;
                        break;
                    case TweenType.AnchorPositionX:
                    case TweenType.AnchorPositionY:
                        var position = ((RectTransform)Target).anchoredPosition;
                        if (AnimationType == TweenType.AnchorPositionX)
                        {
                            position.x = Convert.ToSingle(resolved);
                        }
                        else
                        {
                            position.y = Convert.ToSingle(resolved);
                        }
                        ((RectTransform)Target).anchoredPosition = position;
                        break;
                    case TweenType.AnchorPositionZ:
                        var position3D = ((RectTransform)Target).anchoredPosition3D;
                        position3D.z = Convert.ToSingle(resolved);
                        ((RectTransform)Target).anchoredPosition3D = position3D;
                        break;
                    case TweenType.MoveX:
                    case TweenType.MoveY:
                    case TweenType.MoveZ:
                        var worldPosition = Target.transform.position;
                        SetAxis(ref worldPosition, Convert.ToSingle(resolved), AnimationType);
                        Target.transform.position = worldPosition;
                        break;
                    case TweenType.LocalMoveX:
                    case TweenType.LocalMoveY:
                    case TweenType.LocalMoveZ:
                        SetLocalAxis(Target.transform, Convert.ToSingle(resolved), AnimationType);
                        break;
                    case TweenType.ScaleX:
                    case TweenType.ScaleY:
                    case TweenType.ScaleZ:
                        SetScaleAxis(Target.transform, Convert.ToSingle(resolved), AnimationType);
                        break;
                }
            }

            private object ReadComponentValue(Component component)
            {
                var error = GetTargetValidationError(component, component == Target ? nameof(Target) : nameof(ToTarget));
                if (error != null) throw new InvalidOperationException(error);

                return AnimationType switch
                {
                    TweenType.Move => component.transform.position,
                    TweenType.MoveX => component.transform.position.x,
                    TweenType.MoveY => component.transform.position.y,
                    TweenType.MoveZ => component.transform.position.z,
                    TweenType.LocalMove => component.transform.localPosition,
                    TweenType.LocalMoveX => component.transform.localPosition.x,
                    TweenType.LocalMoveY => component.transform.localPosition.y,
                    TweenType.LocalMoveZ => component.transform.localPosition.z,
                    TweenType.Scale => component.transform.localScale,
                    TweenType.ScaleX => component.transform.localScale.x,
                    TweenType.ScaleY => component.transform.localScale.y,
                    TweenType.ScaleZ => component.transform.localScale.z,
                    TweenType.Rotation => component.transform.eulerAngles,
                    TweenType.LocalRotation => component.transform.localEulerAngles,
                    TweenType.AnchorPosition => ((RectTransform)component).anchoredPosition,
                    TweenType.AnchorPositionX => ((RectTransform)component).anchoredPosition.x,
                    TweenType.AnchorPositionY => ((RectTransform)component).anchoredPosition.y,
                    TweenType.AnchorPositionZ => ((RectTransform)component).anchoredPosition3D.z,
                    TweenType.AnchorPosition3D => ((RectTransform)component).anchoredPosition3D,
                    TweenType.SizeDelta => ((RectTransform)component).sizeDelta,
                    TweenType.Color => ((Graphic)component).color,
                    TweenType.Fade => ((Graphic)component).color.a,
                    TweenType.CanvasGroupFade => ((CanvasGroup)component).alpha,
                    TweenType.FillAmount => ((Image)component).fillAmount,
                    TweenType.SliderValue => ((Slider)component).value,
                    TweenType.LayoutFlexibleSize => new Vector2(((LayoutElement)component).flexibleWidth, ((LayoutElement)component).flexibleHeight),
                    TweenType.LayoutMinSize => new Vector2(((LayoutElement)component).minWidth, ((LayoutElement)component).minHeight),
                    TweenType.LayoutPreferredSize => new Vector2(((LayoutElement)component).preferredWidth, ((LayoutElement)component).preferredHeight),
                    _ => throw new InvalidOperationException($"不支持的 AnimationType: {AnimationType}。")
                };
            }

            private object ValueForType(Vector4 value)
            {
                return AnimationType switch
                {
                    TweenType.Move or TweenType.LocalMove or TweenType.Scale or TweenType.Rotation
                    or TweenType.LocalRotation or TweenType.AnchorPosition3D => (Vector3)value,
                    TweenType.AnchorPosition or TweenType.SizeDelta or TweenType.LayoutFlexibleSize
                    or TweenType.LayoutMinSize or TweenType.LayoutPreferredSize => (Vector2)value,
                    TweenType.Color => (Color)value,
                    _ => value.x
                };
            }
        }
    }
}
