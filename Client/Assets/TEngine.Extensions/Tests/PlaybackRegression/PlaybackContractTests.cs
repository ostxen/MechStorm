using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace TEngine.Extensions.SimpleAnimation.Tests
{
    public sealed class PlaybackContractTests
    {
        private GameObject _owner;
        private AnimationClip _clip;
        private AnimationClipLibrary _library;
        private PlayableAnimationController _controller;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("PlaybackContractTests");
            _owner.SetActive(false);
            _owner.AddComponent<Animator>();
            _controller = _owner.AddComponent<PlayableAnimationController>();
            _controller.Initialize();
            _clip = new AnimationClip();
            _clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            _library = ScriptableObject.CreateInstance<AnimationClipLibrary>();
            AddEntry("Base", 0);
            AddEntry("Upper", 1);
            _controller.BindLibrary(_library);
        }

        [TearDown]
        public void TearDown()
        {
            _controller.ResetController();
            Object.DestroyImmediate(_owner);
            Object.DestroyImmediate(_library);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void Layers_DefaultMutedAndPlayIndependently()
        {
            Assert.That(_controller.GetLayerWeight(0), Is.EqualTo(1f));
            Assert.That(_controller.GetLayerWeight(1), Is.Zero);
            _controller.SetLayerWeight(1, 0.5f);
            _controller.Play("Base", 0f);
            _controller.Play("Upper", 0f);
            Assert.That(_controller.TryGetCurrentState(0, out var lower), Is.True);
            Assert.That(_controller.TryGetCurrentState(1, out var upper), Is.True);
            Assert.That(lower.Key, Is.EqualTo("Base"));
            Assert.That(upper.Key, Is.EqualTo("Upper"));
            _controller.StopLayer(1);
            Assert.That(lower.IsPlaying, Is.True);
            Assert.That(upper.PlaybackState, Is.EqualTo(AnimationPlaybackState.Stopped));
        }

        [Test]
        public void PauseResumePreservesTimeAndStopResetsAllLayers()
        {
            _controller.Play("Base", 0f);
            _controller.Play("Upper", 0f);
            var state = _controller.GetState("Upper");
            state.Time = 0.4f;
            _controller.Pause();
            Assert.That(state.IsPlaying, Is.False);
            _controller.Resume();
            Assert.That(state.Time, Is.EqualTo(0.4f).Within(0.001f));
            _controller.Stop();
            foreach (var bound in _controller.States.Values)
            {
                Assert.That(bound.Time, Is.Zero);
                Assert.That(bound.PlaybackState, Is.EqualTo(AnimationPlaybackState.Stopped));
                Assert.That(bound.Weight, Is.Zero);
            }
        }

        [Test]
        public void CompletedIsQueuedOnceAndReplayCanCompleteAgain()
        {
            var count = 0;
            _controller.EventQueue.Completed += _ => count++;
            var state = _controller.GetState("Base");
            _controller.Play("Base", 0f);
            CompleteCurrentNode(state);
            Assert.That(count, Is.Zero);
            _controller.EventQueue.Flush();
            _controller.EventQueue.Flush();
            Assert.That(count, Is.EqualTo(1));
            Assert.That(state.IsPlaying, Is.False);
            _controller.Play(new AnimationPlaybackOptions("Base")
            {
                WrapMode = WrapMode.Once,
                FadeDuration = 0f
            });
            CompleteCurrentNode(state);
            _controller.EventQueue.Flush();
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void StopDiscardsPendingCompletion()
        {
            var count = 0;
            _controller.EventQueue.Completed += _ => count++;
            _controller.Play("Base", 0f);
            CompleteCurrentNode(_controller.GetState("Base"));
            _controller.Stop();
            _controller.EventQueue.Flush();
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void RebindInvalidatesHandleAndDoesNotRetainChildren()
        {
            var oldState = _controller.GetState("Base");
            for (var i = 0; i < 4; i++) _controller.BindLibrary(_library);
            Assert.That(oldState.IsValid, Is.False);
            Assert.Throws<ObjectDisposedException>(() => { var unused = oldState.Time; });
            var children = (List<AnimationPlayableNode>)typeof(AnimationPlayableNode)
                .GetProperty("Children", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_controller.Layers[0]);
            Assert.That(children.Count, Is.EqualTo(1));
        }

        [Test]
        public void CancellationAfterProviderReturnsReleasesUntrackedLease()
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            try
            {
                var entry = new AnimationClipCatalogEntry();
                SetField(entry, "_animationKey", "Late");
                ((List<AnimationClipCatalogEntry>)catalog.Entries).Add(entry);
                var released = 0;
                var lease = new AnimationClipLease("Late", _clip, () => released++);
                var provider = new ResettingProvider(_controller, lease);
                var task = _controller.BindCatalogAsync(catalog, provider);
                Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                Assert.That(released, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void CartesianWeights_ExactMatchClearsEarlierWeights()
        {
            var weights = new float[3];
            new CartesianBlendWeightCalculator2D().Calculate(Vector2.right,
                new[] { Vector2.zero, Vector2.right, Vector2.up }, weights);
            Assert.That(weights, Is.EqualTo(new[] { 0f, 1f, 0f }));
        }

        [Test]
        public void BlendTree_RejectsExcessChildrenAndNonFiniteParameters()
        {
            var graph = PlayableGraph.Create();
            var mixer = new AnimationBlendTree2D(graph, new[] { Vector2.zero });
            var first = new AnimationClipNode(graph, _clip);
            var extra = new AnimationClipNode(graph, _clip);
            try
            {
                mixer.AddChild(first);
                Assert.Throws<InvalidOperationException>(() => mixer.AddChild(extra));
                Assert.Throws<ArgumentOutOfRangeException>(() => mixer.Parameter = new Vector2(float.NaN, 0f));
            }
            finally
            {
                mixer.Dispose();
                extra.Dispose();
                graph.Destroy();
            }
        }

        [Test]
        public void Sequence_AutoAdvancesOnNextUpdate()
        {
            var graph = PlayableGraph.Create();
            var sequence = new AnimationSequenceNode(graph) { AutoAdvanceOnComplete = true };
            var first = new AnimationClipNode(graph, _clip, WrapMode.Once);
            var second = new AnimationClipNode(graph, _clip, WrapMode.Once);
            try
            {
                sequence.AddEntry(first);
                sequence.AddEntry(second);
                sequence.Play();
                first.Time = first.Length;
                sequence.PostUpdate(0f);
                Assert.That(sequence.CurrentIndex, Is.Zero);
                sequence.Update(0f);
                Assert.That(sequence.CurrentIndex, Is.EqualTo(1));
                second.Time = second.Length;
                sequence.PostUpdate(0f);
                sequence.Update(0f);
                Assert.That(sequence.PlaybackState, Is.EqualTo(AnimationPlaybackState.Completed));
            }
            finally
            {
                sequence.Dispose();
                graph.Destroy();
            }
        }

        [Test]
        public void PauseAtFadeStartResumesZeroWeightTarget()
        {
            _controller.Play("Upper", 1f);
            var state = _controller.GetState("Upper");
            Assert.That(state.Weight, Is.Zero);
            _controller.Pause();
            _controller.Layers[1].Update(0.5f);
            Assert.That(state.Weight, Is.Zero);
            _controller.Resume();
            Assert.That(state.IsPlaying, Is.True);
            _controller.Layers[1].Update(0.5f);
            Assert.That(state.Weight, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void MarkersCanBeRegisteredThroughStateHandle()
        {
            var state = _controller.GetState("Base");
            state.AddMarker(new AnimationEventMarker("Hit", 0.3f));
            var count = 0;
            _controller.EventQueue.EventReceived += record =>
            {
                Assert.That(record.Marker.Id, Is.EqualTo("Hit"));
                count++;
            };
            _controller.Play("Base", 0f);
            var node = _controller.Layers[0].CurrentNode;
            node.Output.SetTime(0.5d);
            node.PostUpdate(0.5f);
            _controller.EventQueue.Flush();
            Assert.That(count, Is.EqualTo(1));
            state.ClearMarkers();
        }

        [Test]
        public void ReplayBeforeFlushDiscardsOldCompletion()
        {
            var count = 0;
            _controller.EventQueue.Completed += _ => count++;
            _controller.Play("Base", 0f);
            CompleteCurrentNode(_controller.GetState("Base"));
            _controller.Play("Base", 0f);
            _controller.EventQueue.Flush();
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void ClearSubscribersRemovesCompletionCallbacks()
        {
            var count = 0;
            _controller.EventQueue.Completed += _ => count++;
            _controller.EventQueue.ClearSubscribers();
            _controller.Play("Base", 0f);
            CompleteCurrentNode(_controller.GetState("Base"));
            _controller.EventQueue.Flush();
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void JobFirstInputUsesPortZero()
        {
            var graph = PlayableGraph.Create();
            var job = new TransformOffsetAnimationJobNode(graph, _owner.GetComponent<Animator>(),
                Array.Empty<Transform>(), Array.Empty<Vector3>());
            try
            {
                var clip = new AnimationClipNode(graph, _clip);
                Assert.That(job.AddInput(clip), Is.Zero);
                Assert.That(job.Output.GetInputCount(), Is.EqualTo(1));
            }
            finally
            {
                job.Dispose();
                graph.Destroy();
            }
        }

        [Test]
        public void UnsupportedWrapModeIsRejected()
        {
            var options = new AnimationPlaybackOptions("Base") { WrapMode = WrapMode.PingPong };
            Assert.That(options.IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => _controller.Play(options));
        }

        [Test]
        public void BlendTree1D_RegistersPairedSamplesAndUsesIndependentClipNodes()
        {
            var leaseCount = _controller.CaptureSnapshot().LeaseCount;
            var handle = _controller.RegisterBlendTree("Move", MovementDefinition());
            var root = (AnimationBlendTree1D)handle.Root;
            Assert.That(_controller.Layers[0].ChildNodes.Count, Is.EqualTo(2));
            Assert.That(_controller.CaptureSnapshot().LeaseCount, Is.EqualTo(leaseCount));
            Assert.That(root.ChildNodes[0], Is.Not.SameAs(_controller.Layers[0].ChildNodes[0]));
            Assert.That(((AnimationClipNode)root.ChildNodes[0]).WrapMode, Is.EqualTo(WrapMode.Loop));
            _controller.SetBlendTreeParameter("Move", 0.25f);
            Assert.That(root.GetChildWeight(0), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(root.GetChildWeight(1), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(_controller.Play("Move", 0f), Is.True);
            Assert.That(_controller.TryGetCurrentBlendTree(0, out var current), Is.True);
            Assert.That(current, Is.SameAs(handle));
            Assert.That(_controller.TryGetCurrentState(0, out _), Is.False);
            Assert.That(_controller.TryGetCurrentKey(0, out var key), Is.True);
            Assert.That(key, Is.EqualTo("Move"));
            Assert.That(_controller.CaptureSnapshot().BlendTreeCount, Is.EqualTo(1));
            Assert.That(_controller.BuildMonitorLabelTable()[root.Output.GetHandle()], Does.Contain("Move"));
        }

        [Test]
        public void BlendTree2D_UsesCartesianOrDirectionalCalculator()
        {
            var samples = new[]
            {
                new AnimationBlendTreeSample2D("Base", Vector2.right),
                new AnimationBlendTreeSample2D("Upper", Vector2.up)
            };
            var cartesian = _controller.RegisterBlendTree("Cartesian",
                new AnimationBlendTree2DDefinition(samples));
            _controller.SetBlendTreeParameter("Cartesian", Vector2.up);
            Assert.That(cartesian.Root.GetChildWeight(0), Is.Zero);
            Assert.That(cartesian.Root.GetChildWeight(1), Is.EqualTo(1f));

            var directional = _controller.RegisterBlendTree("Direction",
                new AnimationBlendTree2DDefinition(samples, calculator: new DirectionalBlendWeightCalculator2D()));
            directional.SetParameter(Vector2.one);
            Assert.That(directional.Root.GetChildWeight(0), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(directional.Root.GetChildWeight(1), Is.EqualTo(0.5f).Within(0.0001f));
            directional.SetParameter(Vector2.zero);
            Assert.That(directional.Root.GetChildWeight(0), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(directional.Root.GetChildWeight(1), Is.EqualTo(0.5f).Within(0.0001f));
            var idle = _controller.RegisterBlendTree("IdleMove", new AnimationBlendTree2DDefinition(new[]
            {
                new AnimationBlendTreeSample2D("Base", Vector2.zero),
                new AnimationBlendTreeSample2D("Upper", Vector2.up)
            }, calculator: new DirectionalBlendWeightCalculator2D()));
            idle.SetParameter(Vector2.up * 0.25f);
            Assert.That(idle.Root.GetChildWeight(0), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(idle.Root.GetChildWeight(1), Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void BlendTree_NestedDefinitionsCreateSeparateOwnedNodes()
        {
            var childDefinition = MovementDefinition();
            var definition = new AnimationBlendTree2DDefinition(new[]
            {
                new AnimationBlendTreeSample2D(childDefinition, Vector2.zero),
                new AnimationBlendTreeSample2D(childDefinition, Vector2.right)
            });
            var handle = _controller.RegisterBlendTree("Nested", definition);
            var root = handle.Root;
            var first = (AnimationBlendTree1D)root.ChildNodes[0];
            var second = (AnimationBlendTree1D)root.ChildNodes[1];
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.ChildNodes[0], Is.Not.SameAs(second.ChildNodes[0]));
            first.Parameter = 0.4f;
            Assert.That(second.Parameter, Is.Zero);
            _controller.Play("Nested", 0f);
            Assert.That(first.IsActive, Is.True);
            Assert.That(second.IsActive, Is.False);
            handle.SetParameter(Vector2.right);
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.True);
            _controller.BindLibrary(_library);
            Assert.That(handle.IsValid, Is.False);
            Assert.That(root.IsDisposed && first.IsDisposed && second.IsDisposed, Is.True);
            Assert.That(first.ChildNodes[0].IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => handle.SetParameter(Vector2.zero));
            Assert.That(_controller.BlendTrees.Count, Is.Zero);
        }

        [Test]
        public void BlendTree_PauseAtFadeStartAndStopReplayPreserveParameter()
        {
            var handle = _controller.RegisterBlendTree("Move", MovementDefinition());
            handle.SetParameter(0.75f);
            _controller.Play("Move", 1f);
            Assert.That(handle.Weight, Is.Zero);
            _controller.Pause();
            _controller.Layers[0].Update(0.5f);
            Assert.That(handle.Weight, Is.Zero);
            _controller.Resume();
            _controller.Layers[0].Update(0.5f);
            Assert.That(handle.Weight, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(handle.Root.ChildNodes[1].IsActive, Is.True);
            _controller.Stop();
            Assert.That(handle.PlaybackState, Is.EqualTo(AnimationPlaybackState.Stopped));
            _controller.Play("Move", 0f);
            Assert.That(handle.Root.GetChildWeight(0), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(handle.Root.GetChildWeight(1), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(handle.IsPlaying, Is.True);
        }

        [Test]
        public void BlendTree_CrossFadesWithClipsWithoutChangingOtherLayers()
        {
            var handle = _controller.RegisterBlendTree("Move", MovementDefinition());
            _controller.Play("Upper", 0f);
            _controller.Play("Base", 0f);
            _controller.Play("Move", 1f);
            _controller.Layers[0].Update(0.5f);
            Assert.That(handle.Weight, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(_controller.GetState("Base").Weight, Is.EqualTo(0.5f).Within(0.0001f));
            _controller.Play("Base", 0f);
            Assert.That(handle.Weight, Is.Zero);
            Assert.That(handle.IsPlaying, Is.False);
            Assert.That(_controller.GetState("Upper").IsPlaying, Is.True);
        }

        [Test]
        public void BlendTree_RegistrationFailureReleasesPartialSubtree()
        {
            var count = _controller.Graph.Graph.GetPlayableCount();
            var childCount = _controller.Layers[0].ChildNodes.Count;
            var invalid = new AnimationBlendTree1DDefinition(new[]
            {
                new AnimationBlendTreeSample1D(MovementDefinition(), 0f),
                new AnimationBlendTreeSample1D("Missing", 1f)
            });
            Assert.Throws<KeyNotFoundException>(() => _controller.RegisterBlendTree("Broken", invalid));
            Assert.That(_controller.Graph.Graph.GetPlayableCount(), Is.EqualTo(count));
            Assert.That(_controller.Layers[0].ChildNodes.Count, Is.EqualTo(childCount));
            Assert.That(_controller.TryGetBlendTree("Broken", out _), Is.False);
        }

        [Test]
        public void BlendTree_CalculatorFailureReleasesPartialSubtree()
        {
            var count = _controller.Graph.Graph.GetPlayableCount();
            var invalid = new AnimationBlendTree2DDefinition(new[]
            {
                new AnimationBlendTreeSample2D("Base", Vector2.zero),
                new AnimationBlendTreeSample2D("Upper", Vector2.right)
            }, calculator: new ThrowingCalculator());
            Assert.Throws<InvalidOperationException>(() => _controller.RegisterBlendTree("Broken", invalid));
            Assert.That(_controller.Graph.Graph.GetPlayableCount(), Is.EqualTo(count));
            Assert.That(_controller.BlendTrees.Count, Is.Zero);
        }

        [Test]
        public void BlendTree_RejectsDuplicateKeysWrongParameterTypesAndClipOptions()
        {
            var definition = MovementDefinition();
            Assert.Throws<InvalidOperationException>(() => _controller.RegisterBlendTree("Base", definition));
            var tree = _controller.RegisterBlendTree("Move", definition, 1);
            Assert.Throws<InvalidOperationException>(() => _controller.RegisterBlendTree("Move", definition));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RegisterBlendTree("BadLayer", definition, 4));
            Assert.Throws<InvalidOperationException>(() => tree.SetParameter(Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => tree.SetParameter(float.NaN));
            Assert.Throws<NotSupportedException>(() => _controller.Play(new AnimationPlaybackOptions("Move")));
            Assert.Throws<NotSupportedException>(() => tree.Root.CrossFade(0));
            _controller.Play("Move", 0f);
            Assert.That(_controller.GetLayerWeight(1), Is.Zero);
            _controller.StopLayer(1);
            Assert.That(tree.IsPlaying, Is.False);
            _controller.ResetController();
            Assert.That(tree.IsValid, Is.False);
        }

        [Test]
        public void BlendTreeDefinitions_RejectInvalidSamplesBeforeCreatingPlayables()
        {
            Assert.Throws<ArgumentException>(() => new AnimationBlendTree1DDefinition(new[]
            {
                new AnimationBlendTreeSample1D("Base", 1f),
                new AnimationBlendTreeSample1D("Upper", 1f)
            }));
            Assert.Throws<ArgumentException>(() => new AnimationBlendTree1DDefinition(
                Array.Empty<AnimationBlendTreeSample1D>()));
            Assert.Throws<ArgumentException>(() => new AnimationBlendTree2DDefinition(new[]
            {
                new AnimationBlendTreeSample2D("Base", Vector2.zero),
                new AnimationBlendTreeSample2D("Base", Vector2.zero)
            }, calculator: new DirectionalBlendWeightCalculator2D()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationBlendTreeSample1D("Base", float.PositiveInfinity));
        }

        [Test]
        public void BlendTree1D_ClampsOutsideSamplesAndHandlesLargeFiniteRange()
        {
            var handle = _controller.RegisterBlendTree("Range", new AnimationBlendTree1DDefinition(new[]
            {
                new AnimationBlendTreeSample1D("Base", -float.MaxValue),
                new AnimationBlendTreeSample1D("Upper", float.MaxValue)
            }));
            Assert.That(handle.Root.GetChildWeight(0), Is.EqualTo(0.5f));
            handle.SetParameter(float.MaxValue);
            Assert.That(handle.Root.GetChildWeight(1), Is.EqualTo(1f));
            handle.SetParameter(-float.MaxValue);
            Assert.That(handle.Root.GetChildWeight(0), Is.EqualTo(1f));
        }

        [Test]
        public void DirectionalWeights_UncoveredDirectionUsesNearestSample()
        {
            var weights = new float[2];
            new DirectionalBlendWeightCalculator2D().Calculate(Vector2.left,
                new[] { Vector2.right, Vector2.up }, weights);
            Assert.That(weights, Is.EqualTo(new[] { 0f, 1f }));
        }

        private static AnimationBlendTree1DDefinition MovementDefinition() =>
            new AnimationBlendTree1DDefinition(new[]
            {
                new AnimationBlendTreeSample1D("Base", 0f),
                new AnimationBlendTreeSample1D("Upper", 1f)
            });

        private sealed class ThrowingCalculator : IBlendWeightCalculator2D
        {
            public void Calculate(Vector2 parameter, IReadOnlyList<Vector2> thresholds, IList<float> weights)
            {
                throw new InvalidOperationException("Test calculator failure.");
            }
        }

        private void CompleteCurrentNode(AnimationStateHandle state)
        {
            state.Time = state.Length;
            _controller.Layers[state.Layer].CurrentNode.PostUpdate(0f);
        }

        private void AddEntry(string key, int layer)
        {
            var entry = new AnimationClipLibraryEntry();
            SetField(entry, "_animationKey", key);
            SetField(entry, "_clip", _clip);
            SetField(entry, "_layer", layer);
            SetField(entry, "_wrapMode", WrapMode.Once);
            ((List<AnimationClipLibraryEntry>)_library.Entries).Add(entry);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class ResettingProvider : IAnimationClipProvider
        {
            private readonly PlayableAnimationController _controller;
            private readonly AnimationClipLease _lease;

            public ResettingProvider(PlayableAnimationController controller, AnimationClipLease lease)
            {
                _controller = controller;
                _lease = lease;
            }

            public UniTask<AnimationClipLease> LoadAsync(AnimationClipCatalogEntry entry,
                CancellationToken cancellationToken = default)
            {
                _controller.ResetController();
                return UniTask.FromResult(_lease);
            }
        }
    }
}
