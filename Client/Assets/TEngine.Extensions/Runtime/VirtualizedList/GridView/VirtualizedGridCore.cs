using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 固定列数网格的独立虚拟化内核。
    /// 以 viewport 行范围为边界复用 Item。
    /// </summary>
    public sealed class VirtualizedGridCore : IDisposable
    {
        private readonly Dictionary<int, VirtualizedGridItem> _shownItems = new();
        private readonly List<int> _recycleIndices = new();
        private ScrollRect _scrollRect;
        private RectTransform _viewport;
        private RectTransform _content;
        private Func<int, int, int, VirtualizedGridItem> _createItem;
        private Action<VirtualizedGridItem, int, int, int, uint> _bindItem;
        private Action<VirtualizedGridItem> _recycleItem;
        private VirtualizedGridOptions _options;
        private int _itemCount;
        private int _rowCount;
        private bool _initialized;
        private bool _refreshing;
        private float? _snapTargetOffset;
        private float _snapVelocity;

        public int ItemCount => _itemCount;
        public bool ShouldSnapOnEndDrag => _options?.Snap?.SnapOnEndDrag == true;
        public int LastShownIndex =>
            _options == null || _options.LastShownRow < 0
                ? -1
                : Math.Min(
                    _itemCount - 1,
                    (_options.LastShownRow + 1) * _options.ColumnCount - 1);
        public int MaxCachedItemsPerPrefab =>
            Math.Max(0, _options?.MaxCachedItemsPerPrefab ?? 0);

        public void Initialize(
            ScrollRect scrollRect,
            int itemCount,
            Func<int, int, int, VirtualizedGridItem> createItem,
            Action<VirtualizedGridItem, int, int, int, uint> bindItem,
            Action<VirtualizedGridItem> recycleItem,
            VirtualizedGridOptions options)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("VirtualizedGridCore can only be initialized once.");
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
            _options = options ?? new VirtualizedGridOptions();
            ValidateOptions(_options);
            _itemCount = Math.Max(0, itemCount);
            _rowCount = GetRowCount();
            _scrollRect.vertical = _options.Vertical;
            _scrollRect.horizontal = !_options.Vertical;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = _content.anchorMin;
            _content.pivot = _content.anchorMin;
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
            _rowCount = GetRowCount();
            _snapTargetOffset = null;
            if (resetPosition)
            {
                SetScrollOffset(0f);
            }

            Refresh(true);
        }

        public void SnapToIndex(int index)
        {
            EnsureInitialized();
            if (!_options.Snap.Enabled || index < 0 || index >= _itemCount)
            {
                return;
            }

            SetSnapTarget(GetSnapOffset(index / _options.ColumnCount));
        }

        public void SnapToNearest()
        {
            EnsureInitialized();
            if (!_options.Snap.Enabled || _rowCount == 0)
            {
                return;
            }

            var rowSize = _options.Vertical
                ? _options.ItemSize.y + _options.ItemSpacing.y
                : _options.ItemSize.x + _options.ItemSpacing.x;
            var row = Mathf.Clamp(
                Mathf.RoundToInt(GetScrollOffset() / rowSize),
                0,
                _rowCount - 1);
            SetSnapTarget(GetSnapOffset(row));
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
                var range = CalculateVisibleRows();
                if (force ||
                    range.min != _options.FirstShownRow ||
                    range.max != _options.LastShownRow)
                {
                    RecycleOutside(range.min, range.max);
                    for (var row = Math.Max(0, range.min); row <= range.max; row++)
                    {
                        for (var slot = 0; slot < _options.ColumnCount; slot++)
                        {
                            var index = row * _options.ColumnCount + slot;
                            if (index >= _itemCount)
                            {
                                break;
                            }

                            EnsureItem(
                                index,
                                _options.Vertical ? row : slot,
                                _options.Vertical ? slot : row);
                        }
                    }

                    _options.LastShownRow = range.max;
                    _options.FirstShownRow = range.min;
                }

                PositionShownItems();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private (int min, int max) CalculateVisibleRows()
        {
            if (_rowCount == 0)
            {
                return (-1, -1);
            }

            var rowSize = _options.Vertical
                ? _options.ItemSize.y + _options.ItemSpacing.y
                : _options.ItemSize.x + _options.ItemSpacing.x;
            var start = Mathf.Max(0f, GetScrollOffset() - _options.BufferPixels);
            var end = GetScrollOffset() +
                      (_options.Vertical ? _viewport.rect.height : _viewport.rect.width) +
                      _options.BufferPixels;
            var min = Mathf.Clamp(Mathf.FloorToInt(start / rowSize), 0, _rowCount - 1);
            var max = Mathf.Clamp(Mathf.CeilToInt(end / rowSize), min, _rowCount - 1);
            return (min, max);
        }

        private void EnsureItem(int index, int row, int column)
        {
            if (_shownItems.ContainsKey(index))
            {
                return;
            }

            var item = _createItem(index, row, column);
            if (item == null)
            {
                throw new InvalidOperationException($"Failed to create virtualized grid item {index}.");
            }

            item.transform.SetParent(_content, false);
            item.gameObject.SetActive(true);
            var version = item.PrepareBinding(index);
            _bindItem(item, index, row, column, version);
            _shownItems.Add(index, item);
            _options.Animator?.OnShown(item, index);
        }

        private void RecycleOutside(int minRow, int maxRow)
        {
            _recycleIndices.Clear();
            foreach (var pair in _shownItems)
            {
                var row = pair.Key / _options.ColumnCount;
                if (row < minRow || row > maxRow)
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
                var line = pair.Key / _options.ColumnCount;
                var slot = pair.Key % _options.ColumnCount;
                var main = line * (_options.Vertical
                    ? _options.ItemSize.y + _options.ItemSpacing.y
                    : _options.ItemSize.x + _options.ItemSpacing.x);
                var cross = slot * (_options.Vertical
                    ? _options.ItemSize.x + _options.ItemSpacing.x
                    : _options.ItemSize.y + _options.ItemSpacing.y);
                var rect = new Rect(
                    _options.Vertical ? cross : main,
                    _options.Vertical ? main : cross,
                    _options.ItemSize.x,
                    _options.ItemSize.y);
                if (_options.Decoration != null)
                {
                    rect = _options.Decoration.Decorate(pair.Key, rect);
                }

                pair.Value.RectTransform.anchoredPosition = new Vector2(
                    rect.x,
                    -rect.y);
                pair.Value.RectTransform.sizeDelta = new Vector2(rect.width, rect.height);
            }
        }

        private int GetRowCount()
        {
            return _options.ColumnCount == 0
                ? 0
                : (_itemCount + _options.ColumnCount - 1) / _options.ColumnCount;
        }

        private void UpdateContentSize()
        {
            var size = _content.sizeDelta;
            var crossSize = _options.ColumnCount *
                            (_options.Vertical ? _options.ItemSize.x : _options.ItemSize.y) +
                            Math.Max(0, _options.ColumnCount - 1) *
                            (_options.Vertical ? _options.ItemSpacing.x : _options.ItemSpacing.y);
            var mainSize = _rowCount *
                           (_options.Vertical ? _options.ItemSize.y : _options.ItemSize.x) +
                           Math.Max(0, _rowCount - 1) *
                           (_options.Vertical ? _options.ItemSpacing.y : _options.ItemSpacing.x);
            size.x = _options.Vertical ? crossSize : mainSize;
            size.y = _options.Vertical ? mainSize : crossSize;
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
            return _options.Vertical
                ? Mathf.Max(0f, _content.sizeDelta.y - _viewport.rect.height)
                : Mathf.Max(0f, _content.sizeDelta.x - _viewport.rect.width);
        }

        private float GetSnapOffset(int row)
        {
            var start = row * (_options.Vertical
                ? _options.ItemSize.y + _options.ItemSpacing.y
                : _options.ItemSize.x + _options.ItemSpacing.x);
            var itemSize = _options.Vertical ? _options.ItemSize.y : _options.ItemSize.x;
            var viewportSize = _options.Vertical ? _viewport.rect.height : _viewport.rect.width;
            return _options.Snap.Alignment switch
            {
                VirtualizedSnapAlignment.Center =>
                    start + (itemSize - viewportSize) * 0.5f,
                VirtualizedSnapAlignment.End =>
                    start + itemSize - viewportSize,
                _ => start
            };
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

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("VirtualizedGridCore is not initialized.");
            }
        }

        private static void ValidateOptions(VirtualizedGridOptions options)
        {
            if (options.Snap == null)
            {
                options.Snap = new VirtualizedSnapOptions();
            }

            if (options.ColumnCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.ColumnCount),
                    "ColumnCount must be greater than zero.");
            }

            if (options.ItemSize.x <= 0f || options.ItemSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.ItemSize),
                    "ItemSize must be greater than zero.");
            }

            if (options.ItemSpacing.x < 0f ||
                options.ItemSpacing.y < 0f ||
                options.BufferPixels < 0f ||
                options.Snap?.SmoothTime < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "ItemSpacing and BufferPixels cannot be negative.");
            }

            if (options.MaxCachedItemsPerPrefab < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.MaxCachedItemsPerPrefab));
            }
        }
    }
}
