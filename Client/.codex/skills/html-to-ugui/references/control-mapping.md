# 当前窗口的控件映射

实现：`Assets/TEngine/Extension/HtmlToUGUI/Editor/HtmlToUGUIBaker.cs`。
这是交互窗口，不存在共用BakeService/自动CLI服务。

| UI-DSL type | Unity结构 |
| --- | --- |
| div / image | RectTransform + Image |
| text | TextMeshProUGUI或Legacy Text |
| button | Image + Button及文字子节点 |
| input | TMP_InputField/InputField及文本、占位层级 |
| scroll | ScrollRect + Viewport + Content |
| toggle | Toggle + Background + Checkmark + Label |
| slider | Slider + Background + Fill + Handle |
| dropdown | Dropdown及Label、Arrow、Template层级 |

v1采用固定左上坐标，v2按显式anchors/offsets、layoutHint及几何推断处理。
图片优先imageSrc，其次backgroundImageSrc；浏览器输入应使用HTML相对本地路径。
Unity窗口可解析Assets/Packages路径或本地文件，可能复制图片及调整importer，必须先确认具体写入范围。
缺图仅可能警告，不存在自动化严格预检保证。不可声称可重复运行保留任意手工内容、组件fileID或Prefab GUID。

contain/cover通常保留宽高比，cover不实现CSS crop-to-fill；slice依赖Sprite border。
字体不会自动转为TMP资产，需在Unity单独验证字形和排版。safeArea不创建运行时安全区组件。
Unity节点生成不等于保存Prefab，也不等于接好现有业务绑定。
