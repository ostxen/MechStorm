using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 在 HUD 就绪前异步准备可复用条目，单位切换只更新显示，不重新加载资源。
    /// </summary>
    internal sealed class BattleHUDItemList<T> : IDisposable where T : UIWidget, new()
    {
        private readonly UIWidget _owner;
        private readonly List<T> _items = new();
        private bool _disposed;
        private const float ItemSpacing = 8f;

        public BattleHUDItemList(UIWidget owner)
        {
            _owner = owner;
        }

        public async UniTask PrepareAsync(int count, string location, Action<T, int> initialize,
            CancellationToken cancellationToken)
        {
            if (!GameModule.Resource.CheckLocationValid(location))
            {
                throw new GameFrameworkException($"HUD 条目资源地址无效：{location}");
            }

            for (int i = _items.Count; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 先检查生命周期再创建 Widget，避免加载结束后向已销毁父对象注册子 Widget。
                var go = await UIModule.Resource.LoadGameObjectAsync(location, _owner.transform, cancellationToken);
                if (_disposed || cancellationToken.IsCancellationRequested || _owner.gameObject == null)
                {
                    if (go != null)
                    {
                        UnityEngine.Object.Destroy(go);
                    }
                    throw new OperationCanceledException(cancellationToken);
                }
                if (go == null)
                {
                    throw new GameFrameworkException($"HUD 条目加载失败：{location}");
                }

                var item = _owner.CreateWidget<T>(go, false);
                if (item == null)
                {
                    UnityEngine.Object.Destroy(go);
                    throw new GameFrameworkException($"HUD 条目创建失败：{location}");
                }
                var rect = item.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(0f, -GetContentHeight(i) - (i > 0 ? ItemSpacing : 0f));
                _items.Add(item);
                initialize(item, i);
            }
        }

        public T Get(int index)
        {
            return _items[index];
        }

        public float GetContentHeight(int count)
        {
            float height = 0f;
            for (int i = 0; i < count; i++)
            {
                height += _items[i].rectTransform.rect.height;
            }
            return height + Math.Max(0, count - 1) * ItemSpacing;
        }

        public void SetCount(int count)
        {
            if (count < 0 || count > _items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Visible = i < count;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            // 实例、Widget 和资源引用由 TE 父子生命周期统一回收。
            _items.Clear();
        }
    }
}
