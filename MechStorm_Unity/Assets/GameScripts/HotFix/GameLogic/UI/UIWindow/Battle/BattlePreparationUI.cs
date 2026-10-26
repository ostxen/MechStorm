using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MechStorm.Battle.Units;
using TEngine;
using UnityEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattlePreparationUI")]
	public partial class BattlePreparationUI
	{
		private const string CandidateLocation = "UnitCandidateItem";
		private const string SequenceLocation = "BattleUnitActionSequenceItem";
		public event Action EnterBattleRequested;
		public event Action MenuRequested;

		protected override void OnCreate()
		{
			m_btnEnterBattle.interactable = false;
			m_btnEcs.interactable = false;
			m_btnBattleUnitFilter.interactable = false;
			m_btnClassDetails.interactable = false;
		}

		/// <summary>
		/// 展示本场固定队伍；所有条目就绪后才允许开始，不提供角色替换或排序。
		/// </summary>
		public async UniTask DisplayAsync(IReadOnlyList<CombatUnit> units, CancellationToken cancellationToken)
		{
			m_tmpParticipantCount.text = $"参战人数：{units.Count}";
			m_tmpObjectiveDescription.text = "全灭敌方单位";
			for (int i = 0; i < units.Count; i++)
			{
				var unit = units[i];
				var candidateObject = await LoadItemAsync(CandidateLocation, m_rectBattleUnitCandidates, cancellationToken);
				CreateWidget<BattleUnitCandidateItem>(candidateObject).SetData(unit.UnitId, unit.Pilot.Name, unit.Mech.Name);
				var sequenceObject = await LoadItemAsync(SequenceLocation, _battlcUnitActionSequences.Content, cancellationToken);
				_battlcUnitActionSequences.AddUnit(sequenceObject, i + 1, unit.Pilot.Name);
			}
			_battlcUnitActionSequences.SetInteractable(true);
			m_btnEcs.interactable = true;
			m_btnEnterBattle.interactable = true;
		}

		private static async UniTask<GameObject> LoadItemAsync(string location, Transform parent, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var itemObject = await UIModule.Resource.LoadGameObjectAsync(location, parent, cancellationToken);
			// 缓存分支不保证取消，晚到实例不能注册到已经关闭的准备页。
			if (cancellationToken.IsCancellationRequested)
			{
				if (itemObject != null)
				{
					UnityEngine.Object.Destroy(itemObject);
				}
				cancellationToken.ThrowIfCancellationRequested();
			}
			if (itemObject == null)
			{
				throw new GameFrameworkException($"战前准备条目加载失败：{location}");
			}
			return itemObject;
		}

		protected override void OnDestroy()
		{
			EnterBattleRequested = null;
			MenuRequested = null;
		}

		#region 事件

		private partial UniTaskVoid OnClickEcsBtn()
		{
			MenuRequested?.Invoke();

			return default;
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
			m_btnEnterBattle.interactable = false;
			m_btnEcs.interactable = false;
			EnterBattleRequested?.Invoke();

			return default;
		}

		#endregion
	}
}
