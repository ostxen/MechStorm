using System;
using Cysharp.Threading.Tasks;
using MechStorm.Battle.Statistics;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattleStatisticsUI")]
	public partial class BattleStatisticsUI
	{
		private const string ItemLocation = "BattleUnitStatisticsItem";
		public event Action BackRequested;
		public event Action HomeRequested;

		protected override void RegisterEvent()
		{
			_pageHeader.BackRequested += ReturnToResult;
			_pageHeader.HomeRequested += ReturnToMain;
		}

		protected override void OnCreate()
		{
			_pageHeader.SetNavigationInteractable(false);
		}

		/// <summary>
		/// 填充只读统计行，全部加载完成后开放页面导航。
		/// </summary>
		public async UniTask DisplayAsync(BattleStatisticsReport report)
		{
			foreach (var unit in report.Units)
			{
				var rowObject = await UIModule.Resource.LoadGameObjectAsync(ItemLocation, m_rectContent);
				var row = CreateWidget<BattleUnitStatisticsItem>(rowObject);
				row.SetData(unit.PilotName, unit.KillCount, unit.DamageDealt, unit.DamageTaken);
			}
			_pageHeader.SetNavigationInteractable(true);
		}

		private void ReturnToResult()
		{
			BackRequested?.Invoke();
		}

		private void ReturnToMain()
		{
			HomeRequested?.Invoke();
		}

		protected override void OnDestroy()
		{
			BackRequested = null;
			HomeRequested = null;
		}
	}
}
