using MechStorm.Presentation.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MechStorm.Presentation.Tests
{
    public class UnitHealthBarViewTests
    {
        [Test]
        public void ConstructionLoadsBuiltinFontAndDisplaysDurability()
        {
            var anchor = new GameObject("HealthBarTestAnchor");
            try
            {
                var view = new UnitHealthBarView(anchor.transform, null);
                view.RefreshValue(75, 100);
                var text = anchor.GetComponentInChildren<Text>();
                Assert.That(text, Is.Not.Null);
                Assert.That(text.font, Is.SameAs(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")));
                Assert.That(text.text, Is.EqualTo("75/100"));
            }
            finally
            {
                Object.DestroyImmediate(anchor);
            }
        }
    }
}
