using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GameLogic;

namespace TEngine.Editor.UI
{
    public static class UIBindComponentGenerator
    {
        private static TextEditor m_textEditor = new TextEditor();
        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent", priority = 84)]
        public static void UIPropertyBindComponent()
        {
            GenerateCSharpScript(false);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent", true)]
        public static bool ValidateUIPropertyBindComponent()
        {
            return UIScriptGeneration.IsAvailable && UIScriptGeneratorSetting.Instance.UseBindComponent;
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent - UniTask", priority = 85)]
        public static void UIPropertyBindComponentUniTask()
        {
            GenerateCSharpScript(false, true);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent - UniTask", true)]
        public static bool ValidateUIPropertyBindComponentUniTask()
        {
            return UIScriptGeneration.IsAvailable && UIScriptGeneratorSetting.Instance.UseBindComponent;
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponent", priority = 86)]
        public static void UIPropertyAndListenerBindComponent()
        {
            GenerateCSharpScript(true);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponent", true)]
        public static bool ValidateUIPropertyAndListenerBindComponent()
        {
            return UIScriptGeneration.IsAvailable && UIScriptGeneratorSetting.Instance.UseBindComponent;
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponentUniTask - UniTask", priority = 87)]
        public static void UIPropertyAndListenerBindComponentUniTask()
        {
            GenerateCSharpScript(true, true);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponentUniTask - UniTask", true)]
        public static bool ValidateUIPropertyAndListenerBindComponentUniTask()
        {
            return UIScriptGeneration.IsAvailable && UIScriptGeneratorSetting.Instance.UseBindComponent;
        }

        private static string GetUITypeName(string uiGenTypeName, string fileName)
        {
            var uiGenType = UIScriptGeneratorSetting.GetUIGenType(uiGenTypeName);

            if (uiGenType == null)
            {
                return UIScriptGeneration.Adapter.GetBaseType(false);
            }
            return !uiGenType.isGeneric ? uiGenType.uiTypeName : $"{uiGenType.uiTypeName}<{fileName}>";
        }

        public static bool GenerateCSharpScript(bool includeListener, bool isUniTask = false,
            bool isAutoGenerate = false, string savePath = null, string className = null,
            string uiGenTypeName = null, bool isGenImp = false,
            string impSavePath = null, Transform targetRoot = null)
        {
            var root = targetRoot != null ? targetRoot : Selection.activeTransform;
            if (root == null || !UIScriptGeneration.IsAvailable)
            {
                return false;
            }
            StringBuilder strVar = new StringBuilder();
            StringBuilder strBind = new StringBuilder();
            StringBuilder strOnCreate = new StringBuilder();
            StringBuilder strCallback = new StringBuilder();

            var widgetPrefix = GetUIWidgetName();
            string fileName = $"{root.name}.cs";
            if (!string.IsNullOrEmpty(className))
            {
                fileName = $"{className}.cs";
            }
            string uiTypeName = GetUITypeName(uiGenTypeName, className);
            if (!isAutoGenerate)
            {
                uiTypeName = UIScriptGeneration.Adapter.GetBaseType(false);
                if (root.name.StartsWith(widgetPrefix))
                {
                    uiTypeName = UIScriptGeneration.Adapter.GetBaseType(true);
                    fileName = $"{root.name.Replace(GetUIWidgetName(), string.Empty)}.cs";
                }
            }

            strVar.AppendLine($"\t\tprivate global::GameLogic.UIBindComponent m_bindComponent;");

            strBind.AppendLine($"\t\t\tm_bindComponent = gameObject.GetComponent<global::GameLogic.UIBindComponent>();");
            strBind.AppendLine($"\t\t\tif(m_bindComponent == null)");
            strBind.AppendLine($"\t\t\t{{");
            strBind.AppendLine($"\t\t\t\tLog.Error($\"根物体: {{gameObject.name}} 缺少组件 UIBindComponent, 请检查！！！\");");
            strBind.AppendLine($"\t\t\t\treturn;");
            strBind.AppendLine($"\t\t\t}}");
            m_bindIndex = 0;
            AutoErgodic(root, root, ref strVar, ref strBind, ref strOnCreate, ref strCallback, isUniTask);
            StringBuilder strFile = new StringBuilder();

            if (includeListener)
            {
                strFile.AppendLine("//----------------------------------------------------------");
                strFile.AppendLine("// <auto-generated>");
                strFile.AppendLine("// -This code was generated.");
                strFile.AppendLine("// -Changes to this file may cause incorrect behavior.");
                strFile.AppendLine("// -will be lost if the code is regenerated.");
                strFile.AppendLine("// <auto-generated/>");
                strFile.AppendLine("//----------------------------------------------------------");
#if TextMeshPro
                strFile.AppendLine("using TMPro;");
#endif
                if (isUniTask)
                {
                    strFile.AppendLine("using Cysharp.Threading.Tasks;");
                }

                strFile.AppendLine("using UnityEngine;");
                strFile.AppendLine("using UnityEngine.UI;");
                strFile.AppendLine("using TEngine;");
                strFile.AppendLine();
                strFile.AppendLine($"namespace {UIScriptGeneratorSetting.GetUINameSpace()}");
                strFile.AppendLine("{");
                {
                    if (!isAutoGenerate)
                    {
                        if (root.name.StartsWith(widgetPrefix))
                        {
                            strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")} : {uiTypeName}");
                        }
                        else
                        {
                            var attribute = UIScriptGeneration.Adapter.GetClassAttribute(fileName.Replace(".cs", ""), uiTypeName);
                            if (!string.IsNullOrEmpty(attribute))
                                strFile.AppendLine($"\t{attribute}");
                            strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")} : {uiTypeName}");
                        }
                    }
                    else
                    {
                        strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")} : {uiTypeName}");
                    }

                    strFile.AppendLine("\t{");
                }
            }

            // 脚本工具生成的代码
            strFile.AppendLine($"\t\t#region 脚本工具生成的代码");
            strFile.AppendLine();
            strFile.AppendLine(strVar.ToString());
            strFile.AppendLine($"\t\t{UIScriptGeneration.Adapter.GetBindingMethodDeclaration()}");
            strFile.AppendLine("\t\t{");
            {
                strFile.Append(strBind.ToString());
                strFile.Append(strOnCreate.ToString());
            }
            strFile.AppendLine("\t\t}");
            strFile.AppendLine();
            strFile.Append($"\t\t#endregion");
            strFile.AppendLine();

            if (includeListener)
            {
                strFile.AppendLine();
                strFile.AppendLine("\t\t#region 事件");
                strFile.AppendLine();
                strFile.Append(strCallback.ToString());
                strFile.AppendLine($"\t\t#endregion");
                strFile.AppendLine("\t}");
                strFile.AppendLine("}");
            }

            m_textEditor.Delete();
            m_textEditor.text = strFile.ToString();
            m_textEditor.SelectAll();
            m_textEditor.Copy();

            if (isAutoGenerate)
            {
                string path = savePath?.Replace("\\", "/");
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                var saveFileName = fileName.Replace(".cs", "_Gen.g.cs");
                var filePath = Path.Combine(path, saveFileName).Replace("\\", "/");

                if (File.Exists(filePath))
                {
                    FileAttributes attributes = File.GetAttributes(filePath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(filePath);
                    AssetDatabase.Refresh();
                }

                File.WriteAllText(filePath, strFile.ToString(), Encoding.UTF8);
                File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
                if (isGenImp)
                {
                    GenerateImpCSharpScript(isUniTask, fileName, impSavePath, uiTypeName, root);
                }
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log($"<color=#1E90FF>脚本已生成到剪贴板，请自行Ctl+V粘贴</color>");
            }

            return true;
        }

        private static int m_bindIndex = 0;

        public static void AutoErgodic(Transform root, Transform transform, ref StringBuilder strVar,
            ref StringBuilder strBind, ref StringBuilder strOnCreate, ref StringBuilder strCallback, bool isUniTask)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                WriteAutoScript(root, child, ref strVar, ref strBind, ref strOnCreate, ref strCallback, isUniTask);
                // 跳过 "m_item"
                if (child.name.StartsWith(GetUIWidgetGameObjectName()))
                {
                    continue;
                }

                AutoErgodic(root, child, ref strVar, ref strBind, ref strOnCreate, ref strCallback, isUniTask);
            }
        }

        private static void WriteAutoScript(Transform root, Transform child, ref StringBuilder strVar,
            ref StringBuilder strBind, ref StringBuilder strOnCreate, ref StringBuilder strCallback, bool isUniTask)
        {
            string varName = child.name;
            // 查找相关的规则定义
            var rule = UIScriptGeneratorSetting.GetScriptGenerateRule()
                .Find(r => varName.StartsWith(r.uiElementRegex));

            if (rule == null)
            {
                return;
            }

            var componentName = rule.componentName.ToString();

            if (string.IsNullOrEmpty(componentName))
            {
                return;
            }

            varName = GetVariableName(varName);

            if (string.IsNullOrEmpty(varName))
            {
                return;
            }

            if (!HasBindableComponent(child, rule))
            {
                return;
            }

            strVar.AppendLine($"\t\tprivate {componentName} {varName}{(UIScriptGeneratorSetting.Instance.NullableEnable ? " = null!;" : ";")}");
            if (rule.componentName == UIComponentName.GameObject)
            {
                strBind.AppendLine($"\t\t\t{varName} = m_bindComponent.GetComponent<RectTransform>({m_bindIndex}).gameObject;");
            }
            else if (rule.componentName != UIComponentName.GameObject && rule.isUIWidget)
            {
                var expression = UIScriptGeneration.Adapter.GetWidgetExpression(componentName,
                    $"m_bindComponent.GetComponent<RectTransform>({m_bindIndex}).gameObject");
                strBind.AppendLine($"\t\t\t{varName} = {expression};");
            }
            else
            {
                strBind.AppendLine($"\t\t\t{varName} = m_bindComponent.GetComponent<{componentName}>({m_bindIndex});");
            }
            m_bindIndex++;

            UIAutoGenerationCore.AppendEventCode(rule.componentName, varName, isUniTask,
                strOnCreate, strCallback, false);
        }

        #region GenerateImpCSharp

        private static bool GenerateImpCSharpScript(bool isUniTask = false, string fileName = null, string impSavePath = null, string uiTypeName = null,
            Transform targetRoot = null)
        {
            var root = targetRoot != null ? targetRoot : Selection.activeTransform;
            if (root == null || string.IsNullOrEmpty(fileName))
            {
                return false;
            }
            StringBuilder strCallback = new StringBuilder();

            AutoImpErgodic(root, root, ref strCallback, isUniTask);
            StringBuilder strFile = new StringBuilder();

#if TextMeshPro
            strFile.AppendLine("using TMPro;");
#endif
            if (isUniTask)
            {
                strFile.AppendLine("using Cysharp.Threading.Tasks;");
            }

            strFile.AppendLine("using UnityEngine;");
            strFile.AppendLine("using UnityEngine.UI;");
            strFile.AppendLine("using TEngine;");
            strFile.AppendLine();
            strFile.AppendLine($"namespace {UIScriptGeneratorSetting.GetUINameSpace()}");
            strFile.AppendLine("{");
            {
                var attribute = UIScriptGeneration.Adapter.GetClassAttribute(fileName.Replace(".cs", ""), uiTypeName);
                if (!string.IsNullOrEmpty(attribute))
                    strFile.AppendLine($"\t{attribute}");
                strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")}");
                strFile.AppendLine("\t{");
                {
                    strFile.AppendLine("\t\t#region 事件");
                    strFile.AppendLine();
                    strFile.Append(strCallback.ToString());
                    strFile.AppendLine($"\t\t#endregion");
                }
                strFile.AppendLine("\t}");
            }
            strFile.AppendLine("}");

            m_textEditor.Delete();
            m_textEditor.text = strFile.ToString();
            m_textEditor.SelectAll();
            m_textEditor.Copy();

            string path = impSavePath?.Replace("\\", "/");
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var filePath = Path.Combine(path, fileName).Replace("\\", "/");

            if (File.Exists(filePath))
            {
                Debug.LogWarning("相关实现类脚本已生成，再次生成跳过");
                return false;
            }

            File.WriteAllText(filePath, strFile.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            return true;
        }

        public static void AutoImpErgodic(Transform root, Transform transform, ref StringBuilder strCallback, bool isUniTask)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                WriteAutoImpScript(root, child, ref strCallback, isUniTask);
                // 跳过 "m_item"
                if (child.name.StartsWith(GetUIWidgetGameObjectName()))
                {
                    continue;
                }

                AutoImpErgodic(root, child, ref strCallback, isUniTask);
            }
        }

        private static void WriteAutoImpScript(Transform root, Transform child, ref StringBuilder strCallback, bool isUniTask)
        {
            string varName = child.name;
            // 查找相关的规则定义
            var rule = UIScriptGeneratorSetting.GetScriptGenerateRule()
                .Find(r => varName.StartsWith(r.uiElementRegex));

            if (rule == null)
            {
                return;
            }

            var componentName = rule.componentName.ToString();

            if (string.IsNullOrEmpty(componentName))
            {
                return;
            }

            varName = GetVariableName(varName);

            if (string.IsNullOrEmpty(varName))
            {
                return;
            }

            if (HasBindableComponent(child, rule))
            {
                UIAutoGenerationCore.AppendEventCode(rule.componentName, varName, isUniTask,
                    null, strCallback, true);
            }
        }

        #endregion


        #region GenerateUIComponent

        public static bool GenerateUIComponentScript(Transform targetRoot = null)
        {
            var root = targetRoot != null ? targetRoot : Selection.activeTransform;

            if (root == null || !UIScriptGeneration.IsAvailable)
            {
                return false;
            }
            // 检查是否在预制体编辑模式下
            bool isInPrefabMode = IsInPrefabMode(root.gameObject);

            var uiComponent = AddComponent2Window(root);

            if (uiComponent == null)
            {
                return false;
            }

            ErgodicUIComponent(root, root, uiComponent);
            // 如果是预制体模式，需要特殊处理保存
            if (isInPrefabMode)
            {
                SavePrefabChanges(root.gameObject);
            }
            AssetDatabase.Refresh();
            return true;
            // Debug.Log($"<color=#1E90FF>脚本已生成到剪贴板，请自行Ctl+V粘贴</color>");
        }

        public static void ErgodicUIComponent(Transform root, Transform transform, UIBindComponent uiBindComponent)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                WriteScriptUIComponent(root, child, uiBindComponent);

                // 跳过 "m_item"
                if (child.name.StartsWith(GetUIWidgetGameObjectName()))
                {
                    continue;
                }

                ErgodicUIComponent(root, child, uiBindComponent);
            }
        }

        private static void WriteScriptUIComponent(Transform root, Transform child, UIBindComponent uiBindComponent)
        {
            string varName = child.name;
            // 查找相关的规则定义
            var rule = UIScriptGeneratorSetting.GetScriptGenerateRule()
                .Find(r => varName.StartsWith(r.uiElementRegex));

            if (rule == null)
            {
                return;
            }

            var componentName = rule.componentName.ToString();

            if (string.IsNullOrEmpty(componentName))
            {
                return;
            }

            varName = GetVariableName(varName);

            if (string.IsNullOrEmpty(varName))
            {
                return;
            }

            var com = GetBindableComponent(child, rule);
            if (com == null)
            {
                Debug.LogError($"{child.name}上未找到可绑定组件: {componentName}", child);
                return;
            }
            uiBindComponent.AddComponent(com);
        }

        private static System.Type GetComponentTypeFromEnumName(string enumName)
        {
            System.Type type = System.Type.GetType($"UnityEngine.{enumName}, UnityEngine");
            if (type != null) return type;

            type = System.Type.GetType($"UnityEngine.UI.{enumName}, UnityEngine.UI");
            if (type != null) return type;

            type = UIScriptGeneration.Adapter.ResolveComponentType(enumName);
            if (type != null) return type;

            type = System.Type.GetType($"TMPro.{enumName}, TMPro");
            if (type != null) return type;

            type = System.Type.GetType(enumName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(enumName);
                if (type != null) return type;

                type = assembly.GetType($"UnityEngine.{enumName}");
                if (type != null) return type;

                type = assembly.GetType($"UnityEngine.UI.{enumName}");
                if (type != null) return type;

                type = assembly.GetType($"TMPro.{enumName}");
                if (type != null) return type;
            }

            return null;
        }

        private static bool HasBindableComponent(Transform child, UIScriptGenerateRuler rule)
        {
            return GetBindableComponent(child, rule) != null;
        }

        private static Component GetBindableComponent(Transform child, UIScriptGenerateRuler rule)
        {
            if (rule.componentName == UIComponentName.GameObject || rule.isUIWidget)
            {
                return child.GetComponent<RectTransform>();
            }

            var type = GetComponentTypeFromEnumName(rule.componentName)
                       ?? GetComponentTypeFromEnumName(rule.componentName.ToString());
            return type != null && typeof(Component).IsAssignableFrom(type) ? child.GetComponent(type) : null;
        }

        private static System.Type GetComponentTypeFromEnumName(UIComponentName enumName)
        {
            return enumName switch
            {
                UIComponentName.GameObject => typeof(GameObject),
                UIComponentName.Button => typeof(Button),
                UIComponentName.Toggle => typeof(Toggle),
                UIComponentName.Slider => typeof(Slider),
                UIComponentName.Text => typeof(Text),
                UIComponentName.Canvas => typeof(Canvas),
                UIComponentName.Image => typeof(Image),
                UIComponentName.RectTransform => typeof(RectTransform),
                UIComponentName.Transform => typeof(Transform),
                UIComponentName.AnimationCurve => typeof(AnimationCurve),
                UIComponentName.Scrollbar => typeof(Scrollbar),
                UIComponentName.ScrollRect => typeof(ScrollRect),
                UIComponentName.CanvasGroup => typeof(CanvasGroup),
                UIComponentName.InputField => typeof(InputField),
                UIComponentName.ToggleGroup => typeof(ToggleGroup),
                UIComponentName.RawImage => typeof(RawImage),
                UIComponentName.GridLayoutGroup => typeof(GridLayoutGroup),
                UIComponentName.HorizontalLayoutGroup => typeof(HorizontalLayoutGroup),
                UIComponentName.VerticalLayoutGroup => typeof(VerticalLayoutGroup),
                UIComponentName.Dropdown => typeof(Dropdown),
#if TextMeshPro
                UIComponentName.TMP_InputField => typeof(TMP_InputField),
                UIComponentName.TMP_Dropdown => typeof(TMP_Dropdown),
                UIComponentName.TextMeshProUGUI => typeof(TextMeshProUGUI),
#endif
                _ => null,
            };
        }

        private static UIBindComponent AddComponent2Window(Transform root)
        {
            if (root == null)
            {
                Debug.LogWarning("请先选中一个物体，再进行脚本生成");
                return null;
            }

            GameObject rootObj = root.gameObject;

            var compt = rootObj.GetComponent<UIBindComponent>();

            if (compt == null)
            {
                compt = rootObj.AddComponent<UIBindComponent>();
            }

            compt.Clear();
            return compt;
        }

        private static bool IsInPrefabMode(GameObject gameObject)
        {
#if UNITY_EDITOR
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return true;
            }

            var prefabAssetType = PrefabUtility.GetPrefabAssetType(gameObject);

            if (prefabAssetType != PrefabAssetType.NotAPrefab)
            {
                return true;
            }

            var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
            return prefabInstanceStatus != PrefabInstanceStatus.NotAPrefab;
#else
            return false;
#endif
        }

        private static void SavePrefabChanges(GameObject prefabObject)
        {
#if UNITY_EDITOR
            try
            {
                EditorUtility.SetDirty(prefabObject);
                var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(prefabObject);

                if (prefabInstanceStatus == PrefabInstanceStatus.Connected)
                {
                    var rootPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabObject);

                    if (rootPrefab != null)
                    {
                        PrefabUtility.ApplyPrefabInstance(prefabObject,
                            InteractionMode.AutomatedAction);
                    }
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Debug.Log("预制体修改已保存");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"保存预制体时出错: {e.Message}");
            }
#endif
        }

        #endregion

        private static string GetUIWidgetGameObjectName()
        {
            return UIAutoGenerationCore.GetWidgetGameObjectName();
        }

        private static string GetUIWidgetName()
        {
            return UIAutoGenerationCore.GetWidgetName();
        }

        private static string GetVariableName(string varName)
        {
            return UIAutoGenerationCore.GetVariableName(varName, UIScriptGeneratorSetting.Instance.CodeStyle);
        }
    }
}