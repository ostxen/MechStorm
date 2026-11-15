using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 基于 LitMotion 的 Tween 辅助器实现。
    /// </summary>
    public sealed partial class LitMotionHelper : Utility.Tween.ITweenHelper
    {
        /// <summary>
        /// 记录单个 Tween 的运行信息。
        /// </summary>
        private sealed class TweenEntry
        {
            /// <summary>LitMotion 动画句柄。</summary>
            public MotionHandle Handle;
            /// <summary>Tween 目标对象。</summary>
            public object Target;
            /// <summary>Tween 完成回调。</summary>
            public Action Callback;
            /// <summary>目标销毁时是否输出警告。</summary>
            public bool WarnIfTargetDestroyed;
            /// <summary>是否已取消。</summary>
            public bool Canceled;
        }
        private readonly Dictionary<long, TweenEntry> m_Entries = new Dictionary<long, TweenEntry>();
        private long m_NextId;

        private long Add<T>(object target, T start, T end, float duration, Ease ease, int cycles, CycleMode mode, float delay, float endDelay, bool unscaled, Action<T> setter) where T : unmanaged
        {
            if (typeof(T) == typeof(float)) return AddFloat(target, (float)(object)start, (float)(object)end, duration, ease, cycles, mode, delay, endDelay, unscaled, v => setter((T)(object)v));
            if (typeof(T) == typeof(long)) return AddLong(target, (long)(object)start, (long)(object)end, duration, ease, cycles, mode, delay, endDelay, unscaled, v => setter((T)(object)v));
            if (typeof(T) == typeof(Vector2)) return AddVector2(target, (Vector2)(object)start, (Vector2)(object)end, duration, ease, cycles, mode, delay, endDelay, unscaled, v => setter((T)(object)v));
            if (typeof(T) == typeof(Vector3)) return AddVector3(target, (Vector3)(object)start, (Vector3)(object)end, duration, ease, cycles, mode, delay, endDelay, unscaled, v => setter((T)(object)v));
            if (typeof(T) == typeof(Quaternion)) return AddQuaternion(target, (Quaternion)(object)start, (Quaternion)(object)end, duration, ease, cycles, mode, delay, endDelay, unscaled, v => setter((T)(object)v));
            if (typeof(T) == typeof(Color)) return AddColor(target, (Color)(object)start, (Color)(object)end, duration, ease, cycles, mode, delay, endDelay, unscaled, v => setter((T)(object)v));
            throw new NotSupportedException($"LitMotion value type {typeof(T)} is not supported.");
        }
        private long AddFloat(object t,float s,float e,float d,Ease x,int c,CycleMode m,float q,float z,bool u,Action<float> a)=>AddMotion(t,LMotion.Create(s,e,Mathf.Max(0,d)),x,c,m,q,z,u,a);
        private long AddLong(object t,long s,long e,float d,Ease x,int c,CycleMode m,float q,float z,bool u,Action<long> a)=>AddMotion(t,LMotion.Create(s,e,Mathf.Max(0,d)),x,c,m,q,z,u,a);
        private long AddVector2(object t,Vector2 s,Vector2 e,float d,Ease x,int c,CycleMode m,float q,float z,bool u,Action<Vector2> a)=>AddMotion(t,LMotion.Create(s,e,Mathf.Max(0,d)),x,c,m,q,z,u,a);
        private long AddVector3(object t,Vector3 s,Vector3 e,float d,Ease x,int c,CycleMode m,float q,float z,bool u,Action<Vector3> a)=>AddMotion(t,LMotion.Create(s,e,Mathf.Max(0,d)),x,c,m,q,z,u,a);
        private long AddQuaternion(object t,Quaternion s,Quaternion e,float d,Ease x,int c,CycleMode m,float q,float z,bool u,Action<Quaternion> a)=>AddMotion(t,LMotion.Create(s,e,Mathf.Max(0,d)),x,c,m,q,z,u,a);
        private long AddColor(object t,Color s,Color e,float d,Ease x,int c,CycleMode m,float q,float z,bool u,Action<Color> a)=>AddMotion(t,LMotion.Create(s,e,Mathf.Max(0,d)),x,c,m,q,z,u,a);
        private long AddMotion<TValue,TOptions,TAdapter>(object target, MotionBuilder<TValue,TOptions,TAdapter> builder, Ease ease,int cycles,CycleMode mode,float delay,float endDelay,bool unscaled,Action<TValue> setter, bool warnIfTargetDestroyed = false)
            where TValue:unmanaged where TOptions:unmanaged,IMotionOptions where TAdapter:unmanaged,IMotionAdapter<TValue,TOptions>
        {
            var id = ++m_NextId;
            var entry = new TweenEntry { Target = target, WarnIfTargetDestroyed = warnIfTargetDestroyed };
            m_Entries.Add(id, entry);
            try
            {
                builder = builder.WithEase(ToEase(ease))
                    .WithLoops(Mathf.Max(1, cycles), ToLoop(mode))
                    .WithDelay(Mathf.Max(0, delay))
                    .WithCancelOnError(true);
                if (unscaled) builder = builder.WithScheduler(MotionScheduler.UpdateIgnoreTimeScale);
                Action<TValue> update = value =>
                {
                    if (IsDestroyed(target))
                    {
                        Cancel(id);
                        return;
                    }

                    setter(value);
                };

                if (endDelay > 0)
                {
                    var motion = builder.Bind(update);
                    try
                    {
                        var sequence = LSequence.Create().Append(motion).AppendInterval(endDelay);
                        entry.Handle = sequence.Run(configuration =>
                        {
                            if (unscaled) configuration = configuration.WithScheduler(MotionScheduler.UpdateIgnoreTimeScale);
                            configuration.WithOnComplete(() => Finish(id)).WithOnCancel(() => Cancel(id));
                        });
                    }
                    catch
                    {
                        motion.TryCancel();
                        throw;
                    }
                }
                else
                {
                    entry.Handle = builder.WithOnComplete(() => Finish(id))
                        .WithOnCancel(() => Cancel(id)).Bind(update);
                }

                if (entry.Canceled)
                {
                    entry.Handle.TryCancel();
                }
                else if (target is Component component)
                {
                    entry.Handle.AddTo(component);
                }
                else if (target is GameObject gameObject)
                {
                    entry.Handle.AddTo(gameObject);
                }

                return id;
            }
            catch
            {
                m_Entries.Remove(id);
                entry.Handle.TryCancel();
                throw;
            }
        }
        private long Add<T>(object target, Func<T> read, Action<T> write, T end, float duration, Ease ease, int cycles, CycleMode mode, float delay, float endDelay, bool unscaled) where T : unmanaged => Add(target, read(), end, duration, ease, cycles, mode, delay, endDelay, unscaled, write);
        private static LitMotion.Ease ToEase(TEngine.Ease e) => e == TEngine.Ease.Default || e == TEngine.Ease.Custom ? LitMotion.Ease.Linear : (LitMotion.Ease)(int)e - 1;
        private static LitMotion.LoopType ToLoop(CycleMode m) => m == CycleMode.Yoyo ? LitMotion.LoopType.Flip : m == CycleMode.Rewind ? LitMotion.LoopType.Yoyo : m == CycleMode.Incremental ? LitMotion.LoopType.Incremental : LitMotion.LoopType.Restart;
        private void Finish(long id)
        {
            if (!m_Entries.TryGetValue(id, out var e)) return;
            m_Entries.Remove(id);
            if (IsDestroyed(e.Target)) WarnDestroyed(e);
            else e.Callback?.Invoke();
        }
        private static bool IsDestroyed(object target) => target is UnityEngine.Object unityObject && unityObject == null;

        private static void WarnDestroyed(TweenEntry tweenEntry)
        {
            if (tweenEntry.WarnIfTargetDestroyed) Log.Warning("Tween canceled because its target was destroyed.");
        }

        private void Cancel(long id)
        {
            if (!m_Entries.TryGetValue(id, out var entry)) return;
            m_Entries.Remove(id);
            entry.Canceled = true;
            if (IsDestroyed(entry.Target)) WarnDestroyed(entry);
            entry.Handle.TryCancel();
        }

        private static bool Same(object a, object b) => ReferenceEquals(a, b) || (a is UnityEngine.Object ua && b is UnityEngine.Object ub && ua == ub);
        public bool IsTweening(object target) => GetTweenCount(target) > 0;
        public int GetTweenCount(object target) { Purge(); var n=0; foreach(var e in m_Entries.Values) if(target==null || Same(e.Target,target)) n++; return n; }
        public bool IsAlive(long id) { Purge(); return m_Entries.TryGetValue(id,out var e) && e.Handle.IsValid(); }
        public void Stop(long id) => Cancel(id);
        public void Complete(long id)
        {
            Purge();
            if (!m_Entries.TryGetValue(id, out var entry)) return;
            if (!entry.Handle.TryComplete()) m_Entries.Remove(id);
        }
        public int StopAll(object target=null) { Purge(); var ids=new List<long>(); foreach(var p in m_Entries) if(target==null||Same(p.Value.Target,target)) ids.Add(p.Key); foreach(var id in ids) Stop(id); return ids.Count; }
        public int CompleteAll(object target=null) { Purge(); var ids=new List<long>(); foreach(var p in m_Entries) if(target==null||Same(p.Value.Target,target)) ids.Add(p.Key); foreach(var id in ids) Complete(id); return ids.Count; }
        public void OnComplete(long id, Action callback)
        {
            Purge();
            if (m_Entries.TryGetValue(id, out var entry))
            {
                entry.Callback += callback;
                return;
            }

            callback?.Invoke();
        }

        private void Purge()
        {
            List<long> dead = null;
            foreach (var pair in m_Entries)
            {
                if (!IsDestroyed(pair.Value.Target) && pair.Value.Handle.IsActive()) continue;
                if (dead == null) dead = new List<long>();
                dead.Add(pair.Key);
            }

            if (dead == null) return;
            foreach (var id in dead) Cancel(id);
        }

        public long Delay(float duration, Action callback=null, bool unscaled=false, bool warnIfTargetDestroyed=true)
            => Delay(null, duration, callback, unscaled, warnIfTargetDestroyed);

        public long Delay(object target,float duration,Action callback=null,bool unscaled=false,bool warnIfTargetDestroyed=true)
        {
            var id = AddMotion(target, LMotion.Create(0f, 0f, Mathf.Max(0, duration)), Ease.Linear,
                1, CycleMode.Restart, 0, 0, unscaled, _ => { }, warnIfTargetDestroyed);
            OnComplete(id, callback);
            return id;
        }

        public long LocalRotation(UnityEngine.Transform target, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localEulerAngles, v=>target.localEulerAngles=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long LocalRotation(UnityEngine.Transform target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.localEulerAngles=v); }
        public long Scale(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localScale.x, v=>target.localScale=new Vector3(v,v,v), endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Scale(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.localScale=new Vector3(v,v,v)); }
        public long Rotation(UnityEngine.Transform target, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.eulerAngles, v=>target.eulerAngles=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Rotation(UnityEngine.Transform target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.eulerAngles=v); }
        public long Position(UnityEngine.Transform target, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.position, v=>target.position=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Position(UnityEngine.Transform target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.position=v); }
        public long PositionX(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.position.x, v=>{var x=target.position;x.x=v;target.position=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long PositionX(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.position;x.x=v;target.position=x;}); }
        public long PositionY(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.position.y, v=>{var x=target.position;x.y=v;target.position=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long PositionY(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.position;x.y=v;target.position=x;}); }
        public long PositionZ(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.position.z, v=>{var x=target.position;x.z=v;target.position=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long PositionZ(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.position;x.z=v;target.position=x;}); }
        public long LocalPosition(UnityEngine.Transform target, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localPosition, v=>target.localPosition=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long LocalPosition(UnityEngine.Transform target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.localPosition=v); }
        public long LocalPositionX(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localPosition.x, v=>{var x=target.localPosition;x.x=v;target.localPosition=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long LocalPositionX(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.localPosition;x.x=v;target.localPosition=x;}); }
        public long LocalPositionY(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localPosition.y, v=>{var x=target.localPosition;x.y=v;target.localPosition=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long LocalPositionY(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.localPosition;x.y=v;target.localPosition=x;}); }
        public long LocalPositionZ(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localPosition.z, v=>{var x=target.localPosition;x.z=v;target.localPosition=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long LocalPositionZ(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.localPosition;x.z=v;target.localPosition=x;}); }
        public long Rotation(UnityEngine.Transform target, UnityEngine.Quaternion endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.rotation, v=>target.rotation=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Rotation(UnityEngine.Transform target, UnityEngine.Quaternion startValue, UnityEngine.Quaternion endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.rotation=v); }
        public long LocalRotation(UnityEngine.Transform target, UnityEngine.Quaternion endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localRotation, v=>target.localRotation=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long LocalRotation(UnityEngine.Transform target, UnityEngine.Quaternion startValue, UnityEngine.Quaternion endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.localRotation=v); }
        public long Scale(UnityEngine.Transform target, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localScale, v=>target.localScale=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Scale(UnityEngine.Transform target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.localScale=v); }
        public long ScaleX(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localScale.x, v=>{var x=target.localScale;x.x=v;target.localScale=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long ScaleX(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.localScale;x.x=v;target.localScale=x;}); }
        public long ScaleY(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localScale.y, v=>{var x=target.localScale;x.y=v;target.localScale=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long ScaleY(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.localScale;x.y=v;target.localScale=x;}); }
        public long ScaleZ(UnityEngine.Transform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.localScale.z, v=>{var x=target.localScale;x.z=v;target.localScale=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long ScaleZ(UnityEngine.Transform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.localScale;x.z=v;target.localScale=x;}); }
        public long Color(UnityEngine.SpriteRenderer target, UnityEngine.Color endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.color, v=>target.color=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Color(UnityEngine.SpriteRenderer target, UnityEngine.Color startValue, UnityEngine.Color endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.color=v); }
        public long MaterialColor(UnityEngine.Material target, UnityEngine.Color startValue, UnityEngine.Color endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.SetColor("_Color", v)); }
        public long Alpha(UnityEngine.SpriteRenderer target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.color.a, v=>{var x=target.color;x.a=v;target.color=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Alpha(UnityEngine.SpriteRenderer target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.color;x.a=v;target.color=x;}); }
        public long UISliderValue(UnityEngine.UI.Slider target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.value, v=>target.value=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UISliderValue(UnityEngine.UI.Slider target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.value=v); }
        public long UINormalizedPosition(UnityEngine.UI.ScrollRect target, UnityEngine.Vector2 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.normalizedPosition, v=>target.normalizedPosition=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UINormalizedPosition(UnityEngine.UI.ScrollRect target, UnityEngine.Vector2 startValue, UnityEngine.Vector2 endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.normalizedPosition=v); }
        public long UIHorizontalNormalizedPosition(UnityEngine.UI.ScrollRect target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.horizontalNormalizedPosition, v=>target.horizontalNormalizedPosition=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIHorizontalNormalizedPosition(UnityEngine.UI.ScrollRect target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.horizontalNormalizedPosition=v); }
        public long UIAnchoredPosition(UnityEngine.RectTransform target, UnityEngine.Vector2 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.anchoredPosition, v=>target.anchoredPosition=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIAnchoredPosition(UnityEngine.RectTransform target, UnityEngine.Vector2 startValue, UnityEngine.Vector2 endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.anchoredPosition=v); }
        public long UIAnchoredPositionX(UnityEngine.RectTransform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.anchoredPosition.x, v=>{var x=target.anchoredPosition;x.x=v;target.anchoredPosition=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIAnchoredPositionX(UnityEngine.RectTransform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.anchoredPosition;x.x=v;target.anchoredPosition=x;}); }
        public long UIAnchoredPositionY(UnityEngine.RectTransform target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.anchoredPosition.y, v=>{var x=target.anchoredPosition;x.y=v;target.anchoredPosition=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIAnchoredPositionY(UnityEngine.RectTransform target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.anchoredPosition;x.y=v;target.anchoredPosition=x;}); }
        public long UIVerticalNormalizedPosition(UnityEngine.UI.ScrollRect target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.verticalNormalizedPosition, v=>target.verticalNormalizedPosition=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIVerticalNormalizedPosition(UnityEngine.UI.ScrollRect target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.verticalNormalizedPosition=v); }
        public long UIAnchoredPosition3D(UnityEngine.RectTransform target, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.anchoredPosition3D, v=>target.anchoredPosition3D=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIAnchoredPosition3D(UnityEngine.RectTransform target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.anchoredPosition3D=v); }
        public long UISizeDelta(UnityEngine.RectTransform target, UnityEngine.Vector2 endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.sizeDelta, v=>target.sizeDelta=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UISizeDelta(UnityEngine.RectTransform target, UnityEngine.Vector2 startValue, UnityEngine.Vector2 endValue, float duration, Ease ease = Ease.Default,
                    int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.sizeDelta=v); }
        public long Color(UnityEngine.UI.Graphic target, UnityEngine.Color endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.color, v=>target.color=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Color(UnityEngine.UI.Graphic target, UnityEngine.Color startValue, UnityEngine.Color endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.color=v); }
        public long Alpha(UnityEngine.CanvasGroup target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.alpha, v=>target.alpha=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Alpha(UnityEngine.CanvasGroup target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.alpha=v); }
        public long Alpha(UnityEngine.UI.Graphic target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.color.a, v=>{var x=target.color;x.a=v;target.color=x;}, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long Alpha(UnityEngine.UI.Graphic target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>{var x=target.color;x.a=v;target.color=x;}); }
        public long UIFillAmount(UnityEngine.UI.Image target, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart,
                    float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, ()=>target.fillAmount, v=>target.fillAmount=v, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime); }
        public long UIFillAmount(UnityEngine.UI.Image target, Single startValue, Single endValue, float duration, Ease ease = Ease.Default, int cycles = 1,
                    CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false) { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>target.fillAmount=v); }
        public long Custom<T>(T target, UnityEngine.Vector3 startValue, UnityEngine.Vector3 endValue, float duration, Action<T, UnityEngine.Vector3> onValueChange,
                    Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
                    where T : class { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>onValueChange(target, v)); }
        public long Custom<T>(T target, long startValue, long endValue, float duration, Action<T, long> onValueChange,
                    Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
                    where T : class { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>onValueChange(target, v)); }
        public long Custom<T>(T target, float startValue, float endValue, float duration, Action<T, float> onValueChange,
                    Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
                    where T : class { return Add(target, startValue, endValue, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, v=>onValueChange(target, v)); }

    }
}
