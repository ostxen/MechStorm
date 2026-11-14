using System;

namespace MechStorm.Battle.Units
{
    public sealed class PilotRuntime
    {
        public int CurrentActionPoint { get; private set; }

        public PilotRuntime(int currentActionPoint)
        {
            if (currentActionPoint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentActionPoint), currentActionPoint,
                    "Current action point must be non-negative.");
            }

            CurrentActionPoint = currentActionPoint;
        }

        public bool TrySpendActionPoint(int amount)
        {
            if (!HasEnoughActionPoint(amount))
            {
                return false;
            }

            CurrentActionPoint -= amount;
            return true;
        }

        public bool HasEnoughActionPoint(int amount)
        {
            ValidateAmount(amount);
            return CurrentActionPoint >= amount;
        }

        public void ResetActionPointToMax(int maxActionPoint)
        {
            if (maxActionPoint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxActionPoint), maxActionPoint,
                    "Maximum action point must be non-negative.");
            }

            CurrentActionPoint = maxActionPoint;
        }

        private static void ValidateAmount(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "Action point amount must be non-negative.");
            }
        }
    }
}