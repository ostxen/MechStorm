using MechStorm.Battle.Foundation;

namespace MechStorm.Battle.Units
{
    public sealed class CombatUnitFactory
    {
        public CombatUnit Create(int unitId, PilotData pilot, MechData mech, Vector2Int position)
        {
            var pilotRuntime = new PilotRuntime(pilot.MaxActionPoint);
            var mechRuntime = new MechRuntime(mech.MaxDurability);
            return new CombatUnit(unitId, pilot, pilotRuntime, mech, mechRuntime, position);
        }
    }
}