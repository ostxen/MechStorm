using System;
using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public interface IVirtualizedItemAnimator
    {
        void OnShown(MonoBehaviour item, int index);
        void OnRecycled(MonoBehaviour item, int index);
    }

    public interface IVirtualizedItemDecoration
    {
        Rect Decorate(int index, Rect layoutRect);
    }

    public interface IVirtualizedReorderHandler
    {
        bool CanMove(int fromIndex, int toIndex);
        void Move(int fromIndex, int toIndex);
    }

    public interface IVirtualizedPagingSource
    {
        int ItemCount { get; }
        bool HasMore { get; }
        bool IsLoading { get; }
        event Action<int> ItemCountChanged;
        void RequestNextPage();
    }

    public enum VirtualizedLoadState
    {
        None,
        Loading,
        Loaded,
        Failed,
        Empty
    }

    public sealed class VirtualizedPagingController : IDisposable
    {
        private IVirtualizedPagingSource _source;

        public event Action<int> ItemCountChanged;
        public IVirtualizedPagingSource Source => _source;

        public void Attach(IVirtualizedPagingSource source)
        {
            Detach();
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.ItemCountChanged += HandleItemCountChanged;
            ItemCountChanged?.Invoke(_source.ItemCount);
        }

        public void RequestNextPage()
        {
            if (_source != null && _source.HasMore && !_source.IsLoading)
            {
                _source.RequestNextPage();
            }
        }

        public void NotifyRange(int lastShownIndex)
        {
            if (_source == null ||
                lastShownIndex < _source.ItemCount - 1 ||
                !_source.HasMore)
            {
                return;
            }

            RequestNextPage();
        }

        public void Dispose()
        {
            Detach();
            ItemCountChanged = null;
        }

        private void Detach()
        {
            if (_source != null)
            {
                _source.ItemCountChanged -= HandleItemCountChanged;
                _source = null;
            }
        }

        private void HandleItemCountChanged(int count)
        {
            ItemCountChanged?.Invoke(count);
        }
    }
}
