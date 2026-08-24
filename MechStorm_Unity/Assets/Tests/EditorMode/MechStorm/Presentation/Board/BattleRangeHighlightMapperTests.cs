using System;
using System.Collections.Generic;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using NUnit.Framework;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests.Board
{
    public class BattleRangeHighlightMapperTests
    {
        [Test]
        public void Build_CombinesRangesAndPrioritizesValidTargets()
        {
            var mapper = new BattleRangeHighlightMapper();
            var movePosition = new Vector2Int(0, 0);
            var overlappingPosition = new Vector2Int(1, 0);
            var target = CreateCombatUnit(1, new Vector2Int(2, 0));

            var highlights = mapper.Build(
                new[] { movePosition, overlappingPosition },
                new[] { overlappingPosition, target.Position },
                new[] { target });

            Assert.AreEqual(3, highlights.Count);
            Assert.AreEqual(BattleCellHighlightType.Move, highlights[movePosition]);
            Assert.AreEqual(BattleCellHighlightType.MoveAndAttack, highlights[overlappingPosition]);
            Assert.AreEqual(BattleCellHighlightType.ValidTarget, highlights[target.Position]);
        }

        [Test]
        public void Build_WithEmptyInputs_ReturnsEmptyResult()
        {
            var mapper = new BattleRangeHighlightMapper();

            var highlights = mapper.Build(
                Array.Empty<Vector2Int>(),
                Array.Empty<Vector2Int>(),
                Array.Empty<CombatUnit>());

            Assert.IsEmpty(highlights);
        }

        [Test]
        public void Build_WithDuplicateAttackPositions_KeepsAttackType()
        {
            var mapper = new BattleRangeHighlightMapper();
            var attackPosition = new Vector2Int(1, 0);

            var highlights = mapper.Build(
                Array.Empty<Vector2Int>(),
                new[] { attackPosition, attackPosition },
                Array.Empty<CombatUnit>());

            Assert.AreEqual(BattleCellHighlightType.Attack, highlights[attackPosition]);
        }

        [Test]
        public void Build_WithSelectedAttackTarget_PrioritizesSelectedTarget()
        {
            var mapper = new BattleRangeHighlightMapper();
            var target = CreateCombatUnit(1, new Vector2Int(2, 0));

            var highlights = mapper.Build(
                Array.Empty<Vector2Int>(),
                new[] { target.Position },
                new[] { target },
                target);

            Assert.AreEqual(
                BattleCellHighlightType.SelectedAttackTarget,
                highlights[target.Position]);
        }

        [Test]
        public void Build_DoesNotModifyInputCollections()
        {
            var mapper = new BattleRangeHighlightMapper();
            var movePositions = new List<Vector2Int> { new Vector2Int(0, 0) };
            var attackPositions = new List<Vector2Int> { new Vector2Int(1, 0) };
            var attackTargets = new List<CombatUnit> { CreateCombatUnit(1, new Vector2Int(2, 0)) };

            mapper.Build(movePositions, attackPositions, attackTargets);

            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 0) }, movePositions);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, attackPositions);
            CollectionAssert.AreEqual(new[] { attackTargets[0] }, attackTargets);
        }

        [Test]
        public void BuildAbility_PrioritizesValidAbilityTargets()
        {
            var mapper = new BattleRangeHighlightMapper();
            var abilityPosition = new Vector2Int(1, 0);
            var target = CreateCombatUnit(1, new Vector2Int(2, 0));

            var highlights = mapper.BuildAbility(
                new[] { abilityPosition, target.Position },
                new[] { target });

            Assert.AreEqual(BattleCellHighlightType.Ability, highlights[abilityPosition]);
            Assert.AreEqual(BattleCellHighlightType.ValidAbilityTarget, highlights[target.Position]);
        }

        [Test]
        public void BuildAbility_WithSelectedTarget_PrioritizesSelectedTarget()
        {
            var mapper = new BattleRangeHighlightMapper();
            var target = CreateCombatUnit(1, new Vector2Int(2, 0));

            var highlights = mapper.BuildAbility(
                new[] { target.Position },
                new[] { target },
                target);

            Assert.AreEqual(
                BattleCellHighlightType.SelectedAbilityTarget,
                highlights[target.Position]);
        }

        [Test]
        public void BuildSelectedActionTarget_UsesGenericSelectedTargetType()
        {
            var mapper = new BattleRangeHighlightMapper();
            var target = CreateCombatUnit(1, new Vector2Int(2, 0));

            var highlights = mapper.BuildSelectedActionTarget(target);

            Assert.AreEqual(1, highlights.Count);
            Assert.AreEqual(
                BattleCellHighlightType.SelectedActionTarget,
                highlights[target.Position]);
            Assert.Throws<ArgumentNullException>(() =>
                mapper.BuildSelectedActionTarget(null));
        }

        [Test]
        public void BuildAbility_WithInvalidInputs_Throws()
        {
            var mapper = new BattleRangeHighlightMapper();
            var positions = Array.Empty<Vector2Int>();
            var targets = Array.Empty<CombatUnit>();

            Assert.Throws<ArgumentNullException>(() => mapper.BuildAbility(null, targets));
            Assert.Throws<ArgumentNullException>(() => mapper.BuildAbility(positions, null));
            Assert.Throws<ArgumentException>(() =>
                mapper.BuildAbility(positions, new CombatUnit[] { null }));
            Assert.Throws<ArgumentException>(() =>
                mapper.BuildAbility(positions, targets, CreateCombatUnit(1, new Vector2Int(0, 0))));
        }

        [Test]
        public void Build_WithInvalidInputs_Throws()
        {
            var mapper = new BattleRangeHighlightMapper();
            var positions = Array.Empty<Vector2Int>();
            var targets = Array.Empty<CombatUnit>();

            Assert.Throws<ArgumentNullException>(() => mapper.Build(null, positions, targets));
            Assert.Throws<ArgumentNullException>(() => mapper.Build(positions, null, targets));
            Assert.Throws<ArgumentNullException>(() => mapper.Build(positions, positions, null));
            Assert.Throws<ArgumentException>(() =>
                mapper.Build(positions, positions, new CombatUnit[] { null }));
            Assert.Throws<ArgumentException>(() =>
                mapper.Build(positions, positions, targets, CreateCombatUnit(1, new Vector2Int(0, 0))));
        }

        private static CombatUnit CreateCombatUnit(int unitId, Vector2Int position)
        {
            var pilot = new PilotData(unitId, $"Pilot {unitId}", 3);
            var mech = new MechData(
                unitId, $"Mech {unitId}", new BasicAttackData(10, 1, 1), 100, 3);
            return new CombatUnitFactory().Create(unitId, pilot, mech, position);
        }
    }
}
