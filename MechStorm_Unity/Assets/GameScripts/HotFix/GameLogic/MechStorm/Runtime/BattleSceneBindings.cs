using System;
using MechStorm.Config;
using UnityEngine;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 保存战斗场景的依赖，不自行创建或更新战斗。启动与释放由 ProcedureBattle 控制。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSceneBindings : MonoBehaviour
    {
        [SerializeField] private BattleScenarioConfig _scenarioConfig;
        [SerializeField] private Transform _plane;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _unitParent;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector3 _boardOrigin = new Vector3(-5f, 0f, -5f);
        [SerializeField] private Color _boardColor = new Color(0.12f, 0.14f, 0.16f);
        [SerializeField] private GameObject _rangeHighlightTemplate;
        [SerializeField] private Color _gridCellColor = new Color(0.2f, 0.23f, 0.26f);
        [SerializeField] private Color _gridFocusColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private LayerMask _battleInputLayerMask = Physics.DefaultRaycastLayers;

        public BattleScenarioConfig ScenarioConfig => _scenarioConfig;
        public Transform Plane => _plane;
        public Camera BattleCamera => _camera;
        public Transform UnitParent => _unitParent;
        public float CellSize => _cellSize;
        public Vector3 BoardOrigin => _boardOrigin;
        public Color BoardColor => _boardColor;
        public GameObject RangeHighlightTemplate => _rangeHighlightTemplate;
        public Color GridCellColor => _gridCellColor;
        public Color GridFocusColor => _gridFocusColor;
        public int BattleInputLayerMask => _battleInputLayerMask.value;

        public void Validate()
        {
            if (_scenarioConfig == null || _plane == null || _camera == null || _unitParent == null)
            {
                throw new InvalidOperationException(
                    "BattleSceneBindings 缺少战斗配置、地面、相机或单位父节点，请先完成场景绑定。");
            }
        }
    }
}
