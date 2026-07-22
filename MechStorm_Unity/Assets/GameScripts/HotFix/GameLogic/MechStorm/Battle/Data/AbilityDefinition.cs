using System;

namespace MechStorm.Battle.Data
{
    /// <summary>
    /// 描述主动技能的静态配置。
    /// </summary>
    public sealed class AbilityDefinition
    {
        public int AbilityId { get; }

        public string Name { get; }

        public int MinRange { get; }

        public int MaxRange { get; }

        public int Damage { get; }

        public TargetRule TargetRule { get; }

        public AbilityDefinition(int abilityId, string name, int minRange, int maxRange, int damage, TargetRule targetRule)
        {
            if (abilityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(abilityId), abilityId,
                    "Ability ID must be greater than zero.");
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Ability name cannot be empty or whitespace.", nameof(name));
            }

            if (minRange < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minRange), minRange,
                    "Minimum range must be at least one.");
            }

            if (maxRange < minRange)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRange), maxRange,
                    "Maximum range must be greater than or equal to minimum range.");
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage,
                    "Damage must be non-negative.");
            }

            if (!Enum.IsDefined(typeof(TargetRule), targetRule))
            {
                throw new ArgumentOutOfRangeException(nameof(targetRule), targetRule,
                    "Target rule must be defined.");
            }

            AbilityId = abilityId;
            Name = name;
            MinRange = minRange;
            MaxRange = maxRange;
            Damage = damage;
            TargetRule = targetRule;
        }
    }
}