using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 将动画集合绑定到 Animator，按动画标识或播放配置播放，并管理层、Graph 和资源释放。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class PlayableAnimationController : MonoBehaviour
    {
        public const int MaxLayerCount = 4;
        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private AnimationClipLibrary _library;

        [SerializeField]
        private float _defaultFadeDuration = 0.15f;

        [SerializeField]
        [Range(1, 4)]
        private int _layerCount = 4;

        [SerializeField]
        private AnimationLayerSettings[] _layerSettings = Array.Empty<AnimationLayerSettings>();

        private readonly Dictionary<string, AnimationStateHandle> _states =
            new Dictionary<string, AnimationStateHandle>(
                StringComparer.Ordinal);
        private readonly List<AnimationClipLease> _leases =
            new List<AnimationClipLease>();
        private readonly Dictionary<string, AnimationBlendTreeHandle> _blendTrees =
            new Dictionary<string, AnimationBlendTreeHandle>(StringComparer.Ordinal);
        private readonly List<AnimationMixerNode> _layers =
            new List<AnimationMixerNode>();

        private PlayableAnimationGraph _graph;
        private AnimationLayerMixerNode _layerMixer;
        private AnimationResetContext _resetContext;
        private CancellationTokenSource _lifecycleCancellation;
        private int _bindingVersion;
        private bool _initialized;
        private bool _isUpdating;

        public AnimationEventQueue EventQueue { get; } =
            new AnimationEventQueue();

        public PlayableAnimationGraph Graph => _graph;

        public IReadOnlyDictionary<string, AnimationStateHandle> States => _states;

        public IReadOnlyDictionary<string, AnimationBlendTreeHandle> BlendTrees => _blendTrees;

        public IReadOnlyList<AnimationMixerNode> Layers => _layers;

        public int LayerCount => _layers.Count;

        public bool IsInitialized => _initialized;

        public IReadOnlyDictionary<PlayableHandle, string>
            BuildMonitorLabelTable()
        {
            var labels = new Dictionary<PlayableHandle, string>();
            if (_graph == null || !_graph.Graph.IsValid() || _layerMixer == null)
            {
                return labels;
            }

            labels[_layerMixer.Output.GetHandle()] = "LayerMixer [Root]";
            for (int layerIndex = 0;
                 layerIndex < _layerMixer.Layers.Count;
                 layerIndex++)
            {
                var layer = _layerMixer.Layers[layerIndex];
                labels[layer.Output.GetHandle()] =
                    $"Layer {layerIndex} [Weight={_layerMixer.GetLayerWeight(layerIndex):F2}]";

                for (int childIndex = 0;
                     childIndex < layer.ChildNodes.Count;
                     childIndex++)
                {
                    var child = layer.ChildNodes[childIndex];
                    var clipNode = child as AnimationClipNode;
                    var key = clipNode == null ||
                              string.IsNullOrEmpty(clipNode.AnimationKey)
                        ? child.GetType().Name
                        : clipNode.AnimationKey;
                    labels[child.Output.GetHandle()] =
                        $"{key} [Weight={child.Weight:F2}]";
                }
            }

            foreach (var tree in _blendTrees.Values)
                AddTreeMonitorLabels(tree.Node, tree.Key, labels);
            return labels;
        }

        private static void AddTreeMonitorLabels(AnimationPlayableNode node, string path,
            Dictionary<PlayableHandle, string> labels)
        {
            if (node.IsDisposed) return;
            var clip = node as AnimationClipNode;
            labels[node.Output.GetHandle()] =
                $"{path} [{(clip == null ? node.GetType().Name : clip.AnimationKey)}, Weight={node.Weight:F2}]";
            if (!(node is AnimationMixerNode mixer)) return;
            for (int i = 0; i < mixer.ChildNodes.Count; i++)
                AddTreeMonitorLabels(mixer.ChildNodes[i], $"{path}/{i}", labels);
        }

        public AnimationSnapshot CaptureSnapshot()
        {
            var states = new List<AnimationStateSnapshot>(
                _states.Count);
            foreach (var pair in _states)
            {
                states.Add(new AnimationStateSnapshot(pair.Value));
            }
            var trees = new List<AnimationBlendTreeSnapshot>(_blendTrees.Count);
            foreach (var tree in _blendTrees.Values)
                trees.Add(new AnimationBlendTreeSnapshot(tree));

            var graph = _graph;
            var playableRootCount = graph == null || !graph.Graph.IsValid()
                ? 0
                : graph.Graph.GetRootPlayableCount();

            return new AnimationSnapshot(
                graph == null ? string.Empty : graph.GraphName,
                graph == null
                    ? DirectorUpdateMode.Manual
                    : graph.UpdateMode,
                graph != null && graph.IsPlaying,
                graph == null || graph.IsDisposed,
                playableRootCount,
                states.Count,
                _leases.Count,
                EventQueue.Count,
                states,
                trees);
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Update()
        {
            Evaluate(Time.deltaTime);
        }

        private void OnDisable()
        {
            ResetController();
        }

        private void OnDestroy()
        {
            ResetController();
            EventQueue.ClearSubscribers();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator == null)
            {
                throw new InvalidOperationException(
                    "PlayableAnimationController 需要 Animator。");
            }

            _resetContext = new AnimationResetContext();
            _lifecycleCancellation = new CancellationTokenSource();
            _graph = new PlayableAnimationGraph(
                _animator,
                name + ".SimplePlayable",
                DirectorUpdateMode.Manual);
            _layerCount = Mathf.Clamp(_layerCount, 1, MaxLayerCount);
            _layerMixer = new AnimationLayerMixerNode(
                _graph.Graph,
                _layerCount);
            _layers.Clear();
            for (int i = 0; i < _layerCount; i++)
            {
                var layer = new AnimationMixerNode(_graph.Graph);
                var settings = _layerSettings != null && i < _layerSettings.Length
                    ? _layerSettings[i] : null;
                _layerMixer.AddLayer(layer,
                    settings != null ? settings.Weight : (i == 0 ? 1f : 0f),
                    settings?.Mask, settings != null && settings.Additive);
                _layers.Add(layer);
            }
            _graph.SetRoot(_layerMixer);
            _initialized = true;

            if (_library != null)
            {
                BindLibrary(_library);
            }
        }

        public bool TryGetState(
            string animationKey,
            out AnimationStateHandle state)
        {
            return _states.TryGetValue(animationKey, out state);
        }

        public AnimationStateHandle GetState(string animationKey)
        {
            if (!TryGetState(animationKey, out var state))
            {
                throw new KeyNotFoundException(
                    $"未注册动画状态：{animationKey}");
            }

            return state;
        }

        /// <summary>在绑定资源后注册树。树与 Clip 共用 Key 空间；重绑、禁用和 Reset 时一起失效。</summary>
        public AnimationBlendTreeHandle RegisterBlendTree(string animationKey,
            AnimationBlendTreeDefinition definition, int layer = 0)
        {
            if (string.IsNullOrWhiteSpace(animationKey))
                throw new ArgumentException("树 Key 不能为空。", nameof(animationKey));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            EnsureInitialized();
            var targetLayer = GetLayer(layer);
            if (_states.ContainsKey(animationKey) || _blendTrees.ContainsKey(animationKey))
                throw new InvalidOperationException($"重复动画 Key：{animationKey}");

            var node = definition.CreateNode(_graph.Graph, key => GetState(key).Node.Clip);
            try
            {
                targetLayer.AddChild(node);
                var handle = new AnimationBlendTreeHandle(animationKey, layer, node);
                _blendTrees.Add(animationKey, handle);
                return handle;
            }
            catch
            {
                node.Dispose();
                throw;
            }
        }

        public bool TryGetBlendTree(string animationKey, out AnimationBlendTreeHandle tree) =>
            _blendTrees.TryGetValue(animationKey, out tree);

        public AnimationBlendTreeHandle GetBlendTree(string animationKey)
        {
            if (!TryGetBlendTree(animationKey, out var tree))
                throw new KeyNotFoundException($"未注册混合树：{animationKey}");
            return tree;
        }

        public void SetBlendTreeParameter(string animationKey, float parameter) =>
            GetBlendTree(animationKey).SetParameter(parameter);

        public void SetBlendTreeParameter(string animationKey, Vector2 parameter) =>
            GetBlendTree(animationKey).SetParameter(parameter);

        public void BindLibrary(AnimationClipLibrary library)
        {
            if (library == null)
            {
                throw new ArgumentNullException(nameof(library));
            }

            EnsureInitialized();
            _bindingVersion++;
            ClearBoundAnimations();

            try
            {
                for (int i = 0; i < library.Entries.Count; i++)
                {
                    var entry = library.Entries[i];
                    if (entry == null || entry.Clip == null)
                    {
                        continue;
                    }

                    var lease = new AnimationClipLease(
                        entry.AnimationKey,
                        entry.Clip);
                    AddAnimation(
                        lease,
                        entry.Layer,
                        entry.WrapMode);
                }
            }
            catch (Exception exception)
            {
                ClearBoundAnimations();
                Log.Error(
                    $"动画库绑定失败，Controller：{name}，异常：{exception}");
                throw;
            }
        }

        public async UniTask BindCatalogAsync(
            AnimationClipCatalog catalog,
            IAnimationClipProvider provider,
            CancellationToken cancellationToken = default)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            EnsureInitialized();
            var bindingVersion = ++_bindingVersion;
            ClearBoundAnimations();
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _lifecycleCancellation.Token);
            var requestToken = linkedCancellation.Token;
            var pendingLeases = new List<AnimationClipLease>();

            try
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    requestToken.ThrowIfCancellationRequested();
                    var entry = catalog.Entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    var lease = await provider.LoadAsync(
                        entry,
                        requestToken);
                    if (lease == null)
                    {
                        throw new InvalidOperationException(
                            $"Provider 返回空 Lease：{entry.AnimationKey}");
                    }

                    pendingLeases.Add(lease);
                    requestToken.ThrowIfCancellationRequested();
                    if (bindingVersion != _bindingVersion)
                    {
                        throw new OperationCanceledException(requestToken);
                    }

                    AddAnimation(lease, entry.Layer, entry.WrapMode);
                    pendingLeases.Remove(lease);
                }
            }
            catch (Exception exception)
            {
                for (int i = 0; i < pendingLeases.Count; i++)
                {
                    pendingLeases[i].Release();
                }

                if (_initialized && bindingVersion == _bindingVersion)
                {
                    ClearBoundAnimations();
                }

                if (!(exception is OperationCanceledException))
                {
                    Log.Error(
                        $"动画目录绑定失败，Controller：{name}，异常：{exception}");
                }

                throw;
            }
        }

        /// <summary>播放已绑定 Clip 或整棵树；负数过渡时长使用默认值，未找到标识时返回 false。</summary>
        public bool Play(
            string animationKey,
            float fadeDuration = -1f)
        {
            EnsureInitialized();
            AnimationPlayableNode node;
            int layerIndex;
            if (_states.TryGetValue(animationKey, out var state))
            {
                node = state.Node;
                layerIndex = state.Layer;
            }
            else if (_blendTrees.TryGetValue(animationKey, out var tree))
            {
                node = tree.Node;
                layerIndex = tree.Layer;
            }
            else
            {
                return false;
            }

            var duration = fadeDuration < 0f
                ? _defaultFadeDuration
                : fadeDuration;
            var layer = GetLayer(layerIndex);
            var index = FindNodeIndex(node, layer);
            layer.CrossFade(index, duration);
            _graph.Play();
            return true;
        }

        /// <summary>将播放配置应用到 Clip 状态并切换；未知 Key 返回 false，树 Key 抛出 NotSupportedException。</summary>
        public bool Play(AnimationPlaybackOptions request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            EnsureInitialized();
            if (_blendTrees.ContainsKey(request.AnimationKey))
                throw new NotSupportedException("AnimationPlaybackOptions 只用于 Clip；树请使用 Play(key, fadeDuration)。");
            if (!_states.TryGetValue(
                    request.AnimationKey,
                    out var state))
            {
                return false;
            }

            request.ApplyTo(state.Node);
            var layer = GetLayer(state.Layer);
            var index = FindNodeIndex(state.Node, layer);
            layer.CrossFade(index, request.FadeDuration);
            _graph.Play();
            return true;
        }

        public void Pause()
        {
            _graph?.Pause();
        }

        /// <summary>恢复暂停的层；不改变时间或已完成状态。</summary>
        public void Resume()
        {
            EnsureInitialized();
            _graph.Play();
        }

        public bool TryGetCurrentState(int layer, out AnimationStateHandle state)
        {
            EnsureInitialized();
            var node = GetLayer(layer).CurrentNode as AnimationClipNode;
            state = node?.StateHandle;
            return state != null && state.IsValid;
        }

        public bool TryGetCurrentBlendTree(int layer, out AnimationBlendTreeHandle tree)
        {
            EnsureInitialized();
            var node = GetLayer(layer).CurrentNode;
            foreach (var candidate in _blendTrees.Values)
            {
                if (ReferenceEquals(candidate.Node, node) && candidate.IsValid)
                {
                    tree = candidate;
                    return true;
                }
            }
            tree = null;
            return false;
        }

        public bool TryGetCurrentKey(int layer, out string animationKey)
        {
            if (TryGetCurrentState(layer, out var state))
            {
                animationKey = state.Key;
                return true;
            }
            if (TryGetCurrentBlendTree(layer, out var tree))
            {
                animationKey = tree.Key;
                return true;
            }
            animationKey = null;
            return false;
        }

        /// <summary>停止指定层并清空该层混合权重，不影响其他层。</summary>
        public void StopLayer(int layer)
        {
            EnsureInitialized();
            GetLayer(layer).Stop();
        }

        public void Stop()
        {
            _graph?.Stop();
            EventQueue.Clear();
        }

        public void Evaluate(float deltaTime)
        {
            if (!_initialized || _isUpdating || _graph == null)
            {
                return;
            }

            _isUpdating = true;
            try
            {
                _graph.Evaluate(deltaTime);
                EventQueue.Flush();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void ResetController()
        {
            if (!_initialized)
            {
                return;
            }

            _graph.Stop();
            _bindingVersion++;
            _lifecycleCancellation?.Cancel();
            EventQueue.Clear();
            ClearBoundAnimations();
            _graph.Dispose();
            _graph = null;
            _layerMixer = null;
            _layers.Clear();
            _resetContext?.Dispose();
            _resetContext = null;
            _lifecycleCancellation?.Dispose();
            _lifecycleCancellation = null;
            _initialized = false;
        }

        private void AddAnimation(
            AnimationClipLease lease,
            int layer,
            WrapMode wrapMode)
        {
            if (layer < 0 || layer >= _layers.Count)
            {
                lease.Release();
                throw new ArgumentOutOfRangeException(
                    nameof(layer),
                    layer,
                    $"动画层必须位于 0 到 {_layers.Count - 1} 之间。");
            }

            if (_states.ContainsKey(lease.AnimationKey) || _blendTrees.ContainsKey(lease.AnimationKey))
            {
                lease.Release();
                throw new InvalidOperationException(
                    $"重复动画 Key：{lease.AnimationKey}");
            }

            _resetContext.Track(lease);
            _leases.Add(lease);
            var node = new AnimationClipNode(
                _graph.Graph,
                lease.Clip,
                wrapMode);
            node.AnimationKey = lease.AnimationKey;
            node.EventQueue = EventQueue;
            var targetLayer = _layers[layer];
            var index = targetLayer.AddChild(node);
            var state = new AnimationStateHandle(lease.AnimationKey, layer, node);
            node.StateHandle = state;
            _states.Add(lease.AnimationKey, state);

            if (layer == 0 && targetLayer.ChildNodes.Count == 1)
            {
                targetLayer.SetChildWeight(index, 1f);
            }
        }

        public void SetLayerWeight(int layer, float weight)
        {
            EnsureInitialized();
            _layerMixer.SetLayerWeight(layer, weight);
        }

        public float GetLayerWeight(int layer)
        {
            EnsureInitialized();
            return _layerMixer.GetLayerWeight(layer);
        }

        public void SetLayerMask(int layer, AvatarMask mask)
        {
            EnsureInitialized();
            _layerMixer.SetLayerMask(layer, mask);
        }

        public void SetLayerAdditive(int layer, bool additive)
        {
            EnsureInitialized();
            _layerMixer.SetLayerAdditive(layer, additive);
        }

        private static int FindNodeIndex(
            AnimationPlayableNode node,
            AnimationMixerNode layer)
        {
            for (int i = 0; i < layer.ChildNodes.Count; i++)
            {
                if (ReferenceEquals(layer.ChildNodes[i], node))
                {
                    return i;
                }
            }

            throw new InvalidOperationException(
                "动画节点未连接到指定 Layer。");
        }

        private void ClearBoundAnimations()
        {
            EventQueue.Clear();
            _states.Clear();
            _blendTrees.Clear();
            for (int i = 0; i < _layers.Count; i++)
            {
                _layers[i].ClearChildren();
            }

            _resetContext?.Reset();
            _leases.Clear();
        }

        private AnimationMixerNode GetLayer(int layer)
        {
            if (layer < 0 || layer >= _layers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(layer));
            }

            return _layers[layer];
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }
    }
}
