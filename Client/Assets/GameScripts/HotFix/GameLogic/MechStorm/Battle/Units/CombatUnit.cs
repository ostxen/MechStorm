using System;
using MechStorm.Battle.Foundation;

namespace MechStorm.Battle.Units
{
    public enum CombatFaction
    {
        TeamA,
        TeamB,
        Neutral,
    }

    public sealed class CombatUnit
    {
        public int UnitId { get; }

        public PilotData Pilot { get; }
        
        public PilotRuntime PilotRuntime { get; }
        
        public MechData Mech { get; }
        
        public MechRuntime MechRuntime { get; }
        
        public Vector2Int Position { get; private set; }

        public CombatUnit(int unitId, PilotData pilot, PilotRuntime pilotRuntime, MechData mech, MechRuntime mechRuntime, Vector2Int position)
        {
            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), unitId, "Unit ID must be greater than zero.");
            }

            UnitId = unitId;
            Pilot = pilot;
            PilotRuntime = pilotRuntime;
            Mech = mech;
            MechRuntime = mechRuntime;
            Position = position;
        }

        public void MoveTo(Vector2Int position)
        {
            Position = position;
        }

        public bool IsAlive() =>  !IsDead();

        public bool IsDead() => MechRuntime.IsDestroyed;
    }
}