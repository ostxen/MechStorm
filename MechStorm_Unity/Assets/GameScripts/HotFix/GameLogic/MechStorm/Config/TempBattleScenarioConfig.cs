using System;
using System.Collections.Generic;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using UnityEngine;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 保存临时战斗场景的初始配置，不持有任何运行中的战斗对象。
    /// </summary>
    [CreateAssetMenu(
        fileName = "TempBattleScenarioConfig",
        menuName = "MechStorm/Temporary/Battle Scenario")]
    public sealed class TempBattleScenarioConfig : ScriptableObject
    {
        [SerializeField] private int _boardWidth = 5;
        [SerializeField] private int _boardHeight = 5;
        [SerializeField] private List<TempCombatUnitConfig> _units = new();

        public int BoardWidth => _boardWidth;
        public int BoardHeight => _boardHeight;
        public IReadOnlyList<TempCombatUnitConfig> Units => _units;
    }

    /// <summary>
    /// 保存单个单位的阵营、初始位置以及静态战斗配置。
    /// </summary>
    [Serializable]
    public sealed class TempCombatUnitConfig
    {
        [SerializeField] private int _unitId;
        [SerializeField] private CombatFaction _faction;
        [SerializeField] private Vector2Int _initialPosition;
        [SerializeField] private TempPilotConfig _pilot = new();
        [SerializeField] private TempMechConfig _mech = new();
        [SerializeField] private List<TempAbilityConfig> _abilities = new();

        public int UnitId => _unitId;
        public CombatFaction Faction => _faction;
        public Vector2Int InitialPosition => _initialPosition;
        public TempPilotConfig Pilot => _pilot;
        public TempMechConfig Mech => _mech;
        public IReadOnlyList<TempAbilityConfig> Abilities => _abilities;
    }

    /// <summary>
    /// 保存创建 PilotData 所需的初始值。
    /// </summary>
    [Serializable]
    public sealed class TempPilotConfig
    {
        [SerializeField] private int _id;
        [SerializeField] private string _name = string.Empty;
        [SerializeField] private int _maxActionPoint;

        public int Id => _id;
        public string Name => _name;
        public int MaxActionPoint => _maxActionPoint;
    }

    /// <summary>
    /// 保存创建 MechData 所需的初始值。
    /// </summary>
    [Serializable]
    public sealed class TempMechConfig
    {
        [SerializeField] private int _id;
        [SerializeField] private string _name = string.Empty;
        [SerializeField] private int _maxDurability;
        [SerializeField] private int _moveRange;
        [SerializeField] private TempBasicAttackConfig _basicAttack = new();

        public int Id => _id;
        public string Name => _name;
        public int MaxDurability => _maxDurability;
        public int MoveRange => _moveRange;
        public TempBasicAttackConfig BasicAttack => _basicAttack;
    }

    /// <summary>
    /// 保存创建 BasicAttackData 所需的初始值。
    /// </summary>
    [Serializable]
    public sealed class TempBasicAttackConfig
    {
        [SerializeField] private int _actionPointCost;
        [SerializeField] private int _damage;
        [SerializeField] private int _minRange = 1;
        [SerializeField] private int _maxRange = 1;

        public int ActionPointCost => _actionPointCost;
        public int Damage => _damage;
        public int MinRange => _minRange;
        public int MaxRange => _maxRange;
    }

    /// <summary>
    /// 保存创建 AbilityDefinition 所需的初始值。
    /// </summary>
    [Serializable]
    public sealed class TempAbilityConfig
    {
        [SerializeField] private int _abilityId;
        [SerializeField] private string _name = string.Empty;
        [SerializeField] private int _actionPointCost;
        [SerializeField] private int _damage;
        [SerializeField] private int _minRange = 1;
        [SerializeField] private int _maxRange = 1;
        [SerializeField] private TargetRule _targetRule = TargetRule.EnemyUnit;
        [SerializeField] private bool _requiresBasicWeapon;

        public int AbilityId => _abilityId;
        public string Name => _name;
        public int ActionPointCost => _actionPointCost;
        public int Damage => _damage;
        public int MinRange => _minRange;
        public int MaxRange => _maxRange;
        public TargetRule TargetRule => _targetRule;
        public bool RequiresBasicWeapon => _requiresBasicWeapon;
    }
}