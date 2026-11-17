namespace TEngine.Extensions.VirtualizedList
{
    public interface IVirtualizedGridAdapter
    {
        int GetItemCount();
        VirtualizedGridItem CreateItem(
            VirtualizedGridView gridView,
            int index,
            int row,
            int column);
        void Bind(
            VirtualizedGridItem item,
            int index,
            int row,
            int column,
            uint bindingVersion);
        void OnRecycle(VirtualizedGridItem item);
    }

    public abstract class VirtualizedGridAdapter : IVirtualizedGridAdapter
    {
        public abstract int GetItemCount();

        public abstract VirtualizedGridItem CreateItem(
            VirtualizedGridView gridView,
            int index,
            int row,
            int column);

        public abstract void Bind(
            VirtualizedGridItem item,
            int index,
            int row,
            int column,
            uint bindingVersion);

        public virtual void OnRecycle(VirtualizedGridItem item)
        {
            item?.ClearView();
        }
    }
}
