using System;
using System.Collections;
using System.Reflection;
using LitMotion;
using TEngine.Extensions.Tween;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TEngine.Extensions.Editor.Tween
{
    internal sealed class TweenAnimationBinding
    {
        internal readonly Object Owner;
        internal readonly string Path;
        internal TweenAnimationBinding(Object owner, string path = "m_Animation") { Owner = owner; Path = path; }
        internal TweenAnimation Animation => Resolve(Owner, Path) as TweenAnimation;
        internal string ListPath => PropertyPath("m_Sequence");
        internal string PropertyPath(string name) => Path + "." + name;
        internal bool IsAvailable => Owner is MonoBehaviour host && host.isActiveAndEnabled && !EditorUtility.IsPersistent(host) && Animation != null;
        internal bool IsBackward => Animation != null && Animation.IsBackward;
        internal string Name => Owner == null ? "已销毁" : Owner.name + "." + Path;
        internal bool Same(TweenAnimationBinding other) => other != null && Owner == other.Owner && Path == other.Path;
        internal MotionHandle Play(bool reverse, bool backward) => reverse ? Animation.DORewind() : backward ? Animation.PlayBackward() : Animation.DOPlay();
        internal void Kill() => Animation?.DOKill();
        internal void SetDirection(bool backward)
        {
            if (backward) Animation.PlayBackward();
            else Animation.PlayForward();
        }

        internal static TweenAnimationBinding ForItem(SerializedProperty item)
        {
            var marker = item.propertyPath.LastIndexOf("m_Sequence.Array.data[", StringComparison.Ordinal);
            if (marker <= 0) return null;
            var path = item.propertyPath.Substring(0, marker - 1);
            var binding = new TweenAnimationBinding(item.serializedObject.targetObject, path);
            return binding.Animation == null ? null : binding;
        }

        private static object Resolve(object value, string path)
        {
            foreach (var part in path.Replace(".Array.data[", "[").Split('.'))
            {
                if (value == null) return null;
                var bracket = part.IndexOf('[');
                var name = bracket < 0 ? part : part.Substring(0, bracket);
                FieldInfo field = null;
                for (var type = value.GetType(); type != null && field == null; type = type.BaseType)
                    field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) return null;
                value = field.GetValue(value);
                if (bracket >= 0)
                {
                    var index = int.Parse(part.Substring(bracket + 1, part.Length - bracket - 2));
                    if (!(value is IList list) || index >= list.Count) return null;
                    value = list[index];
                }
            }
            return value;
        }
    }
}
