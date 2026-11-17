using System;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 用 Fenwick tree 保存虚拟列表的 Item 尺寸，支持动态尺寸下的前缀和与反查索引。
    /// </summary>
    internal sealed class VirtualizedItemSizeMap
    {
        private float[] _sizes = Array.Empty<float>();
        private float[] _tree = Array.Empty<float>();

        public int Count => _sizes.Length;

        public void Reset(int count, float defaultSize)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _sizes = new float[count];
            _tree = new float[count + 1];
            for (var i = 0; i < count; i++)
            {
                _sizes[i] = defaultSize;
                Add(i, defaultSize);
            }
        }

        public float GetSize(int index)
        {
            return _sizes[index];
        }

        public bool SetSize(int index, float size)
        {
            if (size <= 0f)
            {
                return false;
            }

            var delta = size - _sizes[index];
            if (Math.Abs(delta) < 0.01f)
            {
                return false;
            }

            _sizes[index] = size;
            Add(index, delta);
            return true;
        }

        public float PrefixSum(int exclusiveIndex)
        {
            var result = 0f;
            for (var index = Math.Min(exclusiveIndex, _sizes.Length); index > 0;)
            {
                result += _tree[index];
                index -= index & -index;
            }

            return result;
        }

        public int FindIndex(float value)
        {
            if (_sizes.Length == 0)
            {
                return -1;
            }

            var index = 0;
            var bit = HighestPowerOfTwo(_sizes.Length);
            var accumulated = 0f;
            while (bit != 0)
            {
                var next = index + bit;
                if (next <= _sizes.Length && accumulated + _tree[next] <= value)
                {
                    index = next;
                    accumulated += _tree[next];
                }

                bit >>= 1;
            }

            return Math.Min(index, _sizes.Length - 1);
        }

        private void Add(int index, float value)
        {
            for (var i = index + 1; i < _tree.Length; i += i & -i)
            {
                _tree[i] += value;
            }
        }

        private static int HighestPowerOfTwo(int value)
        {
            var result = 1;
            while ((result << 1) <= value)
            {
                result <<= 1;
            }

            return result;
        }
    }
}
