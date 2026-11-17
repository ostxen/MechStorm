using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 基于 VirtualizedListCore 的业务入口。
    /// </summary>
    public class VirtualizedListView : MonoBehaviour, IEndDragHandler
    {
        private readonly Dictionary<string, Stack<VirtualizedListItem>> _recycledItems =
            new(StringComparer.Ordinal);
        private VirtualizedListCore _core;
        private IVirtualizedListAdapter _virtualizedAdapter;
        private IVirtualizedListPrefabSource _prefabSource;
        private Transform _recycleRoot;
        private VirtualizedPagingController _paging;

        public IVirtualizedListAdapter VirtualizedAdapter => _virtualizedAdapter;
        public VirtualizedListCore Core => _core;
        public int ItemCount => _core?.ItemCount ?? 0;
        public VirtualizedPagingController Paging => _paging;

        public void Initialize(
            IVirtualizedListAdapter adapter,
            IVirtualizedListPrefabSource prefabSource,
            VirtualizedListOptions options = null)
        {
            if (_core != null)
            {
                throw new InvalidOperationException("VirtualizedListView can only be initialized once.");
            }

            _virtualizedAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _prefabSource = prefabSource ?? throw new ArgumentNullException(nameof(prefabSource));
            _recycleRoot = transform;
            var scrollRect = GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                throw new InvalidOperationException("VirtualizedListView requires a ScrollRect.");
            }

            _core = new VirtualizedListCore();
            _core.Initialize(
                scrollRect,
                adapter.GetItemCount(),
                index => adapter.CreateItem(this, index),
                (item, index, version) => adapter.Bind(item, index, version),
                RecycleItem,
                options);
        }

        public void Initialize(
            IVirtualizedListAdapter adapter,
            VirtualizedListOptions options = null)
        {
            Initialize(adapter, new VirtualizedListServicePrefabSource(), options);
        }

        public void SetItemCount(int count, bool resetPosition = true)
        {
            EnsureInitialized();
            _core.SetItemCount(count, resetPosition);
        }

        public void AttachPagingSource(IVirtualizedPagingSource source)
        {
            _paging ??= new VirtualizedPagingController();
            _paging.ItemCountChanged -= OnPagingItemCountChanged;
            _paging.ItemCountChanged += OnPagingItemCountChanged;
            _paging.Attach(source);
        }

        public void InvalidateItemSize(int index, float size)
        {
            EnsureInitialized();
            _core.InvalidateItemSize(index, size);
        }

        public bool TryCaptureAnchor(out VirtualizedListScrollAnchor anchor)
        {
            EnsureInitialized();
            return _core.TryCaptureAnchor(out anchor);
        }

        public void RestoreAnchor(VirtualizedListScrollAnchor anchor)
        {
            EnsureInitialized();
            _core.RestoreAnchor(anchor);
        }

        public void SnapToIndex(int index)
        {
            EnsureInitialized();
            _core.SnapToIndex(index);
        }

        public void SnapToNearest()
        {
            EnsureInitialized();
            _core.SnapToNearest();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_core != null && _core.ShouldSnapOnEndDrag)
            {
                _core.SnapToNearest();
            }
        }

        public VirtualizedListItem GetOrCreateItem(string prefabKey)
        {
            EnsureInitialized();
            if (!_recycledItems.TryGetValue(prefabKey, out var items))
            {
                items = new Stack<VirtualizedListItem>();
                _recycledItems.Add(prefabKey, items);
            }

            while (items.Count > 0)
            {
                var item = items.Pop();
                if (item != null)
                {
                    item.PrefabKey = prefabKey;
                    return item;
                }
            }

            return _prefabSource.Create(prefabKey, _recycleRoot);
        }

        public VirtualizedListItem AllocOrNewItem(string prefabKey)
        {
            return GetOrCreateItem(prefabKey);
        }

        private void Update()
        {
            _core?.Tick();
            if (_core != null)
            {
                _paging?.NotifyRange(_core.LastShownIndex);
            }
        }

        private void RecycleItem(VirtualizedListItem item)
        {
            if (item == null)
            {
                return;
            }

            _virtualizedAdapter.OnRecycle(item);
            item.gameObject.SetActive(false);
            if (!_recycledItems.TryGetValue(item.PrefabKey, out var items))
            {
                items = new Stack<VirtualizedListItem>();
                _recycledItems.Add(item.PrefabKey, items);
            }

            if (items.Count >= _core.MaxCachedItemsPerPrefab)
            {
                _prefabSource?.Release(item);
                return;
            }

            items.Push(item);
        }

        private void OnDestroy()
        {
            _core?.Dispose();
            foreach (var items in _recycledItems.Values)
            {
                while (items.Count > 0)
                {
                    _prefabSource?.Release(items.Pop());
                }
            }

            _recycledItems.Clear();
            _paging?.Dispose();
            _paging = null;
        }

        private void OnPagingItemCountChanged(int count)
        {
            if (_core != null)
            {
                _core.SetItemCount(count, false);
            }
        }

        private void EnsureInitialized()
        {
            if (_core == null)
            {
                throw new InvalidOperationException("VirtualizedListView is not initialized.");
            }
        }
    }
}
