using System.Collections.Generic;
using TEngine.Extensions.Tween;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Type = TEngine.Extensions.Tween.TweenAnimation.TweenType;

namespace TEngine.Extensions.Editor.Tween
{
    internal static class TweenAnimationDiagnostics
    {
        internal static List<string> Collect(SerializedProperty animation)
        {
            var list = animation.FindPropertyRelative("m_Sequence");
            var items = new TweenAnimation.SequenceAnimation[list.arraySize];
            for (var i = 0; i < items.Length; i++)
                items[i] = TweenAnimationItemDrawer.ReadAnimation(list.GetArrayElementAtIndex(i));
            return CollectItems(items, (LitMotion.Ease)animation.FindPropertyRelative("m_Ease").intValue);
        }

        internal static List<string> CollectItems(TweenAnimation.SequenceAnimation[] items, LitMotion.Ease ease)
        {
            var warnings = new List<string>();
            if (ease != LitMotion.Ease.Linear)
                warnings.Add("全局缓动不是 Linear，会再次改变时间线推进速度；单项预览不含此设置，错峰的实际间隔也会改变。通常让单项负责缓动。\n仅提示，不修改已有配置。");
            var sequence = new TweenAnimation();
            // 配置不合法时仍可显示布局提示，但不能猜测其编排时间。
            TweenAnimation.TimelineSnapshot timeline = null;
            try { if (items.Length > 0) { sequence.SetAnimations(items); timeline = sequence.CaptureTimeline(); } }
            catch (System.InvalidOperationException) { }
            for (var i = 0; i < items.Length; i++)
            {
                if (warnings.Count >= 12)
                {
                    warnings.Add("其余提示已折叠；先处理以上配置，再查看剩余问题。");
                    break;
                }
                var item = items[i];
                if (item.AnimationType == Type.Wait || item.Target == null) continue;
                var layout = LayoutWarning(item);
                if (layout != null) warnings.Add($"第 {i + 1} 项：{layout}");
                for (var j = 0; j < i; j++)
                {
                    if (warnings.Count >= 12) break;
                    var previous = items[j];
                    if (!SharesProperty(previous, item) || timeline == null) continue;
                    var a = timeline.Entries[j];
                    var b = timeline.Entries[i];
                    if (a.ActiveStart < b.End && b.ActiveStart < a.End)
                        warnings.Add($"第 {j + 1} / {i + 1} 项：同一属性的动画时间重叠，可能互相覆盖；不提供叠加混合。世界/局部坐标也可能互相影响。");
                    else if (!item.UseFromValue && !item.UseFromTarget && a.End <= b.ActiveStart)
                        warnings.Add($"第 {i + 1} 项：起始值来源“当前值”在构建整段序列时采样，不会自动接续第 {j + 1} 项结束值。连续动作建议指定起始值。引用对象也不会持续追踪。");
                }
            }
            return warnings;
        }

        private static string LayoutWarning(TweenAnimation.SequenceAnimation item)
        {
            if (!(item.Target is RectTransform rect)) return null;
            var position = (Channels(item.AnimationType) & 7) != 0;
            var size = item.AnimationType == Type.SizeDelta;
            var element = rect.GetComponent<LayoutElement>();
            var group = rect.parent != null ? rect.parent.GetComponent<LayoutGroup>() : null;
            if ((position || size) && group != null && group.isActiveAndEnabled && (element == null || !element.ignoreLayout))
                return "父级 LayoutGroup 可能控制此位置或尺寸。建议布局作用于外层，Tween 作用于内层视觉节点；不要自动关闭布局。";
            var fitter = rect.GetComponent<ContentSizeFitter>();
            if (size && fitter != null && fitter.isActiveAndEnabled
                && (fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained || fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained))
                return "ContentSizeFitter 正在控制尺寸，可能覆盖 SizeDelta 动画。建议使用独立视觉子节点。";
            var aspect = rect.GetComponent<AspectRatioFitter>();
            if ((size || position) && aspect != null && aspect.isActiveAndEnabled && aspect.aspectMode != AspectRatioFitter.AspectMode.None)
                return "AspectRatioFitter 可能控制此位置或尺寸；建议使用独立视觉子节点。";
            return null;
        }

        internal static bool SharesProperty(TweenAnimation.SequenceAnimation a, TweenAnimation.SequenceAnimation b)
        {
            if (a.Target == null || b.Target == null) return false;
            var ca = Channels(a.AnimationType);
            var cb = Channels(b.AnimationType);
            if ((ca & cb) == 0) return false;
            // Transform 三种位置坐标和旋转共用底层属性；Graphic 等按具体组件判定。
            return ca < 1024 && cb < 1024 ? a.Target.transform == b.Target.transform : a.Target == b.Target;
        }

        private static int Channels(Type type) => type switch
        {
            Type.Move or Type.LocalMove or Type.AnchorPosition3D => 7,
            Type.AnchorPosition => 3,
            Type.MoveX or Type.LocalMoveX or Type.AnchorPositionX => 1,
            Type.MoveY or Type.LocalMoveY or Type.AnchorPositionY => 2,
            Type.MoveZ or Type.LocalMoveZ or Type.AnchorPositionZ => 4,
            Type.Scale => 56,
            Type.ScaleX => 8, Type.ScaleY => 16, Type.ScaleZ => 32,
            Type.Rotation or Type.LocalRotation => 64,
            Type.SizeDelta => 128,
            Type.Color => 1024 | 2048, Type.Fade => 2048,
            Type.CanvasGroupFade => 4096, Type.FillAmount => 8192,
            Type.SliderValue => 16384, Type.LayoutFlexibleSize => 32768,
            Type.LayoutMinSize => 65536, Type.LayoutPreferredSize => 131072,
            _ => 0
        };
    }
}
