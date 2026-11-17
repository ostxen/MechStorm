using System.Text;

namespace TEngine.Editor.UI
{
    /// <summary>
    /// Shared source-generation operations used by UI binding adapters.
    /// This type knows nothing about a concrete binding component.
    /// </summary>
    public static class UIAutoGenerationCore
    {
        public static string GetVariableName(string objectName, UIFieldCodeStyle codeStyle)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return objectName;
            }

            var prefixes = new[]
            {
                UIScriptGeneratorSetting.GetPrefixNameByCodeStyle(UIFieldCodeStyle.UnderscorePrefix),
                UIScriptGeneratorSetting.GetPrefixNameByCodeStyle(UIFieldCodeStyle.MPrefix),
            };

            foreach (var prefix in prefixes)
            {
                if (objectName.StartsWith(prefix))
                {
                    return $"{UIScriptGeneratorSetting.GetPrefixNameByCodeStyle(codeStyle)}{objectName[prefix.Length..]}";
                }
            }

            return objectName;
        }

        public static string GetWidgetGameObjectName()
        {
            foreach (var rule in UIScriptGeneratorSetting.Instance.UIScriptGenerateRule)
            {
                if (rule.isUIWidget)
                {
                    return rule.uiElementRegex;
                }
            }

            return GetWidgetName();
        }

        public static string GetWidgetName()
        {
            return $"{UIScriptGeneratorSetting.GetPrefixNameByCodeStyle(UIScriptGeneratorSetting.Instance.CodeStyle)}" +
                   UIScriptGeneratorSetting.GetWidgetName();
        }

        public static void AppendEventCode(UIComponentName componentName, string variableName, bool isUniTask,
            StringBuilder onCreate, StringBuilder callbacks, bool implementation)
        {
            switch (componentName)
            {
                case UIComponentName.Button:
                {
                    var functionName = UIScriptGenerator.GetBtnFuncName(variableName);
                    onCreate?.AppendLine($"\t\t\t{variableName}.onClick.RemoveAllListeners();");
                    onCreate?.AppendLine(isUniTask
                        ? $"\t\t\t{variableName}.onClick.AddListener(UniTask.UnityAction({functionName}));"
                        : $"\t\t\t{variableName}.onClick.AddListener({functionName});");

                    if (implementation)
                    {
                        callbacks.AppendLine(isUniTask
                            ? $"\t\tprivate async partial UniTaskVoid {functionName}()"
                            : $"\t\tprivate partial void {functionName}()");
                        callbacks.AppendLine("\t\t{");
                        if (isUniTask)
                        {
                            callbacks.AppendLine("\t\t\tawait UniTask.Yield();");
                        }
                        callbacks.AppendLine("\t\t}");
                    }
                    else
                    {
                        callbacks.AppendLine(isUniTask
                            ? $"\t\tprivate partial UniTaskVoid {functionName}();"
                            : $"\t\tprivate partial void {functionName}();");
                    }

                    callbacks.AppendLine();
                    break;
                }
                case UIComponentName.Toggle:
                {
                    var functionName = UIScriptGenerator.GetToggleFuncName(variableName);
                    onCreate?.AppendLine($"\t\t\t{variableName}.onValueChanged.RemoveAllListeners();");
                    onCreate?.AppendLine($"\t\t\t{variableName}.onValueChanged.AddListener({functionName});");
                    callbacks.AppendLine(implementation
                        ? $"\t\tprivate partial void {functionName}(bool isOn)"
                        : $"\t\tprivate partial void {functionName}(bool isOn);");
                    if (implementation)
                    {
                        callbacks.AppendLine("\t\t{");
                        callbacks.AppendLine("\t\t}");
                    }
                    callbacks.AppendLine();
                    break;
                }
                case UIComponentName.TMP_Dropdown:
                {
                    var functionName = UIScriptGenerator.GetTMPDropdownFuncName(variableName);
                    onCreate?.AppendLine($"\t\t\t{variableName}.onValueChanged.RemoveAllListeners();");
                    onCreate?.AppendLine($"\t\t\t{variableName}.onValueChanged.AddListener({functionName});");
                    callbacks.AppendLine(implementation
                        ? $"\t\tprivate partial void {functionName}(int selectedIndex)"
                        : $"\t\tprivate partial void {functionName}(int selectedIndex);");
                    if (implementation)
                    {
                        callbacks.AppendLine("\t\t{");
                        callbacks.AppendLine("\t\t}");
                    }
                    callbacks.AppendLine();
                    break;
                }
                case UIComponentName.Slider:
                {
                    var functionName = UIScriptGenerator.GetSliderFuncName(variableName);
                    onCreate?.AppendLine($"\t\t\t{variableName}.onValueChanged.RemoveAllListeners();");
                    onCreate?.AppendLine($"\t\t\t{variableName}.onValueChanged.AddListener({functionName});");
                    callbacks.AppendLine(implementation
                        ? $"\t\tprivate partial void {functionName}(float value)"
                        : $"\t\tprivate partial void {functionName}(float value);");
                    if (implementation)
                    {
                        callbacks.AppendLine("\t\t{");
                        callbacks.AppendLine("\t\t}");
                    }
                    callbacks.AppendLine();
                    break;
                }
            }
        }
    }
}
