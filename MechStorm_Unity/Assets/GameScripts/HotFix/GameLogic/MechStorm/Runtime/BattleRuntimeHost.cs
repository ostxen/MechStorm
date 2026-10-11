using System;
using System.Collections.Generic;
using MechStorm.Battle.Units;
using MechStorm.Battle;
using MechStorm.Presentation.Board;
using MechStorm.Presentation.Units;
using UnityEngine;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 拥有一场战斗的逻辑实例、坐标转换与单位对象根节点，不负责 UI 或输入规则。
    /// </summary>
    public sealed class BattleRuntimeHost : IDisposable
    {
        private readonly Transform _plane;
        private readonly Renderer _planeRenderer;
        private readonly Vector3 _originalPlanePosition;
        private readonly Vector3 _originalPlaneScale;
        private readonly MaterialPropertyBlock _originalPlaneProperties;
        private readonly Dictionary<CombatUnit, CombatUnitVisual> _combatUnitVisuals = new();
        private readonly Dictionary<Collider, CombatUnit> _combatUnitColliders = new();
        private readonly Dictionary<CombatUnit, Transform> _healthBarAnchors = new();
        private bool _boardChanged;
        private bool _disposed;

        public TempBattleScenarioBuildResult Scenario { get; private set; }
        public BattleSession Session => Scenario?.BattleSession;
        public GridCoordinateConverter Coordinates { get; private set; }
        public Camera BattleCamera { get; private set; }
        public Transform UnitRoot { get; private set; }
        public IReadOnlyDictionary<CombatUnit, CombatUnitVisual> CombatUnitVisuals => _combatUnitVisuals;
        public IReadOnlyDictionary<Collider, CombatUnit> CombatUnitColliders => _combatUnitColliders;
        public IReadOnlyDictionary<CombatUnit, Transform> HealthBarAnchors => _healthBarAnchors;
        public BattleUnitPresenter UnitPresenter { get; private set; }

        public BattleRuntimeHost(TempBattleScenarioConfig config, Transform plane, Camera battleCamera,
            Transform unitParent, float cellSize, Vector3 boardOrigin, Color boardColor)
        {
            var renderer = ValidateSceneBindings(plane, battleCamera, unitParent, cellSize, boardOrigin);
            var builder = new TempBattleScenarioBuilder();
            if (!builder.TryBuild(config, out var scenario, out var validation))
            {
                throw new ArgumentException($"Battle scenario is invalid:{Environment.NewLine}{validation.ErrorMessage}", nameof(config));
            }

            _plane = plane;
            _planeRenderer = renderer;
            _originalPlanePosition = plane.position;
            _originalPlaneScale = plane.localScale;
            _originalPlaneProperties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_originalPlaneProperties);
            Scenario = scenario;
            Coordinates = new GridCoordinateConverter(cellSize, boardOrigin);
            BattleCamera = battleCamera;
            try
            {
                CreateSceneObjects(unitParent, config.BoardWidth, config.BoardHeight, cellSize, boardOrigin, boardColor);
                CreateUnitViews(scenario.Units, cellSize);
                UnitPresenter = new BattleUnitPresenter(_healthBarAnchors, BattleCamera);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static Renderer ValidateSceneBindings(Transform plane, Camera battleCamera, Transform unitParent,
            float cellSize, Vector3 boardOrigin)
        {
            if (plane == null)
            {
                throw new ArgumentNullException(nameof(plane));
            }

            if (battleCamera == null)
            {
                throw new ArgumentNullException(nameof(battleCamera));
            }

            if (unitParent == null)
            {
                throw new ArgumentNullException(nameof(unitParent));
            }

            if (!plane.TryGetComponent<Renderer>(out var renderer) || !plane.TryGetComponent<Collider>(out _))
            {
                throw new ArgumentException("Battle plane requires a Renderer and Collider.", nameof(plane));
            }

            if (!IsFinite(cellSize) || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be finite and greater than zero.");
            }

            if (!IsFinite(boardOrigin.x) || !IsFinite(boardOrigin.y) || !IsFinite(boardOrigin.z))
            {
                throw new ArgumentException("Board origin must be finite.", nameof(boardOrigin));
            }

            return renderer;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void CreateSceneObjects(Transform unitParent, int width, int height, float cellSize, Vector3 origin, Color color)
        {
            UnitRoot = new GameObject("BattleCombatUnits").transform;
            UnitRoot.SetParent(unitParent, true);
            _boardChanged = true;
            new BattleBoardRenderer().Initialize(width, height, cellSize, origin, _plane, color);
        }

        private void CreateUnitViews(IReadOnlyList<CombatUnit> combatUnits, float cellSize)
        {
            foreach (var combatUnit in combatUnits)
            {
                var unitObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unitObject.name = $"BattleUnit_{combatUnit.UnitId}_{combatUnit.Pilot.Name}";
                unitObject.transform.SetParent(UnitRoot, false);
                unitObject.transform.localScale = new Vector3(cellSize * 0.6f, cellSize, cellSize * 0.6f);

                var unitVisual = new CombatUnitVisual(unitObject.transform, Coordinates, unitObject.transform.localScale.y * 0.5f);
                unitVisual.RefreshPosition(combatUnit.Position);
                _combatUnitVisuals.Add(combatUnit, unitVisual);

                if (!unitObject.TryGetComponent<Collider>(out var unitCollider))
                {
                    throw new InvalidOperationException($"Combat unit {combatUnit.UnitId} requires a collider.");
                }

                _combatUnitColliders.Add(unitCollider, combatUnit);
                _healthBarAnchors.Add(combatUnit, CreateHealthBarAnchor(unitObject.transform));
            }
        }

        private static Transform CreateHealthBarAnchor(Transform unitTransform)
        {
            var anchorObject = new GameObject("HealthBarAnchor");
            anchorObject.transform.SetParent(unitTransform, false);
            anchorObject.transform.localPosition = Vector3.up * 0.8f;
            anchorObject.transform.localScale = Vector3.one;
            return anchorObject.transform;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (UnitRoot != null)
            {
                // Destroy 延迟到帧末，先禁用整棵对象树，避免重建当帧残留 Collider 响应。
                UnitRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(UnitRoot.gameObject);
            }

            if (_boardChanged && _plane != null)
            {
                _plane.position = _originalPlanePosition;
                _plane.localScale = _originalPlaneScale;
                if (_planeRenderer != null)
                {
                    _planeRenderer.SetPropertyBlock(_originalPlaneProperties);
                }
            }

            UnitRoot = null;
            BattleCamera = null;
            Coordinates = null;
            Scenario = null;
            UnitPresenter = null;
            _combatUnitVisuals.Clear();
            _combatUnitColliders.Clear();
            _healthBarAnchors.Clear();
        }
    }
}
