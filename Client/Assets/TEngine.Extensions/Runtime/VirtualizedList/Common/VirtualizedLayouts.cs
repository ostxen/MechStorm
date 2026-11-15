using System;
using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public readonly struct VirtualizedLayoutRange
    {
        public VirtualizedLayoutRange(int minIndex, int maxIndex)
        {
            MinIndex = minIndex;
            MaxIndex = maxIndex;
        }

        public int MinIndex { get; }
        public int MaxIndex { get; }
        public bool IsEmpty => MinIndex < 0 || MaxIndex < MinIndex;

        public static VirtualizedLayoutRange Empty => new(-1, -1);
    }

    /// <summary>
    /// 业务布局策略。坐标使用 content 左上角为原点，Y 轴向下。
    /// Core 负责把逻辑 Rect 转换成 UGUI anchoredPosition。
    /// </summary>
    public interface IVirtualizedListLayout
    {
        Vector2 ContentSize { get; }

        void Reset(int itemCount, Vector2 viewportSize);
        VirtualizedLayoutRange CalculateVisibleRange(
            Vector2 scrollOffset,
            Vector2 viewportSize,
            float bufferPixels);
        Rect GetItemRect(int index);
        void SetItemSize(int index, Vector2 size);
    }

    public sealed class LinearVirtualizedLayout : IVirtualizedListLayout
    {
        private readonly bool _vertical;
        private readonly float _defaultSize;
        private readonly float _spacing;
        private readonly VirtualizedItemSizeMap _sizeMap = new();
        private int _itemCount;

        public LinearVirtualizedLayout(
            bool vertical = true,
            float defaultSize = 100f,
            float spacing = 0f)
        {
            if (defaultSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultSize));
            }

            if (spacing < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spacing));
            }

            _vertical = vertical;
            _defaultSize = defaultSize;
            _spacing = spacing;
        }

        public Vector2 ContentSize
        {
            get
            {
                var length = _sizeMap.PrefixSum(_itemCount) +
                             Math.Max(0, _itemCount - 1) * _spacing;
                return _vertical ? new Vector2(0f, length) : new Vector2(length, 0f);
            }
        }

        public void Reset(int itemCount, Vector2 viewportSize)
        {
            _itemCount = Math.Max(0, itemCount);
            _sizeMap.Reset(_itemCount, _defaultSize);
        }

        public VirtualizedLayoutRange CalculateVisibleRange(
            Vector2 scrollOffset,
            Vector2 viewportSize,
            float bufferPixels)
        {
            if (_itemCount == 0)
            {
                return VirtualizedLayoutRange.Empty;
            }

            var start = Math.Max(
                0f,
                (_vertical ? scrollOffset.y : scrollOffset.x) - bufferPixels);
            var end = (_vertical ? scrollOffset.y + viewportSize.y : scrollOffset.x + viewportSize.x) +
                      bufferPixels;
            var min = Math.Max(0, _sizeMap.FindIndex(FindTreeValue(start)));
            var max = Math.Min(_itemCount - 1, _sizeMap.FindIndex(FindTreeValue(end)));
            return new VirtualizedLayoutRange(min, Math.Max(min, max));
        }

        public Rect GetItemRect(int index)
        {
            ValidateIndex(index);
            var offset = _sizeMap.PrefixSum(index) + index * _spacing;
            var size = _sizeMap.GetSize(index);
            return _vertical
                ? new Rect(0f, offset, 0f, size)
                : new Rect(offset, 0f, size, 0f);
        }

        public void SetItemSize(int index, Vector2 size)
        {
            ValidateIndex(index);
            _sizeMap.SetSize(index, _vertical ? size.y : size.x);
        }

        private float FindTreeValue(float contentOffset)
        {
            return Math.Max(0f, contentOffset - _spacing * 0.5f);
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _itemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    /// <summary>
    /// 多列不等高瀑布流。Item 尺寸变化后会重新计算后续列的位置。
    /// </summary>
    public sealed class WaterfallVirtualizedLayout : IVirtualizedListLayout
    {
        private readonly int _columnCount;
        private readonly float _itemWidth;
        private readonly float _defaultHeight;
        private readonly Vector2 _spacing;
        private Rect[] _rects = Array.Empty<Rect>();
        private int _itemCount;
        private Vector2 _contentSize;
        private float _viewportWidth;

        public WaterfallVirtualizedLayout(
            int columnCount,
            float itemWidth,
            float defaultHeight = 100f,
            Vector2 spacing = default)
        {
            if (columnCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columnCount));
            }

            if (itemWidth <= 0f || defaultHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(itemWidth));
            }

            _columnCount = columnCount;
            _itemWidth = itemWidth;
            _defaultHeight = defaultHeight;
            _spacing = spacing;
            if (spacing.x < 0f || spacing.y < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spacing));
            }

        }

        public Vector2 ContentSize => _contentSize;

        public void Reset(int itemCount, Vector2 viewportSize)
        {
            _itemCount = Math.Max(0, itemCount);
            _viewportWidth = viewportSize.x;
            _rects = new Rect[_itemCount];
            var heights = new float[_itemCount];
            for (var i = 0; i < _itemCount; i++)
            {
                heights[i] = _defaultHeight;
            }

            Rebuild(heights);
        }

        public VirtualizedLayoutRange CalculateVisibleRange(
            Vector2 scrollOffset,
            Vector2 viewportSize,
            float bufferPixels)
        {
            var min = -1;
            var max = -1;
            var visible = new Rect(
                scrollOffset.x - bufferPixels,
                scrollOffset.y - bufferPixels,
                viewportSize.x + bufferPixels * 2f,
                viewportSize.y + bufferPixels * 2f);
            for (var i = 0; i < _rects.Length; i++)
            {
                if (!visible.Overlaps(_rects[i], true))
                {
                    continue;
                }

                min = min < 0 ? i : Math.Min(min, i);
                max = Math.Max(max, i);
            }

            return min < 0 ? VirtualizedLayoutRange.Empty : new VirtualizedLayoutRange(min, max);
        }

        public Rect GetItemRect(int index)
        {
            if (index < 0 || index >= _rects.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _rects[index];
        }

        public void SetItemSize(int index, Vector2 size)
        {
            if (index < 0 || index >= _itemCount || size.y <= 0f)
            {
                return;
            }

            var heights = new float[_itemCount];
            for (var i = 0; i < _itemCount; i++)
            {
                heights[i] = _rects[i].height;
            }

            heights[index] = size.y;
            Rebuild(heights);
        }

        private void Rebuild(float[] heights)
        {
            var columnHeights = new float[_columnCount];
            _rects = new Rect[_itemCount];
            for (var i = 0; i < _itemCount; i++)
            {
                var column = GetShortestColumn(columnHeights);
                var x = column * (_itemWidth + _spacing.x);
                var y = columnHeights[column];
                _rects[i] = new Rect(x, y, _itemWidth, heights[i]);
                columnHeights[column] += heights[i] + _spacing.y;
            }

            var width = _columnCount * _itemWidth +
                        Math.Max(0, _columnCount - 1) * _spacing.x;
            var height = 0f;
            for (var i = 0; i < columnHeights.Length; i++)
            {
                height = Math.Max(height, columnHeights[i] - _spacing.y);
            }

            _contentSize = new Vector2(
                Math.Max(width, _viewportWidth),
                Math.Max(0f, height));
        }

        private static int GetShortestColumn(float[] heights)
        {
            var index = 0;
            for (var i = 1; i < heights.Length; i++)
            {
                if (heights[i] < heights[index])
                {
                    index = i;
                }
            }

            return index;
        }
    }

    /// <summary>
    /// 带 Section 起始间隔的线性布局，适合分组列表和分段列表。
    /// </summary>
    public sealed class SectionedLinearVirtualizedLayout : IVirtualizedListLayout
    {
        private readonly Func<int, bool> _isSectionStart;
        private readonly float _defaultSize;
        private readonly float _itemSpacing;
        private readonly float _sectionSpacing;
        private float[] _sizes = Array.Empty<float>();
        private Rect[] _rects = Array.Empty<Rect>();
        private int _itemCount;
        private Vector2 _contentSize;

        public SectionedLinearVirtualizedLayout(
            Func<int, bool> isSectionStart,
            float defaultSize = 100f,
            float itemSpacing = 0f,
            float sectionSpacing = 16f)
        {
            _isSectionStart = isSectionStart ?? throw new ArgumentNullException(nameof(isSectionStart));
            if (defaultSize <= 0f ||
                itemSpacing < 0f ||
                sectionSpacing < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultSize));
            }

            _defaultSize = defaultSize;
            _itemSpacing = itemSpacing;
            _sectionSpacing = sectionSpacing;
        }

        public Vector2 ContentSize => _contentSize;

        public void Reset(int itemCount, Vector2 viewportSize)
        {
            _itemCount = Math.Max(0, itemCount);
            _sizes = new float[_itemCount];
            for (var i = 0; i < _sizes.Length; i++)
            {
                _sizes[i] = _defaultSize;
            }

            Rebuild();
        }

        public VirtualizedLayoutRange CalculateVisibleRange(
            Vector2 scrollOffset,
            Vector2 viewportSize,
            float bufferPixels)
        {
            var start = Math.Max(0f, scrollOffset.y - bufferPixels);
            var end = scrollOffset.y + viewportSize.y + bufferPixels;
            var min = -1;
            var max = -1;
            for (var i = 0; i < _rects.Length; i++)
            {
                var rect = _rects[i];
                if (rect.y + rect.height < start || rect.y > end)
                {
                    continue;
                }

                min = min < 0 ? i : Math.Min(min, i);
                max = Math.Max(max, i);
            }

            return min < 0 ? VirtualizedLayoutRange.Empty : new VirtualizedLayoutRange(min, max);
        }

        public Rect GetItemRect(int index)
        {
            ValidateIndex(index);
            return _rects[index];
        }

        public void SetItemSize(int index, Vector2 size)
        {
            ValidateIndex(index);
            if (size.y <= 0f)
            {
                return;
            }

            _sizes[index] = size.y;
            Rebuild();
        }

        private void Rebuild()
        {
            _rects = new Rect[_itemCount];
            var offset = 0f;
            for (var i = 0; i < _itemCount; i++)
            {
                if (i > 0)
                {
                    offset += _itemSpacing;
                }

                if (i > 0 && _isSectionStart(i))
                {
                    offset += _sectionSpacing;
                }

                _rects[i] = new Rect(0f, offset, 0f, _sizes[i]);
                offset += _sizes[i];
            }

            _contentSize = new Vector2(0f, offset);
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _itemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    /// <summary>
    /// 业务自定义矩形布局。适合异形卡牌、弧形、扇形和棋盘等低频规则。
    /// </summary>
    public sealed class CustomRectVirtualizedLayout : IVirtualizedListLayout
    {
        private readonly Func<int, Rect> _rectProvider;
        private readonly Vector2 _contentSize;
        private readonly int _itemCount;

        public CustomRectVirtualizedLayout(
            int itemCount,
            Func<int, Rect> rectProvider,
            Vector2 contentSize)
        {
            _itemCount = Math.Max(0, itemCount);
            _rectProvider = rectProvider ?? throw new ArgumentNullException(nameof(rectProvider));
            _contentSize = contentSize;
        }

        public Vector2 ContentSize => _contentSize;

        public void Reset(int itemCount, Vector2 viewportSize)
        {
            if (itemCount != _itemCount)
            {
                throw new InvalidOperationException(
                    "CustomRectVirtualizedLayout item count is fixed for this instance.");
            }
        }

        public VirtualizedLayoutRange CalculateVisibleRange(
            Vector2 scrollOffset,
            Vector2 viewportSize,
            float bufferPixels)
        {
            var visible = new Rect(
                scrollOffset.x - bufferPixels,
                scrollOffset.y - bufferPixels,
                viewportSize.x + bufferPixels * 2f,
                viewportSize.y + bufferPixels * 2f);
            var min = -1;
            var max = -1;
            for (var i = 0; i < _itemCount; i++)
            {
                if (!visible.Overlaps(_rectProvider(i), true))
                {
                    continue;
                }

                min = min < 0 ? i : Math.Min(min, i);
                max = Math.Max(max, i);
            }

            return min < 0 ? VirtualizedLayoutRange.Empty : new VirtualizedLayoutRange(min, max);
        }

        public Rect GetItemRect(int index)
        {
            if (index < 0 || index >= _itemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _rectProvider(index);
        }

        public void SetItemSize(int index, Vector2 size)
        {
        }
    }
}
