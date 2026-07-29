using System.Collections.Generic;
using MechStorm.Battle;
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
        [SerializeField]
        private Transform _plane;
        [SerializeField]
        private GameObject _rangeHighlightTemplate;
        [SerializeField]
        private TempBattleScenarioConfig _scenarioConfig;
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

        private readonly Dictionary<CombatUnit, CombatUnitVisual> _combatUnitVisuals = new();
        private readonly Dictionary<Collider, CombatUnit> _combatUnitColliders = new();
        private readonly Dictionary<CombatUnit, Transform> _healthBarAnchors = new();

        private int _boardWidth;
        private int _boardHeight;
        private GridCoordinateConverter _coordConverter;
        private BattleRangeHighlighter _rangeHighlighter;
        private BattleBoardInputter _boardInputter;
        private BattlePresentationController _presentationController;
        private BattleSession _battleSession;
        private TempBattleScenarioBuildResult _scenarioBuildResult;
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

            if (_scenarioConfig == null)
            {
                Log.Error("[MechStorm] TempGameEntry requires a TempBattleScenarioConfig.");
                return;
            }

            var scenarioBuilder = new TempBattleScenarioBuilder();
            if (!scenarioBuilder.TryBuild(
                    _scenarioConfig,
                    out _scenarioBuildResult,
                    out var validationResult))
            {
                Log.Error(
                    $"[MechStorm] TempBattleScenarioConfig is invalid:" +
                    $"{System.Environment.NewLine}{validationResult.ErrorMessage}");
                return;
            }

            _boardWidth = _scenarioConfig.BoardWidth;
            _boardHeight = _scenarioConfig.BoardHeight;
            _battleSession = _scenarioBuildResult.BattleSession;
            EnsureDebugPanel();
            _debugPanel.SetTheme(_hudTheme);
            CreateBoard();
            CreateRangeHighlighter();
            LogBoardValidation();
            CreateDebugMarkers();
            CreateCombatUnitVisuals();
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

        private void CreateCombatUnitVisuals()
        {
            foreach (var combatUnit in _scenarioBuildResult.Units)
            {
                var unitObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unitObject.name = $"BattleUnit_{combatUnit.UnitId}_{combatUnit.Pilot.Name}";
                unitObject.transform.localScale = new Vector3(
                    _cellSize * 0.6f,
                    _cellSize,
                    _cellSize * 0.6f);

                var anchorObject = new GameObject("HealthBarAnchor");
                anchorObject.transform.SetParent(unitObject.transform, false);
                anchorObject.transform.localPosition = Vector3.up * 0.8f;
                anchorObject.transform.localScale = Vector3.one;

                var visual = new CombatUnitVisual(
                    unitObject.transform,
                    _coordConverter,
                    unitObject.transform.localScale.y * 0.5f);
                visual.RefreshPosition(combatUnit.Position);
                _combatUnitVisuals.Add(combatUnit, visual);
                _healthBarAnchors.Add(combatUnit, anchorObject.transform);

                if (!unitObject.TryGetComponent<Collider>(out var unitCollider))
                {
                    Log.Error($"[MechStorm] Combat unit {combatUnit.UnitId} requires a collider.");
                    continue;
                }

                _combatUnitColliders.Add(unitCollider, combatUnit);
                LogUnitStatus(combatUnit, unitObject.transform.position);
            }
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

            if (_combatUnitColliders.Count != _scenarioBuildResult.Units.Count)
            {
                Log.Error("[MechStorm] Every combat unit requires an input collider.");
                return;
            }

            _boardInputter = new BattleBoardInputter(
                camera,
                boardCollider,
                _coordConverter,
                _combatUnitColliders,
                _battleInputLayerMask.value,
                () => _debugPanel != null && _debugPanel.IsPointerOverPanel());
        }

        private void CreatePresentationController()
        {
            if (_battleSession == null ||
                _combatUnitVisuals.Count != _scenarioBuildResult.Units.Count ||
                _boardInputter == null ||
                _rangeHighlighter == null)
            {
                Log.Error("[MechStorm] BattlePresentationController dependencies are not ready.");
                return;
            }

            _presentationController = new BattlePresentationController(
                _battleSession, _combatUnitVisuals, _boardInputter, _rangeHighlighter,
                _scenarioBuildResult.AbilitiesByUnitId);
        }

        private void CreateDebugTools()
        {
            var camera = _camera != null ? _camera : Camera.main;
            if (_presentationController == null || camera == null)
            {
                Log.Error("[MechStorm] Temporary battle debug tools require a presentation controller and camera.");
                return;
            }

            _unitPresenter = new TempBattleUnitPresenter(
                _healthBarAnchors,
                camera);

            var teamAUnits = _battleSession.GetFactionCombatUnits(CombatFaction.TeamA);
            _debugActions = new TempBattleDebugActions(
                _battleSession,
                _presentationController,
                teamAUnits[0],
                _scenarioBuildResult.AbilitiesByUnitId,
                _unitPresenter,
                _debugDamage);
            _debugPanel?.Bind(_debugActions);
            var currentUnit = _battleSession.CurrentCombatUnit;
            Log.Info(
                $"[MechStorm] Battle Started: " +
                $"Round={_battleSession.CurrentRoundNumber}, " +
                $"Faction={_battleSession.CurrentFaction}, " +
                $"Unit={currentUnit.Pilot.Name}/{currentUnit.Mech.Name}");
        }
    }
}
