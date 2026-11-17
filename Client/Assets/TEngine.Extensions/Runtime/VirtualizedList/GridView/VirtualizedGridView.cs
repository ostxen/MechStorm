using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 基于 VirtualizedGridCore 的业务入口。
    /// </summary>
    public class VirtualizedGridView : MonoBehaviour, IEndDragHandler
    {
        private readonly Dictionary<string, Stack<VirtualizedGridItem>> _recycledItems =
            new(StringComparer.Ordinal);
        private VirtualizedGridCore _core;
        private IVirtualizedGridAdapter _virtualizedAdapter;
        private IVirtualizedGridPrefabSource _prefabSource;
        private VirtualizedPagingController _paging;

        public IVirtualizedGridAdapter VirtualizedAdapter => _virtualizedAdapter;
        public VirtualizedGridCore Core => _core;
        public int ItemCount => _core?.ItemCount ?? 0;
        public VirtualizedPagingController Paging => _paging;

        public void Initialize(
            IVirtualizedGridAdapter adapter,
            IVirtualizedGridPrefabSource prefabSource,
            VirtualizedGridOptions options = null)
        {
            if (_core != null)
            {
                throw new InvalidOperationException("VirtualizedGridView can only be initialized once.");
            }

            _virtualizedAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _prefabSource = prefabSource ?? throw new ArgumentNullException(nameof(prefabSource));
            var scrollRect = GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                throw new InvalidOperationException("VirtualizedGridView requires a ScrollRect.");
            }

            _core = new VirtualizedGridCore();
            _core.Initialize(
                scrollRect,
                adapter.GetItemCount(),
                (index, row, column) => adapter.CreateItem(this, index, row, column),
                (item, index, row, column, version) =>
                    adapter.Bind(item, index, row, column, version),
                RecycleItem,
                options);
        }

        public void Initialize(
            IVirtualizedGridAdapter adapter,
            VirtualizedGridOptions options = null)
        {
            Initialize(adapter, new VirtualizedGridServicePrefabSource(), options);
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

        public VirtualizedGridItem GetOrCreateItem(string prefabKey)
        {
            EnsureInitialized();
            if (!_recycledItems.TryGetValue(prefabKey, out var items))
            {
                items = new Stack<VirtualizedGridItem>();
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

            return _prefabSource.Create(prefabKey, transform);
        }

        public VirtualizedGridItem AllocOrNewItem(string prefabKey)
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

        private void RecycleItem(VirtualizedGridItem item)
        {
            if (item == null)
            {
                return;
            }

            _virtualizedAdapter.OnRecycle(item);
            item.gameObject.SetActive(false);
            if (!_recycledItems.TryGetValue(item.PrefabKey, out var items))
            {
                items = new Stack<VirtualizedGridItem>();
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
                throw new InvalidOperationException("VirtualizedGridView is not initialized.");
            }
        }
    }
}
