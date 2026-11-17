namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 常用虚拟列表 Adapter。
    ///
    /// 创建/回收由 Adapter 负责，绑定发生在 Item 准备好 binding version 之后。
    /// 这对应 LoopScrollRect 的 DataSource/PrefabSource 分离，以及
    /// AlicizaX RecyclerView 的 Adapter/ViewHolder 生命周期。
    /// </summary>
    public interface IVirtualizedListAdapter
    {
        int GetItemCount();
        VirtualizedListItem CreateItem(VirtualizedListView listView, int index);
        void Bind(VirtualizedListItem item, int index, uint bindingVersion);
        void OnRecycle(VirtualizedListItem item);
    }

    public abstract class VirtualizedListAdapter : IVirtualizedListAdapter
    {
        public abstract int GetItemCount();
        public abstract VirtualizedListItem CreateItem(VirtualizedListView listView, int index);
        public abstract void Bind(VirtualizedListItem item, int index, uint bindingVersion);

        public virtual void OnRecycle(VirtualizedListItem item)
        {
            item?.ClearView();
        }
    }
}
