using System;

namespace MechStorm.Battle.Units
{
    public sealed class PilotData
    {
        public int Id { get; }
        
        public string Name { get; }
        
        public int MaxActionPoint { get; }

        public PilotData(int id, string name, int maxActionPoint)
        {
            if (maxActionPoint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxActionPoint), maxActionPoint,
                    "Maximum action point must be non-negative.");
            }

            Id = id;
            Name = name;
            MaxActionPoint = maxActionPoint;
        }
    }
}