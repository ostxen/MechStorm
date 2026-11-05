#if !UNITY_6000_7_OR_NEWER
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Pipeline.Runtime.Commands
{
    /// <summary>Captures the visible panel rectangle, not an isolated subtree, on older engines.</summary>
    static class PanelElementCapture
    {
        const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static byte[] Capture(VisualElement element, out int width, out int height)
        {
            var panel = element.panel;
            if (panel == null || panel.contextType != ContextType.Player)
                throw new InvalidOperationException("A live runtime panel is required; use capture editor_element for Editor windows.");

            var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.BaseRuntimePanel");
            var targetField = panelType?.GetField("targetTexture", InstanceMembers);
            var scaleProperty = panelType?.GetProperty("scaledPixelsPerPoint", InstanceMembers);
            var repaint = panelType?.GetMethod("Repaint", InstanceMembers, null, new[] { typeof(Event) }, null);
            if (targetField == null || scaleProperty == null || repaint == null)
                throw new InvalidOperationException("This engine's runtime panel rendering interface is unavailable.");

            var rootBounds = panel.visualTree.worldBound;
            var scale = (float)scaleProperty.GetValue(panel);
            var target = targetField.GetValue(panel) as RenderTexture;
            int panelWidth = target != null ? target.width : Mathf.CeilToInt(rootBounds.width * scale);
            int panelHeight = target != null ? target.height : Mathf.CeilToInt(rootBounds.height * scale);
            if (panelWidth < 1 || panelHeight < 1 || panelWidth > SystemInfo.maxTextureSize || panelHeight > SystemInfo.maxTextureSize)
                throw new InvalidOperationException("The runtime panel has no valid drawable size.");

            var rt = RenderTexture.GetTemporary(panelWidth, panelHeight, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            var previousCamera = Camera.current;
            Texture2D texture = null;
            GL.PushMatrix();
            try
            {
                Camera.SetupCurrent(null);
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
                repaint.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                if (target != null)
                    Graphics.Blit(target, rt);

                RenderTexture.active = rt;
                texture = new Texture2D(panelWidth, panelHeight, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, panelWidth, panelHeight), 0, 0, false);
                texture.Apply(false, false);
                rootBounds = panel.visualTree.worldBound;
                return Crop(texture, element, rootBounds,
                    new Vector2(panelWidth / rootBounds.width, panelHeight / rootBounds.height), out width, out height);
            }
            finally
            {
                GL.PopMatrix();
                Camera.SetupCurrent(previousCamera);
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                if (texture != null)
                    DestroyTexture(texture);
            }
        }

        internal static byte[] Crop(Texture2D panel, VisualElement element, Rect panelBounds, Vector2 scale, out int width, out int height)
        {
            var rect = PixelRect(element.worldBound, panelBounds, scale, panel.width, panel.height);
            width = rect.width;
            height = rect.height;
            if (!element.visible || element.resolvedStyle.display == DisplayStyle.None || width < 1 || height < 1)
                throw new InvalidOperationException("The element has no visible pixels inside its panel.");

            var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                cropped.SetPixels(panel.GetPixels(rect.x, rect.y, width, height));
                cropped.Apply(false, false);
                return cropped.EncodeToPNG();
            }
            finally
            {
                DestroyTexture(cropped);
            }
        }

        internal static RectInt PixelRect(Rect bounds, Rect panelBounds, Vector2 scale, int width, int height)
        {
            if (float.IsNaN(bounds.x) || float.IsNaN(bounds.y) || float.IsNaN(bounds.width) || float.IsNaN(bounds.height)
                || float.IsInfinity(bounds.x) || float.IsInfinity(bounds.y) || float.IsInfinity(bounds.width) || float.IsInfinity(bounds.height)
                || scale.x <= 0 || scale.y <= 0)
                return new RectInt();

            int left = Mathf.Clamp(Mathf.FloorToInt((bounds.xMin - panelBounds.xMin) * scale.x), 0, width);
            int right = Mathf.Clamp(Mathf.CeilToInt((bounds.xMax - panelBounds.xMin) * scale.x), 0, width);
            int top = Mathf.Clamp(Mathf.FloorToInt((bounds.yMin - panelBounds.yMin) * scale.y), 0, height);
            int bottom = Mathf.Clamp(Mathf.CeilToInt((bounds.yMax - panelBounds.yMin) * scale.y), 0, height);
            return new RectInt(left, height - bottom, Mathf.Max(0, right - left), Mathf.Max(0, bottom - top));
        }

        static void DestroyTexture(Texture2D texture)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return;
            }
#endif
            UnityEngine.Object.Destroy(texture);
        }
    }
}
#endif
