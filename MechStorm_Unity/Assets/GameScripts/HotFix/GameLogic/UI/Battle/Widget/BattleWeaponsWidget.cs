using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
	public partial class BattleWeaponsWidget
	{
		private BattleHUDItemList<WeaponItem> _items;
		public event Action Clicked;
		public float ContentHeight => _items.GetContentHeight(1);

		protected override void OnCreate()
		{
			_items = new BattleHUDItemList<WeaponItem>(this);
		}

		public UniTask InitializeAsync(CancellationToken cancellationToken)
		{
			return _items.PrepareAsync(1, "WeaponItem",
				(item, index) => item.Clicked += () => Clicked?.Invoke(), cancellationToken);
		}

		public void SetData(int damage, int minRange, int maxRange, bool interactable, bool selected)
		{
			_items.SetCount(1);
			_items.Get(0).SetData(damage, minRange, maxRange, interactable, selected);
		}

		protected override void OnDestroy()
		{
			_items?.Dispose();
			Clicked = null;
		}
	}
}
