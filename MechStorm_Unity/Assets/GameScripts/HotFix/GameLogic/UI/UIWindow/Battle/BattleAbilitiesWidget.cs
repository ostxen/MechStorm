using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
	public partial class BattleAbilitiesWidget
	{
		private BattleHUDItemList<AbilityItem> _items;
		public event Action<int> Selected;

		protected override void OnCreate()
		{
			_items = new BattleHUDItemList<AbilityItem>(this);
		}

		public UniTask InitializeAsync(int capacity, CancellationToken cancellationToken)
		{
			return _items.PrepareAsync(capacity, "AbilityItem", (item, index) =>
				item.Clicked += () => Selected?.Invoke(index), cancellationToken);
		}

		public float GetContentHeight(int count) => _items.GetContentHeight(count);

		public void SetCount(int count)
		{
			_items.SetCount(count);
		}

		public void SetItem(int index, string title, int actionPointCost, bool interactable, bool selected)
		{
			_items.Get(index).SetData(title, actionPointCost, interactable, selected);
		}

		protected override void OnDestroy()
		{
			_items?.Dispose();
			Selected = null;
		}
	}
}
