using NuclearOption.UIStyleSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class HudStyle
    {
        private static readonly Color FallbackNormal = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color FallbackWarning = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color FallbackAlert = new Color(1f, 0.25f, 0.2f, 1f);

        public static Color Normal { get; private set; } = FallbackNormal;
        public static Color Warning { get; private set; } = FallbackWarning;
        public static Color Alert { get; private set; } = FallbackAlert;
        public static Color Dim => WithAlpha(Normal, 0.45f);
        public static Color Panel => new Color(0f, 0f, 0f, 0.45f);

        public static TMP_FontAsset Font { get; private set; }
        public static Material FontMaterial { get; private set; }

        public static void Refresh()
        {
            Normal = FallbackNormal;
            Warning = FallbackWarning;
            Alert = FallbackAlert;
            try
            {
                ColorTheme theme = ThemeManager.Active?.ColorTheme;
                if (theme != null)
                {
                    Normal = theme.AllClear;
                    Warning = theme.Warning;
                    Alert = theme.Alert;
                }
            }
            catch { }

            Font = null;
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null)
            {
                TextMeshProUGUI sample = GameAccess.CombatHudTargetText(combatHud);
                if (sample != null)
                {
                    Font = sample.font;
                    FontMaterial = ReadableMaterial(sample.fontSharedMaterial);
                }
            }
        }

        private static Material _readable;
        private static Material _readableSource;

        private static Material ReadableMaterial(Material source)
        {
            if (source == null) return null;
            if (source == _readableSource && _readable != null) return _readable;
            _readableSource = source;
            ShaderUtilities.GetShaderPropertyIDs();
            bool additive = source.name.IndexOf("Additive", System.StringComparison.OrdinalIgnoreCase) >= 0
                || (source.shader != null && source.shader.name.IndexOf("Additive", System.StringComparison.OrdinalIgnoreCase) >= 0);
            bool styled = additive || source.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay) || source.IsKeywordEnabled(ShaderUtilities.Keyword_Outline)
                || (source.HasProperty(ShaderUtilities.ID_OutlineWidth) && source.GetFloat(ShaderUtilities.ID_OutlineWidth) > 0f);
            if (styled)
            {
                _readable = source;
                return _readable;
            }
            _readable = new Material(source) { name = source.name + " (PlayableVehicles shadow)" };
            _readable.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            _readable.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
            _readable.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            _readable.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            _readable.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
            _readable.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.4f);
            return _readable;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform CreateFill(string name, Transform parent)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var rect = CreateRect(name, parent, anchor, anchoredPosition, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static RawImage CreateRawImage(string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Texture texture, Color color)
        {
            var rect = CreateRect(name, parent, anchor, anchoredPosition, size);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 size,
            float fontSize, TextAlignmentOptions alignment)
        {
            var rect = CreateRect(name, parent, anchor, anchoredPosition, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) text.font = Font;
            if (FontMaterial != null) text.fontSharedMaterial = FontMaterial;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Normal;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        public static Texture2D CreateRingTexture(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - thickness;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(dist - radius) / thickness);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D _vignette;
        private static int _vignetteSize;

        public static Texture2D CreateVignetteTexture(int size)
        {
            if (_vignette != null && _vignetteSize == size) return _vignette;
            if (_vignette != null) Object.Destroy(_vignette);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((dist - r * 0.7f) / (r * 0.3f));
                    pixels[y * size + x] = new Color(0f, 0.02f, 0f, alpha * 0.96f);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _vignette = tex;
            _vignetteSize = size;
            return tex;
        }
    }

    internal static class HudReadings
    {
        private static long Round(double value)
        {
            return (long)System.Math.Floor(value + 0.5);
        }

        public static long SpeedGroundKey(float speed)
        {
            return PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Metric ? Round(speed * 3.6) : -1 - Round(speed * 2.23694);
        }

        public static long DistanceKey(float distance)
        {
            const long tier = 1000000000L;
            if (PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Metric)
            {
                if (distance > 10000f) return 3 * tier + Round(distance * 0.001f);
                if (distance > 1000f) return 2 * tier + Round(distance * 0.001f * 10f);
                return tier + Round(distance);
            }
            float yards = distance * 1.09361f;
            return yards < 1000f ? -tier - Round(yards) : -2 * tier - Round(distance * 0.000539957f * 10f);
        }
    }

    internal sealed class DistanceText
    {
        private readonly string _prefix;
        private long _key = long.MinValue;
        private string _text;

        public DistanceText(string prefix = "")
        {
            _prefix = prefix;
        }

        public string Get(float distance)
        {
            long key = HudReadings.DistanceKey(distance);
            if (_text == null || key != _key)
            {
                _key = key;
                _text = _prefix + UnitConverter.DistanceReading(distance);
            }
            return _text;
        }
    }

    internal sealed class SpeedGroundText
    {
        private long _key = long.MinValue;
        private string _text;

        public string Get(float speed)
        {
            long key = HudReadings.SpeedGroundKey(speed);
            if (_text == null || key != _key)
            {
                _key = key;
                _text = UnitConverter.SpeedReadingGround(speed);
            }
            return _text;
        }
    }
}
