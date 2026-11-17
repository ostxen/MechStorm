using System;
using System.Collections.Generic;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 集中登记动画资源 Lease，在重置或释放时归还资源，供 Controller 重建和对象池回收使用。
    /// </summary>
    public sealed class AnimationResetContext : IDisposable
    {
        private readonly List<AnimationClipLease> _leases =
            new List<AnimationClipLease>();
        private bool _isDisposed;

        public void Track(AnimationClipLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            if (!_leases.Contains(lease))
            {
                _leases.Add(lease);
            }
        }

        public void Reset()
        {
            for (int i = _leases.Count - 1; i >= 0; i--)
            {
                _leases[i].Release();
            }

            _leases.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Reset();
        }
    }
}
