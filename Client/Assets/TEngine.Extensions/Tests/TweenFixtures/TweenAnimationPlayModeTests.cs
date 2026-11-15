using System.Collections;
using System.Reflection;
using LitMotion;
using NUnit.Framework;
using TEngine.Extensions.Tween;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Animation = TEngine.Extensions.Tween.TweenAnimation.SequenceAnimation;

namespace TEngine.Extensions.Tests.TweenEditor
{
    public sealed class TweenAnimationPlayModeTests
    {
        private GameObject m_HostObject, m_TargetObject;
        private TweenAnimationTestHost m_Host;
        [SetUp]
        public void SetUp()
        {
            m_HostObject = new GameObject("TweenAnimationPlayModeHost");
            m_Host = m_HostObject.AddComponent<TweenAnimationTestHost>();
            m_TargetObject = new GameObject("TweenAnimationPlayModeTarget");
            m_Host.First.SetAnimations(new[] { Item(TweenAnimation.TweenType.MoveX) });
            m_Host.Second.SetAnimations(new[] { Item(TweenAnimation.TweenType.ScaleY) });
            m_Host.First.Initialize(m_Host);
            m_Host.Second.Initialize(m_Host);
        }
        [TearDown]
        public void TearDown()
        {
            if (m_Host != null) { m_Host.First.DOKill(); m_Host.Second.DOKill(); }
            if (m_HostObject != null) Object.DestroyImmediate(m_HostObject);
            if (m_TargetObject != null) Object.DestroyImmediate(m_TargetObject);
        }
        private Animation Item(TweenAnimation.TweenType type) => new Animation
        {
            AnimationType = type, Target = m_TargetObject.transform, UseFromValue = true,
            FromValue = Vector4.zero, ToValue = Vector4.one, DurationOrSpeed = .1f, Ease = Ease.Linear
        };
        [UnityTest]
        public IEnumerator OwnerDisableCancelsBothAndEnableAllowsReplay()
        {
            var a = m_Host.First.DOPlay();
            var b = m_Host.Second.DOPlay();
            m_Host.enabled = false;
            yield return null;
            yield return null;
            Assert.That(a.IsActive(), Is.False);
            Assert.That(b.IsActive(), Is.False);
            m_Host.enabled = true;
            var replay = m_Host.First.DOPlay();
            Assert.That(replay.IsActive(), Is.True);
        }

        [UnityTest]
        public IEnumerator RelativePlaybackSnapshotsOffsetAndResamplesAfterKill()
        {
            var item = Item(TweenAnimation.TweenType.MoveX);
            item.Relative = true;
            item.UseFromValue = false;
            item.ToValue = new Vector4(6, 0, 0, 0);
            item.DurationOrSpeed = .1f;
            m_TargetObject.transform.position = new Vector3(10, 8, 9);
            m_Host.First.SetAnimations(new[] { item });
            var motion = m_Host.First.PlayForward();
            var deadline = Time.realtimeSinceStartup + 3;
            while (motion.Time < motion.TotalDuration && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(16).Within(.001));
            Assert.That(m_Host.First.PlayBackward(), Is.EqualTo(motion));
            deadline = Time.realtimeSinceStartup + 3;
            while (motion.Time > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(10).Within(.001));
            m_Host.First.DOKill();
            m_TargetObject.transform.position = new Vector3(20, 8, 9);
            motion = m_Host.First.DOPlay();
            deadline = Time.realtimeSinceStartup + 3;
            while (motion.Time < motion.TotalDuration && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(26).Within(.001));
        }
        [UnityTest]
        public IEnumerator DestroyHostComponentCancelsAnimationWithExternalTarget()
        {
            var motion = m_Host.First.DOPlay();
            Object.Destroy(m_Host);
            yield return null;
            yield return null;
            Assert.That(motion.IsActive(), Is.False);
            Assert.That(m_TargetObject, Is.Not.Null);
        }
        [UnityTest]
        public IEnumerator DirectionHoldsEndpointsAndCompleteCallbackFiresOnce()
        {
            var count = 0;
            ((UnityEvent)typeof(TweenAnimation).GetField("m_OnComplete", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m_Host.First))
                .AddListener(() => count++);
            var motion = m_Host.First.PlayForward();
            var deadline = Time.realtimeSinceStartup + 3;
            while (motion.Time < motion.TotalDuration && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(motion.IsActive(), Is.True);
            Assert.That(motion.PlaybackSpeed, Is.Zero);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(m_Host.First.PlayBackward(), Is.EqualTo(motion));
            deadline = Time.realtimeSinceStartup + 3;
            while (motion.Time > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(motion.Time, Is.Zero);
            Assert.That(motion.PlaybackSpeed, Is.Zero);
            Assert.That(count, Is.EqualTo(1));
        }
        [Test]
        public void SameFrameDisableEnableCancelsSynchronouslyAndDisposeUnregisters()
        {
            var motion = m_Host.First.DOPlay();
            m_Host.enabled = false;
            m_Host.enabled = true;
            Assert.That(motion.IsActive(), Is.False);
            var driverType = typeof(TweenAnimation).Assembly.GetType("TEngine.Extensions.Tween.TweenAnimationDriver");
            var driver = m_Host.GetComponent(driverType);
            var list = (System.Collections.IList)driverType.GetField("m_Animations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            Assert.That(list.Count, Is.EqualTo(2));
            m_Host.First.Dispose();
            Assert.That(list.Count, Is.EqualTo(1));
            m_Host.First.Initialize(m_Host);
            Assert.That(list.Count, Is.EqualTo(2));
        }

        [Test]
        public void InitializeIsIdempotentAndStopDoesNotCancelSibling()
        {
            var a = m_Host.First.DOPlay();
            var b = m_Host.Second.DOPlay();
            m_Host.First.Initialize(m_Host);
            Assert.That(m_Host.First.Handle, Is.EqualTo(a));
            m_Host.First.DOKill();
            Assert.That(a.IsActive(), Is.False);
            Assert.That(b.IsActive(), Is.True);
        }

        [Test]
        public void KillRetainsCurrentValueClearsDirectionAndDoesNotComplete()
        {
            var item = Item(TweenAnimation.TweenType.MoveX);
            item.DurationOrSpeed = 1;
            var completions = 0;
            item.OnComplete.AddListener(() => completions++);
            m_Host.First.SetAnimations(new[] { item });
            var motion = m_Host.First.PlayBackward();
            motion.Time = .4;
            Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(.4f).Within(.001));
            m_Host.First.DOKill();
            m_Host.First.DOKill();
            Assert.That(motion.IsActive(), Is.False);
            Assert.That(m_Host.First.Handle.IsValid(), Is.False);
            Assert.That(m_Host.First.IsBackward, Is.False);
            Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(.4f).Within(.001));
            Assert.That(completions, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CoreResetResamplesStartWithoutCancellingOrRebuildingMotion(int source)
        {
            var marker = new GameObject("LifecycleStartReference");
            try
            {
                var item = Item(TweenAnimation.TweenType.MoveX);
                item.DurationOrSpeed = 1;
                item.FromValue = new Vector4(2, 0, 0, 0);
                item.ToValue = new Vector4(10, 0, 0, 0);
                item.UseFromValue = source == 1;
                item.UseFromTarget = source == 2;
                item.FromTarget = marker.transform;
                marker.transform.position = Vector3.right * 3;
                m_TargetObject.transform.position = Vector3.right * 4;
                m_Host.First.SetAnimations(new[] { item });
                var motion = m_Host.First.DOPlay();
                motion.Time = .5;
                var current = m_TargetObject.transform.position.x;
                marker.transform.position = Vector3.right * 6;
                m_Host.First.ResetToFromValue();
                Assert.That(m_Host.First.Handle, Is.EqualTo(motion));
                Assert.That(motion.IsActive(), Is.True);
                Assert.That(motion.Time, Is.EqualTo(.5));
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(source == 0 ? current : source == 1 ? 2 : 6).Within(.001));
                motion.Time = .75;
                var capturedStart = source == 0 ? 4 : source == 1 ? 2 : 3;
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(Mathf.Lerp(capturedStart, 10, .75f)).Within(.001));
            }
            finally { Object.DestroyImmediate(marker); }
        }

        [Test]
        public void ComponentResetStopsMotionBeforeApplyingStart()
        {
            var component = m_HostObject.AddComponent<TweenAnimationComponent>();
            var item = Item(TweenAnimation.TweenType.MoveX);
            item.DurationOrSpeed = 1;
            component.Animation.SetAnimations(new[] { item });
            var motion = component.PlayForward();
            motion.Time = .5;
            component.ResetToStart();
            Assert.That(motion.IsActive(), Is.False);
            Assert.That(component.Animation.Handle.IsValid(), Is.False);
            Assert.That(m_TargetObject.transform.position.x, Is.Zero);
        }

        [Test]
        public void FirstBackwardSamplesReferencesOnceAndReplayResamplesAfterKill()
        {
            var from = new GameObject("LifecycleFrom");
            var to = new GameObject("LifecycleTo");
            try
            {
                from.transform.position = Vector3.right * 2;
                to.transform.position = Vector3.right * 10;
                var item = Item(TweenAnimation.TweenType.MoveX);
                item.DurationOrSpeed = 1;
                item.UseFromTarget = true; item.FromTarget = from.transform;
                item.UseToTarget = true; item.ToTarget = to.transform;
                var completions = 0;
                item.OnComplete.AddListener(() => completions++);
                m_Host.First.SetAnimations(new[] { item });
                var motion = m_Host.First.PlayBackward();
                Assert.That(motion.Time, Is.EqualTo(1));
                Assert.That(motion.PlaybackSpeed, Is.EqualTo(-1));
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(10).Within(.001));
                Assert.That(completions, Is.Zero, "首次倒播定位结束值不应触发完成事件。");
                from.transform.position = Vector3.right * 4;
                to.transform.position = Vector3.right * 20;
                motion.Time = .5;
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(6).Within(.001));
                Assert.That(m_Host.First.PlayForward(), Is.EqualTo(motion));
                Assert.That(motion.Time, Is.EqualTo(.5));
                m_Host.First.DOKill();
                var replay = m_Host.First.PlayBackward();
                Assert.That(replay, Is.Not.EqualTo(motion));
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(20).Within(.001));
                replay.Time = .5;
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(12).Within(.001));
            }
            finally { Object.DestroyImmediate(from); Object.DestroyImmediate(to); }
        }

        [Test]
        public void DisposeIsIdempotentAndAllowsRebindingToAnotherOwner()
        {
            var replacement = new GameObject("ReplacementTweenOwner");
            try
            {
                var nextOwner = replacement.AddComponent<TweenAnimationTestHost>();
                var animation = m_Host.First;
                var motion = animation.DOPlay();
                Assert.Throws<System.InvalidOperationException>(() => animation.Initialize(nextOwner));
                Assert.That(motion.IsActive(), Is.True);
                animation.Dispose(); animation.Dispose();
                Assert.That(motion.IsActive(), Is.False);
                Assert.Throws<System.InvalidOperationException>(() => animation.DOPlay());
                var driverType = typeof(TweenAnimation).Assembly.GetType("TEngine.Extensions.Tween.TweenAnimationDriver");
                var oldDriver = m_Host.GetComponent(driverType);
                var oldList = (IList)driverType.GetField("m_Animations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(oldDriver);
                Assert.That(oldList.Count, Is.EqualTo(1));
                m_Host.First = new TweenAnimation();
                animation.Initialize(nextOwner);
                animation.Initialize(nextOwner);
                nextOwner.Animations.Add(animation);
                var nextDriver = nextOwner.GetComponent(driverType);
                var nextList = (IList)driverType.GetField("m_Animations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(nextDriver);
                Assert.That(nextList.Count, Is.EqualTo(1));
                var replay = animation.DOPlay();
                m_Host.enabled = false;
                Assert.That(replay.IsActive(), Is.True);
                Assert.That(oldList.Contains(animation), Is.False);
                animation.Dispose();
                Assert.That(replay.IsActive(), Is.False);
                Assert.That(nextList.Count, Is.Zero);
            }
            finally { Object.DestroyImmediate(replacement); }
        }

        [UnityTest]
        public IEnumerator DestroyExternalTargetCancelsWholeSequenceWithoutCompleting()
        {
            var item = Item(TweenAnimation.TweenType.MoveX);
            item.DurationOrSpeed = 10;
            var completions = 0;
            item.OnComplete.AddListener(() => completions++);
            m_Host.First.SetAnimations(new[] { item });
            var motion = m_Host.First.PlayForward();
            Object.Destroy(m_TargetObject);
            yield return null;
            yield return null;
            Assert.That(motion.IsActive(), Is.False);
            Assert.That(completions, Is.Zero);
            Assert.That(m_Host, Is.Not.Null);
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ComponentAwakeAppliesCoreSettingsOnlyOnce(bool play, bool reset)
        {
            var go = new GameObject("TweenAnimationComponentAwake");
            go.SetActive(false);
            try
            {
                var component = go.AddComponent<TweenAnimationComponent>();
                component.Animation.SetAnimations(new[] { Item(TweenAnimation.TweenType.MoveX) });
                typeof(TweenAnimation).GetField("m_PlayOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component.Animation, play);
                typeof(TweenAnimation).GetField("m_ResetOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component.Animation, reset);
                m_TargetObject.transform.position = Vector3.one * 5;
                go.SetActive(true);
                Assert.That(component.Animation.Handle.IsActive(), Is.EqualTo(play));
                if (!play) Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(reset ? 0 : 5));
                component.Stop();
                m_TargetObject.transform.position = Vector3.one * 7;
                component.enabled = false;
                component.enabled = true;
                Assert.That(component.Animation.Handle.IsActive(), Is.False);
                Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(7));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AwakeSettingsRunBeforeEnableAction(
            [Values(false, true)] bool play, [Values(false, true)] bool reset,
            [Values(TweenAnimationComponent.EnableAction.Replay, TweenAnimationComponent.EnableAction.PlayForward,
                TweenAnimationComponent.EnableAction.ResetToStart)] TweenAnimationComponent.EnableAction action)
        {
            var go = new GameObject("TweenAwakeEnableOrder");
            go.SetActive(false);
            try
            {
                var component = go.AddComponent<TweenAnimationComponent>();
                var animation = component.Animation;
                animation.SetAnimations(new[] { Item(TweenAnimation.TweenType.MoveX) });
                typeof(TweenAnimation).GetField("m_PlayOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animation, play);
                typeof(TweenAnimation).GetField("m_ResetOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animation, reset);
                typeof(TweenAnimationComponent).GetField("m_OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, action);
                var plays = 0;
                ((UnityEvent)typeof(TweenAnimation).GetField("m_OnPlay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(animation))
                    .AddListener(() => plays++);
                go.SetActive(true);
                var initialPlays = (play ? 1 : 0) + (action == TweenAnimationComponent.EnableAction.Replay
                    || action == TweenAnimationComponent.EnableAction.PlayForward && !play ? 1 : 0);
                Assert.That(plays, Is.EqualTo(initialPlays));
                Assert.That(animation.Handle.IsActive(), Is.EqualTo(action != TweenAnimationComponent.EnableAction.ResetToStart));
                go.SetActive(false);
                go.SetActive(true);
                Assert.That(plays, Is.EqualTo(initialPlays + (action == TweenAnimationComponent.EnableAction.ResetToStart ? 0 : 1)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ComponentDisableAndDestroyCancelExternalTargetAndUnregister()
        {
            var component = m_HostObject.AddComponent<TweenAnimationComponent>();
            component.Animation.SetAnimations(new[] { Item(TweenAnimation.TweenType.MoveX) });
            component.Play();
            var first = component.Animation.Handle;
            component.enabled = false;
            component.enabled = true;
            Assert.That(first.IsActive(), Is.False);
            var replay = component.PlayForward();
            var animation = component.Animation;
            Object.DestroyImmediate(component);
            Assert.That(replay.IsActive(), Is.False);
            Assert.That(m_TargetObject, Is.Not.Null);
            var driverType = typeof(TweenAnimation).Assembly.GetType("TEngine.Extensions.Tween.TweenAnimationDriver");
            var driver = m_HostObject.GetComponent(driverType);
            var list = (System.Collections.IList)driverType.GetField("m_Animations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            Assert.That(list.Contains(animation), Is.False);
            Assert.That(list.Count, Is.EqualTo(2));
        }

        [TestCase(TweenAnimationComponent.EnableAction.Replay)]
        [TestCase(TweenAnimationComponent.EnableAction.PlayForward)]
        [TestCase(TweenAnimationComponent.EnableAction.ResetToStart)]
        public void ComponentEnableActionsRepeatAndDisableRestoresSpecifiedStart(TweenAnimationComponent.EnableAction action)
        {
            var go = new GameObject("TweenLifecycleActions");
            go.SetActive(false);
            try
            {
                var component = go.AddComponent<TweenAnimationComponent>();
                component.Animation.SetAnimations(new[] { Item(TweenAnimation.TweenType.MoveX) });
                typeof(TweenAnimationComponent).GetField("m_OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, action);
                typeof(TweenAnimationComponent).GetField("m_OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, TweenAnimationComponent.DisableAction.ResetToStart);
                for (var i = 0; i < 3; i++)
                {
                    m_TargetObject.transform.position = Vector3.one * 9;
                    go.SetActive(true);
                    var handle = component.Animation.Handle;
                    Assert.That(handle.IsActive(), Is.EqualTo(action != TweenAnimationComponent.EnableAction.ResetToStart));
                    if (handle.IsActive()) handle.Time = .05;
                    else Assert.That(m_TargetObject.transform.position.x, Is.Zero);
                    go.SetActive(false);
                    Assert.That(handle.IsActive(), Is.False);
                    Assert.That(m_TargetObject.transform.position.x, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ComponentPlaybackMethodsUseOneCoreHandleAndCompleteEvent()
        {
            var component = m_HostObject.AddComponent<TweenAnimationComponent>();
            component.Animation.SetAnimations(new[] { Item(TweenAnimation.TweenType.MoveX) });
            var completions = 0;
            ((UnityEvent)typeof(TweenAnimation).GetField("m_OnComplete", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component.Animation))
                .AddListener(() => completions++);
            var forward = component.PlayForward();
            Assert.That(component.PlayBackward(), Is.EqualTo(forward));
            component.SetState(true);
            Assert.That(component.Animation.Handle, Is.EqualTo(forward));
            component.Complete();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(m_TargetObject.transform.position.x, Is.EqualTo(1).Within(.001));
            component.Stop();
            Assert.That(forward.IsActive(), Is.False);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ShowcaseSavedPrefabRunsButtonsLayoutAndRepeatedEnable()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameScripts/TweenSample/Showcase/TweenShowcase.prefab");
            Assert.That(prefab, Is.Not.Null, "缺少已授权的真实示例 Prefab。");
            var copy = Object.Instantiate(prefab);
            try
            {
                var sample = copy.GetComponent("TweenShowcase");
                Assert.That(sample, Is.Not.Null);
                var popup = (TweenAnimation)sample.GetType().GetProperty("Popup").GetValue(sample);
                var feedback = (TweenAnimation)sample.GetType().GetProperty("Feedback").GetValue(sample);
                var list = (TweenAnimation)sample.GetType().GetProperty("List").GetValue(sample);
                yield return null;
                Assert.That(popup.Handle.IsActive(), Is.True);
                var first = popup.Handle;
                var time = first.Time;
                sample.SendMessage("ClosePopup");
                Assert.That(popup.Handle, Is.EqualTo(first));
                Assert.That(first.Time, Is.EqualTo(time));
                Assert.That(first.PlaybackSpeed, Is.EqualTo(-1));
                sample.SendMessage("OpenPopup");
                var button = copy.transform.Find("Examples/Example_02/ButtonVisual/Claim").GetComponent("Button");
                var click = (UnityEvent)button.GetType().GetProperty("onClick").GetValue(button);
                for (var i = 0; i < 10; i++)
                {
                    var old = feedback.Handle;
                    click.Invoke();
                    Assert.That(old.IsActive(), Is.False);
                    Assert.That(feedback.Handle.IsActive(), Is.True);
                }
                var row = copy.transform.Find("Examples/Example_03/LayoutRows/LayoutSlot_1");
                var before = row.localPosition;
                sample.SendMessage("RevealList");
                yield return null;
                yield return null;
                Assert.That(row.localPosition, Is.EqualTo(before), "动画不得移动布局外层。");
                var deadline = Time.realtimeSinceStartup + 3;
                while (list.Handle.Time < list.Handle.TotalDuration && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(list.Handle.Time, Is.GreaterThanOrEqualTo(list.Handle.TotalDuration));
                Assert.That(copy.transform.Find("Examples/Example_03/LayoutRows/LayoutSlot_4/Visual").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1).Within(.001));
                Assert.That(copy.transform.Find("Examples/Example_02/ButtonVisual").localScale.x, Is.EqualTo(1).Within(.001));
                for (var i = 0; i < 3; i++)
                {
                    var oldPopup = popup.Handle;
                    copy.SetActive(false);
                    Assert.That(oldPopup.IsActive(), Is.False);
                    Assert.That(feedback.Handle.IsActive(), Is.False);
                    Assert.That(list.Handle.IsActive(), Is.False);
                    copy.SetActive(true);
                    Assert.That(popup.Handle.IsActive(), Is.True);
                    Assert.That(list.Handle.IsActive(), Is.True);
                    yield return null;
                }
            }
            finally { Object.DestroyImmediate(copy); }
        }
#endif
    }
}
