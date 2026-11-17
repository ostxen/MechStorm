using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace TEngine.Editor.UI
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "TEngine/UIScriptGeneratorSetting", fileName = "UIScriptGeneratorSetting")]
    [MovedFrom(true, "TEngine.Editor.UI", "Assembly-CSharp-Editor", "ScriptGeneratorSetting")]
    public class UIScriptGeneratorSetting : ScriptableObject
    {
        private static UIScriptGeneratorSetting _instance;

        public static UIScriptGeneratorSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:UIScriptGeneratorSetting");
                    if (guids.Length >= 1)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = AssetDatabase.LoadAssetAtPath<UIScriptGeneratorSetting>(path);
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private bool useBindComponent;

        // [FolderPath]
        // [LabelText("默认组件代码保存路径")]
        [SerializeField] private string genCodePath = "Assets/UI/Generated";
        [SerializeField] private string impCodePath = "Assets/UI";

        // [LabelText("绑定代码命名空间")]
        [SerializeField]
        private string _namespace = "UI";

        // [LabelText("子组件名称(不会往下继续遍历)")]
        [SerializeField]
        private string _widgetName = "item";

        public bool UseBindComponent => useBindComponent;

        public string GenCodePath => genCodePath;
        public string ImpCodePath => impCodePath;

        public string Namespace => _namespace;

        public string WidgetName => _widgetName;

        public UIFieldCodeStyle CodeStyle = UIFieldCodeStyle.UnderscorePrefix;

        [SerializeField]
        private List<UIGenType> uiGenTypes = new List<UIGenType>()
        {
            new UIGenType("UIWindow", false),
            new UIGenType("UIWidget", false),
        };
        public List<UIGenType> UIGenTypes => uiGenTypes;

        public bool NullableEnable;

        [FormerlySerializedAs("scriptGenerateRule")]
        [SerializeField]
        private List<UIScriptGenerateRuler> uiScriptGenerateRule = new List<UIScriptGenerateRuler>()
        {
            new UIScriptGenerateRuler("m_go", UIComponentName.GameObject),
            new UIScriptGenerateRuler("m_item", UIComponentName.GameObject),
            new UIScriptGenerateRuler("m_tf", UIComponentName.Transform),
            new UIScriptGenerateRuler("m_rect", UIComponentName.RectTransform),
            new UIScriptGenerateRuler("m_text", UIComponentName.Text),
            new UIScriptGenerateRuler("m_richText", UIComponentName.RichTextItem),
            new UIScriptGenerateRuler("m_btn", UIComponentName.Button),
            new UIScriptGenerateRuler("m_img", UIComponentName.Image),
            new UIScriptGenerateRuler("m_rimg", UIComponentName.RawImage),
            new UIScriptGenerateRuler("m_scrollBar", UIComponentName.Scrollbar),
            new UIScriptGenerateRuler("m_scroll", UIComponentName.ScrollRect),
            new UIScriptGenerateRuler("m_input", UIComponentName.InputField),
            new UIScriptGenerateRuler("m_grid", UIComponentName.GridLayoutGroup),
            new UIScriptGenerateRuler("m_hlay", UIComponentName.HorizontalLayoutGroup),
            new UIScriptGenerateRuler("m_vlay", UIComponentName.VerticalLayoutGroup),
            new UIScriptGenerateRuler("m_slider", UIComponentName.Slider),
            new UIScriptGenerateRuler("m_group", UIComponentName.ToggleGroup),
            new UIScriptGenerateRuler("m_curve", UIComponentName.AnimationCurve),
            new UIScriptGenerateRuler("m_canvasGroup", UIComponentName.CanvasGroup),
            new UIScriptGenerateRuler("m_toggle",UIComponentName.Toggle),
#if TextMeshPro
            new UIScriptGenerateRuler("m_tmpInput", UIComponentName.TMP_InputField),
            new UIScriptGenerateRuler("m_tmpDropdown", UIComponentName.TMP_Dropdown),
            new UIScriptGenerateRuler("m_tmp",UIComponentName.TextMeshProUGUI),
#endif
        };

        public List<UIScriptGenerateRuler> UIScriptGenerateRule => uiScriptGenerateRule;


        [MenuItem("TEngine/Create UIScriptGeneratorSetting")]
        private static void CreateAutoBindGlobalSetting()
        {
            string[] paths = AssetDatabase.FindAssets("t:UIScriptGeneratorSetting");
            if (paths.Length >= 1)
            {
                string path = AssetDatabase.GUIDToAssetPath(paths[0]);
                EditorUtility.DisplayDialog("警告", $"已存在UIScriptGeneratorSetting，路径:{path}", "确认");
                return;
            }

            UIScriptGeneratorSetting setting = CreateInstance<UIScriptGeneratorSetting>();
            AssetDatabase.CreateAsset(
                setting,
                "Assets/Editor/UIScriptGenerator/UIScriptGeneratorSetting.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static List<UIScriptGenerateRuler> GetScriptGenerateRule()
        {
            if (Instance == null)
            {
                return null;
            }
            return Instance.UIScriptGenerateRule;
        }

        public static string GetUINameSpace()
        {
            if (Instance == null)
            {
                return string.Empty;
            }

            return Instance.Namespace;
        }

        public static UIFieldCodeStyle GetCodeStyle()
        {
            if (Instance == null)
            {
                return UIFieldCodeStyle.UnderscorePrefix;
            }

            return Instance.CodeStyle;
        }

        public static string GetGenCodePath() => Instance?.GenCodePath;
        public static string GetImpCodePath() => Instance?.ImpCodePath;

        public static string GetWidgetName()
        {
            if (Instance == null)
            {
                return string.Empty;
            }

            return Instance.WidgetName;
        }

        public static string GetPrefixNameByCodeStyle(UIFieldCodeStyle style)
        {
            return style switch
            {
                UIFieldCodeStyle.UnderscorePrefix => "_",
                UIFieldCodeStyle.MPrefix => "m_",
                _ => "m_"
            };
        }

        public static string GetUIComponentWithoutPrefixName(UIComponentName uiComponentName)
        {
            if (Instance.UIScriptGenerateRule == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < Instance.UIScriptGenerateRule.Count; i++)
            {
                var rule = Instance.UIScriptGenerateRule[i];

                if (rule.componentName == uiComponentName)
                {
                    return rule.uiElementRegex.Substring(rule.uiElementRegex.IndexOf("_", StringComparison.Ordinal) + 1);
                }
            }
            return string.Empty;
        }

        public static UIGenType GetUIGenType(string uiGenTypeName)
        {
            if (string.IsNullOrEmpty(uiGenTypeName))
            {
                return null;
            }
            var tempList = Instance.UIGenTypes;
            for (int i = 0; i < tempList.Count; i++)
            {
                var uiGenType = tempList[i];

                if (string.Equals(uiGenTypeName, uiGenType.uiTypeName, StringComparison.Ordinal))
                {
                    return uiGenType;
                }
            }
            return null;
        }
    }
}