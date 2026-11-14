using System;

namespace MechStorm.Battle.Units
{
    public sealed class MechRuntime
    {
        public int CurrentDurability { get; private set; }

        public bool IsDestroyed => CurrentDurability <= 0;

        public MechRuntime(int currentDurability)
        {
            CurrentDurability = currentDurability;
        }

        public void TakeDamage(int damage)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage must be non-negative.");
            }

            if (damage > 0)
            {
                CurrentDurability = Math.Max(0, CurrentDurability - damage);
            }
        }
    }
}