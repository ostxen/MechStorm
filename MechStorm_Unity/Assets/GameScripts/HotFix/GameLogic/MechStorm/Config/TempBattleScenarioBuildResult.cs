using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MechStorm.Battle;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 保存一次成功构建得到的对局、单位和单位技能映射。
    /// </summary>
    public sealed class TempBattleScenarioBuildResult
    {
        private static readonly IReadOnlyList<AbilityDefinition> EmptyAbilities =
            Array.Empty<AbilityDefinition>();

        private readonly ReadOnlyCollection<CombatUnit> _units;
        private readonly ReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> _abilitiesByUnitId;

        public BattleSession BattleSession { get; }
        public IReadOnlyList<CombatUnit> Units => _units;
        public IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> AbilitiesByUnitId =>
            _abilitiesByUnitId;

        internal TempBattleScenarioBuildResult(
            BattleSession battleSession,
            IEnumerable<CombatUnit> units,
            IDictionary<int, IReadOnlyList<AbilityDefinition>> abilitiesByUnitId)
        {
            BattleSession = battleSession ?? throw new ArgumentNullException(nameof(battleSession));
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (abilitiesByUnitId == null)
            {
                throw new ArgumentNullException(nameof(abilitiesByUnitId));
            }

            _units = units.ToList().AsReadOnly();
            _abilitiesByUnitId =
                new ReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>>(
                    new Dictionary<int, IReadOnlyList<AbilityDefinition>>(abilitiesByUnitId));
        }

        public IReadOnlyList<AbilityDefinition> GetAbilitiesForUnit(int unitId)
        {
            return _abilitiesByUnitId.TryGetValue(unitId, out var abilities)
                ? abilities
                : EmptyAbilities;
        }
    }
}
