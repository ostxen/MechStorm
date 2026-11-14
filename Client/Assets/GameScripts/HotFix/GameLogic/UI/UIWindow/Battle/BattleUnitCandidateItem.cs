using TMPro;

namespace GameLogic
{
	public partial class BattleUnitCandidateItem
	{
		private TextMeshProUGUI _pilotInitial;
		private TextMeshProUGUI _mechName;
		private TextMeshProUGUI _unitNumber;

		protected override void BindMemberProperty()
		{
			_pilotInitial = FindChildComponent<TextMeshProUGUI>("PilotInitial");
			_mechName = FindChildComponent<TextMeshProUGUI>("MechName");
			_unitNumber = FindChildComponent<TextMeshProUGUI>("UnitNumber");
		}

		public void SetData(int unitId, string pilotName, string mechName)
		{
			m_tmpPilotName.text = pilotName;
			_pilotInitial.text = pilotName.Substring(0, 1);
			_mechName.text = mechName;
			_unitNumber.text = $"#{unitId:00}";
		}
	}
}
