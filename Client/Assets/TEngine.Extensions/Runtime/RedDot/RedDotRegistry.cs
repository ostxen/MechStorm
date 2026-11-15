using System;
using System.Collections.Generic;
using System.Linq;

namespace TEngine.Extensions.RedDot
{
    public enum RedDotRelationMode
    {
        AnyActive,
        SumCount
    }

    public sealed class RedDotRegistry
    {
        private readonly Dictionary<string, RedDotState> _states = new();
        private readonly Dictionary<string, HashSet<string>> _children = new();
        private readonly Dictionary<string, HashSet<string>> _parents = new();
        private readonly Dictionary<string, List<Action<RedDotState>>> _listeners = new();
        private readonly Dictionary<string, RedDotRelationMode> _relationModes = new();
        private readonly HashSet<string> _batchedChangedKeys = new();
        private int _batchDepth;

        public RedDotState? Get(string key)
        {
            return _states.TryGetValue(key, out var state) ? state : null;
        }

        public RedDotState Ensure(
            string key,
            RedDotDisplayType displayType = RedDotDisplayType.Dot)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Red-dot key cannot be empty.", nameof(key));
            }

            var state = GetOrCreate(key);
            state.DisplayType = displayType;
            return state;
        }

        public RedDotState Register(
            string key,
            RedDotDisplayType displayType = RedDotDisplayType.Dot)
        {
            return Ensure(key, displayType);
        }

        public RedDotState Set(string key, bool active)
        {
            return Set(key, active ? 1 : 0);
        }

        public RedDotState Set(string key, int count)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Red-dot key cannot be empty.", nameof(key));
            }

            var state = GetOrCreate(key);
            var nextCount = Math.Max(0, count);
            var changed = state.Count != nextCount || state.IsActive != (nextCount > 0);
            state.Count = nextCount;
            state.IsActive = nextCount > 0;
            if (!changed)
            {
                return state;
            }

            if (_batchDepth > 0)
            {
                _batchedChangedKeys.Add(key);
            }
            else
            {
                Notify(key);
                RecalculateParents(key, new HashSet<string>());
            }

            return state;
        }

        /// <summary>
        /// 合并一批红点更新，减少重复的父节点通知。
        /// </summary>
        public IDisposable BeginBatch()
        {
            _batchDepth++;
            return new BatchScope(this);
        }

        public void DefineRelation(string parentKey, RedDotRelationMode mode, params string[] childKeys)
        {
            DefineRelation(parentKey, mode, RedDotDisplayType.Dot, childKeys);
        }

        public void DefineRelation(
            string parentKey,
            RedDotRelationMode mode,
            RedDotDisplayType displayType,
            params string[] childKeys)
        {
            if (string.IsNullOrWhiteSpace(parentKey))
            {
                throw new ArgumentException("Parent key cannot be empty.", nameof(parentKey));
            }

            if (!_children.TryGetValue(parentKey, out var children))
            {
                children = new HashSet<string>();
                _children[parentKey] = children;
            }

            _relationModes[parentKey] = mode;
            Ensure(parentKey, displayType);
            for (var i = 0; i < childKeys.Length; i++)
            {
                var childKey = childKeys[i];
                if (string.IsNullOrWhiteSpace(childKey) || !children.Add(childKey))
                {
                    continue;
                }

                GetOrCreate(childKey);
                if (!_parents.TryGetValue(childKey, out var parents))
                {
                    parents = new HashSet<string>();
                    _parents[childKey] = parents;
                }

                parents.Add(parentKey);
            }

            Recalculate(parentKey);
            RecalculateParents(parentKey, new HashSet<string>());
        }

        public IDisposable Subscribe(string key, Action<RedDotState> listener, bool invokeImmediately = true)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            if (!_listeners.TryGetValue(key, out var listeners))
            {
                listeners = new List<Action<RedDotState>>();
                _listeners[key] = listeners;
            }

            listeners.Add(listener);
            if (invokeImmediately)
            {
                listener(GetOrCreate(key));
            }

            return new Subscription(() => listeners.Remove(listener));
        }

        public void Clear()
        {
            _states.Clear();
            _children.Clear();
            _parents.Clear();
            _listeners.Clear();
            _relationModes.Clear();
            _batchedChangedKeys.Clear();
            _batchDepth = 0;
        }

        private void EndBatch()
        {
            if (_batchDepth <= 0)
            {
                return;
            }

            _batchDepth--;
            if (_batchDepth > 0)
            {
                return;
            }

            _batchDepth = 1;
            try
            {
                while (_batchedChangedKeys.Count > 0)
                {
                    var changedKeys = _batchedChangedKeys.ToArray();
                    _batchedChangedKeys.Clear();
                    for (var i = 0; i < changedKeys.Length; i++)
                    {
                        var key = changedKeys[i];
                        Notify(key);
                        RecalculateParents(key, new HashSet<string>());
                    }
                }
            }
            finally
            {
                _batchDepth = 0;
            }
        }

        private RedDotState GetOrCreate(string key)
        {
            if (!_states.TryGetValue(key, out var state))
            {
                state = new RedDotState(key);
                _states.Add(key, state);
            }

            return state;
        }

        private void RecalculateParents(string childKey, HashSet<string> visited)
        {
            if (!_parents.TryGetValue(childKey, out var parents))
            {
                return;
            }

            foreach (var parentKey in parents)
            {
                if (!visited.Add(parentKey))
                {
                    continue;
                }

                Recalculate(parentKey);
                RecalculateParents(parentKey, visited);
            }
        }

        private void Recalculate(string parentKey)
        {
            if (!_children.TryGetValue(parentKey, out var children))
            {
                return;
            }

            var count = 0;
            foreach (var childKey in children)
            {
                var child = GetOrCreate(childKey);
                count += _relationModes[parentKey] == RedDotRelationMode.SumCount
                    ? child.Count
                    : child.IsActive ? 1 : 0;
            }

            var state = GetOrCreate(parentKey);
            var changed = state.Count != count || state.IsActive != (count > 0);
            state.Count = count;
            state.IsActive = count > 0;
            if (changed)
            {
                if (_batchDepth > 0)
                {
                    _batchedChangedKeys.Add(parentKey);
                }
                else
                {
                    Notify(parentKey);
                }
            }
        }

        private void Notify(string key)
        {
            if (!_listeners.TryGetValue(key, out var listeners))
            {
                return;
            }

            var state = GetOrCreate(key);
            var copy = listeners.ToArray();
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i](state);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action? _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }

        private sealed class BatchScope : IDisposable
        {
            private RedDotRegistry _registry;

            public BatchScope(RedDotRegistry registry)
            {
                _registry = registry;
            }

            public void Dispose()
            {
                _registry?.EndBatch();
                _registry = null;
            }
        }
    }
}
