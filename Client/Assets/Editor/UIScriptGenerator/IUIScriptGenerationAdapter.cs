using System;

namespace TEngine.Editor.UI
{
    public interface IUIScriptGenerationAdapter
    {
        string GetBaseType(bool isWidget);
        string GetClassAttribute(string className, string baseType);
        string GetWidgetExpression(string componentName, string gameObjectExpression);
        string GetBindingMethodDeclaration();
        string GetHierarchyExpression(string componentName, string path, bool isWidget);
        Type ResolveComponentType(string componentName);
    }

    /// <summary>项目 UI 编辑器工具在域加载时注册生成约定。</summary>
    public static class UIScriptGeneration
    {
        public static IUIScriptGenerationAdapter Adapter { get; set; }

        public static bool IsAvailable => Adapter != null && UIScriptGeneratorSetting.Instance != null;
    }
}
