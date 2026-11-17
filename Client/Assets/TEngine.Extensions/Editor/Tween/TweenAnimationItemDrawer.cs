using TEngine.Extensions.Tween;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.Editor.Tween
{
    [CustomPropertyDrawer(typeof(TweenAnimation.SequenceAnimation))]
    internal sealed class TweenAnimationItemDrawer : PropertyDrawer
    {
        private const float Spacing = 3f;
        private const float ErrorHeight = 58f;
        private readonly TweenAnimation.SequenceAnimation m_Animation = new TweenAnimation.SequenceAnimation();
        private static float RowHeight => EditorGUIUtility.singleLineHeight + Spacing;
        internal const float TimelineHeight = 8f;

        internal static Rect GetTimelineRect(Rect position) => new Rect(position.x,
            position.y + RowHeight + 1f, Mathf.Max(0, position.width), 3f);

        private static readonly TweenAnimation.TweenType[] AnimationTypes =
            (TweenAnimation.TweenType[])System.Enum.GetValues(typeof(TweenAnimation.TweenType));
        private static readonly string[] FromModes = { "当前值", "指定数值", "引用对象" };
        private static readonly string[] ToModes = { "指定数值", "引用对象" };
        internal static GUIContent ValueLabel(bool to) => new GUIContent(to ? "结束值" : "起始值");
        internal static GUIContent SourceLabel(bool to) => new GUIContent(to ? "结束值来源" : "起始值来源");
        internal static GUIContent ReferenceLabel(bool to) => new GUIContent(to ? "结束值引用" : "起始值引用");
        private static readonly string[] Operations = { "接续 Chain", "并行 Group", "指定时间 Insert" };
        private static readonly LitMotion.Ease[] EaseValues = (LitMotion.Ease[])System.Enum.GetValues(typeof(LitMotion.Ease));
        internal static readonly string[] EaseCategories = { "常用", "全部", "匀速 (Linear)", "正弦 (Sine)", "二次 (Quad)", "三次 (Cubic)", "四次 (Quart)", "五次 (Quint)", "指数 (Expo)", "圆形 (Circ)", "弹性 (Elastic)", "回弹 (Back)", "弹跳 (Bounce)", "自定义 (CustomAnimationCurve)" };
        private static readonly System.Collections.Generic.Dictionary<LitMotion.Ease, Vector3[]> EaseSamples = new();
        private static readonly string[] ParameterNames = { "FromValue", "ToValue", "Relative", "DurationOrSpeed", "SpeedBased", "Delay", "Loops", "LoopType", "CustomEase", "Ease", "EaseCurve", "Snapping" };
        private static readonly System.Collections.Generic.Dictionary<string, object> CopiedParameters = new();
        private static int m_CopiedType;
        private static GUIStyle s_HeaderStyle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return RowHeight + TimelineHeight;
            var animation = ReadAnimation(property, m_Animation);
            if (!SupportsAnimationFields(animation.AnimationType))
                return RowHeight * (5 + (ShowInsert(property) ? 1 : 0)) + TimelineHeight
                    + (animation.GetValidationError() == null ? 0 : ErrorHeight + Spacing);
            var height = RowHeight * (10 + EndpointRows(property, false) + EndpointRows(property, true));
            if (GetTextError(animation) != null) height += ErrorHeight + Spacing;
            if (ShowCurve(property)) height += RowHeight;
            if (ShowInsert(property)) height += RowHeight;
            if (property.FindPropertyRelative("SpeedBased").isExpanded)
                height += RowHeight * (4 + (SupportsSnapping(animation.AnimationType) ? 1 : 0) + (ShowRelative(property) ? 1 : 0));
            if (property.FindPropertyRelative("CustomEase").isExpanded)
                foreach (var name in new[] { "OnPlay", "OnUpdate", "OnComplete" })
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name), true) + Spacing;
            return height + TimelineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            var oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = Mathf.Min(100f, position.width * .36f);
            try
            {
                var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                DrawItemMenu(rect, property);
                var animation = ReadAnimation(property, m_Animation);
                var error = animation.GetValidationError();
                var textError = GetTextError(animation);
                var referenceError = GetReferenceErrorWithSnapshot(property, "Target", animation)
                    ?? GetReferenceErrorWithSnapshot(property, "FromTarget", animation) ?? GetReferenceErrorWithSnapshot(property, "ToTarget", animation);
                var targetName = animation.AnimationType == TweenAnimation.TweenType.Wait ? "无目标" : animation.Target == null ? "缺少目标" : animation.Target.name;
                var targetSummary = FormatTargetSummary(property.hasMultipleDifferentValues ? "多个目标" : targetName, referenceError != null);
                var duration = animation.SpeedBased ? $"速度 {animation.DurationOrSpeed:g}" : $"{animation.DurationOrSpeed:g}秒";
                var summary = property.hasMultipleDifferentValues ? $"{label.text} / 多个不同配置 / {targetSummary}"
                    : $"{EscapeRichText(GetItemTitle(animation.Name, label.text))} / {(System.Enum.IsDefined(typeof(TweenAnimation.AddType), animation.AddType) ? Operations[(int)animation.AddType] : "无效组合")} / {GetTypePath(animation.AnimationType)} / {targetSummary} / {duration}";
                if (s_HeaderStyle == null) s_HeaderStyle = new GUIStyle(EditorStyles.foldout) { richText = true };
                property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded,
                    new GUIContent((textError == null ? "" : "[错误] ") + summary, referenceError ?? error ?? targetName), true, s_HeaderStyle);
                Next(ref rect);
                rect.y += TimelineHeight;
                if (!property.isExpanded) return;
                if (textError != null)
                {
                    EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, ErrorHeight), textError, MessageType.Error);
                    rect.y += ErrorHeight + Spacing;
                }
                Field(ref rect, property, "Name", new GUIContent("名称", "用于识别动画项，不影响播放；留空时显示序号。"));
                PopupField(rect, property.FindPropertyRelative("AddType"), new GUIContent("组合方式",
                    "Chain：等待所有前项结束；Group：与最近添加项同起点；Insert：在单轮序列的绝对时间插入，不含全局延迟。"), Operations);
                Next(ref rect);
                if (ShowInsert(property)) Field(ref rect, property, "InsertTime", new GUIContent("插入时间（秒）", "单轮序列绝对时间，不含全局延迟；单项延迟在此基础上叠加。"));
                EditorGUI.BeginChangeCheck();
                DrawType(rect, property);
                var typeChanged = EditorGUI.EndChangeCheck();
                Next(ref rect);
                // 类型更改可能自动匹配引用，只有编辑后才重新读取整项。
                if (typeChanged) ReadAnimation(property, animation);
                if (!SupportsAnimationFields(animation.AnimationType))
                {
                    FloatField(rect, property.FindPropertyRelative("DurationOrSpeed"), new GUIContent("等待（秒）", "独立等待项，无目标、不触发事件，不使用隐藏的动画参数。"));
                    return;
                }
                var mixedType = property.FindPropertyRelative("AnimationType").hasMultipleDifferentValues;
                using (new EditorGUI.DisabledScope(mixedType))
                    ReferenceField(rect, property, "Target", animation.AnimationType,
                        new GUIContent("动画目标", "被此动画修改属性的对象。匹配同一物体上的所需组件；缺少组件时保留引用并报错，不添加组件。"));
                Next(ref rect);
                animation.Target = property.FindPropertyRelative("Target").objectReferenceValue as Component;
                Endpoint(ref rect, property, animation, false);
                Endpoint(ref rect, property, animation, true);
                FloatField(rect, property.FindPropertyRelative("DurationOrSpeed"), new GUIContent(
                    property.FindPropertyRelative("SpeedBased").hasMultipleDifferentValues ? "时长 / 速度" : animation.SpeedBased ? "速度（单位/秒）" : "时长（秒）"));
                Next(ref rect);
                DrawEase(rect, property);
                Next(ref rect);
                if (ShowCurve(property)) Field(ref rect, property, "EaseCurve", new GUIContent("自定义曲线"));
                using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects
                    || TweenAnimationBinding.ForItem(property) == null || Application.isPlaying
                    || error != null || AnimationMode.InAnimationMode() || EditorApplication.isCompiling))
                    if (GUI.Button(rect, new GUIContent("预览此项", "只播放这一项（含单项延迟和循环），不应用序列缓动、延迟、循环或组合；停止恢复原值，不触发事件。")))
                    {
                        var sequence = TweenAnimationBinding.ForItem(property);
                        var path = property.propertyPath;
                        property.serializedObject.ApplyModifiedProperties();
                        try { TweenAnimationPreview.StartSingleBinding(sequence, path); }
                        catch (System.Exception exception) { TweenAnimationPreview.Stop(); Debug.LogException(exception, sequence?.Owner); }
                        GUIUtility.ExitGUI();
                    }
                Next(ref rect);
                var advanced = property.FindPropertyRelative("SpeedBased");
                advanced.isExpanded = EditorGUI.Foldout(rect, advanced.isExpanded, AdvancedSummary(property), true);
                Next(ref rect);
                if (advanced.isExpanded)
                {
                    if (ShowRelative(property))
                        Field(ref rect, property, "Relative", new GUIContent("相对结束值", "结束值作为相对于起始值的加法偏移；缩放不是倍率，旋转使用欧拉角。每次新播放重新采样，方向切换不重新采样。"));
                    Field(ref rect, property, "Delay", new GUIContent("延迟（秒）"));
                    var mixed = EditorGUI.showMixedValue;
                    EditorGUI.showMixedValue = advanced.hasMultipleDifferentValues;
                    EditorGUI.BeginChangeCheck();
                    var mode = EditorGUI.Popup(rect, "计时方式", advanced.boolValue ? 1 : 0, new[] { "按时长", "按速度" });
                    if (EditorGUI.EndChangeCheck()) advanced.boolValue = mode == 1;
                    EditorGUI.showMixedValue = mixed;
                    Next(ref rect);
                    IntField(rect, property.FindPropertyRelative("Loops"), new GUIContent("播放次数", "单项仅支持正整数；无限循环请在序列设置中配置。"));
                    Next(ref rect);
                    using (new EditorGUI.DisabledScope(!property.FindPropertyRelative("Loops").hasMultipleDifferentValues
                        && property.FindPropertyRelative("Loops").intValue == 1))
                        Field(ref rect, property, "LoopType", new GUIContent("循环方式"));
                    if (SupportsSnapping(animation.AnimationType))
                        using (new EditorGUI.DisabledScope(mixedType))
                            Field(ref rect, property, "Snapping", new GUIContent("数值取整"));
                }
                var events = property.FindPropertyRelative("CustomEase");
                events.isExpanded = EditorGUI.Foldout(rect, events.isExpanded,
                    "事件（仅运行时）" + EventSummary(property, false), true);
                Next(ref rect);
                if (events.isExpanded)
                    foreach (var name in new[] { "OnPlay", "OnUpdate", "OnComplete" })
                    {
                        var eventProperty = property.FindPropertyRelative(name);
                        rect.height = EditorGUI.GetPropertyHeight(eventProperty, true);
                        EditorGUI.PropertyField(rect, eventProperty, new GUIContent(EventLabel(name)), true);
                        rect.y += rect.height + Spacing;
                    }
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndent;
                EditorGUI.EndProperty();
            }
        }

        internal static string GetTypePath(TweenAnimation.TweenType type)
        {
            return type switch
            {
                TweenAnimation.TweenType.Move => "位置 (Move)/世界/XYZ",
                TweenAnimation.TweenType.MoveX => "位置 (Move)/世界/X",
                TweenAnimation.TweenType.MoveY => "位置 (Move)/世界/Y",
                TweenAnimation.TweenType.MoveZ => "位置 (Move)/世界/Z",
                TweenAnimation.TweenType.LocalMove => "位置 (Move)/局部 (LocalMove)/XYZ",
                TweenAnimation.TweenType.LocalMoveX => "位置 (Move)/局部 (LocalMove)/X",
                TweenAnimation.TweenType.LocalMoveY => "位置 (Move)/局部 (LocalMove)/Y",
                TweenAnimation.TweenType.LocalMoveZ => "位置 (Move)/局部 (LocalMove)/Z",
                TweenAnimation.TweenType.Scale => "缩放 (Scale)/XYZ",
                TweenAnimation.TweenType.ScaleX => "缩放 (Scale)/X",
                TweenAnimation.TweenType.ScaleY => "缩放 (Scale)/Y",
                TweenAnimation.TweenType.ScaleZ => "缩放 (Scale)/Z",
                TweenAnimation.TweenType.Rotation => "旋转 (Rotation)/世界",
                TweenAnimation.TweenType.LocalRotation => "旋转 (Rotation)/局部 (LocalRotation)",
                TweenAnimation.TweenType.AnchorPosition => "UI/锚点位置 (AnchorPosition)/XY",
                TweenAnimation.TweenType.AnchorPositionX => "UI/锚点位置 (AnchorPosition)/X",
                TweenAnimation.TweenType.AnchorPositionY => "UI/锚点位置 (AnchorPosition)/Y",
                TweenAnimation.TweenType.AnchorPositionZ => "UI/锚点位置 (AnchorPosition)/Z",
                TweenAnimation.TweenType.AnchorPosition3D => "UI/锚点位置 (AnchorPosition)/XYZ",
                TweenAnimation.TweenType.Color => "UI/颜色 (Color)",
                TweenAnimation.TweenType.Fade => "UI/透明度 (Fade)",
                TweenAnimation.TweenType.CanvasGroupFade => "UI/CanvasGroup 透明度",
                TweenAnimation.TweenType.FillAmount => "UI/填充量 (FillAmount)",
                TweenAnimation.TweenType.LayoutFlexibleSize => "UI/布局尺寸 (Layout)/Flexible",
                TweenAnimation.TweenType.LayoutMinSize => "UI/布局尺寸 (Layout)/Min",
                TweenAnimation.TweenType.LayoutPreferredSize => "UI/布局尺寸 (Layout)/Preferred",
                TweenAnimation.TweenType.SizeDelta => "UI/尺寸 (SizeDelta)",
                TweenAnimation.TweenType.SliderValue => "UI/Slider 数值",
                TweenAnimation.TweenType.Wait => "时间 (Time)/等待 (Wait)",
                _ => $"未知类型 ({(int)type})"
            };
        }

        internal static string GetItemTitle(string name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();

        private static void DrawItemMenu(Rect rect, SerializedProperty property)
        {
            if (!GUI.enabled || Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition)) return;
            var objects = property.serializedObject.targetObjects;
            var path = property.propertyPath;
            property.serializedObject.ApplyModifiedProperties();
            var menu = new GenericMenu();
            if (!property.serializedObject.isEditingMultipleObjects)
                menu.AddItem(new GUIContent("复制此项（含引用与事件）"), false, () => DuplicateItem(objects, path));
            else menu.AddDisabledItem(new GUIContent("复制此项（仅单选）"));
            if (!property.hasMultipleDifferentValues) menu.AddItem(new GUIContent("复制参数"), false, () =>
            {
                using (var data = new SerializedObject(objects)) CopyParameters(data.FindProperty(path));
            });
            else menu.AddDisabledItem(new GUIContent("复制参数（配置不同）"));
            var pasteLabel = new GUIContent("粘贴参数（保留引用与事件）");
            if (CanPasteParameters(property))
                menu.AddItem(pasteLabel, false, () =>
                {
                    using (var data = new SerializedObject(objects))
                    {
                        PasteParameters(data.FindProperty(path));
                        data.ApplyModifiedProperties();
                    }
                });
            else menu.AddDisabledItem(pasteLabel);
            menu.AddSeparator(string.Empty);
            var swap = new GUIContent("交换起始值与结束值");
            if (CanSwapEndpoints(property)) menu.AddItem(swap, false, () =>
            {
                using (var data = new SerializedObject(objects))
                {
                    SwapEndpoints(data.FindProperty(path));
                    data.ApplyModifiedProperties();
                }
            });
            else menu.AddDisabledItem(swap);
            var focus = new GUIContent("在 Scene 聚焦目标");
            var target = property.FindPropertyRelative("Target");
            if (!target.hasMultipleDifferentValues && target.objectReferenceValue is Component component && !EditorUtility.IsPersistent(component))
                menu.AddItem(focus, false, () => FocusTarget(component));
            else menu.AddDisabledItem(focus);
            menu.ShowAsContext();
            Event.current.Use();
        }

        internal static void DuplicateItem(Object[] targets, string path)
        {
            if (targets.Length != 1 || TweenAnimationPreview.IsActive) return;
            using (var data = new SerializedObject(targets))
            {
                var marker = path.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
                if (marker < 0) return;
                var array = data.FindProperty(path.Substring(0, marker));
                if (!int.TryParse(path.Substring(marker + 12).TrimEnd(']'), out var index) || array == null || index >= array.arraySize) return;
                array.InsertArrayElementAtIndex(index);
                array.GetArrayElementAtIndex(index + 1).isExpanded = true;
                data.ApplyModifiedProperties();
            }
        }

        internal static Bounds GetTargetBounds(Component target)
        {
            if (target == null || EditorUtility.IsPersistent(target)) throw new System.ArgumentException("目标必须是场景对象。", nameof(target));
            if (target.transform is RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var bounds = new Bounds(corners[0], Vector3.zero);
                foreach (var corner in corners) bounds.Encapsulate(corner);
                return bounds;
            }
            var renderer = target.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds : new Bounds(target.transform.position, Vector3.one);
        }

        internal static void FocusTarget(Component target)
        {
            if (target == null || EditorUtility.IsPersistent(target)) return;
            var view = SceneView.lastActiveSceneView;
            if (view != null) view.Frame(GetTargetBounds(target), false);
        }

        internal static bool CanSwapEndpoints(SerializedProperty item) => item != null
            && GetEndpointMode(item, false) == 1 && GetEndpointMode(item, true) == 1
            && !item.FindPropertyRelative("AnimationType").hasMultipleDifferentValues
            && (!ShowRelative(item) || !item.FindPropertyRelative("Relative").boolValue
                && !item.FindPropertyRelative("Relative").hasMultipleDifferentValues);

        internal static void SwapEndpoints(SerializedProperty item)
        {
            if (!CanSwapEndpoints(item)) throw new System.InvalidOperationException("仅当起始值与结束值均为指定数值时支持交换。");
            var data = item.serializedObject;
            if (data.isEditingMultipleObjects)
            {
                var path = item.propertyPath;
                data.ApplyModifiedProperties();
                foreach (var target in data.targetObjects)
                    using (var individual = new SerializedObject(target))
                    {
                        SwapEndpoints(individual.FindProperty(path));
                        individual.ApplyModifiedProperties();
                    }
                data.Update();
                return;
            }
            var from = item.FindPropertyRelative("FromValue");
            var to = item.FindPropertyRelative("ToValue");
            var value = from.vector4Value;
            from.vector4Value = to.vector4Value;
            to.vector4Value = value;
        }

        internal static void CopyParameters(SerializedProperty item)
        {
            CopiedParameters.Clear();
            if (item == null || item.hasMultipleDifferentValues) return;
            m_CopiedType = item.FindPropertyRelative("AnimationType").intValue;
            foreach (var name in ParameterNames)
            {
                var property = item.FindPropertyRelative(name);
                CopiedParameters[name] = property.propertyType switch
                {
                    SerializedPropertyType.Boolean => property.boolValue,
                    SerializedPropertyType.Float => property.floatValue,
                    SerializedPropertyType.Enum => property.intValue,
                    SerializedPropertyType.Integer => property.intValue,
                    SerializedPropertyType.Vector4 => property.vector4Value,
                    SerializedPropertyType.AnimationCurve => property.animationCurveValue,
                    _ => throw new System.InvalidOperationException(name)
                };
            }
        }

        internal static bool CanPasteParameters(SerializedProperty item) => item != null && CopiedParameters.Count != 0
            && !item.FindPropertyRelative("AnimationType").hasMultipleDifferentValues
            && item.FindPropertyRelative("AnimationType").intValue == m_CopiedType;

        internal static void PasteParameters(SerializedProperty item)
        {
            if (!CanPasteParameters(item)) throw new System.InvalidOperationException("请先复制相同动画类型的参数。");
            foreach (var pair in CopiedParameters)
            {
                var property = item.FindPropertyRelative(pair.Key);
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Boolean: property.boolValue = (bool)pair.Value; break;
                    case SerializedPropertyType.Float: property.floatValue = (float)pair.Value; break;
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Integer: property.intValue = (int)pair.Value; break;
                    case SerializedPropertyType.Vector4: property.vector4Value = (Vector4)pair.Value; break;
                    case SerializedPropertyType.AnimationCurve: property.animationCurveValue = (AnimationCurve)pair.Value; break;
                }
            }
        }

        internal static bool SupportsSnapping(TweenAnimation.TweenType type) => type switch
        {
            TweenAnimation.TweenType.Move or TweenAnimation.TweenType.MoveX or TweenAnimation.TweenType.MoveY
            or TweenAnimation.TweenType.MoveZ or TweenAnimation.TweenType.LocalMove or TweenAnimation.TweenType.LocalMoveX
            or TweenAnimation.TweenType.LocalMoveY or TweenAnimation.TweenType.LocalMoveZ
            or TweenAnimation.TweenType.AnchorPosition or TweenAnimation.TweenType.AnchorPositionX
            or TweenAnimation.TweenType.AnchorPositionY or TweenAnimation.TweenType.AnchorPositionZ
            or TweenAnimation.TweenType.AnchorPosition3D or TweenAnimation.TweenType.SizeDelta => true,
            _ => false
        };

        private static void DrawType(Rect rect, SerializedProperty parent)
        {
            var property = parent.FindPropertyRelative("AnimationType");
            var labels = new System.Collections.Generic.List<GUIContent>();
            foreach (var type in AnimationTypes) labels.Add(new GUIContent(GetTypePath(type)));
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.Popup(rect, new GUIContent("动画类型"),
                System.Array.IndexOf(AnimationTypes, (TweenAnimation.TweenType)property.intValue), labels.ToArray());
            if (EditorGUI.EndChangeCheck() && selected >= 0)
            {
                property.intValue = (int)AnimationTypes[selected];
                MatchReferences(parent);
            }
            EditorGUI.showMixedValue = mixed;
        }

        // -1 表示多选来源不同；0 当前值，1 数值，2 对象引用。
        internal static int GetEndpointMode(SerializedProperty parent, bool to)
        {
            var reference = parent.FindPropertyRelative(to ? "UseToTarget" : "UseFromTarget");
            if (reference.hasMultipleDifferentValues) return -1;
            if (reference.boolValue) return 2;
            if (to) return 1;
            var from = parent.FindPropertyRelative("UseFromValue");
            return from.hasMultipleDifferentValues ? -1 : from.boolValue ? 1 : 0;
        }

        internal static void SetEndpointMode(SerializedProperty parent, bool to, int mode)
        {
            if (mode < (to ? 1 : 0) || mode > 2) throw new System.ArgumentOutOfRangeException(nameof(mode));
            parent.FindPropertyRelative(to ? "UseToTarget" : "UseFromTarget").boolValue = mode == 2;
            if (!to && mode != 2) parent.FindPropertyRelative("UseFromValue").boolValue = mode == 1;
        }

        private static int EndpointRows(SerializedProperty property, bool to)
        {
            var mode = GetEndpointMode(property, to);
            var type = (TweenAnimation.TweenType)property.FindPropertyRelative("AnimationType").intValue;
            return 1 + (mode == 2 ? 1 : mode == 1 ? 2 : 0) + (TweenAnimationPreview.SupportsPosition(type) ? 1 : 0);
        }

        private static void Endpoint(ref Rect rect, SerializedProperty parent, TweenAnimation.SequenceAnimation animation, bool to)
        {
            var mode = GetEndpointMode(parent, to);
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mode == -1;
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.Popup(rect, SourceLabel(to),
                mode < 0 ? -1 : to ? mode - 1 : mode,
                System.Array.ConvertAll(to ? ToModes : FromModes, text => new GUIContent(text)));
            if (EditorGUI.EndChangeCheck())
            {
                mode = to ? selected + 1 : selected;
                SetEndpointMode(parent, to, mode);
            }
            EditorGUI.showMixedValue = mixed;
            Next(ref rect);
            var mixedType = parent.FindPropertyRelative("AnimationType").hasMultipleDifferentValues;
            using (new EditorGUI.DisabledScope(mixedType))
            {
                if (mode == 2)
                {
                    ReferenceField(rect, parent, to ? "ToTarget" : "FromTarget", animation.AnimationType,
                        ReferenceLabel(to));
                    Next(ref rect);
                }
                else if (mode == 1)
                {
                    var value = parent.FindPropertyRelative(to ? "ToValue" : "FromValue");
                    var button = new Rect(rect.xMax - 76f, rect.y, 76f, rect.height);
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - 80f), rect.height),
                        to && animation.UsesRelativeEnd ? new GUIContent("结束偏移") : ValueLabel(to));
                    var valid = TryCaptureCurrentValue(animation, to, out var current);
                    using (new EditorGUI.DisabledScope(!valid || parent.serializedObject.isEditingMultipleObjects))
                        if (GUI.Button(button, new GUIContent("读取当前", "把动画目标的当前属性复制到此数值配置；不修改目标属性。")))
                            value.vector4Value = current;
                    Next(ref rect);
                    if (mixedType) EditorGUI.LabelField(rect, "动画类型不同，请先统一类型");
                    else DrawValue(rect, value, string.Empty, animation.AnimationType);
                    Next(ref rect);
                }
            }
            if (TweenAnimationPreview.SupportsPosition(animation.AnimationType))
            {
                var width = (rect.width - 3f) * .5f;
                using (new EditorGUI.DisabledScope(mixedType || TweenAnimationBinding.ForItem(parent) == null
                    || parent.serializedObject.isEditingMultipleObjects || Application.isPlaying
                    || animation.GetValidationError() != null || AnimationMode.InAnimationMode()))
                {
                    if (GUI.Button(new Rect(rect.x, rect.y, width, rect.height), to ? "查看终点" : "查看起点"))
                        BeginEndpoint(parent, to, false);
                    using (new EditorGUI.DisabledScope(mode != 1))
                        if (GUI.Button(new Rect(rect.x + width + 3, rect.y, width, rect.height),
                            new GUIContent(to ? "编辑终点" : "编辑起点", "在Scene中拖动位置手柄，写回指定数值；结束后恢复目标原位置。")))
                            BeginEndpoint(parent, to, true);
                }
                Next(ref rect);
            }
        }

        internal static bool TryCaptureCurrentValue(TweenAnimation.SequenceAnimation animation, bool to, out Vector4 value)
        {
            value = default;
            if (animation.Target == null || animation.AnimationType == TweenAnimation.TweenType.Wait
                || !System.Enum.IsDefined(typeof(TweenAnimation.TweenType), animation.AnimationType)) return false;
            var required = TweenAnimation.GetRequiredComponentType(animation.AnimationType);
            if (!required.IsInstanceOfType(animation.Target)) return false;
            var relative = to && animation.UsesRelativeEnd;
            if (relative && animation.UseFromTarget
                && (animation.FromTarget == null || !required.IsInstanceOfType(animation.FromTarget))) return false;
            value = animation.CaptureCurrentValue() - (relative ? animation.CaptureEndpointValue(false) : Vector4.zero);
            for (var i = 0; i < 4; i++)
                if (float.IsNaN(value[i]) || float.IsInfinity(value[i])) return false;
            return true;
        }

        private static void BeginEndpoint(SerializedProperty property, bool to, bool edit)
        {
            var sequence = TweenAnimationBinding.ForItem(property);
            var path = property.propertyPath;
            property.serializedObject.ApplyModifiedProperties();
            try { TweenAnimationPreview.StartEndpointBinding(sequence, path, to, edit); }
            catch (System.Exception exception)
            {
                TweenAnimationPreview.Stop();
                Debug.LogException(exception, sequence?.Owner);
            }
            GUIUtility.ExitGUI();
        }

        private static bool ShowCurve(SerializedProperty parent)
        {
            var custom = parent.FindPropertyRelative("CustomEase");
            return !custom.hasMultipleDifferentValues && custom.boolValue;
        }

        internal static bool ShowRelative(SerializedProperty parent) =>
            !parent.FindPropertyRelative("AnimationType").hasMultipleDifferentValues
            && TweenAnimation.SequenceAnimation.SupportsRelative((TweenAnimation.TweenType)parent.FindPropertyRelative("AnimationType").intValue)
            && GetEndpointMode(parent, true) == 1;

        private static bool ShowInsert(SerializedProperty parent)
        {
            var property = parent.FindPropertyRelative("AddType");
            return !property.hasMultipleDifferentValues && property.intValue == (int)TweenAnimation.AddType.Insert;
        }

        internal static void SetEaseSelection(SerializedProperty parent, LitMotion.Ease ease)
        {
            parent.FindPropertyRelative("CustomEase").boolValue = ease == LitMotion.Ease.CustomAnimationCurve;
            if (ease != LitMotion.Ease.CustomAnimationCurve) parent.FindPropertyRelative("Ease").intValue = (int)ease;
        }

        private static void DrawEase(Rect rect, SerializedProperty parent)
        {
            var custom = parent.FindPropertyRelative("CustomEase");
            var ease = parent.FindPropertyRelative("Ease");
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = custom.hasMultipleDifferentValues || (!custom.boolValue && ease.hasMultipleDifferentValues);
            var current = custom.boolValue ? LitMotion.Ease.CustomAnimationCurve : (LitMotion.Ease)ease.intValue;
            DrawEaseButton(rect, new GUIContent("缓动"), current, EditorGUI.showMixedValue,
                () => PopupWindow.Show(rect, new EasePicker(parent.serializedObject.targetObjects, parent.propertyPath, current, true)));
            EditorGUI.showMixedValue = mixed;
        }

        internal static void DrawGlobalEase(Rect rect, SerializedProperty property)
        {
            var current = (LitMotion.Ease)property.intValue;
            DrawEaseButton(rect, new GUIContent("全局缓动", "改变整个时间线的推进速度，与单项缓动叠加；通常使用匀速。"),
                current, property.hasMultipleDifferentValues,
                () => PopupWindow.Show(rect, new EasePicker(property.serializedObject.targetObjects, property.propertyPath, current, false)));
        }

        private static void DrawEaseButton(Rect rect, GUIContent label, LitMotion.Ease current, bool mixed, System.Action open)
        {
            var field = EditorGUI.PrefixLabel(rect, label);
            var content = new GUIContent(mixed ? "—" : GetEaseLabel(current), "选择缓动曲线");
            if (GUI.Button(field, content, EditorStyles.popup)) open();
            if (!mixed && current != LitMotion.Ease.CustomAnimationCurve
                && field.width > EditorStyles.popup.CalcSize(content).x + 90)
                DrawEaseCurve(new Rect(field.xMax - 72, field.y + 2, 52, field.height - 4), current);
        }

        internal static string GetEaseCategory(LitMotion.Ease ease)
        {
            if (ease == LitMotion.Ease.Linear) return EaseCategories[2];
            if (ease == LitMotion.Ease.CustomAnimationCurve) return EaseCategories[13];
            var name = ease.ToString();
            if (name.EndsWith("Sine")) return EaseCategories[3];
            if (name.EndsWith("Quad")) return EaseCategories[4];
            if (name.EndsWith("Cubic")) return EaseCategories[5];
            if (name.EndsWith("Quart")) return EaseCategories[6];
            if (name.EndsWith("Quint")) return EaseCategories[7];
            if (name.EndsWith("Expo")) return EaseCategories[8];
            if (name.EndsWith("Circ")) return EaseCategories[9];
            if (name.EndsWith("Elastic")) return EaseCategories[10];
            if (name.EndsWith("Back")) return EaseCategories[11];
            if (name.EndsWith("Bounce")) return EaseCategories[12];
            return "未知";
        }

        internal static string GetEaseLabel(LitMotion.Ease ease)
        {
            var category = GetEaseCategory(ease).Split(' ')[0];
            if (ease == LitMotion.Ease.Linear) return "匀速 (Linear)";
            if (ease == LitMotion.Ease.CustomAnimationCurve) return "自定义 (CustomAnimationCurve)";
            var name = ease.ToString();
            return category + " · " + (name.StartsWith("InOut") ? "先慢后慢" : name.StartsWith("Out") ? "减速" : "加速") + $" ({name})";
        }

        internal static bool IsCommonEase(LitMotion.Ease ease) => ease is LitMotion.Ease.Linear or LitMotion.Ease.OutQuad
            or LitMotion.Ease.InOutQuad or LitMotion.Ease.OutCubic or LitMotion.Ease.OutBack or LitMotion.Ease.OutBounce;

        internal static Vector3[] GetEaseSamples(LitMotion.Ease ease)
        {
            if (ease == LitMotion.Ease.CustomAnimationCurve || !System.Enum.IsDefined(typeof(LitMotion.Ease), ease))
                throw new System.ArgumentOutOfRangeException(nameof(ease));
            if (EaseSamples.TryGetValue(ease, out var points)) return points;
            points = new Vector3[65];
            for (var i = 0; i < points.Length; i++)
            {
                var t = i / (float)(points.Length - 1);
                points[i] = new Vector3(t, LitMotion.EaseUtility.Evaluate(t, ease));
            }
            EaseSamples.Add(ease, points);
            return points;
        }

        private static void DrawEaseCurve(Rect rect, LitMotion.Ease ease)
        {
            if (Event.current.type != EventType.Repaint) return;
            var samples = GetEaseSamples(ease);
            var points = new Vector3[samples.Length];
            // 所有缩略图使用相同纵轴，保留超出端点的回弹/弹性，不截成 0–1。
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector3(rect.x + samples[i].x * rect.width, rect.yMax - (samples[i].y + .5f) / 2 * rect.height);
            var previous = Handles.color;
            Handles.BeginGUI();
            Handles.color = new Color(.5f, .5f, .5f, .4f);
            foreach (var value in new[] { 0f, 1f })
            {
                var y = rect.yMax - (value + .5f) / 2 * rect.height;
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }
            Handles.color = new Color(.3f, .75f, 1f);
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = previous;
            Handles.EndGUI();
        }

        internal static void ApplyEaseSelection(Object[] targets, string path, LitMotion.Ease ease, bool item)
        {
            if (!System.Enum.IsDefined(typeof(LitMotion.Ease), ease) || !item && ease == LitMotion.Ease.CustomAnimationCurve)
                throw new System.ArgumentOutOfRangeException(nameof(ease));
            foreach (var target in targets) if (target == null) return;
            using (var data = new SerializedObject(targets))
            {
                var property = data.FindProperty(path);
                if (property == null) return;
                if (item) SetEaseSelection(property, ease);
                else property.intValue = (int)ease;
                data.ApplyModifiedProperties();
            }
        }

        private sealed class EasePicker : PopupWindowContent
        {
            private readonly Object[] m_Targets;
            private readonly string m_Path;
            private readonly LitMotion.Ease m_Selected;
            private readonly bool m_Item;
            private int m_Category;
            private Vector2 m_Scroll;

            internal EasePicker(Object[] targets, string path, LitMotion.Ease selected, bool item)
            {
                m_Targets = targets;
                m_Path = path;
                m_Selected = selected;
                m_Item = item;
            }

            public override Vector2 GetWindowSize() => new Vector2(390, 390);

            public override void OnGUI(Rect rect)
            {
                EditorGUI.BeginChangeCheck();
                m_Category = EditorGUILayout.Popup("曲线分类", m_Category, EaseCategories);
                if (EditorGUI.EndChangeCheck()) m_Scroll = Vector2.zero;
                m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
                var values = System.Array.FindAll(EaseValues, ease => (m_Item || ease != LitMotion.Ease.CustomAnimationCurve)
                    && (m_Category == 1 || m_Category == 0 && IsCommonEase(ease) || GetEaseCategory(ease) == EaseCategories[m_Category]));
                for (var i = 0; i < values.Length; i += 3)
                {
                    using (new EditorGUILayout.HorizontalScope())
                        for (var column = 0; column < 3 && i + column < values.Length; column++)
                        {
                            var ease = values[i + column];
                            var card = GUILayoutUtility.GetRect(110, 94, GUILayout.ExpandWidth(true));
                            var oldColor = GUI.backgroundColor;
                            if (ease == m_Selected) GUI.backgroundColor = new Color(.4f, .7f, 1f);
                            var clicked = GUI.Button(card, new GUIContent(string.Empty, ease.ToString()));
                            GUI.backgroundColor = oldColor;
                            if (ease != LitMotion.Ease.CustomAnimationCurve)
                                DrawEaseCurve(new Rect(card.x + 8, card.y + 4, card.width - 16, 50), ease);
                            GUI.Label(new Rect(card.x + 3, card.yMax - 39, card.width - 6, 36),
                                GetEaseLabel(ease).Replace(" (", "\n("), EditorStyles.centeredGreyMiniLabel);
                            if (clicked)
                            {
                                ApplyEaseSelection(m_Targets, m_Path, ease, m_Item);
                                editorWindow.Close();
                                GUIUtility.ExitGUI();
                            }
                        }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField("曲线为进度示意；自定义曲线在动画项内编辑。", EditorStyles.miniLabel);
            }
        }

        private static void PopupField(Rect rect, SerializedProperty property, GUIContent label, string[] options)
        {
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.Popup(rect, label, property.intValue, System.Array.ConvertAll(options, text => new GUIContent(text)));
            if (EditorGUI.EndChangeCheck()) property.intValue = selected;
            EditorGUI.showMixedValue = mixed;
        }

        private static string AdvancedSummary(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues) return "更多设置 · 多个不同配置";
            var result = "更多设置";
            var delay = property.FindPropertyRelative("Delay").floatValue;
            var loops = property.FindPropertyRelative("Loops").intValue;
            if (delay != 0f) result += $" · 延迟{delay:g}秒";
            if (loops != 1) result += $" · 播放{loops}次";
            if (property.FindPropertyRelative("SpeedBased").boolValue) result += " · 按速度";
            var type = (TweenAnimation.TweenType)property.FindPropertyRelative("AnimationType").intValue;
            if (ShowRelative(property) && property.FindPropertyRelative("Relative").boolValue) result += " · 相对结束值";
            if (SupportsSnapping(type) && property.FindPropertyRelative("Snapping").boolValue) result += " · 取整";
            return result;
        }

        internal static bool SupportsAnimationFields(TweenAnimation.TweenType type) => type != TweenAnimation.TweenType.Wait;

        internal static string EventSummary(SerializedProperty parent, bool sequence)
        {
            var count = 0;
            foreach (var name in new[] { "OnPlay", "OnUpdate", "OnComplete" })
            {
                var calls = (sequence ? parent.serializedObject.FindProperty(parent.propertyPath.Substring(0, parent.propertyPath.Length - "m_Sequence".Length) + "m_" + name) : parent.FindPropertyRelative(name))
                    .FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (calls.hasMultipleDifferentValues) return " · 多个不同配置";
                count += calls.arraySize;
            }
            return count == 0 ? "" : $" · {count}个回调";
        }

        internal static string EventLabel(string name) => name switch
        {
            "OnPlay" => "开始时 (OnPlay)", "OnUpdate" => "更新时 (OnUpdate)", _ => "完成时 (OnComplete)"
        };
        internal static Component MatchComponent(Component component, TweenAnimation.TweenType type)
        {
            if (component == null || !System.Enum.IsDefined(typeof(TweenAnimation.TweenType), type)) return component;
            if (type == TweenAnimation.TweenType.Wait) return component;
            var required = TweenAnimation.GetRequiredComponentType(type);
            if (required.IsInstanceOfType(component)) return component;
            var matched = component.GetComponent(required);
            return matched != null ? matched : component;
        }

        internal static void MatchReferences(SerializedProperty property)
        {
            var typeProperty = property.FindPropertyRelative("AnimationType");
            if (typeProperty.hasMultipleDifferentValues) return;
            var serialized = property.serializedObject;
            if (serialized.isEditingMultipleObjects)
            {
                // 分别读取每个对象，避免把首个对象的引用复制到其他对象。
                var path = property.propertyPath;
                serialized.ApplyModifiedProperties();
                foreach (var target in serialized.targetObjects)
                {
                    using (var individual = new SerializedObject(target))
                    {
                        var item = individual.FindProperty(path);
                        if (item == null) continue;
                        MatchReferences(item);
                        individual.ApplyModifiedProperties();
                    }
                }
                serialized.Update();
                return;
            }
            var type = (TweenAnimation.TweenType)typeProperty.intValue;
            foreach (var name in new[] { "Target", "FromTarget", "ToTarget" })
            {
                var reference = property.FindPropertyRelative(name);
                var current = reference.objectReferenceValue as Component;
                var matched = MatchComponent(current, type);
                if (matched != current) reference.objectReferenceValue = matched;
            }
        }

        internal static bool IsReferenceValidationError(string error) => error != null
            && (error.StartsWith("Target ", System.StringComparison.Ordinal)
                || error.StartsWith("FromTarget ", System.StringComparison.Ordinal)
                || error.StartsWith("ToTarget ", System.StringComparison.Ordinal));

        internal static string GetTextError(TweenAnimation.SequenceAnimation animation)
        {
            var error = animation.GetValidationError();
            return IsReferenceValidationError(error) ? null : error;
        }

        internal static string GetReferenceError(TweenAnimation.SequenceAnimation animation, string name)
        {
            var type = animation.AnimationType;
            if (type == TweenAnimation.TweenType.Wait || !System.Enum.IsDefined(typeof(TweenAnimation.TweenType), type)) return null;
            if (name == "FromTarget" && !animation.UseFromTarget || name == "ToTarget" && !animation.UseToTarget) return null;
            var target = name == "Target" ? animation.Target : name == "FromTarget" ? animation.FromTarget : animation.ToTarget;
            var required = TweenAnimation.GetRequiredComponentType(type);
            return target == null ? $"{name} 缺失，{type} 必须引用 {required.Name}。"
                : required.IsInstanceOfType(target) ? null : $"{name} '{target.name}' 类型为 {target.GetType().Name}，{type} 必须引用 {required.Name}。";
        }

        internal static string GetSerializedReferenceError(SerializedProperty parent, string name)
            => GetReferenceErrorWithSnapshot(parent, name, null);

        private static string GetReferenceErrorWithSnapshot(SerializedProperty parent, string name, TweenAnimation.SequenceAnimation animation)
        {
            if (!parent.serializedObject.isEditingMultipleObjects)
            {
                if (animation == null)
                {
                    // 引用校验只读取类型、开关和引用，不读取数值、曲线或其他播放参数。
                    animation = new TweenAnimation.SequenceAnimation
                    {
                        AnimationType = (TweenAnimation.TweenType)parent.FindPropertyRelative("AnimationType").intValue,
                        UseFromTarget = parent.FindPropertyRelative("UseFromTarget").boolValue,
                        UseToTarget = parent.FindPropertyRelative("UseToTarget").boolValue
                    };
                    var reference = parent.FindPropertyRelative(name).objectReferenceValue as Component;
                    if (name == "Target") animation.Target = reference;
                    else if (name == "FromTarget") animation.FromTarget = reference;
                    else animation.ToTarget = reference;
                }
                return GetReferenceError(animation, name);
            }
            foreach (var target in parent.serializedObject.targetObjects)
                using (var data = new SerializedObject(target))
                {
                    var item = data.FindProperty(parent.propertyPath);
                    if (item == null) continue;
                    var individual = ReadAnimation(item);
                    // 混合值逐对象检查；已在本帧统一编辑的字段以当前序列化值为准。
                    var type = parent.FindPropertyRelative("AnimationType");
                    if (!type.hasMultipleDifferentValues) individual.AnimationType = (TweenAnimation.TweenType)type.intValue;
                    var from = parent.FindPropertyRelative("UseFromTarget");
                    var to = parent.FindPropertyRelative("UseToTarget");
                    if (!from.hasMultipleDifferentValues) individual.UseFromTarget = from.boolValue;
                    if (!to.hasMultipleDifferentValues) individual.UseToTarget = to.boolValue;
                    var reference = parent.FindPropertyRelative(name);
                    if (!reference.hasMultipleDifferentValues)
                    {
                        var value = reference.objectReferenceValue as Component;
                        if (name == "Target") individual.Target = value;
                        else if (name == "FromTarget") individual.FromTarget = value;
                        else individual.ToTarget = value;
                    }
                    var error = GetReferenceError(individual, name);
                    if (error != null) return $"{target.name}: {error}";
                }
            return null;
        }

        private static string EscapeRichText(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        internal static string FormatTargetSummary(string name, bool invalid)
        {
            var escaped = EscapeRichText(name);
            return invalid ? $"<color=#EF6565>{escaped}</color>" : escaped;
        }

        private static void ReferenceField(Rect rect, SerializedProperty parent, string name, TweenAnimation.TweenType type, GUIContent content)
        {
            var property = parent.FindPropertyRelative(name);
            var error = GetSerializedReferenceError(parent, name);
            content = new GUIContent(content.text, error ?? content.tooltip);
            EditorGUI.BeginProperty(rect, content, property);
            var mixed = EditorGUI.showMixedValue;
            var background = GUI.backgroundColor;
            var field = EditorGUI.PrefixLabel(rect, content);
            // 叠乘原生输入框底色，得到暗红填充，保留文字与对象选择按钮。
            if (error != null) GUI.backgroundColor = new Color(1f, .3f, .3f);
            try
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var reference = EditorGUI.ObjectField(field, new GUIContent(string.Empty, content.tooltip), property.objectReferenceValue, typeof(Component), true);
                if (EditorGUI.EndChangeCheck()) property.objectReferenceValue = MatchComponent(reference as Component, type);
                if (error != null)
                {
                    GUI.Label(field, new GUIContent(string.Empty, error));
                }
            }
            finally
            {
                GUI.backgroundColor = background;
                EditorGUI.showMixedValue = mixed;
                EditorGUI.EndProperty();
            }
        }

        private static void DrawValue(Rect rect, SerializedProperty property, string label, TweenAnimation.TweenType type)
        {
            var value = property.vector4Value;
            var previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            switch (type)
            {
                case TweenAnimation.TweenType.Color:
                    value = EditorGUI.ColorField(rect, label, value); break;
                case TweenAnimation.TweenType.Move:
                case TweenAnimation.TweenType.LocalMove:
                case TweenAnimation.TweenType.Scale:
                case TweenAnimation.TweenType.Rotation:
                case TweenAnimation.TweenType.LocalRotation:
                case TweenAnimation.TweenType.AnchorPosition3D:
                    DrawVectorValue(rect, property, label, 3); break;
                case TweenAnimation.TweenType.AnchorPosition:
                case TweenAnimation.TweenType.SizeDelta:
                case TweenAnimation.TweenType.LayoutFlexibleSize:
                case TweenAnimation.TweenType.LayoutMinSize:
                case TweenAnimation.TweenType.LayoutPreferredSize:
                    DrawVectorValue(rect, property, label, 2); break;
                default:
                    FloatField(rect, property.FindPropertyRelative("x"), new GUIContent(label)); break;
            }
            if (EditorGUI.EndChangeCheck() && type == TweenAnimation.TweenType.Color) property.vector4Value = value;
            EditorGUI.showMixedValue = previousMixed;
        }

        private static void DrawVectorValue(Rect rect, SerializedProperty property, string label, int dimensions)
        {
            if (!string.IsNullOrEmpty(label)) rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 13f;
            try
            {
                var width = (rect.width - (dimensions - 1) * 2f) / dimensions;
                for (var axis = 0; axis < dimensions; axis++)
                {
                    var component = property.FindPropertyRelative(axis == 0 ? "x" : axis == 1 ? "y" : "z");
                    EditorGUI.showMixedValue = component.hasMultipleDifferentValues;
                    EditorGUI.BeginChangeCheck();
                    var value = EditorGUI.FloatField(new Rect(rect.x + axis * (width + 2f), rect.y, width, rect.height),
                        axis == 0 ? "X" : axis == 1 ? "Y" : "Z", component.floatValue);
                    if (EditorGUI.EndChangeCheck()) SetValueComponent(property, axis, value);
                }
            }
            finally { EditorGUIUtility.labelWidth = labelWidth; }
        }

        internal static void SetValueComponent(SerializedProperty property, int axis, float value)
        {
            if (axis < 0 || axis > 2) throw new System.ArgumentOutOfRangeException(nameof(axis));
            property.FindPropertyRelative(axis == 0 ? "x" : axis == 1 ? "y" : "z").floatValue = value;
        }

        private static void Field(ref Rect rect, SerializedProperty parent, string name, GUIContent label)
        {
            var property = parent.FindPropertyRelative(name);
            if (name == "Delay") FloatField(rect, property, label);
            else EditorGUI.PropertyField(rect, property, label);
            Next(ref rect);
        }

        private static void Next(ref Rect rect) => rect.y += RowHeight;

        internal static void FloatField(Rect rect, SerializedProperty property, GUIContent label)
        {
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.FloatField(rect, label, property.floatValue);
            if (EditorGUI.EndChangeCheck()) property.floatValue = value;
            EditorGUI.showMixedValue = mixed;
        }

        internal static void IntField(Rect rect, SerializedProperty property, GUIContent label)
        {
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.IntField(rect, label, property.intValue);
            if (EditorGUI.EndChangeCheck()) property.intValue = value;
            EditorGUI.showMixedValue = mixed;
        }

        private static void Toggle(Rect rect, SerializedProperty property, string label, string tooltip = null)
        {
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.ToggleLeft(rect, new GUIContent(label, tooltip), property.boolValue);
            if (EditorGUI.EndChangeCheck()) property.boolValue = value;
            EditorGUI.showMixedValue = mixed;
        }

        internal static TweenAnimation.SequenceAnimation ReadAnimation(SerializedProperty property, TweenAnimation.SequenceAnimation destination = null)
        {
            var animation = destination ?? new TweenAnimation.SequenceAnimation();
            PopulateAnimation(property, animation);
            return animation;
        }

        private static void PopulateAnimation(SerializedProperty property, TweenAnimation.SequenceAnimation animation)
        {
            animation.Name = property.FindPropertyRelative("Name").stringValue;
            animation.AddType = (TweenAnimation.AddType)property.FindPropertyRelative("AddType").intValue;
            animation.InsertTime = property.FindPropertyRelative("InsertTime").floatValue;
            animation.AnimationType = (TweenAnimation.TweenType)property.FindPropertyRelative("AnimationType").intValue;
            animation.Target = property.FindPropertyRelative("Target").objectReferenceValue as Component;
            animation.UseToTarget = property.FindPropertyRelative("UseToTarget").boolValue;
            animation.ToTarget = property.FindPropertyRelative("ToTarget").objectReferenceValue as Component;
            animation.UseFromValue = property.FindPropertyRelative("UseFromValue").boolValue;
            animation.FromValue = property.FindPropertyRelative("FromValue").vector4Value;
            animation.UseFromTarget = property.FindPropertyRelative("UseFromTarget").boolValue;
            animation.FromTarget = property.FindPropertyRelative("FromTarget").objectReferenceValue as Component;
            animation.ToValue = property.FindPropertyRelative("ToValue").vector4Value;
            animation.Relative = property.FindPropertyRelative("Relative").boolValue;
            animation.DurationOrSpeed = property.FindPropertyRelative("DurationOrSpeed").floatValue;
            animation.SpeedBased = property.FindPropertyRelative("SpeedBased").boolValue;
            animation.Delay = property.FindPropertyRelative("Delay").floatValue;
            animation.Loops = property.FindPropertyRelative("Loops").intValue;
            animation.LoopType = (TweenAnimation.LoopType)property.FindPropertyRelative("LoopType").intValue;
            animation.UpdateType = (TweenAnimation.UpdateType)property.FindPropertyRelative("UpdateType").intValue;
            animation.IgnoreTimeScale = property.FindPropertyRelative("IgnoreTimeScale").boolValue;
            animation.CustomEase = property.FindPropertyRelative("CustomEase").boolValue;
            animation.Ease = (LitMotion.Ease)property.FindPropertyRelative("Ease").intValue;
            animation.EaseCurve = animation.CustomEase ? property.FindPropertyRelative("EaseCurve").animationCurveValue : null;
            animation.Snapping = property.FindPropertyRelative("Snapping").boolValue;
        }
    }
}
