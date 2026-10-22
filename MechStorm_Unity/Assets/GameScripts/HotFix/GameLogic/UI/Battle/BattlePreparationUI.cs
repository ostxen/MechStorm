using System;
using Cysharp.Threading.Tasks;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattlePreparationUI")]
	public partial class BattlePreparationUI
	{
		public event Action EnterBattleRequested;

		protected override void OnCreate()
		{
			
		}

		protected override void OnDestroy()
		{
			
		}

		#region 事件

		private async partial UniTaskVoid OnClickEcsBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickBattleUnitFilterBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickClassDetailsBtn()
		{
			await UniTask.Yield();
		}

		private partial UniTaskVoid OnClickEnterBattleBtn()
		{
			EnterBattleRequested?.Invoke();

			return default;
		}

		#endregion
	}
}
