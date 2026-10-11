using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using MechStorm.Presentation.Controllers;
using MechStorm.Presentation.Input;
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
        private Color _gridFocusColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField]
        private bool _showBoardDebugMarkers = true;
        [SerializeField]
        private bool _createPresentationTools = true;
        [SerializeField]
        private bool _createTemporaryDebugTools = true;

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

        private BattleRuntimeHost _runtimeHost;
        private Transform _combatUnitRoot => _runtimeHost?.UnitRoot;
        private int _boardWidth;
        private int _boardHeight;
        private GridCoordinateConverter _coordConverter;
        private BattleRangeHighlighter _rangeHighlighter;
        private BattleGridFocusIndicator _gridFocusIndicator;
        private BattleBoardInputter _boardInputter;
        private BattlePresentationController _presentationController;
        private BattleSession _battleSession => _runtimeHost?.Session;
        private TempBattleScenarioBuildResult _scenarioBuildResult => _runtimeHost?.Scenario;
        private BattleUnitPresenter _unitPresenter;
        private TempBattleDebugActions _debugActions;

        void Start()
        {
            if (_plane == null)
            {
                Log.Error("[MechStorm] TempGameEntry requires a Plane transform.");
                return;
            }

            if (_createPresentationTools)
            {
                EnsureRangeHighlightTemplate();
            }

            if (_scenarioConfig == null)
            {
                Log.Error("[MechStorm] TempGameEntry requires a TempBattleScenarioConfig.");
                return;
            }

            if (!TryBuildScenario())
            {
                return;
            }

            if (!_createPresentationTools)
            {
                return;
            }

            try
            {
                if (_createTemporaryDebugTools)
                {
                    EnsureDebugPanel();
                    _debugPanel.SetTheme(_hudTheme);
                }

                CreateRangeHighlighter();
                CreateGridFocusIndicator();
                LogBoardValidation();
                CreateDebugMarkers();
                CreateBattleRuntime();
            }
            catch (System.Exception exception)
            {
                ReleaseBattleRuntime();
                Log.Error($"[MechStorm] Temporary battle initialization failed: {exception}");
            }
        }

        private void CreateBattleRuntime()
        {
            _unitPresenter = _runtimeHost.UnitPresenter;
            CreateBoardInputter();
            CreatePresentationController();
            CreateDebugTools();
        }

        void Update()
        {
            if (_createTemporaryDebugTools)
            {
                _debugActions?.Tick();
            }
            else
            {
                _presentationController?.Tick(out _);
            }

            _unitPresenter?.RefreshFacing();
        }

        void OnDestroy()
        {
            ReleaseBattleRuntime();
        }

        private bool TryBuildScenario()
        {
            try
            {
                var camera = _camera != null ? _camera : Camera.main;
                _runtimeHost = new BattleRuntimeHost(_scenarioConfig, _plane, camera, transform,
                    _cellSize, _boardOrigin, _boardColor);
                _coordConverter = _runtimeHost.Coordinates;
                _boardWidth = _scenarioConfig.BoardWidth;
                _boardHeight = _scenarioConfig.BoardHeight;
                return true;
            }
            catch (System.Exception exception)
            {
                Log.Error($"[MechStorm] Battle Runtime Host initialization failed: {exception}");
                return false;
            }
        }

        private void EnsureDebugPanel()
        {
            if (_debugPanel == null)
            {
                _debugPanel = gameObject.AddComponent<TempBattleDebugPanel>();
            }
        }

        private void CreateRangeHighlighter()
        {
            _rangeHighlighter = new BattleRangeHighlighter(_boardWidth, _boardHeight, _cellSize, _coordConverter,
                _rangeHighlightTemplate, _gridCellColor);
        }

        private void CreateGridFocusIndicator()
        {
            var focusMaterial = _rangeHighlightTemplate.GetComponent<Renderer>().sharedMaterial;
            _gridFocusIndicator = new BattleGridFocusIndicator(
                _cellSize, _coordConverter, focusMaterial, _gridFocusColor);
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
            marker.transform.SetParent(_combatUnitRoot, false);
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

        private void CreateBoardInputter()
        {
            var camera = _runtimeHost.BattleCamera;
            if (camera == null)
            {
                throw new System.InvalidOperationException("BattleBoardInputter requires a camera.");
            }

            if (!_plane.TryGetComponent<Collider>(out var boardCollider))
            {
                throw new System.InvalidOperationException("BattleBoardInputter requires a collider on the board plane.");
            }

            if (_runtimeHost.CombatUnitColliders.Count != _scenarioBuildResult.Units.Count)
            {
                throw new System.InvalidOperationException("Every combat unit requires an input collider.");
            }

            _boardInputter = new BattleBoardInputter(
                camera,
                boardCollider,
                _coordConverter,
                _runtimeHost.CombatUnitColliders,
                _battleInputLayerMask.value,
                () => _debugPanel != null && _debugPanel.IsPointerOverPanel());
        }

        private void CreatePresentationController()
        {
            if (_battleSession == null ||
                _runtimeHost.CombatUnitVisuals.Count != _scenarioBuildResult.Units.Count ||
                _boardInputter == null ||
                _rangeHighlighter == null ||
                _gridFocusIndicator == null)
            {
                throw new System.InvalidOperationException("BattlePresentationController dependencies are not ready.");
            }

            _presentationController = new BattlePresentationController(
                _battleSession, _runtimeHost.CombatUnitVisuals, _boardInputter, _rangeHighlighter, _gridFocusIndicator,
                _scenarioBuildResult.AbilitiesByUnitId);
            _presentationController.SelectCurrentCombatUnit();
        }

        private void CreateDebugTools()
        {
            if (!_createTemporaryDebugTools)
            {
                return;
            }

            var camera = _runtimeHost.BattleCamera;
            if (_presentationController == null || camera == null)
            {
                throw new System.InvalidOperationException("Temporary battle debug tools require a presentation controller and camera.");
            }

            var teamAUnits = _battleSession.GetFactionCombatUnits(CombatFaction.TeamA);
            _debugActions = new TempBattleDebugActions(
                _battleSession,
                _presentationController,
                teamAUnits[0],
                _scenarioBuildResult.AbilitiesByUnitId,
                _unitPresenter,
                _debugDamage);
            _debugPanel?.Bind(_debugActions, RestartBattle);
            var currentUnit = _battleSession.CurrentCombatUnit;
            Log.Info(
                $"[MechStorm] Battle Started: " +
                $"Round={_battleSession.CurrentRoundNumber}, " +
                $"Faction={_battleSession.CurrentFaction}, " +
                $"Unit={currentUnit.Pilot.Name}/{currentUnit.Mech.Name}");
        }

        private void RestartBattle()
        {
            ReleaseBattleRuntime();
            if (!TryBuildScenario())
            {
                return;
            }

            try
            {
                CreateRangeHighlighter();
                CreateGridFocusIndicator();
                CreateDebugMarkers();
                CreateBattleRuntime();
                Log.Info("[MechStorm] Battle restarted from TempBattleScenarioConfig.");
            }
            catch (System.Exception exception)
            {
                ReleaseBattleRuntime();
                Log.Error($"[MechStorm] Temporary battle restart failed: {exception}");
            }
        }

        private void ReleaseBattleRuntime()
        {
            _debugPanel?.Bind(null);
            _presentationController?.ClearSelection();
            _gridFocusIndicator?.Dispose();
            _rangeHighlighter?.Dispose();
            _gridFocusIndicator = null;
            _rangeHighlighter = null;
            if (_ownsRangeHighlightTemplate && _rangeHighlightTemplate != null)
            {
                Destroy(_rangeHighlightTemplate);
                _rangeHighlightTemplate = null;
                _ownsRangeHighlightTemplate = false;
            }
            _runtimeHost?.Dispose();
            _runtimeHost = null;

            _debugActions = null;
            _unitPresenter = null;
            _presentationController = null;
            _boardInputter = null;
            _coordConverter = null;
        }

        private bool _ownsRangeHighlightTemplate;

        private void EnsureRangeHighlightTemplate()
        {
            if (_rangeHighlightTemplate != null)
            {
                return;
            }

            _rangeHighlightTemplate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _rangeHighlightTemplate.name = "RuntimeRangeHighlightTemplate";
            _rangeHighlightTemplate.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _rangeHighlightTemplate.transform.localScale = Vector3.one * 0.9f;
            _rangeHighlightTemplate.layer = Physics.IgnoreRaycastLayer;
            _ownsRangeHighlightTemplate = true;
        }
    }
}
