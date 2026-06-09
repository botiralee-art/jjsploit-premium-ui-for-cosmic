using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace JJSploitPremium
{
    public static class AppSettings
    {
        public static string WindowTitle = "JJSploit Premium";
        public static string InjectText = "Inject";
        public static string BackgroundImageUrl = BackgroundManager.DefaultImageUrl;
        public static int BackgroundOverlayAlpha = 55;

        // JJSploit logo cyan theme
        public static Color AccentColor = Color.FromArgb(0, 223, 255);
        public static Color WindowBackground = Color.FromArgb(8, 28, 48);
        public static Color TitleBarBackground = Color.FromArgb(5, 35, 58);
        public static Color PanelBackground = Color.FromArgb(5, 28, 50);
        public static Color EditorBackground = Color.FromArgb(5, 18, 35);
        public static Color EditorTextColor = Color.FromArgb(0, 234, 255);
        public static Color ButtonBackground = Color.FromArgb(0, 70, 105);
        public static Color TextColor = Color.White;
        public static Color InactiveTabColor = Color.FromArgb(0, 45, 75);
        public static Color SuccessColor = Color.FromArgb(40, 180, 100);
        public static Color InstanceTextColor = Color.FromArgb(0, 223, 255);

        public static int EditorFontSize = 12;
        public static string EditorFontName = "Consolas";

        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config.cfg");

        private static readonly Dictionary<string, Action<string>> _colorKeys =
            new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "AccentColor", v => AccentColor = ParseColor(v, AccentColor) },
                { "WindowBackground", v => WindowBackground = ParseColor(v, WindowBackground) },
                { "TitleBarBackground", v => TitleBarBackground = ParseColor(v, TitleBarBackground) },
                { "PanelBackground", v => PanelBackground = ParseColor(v, PanelBackground) },
                { "EditorBackground", v => EditorBackground = ParseColor(v, EditorBackground) },
                { "EditorTextColor", v => EditorTextColor = ParseColor(v, EditorTextColor) },
                { "ButtonBackground", v => ButtonBackground = ParseColor(v, ButtonBackground) },
                { "TextColor", v => TextColor = ParseColor(v, TextColor) },
                { "InactiveTabColor", v => InactiveTabColor = ParseColor(v, InactiveTabColor) },
                { "SuccessColor", v => SuccessColor = ParseColor(v, SuccessColor) },
                { "InstanceTextColor", v => InstanceTextColor = ParseColor(v, InstanceTextColor) },
            };

        /// <summary>Semi-transparent color for Panel controls only.</summary>
        public static Color WithAlpha(Color color, int alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        /// <summary>Opaque blend for controls that cannot use transparent BackColor.</summary>
        public static Color Blend(Color foreground, Color background, int alpha)
        {
            float t = Math.Max(0f, Math.Min(255f, alpha)) / 255f;
            int r = (int)(foreground.R * t + background.R * (1f - t));
            int g = (int)(foreground.G * t + background.G * (1f - t));
            int b = (int)(foreground.B * t + background.B * (1f - t));
            return Color.FromArgb(255, r, g, b);
        }

        public static Color BlendOverWindow(Color color, int alpha) =>
            Blend(color, WindowBackground, alpha);

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;

                foreach (var line in File.ReadAllLines(ConfigPath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    if (key == "WindowTitle") WindowTitle = val;
                    else if (key == "InjectText") InjectText = val;
                    else if (key == "BackgroundImageUrl") BackgroundImageUrl = val;
                    else if (key == "BackgroundOverlayAlpha" && int.TryParse(val, out int alpha))
                        BackgroundOverlayAlpha = Math.Max(0, Math.Min(255, alpha));
                    else if (key == "EditorFontSize" && int.TryParse(val, out int size) && size >= 8 && size <= 32)
                        EditorFontSize = size;
                    else if (key == "EditorFontName" && !string.IsNullOrWhiteSpace(val))
                        EditorFontName = val;
                    else if (_colorKeys.TryGetValue(key, out var setter))
                        setter(val);
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var lines = new[]
                {
                    "WindowTitle=" + WindowTitle,
                    "InjectText=" + InjectText,
                    "BackgroundImageUrl=" + BackgroundImageUrl,
                    "BackgroundOverlayAlpha=" + BackgroundOverlayAlpha,
                    "AccentColor=" + AccentColor.ToArgb(),
                    "WindowBackground=" + WindowBackground.ToArgb(),
                    "TitleBarBackground=" + TitleBarBackground.ToArgb(),
                    "PanelBackground=" + PanelBackground.ToArgb(),
                    "EditorBackground=" + EditorBackground.ToArgb(),
                    "EditorTextColor=" + EditorTextColor.ToArgb(),
                    "ButtonBackground=" + ButtonBackground.ToArgb(),
                    "TextColor=" + TextColor.ToArgb(),
                    "InactiveTabColor=" + InactiveTabColor.ToArgb(),
                    "SuccessColor=" + SuccessColor.ToArgb(),
                    "InstanceTextColor=" + InstanceTextColor.ToArgb(),
                    "EditorFontSize=" + EditorFontSize,
                    "EditorFontName=" + EditorFontName,
                };
                File.WriteAllText(ConfigPath, string.Join(Environment.NewLine, lines));
            }
            catch { }
        }

        public static void ResetToDefaults()
        {
            WindowTitle = "JJSploit Premium";
            InjectText = "Inject";
            BackgroundImageUrl = BackgroundManager.DefaultImageUrl;
            BackgroundOverlayAlpha = 55;
            AccentColor = Color.FromArgb(0, 223, 255);
            WindowBackground = Color.FromArgb(8, 28, 48);
            TitleBarBackground = Color.FromArgb(5, 35, 58);
            PanelBackground = Color.FromArgb(5, 28, 50);
            EditorBackground = Color.FromArgb(5, 18, 35);
            EditorTextColor = Color.FromArgb(0, 234, 255);
            ButtonBackground = Color.FromArgb(0, 70, 105);
            TextColor = Color.White;
            InactiveTabColor = Color.FromArgb(0, 45, 75);
            SuccessColor = Color.FromArgb(40, 180, 100);
            InstanceTextColor = Color.FromArgb(0, 223, 255);
            EditorFontSize = 12;
            EditorFontName = "Consolas";
        }

        private static Color ParseColor(string val, Color fallback)
        {
            return int.TryParse(val, out int argb) ? Color.FromArgb(argb) : fallback;
        }
    }
}
