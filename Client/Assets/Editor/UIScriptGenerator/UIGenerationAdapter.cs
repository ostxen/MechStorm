using System;
using UnityEditor;
using GameLogic;

namespace TEngine.Editor.UI
{
    [InitializeOnLoad]
    public sealed class UIGenerationAdapter : IUIScriptGenerationAdapter
    {
        static UIGenerationAdapter()
        {
            UIScriptGeneration.Adapter = new UIGenerationAdapter();
        }

        public string GetBaseType(bool isWidget) => isWidget ? "UIWidget" : "UIWindow";

        public string GetClassAttribute(string className, string baseType)
        {
            return baseType == "UIWindow"
                ? $"[Window(UILayer.UI, location : \"{className}\")]"
                : null;
        }

        public string GetWidgetExpression(string componentName, string gameObjectExpression)
        {
            return $"CreateWidget<{componentName}>({gameObjectExpression})";
        }

        public string GetBindingMethodDeclaration() => "protected override void ScriptGenerator()";

        public string GetHierarchyExpression(string componentName, string path, bool isWidget)
        {
            if (isWidget)
                return $"CreateWidget<{componentName}>(\"{path}\")";
            switch (componentName)
            {
                case "Transform": return $"FindChild(\"{path}\")";
                case "GameObject": return $"FindChild(\"{path}\").gameObject";
                case "AnimationCurve": return $"FindChildComponent<AnimCurveObject>(\"{path}\").m_animCurve";
                default: return $"FindChildComponent<{componentName}>(\"{path}\")";
            }
        }

        public Type ResolveComponentType(string componentName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType($"GameLogic.{componentName}");
                if (type != null) return type;
            }
            return null;
        }
    }

    internal static class UIBindingNaming
    {
        public static string GetClassName(UIBindComponent component, bool isWidget)
        {
            var name = component.name;
            if (!isWidget) return name;

            foreach (var rule in UIScriptGeneratorSetting.Instance.UIScriptGenerateRule)
            {
                if (!rule.isUIWidget || !name.StartsWith(rule.uiElementRegex)) continue;
                for (var parent = component.transform.parent; parent != null; parent = parent.parent)
                {
                    var binding = parent.GetComponent<UIBindComponent>();
                    if (binding == null) continue;
                    var types = UIScriptGeneratorSetting.Instance.UIGenTypes;
                    var parentName = types.Count > 0 && binding.uiType == types[0].uiTypeName
                        ? parent.name.Replace("UI", string.Empty)
                        : (binding.className ?? parent.name).Replace("Widget", string.Empty);
                    return $"{parentName}{name.Replace(rule.uiElementRegex, string.Empty)}Widget";
                }
            }
            return name;
        }
    }
}
