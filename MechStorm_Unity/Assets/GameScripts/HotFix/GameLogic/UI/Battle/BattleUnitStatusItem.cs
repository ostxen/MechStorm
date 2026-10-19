using UnityEngine;
using TEngine;

namespace GameLogic
{
	public partial class BattleUnitStatusItem
	{
		protected override void OnCreate()
		{
			if (m_tmpPilotName == null || _actionPointBar == null)
			{
				throw new GameFrameworkException("BattleUnitStatusItem 的姓名或 AP 绑定无效。");
			}
			// 初始化不调用 Clear，保留尚无真实数据字段的 Prefab 默认显示。
		}

		public void SetPilotName(string pilotName)
		{
			m_tmpPilotName.SetText(pilotName);
		}

		public void SetPilotInfo(string pilotName = "驾驭员", int level = 0, string pilotHead = "default" , string classIcon = "fighter")
		{
			m_tmpPilotName.SetText(pilotName);
			m_tmpLevelNum.SetText(level.ToString());
			m_imgPilotHeadIcon.SetSprite(pilotHead);
			m_imgClassIcon.SetSprite(classIcon);
		}

		public void SetBodyDurability(int current, int max)
		{
			int displayMax = Mathf.Max(0, max);
			_bodyBar.SetValue(Mathf.Clamp(current, 0, displayMax), displayMax);
			_bodyBar.Visible = true;
		}

		public void SetLeftArmDurability(int current, int max)
		{
			int displayMax = Mathf.Max(0, max);
			_leftArmBar.SetValue(Mathf.Clamp(current, 0, displayMax), displayMax);
			_leftArmBar.Visible = true;

		}

		public void SetRightArmDurability(int current, int max)
		{
			int displayMax = Mathf.Max(0, max);
			_rightArmBar.SetValue(Mathf.Clamp(current, 0, displayMax), displayMax);
			_rightArmBar.Visible = true;
		}

		public void SetLegDurability(int current, int max)
		{
			int displayMax = Mathf.Max(0, max);
			_legBar.SetValue(Mathf.Clamp(current, 0, displayMax), displayMax);
			_legBar.Visible = true;
		}

		public void SetActionPoint(int current, int max)
		{
			_actionPointBar.SetValue(current, max);
			_actionPointBar.Visible = true;
		}

		public void SetActionPointPreview(int cost)
		{
			_actionPointBar.SetHighlight(cost);
		}

		public void ClearActionPointPreview()
		{
			_actionPointBar.ClearHighlight();
		}

		/// <summary>
		/// 恢复默认信息并清空数值，槽位和标签保持显示。
		/// </summary>
		public void Clear()
		{
			SetPilotInfo();
			SetBodyDurability(0, 0);
			SetLeftArmDurability(0, 0);
			SetRightArmDurability(0, 0);
			SetLegDurability(0, 0);
			SetActionPoint(0, 0);
			ClearActionPointPreview();
		}
	}
}
