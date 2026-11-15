using System.Collections;
using GameLogic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TEngine.UIBinding.Tests
{
    [Category("UIBindingHotfix")]
    public sealed class UIBindingLifecycleTests
    {
        public sealed class BindingWidget : UIWidget
        {
            public Button Button { get; private set; }
            public int Clicks { get; private set; }
            public int DestroyCount { get; private set; }

            protected override void ScriptGenerator()
            {
                Button = gameObject.GetComponent<UIBindComponent>().GetComponent<Button>(0);
            }

            protected override void RegisterEvent() => Button.onClick.AddListener(OnClick);
            private void OnClick() => Clicks++;

            protected override void OnDestroy()
            {
                Button.onClick.RemoveListener(OnClick);
                DestroyCount++;
            }
        }

        [UnityTest]
        public IEnumerator PrefabWidget_CreatesHidesDestroysAndReopensWithHotfixBinding()
        {
            var ownerRoot = new GameObject("BindingTestOwner", typeof(RectTransform));
            var template = new GameObject("BindingTestTemplate", typeof(RectTransform), typeof(UIBindComponent));
            var buttonRoot = new GameObject("Button", typeof(RectTransform), typeof(Button));
            buttonRoot.transform.SetParent(template.transform, false);
            var binding = template.GetComponent<UIBindComponent>();
            // Runtime binding fields have no public mutation API; emulate deserialized prefab references.
            typeof(UIBindComponent).GetField("m_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(binding, new System.Collections.Generic.List<Component> { buttonRoot.GetComponent<Button>() });
            var owner = new UIBase();
            BindingWidget widget = null;
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    widget = owner.CreateWidgetByPrefab<BindingWidget>(template, ownerRoot.transform);
                    Assert.That(widget.IsPrepare, Is.True);
                    Assert.That(widget.Button, Is.Not.SameAs(buttonRoot.GetComponent<Button>()));
                    Assert.That(widget.gameObject.GetComponent<UIBindComponent>().GetType().Assembly.GetName().Name, Is.EqualTo("GameLogic"));
                    widget.Button.onClick.Invoke();
                    Assert.That(widget.Clicks, Is.EqualTo(1));
                    widget.Visible = false;
                    Assert.That(widget.gameObject.activeSelf, Is.False);
                    widget.Visible = true;
                    widget.Button.onClick.Invoke();
                    Assert.That(widget.Clicks, Is.EqualTo(2));
                    var instance = widget.gameObject;
                    widget.Destroy();
                    Assert.That(widget.DestroyCount, Is.EqualTo(1));
                    yield return null;
                    Assert.That(instance == null, Is.True);
                    widget = null;
                }
            }
            finally
            {
                if (widget != null && widget.gameObject != null) widget.Destroy();
                Object.Destroy(ownerRoot);
                Object.Destroy(template);
            }
            yield return null;
        }
    }
}
