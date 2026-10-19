namespace GameLogic
{
	public partial class CombatInfoWidget
	{
		public void SetUnits(string actorName, int actorActionPoint, int actorMaxActionPoint,
			string targetName, int targetActionPoint, int targetMaxActionPoint, int actionPointCost)
		{
			_actorStatus.SetPilotName(actorName);
			_actorStatus.SetActionPoint(actorActionPoint, actorMaxActionPoint);
			_actorStatus.SetActionPointPreview(actionPointCost);
			_targetStatus.SetPilotName(targetName);
			_targetStatus.SetActionPoint(targetActionPoint, targetMaxActionPoint);
		}
	}
}
