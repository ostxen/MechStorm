using System;
using System.IO;
using GameConfig;
using Luban;
using UnityEngine;

namespace GameLogic
{
    /// <summary>热更域配置。首次访问单表时同步加载，允许阻塞主线程。</summary>
    public sealed class ConfigSystem : Singleton<ConfigSystem>
    {
        private Tables _tables;

        public Tables Tables => _tables ??= CreateTables(ReadBytes);

        public static Tables CreateTables(Func<string, byte[]> readBytes)
        {
            if (readBytes == null) throw new ArgumentNullException(nameof(readBytes));
            return new Tables(name =>
            {
                var bytes = readBytes(name);
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidDataException($"配置二进制缺失或为空：{name}。");
                return new ByteBuf(bytes);
            });
        }

        private byte[] ReadBytes(string location)
        {
            var asset = GameModule.Resource.LoadAsset<TextAsset>(location);
            if (asset == null) throw new FileNotFoundException($"配置资源加载失败：{location}。", location);
            try
            {
                return asset.bytes;
            }
            finally
            {
                GameModule.Resource.UnloadAsset(asset);
            }
        }

        protected override void OnRelease()
        {
            _tables = null;
        }
    }
}
