using System.Threading;
using Cysharp.Threading.Tasks;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>按目录条目异步取得 Clip，并通过 Lease 将资源释放责任交给调用方。</summary>
    public interface IAnimationClipProvider
    {
        UniTask<AnimationClipLease> LoadAsync(AnimationClipCatalogEntry entry, CancellationToken cancellationToken = default);
    }
}
