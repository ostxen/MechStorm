using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按目录地址加载 YooAsset 动画资源，将加载句柄的释放操作封装到返回的 Lease。
    /// </summary>
    public sealed class YooAssetAnimationClipProvider : IAnimationClipProvider
    {
        private readonly IResourceModule _resourceModule;

        public YooAssetAnimationClipProvider(IResourceModule resourceModule)
        {
            _resourceModule = resourceModule != null
                ? resourceModule
                : throw new ArgumentNullException(nameof(resourceModule));
        }

        public async UniTask<AnimationClipLease> LoadAsync(
            AnimationClipCatalogEntry entry,
            CancellationToken cancellationToken = default)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrEmpty(entry.Location))
            {
                throw new ArgumentException(
                    "动画目录项缺少 Location。",
                    nameof(entry));
            }

            AssetHandle handle = null;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                handle = _resourceModule.LoadAssetAsyncHandle<AnimationClip>(
                    entry.Location,
                    entry.PackageName);
                // 本项目的 YooAsset ToUniTask 重载不接受取消令牌。
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (handle.Status != EOperationStatus.Succeed)
                {
                    throw new InvalidOperationException(
                        $"动画资源加载失败：{entry.AnimationKey}，{handle.LastError}");
                }

                var clip = handle.GetAssetObject<AnimationClip>();
                if (clip == null)
                {
                    throw new InvalidOperationException(
                        $"动画资源类型错误：{entry.AnimationKey}");
                }

                var lease = new AnimationClipLease(
                    entry.AnimationKey,
                    clip,
                    handle.Release);
                handle = null;
                return lease;
            }
            finally
            {
                handle?.Release();
            }
        }
    }
}
