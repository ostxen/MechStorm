using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class BattleUnitStatisticsItem
	{
		/// <summary>
		/// 填充一行只读统计；维修耐久当前没有数据来源，保持 Prefab 占位显示。
		/// </summary>
		public void SetData(string pilotName, int killCount, int damageDealt, int damageTaken)
		{
			m_tmpPilotName.SetText(pilotName);
			m_tmpKillCount.SetText(killCount.ToString());
			m_tmpDamageDealt.SetText(damageDealt.ToString());
			m_tmpDamageTaken.SetText(damageTaken.ToString());
		}
	}
}
