using System;

namespace MechStorm.Battle.Data
{
    /// <summary>
    /// 指定本次技能选择采用自身范围还是已验证的基础武器目标范围。
    /// </summary>
    public enum AbilityTargetRangeSource
    {
        Ability,
        BasicWeapon,
    }

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

        public int ActionPointCost { get; }

        public TargetRule TargetRule { get; }

        /// <summary>
        /// 表示技能需要基础武器条件，不覆盖技能自身配置的使用范围。
        /// </summary>
        public bool RequiresBasicWeapon { get; }

        public AbilityDefinition(int abilityId, string name, int minRange, int maxRange, int damage,
            int actionPointCost, TargetRule targetRule, bool requiresBasicWeapon = false)
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

            if (actionPointCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointCost), actionPointCost,
                    "Action point cost must be non-negative.");
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
            ActionPointCost = actionPointCost;
            TargetRule = targetRule;
            RequiresBasicWeapon = requiresBasicWeapon;
        }
    }
}