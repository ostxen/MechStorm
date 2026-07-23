using System;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Rules;
using MechStorm.Battle.Snapshots;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Turns;
using MechStorm.Battle.Units;

namespace MechStorm.Battle
{
    public sealed class BattleSession
    {
        private readonly SquareGrid _squareGrid;
        private readonly MovementResolver _movementResolver;
        private readonly AttackResolver _attackResolver;
        private readonly AbilityResolver _abilityResolver;
        private readonly CombatUnitRegistry _unitRegistry;
        private readonly TurnCoordinator _turnCoordinator;
        private readonly List<BattleActionLog> _actionLogs;
        private readonly IReadOnlyList<BattleActionLog> _readOnlyActionLogs;

        private int _actionSequence;

        public BattleSession(int boardWidth, int boardHeight, IEnumerable<CombatUnit> teamAUnits, IEnumerable<CombatUnit> teamBUnits, IEnumerable<CombatUnit> neutralUnits = null)
        {
            _actionLogs = new List<BattleActionLog>();
            _readOnlyActionLogs = _actionLogs.AsReadOnly();
            _squareGrid = new SquareGrid(boardWidth, boardHeight);
            _unitRegistry = new CombatUnitRegistry(teamAUnits, teamBUnits, neutralUnits);
            _movementResolver = new MovementResolver(_squareGrid, IsPositionOccupied);
            _attackResolver = new AttackResolver(_squareGrid);
            _abilityResolver = new AbilityResolver(_squareGrid, _unitRegistry);
            ValidateUnitPositions(_unitRegistry.GetFactionUnits(CombatFaction.TeamA), nameof(teamAUnits));
            ValidateUnitPositions(_unitRegistry.GetFactionUnits(CombatFaction.TeamB), nameof(teamBUnits));
            ValidateUnitPositions(_unitRegistry.GetFactionUnits(CombatFaction.Neutral), nameof(neutralUnits));
            ValidateAliveUnitPositionsDoNotOverlap();
            _turnCoordinator = new TurnCoordinator(_unitRegistry);
        }

        public SquareGrid Grid => _squareGrid;

        public IReadOnlyList<CombatUnit> CombatUnits => _unitRegistry.CombatUnits;

        public int CurrentRoundNumber => _turnCoordinator.CurrentRoundNumber;

        public CombatFaction CurrentFaction => _turnCoordinator.CurrentFaction;

        public CombatUnit CurrentCombatUnit => _turnCoordinator.CurrentCombatUnit;

        public IReadOnlyList<BattleActionLog> ActionLogs => _readOnlyActionLogs;

        public BattleSnapshot CreateSnapshot()
        {
            var unitSnapshots = new List<BattleUnitSnapshot>(CombatUnits.Count);
            foreach (var unit in CombatUnits)
            {
                unitSnapshots.Add(new BattleUnitSnapshot(unit.UnitId, _unitRegistry.GetFaction(unit), unit.Position,
                    unit.MechRuntime.CurrentDurability, unit.Mech.MaxDurability,
                    unit.PilotRuntime.CurrentActionPoint, unit.Pilot.MaxActionPoint));
            }

            return new BattleSnapshot(Grid.Width, Grid.Height, CurrentRoundNumber, CurrentFaction,
                CurrentCombatUnit.UnitId, unitSnapshots);
        }

        public BattleActionResult TryMoveCurrentCombatUnit(Vector2Int target)
        {
            var actorUnit = CurrentCombatUnit;
            var positionBefore = actorUnit.Position;
            var sequence = ++_actionSequence;

            var result = !_movementResolver.TryMoveTo(actorUnit, target)
                ? BattleActionResult.Failed(sequence, BattleActionType.Move, actorUnit, BattleActionFailureReason.InvalidMoveTarget)
                : BattleActionResult.MoveSucceeded(sequence, actorUnit, positionBefore, actorUnit.Position);
            return RecordAction(result);
        }

        public BattleActionResult AttackTargetCombatUnit(CombatUnit targetUnit)
        {
            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (!_unitRegistry.Contains(targetUnit))
            {
                throw new ArgumentException("Target unit does not belong to this battle session.", nameof(targetUnit));
            }

            var attackerUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            var failureReason = GetBasicAttackFailureReason(attackerUnit, targetUnit);
            if (failureReason != BattleActionFailureReason.None)
            {
                var result = BattleActionResult.Failed(sequence, BattleActionType.Attack, attackerUnit,
                    failureReason);
                return RecordAction(result, targetUnitId: targetUnit.UnitId);
            }

            var durabilityBefore = targetUnit.MechRuntime.CurrentDurability;
            if (!_attackResolver.TryAttack(attackerUnit, targetUnit))
            {
                throw new InvalidOperationException("Validated basic attack could not be executed.");
            }

            var successResult = BattleActionResult.AttackSucceeded(sequence, attackerUnit, targetUnit,
                durabilityBefore, targetUnit.MechRuntime.CurrentDurability);
            return RecordAction(successResult, targetUnitId: targetUnit.UnitId);
        }

        public BattleActionResult TryUseAbility(AbilityDefinition ability, CombatUnit targetUnit)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (!_unitRegistry.Contains(targetUnit))
            {
                throw new ArgumentException("Target unit does not belong to this battle session.",
                    nameof(targetUnit));
            }

            var actorUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            var failureReason = GetAbilityFailureReason(ability, actorUnit, targetUnit);
            if (failureReason != BattleActionFailureReason.None)
            {
                var failedResult = BattleActionResult.AbilityFailed(sequence, actorUnit, targetUnit, ability.AbilityId, failureReason);
                return RecordAction(failedResult, targetUnitId: targetUnit.UnitId);
            }

            var actionPointBefore = actorUnit.PilotRuntime.CurrentActionPoint;
            var durabilityBefore = targetUnit.MechRuntime.CurrentDurability;
            if (!_abilityResolver.TryResolveAbility(ability, actorUnit, targetUnit))
            {
                throw new InvalidOperationException("Validated ability could not be executed.");
            }

            var successResult = BattleActionResult.AbilitySucceeded(sequence, actorUnit, targetUnit,
                ability.AbilityId, actionPointBefore, actorUnit.PilotRuntime.CurrentActionPoint,
                durabilityBefore, targetUnit.MechRuntime.CurrentDurability);
            return RecordAction(successResult, targetUnitId: targetUnit.UnitId);
        }

        public BattleActionResult EndCurrentUnitAction()
        {
            var actorUnit = CurrentCombatUnit;
            var roundNumberBefore = CurrentRoundNumber;
            var factionBefore = CurrentFaction;
            var sequence = ++_actionSequence;
            var actionPointsBefore = new Dictionary<int, int>(CombatUnits.Count);
            foreach (var combatUnit in CombatUnits)
            {
                actionPointsBefore.Add(combatUnit.UnitId,
                    combatUnit.PilotRuntime.CurrentActionPoint);
            }

            _turnCoordinator.EndCurrentUnitAction();

            var nextCombatUnit = CurrentCombatUnit;
            var result = BattleActionResult.ActionEnded(sequence, actorUnit, nextCombatUnit, roundNumberBefore,
                CurrentRoundNumber, factionBefore, CurrentFaction, actionPointsBefore[nextCombatUnit.UnitId],
                nextCombatUnit.PilotRuntime.CurrentActionPoint);
            return RecordAction(result, nextUnitId: nextCombatUnit.UnitId);
        }

        public IReadOnlyList<CombatUnit> GetCurrentFactionCombatUnits()
        {
            return GetFactionCombatUnits(CurrentFaction);
        }

        public IReadOnlyList<CombatUnit> GetFactionCombatUnits(CombatFaction faction)
        {
            return _unitRegistry.GetFactionUnits(faction);
        }

        public CombatFaction GetCombatUnitFaction(CombatUnit combatUnit)
        {
            return _unitRegistry.GetFaction(combatUnit);
        }

        public IReadOnlyList<CombatUnit> GetAllAliveCombatUnits()
        {
            return _unitRegistry.GetAliveUnits();
        }

        public IReadOnlyList<CombatUnit> GetAllDeadCombatUnits()
        {
            return _unitRegistry.GetDeadUnits();
        }

        public IReadOnlyList<Vector2Int> GetCurrentCombatUnitReachablePositions()
        {
            return _movementResolver.GetReachablePositions(CurrentCombatUnit);
        }

        public IReadOnlyList<CombatUnit> GetCurrentCombatUnitBasicAttackTargets()
        {
            var attackTargets = new List<CombatUnit>();
            var attackerUnit = CurrentCombatUnit;
            foreach (var targetUnit in CombatUnits)
            {
                if (GetBasicAttackFailureReason(attackerUnit, targetUnit) == BattleActionFailureReason.None)
                {
                    attackTargets.Add(targetUnit);
                }
            }

            return attackTargets;
        }

        public IReadOnlyList<Vector2Int> GetCurrentCombatUnitBasicAttackPositions()
        {
            return _attackResolver.GetPositionsInRange(CurrentCombatUnit);
        }

        public bool TryGetAliveCombatUnitAt(Vector2Int position, out CombatUnit combatUnit)
        {
            ValidateGridPosition(position, nameof(position));

            foreach (var unit in CombatUnits)
            {
                if (unit.IsAlive() && unit.Position == position)
                {
                    combatUnit = unit;
                    return true;
                }
            }

            combatUnit = null;
            return false;
        }

        public bool IsPositionOccupied(Vector2Int position)
        {
            return TryGetAliveCombatUnitAt(position, out _);
        }

        public bool AreFactionUnitsDead(CombatFaction faction)
        {
            return _unitRegistry.AreFactionUnitsDead(faction);
        }

        private BattleActionResult RecordAction(BattleActionResult result, int? targetUnitId = null, int? nextUnitId = null)
        {
            _actionLogs.Add(new BattleActionLog(result, result.ActorUnit.UnitId, targetUnitId, nextUnitId));
            return result;
        }

        private BattleActionFailureReason GetBasicAttackFailureReason(CombatUnit attackerUnit, CombatUnit targetUnit)
        {
            if (_unitRegistry.GetFaction(attackerUnit) == _unitRegistry.GetFaction(targetUnit))
            {
                return BattleActionFailureReason.SameFactionTarget;
            }

            if (targetUnit.IsDead())
            {
                return BattleActionFailureReason.TargetAlreadyDead;
            }

            return _attackResolver.IsPositionInRange(attackerUnit, targetUnit.Position)
                ? BattleActionFailureReason.None
                : BattleActionFailureReason.TargetOutOfRange;
        }

        private BattleActionFailureReason GetAbilityFailureReason(AbilityDefinition ability,
            CombatUnit sourceUnit, CombatUnit targetUnit)
        {
            if (targetUnit.IsDead())
            {
                return BattleActionFailureReason.TargetAlreadyDead;
            }

            if (_unitRegistry.GetFaction(sourceUnit) == _unitRegistry.GetFaction(targetUnit))
            {
                return BattleActionFailureReason.SameFactionTarget;
            }

            var distance = _squareGrid.GetManhattanDistance(sourceUnit.Position, targetUnit.Position);
            if (distance < ability.MinRange || distance > ability.MaxRange)
            {
                return BattleActionFailureReason.TargetOutOfRange;
            }

            return sourceUnit.PilotRuntime.HasEnoughActionPoint(ability.ActionPointCost)
                ? BattleActionFailureReason.None
                : BattleActionFailureReason.InsufficientActionPoint;
        }

        private void ValidateUnitPositions(IReadOnlyList<CombatUnit> units, string parameterName)
        {
            foreach (var unit in units)
            {
                ValidateGridPosition(unit.Position, parameterName);
            }
        }

        private void ValidateAliveUnitPositionsDoNotOverlap()
        {
            var occupiedPositions = new HashSet<Vector2Int>();
            foreach (var unit in CombatUnits)
            {
                if (unit.IsAlive() && !occupiedPositions.Add(unit.Position))
                {
                    throw new ArgumentException("Alive combat units cannot share the same grid position.");
                }
            }
        }

        private void ValidateGridPosition(Vector2Int position, string parameterName)
        {
            if (!_squareGrid.IsInside(position))
            {
                throw new ArgumentOutOfRangeException(parameterName, position,
                    "Grid position must be inside the battle grid.");
            }
        }
    }
}
