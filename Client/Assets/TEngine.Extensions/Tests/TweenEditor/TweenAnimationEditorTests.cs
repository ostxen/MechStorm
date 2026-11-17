using System;
using System.Collections.Generic;
using System.Reflection;
using LitMotion;
using NUnit.Framework;
using TEngine.Extensions.Tween;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Animation = TEngine.Extensions.Tween.TweenAnimation.SequenceAnimation;
using TweenType = TEngine.Extensions.Tween.TweenAnimation.TweenType;
using Object = UnityEngine.Object;

namespace TEngine.Extensions.Tests.TweenEditor
{
    public sealed class TweenAnimationEditorTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();
        private Scene m_Scene;
        private Type m_PreviewType;
        private bool m_EnteredFixture;
        private bool m_OwnsExternalMode;

        [SetUp]
        public void SetUp()
        {
            if (AnimationMode.InAnimationMode()) Assert.Ignore("已有其他工具持有 AnimationMode，不能运行隔离预览测试。");
            Assert.That(Application.isPlaying, Is.False);
            m_PreviewType = FindPreviewType();
            Assert.That(m_PreviewType, Is.Not.Null, "未找到 TweenAnimationPreview 编辑器类型。");
            m_Scene = EditorSceneManager.NewPreviewScene();
            m_EnteredFixture = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (!m_EnteredFixture) return;
            try
            {
                InvokePreview("Stop");
            }
            finally
            {
                if (m_OwnsExternalMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                m_OwnsExternalMode = false;
                // 从未启用的测试对象不会收到 OnDestroy；显式释放其保留时间线。
                foreach (var go in m_Objects)
                    if (go != null && go.TryGetComponent<TweenAnimationComponent>(out var sequence)) sequence.Animation.DOKill();
                for (var i = m_Objects.Count - 1; i >= 0; i--)
                    if (m_Objects[i] != null) Object.DestroyImmediate(m_Objects[i]);
                m_Objects.Clear();
                if (m_Scene.IsValid()) EditorSceneManager.ClosePreviewScene(m_Scene);
                m_EnteredFixture = false;
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Validation_RejectsMissingOrWrongTarget(bool toTarget, bool wrongType)
        {
            var group = Create<CanvasGroup>();
            var item = Item(TweenType.CanvasGroupFade, group);
            var invalid = wrongType ? group.transform : null;
            if (toTarget) { item.UseToTarget = true; item.ToTarget = invalid; }
            else item.Target = invalid;
            var error = item.GetValidationError();
            Assert.That(error, Does.Contain(toTarget ? "ToTarget" : "Target"));
            Assert.That(error, Does.Contain(nameof(CanvasGroup)));
            var sequence = Sequence(item);
            Assert.That(sequence.Animation.GetValidationError(), Does.Contain("第 1 项"));
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.ResetToFromValue());
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.DOPlay());
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.DORewind());
        }

        [Test]
        public void Validation_AllowsUnusedMissingToTarget()
        {
            var item = Item(TweenType.Move, Create<Transform>());
            Assert.That(item.GetValidationError(), Is.Null);
            Assert.That(Sequence(item).Animation.GetValidationError(), Is.Null);
        }

        [TestCase("reset", false)]
        [TestCase("reset", true)]
        [TestCase("play", false)]
        [TestCase("play", true)]
        [TestCase("reverse", false)]
        [TestCase("reverse", true)]
        public void FromTarget_InvalidLaterReferenceRejectsWithoutSideEffects(string operation, bool wrongType)
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var group = Create<CanvasGroup>();
            group.alpha = .35f;
            var invalid = Item(TweenType.CanvasGroupFade, group);
            invalid.UseFromValue = false;
            invalid.UseFromTarget = true;
            invalid.FromTarget = wrongType ? group.transform : null;
            Assert.That(invalid.GetValidationError(), Does.Contain("FromTarget").And.Contain(nameof(CanvasGroup)));
            var first = Item(TweenType.LocalMove, target);
            var sequence = Sequence(first, invalid);
            var events = 0;
            first.OnPlay.AddListener(() => events++);
            invalid.OnPlay.AddListener(() => events++);
            ((UnityEvent)typeof(TweenAnimation).GetField("m_OnPlay", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(sequence.Animation)).AddListener(() => events++);
            Assert.That(sequence.Animation.GetValidationError(), Does.Contain("第 2 项"));
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (operation == "reset") sequence.Animation.ResetToFromValue();
                else if (operation == "play") sequence.Animation.DOPlay();
                else sequence.Animation.DORewind();
            });
            AssertVector(target.localPosition, new Vector3(7f, 8f, 9f));
            Assert.That(group.alpha, Is.EqualTo(.35f));
            Assert.That(events, Is.Zero);
            var motion = (MotionHandle)typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(sequence.Animation);
            Assert.That(motion.IsValid(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FromTarget_UnusedMissingReferenceAllowsResetAndPlay(bool useFromValue)
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var item = Item(TweenType.LocalMove, target);
            item.UseFromValue = useFromValue;
            item.UseFromTarget = false;
            item.FromTarget = null;
            var sequence = Sequence(item);
            Assert.That(sequence.Animation.GetValidationError(), Is.Null);
            sequence.Animation.ResetToFromValue();
            AssertVector(target.localPosition, useFromValue ? (Vector3)item.FromValue : new Vector3(7f, 8f, 9f));
            sequence.Animation.DOPlay().Complete();
            AssertVector(target.localPosition, (Vector3)item.ToValue);
        }

        [TestCase(TweenType.AnchorPosition, -1)]
        [TestCase(TweenType.AnchorPosition3D, -1)]
        [TestCase(TweenType.AnchorPositionX, 0)]
        [TestCase(TweenType.AnchorPositionY, 1)]
        [TestCase(TweenType.AnchorPositionZ, 2)]
        public void ReferenceEndpoints_AnchorCoordinatesUseAnimatedParentAndPreserveOtherAxes(TweenType type, int axis)
        {
            var target = CreateReferenceRect(out var from, out var to);
            var initial = new Vector3(13f, 17f, 19f);
            target.anchoredPosition3D = initial;
            var item = Item(type, target);
            item.UseFromValue = false;
            item.UseFromTarget = true;
            item.FromTarget = from;
            item.UseToTarget = true;
            item.ToTarget = to;
            var sequence = Sequence(item);
            var expectedFrom = AnchorReferenceValue(target, from);
            var expectedTo = AnchorReferenceValue(target, to);
            sequence.Animation.ResetToFromValue();
            AssertVector(target.anchoredPosition3D, ReferenceAxes(initial, expectedFrom, type, axis));
            var handle = sequence.Animation.DOPlay();
            handle.Time = .5;
            handle.Complete();
            AssertVector(target.anchoredPosition3D, ReferenceAxes(initial, expectedTo, type, axis));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReferenceEndpoints_SnapshotPerPlayAndResetPrefersFromTarget(bool useFromValue)
        {
            var target = CreateReferenceRect(out var from, out var to);
            var item = Item(TweenType.AnchorPosition3D, target);
            item.UseFromValue = useFromValue;
            item.UseFromTarget = true;
            item.FromTarget = from;
            item.UseToTarget = true;
            item.ToTarget = to;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            typeof(TweenAnimation).GetField("m_Ease", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sequence.Animation, Ease.Linear);
            var firstFrom = AnchorReferenceValue(target, from);
            var firstTo = AnchorReferenceValue(target, to);
            var handle = sequence.Animation.DOPlay();
            from.position += new Vector3(70f, -40f, 30f);
            to.position += new Vector3(-90f, 60f, -20f);
            handle.Time = .5;
            AssertVector(target.anchoredPosition3D, Vector3.Lerp(firstFrom, firstTo, .5f));
            handle.Complete();
            AssertVector(target.anchoredPosition3D, firstTo);
            var nextFrom = AnchorReferenceValue(target, from);
            var nextTo = AnchorReferenceValue(target, to);
            var replay = sequence.Animation.DOPlay();
            replay.Time = .5;
            AssertVector(target.anchoredPosition3D, Vector3.Lerp(nextFrom, nextTo, .5f));
            replay.Complete();
            AssertVector(target.anchoredPosition3D, nextTo);
            from.position += new Vector3(20f, 10f, -5f);
            sequence.Animation.ResetToFromValue();
            AssertVector(target.anchoredPosition3D, AnchorReferenceValue(target, from));
        }

        [TestCase(TweenType.LocalMove, -1)]
        [TestCase(TweenType.LocalMoveX, 0)]
        [TestCase(TweenType.LocalMoveY, 1)]
        [TestCase(TweenType.LocalMoveZ, 2)]
        public void ReferenceEndpoints_LocalMoveUsesMarkerWorldPositionInAnimatedParent(TweenType type, int axis)
        {
            var target = CreateReferenceRect(out var from, out var to);
            var initial = new Vector3(13f, 17f, 19f);
            target.localPosition = initial;
            var item = Item(type, target);
            item.UseFromTarget = true;
            item.FromTarget = from;
            item.UseToTarget = true;
            item.ToTarget = to;
            var expectedFrom = target.parent.InverseTransformPoint(from.position);
            var expectedTo = target.parent.InverseTransformPoint(to.position);
            var sequence = Sequence(item);
            sequence.Animation.ResetToFromValue();
            AssertVector(target.localPosition, ReferenceAxes(initial, expectedFrom, type, axis));
            sequence.Animation.DOPlay().Complete();
            AssertVector(target.localPosition, ReferenceAxes(initial, expectedTo, type, axis));
        }

        [TestCase(TweenType.Scale)]
        [TestCase(TweenType.SizeDelta)]
        [TestCase(TweenType.Color)]
        [TestCase(TweenType.Fade)]
        [TestCase(TweenType.CanvasGroupFade)]
        [TestCase(TweenType.FillAmount)]
        [TestCase(TweenType.LayoutPreferredSize)]
        [TestCase(TweenType.SliderValue)]
        public void FromTarget_ReadsSamePropertyAsToTarget(TweenType type)
        {
            var required = Animation.GetRequiredComponentType(type);
            if (required == typeof(Graphic)) required = typeof(Image);
            var target = Create(required);
            var from = Create(required);
            var to = Create(required);
            var fromItem = Item(type, from);
            var toItem = Item(type, to);
            fromItem.ApplyValue(new Vector4(.2f, .3f, .4f, .5f));
            toItem.ApplyValue(new Vector4(.6f, .7f, .8f, .9f));
            var fromValue = fromItem.CaptureCurrentValue();
            var toValue = toItem.CaptureCurrentValue();
            var item = Item(type, target);
            item.UseFromTarget = true;
            item.FromTarget = from;
            item.UseToTarget = true;
            item.ToTarget = to;
            var sequence = Sequence(item);
            sequence.Animation.ResetToFromValue();
            AssertVector(item.CaptureCurrentValue(), fromValue);
            sequence.Animation.DOPlay().Complete();
            AssertVector(item.CaptureCurrentValue(), toValue);
            AssertVector(fromItem.CaptureCurrentValue(), fromValue);
            AssertVector(toItem.CaptureCurrentValue(), toValue);
        }

        [TestCase(TweenType.Move)]
        [TestCase(TweenType.MoveX)]
        [TestCase(TweenType.MoveY)]
        [TestCase(TweenType.MoveZ)]
        [TestCase(TweenType.LocalMove)]
        [TestCase(TweenType.LocalMoveX)]
        [TestCase(TweenType.LocalMoveY)]
        [TestCase(TweenType.LocalMoveZ)]
        [TestCase(TweenType.Scale)]
        [TestCase(TweenType.ScaleX)]
        [TestCase(TweenType.ScaleY)]
        [TestCase(TweenType.ScaleZ)]
        [TestCase(TweenType.Rotation)]
        [TestCase(TweenType.LocalRotation)]
        [TestCase(TweenType.AnchorPosition)]
        [TestCase(TweenType.AnchorPositionX)]
        [TestCase(TweenType.AnchorPositionY)]
        [TestCase(TweenType.AnchorPositionZ)]
        [TestCase(TweenType.AnchorPosition3D)]
        [TestCase(TweenType.Color)]
        [TestCase(TweenType.Fade)]
        [TestCase(TweenType.CanvasGroupFade)]
        [TestCase(TweenType.FillAmount)]
        [TestCase(TweenType.LayoutFlexibleSize)]
        [TestCase(TweenType.LayoutMinSize)]
        [TestCase(TweenType.LayoutPreferredSize)]
        [TestCase(TweenType.SizeDelta)]
        [TestCase(TweenType.SliderValue)]
        public void ValueSources_AllPropertyTypesResolveDistinctEndpoints(TweenType type)
        {
            var currentValue = new Vector4(.1f, .2f, .3f, .4f);
            var startValue = new Vector4(.2f, .3f, .4f, .5f);
            var fromReferenceValue = new Vector4(.35f, .45f, .55f, .65f);
            var toReferenceValue = new Vector4(.65f, .75f, .85f, .95f);
            var endValue = new Vector4(.8f, .6f, .4f, .2f);
            var target = CreatePropertyComponent(type, out var fromReference, out var toReference);
            var targetSetup = Item(type, target);
            var fromSetup = Item(type, fromReference);
            var toSetup = Item(type, toReference);
            var item = Item(type, target);
            item.UseFromValue = false;
            item.UseFromTarget = false;
            item.UseToTarget = false;
            item.FromTarget = fromReference;
            item.ToTarget = toReference;
            item.FromValue = startValue;
            item.ToValue = endValue;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);

            var sourceCombinations = new[]
            {
                new Vector2Int(0, 0), // 当前起始值 + 指定结束值
                new Vector2Int(1, 0), // 指定起始值 + 指定结束值
                new Vector2Int(2, 0), // 引用起始值 + 指定结束值
                new Vector2Int(0, 2), // 当前起始值 + 引用结束值
                new Vector2Int(1, 2), // 指定起始值 + 引用结束值
                new Vector2Int(2, 2)  // 引用起始值 + 引用结束值
            };
            foreach (var combination in sourceCombinations)
            {
                var fromSource = combination.x;
                var toSource = combination.y;
                item.UseFromValue = fromSource == 1;
                item.UseFromTarget = fromSource == 2;
                item.UseToTarget = toSource == 2;
                sequence.Animation.DOKill();
                targetSetup.ApplyValue(currentValue);
                fromSetup.ApplyValue(fromReferenceValue);
                toSetup.ApplyValue(toReferenceValue);

                var expectedFrom = PropertyValue(type, fromSource == 0 ? currentValue
                    : fromSource == 1 ? startValue : fromReferenceValue);
                var expectedTo = PropertyValue(type, toSource == 2 ? toReferenceValue : endValue);
                var fromBefore = fromSetup.CaptureCurrentValue();
                var toBefore = toSetup.CaptureCurrentValue();
                AssertVector(item.CaptureCurrentValue(), PropertyValue(type, currentValue));
                AssertVector(item.CaptureEndpointValue(false), expectedFrom);
                AssertVector(item.CaptureEndpointValue(true), expectedTo);
                Assert.That(item.GetValidationError(), Is.Null, $"{type}, from={fromSource}, to={toSource}");
                Assert.That(sequence.Animation.GetValidationError(), Is.Null, $"{type}, from={fromSource}, to={toSource}");

                sequence.Animation.ResetToFromValue();
                AssertVector(item.CaptureCurrentValue(), expectedFrom);
                var motion = sequence.Animation.DOPlay();
                motion.Time = motion.TotalDuration * .5;
                Assert.That(Vector4.Distance(item.CaptureCurrentValue(), Vector4.Lerp(expectedFrom, expectedTo, .5f)), Is.LessThan(.001f),
                    $"{type}, from={fromSource}, to={toSource} 中途采样错误。");
                motion.Complete();
                AssertVector(item.CaptureCurrentValue(), expectedTo);
                AssertVector(fromSetup.CaptureCurrentValue(), fromBefore);
                AssertVector(toSetup.CaptureCurrentValue(), toBefore);
            }

            Component wrongReference;
            if (TweenAnimation.GetRequiredComponentType(type) == typeof(Transform))
                wrongReference = Create<CanvasGroup>();
            else
                wrongReference = Create<Transform>();
            item.UseFromValue = true;
            item.UseFromTarget = false;
            item.UseToTarget = false;
            item.FromTarget = wrongReference;
            item.ToTarget = wrongReference;
            Assert.That(item.GetValidationError(), Is.Null, $"{type} 的未启用引用不应影响校验。");
            sequence.Animation.ResetToFromValue();
            AssertVector(item.CaptureCurrentValue(), PropertyValue(type, startValue));
            sequence.Animation.DOPlay().Complete();
            AssertVector(item.CaptureCurrentValue(), PropertyValue(type, endValue));
        }

        [Test]
        public void Relative_AllSupportedTypesAndSourcesResolveOffsetAndReverse()
        {
            foreach (TweenType type in Enum.GetValues(typeof(TweenType)))
            {
                if (!Animation.SupportsRelative(type)) continue;
                var target = CreatePropertyComponent(type, out var from, out var to);
                var item = Item(type, target);
                item.Relative = true;
                item.Ease = Ease.Linear;
                item.FromValue = new Vector4(10, 20, 30, 0);
                item.ToValue = new Vector4(2, 3, 4, 0);
                item.FromTarget = from;
                item.ToTarget = to;
                Item(type, from).ApplyValue(new Vector4(15, 25, 35, 0));
                Item(type, to).ApplyValue(new Vector4(50, 60, 70, 0));
                var sequence = Sequence(item);
                foreach (var source in new[] { 0, 1, 2 })
                    foreach (var referenceEnd in new[] { false, true })
                    {
                        sequence.Animation.DOKill();
                        Item(type, target).ApplyValue(new Vector4(5, 6, 7, 0));
                        item.UseFromValue = source == 1;
                        item.UseFromTarget = source == 2;
                        item.UseToTarget = referenceEnd;
                        var expectedFrom = PropertyValue(type, source == 0 ? new Vector4(5, 6, 7, 0)
                            : source == 1 ? item.FromValue : new Vector4(15, 25, 35, 0));
                        var expectedTo = referenceEnd ? PropertyValue(type, new Vector4(50, 60, 70, 0))
                            : expectedFrom + PropertyValue(type, item.ToValue);
                        AssertVector(item.CaptureEndpointValue(true), expectedTo);
                        var motion = sequence.Animation.DOPlay();
                        motion.Time = .5;
                        AssertVector(item.CaptureCurrentValue(), (expectedFrom + expectedTo) * .5f);
                        motion.Time = 1;
                        AssertVector(item.CaptureCurrentValue(), expectedTo);
                        sequence.Animation.DOKill();
                        // 当前值起点必须恢复，否则新播放按完成后的属性重新采样。
                        Item(type, target).ApplyValue(expectedFrom);
                        motion = sequence.Animation.DORewind();
                        motion.Time = 1;
                        AssertVector(item.CaptureCurrentValue(), expectedFrom);
                        sequence.Animation.DOKill();
                    }
            }
        }

        [Test]
        public void Relative_ReplayResamplesButDirectionSwitchKeepsEndpointsAndSpeedDuration()
        {
            var target = Create<Transform>();
            target.position = new Vector3(10, 8, 9);
            var item = Item(TweenType.MoveX, target);
            item.Relative = item.SpeedBased = true;
            item.UseFromValue = false;
            item.ToValue = new Vector4(6, 0, 0, 0);
            item.DurationOrSpeed = 3;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            Assert.That(sequence.Animation.CaptureTimeline().Duration, Is.EqualTo(2));
            var motion = sequence.Animation.PlayForward();
            motion.Time = 1;
            Assert.That(target.position.x, Is.EqualTo(13).Within(.001));
            Assert.That(sequence.Animation.PlayBackward(), Is.EqualTo(motion));
            motion.Time = 0;
            Assert.That(target.position.x, Is.EqualTo(10).Within(.001));
            sequence.Animation.DOKill();
            target.position = new Vector3(20, 8, 9);
            sequence.Animation.DOPlay().Complete();
            AssertVector(target.position, new Vector3(26, 8, 9));
            item.ToValue = new Vector4(float.MaxValue, 0, 0, 0);
            item.UseFromValue = true;
            item.FromValue = item.ToValue;
            Assert.That(item.GetValidationError(), Is.Not.Null);
        }

        [Test]
        public void Relative_UnsupportedAndReferenceEndpointsPreserveInactiveFlag()
        {
            var item = Item(TweenType.CanvasGroupFade, Create<CanvasGroup>());
            item.Relative = true;
            Assert.That(item.UsesRelativeEnd, Is.False);
            AssertVector(item.CaptureEndpointValue(true), new Vector4(1, 0, 0, 0));
            var sequence = Sequence(item);
            var original = JsonUtility.ToJson(sequence.Animation);
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                Assert.That((bool)InvokeDrawer("ShowRelative", property), Is.False);
                Assert.That(((Animation)InvokeDrawer("ReadAnimation", property, null)).Relative, Is.True);
                Assert.That(data.ApplyModifiedProperties(), Is.False);
            }
            Assert.That(JsonUtility.ToJson(sequence.Animation), Is.EqualTo(original));
            Assert.That(JsonUtility.FromJson<Animation>("{}").Relative, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Relative_EndpointHandleStoresOffsetAndPreviewRestores(bool currentStart)
        {
            var target = Create<Transform>();
            target.position = new Vector3(10, 20, 30);
            target.gameObject.SetActive(true);
            var item = Item(TweenType.Move, target);
            item.Relative = true;
            item.UseFromValue = !currentStart;
            item.FromValue = target.position;
            item.ToValue = new Vector4(2, 3, 4, 0);
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", true, true);
            AssertVector(target.position, new Vector3(12, 23, 34));
            InvokePreview("MoveEndpoint", new Vector3(15, 26, 37));
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                AssertVector(property.FindPropertyRelative("ToValue").vector4Value, new Vector4(5, 6, 7, 0));
                Assert.That((bool)InvokeDrawer("CanSwapEndpoints", property), Is.False);
            }
            AssertVector(target.position, new Vector3(15, 26, 37));
            InvokePreview("Stop");
            AssertVector(target.position, new Vector3(10, 20, 30));
            InvokePreview("Start", new[] { sequence }, false);
            SeekPreview(sequence, 1);
            AssertVector(target.position, new Vector3(15, 26, 37));
            InvokePreview("Stop");
            AssertVector(target.position, new Vector3(10, 20, 30));
        }

        [TestCase(TweenType.MoveX)]
        [TestCase(TweenType.Move)]
        [TestCase(TweenType.LocalMove)]
        [TestCase(TweenType.AnchorPosition)]
        [TestCase(TweenType.AnchorPosition3D)]
        public void ReviewFix_RelativeStartHandleUpdatesEndMarkerAndPreservesOffset(TweenType type)
        {
            var target = type == TweenType.AnchorPosition || type == TweenType.AnchorPosition3D
                ? (Transform)Create<RectTransform>() : Create<Transform>();
            target.position = new Vector3(10, 20, 30);
            target.gameObject.SetActive(true);
            var original = target.position;
            var item = Item(type, target);
            item.Relative = true;
            item.FromValue = type == TweenType.MoveX ? new Vector4(10, 0, 0, 0) : new Vector4(10, 20, 30, 0);
            item.ToValue = new Vector4(2, 3, 4, 0);
            var offset = item.ToValue;
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            const string path = "m_Animation.m_Sequence.Array.data[0]";
            InvokePreview("StartEndpoint", sequence, path, false, true);
            foreach (var world in new[] { new Vector3(20, 30, 40), new Vector3(25, 35, 45) })
            {
                InvokePreview("MoveEndpoint", world);
                using (var data = new SerializedObject(sequence))
                {
                    var property = data.FindProperty(path);
                    var stored = (Animation)InvokeDrawer("ReadAnimation", property, null);
                    AssertVector(stored.ToValue, offset);
                    var cached = (Vector4)m_PreviewType.GetField("m_ToEndpoint", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    AssertVector(cached, stored.CaptureEndpointValue(true));
                    Assert.That(cached.x, Is.EqualTo(world.x + offset.x));
                }
            }
            InvokePreview("Stop");
            AssertVector(target.position, original);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReviewFix_ReadCurrentRejectsInvalidRelativeStartReference(bool wrongType)
        {
            var target = Create<RectTransform>();
            target.anchoredPosition = new Vector2(10, 20);
            var item = Item(TweenType.AnchorPosition, target);
            item.Relative = item.UseFromTarget = true;
            item.FromTarget = wrongType ? Create<Transform>() : null;
            var args = new object[] { item, true, Vector4.zero };
            Assert.That((bool)InvokeDrawer("TryCaptureCurrentValue", args), Is.False);
            args[1] = false;
            Assert.That((bool)InvokeDrawer("TryCaptureCurrentValue", args), Is.True, "读取起始当前值不依赖起始引用。");
            item.Relative = false;
            args[1] = true;
            Assert.That((bool)InvokeDrawer("TryCaptureCurrentValue", args), Is.True, "绝对结束值不依赖起始引用。");
            item.Relative = true;
            var reference = Create<RectTransform>();
            reference.anchoredPosition = new Vector2(3, 4);
            item.FromTarget = reference;
            Assert.That((bool)InvokeDrawer("TryCaptureCurrentValue", args), Is.True);
            AssertVector((Vector4)args[2], new Vector4(7, 16, 0, 0));
            AssertVector(target.anchoredPosition, new Vector2(10, 20));
        }

        [TestCase("FromValue", 0)]
        [TestCase("FromValue", 1)]
        [TestCase("FromValue", 2)]
        [TestCase("ToValue", 0)]
        [TestCase("ToValue", 1)]
        [TestCase("ToValue", 2)]
        public void ReviewFix_VectorComponentEditPreservesOtherObjectsAndSupportsUndo(string field, int axis)
        {
            var first = Sequence(Item(TweenType.Move, Create<Transform>()));
            var second = Sequence(Item(TweenType.Move, Create<Transform>()));
            var a = new Vector4(1, 2, 3, 7);
            var b = new Vector4(4, 5, 6, 8);
            var path = "m_Animation.m_Sequence.Array.data[0]." + field;
            using (var data = new SerializedObject(first))
            {
                data.FindProperty(path).vector4Value = a;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var data = new SerializedObject(second))
            {
                data.FindProperty(path).vector4Value = b;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Undo.IncrementCurrentGroup();
            using (var data = new SerializedObject(new Object[] { first, second }))
            {
                InvokeDrawer("SetValueComponent", data.FindProperty(path), axis, 10f);
                Assert.That(data.ApplyModifiedProperties(), Is.True);
            }
            Undo.FlushUndoRecordObjects();
            var expectedA = a; expectedA[axis] = 10;
            var expectedB = b; expectedB[axis] = 10;
            using (var data = new SerializedObject(first)) AssertVector(data.FindProperty(path).vector4Value, expectedA);
            using (var data = new SerializedObject(second)) AssertVector(data.FindProperty(path).vector4Value, expectedB);
            Undo.PerformUndo();
            using (var data = new SerializedObject(first)) AssertVector(data.FindProperty(path).vector4Value, a);
            using (var data = new SerializedObject(second)) AssertVector(data.FindProperty(path).vector4Value, b);
        }

        [TestCase(TweenAnimation.LoopType.Restart, 12f)]
        [TestCase(TweenAnimation.LoopType.Yoyo, 10f)]
        [TestCase(TweenAnimation.LoopType.Incremental, 14f)]
        public void Relative_ItemLoopsUseResolvedEndpoints(TweenAnimation.LoopType loop, float expected)
        {
            var target = Create<Transform>();
            var item = Item(TweenType.MoveX, target);
            item.Relative = true;
            item.FromValue = new Vector4(10, 0, 0, 0);
            item.ToValue = new Vector4(2, 0, 0, 0);
            item.Loops = 2;
            item.LoopType = loop;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            sequence.Animation.DOPlay().Complete();
            Assert.That(target.position.x, Is.EqualTo(expected).Within(.001));
        }

        private static Vector4 PropertyValue(TweenType type, Vector4 value)
        {
            switch (type)
            {
                case TweenType.Color:
                    return value;
                case TweenType.Move:
                case TweenType.LocalMove:
                case TweenType.Scale:
                case TweenType.Rotation:
                case TweenType.LocalRotation:
                case TweenType.AnchorPosition3D:
                    return new Vector4(value.x, value.y, value.z, 0);
                case TweenType.AnchorPosition:
                case TweenType.SizeDelta:
                case TweenType.LayoutFlexibleSize:
                case TweenType.LayoutMinSize:
                case TweenType.LayoutPreferredSize:
                    return new Vector4(value.x, value.y, 0, 0);
                default:
                    return new Vector4(value.x, 0, 0, 0);
            }
        }

        [Test]
        public void Preview_ReferenceEndpointsRestoreTargetWithoutChangingMarkersOrRaisingEvents()
        {
            var target = CreateReferenceRect(out var from, out var to);
            var original = target.anchoredPosition3D;
            var fromPosition = from.localPosition;
            var toPosition = to.localPosition;
            var fromRotation = from.localRotation;
            var toRotation = to.localRotation;
            var fromScale = from.localScale;
            var toScale = to.localScale;
            var item = Item(TweenType.AnchorPosition3D, target);
            item.UseFromValue = false;
            item.UseFromTarget = true;
            item.FromTarget = from;
            item.UseToTarget = true;
            item.ToTarget = to;
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            var events = 0;
            item.OnPlay.AddListener(() => events++);
            item.OnUpdate.AddListener(() => events++);
            item.OnComplete.AddListener(() => events++);
            foreach (var name in new[] { "m_OnPlay", "m_OnUpdate", "m_OnComplete" })
                ((UnityEvent)typeof(TweenAnimation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation))
                    .AddListener(() => events++);
            StartPreview(new[] { sequence });
            SeekPreview(sequence, .5);
            Assert.That(Vector3.Distance(target.anchoredPosition3D, original), Is.GreaterThan(.1f));
            InvokePreview("Stop");
            AssertVector(target.anchoredPosition3D, original);
            AssertVector(from.localPosition, fromPosition);
            AssertVector(to.localPosition, toPosition);
            Assert.That(from.localRotation, Is.EqualTo(fromRotation));
            Assert.That(to.localRotation, Is.EqualTo(toRotation));
            AssertVector(from.localScale, fromScale);
            AssertVector(to.localScale, toScale);
            Assert.That(events, Is.Zero);
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Drawer_EndpointLayoutPreservesConfiguration(bool fromReference, bool toReference)
        {
            var target = CreateReferenceRect(out var from, out var to);
            var item = Item(TweenType.AnchorPosition, target);
            item.UseFromTarget = fromReference;
            item.FromTarget = from;
            item.UseToTarget = toReference;
            item.ToTarget = to;
            var sequence = Sequence(item);
            var serialized = new SerializedObject(sequence);
            var property = serialized.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
            var drawerType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
            var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType, true);
            property.isExpanded = false;
            var collapsed = drawer.GetPropertyHeight(property, GUIContent.none);
            property.isExpanded = true;
            property.FindPropertyRelative("SpeedBased").isExpanded = false;
            property.FindPropertyRelative("CustomEase").isExpanded = false;
            var expected = (EditorGUIUtility.singleLineHeight + 3f) * (12 + (fromReference ? 2 : 3) + (toReference ? 2 : 3)) + 8f;
            Assert.That(collapsed, Is.EqualTo(EditorGUIUtility.singleLineHeight + 11f));
            Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.EqualTo(expected));
            var read = (Animation)drawerType.GetMethod("ReadAnimation", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { property, null });
            Assert.That(read.UseFromTarget, Is.EqualTo(fromReference));
            Assert.That(read.UseToTarget, Is.EqualTo(toReference));
            Assert.That(read.FromTarget, Is.EqualTo(from));
            Assert.That(read.ToTarget, Is.EqualTo(to));
            Assert.That(read.UseFromValue, Is.True);
            AssertVector(read.FromValue, item.FromValue);
            AssertVector(read.ToValue, item.ToValue);
            Assert.That(serialized.hasModifiedProperties, Is.False);
        }

        [TestCase(TweenType.Color, typeof(Image))]
        [TestCase(TweenType.Fade, typeof(RawImage))]
        [TestCase(TweenType.FillAmount, typeof(Image))]
        [TestCase(TweenType.CanvasGroupFade, typeof(CanvasGroup))]
        [TestCase(TweenType.LayoutPreferredSize, typeof(LayoutElement))]
        [TestCase(TweenType.SliderValue, typeof(Slider))]
        public void Drawer_MatchesRequiredComponentOnSameObject(TweenType type, Type componentType)
        {
            var rect = Create<RectTransform>();
            var expected = rect.gameObject.AddComponent(componentType);
            Assert.That(MatchDrawerComponent(rect, type), Is.SameAs(expected));
            Assert.That(MatchDrawerComponent(expected, type), Is.SameAs(expected));
            Assert.That(MatchDrawerComponent(expected, TweenType.AnchorPosition), Is.SameAs(rect));
            Assert.That(MatchDrawerComponent(expected, TweenType.Move), Is.SameAs(rect));
        }

        [Test]
        public void Drawer_MissingComponentKeepsReferenceWithoutSearchingChildrenOrAdding()
        {
            var parent = Create<RectTransform>();
            var child = Create<Image>();
            child.transform.SetParent(parent, false);
            var count = parent.GetComponents<Component>().Length;
            Assert.That(MatchDrawerComponent(parent, TweenType.Color), Is.SameAs(parent));
            Assert.That(parent.GetComponent<Graphic>(), Is.Null);
            Assert.That(parent.GetComponents<Component>().Length, Is.EqualTo(count));
            Assert.That(Item(TweenType.Color, parent).GetValidationError(), Does.Contain("Graphic"));
            Assert.That(MatchDrawerComponent(null, TweenType.Color), Is.Null);
            Assert.That(MatchDrawerComponent(parent, (TweenType)int.MaxValue), Is.SameAs(parent));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Drawer_TypeChangeMatchesAllReferencesAndSupportsUndo(bool multiple)
        {
            var targets = new List<Object>();
            var originals = new List<Animation>();
            for (var i = 0; i < (multiple ? 2 : 1); i++)
            {
                var target = Create<Image>();
                var from = Create<Image>();
                var to = Create<Image>();
                var item = Item(TweenType.AnchorPosition, target.transform);
                item.FromTarget = from.transform;
                item.ToTarget = to.transform;
                item.UseFromTarget = false;
                item.UseToTarget = true;
                originals.Add(item);
                targets.Add(Sequence(item));
            }
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            using (var serialized = new SerializedObject(targets.ToArray()))
            {
                var property = serialized.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                property.FindPropertyRelative("AnimationType").intValue = (int)TweenType.Color;
                var drawerType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
                drawerType.GetMethod("MatchReferences", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { property });
                serialized.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            for (var i = 0; i < targets.Count; i++)
            {
                using (var serialized = new SerializedObject(targets[i]))
                {
                    var property = serialized.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                    Assert.That(property.FindPropertyRelative("AnimationType").intValue, Is.EqualTo((int)TweenType.Color));
                    Assert.That(property.FindPropertyRelative("Target").objectReferenceValue,
                        Is.SameAs(originals[i].Target.GetComponent<Image>()));
                    Assert.That(property.FindPropertyRelative("FromTarget").objectReferenceValue,
                        Is.SameAs(originals[i].FromTarget.GetComponent<Image>()));
                    Assert.That(property.FindPropertyRelative("ToTarget").objectReferenceValue,
                        Is.SameAs(originals[i].ToTarget.GetComponent<Image>()));
                    Assert.That(property.FindPropertyRelative("UseFromTarget").boolValue, Is.False);
                    Assert.That(property.FindPropertyRelative("UseToTarget").boolValue, Is.True);
                    AssertVector(property.FindPropertyRelative("FromValue").vector4Value, originals[i].FromValue);
                    AssertVector(property.FindPropertyRelative("ToValue").vector4Value, originals[i].ToValue);
                }
            }
            Undo.PerformUndo();
            for (var i = 0; i < targets.Count; i++)
            {
                using (var serialized = new SerializedObject(targets[i]))
                {
                    var property = serialized.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                    Assert.That(property.FindPropertyRelative("AnimationType").intValue, Is.EqualTo((int)TweenType.AnchorPosition));
                    Assert.That(property.FindPropertyRelative("Target").objectReferenceValue, Is.TypeOf<RectTransform>());
                    Assert.That(property.FindPropertyRelative("FromTarget").objectReferenceValue, Is.TypeOf<RectTransform>());
                    Assert.That(property.FindPropertyRelative("ToTarget").objectReferenceValue, Is.TypeOf<RectTransform>());
                }
            }
            foreach (var target in targets) Undo.ClearUndo(target);
            Undo.IncrementCurrentGroup();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Drawer_SourceModesPreserveInactiveDataAndSupportUndo(bool multiple)
        {
            var objects = new List<Object>();
            for (var i = 0; i < (multiple ? 2 : 1); i++)
            {
                var item = Item(TweenType.Move, Create<Transform>());
                item.FromTarget = Create<Transform>();
                item.ToTarget = Create<Transform>();
                item.UseFromTarget = i == 1;
                objects.Add(Sequence(item));
            }
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            using (var data = new SerializedObject(objects.ToArray()))
            {
                var item = data.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                Assert.That((int)InvokeDrawer("GetEndpointMode", item, false), Is.EqualTo(multiple ? -1 : 1));
                var fromValue = item.FindPropertyRelative("FromValue").vector4Value;
                var toValue = item.FindPropertyRelative("ToValue").vector4Value;
                var fromTarget = item.FindPropertyRelative("FromTarget").objectReferenceValue;
                var toTarget = item.FindPropertyRelative("ToTarget").objectReferenceValue;
                foreach (var mode in new[] { 2, 0, 1, 2 })
                {
                    InvokeDrawer("SetEndpointMode", item, false, mode);
                    Assert.That((int)InvokeDrawer("GetEndpointMode", item, false), Is.EqualTo(mode));
                }
                foreach (var mode in new[] { 2, 1, 2 })
                {
                    InvokeDrawer("SetEndpointMode", item, true, mode);
                    Assert.That((int)InvokeDrawer("GetEndpointMode", item, true), Is.EqualTo(mode));
                }
                AssertVector(item.FindPropertyRelative("FromValue").vector4Value, fromValue);
                AssertVector(item.FindPropertyRelative("ToValue").vector4Value, toValue);
                Assert.That(item.FindPropertyRelative("FromTarget").objectReferenceValue, Is.EqualTo(fromTarget));
                Assert.That(item.FindPropertyRelative("ToTarget").objectReferenceValue, Is.EqualTo(toTarget));
                Assert.That(item.FindPropertyRelative("FromTarget").hasMultipleDifferentValues, Is.EqualTo(multiple));
                data.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            Undo.PerformUndo();
            for (var i = 0; i < objects.Count; i++)
                using (var data = new SerializedObject(objects[i]))
                {
                    var item = data.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                    Assert.That(item.FindPropertyRelative("UseFromTarget").boolValue, Is.EqualTo(i == 1));
                    Assert.That(item.FindPropertyRelative("UseToTarget").boolValue, Is.False);
                }
            foreach (var obj in objects) Undo.ClearUndo(obj);
            Undo.IncrementCurrentGroup();
        }

        [Test]
        public void Drawer_CategorizedTypesCoverEveryExistingValue()
        {
            var paths = new HashSet<string>();
            foreach (TweenType type in Enum.GetValues(typeof(TweenType)))
            {
                var path = (string)InvokeDrawer("GetTypePath", type);
                Assert.That(path, Does.Contain("/").And.Not.Contain("未知").And.Not.StartWith("全部/"));
                Assert.That(paths.Add(path), Is.True, path);
            }
            Assert.That((bool)InvokeDrawer("SupportsSnapping", TweenType.AnchorPosition), Is.True);
            Assert.That((bool)InvokeDrawer("SupportsSnapping", TweenType.Color), Is.False);
            Assert.That((bool)InvokeDrawer("SupportsSnapping", TweenType.Scale), Is.False);
        }

        [Test]
        public void Drawer_WaitHidesAnimationOnlyFieldsAndUnsupportedSnapping()
        {
            Assert.That((bool)InvokeDrawer("SupportsAnimationFields", TweenType.Wait), Is.False);
            Assert.That((bool)InvokeDrawer("SupportsAnimationFields", TweenType.Move), Is.True);

            var wait = Item(TweenType.Wait, null);
            wait.Snapping = true;
            using (var data = new SerializedObject(Sequence(wait)))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                var drawer = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
                var summary = (string)drawer.GetMethod("AdvancedSummary", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { property });
                Assert.That(summary, Does.Not.Contain("取整"));
            }

            var move = Item(TweenType.Move, Create<Transform>());
            move.Snapping = true;
            using (var data = new SerializedObject(Sequence(move)))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                var drawer = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
                var summary = (string)drawer.GetMethod("AdvancedSummary", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { property });
                Assert.That(summary, Does.Contain("取整"));
            }
        }

        [TestCase(TweenAnimation.AddType.Append, 5)]
        [TestCase(TweenAnimation.AddType.Join, 5)]
        [TestCase(TweenAnimation.AddType.Insert, 6)]
        public void Drawer_WaitHeightIgnoresHiddenParametersAndPreservesSerializedData(TweenAnimation.AddType addType, int rows)
        {
            var wait = Item(TweenType.Wait, Create<Transform>());
            wait.AddType = addType;
            wait.InsertTime = .2f;
            wait.UseFromTarget = wait.UseToTarget = true;
            wait.SpeedBased = wait.CustomEase = wait.Snapping = true;
            wait.Delay = 50f;
            wait.Loops = 99;
            var sequence = Sequence(wait);
            var original = JsonUtility.ToJson(sequence.Animation);
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                var drawerType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
                var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType, true);
                property.isExpanded = true;
                foreach (var expanded in new[] { false, true })
                {
                    property.FindPropertyRelative("SpeedBased").isExpanded = expanded;
                    property.FindPropertyRelative("CustomEase").isExpanded = expanded;
                    Assert.That(drawer.GetPropertyHeight(property, GUIContent.none),
                        Is.EqualTo((EditorGUIUtility.singleLineHeight + 3f) * rows + 8f));
                }
                Assert.That(data.ApplyModifiedProperties(), Is.False);
            }
            Assert.That(JsonUtility.ToJson(sequence.Animation), Is.EqualTo(original));
        }

        [Test]
        public void Inspector_DefaultsSecondarySectionsCollapsedWithoutChangingSerializedData()
        {
            var host = Create<TweenAnimationTestHost>();
            host.First.SetAnimations(new[] { Item(TweenType.Move, Create<Transform>()) });
            using (var data = new SerializedObject(host))
            {
                var before = JsonUtility.ToJson(host.First);
                var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
                var window = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
                var panel = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { data, "First", (Action)window.Repaint }, null);
                try
                {
                    Assert.That((bool)type.GetField("m_ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel), Is.False);
                    Assert.That((bool)type.GetField("m_ShowAdvanced", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel), Is.False);
                    Assert.That((bool)type.GetField("m_ShowEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel), Is.False);
                    Assert.That((bool)type.GetField("m_ShowLifecycle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel), Is.False);
                    var measure = type.GetMethod("GetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var width in new[] { 280f, 550f })
                    {
                        var collapsed = (float)measure.Invoke(panel, new object[] { width });
                        var settings = type.GetField("m_ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                        settings.SetValue(panel, true);
                        Assert.That((float)measure.Invoke(panel, new object[] { width }), Is.GreaterThan(collapsed));
                        settings.SetValue(panel, false);
                    }
                    Assert.That(data.ApplyModifiedProperties(), Is.False);
                    Assert.That(JsonUtility.ToJson(host.First), Is.EqualTo(before));
                }
                finally
                {
                    type.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                    Object.DestroyImmediate(window);
                }
            }
        }

        [TestCase(TweenType.CanvasGroupFade, "UI/CanvasGroup 透明度")]
        [TestCase(TweenType.SliderValue, "UI/Slider 数值")]
        [TestCase(TweenType.LayoutFlexibleSize, "UI/布局尺寸 (Layout)/Flexible")]
        [TestCase(TweenType.LayoutMinSize, "UI/布局尺寸 (Layout)/Min")]
        [TestCase(TweenType.LayoutPreferredSize, "UI/布局尺寸 (Layout)/Preferred")]
        [TestCase(TweenType.Move, "位置 (Move)/世界/XYZ")]
        [TestCase(TweenType.Scale, "缩放 (Scale)/XYZ")]
        public void Drawer_TypeLabelsAvoidRedundantEnglish(TweenType type, string expected)
        {
            Assert.That((string)InvokeDrawer("GetTypePath", type), Is.EqualTo(expected));
        }

        [TestCase("OnPlay", "开始时 (OnPlay)")]
        [TestCase("OnUpdate", "更新时 (OnUpdate)")]
        [TestCase("OnComplete", "完成时 (OnComplete)")]
        public void Drawer_EventLabelsIncludeApiName(string name, string expected)
        {
            Assert.That((string)InvokeDrawer("EventLabel", name), Is.EqualTo(expected));
        }

        [TestCase(false, "起始值", "起始值来源", "起始值引用")]
        [TestCase(true, "结束值", "结束值来源", "结束值引用")]
        public void Drawer_ValueLabelsSeparateValuesSourcesAndReferences(bool to, string value, string source, string reference)
        {
            var valueLabel = (GUIContent)InvokeDrawer("ValueLabel", to);
            var sourceLabel = (GUIContent)InvokeDrawer("SourceLabel", to);
            var referenceLabel = (GUIContent)InvokeDrawer("ReferenceLabel", to);
            Assert.That(valueLabel.text, Is.EqualTo(value));
            Assert.That(sourceLabel.text, Is.EqualTo(source));
            Assert.That(referenceLabel.text, Is.EqualTo(reference));
            Assert.That(valueLabel.tooltip, Is.Empty);
            Assert.That(sourceLabel.tooltip, Is.Empty);
            Assert.That(referenceLabel.tooltip, Is.Empty);
        }

        [TestCase(TweenType.Move)]
        [TestCase(TweenType.Scale)]
        [TestCase(TweenType.CanvasGroupFade)]
        [TestCase(TweenType.Color)]
        [TestCase(TweenType.SliderValue)]
        public void Drawer_GenericValueLabelsDoNotChangeSerializedSourcesOrValues(TweenType type)
        {
            var target = Create(type == TweenType.Color ? typeof(Image) : TweenAnimation.GetRequiredComponentType(type));
            var sequence = Sequence(Item(type, target));
            var original = JsonUtility.ToJson(sequence.Animation);
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                var drawerType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
                var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType, true);
                property.isExpanded = true;
                Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.GreaterThan(0));
                foreach (var to in new[] { false, true })
                {
                    Assert.That((int)InvokeDrawer("GetEndpointMode", property, to), Is.EqualTo(1));
                    Assert.That(((GUIContent)InvokeDrawer("ValueLabel", to)).text, Does.Not.Contain("点"));
                }
            }
            Assert.That(JsonUtility.ToJson(sequence.Animation), Is.EqualTo(original));
        }

        [Test]
        public void Drawer_EaseSelectionKeepsCurveAndPreviousBuiltInValue()
        {
            var sequence = Sequence(Item(TweenType.Move, Create<Transform>()));
            using (var data = new SerializedObject(sequence))
            {
                var item = data.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                var curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                item.FindPropertyRelative("EaseCurve").animationCurveValue = curve;
                item.FindPropertyRelative("Ease").intValue = (int)Ease.OutBack;
                InvokeDrawer("SetEaseSelection", item, Ease.CustomAnimationCurve);
                Assert.That(item.FindPropertyRelative("CustomEase").boolValue, Is.True);
                Assert.That(item.FindPropertyRelative("Ease").intValue, Is.EqualTo((int)Ease.OutBack));
                InvokeDrawer("SetEaseSelection", item, Ease.Linear);
                Assert.That(item.FindPropertyRelative("CustomEase").boolValue, Is.False);
                Assert.That(item.FindPropertyRelative("Ease").intValue, Is.EqualTo((int)Ease.Linear));
                Assert.That(item.FindPropertyRelative("EaseCurve").animationCurveValue.keys, Is.EqualTo(curve.keys));
            }
        }

        [Test]
        public void EasePicker_AllBuiltInThumbnailsMatchBackendIncludingOvershoot()
        {
            foreach (Ease ease in Enum.GetValues(typeof(Ease)))
            {
                Assert.That((string)InvokeDrawer("GetEaseCategory", ease), Is.Not.EqualTo("未知"));
                Assert.That((string)InvokeDrawer("GetEaseLabel", ease), Does.EndWith($"({ease})"));
                if (ease == Ease.CustomAnimationCurve) continue;
                var points = (Vector3[])InvokeDrawer("GetEaseSamples", ease);
                Assert.That(points.Length, Is.EqualTo(65));
                for (var i = 0; i < points.Length; i++)
                {
                    Assert.That(points[i].x, Is.EqualTo(i / 64f));
                    Assert.That(points[i].y, Is.EqualTo(EaseUtility.Evaluate(points[i].x, ease)).Within(.00001f));
                    Assert.That(float.IsNaN(points[i].y) || float.IsInfinity(points[i].y), Is.False);
                }
            }
            Assert.That(Array.Exists((Vector3[])InvokeDrawer("GetEaseSamples", Ease.OutBack), p => p.y > 1), Is.True);
            Assert.That((bool)InvokeDrawer("IsCommonEase", Ease.OutQuad), Is.True);
            Assert.That((bool)InvokeDrawer("IsCommonEase", Ease.InQuint), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EasePicker_AppliesToMultipleObjectsWithoutChangingReferencesAndSupportsUndo(bool global)
        {
            var firstItem = Item(TweenType.Move, Create<Transform>());
            var secondItem = Item(TweenType.Move, Create<Transform>());
            var first = Sequence(firstItem);
            var second = Sequence(secondItem);
            var objects = new UnityEngine.Object[] { first, second };
            var path = global ? "m_Animation.m_Ease" : "m_Animation.m_Sequence.Array.data[0]";
            var original = new Ease[2];
            for (var i = 0; i < objects.Length; i++)
                using (var data = new SerializedObject(objects[i]))
                    original[i] = (Ease)(global ? data.FindProperty(path) : data.FindProperty(path).FindPropertyRelative("Ease")).intValue;
            Undo.IncrementCurrentGroup();
            InvokeDrawer("ApplyEaseSelection", objects, path, Ease.OutBounce, !global);
            Undo.FlushUndoRecordObjects();
            for (var i = 0; i < objects.Length; i++)
                using (var data = new SerializedObject(objects[i]))
                {
                    var property = data.FindProperty(path);
                    Assert.That((global ? property : property.FindPropertyRelative("Ease")).intValue, Is.EqualTo((int)Ease.OutBounce));
                    Assert.That(data.FindProperty("m_Animation.m_Sequence.Array.data[0].Target").objectReferenceValue,
                        Is.EqualTo(i == 0 ? firstItem.Target : secondItem.Target));
                }
            Undo.PerformUndo();
            for (var i = 0; i < objects.Length; i++)
                using (var data = new SerializedObject(objects[i]))
                    Assert.That((global ? data.FindProperty(path) : data.FindProperty(path).FindPropertyRelative("Ease")).intValue,
                        Is.EqualTo((int)original[i]));
        }

        [Test]
        public void EasePicker_CustomSelectionKeepsEachObjectsCurveAndPreviousEase()
        {
            var firstItem = Item(TweenType.Move, Create<Transform>());
            firstItem.Ease = Ease.OutBack;
            firstItem.EaseCurve = AnimationCurve.Linear(0, 0, 1, 1);
            var secondItem = Item(TweenType.Move, Create<Transform>());
            secondItem.Ease = Ease.InQuad;
            secondItem.EaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            var objects = new UnityEngine.Object[] { Sequence(firstItem), Sequence(secondItem) };
            InvokeDrawer("ApplyEaseSelection", objects, "m_Animation.m_Sequence.Array.data[0]", Ease.CustomAnimationCurve, true);
            for (var i = 0; i < objects.Length; i++)
                using (var data = new SerializedObject(objects[i]))
                {
                    var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                    var original = i == 0 ? firstItem : secondItem;
                    Assert.That(property.FindPropertyRelative("CustomEase").boolValue, Is.True);
                    Assert.That(property.FindPropertyRelative("Ease").intValue, Is.EqualTo((int)original.Ease));
                    Assert.That(property.FindPropertyRelative("EaseCurve").animationCurveValue.keys, Is.EqualTo(original.EaseCurve.keys));
                }
        }

        [TestCase(TweenType.Move, 6)]
        [TestCase(TweenType.Color, 4)]
        public void Drawer_HeightTracksSourceCurveAdvancedAndEvents(TweenType type, int advancedRows)
        {
            Component target = type == TweenType.Color ? (Component)Create<Image>() : Create<Transform>();
            var item = Item(type, target);
            item.UseFromValue = false;
            var sequence = Sequence(item);
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(0);
                property.isExpanded = true;
                property.FindPropertyRelative("SpeedBased").isExpanded = false;
                property.FindPropertyRelative("CustomEase").isExpanded = false;
                var drawerType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
                var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType, true);
                var row = EditorGUIUtility.singleLineHeight + 3f;
                var endpointRows = type == TweenType.Move ? 2 : 0;
                Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.EqualTo(row * (14 + endpointRows) + 8f));
                property.FindPropertyRelative("CustomEase").boolValue = true;
                property.FindPropertyRelative("SpeedBased").isExpanded = true;
                property.FindPropertyRelative("CustomEase").isExpanded = true;
                var expected = row * (15 + advancedRows + endpointRows) + 8f;
                foreach (var name in new[] { "OnPlay", "OnUpdate", "OnComplete" })
                    expected += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name), true) + 3f;
                Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.EqualTo(expected));
                property.FindPropertyRelative("Target").objectReferenceValue = null;
                Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.EqualTo(expected));
                property.FindPropertyRelative("Target").objectReferenceValue = target;
                property.FindPropertyRelative("DurationOrSpeed").floatValue = -1f;
                Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.EqualTo(expected + 61f));
                property.isExpanded = false;
                Assert.That(drawer.GetPropertyHeight(property, GUIContent.none), Is.EqualTo(row + 8f));
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(240f)]
        [TestCase(600f)]
        public void Timeline_InlineBarStaysBetweenHeaderAndFieldsAndSegmentsStayInBounds(float width)
        {
            var rect = new Rect(20, 30, width, 200);
            var bar = (Rect)InvokeDrawer("GetTimelineRect", rect);
            var row = EditorGUIUtility.singleLineHeight + 3f;
            Assert.That(bar.yMin, Is.GreaterThanOrEqualTo(rect.y + row));
            Assert.That(bar.yMax, Is.LessThan(rect.y + row + 8f));
            Assert.That(bar.width, Is.EqualTo(width));
            var editor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var method = editor.GetMethod("SegmentRect", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var interval in new[] { new Vector2(0, .4f), new Vector2(.4f, .5f), new Vector2(.5f, .9f), new Vector2(.9f, .9f) })
            {
                var segment = (Rect)method.Invoke(null, new object[] { bar, .9, (double)interval.x, (double)interval.y });
                Assert.That(segment.xMin, Is.GreaterThanOrEqualTo(bar.xMin));
                Assert.That(segment.xMax, Is.LessThanOrEqualTo(bar.xMax + .001f));
                Assert.That(segment.width, Is.GreaterThanOrEqualTo(0));
            }
        }

        [TestCase(null, "第 1 项")]
        [TestCase("  ", "第 1 项")]
        [TestCase(" 标题滑入 ", "标题滑入")]
        public void Drawer_ItemNameUsesFallbackAndDoesNotAffectPlayback(string name, string expected)
        {
            Assert.That((string)InvokeDrawer("GetItemTitle", name, "第 1 项"), Is.EqualTo(expected));
            var item = Item(TweenType.MoveX, Create<Transform>());
            item.Name = name;
            item.Ease = Ease.Linear;
            item.FromValue = Vector4.zero;
            item.ToValue = new Vector4(10, 0, 0, 0);
            var sequence = Sequence(item);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, 0);
            var handle = sequence.Animation.DOPlay();
            handle.Time = .5;
            Assert.That(item.Target.transform.position.x, Is.EqualTo(5).Within(.001));
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                var read = (Animation)InvokeDrawer("ReadAnimation", property, null);
                Assert.That(read.Name, Is.EqualTo(name ?? string.Empty));
            }
        }

        [Test]
        public void Drawer_DuplicateItemKeepsConfigurationAndSupportsUndo()
        {
            var item = Item(TweenType.Move, Create<Transform>());
            item.Name = "标题滑入";
            var sequence = Sequence(item);
            Undo.IncrementCurrentGroup();
            InvokeDrawer("DuplicateItem", new Object[] { sequence }, "m_Animation.m_Sequence.Array.data[0]");
            Undo.FlushUndoRecordObjects();
            using (var data = new SerializedObject(sequence))
            {
                var list = data.FindProperty("m_Animation.m_Sequence");
                Assert.That(list.arraySize, Is.EqualTo(2));
                var copy = list.GetArrayElementAtIndex(1);
                Assert.That(copy.FindPropertyRelative("Name").stringValue, Is.EqualTo(item.Name));
                Assert.That(copy.FindPropertyRelative("Target").objectReferenceValue, Is.EqualTo(item.Target));
                AssertVector(copy.FindPropertyRelative("ToValue").vector4Value, item.ToValue);
            }
            Undo.PerformUndo();
            Assert.That(new SerializedObject(sequence).FindProperty("m_Animation.m_Sequence").arraySize, Is.EqualTo(1));
        }

        [Test]
        public void Drawer_PasteParametersKeepsIdentitySourcesReferencesAndEventsAndSupportsUndo()
        {
            var source = Item(TweenType.Move, Create<Transform>());
            source.DurationOrSpeed = 3;
            source.Relative = true;
            source.Delay = .4f;
            source.Ease = Ease.OutBounce;
            var destination = Item(TweenType.Move, Create<Transform>());
            destination.Name = "目标名称";
            destination.UseFromTarget = true;
            destination.FromTarget = Create<Transform>();
            destination.UseToTarget = true;
            destination.ToTarget = Create<Transform>();
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(destination.OnComplete, destination.Target.gameObject.SetActive, true);
            var sequence = Sequence(source, destination);
            using (var data = new SerializedObject(sequence))
                InvokeDrawer("CopyParameters", data.FindProperty("m_Animation.m_Sequence.Array.data[0]"));
            Undo.IncrementCurrentGroup();
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[1]");
                InvokeDrawer("PasteParameters", property);
                data.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects();
            using (var data = new SerializedObject(sequence))
            {
                var property = data.FindProperty("m_Animation.m_Sequence.Array.data[1]");
                Assert.That(property.FindPropertyRelative("DurationOrSpeed").floatValue, Is.EqualTo(3));
                Assert.That(property.FindPropertyRelative("Relative").boolValue, Is.True);
                Assert.That(property.FindPropertyRelative("Delay").floatValue, Is.EqualTo(.4f));
                Assert.That(property.FindPropertyRelative("Name").stringValue, Is.EqualTo(destination.Name));
                Assert.That(property.FindPropertyRelative("Target").objectReferenceValue, Is.EqualTo(destination.Target));
                Assert.That(property.FindPropertyRelative("FromTarget").objectReferenceValue, Is.EqualTo(destination.FromTarget));
                Assert.That(property.FindPropertyRelative("ToTarget").objectReferenceValue, Is.EqualTo(destination.ToTarget));
                Assert.That(property.FindPropertyRelative("UseFromTarget").boolValue, Is.True);
                Assert.That(property.FindPropertyRelative("UseToTarget").boolValue, Is.True);
                Assert.That(property.FindPropertyRelative("OnComplete.m_PersistentCalls.m_Calls").arraySize, Is.EqualTo(1));
                property.FindPropertyRelative("AnimationType").intValue = (int)TweenType.Scale;
                Assert.That((bool)InvokeDrawer("CanPasteParameters", property), Is.False);
            }
            Undo.PerformUndo();
            Assert.That(new SerializedObject(sequence).FindProperty("m_Animation.m_Sequence.Array.data[1].DurationOrSpeed").floatValue, Is.EqualTo(1));
            Assert.That(new SerializedObject(sequence).FindProperty("m_Animation.m_Sequence.Array.data[1].Relative").boolValue, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Drawer_SwapNumericalEndpointsPreservesEachObjectsValuesAndSupportsUndo(bool multiple)
        {
            var first = Item(TweenType.Move, Create<Transform>());
            first.FromValue = new Vector4(1, 2, 3, 4);
            first.ToValue = new Vector4(5, 6, 7, 8);
            var second = Item(TweenType.Move, Create<Transform>());
            second.FromValue = new Vector4(9, 10, 11, 12);
            second.ToValue = new Vector4(13, 14, 15, 16);
            var fromValues = new[] { first.FromValue, second.FromValue };
            var toValues = new[] { first.ToValue, second.ToValue };
            var objects = multiple ? new Object[] { Sequence(first), Sequence(second) } : new Object[] { Sequence(first) };
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            using (var data = new SerializedObject(objects))
            {
                InvokeDrawer("SwapEndpoints", data.FindProperty("m_Animation.m_Sequence.Array.data[0]"));
                data.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            for (var i = 0; i < objects.Length; i++)
                using (var data = new SerializedObject(objects[i]))
                {
                    AssertVector(data.FindProperty("m_Animation.m_Sequence.Array.data[0].FromValue").vector4Value, toValues[i]);
                    AssertVector(data.FindProperty("m_Animation.m_Sequence.Array.data[0].ToValue").vector4Value, fromValues[i]);
                    data.FindProperty("m_Animation.m_Sequence.Array.data[0].UseFromValue").boolValue = false;
                    Assert.That((bool)InvokeDrawer("CanSwapEndpoints", data.FindProperty("m_Animation.m_Sequence.Array.data[0]")), Is.False);
                }
            Undo.PerformUndo();
            for (var i = 0; i < objects.Length; i++)
                using (var data = new SerializedObject(objects[i]))
                    AssertVector(data.FindProperty("m_Animation.m_Sequence.Array.data[0].FromValue").vector4Value, fromValues[i]);
        }

        [Test]
        public void Drawer_TypeMenuContainsEachTypeOnceWithoutRecommendationGroup()
        {
            var drawer = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
            var types = (TweenType[])drawer.GetField("AnimationTypes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(types, Is.EqualTo((TweenType[])Enum.GetValues(typeof(TweenType))));
            foreach (var type in types)
                Assert.That((string)InvokeDrawer("GetTypePath", type), Does.Not.StartWith("推荐/").And.Not.StartWith("全部/"));
            var categories = (string[])drawer.GetField("EaseCategories", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
            foreach (Ease ease in Enum.GetValues(typeof(Ease)))
                Assert.That(categories, Does.Contain((string)InvokeDrawer("GetEaseCategory", ease)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaitItem_NeedsNoTargetAndParticipatesInTimelinePlaybackAndPreview(bool reverse)
        {
            var wait = new Animation { AnimationType = TweenType.Wait, DurationOrSpeed = .4f, SpeedBased = true, Delay = 50, Loops = 99 };
            var target = Create<Transform>();
            var move = Item(TweenType.MoveX, target);
            move.FromValue = Vector4.zero;
            move.ToValue = new Vector4(10, 0, 0, 0);
            move.Ease = Ease.Linear;
            var sequence = Sequence(wait, move);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, 0);
            Assert.That(sequence.Animation.GetValidationError(), Is.Null);
            sequence.Animation.ResetToFromValue();
            var timeline = sequence.Animation.CaptureTimeline(reverse);
            Assert.That(timeline.Duration, Is.EqualTo(1.4).Within(.00001));
            Assert.That(timeline.Estimated, Is.False);
            var motion = reverse ? sequence.Animation.DORewind() : sequence.Animation.DOPlay();
            Assert.That(motion.TotalDuration, Is.EqualTo(1.4).Within(.00001));
            motion.Time = reverse ? .5 : .9;
            Assert.That(target.position.x, Is.EqualTo(5).Within(.001));
            sequence.Animation.DOKill();
            sequence.gameObject.SetActive(true);
            InvokePreview("Start", new[] { sequence }, false);
            InvokePreview("Stop");
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void WaitItem_RejectsInvalidDuration(float duration)
        {
            Assert.That(new Animation { AnimationType = TweenType.Wait, DurationOrSpeed = duration }.GetValidationError(), Is.Not.Null);
        }

        [Test]
        public void Editor_StaggerChangesOnlySelectedRangeAndSupportsUndo()
        {
            var items = new Animation[4];
            for (var i = 0; i < items.Length; i++) items[i] = Item(TweenType.Move, Create<Transform>());
            var sequence = Sequence(items);
            Undo.IncrementCurrentGroup();
            var editor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            editor.GetMethod("ApplyStaggerBinding", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { EmbeddedBinding(sequence, "m_Animation"), 1, 2, .2f });
            Undo.FlushUndoRecordObjects();
            using (var data = new SerializedObject(sequence))
                for (var i = 0; i < items.Length; i++)
                {
                    var item = data.FindProperty("m_Animation.m_Sequence").GetArrayElementAtIndex(i);
                    Assert.That(item.FindPropertyRelative("Target").objectReferenceValue, Is.EqualTo(items[i].Target));
                    Assert.That(item.FindPropertyRelative("AddType").intValue, Is.EqualTo(i == 1 || i == 2 ? 2 : 0));
                    if (i == 1 || i == 2) Assert.That(item.FindPropertyRelative("InsertTime").floatValue, Is.EqualTo(1 + (i - 1) * .2f).Within(.00001));
                }
            Undo.PerformUndo();
            Assert.That(new SerializedObject(sequence).FindProperty("m_Animation.m_Sequence.Array.data[1].AddType").intValue, Is.Zero);
        }

        [Test]
        public void Drawer_FocusBoundsCoverTransformedRectWithoutChangingSelection()
        {
            var target = Create<RectTransform>();
            target.sizeDelta = new Vector2(300, 100);
            target.position = new Vector3(40, 50, 0);
            target.rotation = Quaternion.Euler(0, 0, 35);
            var selection = Selection.objects;
            var bounds = (Bounds)InvokeDrawer("GetTargetBounds", target);
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            foreach (var corner in corners) Assert.That(bounds.SqrDistance(corner), Is.LessThan(.0001f));
            Assert.That(Selection.objects, Is.EqualTo(selection));
            var point = Create<Transform>();
            point.position = new Vector3(4, 5, 6);
            AssertVector(((Bounds)InvokeDrawer("GetTargetBounds", point)).center, point.position);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Editor_TimingEditChangesOnlyRequestedTimeAndSupportsUndo(bool move)
        {
            var sequence = Sequence(Item(TweenType.Move, Create<Transform>()));
            var editor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var method = editor.GetMethod("ApplyItemTiming", BindingFlags.Static | BindingFlags.NonPublic);
            Undo.IncrementCurrentGroup();
            using (var data = new SerializedObject(sequence))
            {
                var item = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                method.Invoke(null, new object[] { item, .3f, .7f, move });
                Assert.That(item.FindPropertyRelative("DurationOrSpeed").floatValue, Is.EqualTo(move ? 1 : .7f));
                Assert.That(item.FindPropertyRelative("AddType").intValue, Is.EqualTo(move ? 2 : 0));
                if (move) Assert.That(item.FindPropertyRelative("InsertTime").floatValue, Is.EqualTo(.3f));
                data.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            using (var data = new SerializedObject(sequence))
            {
                var item = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                Assert.That(item.FindPropertyRelative("DurationOrSpeed").floatValue, Is.EqualTo(1));
                Assert.That(item.FindPropertyRelative("AddType").intValue, Is.Zero);
                item.FindPropertyRelative("SpeedBased").boolValue = true;
                Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { item, .3f, .7f, move }));
            }
        }

        [TestCase("Target", false)]
        [TestCase("Target", true)]
        [TestCase("FromTarget", false)]
        [TestCase("FromTarget", true)]
        [TestCase("ToTarget", false)]
        [TestCase("ToTarget", true)]
        public void Drawer_ReferenceErrorsUseFieldFeedbackWithoutTextButKeepRuntimeValidation(string field, bool wrongType)
        {
            var item = Item(TweenType.CanvasGroupFade, Create<CanvasGroup>());
            item.UseFromTarget = field == "FromTarget";
            item.UseToTarget = field == "ToTarget";
            var invalid = wrongType ? Create<Transform>() : null;
            if (field == "Target") item.Target = invalid;
            else if (field == "FromTarget") item.FromTarget = invalid;
            else item.ToTarget = invalid;
            Assert.That((string)InvokeDrawer("GetReferenceError", item, field), Does.Contain(field).And.Contain("CanvasGroup"));
            Assert.That(InvokeDrawer("GetTextError", item), Is.Null);
            var sequence = Sequence(item);
            Assert.That(sequence.Animation.GetValidationError(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.DOPlay());
            var editor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            Assert.That(editor.GetMethod("ShouldShowValidationText", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { sequence.Animation.GetValidationError(), true }), Is.False);
            Assert.That((string)InvokeDrawer("FormatTargetSummary", "<目标>", true), Is.EqualTo("<color=#EF6565>&lt;目标&gt;</color>"));
        }

        [Test]
        public void Drawer_ReferenceFeedbackIgnoresInactiveEndpointsAndWaitButKeepsNumericalErrors()
        {
            var item = Item(TweenType.Move, Create<Transform>());
            Assert.That(InvokeDrawer("GetReferenceError", item, "FromTarget"), Is.Null);
            Assert.That(InvokeDrawer("GetReferenceError", item, "ToTarget"), Is.Null);
            item.DurationOrSpeed = -1;
            Assert.That((string)InvokeDrawer("GetTextError", item), Does.Contain("DurationOrSpeed"));
            item.AnimationType = TweenType.Wait;
            item.Target = null;
            item.UseFromTarget = item.UseToTarget = true;
            Assert.That(InvokeDrawer("GetReferenceError", item, "Target"), Is.Null);
            Assert.That(InvokeDrawer("GetReferenceError", item, "FromTarget"), Is.Null);
            Assert.That((string)InvokeDrawer("GetTextError", item), Does.Contain("等待"));
        }

        [Test]
        public void Drawer_MixedReferencesCheckEachObjectWithoutMutatingConfiguration()
        {
            var valid = Item(TweenType.Move, Create<Transform>());
            var invalid = Item(TweenType.Move, Create<Transform>());
            invalid.Target = null;
            var objects = new Object[] { Sequence(valid), Sequence(invalid) };
            var before = EditorJsonUtility.ToJson(objects[0]);
            using (var data = new SerializedObject(objects))
            {
                var item = data.FindProperty("m_Animation.m_Sequence.Array.data[0]");
                Assert.That((string)InvokeDrawer("GetSerializedReferenceError", item, "Target"), Does.Contain("Target"));
                item.FindPropertyRelative("Target").objectReferenceValue = valid.Target;
                Assert.That(InvokeDrawer("GetSerializedReferenceError", item, "Target"), Is.Null);
            }
            Assert.That(EditorJsonUtility.ToJson(objects[0]), Is.EqualTo(before));
        }

        [TestCase(false, "Insert · 起点 0.35 秒")]
        [TestCase(true, "时长 0.6 秒 / 单轮")]
        public void Editor_TimingFeedbackNamesEditedValueAndInsertMode(bool duration, string expected)
        {
            var editor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var method = editor.GetMethod("GetTimingFeedback", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((string)method.Invoke(null, new object[] { duration, .35f, .6f }), Is.EqualTo(expected));
        }

        [TestCase(false, false, false, MouseCursor.MoveArrow)]
        [TestCase(false, false, true, MouseCursor.ResizeHorizontal)]
        [TestCase(true, false, false, MouseCursor.MoveArrow)]
        [TestCase(true, false, true, MouseCursor.MoveArrow)]
        [TestCase(true, true, false, MouseCursor.ResizeHorizontal)]
        [TestCase(true, true, true, MouseCursor.ResizeHorizontal)]
        public void Editor_TimingCursorLocksToDragOperation(bool dragging, bool duration, bool overEdge, MouseCursor expected)
        {
            var editor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var method = editor.GetMethod("GetTimingCursor", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((MouseCursor)method.Invoke(null, new object[] { dragging, duration, overEdge }), Is.EqualTo(expected));
        }

        private object InvokeDrawer(string name, params object[] arguments)
        {
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
            return type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        }

        private Component MatchDrawerComponent(Component component, TweenType type)
        {
            var drawerType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationItemDrawer");
            return (Component)drawerType.GetMethod("MatchComponent", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { component, type });
        }

        private RectTransform CreateReferenceRect(out RectTransform from, out RectTransform to)
        {
            var parent = Create<RectTransform>();
            parent.sizeDelta = new Vector2(360f, 240f);
            parent.pivot = new Vector2(.2f, .7f);
            parent.position = new Vector3(35f, -28f, 11f);
            parent.rotation = Quaternion.Euler(0f, 0f, 23f);
            parent.localScale = new Vector3(1.5f, .8f, 1.2f);
            var markerParent = Create<RectTransform>();
            markerParent.position = new Vector3(-46f, 75f, -9f);
            markerParent.rotation = Quaternion.Euler(0f, 0f, -31f);
            markerParent.localScale = new Vector3(.7f, 1.3f, .9f);
            var target = Create<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = new Vector2(.1f, .2f);
            target.anchorMax = new Vector2(.8f, .9f);
            target.pivot = new Vector2(.3f, .65f);
            target.anchoredPosition3D = new Vector3(13f, 17f, 19f);
            from = Create<RectTransform>();
            from.SetParent(markerParent, false);
            from.localPosition = new Vector3(42f, -18f, 7f);
            to = Create<RectTransform>();
            to.SetParent(markerParent, false);
            to.localPosition = new Vector3(-67f, 94f, -13f);
            return target;
        }

        private static Vector3 AnchorReferenceValue(RectTransform target, RectTransform marker)
        {
            var parent = (RectTransform)target.parent;
            var local = parent.InverseTransformPoint(marker.position);
            var anchor = parent.rect.min + Vector2.Scale(parent.rect.size,
                target.anchorMin + Vector2.Scale(target.anchorMax - target.anchorMin, target.pivot));
            return new Vector3(local.x - anchor.x, local.y - anchor.y, local.z);
        }

        private static Vector3 ReferenceAxes(Vector3 initial, Vector3 reference, TweenType type, int axis)
        {
            if (axis >= 0) { initial[axis] = reference[axis]; return initial; }
            if (type == TweenType.AnchorPosition) reference.z = initial.z;
            return reference;
        }

        [TestCase("reset")]
        [TestCase("play")]
        [TestCase("reverse")]
        public void InvalidLaterItem_DoesNotPartiallyResetOrPlay(string operation)
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var first = Item(TweenType.LocalMove, target);
            var playCount = 0;
            first.OnPlay.AddListener(() => playCount++);
            var sequence = Sequence(first, Item(TweenType.Move, null));
            var sequencePlay = (UnityEvent)typeof(TweenAnimation).GetField("m_OnPlay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            sequencePlay.AddListener(() => playCount++);
            Assert.That(sequence.Animation.GetValidationError(), Does.Contain("第 2 项"));
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (operation == "reset") sequence.Animation.ResetToFromValue();
                else if (operation == "play") sequence.Animation.DOPlay();
                else sequence.Animation.DORewind();
            });
            AssertVector(target.localPosition, new Vector3(7f, 8f, 9f));
            Assert.That(playCount, Is.Zero);
            var motion = (LitMotion.MotionHandle)typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            Assert.That(motion.IsValid(), Is.False, "校验失败不能创建部分动画。");
        }

        [TestCase(TweenType.LayoutFlexibleSize)]
        [TestCase(TweenType.LayoutMinSize)]
        [TestCase(TweenType.LayoutPreferredSize)]
        public void ResetToFromValue_UpdatesBothLayoutDimensions(TweenType type)
        {
            var layout = Create<LayoutElement>();
            layout.flexibleWidth = 1f; layout.flexibleHeight = 2f;
            layout.minWidth = 3f; layout.minHeight = 4f;
            layout.preferredWidth = 5f; layout.preferredHeight = 6f;
            var item = Item(type, layout);
            item.FromValue = new Vector4(17f, 23f, 0f, 0f);
            Sequence(item).Animation.ResetToFromValue();
            AssertVector(item.CaptureCurrentValue(), item.FromValue);
            if (type != TweenType.LayoutFlexibleSize) AssertVector(new Vector2(layout.flexibleWidth, layout.flexibleHeight), new Vector2(1f, 2f));
            if (type != TweenType.LayoutMinSize) AssertVector(new Vector2(layout.minWidth, layout.minHeight), new Vector2(3f, 4f));
            if (type != TweenType.LayoutPreferredSize) AssertVector(new Vector2(layout.preferredWidth, layout.preferredHeight), new Vector2(5f, 6f));
        }

        [TestCase(TweenType.MoveX, 0)]
        [TestCase(TweenType.MoveY, 1)]
        [TestCase(TweenType.MoveZ, 2)]
        [TestCase(TweenType.LocalMoveX, 0)]
        [TestCase(TweenType.LocalMoveY, 1)]
        [TestCase(TweenType.LocalMoveZ, 2)]
        [TestCase(TweenType.ScaleX, 0)]
        [TestCase(TweenType.ScaleY, 1)]
        [TestCase(TweenType.ScaleZ, 2)]
        [TestCase(TweenType.AnchorPositionX, 0)]
        [TestCase(TweenType.AnchorPositionY, 1)]
        [TestCase(TweenType.AnchorPositionZ, 2)]
        public void CaptureAndApply_AxisUsesXAndPreservesOtherAxes(TweenType type, int axis)
        {
            var target = Create<RectTransform>();
            var initial = new Vector3(3f, 5f, 7f);
            var scale = type == TweenType.ScaleX || type == TweenType.ScaleY || type == TweenType.ScaleZ;
            var anchor = type == TweenType.AnchorPositionX || type == TweenType.AnchorPositionY || type == TweenType.AnchorPositionZ;
            if (scale) target.localScale = initial;
            else if (anchor) target.anchoredPosition3D = initial;
            else target.localPosition = initial;
            var item = Item(type, target);
            AssertVector(item.CaptureCurrentValue(), new Vector4(initial[axis], 0f, 0f, 0f));
            item.ApplyValue(new Vector4(11f, 99f, 98f, 97f));
            var expected = initial;
            expected[axis] = 11f;
            AssertVector(scale ? target.localScale : anchor ? target.anchoredPosition3D : target.localPosition, expected);
            AssertVector(item.CaptureCurrentValue(), new Vector4(11f, 0f, 0f, 0f));
        }

        [Test]
        public void CaptureAndApply_ColorAndFadePreserveUnrelatedChannels()
        {
            var graphic = Create<Image>();
            var color = new Color(.2f, .3f, .4f, .5f);
            graphic.color = color;
            var item = Item(TweenType.Color, graphic);
            AssertVector(item.CaptureCurrentValue(), color);
            var replacement = new Color(.6f, .7f, .8f, .9f);
            item.ApplyValue(replacement);
            AssertVector(graphic.color, replacement);
            item.AnimationType = TweenType.Fade;
            AssertVector(item.CaptureCurrentValue(), new Vector4(.9f, 0f, 0f, 0f));
            item.ApplyValue(new Vector4(.1f, 99f, 98f, 97f));
            AssertVector(graphic.color, new Color(.6f, .7f, .8f, .1f));
        }

        [TestCase(TweenType.Move, typeof(Transform))]
        [TestCase(TweenType.AnchorPositionZ, typeof(RectTransform))]
        [TestCase(TweenType.Color, typeof(Graphic))]
        [TestCase(TweenType.Fade, typeof(Graphic))]
        [TestCase(TweenType.CanvasGroupFade, typeof(CanvasGroup))]
        [TestCase(TweenType.FillAmount, typeof(Image))]
        [TestCase(TweenType.LayoutFlexibleSize, typeof(LayoutElement))]
        [TestCase(TweenType.LayoutMinSize, typeof(LayoutElement))]
        [TestCase(TweenType.LayoutPreferredSize, typeof(LayoutElement))]
        [TestCase(TweenType.SliderValue, typeof(Slider))]
        public void RequiredComponentType_MatchesPublicEntryPoints(TweenType type, Type expected)
        {
            Assert.That(Animation.GetRequiredComponentType(type), Is.EqualTo(expected));
            Assert.That(TweenAnimation.GetRequiredComponentType(type), Is.EqualTo(expected));
        }

        [Test]
        public void RequiredComponentType_RejectsUnknownType()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Animation.GetRequiredComponentType((TweenType)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => TweenAnimation.GetRequiredComponentType((TweenType)999));
        }

        [TestCase(TweenType.LocalMove)]
        [TestCase(TweenType.Scale)]
        [TestCase(TweenType.LocalRotation)]
        [TestCase(TweenType.AnchorPosition3D)]
        [TestCase(TweenType.SizeDelta)]
        [TestCase(TweenType.CanvasGroupFade)]
        [TestCase(TweenType.LayoutFlexibleSize)]
        [TestCase(TweenType.LayoutMinSize)]
        [TestCase(TweenType.LayoutPreferredSize)]
        [TestCase(TweenType.SliderValue)]
        [TestCase(TweenType.Color)]
        [TestCase(TweenType.Fade)]
        [TestCase(TweenType.FillAmount)]
        public void Preview_PlayThenStopRestoresOriginalProperty(TweenType type)
        {
            var targetType = Animation.GetRequiredComponentType(type);
            var target = Create(targetType == typeof(Graphic) ? typeof(Image) : targetType);
            var item = Item(type, target);
            var original = new Vector4(.2f, .3f, .4f, .5f);
            item.ApplyValue(original);
            var captured = item.CaptureCurrentValue();
            item.FromValue = new Vector4(.7f, .8f, .9f, .6f);
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            var events = 0;
            item.OnPlay.AddListener(() => events++);
            item.OnUpdate.AddListener(() => events++);
            item.OnComplete.AddListener(() => events++);
            StartPreview(new[] { sequence });
            SeekPreview(sequence, .5);
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            Assert.That(Vector4.Distance(item.CaptureCurrentValue(), captured), Is.GreaterThan(.1f));
            InvokePreview("Stop");
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            AssertVector(item.CaptureCurrentValue(), captured);
            Assert.That(events, Is.Zero, "编辑预览不应触发运行时事件。");
            InvokePreview("Stop");
            AssertVector(item.CaptureCurrentValue(), captured);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Preview_InvalidLaterSequenceRejectsBeforeChangingAnyProperty(bool missingToTarget)
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var first = Sequence(Item(TweenType.LocalMove, target));
            var invalidItem = Item(TweenType.LocalMove, missingToTarget ? target : null);
            invalidItem.UseToTarget = missingToTarget;
            var second = Sequence(invalidItem);
            first.gameObject.SetActive(true);
            second.gameObject.SetActive(true);
            Assert.Throws<InvalidOperationException>(() => StartPreview(new[] { first, second }));
            AssertVector(target.localPosition, new Vector3(7f, 8f, 9f));
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
        }

        [Test]
        public void Preview_DoesNotTakeOrStopAnotherAnimationModeSession()
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var sequence = Sequence(Item(TweenType.LocalMove, target));
            sequence.gameObject.SetActive(true);
            AnimationMode.StartAnimationMode();
            m_OwnsExternalMode = true;
            Assert.Throws<InvalidOperationException>(() => StartPreview(new[] { sequence }));
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            InvokePreview("Stop");
            Assert.That(AnimationMode.InAnimationMode(), Is.True, "Tween Stop 不能关闭其他工具的会话。");
            AssertVector(target.localPosition, new Vector3(7f, 8f, 9f));
        }

        [TestCase("stop")]
        [TestCase("complete")]
        [TestCase("save")]
        public void Preview_FilledSliderRestoresValueAndVisualWithoutEvents(string finish)
        {
            var slider = Create<Slider>();
            var fill = Create<Image>();
            fill.transform.SetParent(slider.transform, false);
            fill.type = Image.Type.Filled;
            slider.fillRect = (RectTransform)fill.transform;
            slider.gameObject.SetActive(true);
            fill.gameObject.SetActive(true);
            slider.SetValueWithoutNotify(.2f);
            Assert.That(fill.fillAmount, Is.EqualTo(.2f).Within(.0001f));
            var events = 0;
            slider.onValueChanged.AddListener(_ => events++);
            var item = Item(TweenType.SliderValue, slider);
            item.FromValue = new Vector4(.8f, 0f, 0f, 0f);
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            var sequenceEvent = (UnityEvent)typeof(TweenAnimation).GetField("m_OnPlay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            sequenceEvent.AddListener(() => events++);
            item.OnComplete.AddListener(() => events++);
            StartPreview(new[] { sequence });
            if (finish == "complete")
            {
                sequence.Animation.DOComplete(true);
                InvokePreview("Update");
                Assert.That(AnimationMode.InAnimationMode(), Is.True);
                InvokePreview("Stop");
            }
            else
            {
                SeekPreview(sequence, 0);
                Assert.That(slider.value, Is.EqualTo(.8f).Within(.0001f));
                Assert.That(fill.fillAmount, Is.EqualTo(.8f).Within(.0001f));
                if (finish == "save")
                {
                    var saveType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationPreviewSaveProcessor");
                    var paths = new[] { "Assets/TestOnly.unity" };
                    var returned = saveType.GetMethod("OnWillSaveAssets", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { paths });
                    Assert.That(returned, Is.SameAs(paths));
                }
                else InvokePreview("Stop");
            }
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            Assert.That(slider.value, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(fill.fillAmount, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(events, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Preview_PlayAndReverseCompleteRestoreOriginalWithoutEvents(bool reverse)
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(3f, 5f, 7f);
            var item = Item(TweenType.LocalMove, target);
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            var events = 0;
            item.OnPlay.AddListener(() => events++);
            item.OnUpdate.AddListener(() => events++);
            item.OnComplete.AddListener(() => events++);
            foreach (var name in new[] { "m_OnPlay", "m_OnUpdate", "m_OnComplete" })
                ((UnityEvent)typeof(TweenAnimation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation))
                    .AddListener(() => events++);
            if (reverse) InvokePreview("StartReverse", (object)new[] { sequence });
            else InvokePreview("Start", new[] { sequence }, false);
            sequence.Animation.DOComplete(true);
            InvokePreview("Update");
            if (!reverse) InvokePreview("Stop");
            AssertVector(target.localPosition, new Vector3(3f, 5f, 7f));
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            Assert.That(events, Is.Zero);
        }

        [TestCase(TweenAnimation.LoopType.Restart, false)]
        [TestCase(TweenAnimation.LoopType.Restart, true)]
        [TestCase(TweenAnimation.LoopType.Yoyo, false)]
        [TestCase(TweenAnimation.LoopType.Yoyo, true)]
        public void InfiniteSequence_RepeatsReplaysAndCancels(TweenAnimation.LoopType loopType, bool reverse)
        {
            var target = Create<Transform>();
            var item = Item(TweenType.LocalMoveX, target);
            item.FromValue = Vector4.zero;
            item.ToValue = new Vector4(10f, 0f, 0f, 0f);
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            var data = new SerializedObject(sequence);
            data.FindProperty("m_Animation.m_Loops").intValue = -1;
            data.FindProperty("m_Animation.m_LoopType").intValue = (int)loopType;
            data.FindProperty("m_Animation.m_Ease").intValue = (int)Ease.Linear;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(sequence.Animation.GetValidationError(), Is.Null);
            var handle = reverse ? sequence.Animation.DORewind() : sequence.Animation.DOPlay();
            Assert.That(handle.Loops, Is.EqualTo(-1));
            foreach (var time in new[] { .25, 1.25, 2.25, 101.25 })
            {
                handle.Time = time;
                var fraction = loopType == TweenAnimation.LoopType.Yoyo && (int)time % 2 == 1 ? .75f : .25f;
                if (reverse) fraction = 1f - fraction;
                Assert.That(target.localPosition.x, Is.EqualTo(10f * fraction).Within(.0001f));
                Assert.That(handle.IsPlaying(), Is.True);
            }
            // Complete 应依据正在播放的句柄，而非播放后被修改的配置。
            data.Update();
            data.FindProperty("m_Animation.m_Loops").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Assert.Throws<InvalidOperationException>(() => sequence.Animation.DOComplete()).Message, Does.Contain("DOKill"));
            Assert.That(handle.IsPlaying(), Is.True);
            data.FindProperty("m_Animation.m_Loops").intValue = -1;
            data.ApplyModifiedPropertiesWithoutUndo();
            var replay = sequence.Animation.DOPlay();
            Assert.That(handle.IsActive(), Is.False);
            sequence.Animation.DOKill();
            Assert.That(replay.IsActive(), Is.False);
            Assert.DoesNotThrow(() => sequence.Animation.DOKill());
        }

        [TestCase(0, false)]
        [TestCase(-2, false)]
        [TestCase(-1, true)]
        public void InfiniteSequence_RejectsInvalidGlobalOrInfiniteItem(int loops, bool itemLoops)
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var item = Item(TweenType.LocalMove, target);
            if (itemLoops) item.Loops = loops;
            var sequence = Sequence(item);
            var data = new SerializedObject(sequence);
            data.FindProperty("m_Animation.m_Loops").intValue = itemLoops ? -1 : loops;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(sequence.Animation.GetValidationError(), Does.Contain("Loops"));
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.DOPlay());
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.DORewind());
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.ResetToFromValue());
            AssertVector(target.localPosition, new Vector3(7f, 8f, 9f));
        }

        [Test]
        public void InfinitePreview_StaysActiveUntilStopAndRestoresOriginal()
        {
            var target = Create<Transform>();
            target.localPosition = new Vector3(7f, 8f, 9f);
            var item = Item(TweenType.LocalMove, target);
            var sequence = Sequence(item);
            var data = new SerializedObject(sequence);
            data.FindProperty("m_Animation.m_Loops").intValue = -1;
            data.FindProperty("m_Animation.m_LoopType").intValue = (int)TweenAnimation.LoopType.Yoyo;
            data.ApplyModifiedPropertiesWithoutUndo();
            sequence.gameObject.SetActive(true);
            var events = 0;
            item.OnComplete.AddListener(() => events++);
            StartPreview(new[] { sequence });
            SeekPreview(sequence, 10.5);
            InvokePreview("Update");
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            Assert.That(Vector3.Distance(target.localPosition, new Vector3(7f, 8f, 9f)), Is.GreaterThan(1f));
            InvokePreview("Stop");
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            AssertVector(target.localPosition, new Vector3(7f, 8f, 9f));
            Assert.That(events, Is.Zero);
        }

        [TestCase("duration")]
        [TestCase("delay")]
        [TestCase("speed")]
        [TestCase("loops")]
        [TestCase("curve")]
        [TestCase("type")]
        public void Validation_RejectsInvalidParametersWithoutRepair(string parameter)
        {
            var item = Item(TweenType.LocalMove, Create<Transform>());
            switch (parameter)
            {
                case "duration": item.DurationOrSpeed = float.NaN; break;
                case "delay": item.Delay = -1f; break;
                case "speed": item.SpeedBased = true; item.DurationOrSpeed = 0f; break;
                case "loops": item.Loops = 0; break;
                case "curve": item.CustomEase = true; item.EaseCurve = null; break;
                case "type": item.AnimationType = (TweenType)999; break;
            }
            Assert.That(item.GetValidationError(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => Sequence(item).Animation.DOPlay());
            if (parameter == "loops") Assert.That(item.Loops, Is.Zero);
            if (parameter == "speed") Assert.That(item.DurationOrSpeed, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TerminalMotion_RepeatedCompleteKillAndReplayAreSafe(bool cancel, bool preserve)
        {
            var target = Create<Transform>();
            var sequence = Sequence(Item(TweenType.LocalMove, target));
            var handle = sequence.Animation.DOPlay();
            if (preserve) handle.Preserve();
            // 通过外部句柄结束动画，保留组件内的终态句柄以验证清理逻辑。
            if (cancel) handle.Cancel();
            else handle.Complete();
            Assert.That(handle.IsPlaying(), Is.False);
            Assert.DoesNotThrow(() => sequence.Animation.DOComplete(true));
            Assert.DoesNotThrow(() => sequence.Animation.DOComplete());
            Assert.DoesNotThrow(() => sequence.Animation.DOKill());
            Assert.DoesNotThrow(() => sequence.Animation.DOKill());
            Assert.That(handle.IsActive(), Is.False);
            Assert.DoesNotThrow(() => sequence.Animation.DOPlay());
            sequence.Animation.DOKill();
        }

        [TestCase("null")]
        [TestCase("empty")]
        [TestCase("null-entry")]
        [TestCase("invalid-entry")]
        public void SetAnimations_InvalidCandidateRetainsListAndPlayingMotion(string candidateKind)
        {
            var target = Create<Transform>();
            var original = Item(TweenType.LocalMove, target);
            original.Ease = Ease.Linear;
            var sequence = Sequence(original);
            var handle = sequence.Animation.DOPlay();
            var candidateTarget = Create<Transform>();
            candidateTarget.localPosition = new Vector3(7f, 8f, 9f);
            var valid = Item(TweenType.LocalMove, candidateTarget);
            var candidate = candidateKind == "null" ? null
                : candidateKind == "empty" ? Array.Empty<Animation>()
                : new[] { valid, candidateKind == "null-entry" ? null : Item(TweenType.Move, null) };

            Assert.Throws<InvalidOperationException>(() => sequence.Animation.SetAnimations(candidate));
            Assert.That(handle.IsPlaying(), Is.True, "拒绝候选配置不能取消当前动画。");
            handle.Time = .5;
            Assert.That(Vector4.Distance(original.CaptureCurrentValue(), original.FromValue), Is.GreaterThan(.01f));
            AssertVector(candidateTarget.localPosition, new Vector3(7f, 8f, 9f));
            sequence.Animation.DOKill();
            Assert.That(handle.IsActive(), Is.False, "组件仍须持有原句柄。");
            sequence.Animation.ResetToFromValue();
            AssertVector(target.localPosition, (Vector3)original.FromValue);
            var replay = sequence.Animation.DOPlay();
            replay.Complete();
            AssertVector(target.localPosition, (Vector3)original.ToValue);
        }

        [TestCase("m_Delay", -1f)]
        [TestCase("m_Loops", 0)]
        [TestCase("m_LoopType", (TweenAnimation.LoopType)999)]
        [TestCase("m_UpdateType", (TweenAnimation.UpdateType)999)]
        [TestCase("m_Ease", Ease.CustomAnimationCurve)]
        public void SetAnimations_InvalidGlobalSettingsRetainListAndPlayingMotion(string field, object invalidValue)
        {
            var target = Create<Transform>();
            var original = Item(TweenType.LocalMove, target);
            var sequence = Sequence(original);
            var handle = sequence.Animation.DOPlay();
            var setting = typeof(TweenAnimation).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            var previous = setting.GetValue(sequence.Animation);
            setting.SetValue(sequence.Animation, invalidValue);
            try
            {
                Assert.Throws<InvalidOperationException>(() => sequence.Animation.SetAnimations(new[]
                {
                    Item(TweenType.LocalMove, Create<Transform>())
                }));
                Assert.That(handle.IsPlaying(), Is.True);
            }
            finally
            {
                setting.SetValue(sequence.Animation, previous);
            }
            sequence.Animation.DOKill();
            Assert.That(handle.IsActive(), Is.False);
            sequence.Animation.ResetToFromValue();
            AssertVector(target.localPosition, (Vector3)original.FromValue);
        }

        [Test]
        public void SetAnimations_ReplacesBatchCancelsPreviousAndSupportsReplayAndKill()
        {
            var previousTarget = Create<Transform>();
            var previous = Item(TweenType.LocalMove, previousTarget);
            var sequence = Sequence(previous);
            var previousHandle = sequence.Animation.DOPlay();
            var first = Item(TweenType.AnchorPosition, Create<Image>().rectTransform);
            first.FromValue = new Vector4(10f, 20f, 0f, 0f);
            first.ToValue = new Vector4(100f, 200f, 0f, 0f);
            first.Ease = Ease.Linear;
            first.Delay = .1f;
            var second = Item(TweenType.AnchorPosition, Create<Image>().rectTransform);
            second.AddType = TweenAnimation.AddType.Join;
            second.FromValue = new Vector4(30f, 40f, 0f, 0f);
            second.ToValue = new Vector4(300f, 400f, 0f, 0f);
            second.Ease = Ease.InQuad;
            second.Delay = .3f;
            var previousPosition = previousTarget.localPosition;

            sequence.Animation.SetAnimations(new[] { first, second });
            Assert.That(previousHandle.IsActive(), Is.False);
            sequence.Animation.ResetToFromValue();
            AssertVector(first.CaptureCurrentValue(), first.FromValue);
            AssertVector(second.CaptureCurrentValue(), second.FromValue);
            var handle = sequence.Animation.DOPlay();
            var replay = sequence.Animation.DOPlay();
            Assert.That(handle.IsActive(), Is.False, "重播应取消上一轮。");
            replay.Complete();
            AssertVector(first.CaptureCurrentValue(), first.ToValue);
            AssertVector(second.CaptureCurrentValue(), second.ToValue);
            AssertVector(previousTarget.localPosition, previousPosition);
            var canceled = sequence.Animation.DOPlay();
            sequence.Animation.DOKill();
            Assert.That(canceled.IsActive(), Is.False);
        }

        [Test]
        public void SetAnimations_CopiesArraySlotsWithoutCloningEntries()
        {
            var sequence = Create<TweenAnimationComponent>();
            var item = Item(TweenType.LocalMove, Create<Transform>());
            var animations = new[] { item };
            sequence.Animation.SetAnimations(animations);
            animations[0] = null;
            Assert.That(sequence.Animation.GetValidationError(), Is.Null);
            item.FromValue = new Vector4(11f, 12f, 13f, 0f);
            sequence.Animation.ResetToFromValue();
            AssertVector(item.CaptureCurrentValue(), item.FromValue);
            var handle = sequence.Animation.DOPlay();
            handle.Complete();
            AssertVector(item.CaptureCurrentValue(), (Vector3)item.ToValue);
        }

        [TestCase(typeof(Transform))]
        [TestCase(typeof(RectTransform))]
        [TestCase(typeof(CanvasGroup))]
        [TestCase(typeof(Slider))]
        public void Fixture_CreatesRequestedLiveComponentWithOneTransform(Type type)
        {
            var component = Create(type);
            Assert.That(component != null, Is.True);
            Assert.That(component.GetType(), Is.EqualTo(type));
            Assert.That(component.gameObject.GetComponents<Transform>().Length, Is.EqualTo(1));
        }

        [Test]
        public void Preview_ScrubEndBackToDelayAndReplayPreservesOriginalAndSuppressesEvents()
        {
            var target = Create<Transform>();
            target.position = new Vector3(7, 2, 3);
            var a = Item(TweenType.MoveX, target);
            a.FromValue = Vector4.zero;
            a.ToValue = new Vector4(10, 0, 0, 0);
            a.Ease = Ease.Linear;
            a.Delay = .2f;
            var b = Item(TweenType.MoveX, target);
            b.FromValue = a.ToValue;
            b.ToValue = new Vector4(20, 0, 0, 0);
            b.Ease = Ease.Linear;
            var events = 0;
            a.OnPlay.AddListener(() => events++);
            a.OnUpdate.AddListener(() => events++);
            b.OnComplete.AddListener(() => events++);
            var sequence = Sequence(a, b);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, .3f);
            sequence.gameObject.SetActive(true);
            InvokePreview("Seek", sequence, 2.5);
            Assert.That(target.position.x, Is.EqualTo(20).Within(.001));
            InvokePreview("Update");
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            InvokePreview("Seek", sequence, .1);
            Assert.That(target.position.x, Is.EqualTo(0).Within(.001));
            InvokePreview("Seek", sequence, 1.0);
            Assert.That(target.position.x, Is.EqualTo(5).Within(.001));
            InvokePreview("Seek", sequence, 2.0);
            Assert.That(target.position.x, Is.EqualTo(15).Within(.001));
            InvokePreview("Seek", sequence, .4);
            Assert.That(target.position.x, Is.EqualTo(0).Within(.001));
            var handle = (MotionHandle)typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            Assert.That(handle.PlaybackSpeed, Is.Zero);
            InvokePreview("Start", new[] { sequence }, false);
            Assert.That(handle.IsActive(), Is.False);
            InvokePreview("Stop");
            AssertVector(target.position, new Vector3(7, 2, 3));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void SinglePreview_IsolatesItemIgnoresInvalidNeighborsAndGlobalSettingsAndRestores()
        {
            var target = Create<Transform>();
            target.gameObject.SetActive(true);
            target.position = new Vector3(7, 2, 3);
            var item = Item(TweenType.MoveX, target);
            item.FromValue = Vector4.zero;
            item.ToValue = new Vector4(10, 0, 0, 0);
            item.Ease = Ease.Linear;
            item.Delay = .2f;
            item.Loops = 2;
            var other = Create<Transform>();
            other.position = new Vector3(20, 30, 40);
            var invalid = Item(TweenType.Move, other);
            invalid.Target = null;
            var sequence = Sequence(invalid, item);
            SetTimelineGlobals(sequence, Ease.InQuad, -1, TweenAnimation.LoopType.Yoyo, 10);
            sequence.gameObject.SetActive(true);
            var before = EditorJsonUtility.ToJson(sequence);
            InvokePreview("StartSingle", sequence, "m_Animation.m_Sequence.Array.data[1]");
            var motion = ((List<MotionHandle>)m_PreviewType.GetField("Motions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))[0];
            Assert.That(motion.TotalDuration, Is.EqualTo(2.2).Within(.0001));
            motion.Time = .7;
            Assert.That(target.position.x, Is.EqualTo(5).Within(.001));
            AssertVector(other.position, new Vector3(20, 30, 40));
            Assert.That(m_PreviewType.GetMethod("GetTimelineBinding", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { EmbeddedBinding(sequence, "m_Animation") }), Is.Null);
            Assert.Throws<InvalidOperationException>(() => InvokePreview("Seek", sequence, .5));
            InvokePreview("Pause");
            Assert.That(motion.PlaybackSpeed, Is.Zero);
            InvokePreview("Resume");
            Assert.That(motion.PlaybackSpeed, Is.EqualTo(1));
            InvokePreview("Restart");
            Assert.That(motion.IsActive(), Is.False);
            var restarted = ((List<MotionHandle>)m_PreviewType.GetField("Motions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))[0];
            restarted.Time = restarted.TotalDuration;
            InvokePreview("Update");
            Assert.That(restarted.PlaybackSpeed, Is.Zero);
            InvokePreview("Stop");
            Assert.That(restarted.IsActive(), Is.False);
            AssertVector(target.position, new Vector3(7, 2, 3));
            Assert.That(EditorJsonUtility.ToJson(sequence), Is.EqualTo(before));
        }

        [TestCase("save")]
        [TestCase("disable")]
        [TestCase("target")]
        [TestCase("undo")]
        public void SinglePreview_LifecycleRestoresAndCancels(string reason)
        {
            var target = Create<CanvasGroup>();
            target.gameObject.SetActive(true);
            target.alpha = .3f;
            var sequence = Sequence(Item(TweenType.CanvasGroupFade, target));
            sequence.gameObject.SetActive(true);
            InvokePreview("StartSingle", sequence, "m_Animation.m_Sequence.Array.data[0]");
            var motion = ((List<MotionHandle>)m_PreviewType.GetField("Motions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))[0];
            motion.Time = .5;
            if (reason == "save")
            {
                var processor = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationPreviewSaveProcessor");
                processor.GetMethod("OnWillSaveAssets", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Array.Empty<string>() });
            }
            else if (reason == "undo") Undo.PerformUndo();
            else
            {
                if (reason == "disable") sequence.enabled = false;
                else target.gameObject.SetActive(false);
                InvokePreview("Update");
            }
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            Assert.That(motion.IsActive(), Is.False);
            Assert.That(target.alpha, Is.EqualTo(.3f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Preview_PauseResumeScrubRestartAndCompletion(bool reverse)
        {
            var target = Create<Transform>();
            target.position = new Vector3(7, 2, 3);
            var item = Item(TweenType.MoveX, target);
            item.FromValue = Vector4.zero;
            item.ToValue = Vector4.one;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, 0);
            sequence.gameObject.SetActive(true);
            InvokePreview("Start", new[] { sequence }, reverse);
            var field = typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic);
            var handle = (MotionHandle)field.GetValue(sequence.Animation);
            handle.Time = .25;
            InvokePreview("Pause");
            InvokePreview("Update");
            Assert.That(handle.PlaybackSpeed, Is.Zero);
            Assert.That(handle.Time, Is.EqualTo(.25));
            InvokePreview("Resume");
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(reverse ? -1 : 1));
            Assert.That(handle.Time, Is.EqualTo(.25));
            InvokePreview("Seek", sequence, .5);
            InvokePreview("Resume");
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(reverse ? -1 : 1));
            Assert.That(handle.Time, Is.EqualTo(.5));
            InvokePreview("Restart");
            Assert.That(handle.IsActive(), Is.False);
            var restarted = (MotionHandle)field.GetValue(sequence.Animation);
            restarted.Time = .25;
            Assert.That(target.position.x, Is.EqualTo(.25).Within(.001));
            InvokePreview("Seek", sequence, reverse ? 0.0 : 1.0);
            InvokePreview("Update");
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            InvokePreview("Resume");
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            Assert.That(restarted.PlaybackSpeed, Is.Zero);
            InvokePreview("Stop");
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            AssertVector(target.position, new Vector3(7, 2, 3));
            Assert.That(restarted.IsActive(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Timeline_ChainWaitsForLongestGroupAndReverseRebuilds(bool reverse)
        {
            var a = Item(TweenType.MoveX, Create<Transform>());
            var b = Item(TweenType.MoveX, Create<Transform>());
            var c = Item(TweenType.MoveX, Create<Transform>());
            a.DurationOrSpeed = .3f;
            b.DurationOrSpeed = .8f;
            b.AddType = TweenAnimation.AddType.Join;
            c.DurationOrSpeed = .2f;
            c.Ease = Ease.Linear;
            c.FromValue = Vector4.zero;
            c.ToValue = Vector4.one;
            var sequence = Sequence(a, b, c);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, 0);
            var timeline = sequence.Animation.CaptureTimeline(reverse);
            Assert.That(timeline.Entries[0].Index, Is.EqualTo(reverse ? 2 : 0));
            Assert.That(timeline.Entries[1].Start, Is.Zero);
            Assert.That(timeline.Entries[2].Start, Is.EqualTo(.8).Within(.00001));
            Assert.That(timeline.Duration, Is.EqualTo(reverse ? 1.1 : 1.0).Within(.00001));
            var handle = reverse ? sequence.Animation.DORewind() : sequence.Animation.DOPlay();
            Assert.That(timeline.TotalDuration, Is.EqualTo(handle.TotalDuration).Within(.00001));
            handle.Time = reverse ? .1 : .9;
            Assert.That(c.Target.transform.position.x, Is.EqualTo(.5f).Within(.0001));
            sequence.Animation.DOKill();
        }

        [Test]
        public void Timeline_InsertAndFollowingGroupUseAbsoluteStartAndChainWaitsForAll()
        {
            var items = new Animation[4];
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = Item(TweenType.MoveX, Create<Transform>());
                items[i].FromValue = Vector4.zero;
                items[i].ToValue = new Vector4(10, 0, 0, 0);
                items[i].Ease = Ease.Linear;
            }
            items[0].DurationOrSpeed = 2;
            items[1].AddType = TweenAnimation.AddType.Insert;
            items[1].InsertTime = .5f;
            items[1].Delay = .2f;
            items[2].AddType = TweenAnimation.AddType.Join;
            var sequence = Sequence(items);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, .3f);
            var timeline = sequence.Animation.CaptureTimeline();
            Assert.That(timeline.Entries[1].Start, Is.EqualTo(.5));
            Assert.That(timeline.Entries[1].ActiveStart, Is.EqualTo(.7).Within(.00001));
            Assert.That(timeline.Entries[2].Start, Is.EqualTo(.5));
            Assert.That(timeline.Entries[3].Start, Is.EqualTo(2));
            var motion = sequence.Animation.DOPlay();
            Assert.That(motion.TotalDuration, Is.EqualTo(timeline.TotalDuration).Within(.00001));
            motion.Time = 1.3;
            Assert.That(items[1].Target.transform.position.x, Is.EqualTo(3).Within(.001));
            Assert.That(items[2].Target.transform.position.x, Is.EqualTo(5).Within(.001));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Timeline_RejectsInvalidInsertTime(float time)
        {
            var item = Item(TweenType.Move, Create<Transform>());
            item.AddType = TweenAnimation.AddType.Insert;
            item.InsertTime = time;
            var sequence = Sequence(item);
            Assert.That(sequence.Animation.GetValidationError(), Does.Contain("InsertTime"));
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.CaptureTimeline());
            Assert.Throws<InvalidOperationException>(() => sequence.Animation.DOPlay());
        }

        [Test]
        public void Timeline_DelayOccursOnlyOnceAndZeroDurationIsFinite()
        {
            var item = Item(TweenType.MoveX, Create<Transform>());
            item.Delay = .2f;
            item.DurationOrSpeed = .5f;
            item.Loops = 3;
            var sequence = Sequence(item);
            SetTimelineGlobals(sequence, Ease.Linear, 2, TweenAnimation.LoopType.Restart, .4f);
            var timeline = sequence.Animation.CaptureTimeline();
            var handle = sequence.Animation.DOPlay();
            Assert.That(timeline.Entries[0].ActiveStart, Is.EqualTo(.2).Within(.00001));
            Assert.That(timeline.Duration, Is.EqualTo(1.7).Within(.00001));
            Assert.That(timeline.TotalDuration, Is.EqualTo(handle.TotalDuration).Within(.00001));
            Assert.That(timeline.TotalDuration, Is.EqualTo(3.8).Within(.00001));
            sequence.Animation.DOKill();
            item.DurationOrSpeed = 0;
            item.Delay = 0;
            timeline = sequence.Animation.CaptureTimeline();
            Assert.That(timeline.Duration, Is.Zero);
            Assert.That(timeline.GetPosition(100), Is.Zero);
        }

        [TestCase(Ease.Linear, TweenAnimation.LoopType.Restart, 3)]
        [TestCase(Ease.OutQuad, TweenAnimation.LoopType.Restart, 3)]
        [TestCase(Ease.OutBack, TweenAnimation.LoopType.Restart, 3)]
        [TestCase(Ease.OutQuad, TweenAnimation.LoopType.Yoyo, 3)]
        [TestCase(Ease.OutQuad, TweenAnimation.LoopType.Incremental, 3)]
        [TestCase(Ease.OutQuad, TweenAnimation.LoopType.Yoyo, -1)]
        public void Timeline_PlayheadMatchesActualNestedAnimation(Ease ease, TweenAnimation.LoopType loop, int loops)
        {
            var item = Item(TweenType.MoveX, Create<Transform>());
            item.FromValue = Vector4.zero;
            item.ToValue = Vector4.one;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            SetTimelineGlobals(sequence, ease, loops, loop, .2f);
            var timeline = sequence.Animation.CaptureTimeline();
            var handle = sequence.Animation.DOPlay();
            Assert.That(timeline.TotalDuration, Is.EqualTo(handle.TotalDuration));
            foreach (var time in new[] { .1, .45, 1.2, 1.45, 2.45 })
            {
                handle.Time = time;
                var expected = Mathf.Clamp01((float)timeline.GetPosition(time));
                Assert.That(item.Target.transform.position.x, Is.EqualTo(expected).Within(.0001), $"time={time}");
            }
            sequence.Animation.DOKill();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Timeline_PreviewFreezesSpeedSampleAndClearsOnStop(bool reverse)
        {
            var target = Create<Transform>();
            target.position = new Vector3(5, 0, 0);
            var marker = Create<Transform>();
            marker.position = new Vector3(9, 0, 0);
            var item = Item(TweenType.MoveX, target);
            item.UseFromValue = false;
            item.UseToTarget = true;
            item.ToTarget = marker;
            item.SpeedBased = true;
            item.DurationOrSpeed = 2;
            var sequence = Sequence(item);
            var snapshot = sequence.Animation.CaptureTimeline(reverse);
            Assert.That(snapshot.Duration, Is.EqualTo(2));
            Assert.That(target.position.x, Is.EqualTo(5));
            sequence.gameObject.SetActive(true);
            if (reverse) InvokePreview("StartReverse", (object)new[] { sequence });
            else InvokePreview("Start", new[] { sequence }, false);
            var getter = m_PreviewType.GetMethod("GetTimelineBinding", BindingFlags.Static | BindingFlags.NonPublic);
            var frozen = (TweenAnimation.TimelineSnapshot)getter.Invoke(null, new object[] { EmbeddedBinding(sequence, "m_Animation") });
            marker.position = new Vector3(15, 0, 0);
            SeekPreview(sequence, .5);
            Assert.That(getter.Invoke(null, new object[] { EmbeddedBinding(sequence, "m_Animation") }), Is.SameAs(frozen));
            Assert.That(frozen.Duration, Is.EqualTo(2));
            Assert.That(frozen.Reverse, Is.EqualTo(reverse));
            var elapsed = m_PreviewType.GetMethod("GetElapsedBinding", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { EmbeddedBinding(sequence, "m_Animation") });
            Assert.That((double)elapsed, Is.EqualTo(.5));
            InvokePreview("Stop");
            Assert.That(getter.Invoke(null, new object[] { EmbeddedBinding(sequence, "m_Animation") }), Is.Null);
            Assert.That(target.position.x, Is.EqualTo(5));
            Assert.That(sequence.Animation.CaptureTimeline().Duration, Is.EqualTo(5));
        }

        [Test]
        public void Reversible_DirectionKeepsHandleTimeGroupingAndEase()
        {
            var target = Create<Transform>();
            var a = Item(TweenType.MoveX, target);
            a.FromValue = Vector4.zero;
            a.ToValue = Vector4.one * 10;
            a.Ease = Ease.InQuad;
            var b = Item(TweenType.MoveY, target);
            b.AddType = TweenAnimation.AddType.Join;
            b.Delay = .2f;
            b.DurationOrSpeed = 2;
            var c = Item(TweenType.MoveZ, target);
            var sequence = Sequence(a, b, c);
            SetTimelineGlobals(sequence, Ease.OutQuad, 1, TweenAnimation.LoopType.Restart, .3f);
            var handle = sequence.PlayForward();
            var samples = new Vector3[4];
            for (var i = 0; i < samples.Length; i++)
            {
                handle.Time = .1 + i * .6;
                samples[i] = target.position;
            }
            var time = handle.Time;
            Assert.That(sequence.PlayBackward(), Is.EqualTo(handle));
            Assert.That(handle.Time, Is.EqualTo(time));
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(-1));
            AssertVector(target.position, samples[3]);
            for (var i = samples.Length - 1; i >= 0; i--)
            {
                handle.Time = .1 + i * .6;
                AssertVector(target.position, samples[i]);
            }
            handle.Time = -.1;
            sequence.Animation.MaintainReversibleEndpoint();
            Assert.That(handle.Time, Is.Zero);
            Assert.That(handle.PlaybackSpeed, Is.Zero);
            Assert.That(sequence.PlayForward(), Is.EqualTo(handle));
            handle.Time = handle.TotalDuration + .2;
            sequence.Animation.MaintainReversibleEndpoint();
            Assert.That(handle.Time, Is.EqualTo(handle.TotalDuration));
            Assert.That(handle.IsActive(), Is.True);
            Assert.That(sequence.PlayBackward(), Is.EqualTo(handle));
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(-1));
            sequence.Animation.DOKill();
            Assert.That(handle.IsActive(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Reversible_PreviewHoldsEndpointsAndRestores(bool backward)
        {
            var target = Create<Transform>();
            target.position = new Vector3(7, 2, 3);
            var item = Item(TweenType.MoveX, target);
            item.FromValue = Vector4.zero;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, 0);
            sequence.gameObject.SetActive(true);
            InvokePreview("Start", new[] { sequence }, backward);
            var handle = (MotionHandle)typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            Assert.That(handle.Time, Is.EqualTo(backward ? 1 : 0));
            handle.Time = .4;
            InvokePreview("Pause");
            InvokePreview("Resume");
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(backward ? -1 : 1));
            InvokePreview("SetDirection", !backward);
            Assert.That(handle.Time, Is.EqualTo(.4));
            AssertVector(target.position, new Vector3(.4f, 2, 3));
            InvokePreview("Seek", sequence, backward ? 1.0 : 0.0);
            InvokePreview("Resume");
            InvokePreview("Update");
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            Assert.That(handle.PlaybackSpeed, Is.Zero);
            InvokePreview("SetDirection", backward);
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(backward ? -1 : 1));
            InvokePreview("Restart");
            Assert.That(handle.IsActive(), Is.False);
            InvokePreview("Stop");
            AssertVector(target.position, new Vector3(7, 2, 3));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Playback_LoopsAllowDirectBackward(int loops)
        {
            var sequence = Sequence(Item(TweenType.MoveX, Create<Transform>()));
            SetTimelineGlobals(sequence, Ease.Linear, loops, TweenAnimation.LoopType.Restart, 0);
            Assert.That(sequence.Animation.GetValidationError(), Is.Null);
            var handle = sequence.PlayBackward();
            Assert.That(handle.Time, Is.EqualTo(loops < 0 ? handle.Delay + handle.Duration : handle.TotalDuration));
            Assert.That(handle.PlaybackSpeed, Is.EqualTo(-1));
            Assert.That(sequence.PlayForward(), Is.EqualTo(handle));
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Reversible_NaturalEditorTimeMovesBackwardAndHolds()
        {
            var item = Item(TweenType.MoveX, Create<Transform>());
            item.DurationOrSpeed = .2f;
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            InvokePreview("Start", new[] { sequence }, true);
            var handle = (MotionHandle)typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            var deadline = EditorApplication.timeSinceStartup + 5;
            while (handle.Time > 0 && EditorApplication.timeSinceStartup < deadline) yield return null;
            InvokePreview("Update");
            Assert.That(handle.Time, Is.Zero);
            Assert.That(handle.PlaybackSpeed, Is.Zero);
            Assert.That(handle.IsActive(), Is.True);
            InvokePreview("SetDirection", false);
            deadline = EditorApplication.timeSinceStartup + 5;
            while (handle.Time < handle.TotalDuration && EditorApplication.timeSinceStartup < deadline) yield return null;
            InvokePreview("Update");
            Assert.That(handle.Time, Is.EqualTo(handle.TotalDuration));
            Assert.That(handle.PlaybackSpeed, Is.Zero);
            Assert.That(handle.IsActive(), Is.True);
        }

        [Test]
        public void Preview_ReverseOrderDoesNotBecomeBackward()
        {
            var target = Create<Transform>();
            target.position = new Vector3(7, 2, 3);
            var item = Item(TweenType.MoveX, target);
            item.FromValue = Vector4.zero;
            item.Ease = Ease.Linear;
            var sequence = Sequence(item);
            SetTimelineGlobals(sequence, Ease.Linear, 1, TweenAnimation.LoopType.Restart, 0);
            sequence.gameObject.SetActive(true);
            InvokePreview("StartReverse", (object)new[] { sequence });
            var field = typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic);
            var handle = (MotionHandle)field.GetValue(sequence.Animation);
            handle.Time = .25;
            Assert.That(target.position.x, Is.EqualTo(.75).Within(.001));
            Assert.That(sequence.Animation.IsBackward, Is.False);
            Assert.Throws<InvalidOperationException>(() => InvokePreview("SetDirection", true));
            InvokePreview("Restart");
            Assert.That(handle.IsActive(), Is.False);
            handle = (MotionHandle)field.GetValue(sequence.Animation);
            handle.Time = .25;
            Assert.That(target.position.x, Is.EqualTo(.75).Within(.001));
            handle.Time = 1;
            InvokePreview("Update");
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            AssertVector(target.position, new Vector3(7, 2, 3));
        }

        [TestCase(TweenType.Move, false)]
        [TestCase(TweenType.Move, true)]
        [TestCase(TweenType.MoveY, false)]
        [TestCase(TweenType.MoveY, true)]
        [TestCase(TweenType.LocalMove, false)]
        [TestCase(TweenType.LocalMove, true)]
        [TestCase(TweenType.LocalMoveZ, false)]
        [TestCase(TweenType.LocalMoveZ, true)]
        [TestCase(TweenType.AnchorPosition, false)]
        [TestCase(TweenType.AnchorPosition, true)]
        [TestCase(TweenType.AnchorPositionX, false)]
        [TestCase(TweenType.AnchorPositionX, true)]
        [TestCase(TweenType.AnchorPosition3D, false)]
        [TestCase(TweenType.AnchorPosition3D, true)]
        public void Endpoint_EditWorldHandleWritesOnlyEndpointAndRestores(TweenType type, bool to)
        {
            var target = type.ToString().StartsWith("Anchor") ? (Transform)Create<RectTransform>() : Create<Transform>();
            var parent = Create<RectTransform>();
            parent.sizeDelta = new Vector2(600, 400);
            parent.position = new Vector3(10, 20, 30);
            parent.rotation = Quaternion.Euler(0, 0, 25);
            parent.localScale = new Vector3(2, 3, 1);
            target.SetParent(parent, false);
            if (target is RectTransform rect)
            {
                rect.anchorMin = new Vector2(.2f, .3f);
                rect.anchorMax = new Vector2(.8f, .9f);
                rect.pivot = new Vector2(.3f, .7f);
                rect.anchoredPosition3D = new Vector3(7, 8, 9);
            }
            else target.localPosition = new Vector3(7, 8, 9);
            var original = target.position;
            var item = Item(type, target);
            item.FromValue = new Vector4(11, 12, 13, 99);
            item.ToValue = new Vector4(11, 12, 13, 99);
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            target.gameObject.SetActive(true);
            parent.gameObject.SetActive(true);
            var hidden = Tools.hidden;
            var events = 0;
            item.OnUpdate.AddListener(() => events++);
            InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", to, true);
            var getter = m_PreviewType.GetMethod("EndpointWorldPosition", BindingFlags.Static | BindingFlags.NonPublic);
            AssertVector((Vector3)getter.Invoke(null, new object[] { item.ToValue }), target.position);
            var delta = new Vector3(3, 4, 5);
            InvokePreview("MoveEndpoint", target.position + delta);
            var endpointPath = "m_Animation.m_Sequence.Array.data[0]." + (to ? "ToValue" : "FromValue");
            var value = new SerializedObject(sequence).FindProperty(endpointPath).vector4Value;
            var sampled = item.CaptureCurrentValue();
            var axis = type == TweenType.MoveY || type == TweenType.LocalMoveZ || type == TweenType.AnchorPositionX;
            Assert.That(value.x, Is.EqualTo(sampled.x).Within(.0001));
            if (axis) { Assert.That(value.y, Is.EqualTo(12)); Assert.That(value.z, Is.EqualTo(13)); }
            else
            {
                Assert.That(value.y, Is.EqualTo(sampled.y).Within(.0001));
                if (type != TweenType.AnchorPosition) Assert.That(value.z, Is.EqualTo(sampled.z).Within(.0001));
            }
            Assert.That(value.w, Is.EqualTo(99));
            AssertVector((Vector3)getter.Invoke(null, new object[] { value }), target.position);
            Assert.Throws<InvalidOperationException>(() => InvokePreview("Seek", sequence, .5));
            InvokePreview("Stop");
            AssertVector(target.position, original);
            Assert.That(Tools.hidden, Is.EqualTo(hidden));
            Assert.That(events, Is.Zero);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            AssertVector(new SerializedObject(sequence).FindProperty(endpointPath).vector4Value,
                new Vector4(11, 12, 13, 99));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Endpoint_ReferenceViewDoesNotModifyReferenceOrConvertSource(bool to)
        {
            var target = CreateReferenceRect(out var from, out var end);
            var original = target.position;
            var referencePosition = to ? end.position : from.position;
            var item = Item(TweenType.AnchorPosition3D, target);
            item.UseFromTarget = true;
            item.FromTarget = from;
            item.UseToTarget = true;
            item.ToTarget = end;
            var sequence = Sequence(item);
            sequence.gameObject.SetActive(true);
            target.gameObject.SetActive(true);
            target.parent.gameObject.SetActive(true);
            Assert.Throws<InvalidOperationException>(() => InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", to, true));
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", to, false);
            AssertVector(target.position, referencePosition);
            Assert.Throws<InvalidOperationException>(() => InvokePreview("MoveEndpoint", Vector3.one));
            InvokePreview("Stop");
            AssertVector(target.position, original);
            AssertVector(to ? end.position : from.position, referencePosition);
            Assert.That(item.UseFromTarget && item.UseToTarget, Is.True);
        }

        [Test]
        public void Endpoint_StopOnSaveAndDisableRestoresAndRejectsOtherAnimationMode()
        {
            var target = Create<Transform>();
            target.position = new Vector3(7, 8, 9);
            target.gameObject.SetActive(true);
            var sequence = Sequence(Item(TweenType.Move, target));
            sequence.gameObject.SetActive(true);
            AnimationMode.StartAnimationMode();
            m_OwnsExternalMode = true;
            Assert.Throws<InvalidOperationException>(() => InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", true, false));
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            AnimationMode.StopAnimationMode();
            m_OwnsExternalMode = false;
            InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", true, false);
            var saveType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationPreviewSaveProcessor");
            saveType.GetMethod("OnWillSaveAssets", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new[] { "Assets/TestOnly.unity" } });
            AssertVector(target.position, new Vector3(7, 8, 9));
            InvokePreview("StartEndpoint", sequence, "m_Animation.m_Sequence.Array.data[0]", true, true);
            sequence.enabled = false;
            InvokePreview("Update");
            AssertVector(target.position, new Vector3(7, 8, 9));
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
        }

        private static void SetTimelineGlobals(TweenAnimationComponent sequence, Ease ease, int loops, TweenAnimation.LoopType loop, float delay)
        {
            using (var data = new SerializedObject(sequence))
            {
                data.FindProperty("m_Animation.m_Ease").intValue = (int)ease;
                data.FindProperty("m_Animation.m_Loops").intValue = loops;
                data.FindProperty("m_Animation.m_LoopType").intValue = (int)loop;
                data.FindProperty("m_Animation.m_Delay").floatValue = delay;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [Test]
        public void Embedded_RuntimeInstancesAndComponentCoreRemainIndependent()
        {
            var host = Create<TweenAnimationTestHost>();
            var first = Create<Transform>();
            var second = Create<Transform>();
            var a = Item(TweenType.Move, first);
            var b = Item(TweenType.Scale, second);
            host.First.SetAnimations(new[] { a });
            host.Second.SetAnimations(new[] { b });
            var one = host.First.DOPlay();
            var two = host.Second.DOPlay();
            try
            {
                one.Time = .4;
                two.Time = .7;
                host.First.DOKill();
                Assert.That(one.IsActive(), Is.False);
                Assert.That(two.IsActive(), Is.True);
                var sequence = Sequence(a);
                Assert.That(sequence.Animation.CaptureTimeline().Duration, Is.EqualTo(host.First.CaptureTimeline().Duration));
                Assert.That(sequence.Animation, Is.SameAs(sequence.Animation));
            }
            finally { host.First.DOKill(); host.Second.DOKill(); }
        }

        [Test]
        public void Embedded_InitializeRejectsSharedOwnerAndDoesNotCreateEditorDriver()
        {
            var host = Create<TweenAnimationTestHost>();
            var other = Create<TweenAnimationTestHost>();
            var before = host.GetComponents<Component>().Length;
            host.First.Initialize(host);
            host.First.Initialize(host);
            Assert.That(host.GetComponents<Component>().Length, Is.EqualTo(before));
            Assert.Throws<InvalidOperationException>(() => host.First.Initialize(other));
        }

        [Test]
        public void Embedded_PreviewIdentityUsesOwnerAndFieldAndRestoresBeforeSwitching()
        {
            var host = Create<TweenAnimationTestHost>();
            var first = Create<Transform>();
            var second = Create<Transform>();
            first.localPosition = new Vector3(12, 13, 14);
            second.localPosition = new Vector3(21, 22, 23);
            var baseline = first.localPosition;
            host.First.SetAnimations(new[] { Item(TweenType.LocalMove, first) });
            host.Second.SetAnimations(new[] { Item(TweenType.Scale, second) });
            host.gameObject.SetActive(true);
            first.gameObject.SetActive(true);
            second.gameObject.SetActive(true);
            var original = EditorJsonUtility.ToJson(host);
            var a = EmbeddedBinding(host, "First");
            var b = EmbeddedBinding(host, "Second");
            InvokePreview("SeekBinding", a, .5);
            Assert.That((bool)InvokeBindingPreview("ContainsBinding", a), Is.True);
            Assert.That((bool)InvokeBindingPreview("ContainsBinding", b), Is.False);
            Assert.That(first.localPosition, Is.Not.EqualTo(baseline));
            InvokePreview("SeekBinding", b, .5);
            Assert.That(first.localPosition, Is.EqualTo(baseline));
            Assert.That((bool)InvokeBindingPreview("ContainsBinding", a), Is.False);
            InvokePreview("Stop");
            Assert.That(EditorJsonUtility.ToJson(host), Is.EqualTo(original));
            Assert.That(second.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Embedded_SinglePreviewAndEndpointWriteOnlySelectedFieldWithUndo()
        {
            var host = Create<TweenAnimationTestHost>();
            var target = Create<Transform>();
            target.localPosition = new Vector3(5, 6, 7);
            var baseline = target.localPosition;
            host.First.SetAnimations(new[] { Item(TweenType.LocalMove, target) });
            host.Second.SetAnimations(new[] { Item(TweenType.LocalMove, target) });
            var second = JsonUtility.ToJson(host.Second);
            host.gameObject.SetActive(true);
            target.gameObject.SetActive(true);
            var binding = EmbeddedBinding(host, "First");
            const string path = "First.m_Sequence.Array.data[0]";
            InvokePreview("StartSingleBinding", binding, path);
            Assert.That((bool)m_PreviewType.GetProperty("IsSingle", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.True);
            InvokePreview("Stop");
            InvokePreview("StartEndpointBinding", binding, path, true, true);
            InvokePreview("MoveEndpoint", new Vector3(8, 9, 10));
            InvokePreview("Stop");
            Assert.That(target.localPosition, Is.EqualTo(baseline));
            using (var data = new SerializedObject(host))
                AssertVector(data.FindProperty(path + ".ToValue").vector4Value, new Vector4(8, 9, 10, 1));
            Assert.That(JsonUtility.ToJson(host.Second), Is.EqualTo(second));
            Undo.PerformUndo();
            using (var data = new SerializedObject(host))
                AssertVector(data.FindProperty(path + ".ToValue").vector4Value, Vector4.one);
        }

        [Test]
        public void Embedded_StaggerAndClipboardUseSelectedFieldIncludingArrayPath()
        {
            var host = Create<TweenAnimationTestHost>();
            var target = Create<Transform>();
            host.First.SetAnimations(new[] { Item(TweenType.Scale, target), Item(TweenType.Scale, target) });
            host.Second.SetAnimations(new[] { Item(TweenType.Scale, target) });
            host.Animations.Add(new TweenAnimation());
            host.Animations[0].SetAnimations(new[] { Item(TweenType.Scale, target) });
            var second = JsonUtility.ToJson(host.Second);
            var inspector = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            inspector.GetMethod("ApplyStaggerBinding", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { EmbeddedBinding(host, "First"), 0, 1, .2f });
            Assert.That(JsonUtility.ToJson(host.Second), Is.EqualTo(second));
            using (var data = new SerializedObject(host))
            {
                Assert.That(data.FindProperty("First.m_Sequence.Array.data[1].InsertTime").floatValue, Is.EqualTo(.2f));
                var first = data.FindProperty("First.m_Sequence.Array.data[0]");
                InvokeDrawer("CopyParameters", first);
                var item = data.FindProperty("Animations.Array.data[0].m_Sequence.Array.data[0]");
                InvokeDrawer("PasteParameters", item);
                var bindingType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationBinding");
                var binding = bindingType.GetMethod("ForItem", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { item });
                Assert.That(bindingType.GetProperty("Animation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binding), Is.SameAs(host.Animations[0]));
            }
            Undo.PerformUndo();
            Assert.That(host.First.CaptureTimeline().Entries[1].Operation, Is.EqualTo(TweenAnimation.AddType.Append));
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Embedded_InspectorDrawsTwoExpandedFieldsWithoutLayoutErrors()
            => DrawEmbeddedInspector(false);

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Embedded_InspectorDrawsOnRepaintWithoutLayout()
            => DrawEmbeddedInspector(true);

        private System.Collections.IEnumerator DrawEmbeddedInspector(bool repaintOnly)
        {
            var host = Create<TweenAnimationTestHost>();
            host.First.SetAnimations(new[] { Item(TweenType.Scale, Create<Transform>()) });
            host.Second.SetAnimations(new[] { Item(TweenType.Move, Create<Transform>()) });
            var window = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
            window.RepaintOnly = repaintOnly;
            using (var data = new SerializedObject(host))
            {
                data.FindProperty("First").isExpanded = true;
                data.FindProperty("Second").isExpanded = true;
                data.FindProperty("First.m_Sequence.Array.data[0]").isExpanded = true;
                data.FindProperty("Second.m_Sequence.Array.data[0]").isExpanded = true;
                window.Data = data;
                window.position = new Rect(100, 100, 550, 1000);
                window.ShowUtility();
                try
                {
                    for (var i = 0; i < 5; i++) { window.Repaint(); yield return null; }
                    Assert.That(window.Error, Is.Null);
                    Assert.That(window.Draws, Is.GreaterThan(0));
                }
                finally { window.Data = null; window.Close(); }
            }
        }

        [Test]
        public void Embedded_DrawerRebuildsStateForNewSerializedObjectAndTargetSet()
        {
            var first = Create<TweenAnimationTestHost>();
            var second = Create<TweenAnimationTestHost>();
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationDrawer");
            var drawer = Activator.CreateInstance(type, true);
            var method = type.GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
            using (var one = new SerializedObject(first))
            using (var fresh = new SerializedObject(first))
            using (var multi = new SerializedObject(new UnityEngine.Object[] { first, second }))
            {
                var a = method.Invoke(drawer, new object[] { one.FindProperty("First") });
                Assert.That(method.Invoke(drawer, new object[] { one.FindProperty("First") }), Is.SameAs(a));
                Assert.That(method.Invoke(drawer, new object[] { one.FindProperty("Second") }), Is.Not.SameAs(a));
                var b = method.Invoke(drawer, new object[] { fresh.FindProperty("First") });
                var c = method.Invoke(drawer, new object[] { multi.FindProperty("First") });
                Assert.That(a, Is.Not.SameAs(b));
                Assert.That(b, Is.Not.SameAs(c));
                var data = c.GetType().GetField("Data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
                Assert.That(data, Is.SameAs(multi));
                Assert.That(method.Invoke(drawer, new object[] { one.FindProperty("First") }), Is.SameAs(a),
                    "不同 Inspector 的 SerializedObject 不应相互替换字段状态。");
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Embedded_RepaintRequestsAreLocalAndCoalesced()
        {
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenInspectorRepaint");
            var request = type.GetMethod("Request", BindingFlags.Static | BindingFlags.NonPublic);
            var pending = type.GetField("Pending", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var count = pending.GetType().GetProperty("Count");
            var contains = pending.GetType().GetMethod("Contains");
            var window = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
            var other = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
            try
            {
                var before = (int)count.GetValue(pending);
                request.Invoke(null, new object[] { window });
                request.Invoke(null, new object[] { window });
                request.Invoke(null, new object[] { null });
                Assert.That((int)count.GetValue(pending), Is.EqualTo(before + 1));
                Assert.That(contains.Invoke(pending, new object[] { other }), Is.False);
                var deadline = EditorApplication.timeSinceStartup + 2;
                while ((bool)contains.Invoke(pending, new object[] { window }) && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(contains.Invoke(pending, new object[] { window }), Is.False);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(other); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Embedded_LayoutMatchesDrawingAtBothWidthsAndAllFoldouts()
            => VerifyLayoutAtBothWidthsAndAllFoldouts(false);

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Component_LayoutMatchesDrawingAtBothWidthsAndAllFoldouts()
            => VerifyLayoutAtBothWidthsAndAllFoldouts(true);

        private System.Collections.IEnumerator VerifyLayoutAtBothWidthsAndAllFoldouts(bool component)
        {
            Object host;
            string path;
            if (component)
            {
                var owner = Create<TweenAnimationComponent>();
                owner.Animation.SetAnimations(new[] { Item(TweenType.Scale, Create<Transform>()) });
                host = owner;
                path = "m_Animation";
            }
            else
            {
                var owner = Create<TweenAnimationTestHost>();
                owner.First.SetAnimations(new[] { Item(TweenType.Scale, Create<Transform>()) });
                host = owner;
                path = "First";
            }
            var before = EditorJsonUtility.ToJson(host);
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var window = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
            using (var data = new SerializedObject(host))
            {
                var panel = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { data, path, (Action)window.Repaint }, null);
                Assert.That(type.GetProperty("HasComponentLifecycle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel), Is.EqualTo(component));
                window.Data = data;
                window.Panel = panel;
                window.RepaintOnly = true;
                data.FindProperty(path + ".m_Sequence.Array.data[0]").isExpanded = true;
                foreach (var name in new[] { "m_OnPlay", "m_OnUpdate", "m_OnComplete" })
                    data.FindProperty(path + "." + name).isExpanded = true;
                window.ShowUtility();
                try
                {
                    foreach (var width in new[] { 280f, 550f })
                    {
                        window.position = new Rect(100, 100, width, 1000);
                        for (var mask = 0; mask < 64; mask++)
                        {
                            var names = new[] { "m_ShowSettings", "m_ShowAdvanced", "m_ShowReverse", "m_ShowStagger", "m_ShowEvents", "m_ShowLifecycle" };
                            for (var bit = 0; bit < names.Length; bit++)
                                type.GetField(names[bit], BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, (mask & (1 << bit)) != 0);
                            var draws = window.Draws;
                            window.Repaint();
                            for (var frame = 0; frame < 10 && window.Draws == draws; frame++) yield return null;
                            Assert.That(window.Error, Is.Null);
                            Assert.That(window.Draws, Is.GreaterThan(draws));
                            Assert.That(window.DrawingWindow, Is.SameAs(window), "必须定位实际绘制窗口，而不是焦点窗口。");
                            Assert.That(window.DrawnHeight, Is.EqualTo(window.MeasuredHeight).Within(.01f), $"width={width}, mask={mask}");
                        }
                    }
                }
                finally
                {
                    window.Data = null;
                    type.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                    window.Close();
                }
                Assert.That(data.ApplyModifiedProperties(), Is.False);
                Assert.That(EditorJsonUtility.ToJson(host), Is.EqualTo(before));
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Preview_UpdateDrivesContinuousRepaintAndStopsWhenPaused()
        {
            var host = Create<TweenAnimationTestHost>();
            var target = Create<Transform>();
            var item = Item(TweenType.Scale, target);
            item.DurationOrSpeed = 10;
            host.First.SetAnimations(new[] { item });
            host.gameObject.SetActive(true);
            target.gameObject.SetActive(true);
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var repaintType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenInspectorRepaint");
            var window = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
            using (var data = new SerializedObject(host))
            {
                var panel = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { data, "First", (Action)window.Repaint }, null);
                window.Data = data;
                window.Panel = panel;
                window.RepaintOnly = true;
                window.position = new Rect(100, 100, 550, 700);
                window.ShowUtility();
                try
                {
                    var binding = EmbeddedBinding(host, "First");
                    InvokePreview("SeekBinding", binding, 0d);
                    InvokePreview("Resume");
                    window.Repaint();
                    var until = EditorApplication.timeSinceStartup + 1;
                    while (EditorApplication.timeSinceStartup < until) yield return null;
                    Assert.That(window.Error, Is.Null);
                    Assert.That(window.RepaintTimes.Count, Is.GreaterThan(3), "不手动 Repaint 时，预览 Update 仍应连续刷新窗口。");
                    var intervals = new List<double>();
                    for (var i = 1; i < window.RepaintTimes.Count; i++)
                        intervals.Add((window.RepaintTimes[i] - window.RepaintTimes[i - 1]) * 1000);
                    intervals.Sort();
                    Debug.Log($"TweenPreview refresh: samples={intervals.Count}, medianMs={intervals[intervals.Count / 2]:0.###}, maxMs={intervals[intervals.Count - 1]:0.###}");
                    InvokePreview("Pause");
                    InvokePreview("Update");
                    var views = repaintType.GetField("PreviewViews", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    Assert.That((int)views.GetType().GetProperty("Count").GetValue(views), Is.Zero,
                        "暂停后不得继续保留连续重绘订阅。");
                    InvokePreview("Resume");
                    window.Repaint();
                    for (var frame = 0; frame < 10 && (int)views.GetType().GetProperty("Count").GetValue(views) == 0; frame++) yield return null;
                    Assert.That((int)views.GetType().GetProperty("Count").GetValue(views), Is.GreaterThan(0));
                }
                finally
                {
                    window.Data = null;
                    type.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                    InvokePreview("Stop");
                    window.Close();
                }
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator Embedded_LargeListDrawingMeasurement()
        {
            var host = Create<TweenAnimationTestHost>();
            var target = Create<Transform>();
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationInspector");
            var window = ScriptableObject.CreateInstance<TweenAnimationInspectorProbe>();
            window.position = new Rect(100, 100, 550, 1000);
            window.RepaintOnly = true;
            window.ShowUtility();
            try
            {
                foreach (var size in new[] { 10, 100 })
                {
                    var items = new Animation[size];
                    for (var i = 0; i < size; i++) items[i] = Item(TweenType.Scale, target);
                    host.First.SetAnimations(items);
                    using (var data = new SerializedObject(host))
                    {
                        var panel = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                            new object[] { data, "First", (Action)window.Repaint }, null);
                        window.Data = data;
                        window.Panel = panel;
                        try
                        {
                            foreach (var expanded in new[] { false, true })
                            {
                                for (var i = 0; i < size; i++) data.FindProperty($"First.m_Sequence.Array.data[{i}]").isExpanded = expanded;
                                var milliseconds = 0d;
                                var heightMilliseconds = 0d;
                                for (var sample = 0; sample < 5; sample++)
                                {
                                    var draws = window.Draws;
                                    window.Repaint();
                                    for (var frame = 0; frame < 10 && window.Draws == draws; frame++) yield return null;
                                    Assert.That(window.Error, Is.Null);
                                    Assert.That(window.Draws, Is.GreaterThan(draws));
                                    milliseconds += window.RepaintMilliseconds;
                                    heightMilliseconds += window.HeightMilliseconds;
                                }
                                Debug.Log($"TweenInspector Repaint: items={size}, expanded={expanded}, samples=5, meanMs={milliseconds / 5:0.###}");
                                Debug.Log($"TweenInspector Height+Repaint: items={size}, expanded={expanded}, samples=5, heightMs={heightMilliseconds / 5:0.###}, totalMs={(heightMilliseconds + milliseconds) / 5:0.###}");
                                if (size == 100)
                                {
                                    Assert.That(window.DrawnItems, Is.LessThan(50), "不可见动画项不应继续绘制。");
                                    Assert.That(window.LastDrawnIndex, Is.LessThan(99));
                                    if (expanded) Assert.That(window.DrawnItems, Is.LessThan(10));
                                    window.ScrollToBottom = true;
                                    var draws = window.Draws;
                                    window.Repaint();
                                    for (var frame = 0; frame < 10 && window.Draws == draws; frame++) yield return null;
                                    Assert.That(window.Error, Is.Null);
                                    Assert.That(window.Draws, Is.GreaterThan(draws));
                                    Assert.That(window.LastDrawnIndex, Is.EqualTo(99), "滚动到底部必须绘制最后一项。");
                                    window.ScrollToBottom = false;
                                }
                            }
                        }
                        finally
                        {
                            window.Data = null;
                            type.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                        }
                    }
                }
            }
            finally { window.Close(); }
        }

        [Test]
        public void Authoring_NewSequenceIsLinearAndExistingEaseIsNotMigrated()
        {
            var sequence = Sequence(Item(TweenType.MoveX, Create<Transform>()));
            using (var data = new SerializedObject(sequence))
            {
                var ease = data.FindProperty("m_Animation.m_Ease");
                Assert.That(ease.intValue, Is.EqualTo((int)Ease.Linear));
                ease.intValue = (int)Ease.OutQuad;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(sequence.Animation.CaptureTimeline().Ease, Is.EqualTo(Ease.OutQuad));
            }
        }

        [TestCase(TweenAnimation.TriggerMode.Restart)]
        [TestCase(TweenAnimation.TriggerMode.IgnoreWhilePlaying)]
        [TestCase(TweenAnimation.TriggerMode.ContinueOrReverse)]
        public void Authoring_TriggerPolicyDoesNotChangeExplicitPlayback(TweenAnimation.TriggerMode policy)
        {
            var target = Create<Transform>();
            var sequence = Sequence(Item(TweenType.MoveX, target));
            using (var data = new SerializedObject(sequence))
            {
                data.FindProperty("m_Animation.m_TriggerMode").intValue = (int)policy;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var first = sequence.Animation.Trigger();
            first.Time = .4;
            var second = sequence.Animation.Trigger();
            if (policy == TweenAnimation.TriggerMode.Restart) Assert.That(second, Is.Not.EqualTo(first));
            else
            {
                Assert.That(second, Is.EqualTo(first));
                Assert.That(second.Time, Is.EqualTo(.4));
                Assert.That(second.PlaybackSpeed, Is.EqualTo(policy == TweenAnimation.TriggerMode.ContinueOrReverse ? -1 : 1));
            }
            var explicitPlay = sequence.Animation.DOPlay();
            Assert.That(explicitPlay, Is.Not.EqualTo(second));
            Assert.That(second.IsActive(), Is.False);
        }

        [Test]
        public void Authoring_IgnoreTriggerAllowsReplayAfterEndpoint()
        {
            var sequence = Sequence(Item(TweenType.MoveX, Create<Transform>()));
            using (var data = new SerializedObject(sequence))
            {
                data.FindProperty("m_Animation.m_TriggerMode").intValue = (int)TweenAnimation.TriggerMode.IgnoreWhilePlaying;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            sequence.Trigger();
            var first = sequence.Animation.Handle;
            first.Time = first.TotalDuration;
            sequence.Animation.MaintainReversibleEndpoint();
            sequence.Trigger();
            Assert.That(sequence.Animation.Handle, Is.Not.EqualTo(first));
        }

        private List<string> AuthoringWarnings(TweenAnimationComponent sequence)
        {
            using (var data = new SerializedObject(sequence))
                return (List<string>)m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationDiagnostics")
                    .GetMethod("Collect", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { data.FindProperty("m_Animation") });
        }

        [Test]
        public void Authoring_WarnsForOverlapAndCurrentValueButNotIndependentAxes()
        {
            var target = Create<Transform>();
            var first = Item(TweenType.Move, target);
            var second = Item(TweenType.LocalMoveX, target);
            second.AddType = TweenAnimation.AddType.Join;
            var sequence = Sequence(first, second);
            Assert.That(AuthoringWarnings(sequence).Exists(w => w.Contains("时间重叠")), Is.True);
            second.AddType = TweenAnimation.AddType.Append;
            second.UseFromValue = false;
            Assert.That(AuthoringWarnings(sequence).Exists(w => w.Contains("构建整段序列")), Is.True);
            first.AnimationType = TweenType.MoveY;
            second.AddType = TweenAnimation.AddType.Join;
            Assert.That(AuthoringWarnings(sequence).Count, Is.Zero);
        }

        [Test]
        public void Authoring_LayoutWarningDoesNotModifyLayoutOrConfiguration()
        {
            var parent = Create<RectTransform>();
            var group = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            var child = Create<RectTransform>();
            child.SetParent(parent, false);
            parent.gameObject.SetActive(true);
            child.gameObject.SetActive(true);
            var sequence = Sequence(Item(TweenType.AnchorPosition, child));
            var original = JsonUtility.ToJson(sequence.Animation);
            Assert.That(AuthoringWarnings(sequence).Exists(w => w.Contains("LayoutGroup")), Is.True);
            Assert.That(group.enabled, Is.True);
            Assert.That(JsonUtility.ToJson(sequence.Animation), Is.EqualTo(original));
            child.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Assert.That(AuthoringWarnings(sequence).Count, Is.Zero);
            var fitter = child.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sequence.Animation.SetAnimations(new[] { Item(TweenType.SizeDelta, child) });
            Assert.That(AuthoringWarnings(sequence).Exists(w => w.Contains("ContentSizeFitter")), Is.True);
        }

        [Test]
        public void Editor_DoesNotOwnBusinessEffectBuildersOrTemplateWindow()
        {
            foreach (var name in new[] { "TweenAnimationTemplates", "TweenAnimationPresets", "TweenAnimationTemplateWindow" })
                Assert.That(m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween." + name), Is.Null);
        }

        private object EmbeddedBinding(UnityEngine.Object owner, string path)
        {
            var type = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationBinding");
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { owner, path }, null);
        }

        private object InvokeBindingPreview(string name, object binding) =>
            m_PreviewType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { binding });

        private static Animation Item(TweenType type, Component target) => new Animation
        {
            AnimationType = type, Target = target, UseFromValue = true,
            FromValue = new Vector4(.7f, .8f, .9f, .6f),
            ToValue = Vector4.one, DurationOrSpeed = 1f
        };

        private Component CreatePropertyComponent(TweenType type, out Component from, out Component to)
        {
            var required = TweenAnimation.GetRequiredComponentType(type);
            if (required == typeof(Graphic)) required = typeof(Image);
            from = Create(required);
            to = Create(required);
            return Create(required);
        }

        private TweenAnimationComponent Sequence(params Animation[] items)
        {
            var sequence = Create<TweenAnimationComponent>();
            typeof(TweenAnimation).GetField("m_Sequence", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sequence.Animation, items);
            return sequence;
        }

        private T Create<T>() where T : Component => (T)Create(typeof(T));

        private Component Create(Type type)
        {
            var ui = type == typeof(RectTransform) || typeof(Graphic).IsAssignableFrom(type)
                || type == typeof(LayoutElement) || type == typeof(Slider);
            var go = ui ? new GameObject("TweenEditorTests", typeof(RectTransform)) : new GameObject("TweenEditorTests");
            go.SetActive(false);
            m_Objects.Add(go);
            SceneManager.MoveGameObjectToScene(go, m_Scene);
            var component = go.GetComponent(type);
            if (component == null) component = go.AddComponent(type);
            Assert.That(component != null, Is.True, "测试夹具未能创建组件：" + type.Name);
            return component;
        }

        private static Type FindPreviewType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationPreview");
                if (type != null) return type;
            }
            return null;
        }

        private void StartPreview(TweenAnimationComponent[] sequences) => InvokePreview("Start", sequences, false);

        private static void SeekPreview(TweenAnimationComponent sequence, double time)
        {
            var handle = (MotionHandle)typeof(TweenAnimation).GetField("m_Motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sequence.Animation);
            handle.Time = time;
        }

        private void InvokePreview(string name, params object[] arguments)
        {
            if (name == "Start" || name == "StartReverse")
            {
                var components = (TweenAnimationComponent[])arguments[0];
                var bindingType = m_PreviewType.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenAnimationBinding");
                var bindings = Array.CreateInstance(bindingType, components.Length);
                for (var i = 0; i < components.Length; i++) bindings.SetValue(EmbeddedBinding(components[i], "m_Animation"), i);
                arguments[0] = bindings;
                name = name == "Start" ? "StartBindings" : "StartReverseBindings";
            }
            else if (arguments.Length > 0 && arguments[0] is TweenAnimationComponent component)
            {
                arguments[0] = EmbeddedBinding(component, "m_Animation");
                name += "Binding";
            }
            var method = m_PreviewType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "预览 API 缺失：" + name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static void AssertVector(Vector4 actual, Vector4 expected)
        {
            Assert.That(Vector4.Distance(actual, expected), Is.LessThan(.0001f), $"实际 {actual}，预期 {expected}");
        }
    }
}
