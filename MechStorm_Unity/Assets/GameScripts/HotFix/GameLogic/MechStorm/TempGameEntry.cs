using System;
using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using MechStorm.Presentation.Controllers;
using MechStorm.Presentation.Input;
using MechStorm.Presentation.Units;
using TEngine;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation
{
    public class TempGameEntry : MonoBehaviour
    {
        private const int PlayerAUnitId = 1;
        private const int EnemyAUnitId = 2;

        [SerializeField]
        private Transform _plane;
        [SerializeField]
        private GameObject _rangeHighlightTemplate;
        [SerializeField]
        private int _boardWidth = 5;
        [SerializeField]
        private int _boardHeight = 4;
        [SerializeField]
        private float _cellSize = 1f;
        [SerializeField]
        private Vector3 _boardOrigin = Vector3.zero;
        [SerializeField]
        private Color _boardColor = new Color(0.12f, 0.14f, 0.16f);
        [SerializeField]
        private Color _gridCellColor = new Color(0.2f, 0.23f, 0.26f);
        [SerializeField]
        private bool _showBoardDebugMarkers = true;

        [SerializeField]
        private Camera _camera;
        [SerializeField]
        private int _debugDamage = 10;
        [SerializeField]
        private LayerMask _battleInputLayerMask = Physics.DefaultRaycastLayers;
        [SerializeField]
        private TempBattleDebugPanel _debugPanel;
        [SerializeField]
        private TempBattleHudTheme _hudTheme;

        private Transform _playerA;
        private CombatUnitVisual _playerAVisual;
        private Transform _enemyA;
        private CombatUnitVisual _enemyAVisual;
        private GridCoordinateConverter _coordConverter;
        private BattleRangeHighlighter _rangeHighlighter;
        private BattleBoardInputter _boardInputter;
        private BattlePresentationController _presentationController;
        private BattleSession _battleSession;
        private CombatUnit _playerAUnit;
        private CombatUnit _enemyAUnit;
        private Transform _playerAHealthBarAnchor;
        private Transform _enemyAHealthBarAnchor;
        private TempBattleUnitPresenter _unitPresenter;
        private TempBattleDebugActions _debugActions;

        void Start()
        {
            if (_plane == null)
            {
                Log.Error("[MechStorm] TempGameEntry requires a Plane transform.");
                return;
            }

            if (_rangeHighlightTemplate == null)
            {
                Log.Error("[MechStorm] TempGameEntry requires a range highlight template.");
                return;
            }

            if (_boardWidth <= 0 || _boardHeight <= 0)
            {
                Log.Error("[MechStorm] Board width and height must be greater than zero.");
                return;
            }

            EnsureDebugPanel();
            _debugPanel.SetTheme(_hudTheme);
            CreateBoard();
            CreateRangeHighlighter();
            LogBoardValidation();
            CreateDebugMarkers();
            CreatePlayerA();
            CreateEnemyA();
            CreateBattleSession();
            CreateBoardInputter();
            CreatePresentationController();
            CreateDebugTools();
        }

        void Update()
        {
            _debugActions?.Tick();
            _unitPresenter?.RefreshFacing();
        }

        void OnDestroy()
        {
            _rangeHighlighter?.Dispose();
        }

        private void EnsureDebugPanel()
        {
            if (_debugPanel == null)
            {
                _debugPanel = gameObject.AddComponent<TempBattleDebugPanel>();
            }
        }

        private void CreateBoard()
        {
            _coordConverter = new GridCoordinateConverter(_cellSize, _boardOrigin);
            var boardRenderer = new BattleBoardRenderer();
            boardRenderer.Initialize(_boardWidth, _boardHeight, _cellSize, _boardOrigin, _plane, _boardColor);
        }

        private void CreateRangeHighlighter()
        {
            _rangeHighlighter = new BattleRangeHighlighter(_boardWidth, _boardHeight, _cellSize, _coordConverter,
                _rangeHighlightTemplate, _gridCellColor);
        }

        private void LogBoardValidation()
        {
            var worldWidth = _boardWidth * _cellSize;
            var worldHeight = _boardHeight * _cellSize;
            var expectedScale = new Vector3(worldWidth / 10f, 1f, worldHeight / 10f);
            var expectedPosition = new Vector3(_boardOrigin.x + worldWidth / 2f, _boardOrigin.y, _boardOrigin.z + worldHeight / 2f);
            var firstCellCenter = _coordConverter.GridToWorld(new Vector2Int(0, 0));
            var lastCellCenter = _coordConverter.GridToWorld(new Vector2Int(_boardWidth - 1, _boardHeight - 1));

            Log.Info($"[MechStorm] Board expected scale: {expectedScale}, actual: {_plane.localScale}");
            Log.Info($"[MechStorm] Board expected position: {expectedPosition}, actual: {_plane.position}");
            Log.Info($"[MechStorm] Grid(0,0) center: {firstCellCenter}, Grid({_boardWidth - 1},{_boardHeight - 1}) center: {lastCellCenter}");
        }

        private void CreateDebugMarkers()
        {
            if (!_showBoardDebugMarkers)
            {
                return;
            }

            CreateDebugMarker("Grid(0,0)", new Vector2Int(0, 0), Color.green);
            CreateDebugMarker("Grid(Max)", new Vector2Int(_boardWidth - 1, _boardHeight - 1), Color.red);
        }

        private void CreateDebugMarker(string markerName, Vector2Int gridPosition, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"BoardDebug_{markerName}";
            marker.transform.position = _coordConverter.GridToWorld(gridPosition) + Vector3.up * 0.05f;
            marker.transform.localScale = Vector3.one * Mathf.Min(_cellSize * 0.25f, 0.25f);

            if (marker.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.material.color = color;
            }

            if (marker.TryGetComponent<Collider>(out var markerCollider))
            {
                markerCollider.enabled = false;
            }
        }

        private void CreatePlayerA()
        {
            CreatePlayerAUnit();
            CreatePlayerAVisual();
        }

        private void CreatePlayerAUnit()
        {
            var factory = new CombatUnitFactory();
            var pilot = new PilotData(1, "我方机兵", 3);
            var mech = new MechData(1, "训练机", new BasicAttackData(10, 2, 5), 100, 4);
            _playerAUnit = factory.Create(PlayerAUnitId, pilot, mech, new Vector2Int(1, 1));
        }

        private void CreatePlayerAVisual()
        {
            var unitObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            unitObject.name = "PlayerA";
            unitObject.transform.localScale = new Vector3(_cellSize * 0.6f, _cellSize, _cellSize * 0.6f);

            var anchorObject = new GameObject("HealthBarAnchor");
            anchorObject.transform.SetParent(unitObject.transform, false);
            anchorObject.transform.localPosition = Vector3.up * 0.8f;
            anchorObject.transform.localScale = Vector3.one;
            _playerAHealthBarAnchor = anchorObject.transform;

            _playerA = unitObject.transform;
            _playerAVisual = new CombatUnitVisual(_playerA, _coordConverter, _playerA.localScale.y * 0.5f);
            _playerAVisual.RefreshPosition(_playerAUnit.Position);
            LogUnitStatus(_playerAUnit, _playerA.position);
        }

        private void CreateEnemyA()
        {
            CreateEnemyAUnit();
            CreateEnemyAVisual();
        }

        private void CreateEnemyAUnit()
        {
            var factory = new CombatUnitFactory();
            var pilot = new PilotData(2, "敌方机兵", 3);
            var mech = new MechData(2, "训练敌机", new BasicAttackData(8, 1, 3), 100, 3);

            _enemyAUnit = factory.Create(
                EnemyAUnitId, pilot, mech,
                new Vector2Int(_boardWidth - 1, _boardHeight - 1));
        }

        private void CreateEnemyAVisual()
        {
            var unitObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            unitObject.name = "EnemyA";
            unitObject.transform.localScale = new Vector3(
                _cellSize * 0.6f,
                _cellSize,
                _cellSize * 0.6f);

            var anchorObject = new GameObject("HealthBarAnchor");
            anchorObject.transform.SetParent(unitObject.transform, false);
            anchorObject.transform.localPosition = Vector3.up * 0.8f;
            anchorObject.transform.localScale = Vector3.one;
            _enemyAHealthBarAnchor = anchorObject.transform;

            _enemyA = unitObject.transform;
            _enemyAVisual = new CombatUnitVisual(
                _enemyA,
                _coordConverter,
                _enemyA.localScale.y * 0.5f);
            _enemyAVisual.RefreshPosition(_enemyAUnit.Position);
            LogUnitStatus(_enemyAUnit, _enemyA.position);
        }

        private void LogUnitStatus(CombatUnit combatUnit, Vector3 worldPosition)
        {
            Log.Info(
                $"[MechStorm] Unit={combatUnit.Pilot.Name}/{combatUnit.Mech.Name}, " +
                $"Position=({combatUnit.Position.X},{combatUnit.Position.Y}), " +
                $"World={worldPosition}, " +
                $"HP={combatUnit.MechRuntime.CurrentDurability}/{combatUnit.Mech.MaxDurability}, " +
                $"AP={combatUnit.PilotRuntime.CurrentActionPoint}/{combatUnit.Pilot.MaxActionPoint}");
        }

        private void CreateBoardInputter()
        {
            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null)
            {
                Log.Error("[MechStorm] BattleBoardInputter requires a camera.");
                return;
            }

            if (!_plane.TryGetComponent<Collider>(out var boardCollider))
            {
                Log.Error("[MechStorm] BattleBoardInputter requires a collider on the board plane.");
                return;
            }

            if (!_playerA.TryGetComponent<Collider>(out var playerACollider) ||
                !_enemyA.TryGetComponent<Collider>(out var enemyACollider))
            {
                Log.Error("[MechStorm] Combat unit input requires unit colliders.");
                return;
            }

            var combatUnitColliders =
                new Dictionary<Collider, CombatUnit>
                {
                    [playerACollider] = _playerAUnit,
                    [enemyACollider] = _enemyAUnit,
                };
            _boardInputter = new BattleBoardInputter(
                camera,
                boardCollider,
                _coordConverter,
                combatUnitColliders,
                _battleInputLayerMask.value,
                () => _debugPanel != null && _debugPanel.IsPointerOverPanel());
        }

        private void CreatePresentationController()
        {
            if (_battleSession == null ||
                _playerAVisual == null ||
                _enemyAVisual == null ||
                _boardInputter == null ||
                _rangeHighlighter == null)
            {
                Log.Error("[MechStorm] BattlePresentationController dependencies are not ready.");
                return;
            }

            var combatUnitVisuals =
                new Dictionary<CombatUnit, CombatUnitVisual>
                {
                    [_playerAUnit] = _playerAVisual,
                    [_enemyAUnit] = _enemyAVisual,
                };
            _presentationController = new BattlePresentationController(
                _battleSession, combatUnitVisuals, _boardInputter, _rangeHighlighter);
        }

        private void CreateDebugTools()
        {
            var camera = _camera != null ? _camera : Camera.main;
            _unitPresenter = new TempBattleUnitPresenter(
                _playerAUnit,
                _playerAHealthBarAnchor,
                _enemyAUnit,
                _enemyAHealthBarAnchor,
                camera);

            var debugAbility = new AbilityDefinition(
                101, "强袭炮击", 1, Math.Max(1, _boardWidth + _boardHeight - 2),
                20, 2, TargetRule.EnemyUnit);
            _debugActions = new TempBattleDebugActions(
                _battleSession,
                _presentationController,
                _playerAUnit,
                debugAbility,
                _unitPresenter,
                _debugDamage);
            _debugPanel?.Bind(_debugActions);
        }

        private void CreateBattleSession()
        {
            if (_playerAUnit == null || _enemyAUnit == null)
            {
                Log.Error("[MechStorm] BattleSession requires PlayerA and EnemyA.");
                return;
            }

            _battleSession = new BattleSession(
                _boardWidth,
                _boardHeight,
                new[] { _playerAUnit },
                new[] { _enemyAUnit });
            var currentUnit = _battleSession.CurrentCombatUnit;
            Log.Info(
                $"[MechStorm] Battle Started: " +
                $"Round={_battleSession.CurrentRoundNumber}, " +
                $"Faction={_battleSession.CurrentFaction}, " +
                $"Unit={currentUnit.Pilot.Name}/{currentUnit.Mech.Name}");
        }
    }
}
