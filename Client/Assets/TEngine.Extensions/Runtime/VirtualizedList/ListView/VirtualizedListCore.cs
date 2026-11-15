using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 基于 LoopScrollRect 思路的线性 ScrollRect 虚拟化内核。
    ///
    /// 只负责 viewport、可见索引、动态尺寸、位置计算和回收。
    /// Item 的创建、绑定和业务清理通过回调注入。
    /// </summary>
    public sealed class VirtualizedListCore : IDisposable
    {
        private readonly Dictionary<int, VirtualizedListItem> _shownItems = new();
        private readonly List<int> _recycleIndices = new();
        private ScrollRect _scrollRect;
        private RectTransform _viewport;
        private RectTransform _content;
        private Func<int, VirtualizedListItem> _createItem;
        private Action<VirtualizedListItem, int, uint> _bindItem;
        private Action<VirtualizedListItem> _recycleItem;
        private VirtualizedListOptions _options;
        private IVirtualizedListLayout _layout;
        private int _itemCount;
        private bool _initialized;
        private bool _refreshing;
        private float? _snapTargetOffset;
        private float _snapVelocity;

        public int ItemCount => _itemCount;
        public int FirstShownIndex { get; private set; } = -1;
        public int LastShownIndex { get; private set; } = -1;
        public bool ShouldSnapOnEndDrag => _options?.Snap?.SnapOnEndDrag == true;
        public int MaxCachedItemsPerPrefab =>
            Math.Max(0, _options?.MaxCachedItemsPerPrefab ?? 0);

        public void Initialize(
            ScrollRect scrollRect,
            int itemCount,
            Func<int, VirtualizedListItem> createItem,
            Action<VirtualizedListItem, int, uint> bindItem,
            Action<VirtualizedListItem> recycleItem,
            VirtualizedListOptions options)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("VirtualizedListCore can only be initialized once.");
            }

            _scrollRect = scrollRect ?? throw new ArgumentNullException(nameof(scrollRect));
            _content = _scrollRect.content ??
                       throw new InvalidOperationException("ScrollRect.content is required.");
            _viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : _scrollRect.GetComponent<RectTransform>();
            _createItem = createItem ?? throw new ArgumentNullException(nameof(createItem));
            _bindItem = bindItem ?? throw new ArgumentNullException(nameof(bindItem));
            _recycleItem = recycleItem ?? throw new ArgumentNullException(nameof(recycleItem));
            _options = options ?? new VirtualizedListOptions();
            ValidateOptions(_options);
            _itemCount = Math.Max(0, itemCount);
            _layout = _options.Layout ?? new LinearVirtualizedLayout(
                _options.Vertical,
                _options.ItemDefaultSize,
                _options.ItemSpacing);
            _layout.Reset(_itemCount, _viewport.rect.size);
            _scrollRect.vertical = _options.Vertical;
            _scrollRect.horizontal = !_options.Vertical;
            ConfigureRectTransforms();
            _initialized = true;
            Refresh(true);
        }

        public void Tick()
        {
            if (_initialized && !_refreshing)
            {
                UpdateSnap();
                Refresh(false);
            }
        }

        public void SetItemCount(int count, bool resetPosition)
        {
            EnsureInitialized();
            _itemCount = Math.Max(0, count);
            _layout.Reset(_itemCount, _viewport.rect.size);
            _snapTargetOffset = null;
            if (resetPosition)
            {
                SetScrollOffset(0f);
            }

            Refresh(true);
        }

        public void InvalidateItemSize(int index, float size)
        {
            EnsureInitialized();
            if (index < 0 || index >= _itemCount)
            {
                return;
            }

            _layout.SetItemSize(
                index,
                _options.Vertical ? new Vector2(0f, size) : new Vector2(size, 0f));
            UpdateContentSize();
            Refresh(true);
        }

        public bool TryCaptureAnchor(out VirtualizedListScrollAnchor anchor)
        {
            EnsureInitialized();
            if (FirstShownIndex < 0)
            {
                anchor = default;
                return false;
            }

            anchor = new VirtualizedListScrollAnchor(
                FirstShownIndex,
                GetScrollOffset() - GetItemOffset(FirstShownIndex),
                IsAtEnd());
            return true;
        }

        public void RestoreAnchor(VirtualizedListScrollAnchor anchor)
        {
            EnsureInitialized();
            if (_itemCount == 0)
            {
                return;
            }

            var index = Math.Max(0, Math.Min(anchor.ItemIndex, _itemCount - 1));
            SetScrollOffset(anchor.StickToEnd
                ? GetMaxScrollOffset()
                : GetItemOffset(index) + anchor.Offset);
            Refresh(true);
        }

        public void SnapToIndex(int index)
        {
            EnsureInitialized();
            if (!_options.Snap.Enabled || index < 0 || index >= _itemCount)
            {
                return;
            }

            SetSnapTarget(GetSnapOffset(index));
        }

        public void SnapToNearest()
        {
            EnsureInitialized();
            if (!_options.Snap.Enabled || _itemCount == 0)
            {
                return;
            }

            var range = _layout.CalculateVisibleRange(
                _content.anchoredPosition * new Vector2(-1f, 1f),
                _viewport.rect.size,
                _options.Snap.BufferPixels + GetViewportAxisSize());
            if (range.IsEmpty)
            {
                return;
            }

            var current = GetScrollOffset();
            var nearestIndex = range.MinIndex;
            var nearestDistance = float.MaxValue;
            for (var index = range.MinIndex; index <= range.MaxIndex; index++)
            {
                var distance = Mathf.Abs(GetSnapOffset(index) - current);
                if (distance < nearestDistance)
                {
                    nearestIndex = index;
                    nearestDistance = distance;
                }
            }

            SnapToIndex(nearestIndex);
        }

        public void Dispose()
        {
            if (!_initialized)
            {
                return;
            }

            foreach (var item in _shownItems.Values)
            {
                item.gameObject.SetActive(false);
                _options.Animator?.OnRecycled(item, item.CurrentIndex);
                _recycleItem(item);
                item.ClearBinding();
            }

            _shownItems.Clear();
            _initialized = false;
        }

        private void Refresh(bool force)
        {
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            try
            {
                UpdateContentSize();
                var range = CalculateVisibleRange();
                if (force || range.min != FirstShownIndex || range.max != LastShownIndex)
                {
                    RecycleOutside(range.min, range.max);
                    for (var index = Math.Max(0, range.min); index <= range.max; index++)
                    {
                        EnsureItem(index);
                    }

                    UpdateContentSize();
                    FirstShownIndex = range.min;
                    LastShownIndex = range.max;
                }

                PositionShownItems();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private (int min, int max) CalculateVisibleRange()
        {
            if (_itemCount == 0)
            {
                return (-1, -1);
            }

            var range = _layout.CalculateVisibleRange(
                _content.anchoredPosition * new Vector2(-1f, 1f),
                _viewport.rect.size,
                _options.BufferPixels);
            return (range.MinIndex, range.MaxIndex);
        }

        private void EnsureItem(int index)
        {
            if (_shownItems.ContainsKey(index))
            {
                return;
            }

            var item = _createItem(index);
            if (item == null)
            {
                throw new InvalidOperationException($"Failed to create virtualized item {index}.");
            }

            item.transform.SetParent(_content, false);
            item.gameObject.SetActive(true);
            var version = item.PrepareBinding(index);
            _bindItem(item, index, version);
            _shownItems.Add(index, item);
            _layout.SetItemSize(index, item.RectTransform.rect.size);
            _options.Animator?.OnShown(item, index);
        }

        private void RecycleOutside(int min, int max)
        {
            _recycleIndices.Clear();
            foreach (var pair in _shownItems)
            {
                if (pair.Key < min || pair.Key > max)
                {
                    _recycleIndices.Add(pair.Key);
                }
            }

            for (var i = 0; i < _recycleIndices.Count; i++)
            {
                var index = _recycleIndices[i];
                var item = _shownItems[index];
                _shownItems.Remove(index);
                item.gameObject.SetActive(false);
                _options.Animator?.OnRecycled(item, index);
                _recycleItem(item);
                item.ClearBinding();
            }
        }

        private void PositionShownItems()
        {
            foreach (var pair in _shownItems)
            {
                var item = pair.Value;
                var rect = _layout.GetItemRect(pair.Key);
                if (_options.Decoration != null)
                {
                    rect = _options.Decoration.Decorate(pair.Key, rect);
                }
                var position = item.RectTransform.anchoredPosition;
                position.x = rect.x;
                position.y = -rect.y;
                item.RectTransform.anchoredPosition = position;
                var size = item.RectTransform.sizeDelta;
                if (rect.width > 0f)
                {
                    size.x = rect.width;
                }

                if (rect.height > 0f)
                {
                    size.y = rect.height;
                }

                item.RectTransform.sizeDelta = size;
            }
        }

        private float GetItemOffset(int index)
        {
            var rect = _layout.GetItemRect(index);
            return _options.Vertical ? rect.y : rect.x;
        }

        private float GetSnapOffset(int index)
        {
            var rect = _layout.GetItemRect(index);
            var start = _options.Vertical ? rect.y : rect.x;
            var size = _options.Vertical ? rect.height : rect.width;
            var viewportSize = GetViewportAxisSize();
            return _options.Snap.Alignment switch
            {
                VirtualizedSnapAlignment.Center => start + (size - viewportSize) * 0.5f,
                VirtualizedSnapAlignment.End => start + size - viewportSize,
                _ => start
            };
        }

        private float GetViewportAxisSize()
        {
            return _options.Vertical ? _viewport.rect.height : _viewport.rect.width;
        }

        private void SetSnapTarget(float target)
        {
            target = Mathf.Clamp(target, 0f, GetMaxScrollOffset());
            _snapVelocity = 0f;
            if (_options.Snap.SmoothTime <= 0f)
            {
                _snapTargetOffset = null;
                SetScrollOffset(target);
                Refresh(true);
                return;
            }

            _snapTargetOffset = target;
        }

        private void UpdateSnap()
        {
            if (!_snapTargetOffset.HasValue)
            {
                return;
            }

            var current = GetScrollOffset();
            var next = Mathf.SmoothDamp(
                current,
                _snapTargetOffset.Value,
                ref _snapVelocity,
                _options.Snap.SmoothTime);
            SetScrollOffset(next);
            if (Mathf.Abs(next - _snapTargetOffset.Value) < 0.1f)
            {
                SetScrollOffset(_snapTargetOffset.Value);
                _snapTargetOffset = null;
                _snapVelocity = 0f;
            }
        }

        private void UpdateContentSize()
        {
            var size = _content.sizeDelta;
            var contentSize = _layout.ContentSize;
            size.x = contentSize.x > 0f ? contentSize.x : size.x;
            size.y = contentSize.y > 0f ? contentSize.y : size.y;
            _content.sizeDelta = size;
        }

        private float GetScrollOffset()
        {
            return _options.Vertical
                ? Mathf.Max(0f, _content.anchoredPosition.y)
                : Mathf.Max(0f, -_content.anchoredPosition.x);
        }

        private void SetScrollOffset(float offset)
        {
            var position = _content.anchoredPosition;
            if (_options.Vertical)
            {
                position.y = Mathf.Clamp(offset, 0f, GetMaxScrollOffset());
            }
            else
            {
                position.x = -Mathf.Clamp(offset, 0f, GetMaxScrollOffset());
            }

            _content.anchoredPosition = position;
        }

        private float GetMaxScrollOffset()
        {
            var contentSize = _layout.ContentSize;
            var viewportSize = _viewport.rect.size;
            return _options.Vertical
                ? Mathf.Max(0f, contentSize.y - viewportSize.y)
                : Mathf.Max(0f, contentSize.x - viewportSize.x);
        }

        private bool IsAtEnd()
        {
            return GetScrollOffset() >= GetMaxScrollOffset() - 0.5f;
        }

        private void ConfigureRectTransforms()
        {
            _content.anchorMin = _options.Vertical
                ? new Vector2(0f, 1f)
                : new Vector2(0f, 0f);
            _content.anchorMax = _content.anchorMin;
            _content.pivot = _content.anchorMin;
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("VirtualizedListCore is not initialized.");
            }
        }

        private static void ValidateOptions(VirtualizedListOptions options)
        {
            if (options.Layout == null && options.ItemDefaultSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.ItemDefaultSize),
                    "ItemDefaultSize must be greater than zero.");
            }

            if (options.BufferPixels < 0f ||
                options.ItemSpacing < 0f ||
                options.Snap?.SmoothTime < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "BufferPixels and ItemSpacing cannot be negative.");
            }

            if (options.MaxCachedItemsPerPrefab < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.MaxCachedItemsPerPrefab));
            }

            if (options.Snap == null)
            {
                options.Snap = new VirtualizedSnapOptions();
            }
        }
    }
}
