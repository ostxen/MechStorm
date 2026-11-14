using System;
using MechStorm.Battle.Data;

namespace MechStorm.Battle.Units
{
    public sealed class MechData
    {
        public int Id { get; }
        
        public string Name { get; }
        
        public BasicAttackData BasicAttack { get; }
        
        public int MaxDurability { get; }

        public int MoveRange { get; }

        public MechData(int id, string name, BasicAttackData basicAttack, int maxDurability, int moveRange)
        {
            Id = id;
            Name = name;
            BasicAttack = basicAttack ?? throw new ArgumentNullException(nameof(basicAttack));
            MaxDurability = maxDurability;
            MoveRange = moveRange;
        }
    }
}