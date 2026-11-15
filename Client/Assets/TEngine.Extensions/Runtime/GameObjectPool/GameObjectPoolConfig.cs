using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool
{
    public enum GameObjectPoolKeyMatchMode
    {
        Exact,
        Prefix
    }

    public enum GameObjectPoolOverflowMode
    {
        Reject,
        Temporary
    }

    /// <summary>
    /// 一个对象池配置组。Prefix 模式只匹配完整 key 或 prefix/ 子路径。
    /// </summary>
    [Serializable]
    public sealed class GameObjectPoolConfig
    {
        public const string DefaultName = "__default";

        [SerializeField] private string _name = "Pool";
        [SerializeField] private string _key = "";
        [SerializeField] private GameObjectPoolKeyMatchMode _matchMode =
            GameObjectPoolKeyMatchMode.Prefix;
        [SerializeField] private int _capacity = 32;
        [SerializeField] private int _preloadCount;
        [SerializeField] private float _expireTime;
        [SerializeField] private float _autoDestroyTime = -1f;
        [SerializeField] private bool _dontDestroy;
        [SerializeField] private GameObjectPoolOverflowMode _overflowMode =
            GameObjectPoolOverflowMode.Reject;
        [SerializeField] private bool _resetTransform = true;

        public GameObjectPoolConfig()
        {
        }

        public GameObjectPoolConfig(
            string name,
            string key,
            int capacity = 32,
            int preloadCount = 0,
            float expireTime = 0f,
            float autoDestroyTime = -1f,
            bool dontDestroy = false,
            GameObjectPoolKeyMatchMode matchMode = GameObjectPoolKeyMatchMode.Prefix,
            GameObjectPoolOverflowMode overflowMode = GameObjectPoolOverflowMode.Reject,
            bool resetTransform = true)
        {
            _name = string.IsNullOrWhiteSpace(name) ? "Pool" : name;
            _key = key ?? "";
            _capacity = capacity;
            _preloadCount = preloadCount;
            _expireTime = expireTime;
            _autoDestroyTime = autoDestroyTime;
            _dontDestroy = dontDestroy;
            _matchMode = matchMode;
            _overflowMode = overflowMode;
            _resetTransform = resetTransform;
            Validate();
        }

        public string Name => string.IsNullOrWhiteSpace(_name) ? "Pool" : _name;
        public string Key => _key ?? "";
        public GameObjectPoolKeyMatchMode MatchMode => _matchMode;
        public int Capacity => _capacity;
        public int PreloadCount => _preloadCount;
        public float ExpireTime => _expireTime;
        public float AutoDestroyTime => _autoDestroyTime;
        public bool DontDestroy => _dontDestroy;
        public GameObjectPoolOverflowMode OverflowMode => _overflowMode;
        public bool ResetTransform => _resetTransform;

        public int Specificity
        {
            get
            {
                return MatchMode == GameObjectPoolKeyMatchMode.Exact
                    ? Key.Length * 2 + 1
                    : Key.Length;
            }
        }

        internal static GameObjectPoolConfig Default
        {
            get
            {
                return new GameObjectPoolConfig(
                    DefaultName,
                    "",
                    int.MaxValue,
                    0,
                    0f,
                    -1f,
                    false,
                    GameObjectPoolKeyMatchMode.Prefix,
                    GameObjectPoolOverflowMode.Reject);
            }
        }

        public bool Matches(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                return false;
            }

            var key = Key.TrimEnd('/');
            if (MatchMode == GameObjectPoolKeyMatchMode.Exact)
            {
                return string.Equals(assetKey, key, StringComparison.Ordinal);
            }

            return key.Length == 0 ||
                   string.Equals(assetKey, key, StringComparison.Ordinal) ||
                   assetKey.StartsWith(key + "/", StringComparison.Ordinal);
        }

        internal void Validate()
        {
            if (_capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Capacity));
            }

            if (_preloadCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(PreloadCount));
            }

            if (_expireTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ExpireTime));
            }

            if (_autoDestroyTime < -1f)
            {
                throw new ArgumentOutOfRangeException(nameof(AutoDestroyTime));
            }
        }

        internal static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Pool key cannot be empty.", nameof(key));
            }
        }
    }

    [CreateAssetMenu(
        fileName = "GameObjectPoolConfig",
        menuName = "TEngine Extensions/Object Pool Config",
        order = 10)]
    public sealed class GameObjectPoolConfigAsset : ScriptableObject
    {
        [SerializeField] private List<GameObjectPoolConfig> _configs =
            new List<GameObjectPoolConfig>();

        public IReadOnlyList<GameObjectPoolConfig> Configs => _configs;
    }
}
