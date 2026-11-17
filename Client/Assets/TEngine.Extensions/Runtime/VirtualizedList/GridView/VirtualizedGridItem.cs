using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 独立虚拟网格 Item。
    /// </summary>
    public class VirtualizedGridItem : MonoBehaviour
    {
        private uint _bindingVersion;
        private int _bindingDataIndex = -1;

        public RectTransform RectTransform => (RectTransform)transform;
        public string PrefabKey { get; internal set; }
        public int CurrentIndex => _bindingDataIndex;
        public uint CurrentBindingVersion => _bindingVersion;

        public uint PrepareBinding(int dataIndex)
        {
            _bindingVersion = _bindingVersion == uint.MaxValue ? 1u : _bindingVersion + 1u;
            _bindingDataIndex = dataIndex;
            return _bindingVersion;
        }

        public bool IsBindingCurrent(uint bindingVersion)
        {
            return bindingVersion != 0 &&
                   bindingVersion == _bindingVersion &&
                   _bindingDataIndex >= 0;
        }

        public void ClearBinding()
        {
            _bindingDataIndex = -1;
            _bindingVersion = _bindingVersion == uint.MaxValue ? 1u : _bindingVersion + 1u;
        }

        public virtual void ClearView()
        {
        }
    }
}
