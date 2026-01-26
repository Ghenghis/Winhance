using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Theme Studio implementation - Advanced color customization with multiple palettes.
    /// All colors are bright/vivid to be visible on dark backgrounds.
    /// </summary>
    public class ThemeStudioService : IThemeStudioService
    {
        private readonly ILogService _logService;
        private readonly string _settingsPath;
        private ThemeColor _currentAccentColor;
        private ColorPaletteType _currentPalette;

        private readonly Dictionary<ColorPaletteType, List<ThemeColor>> _palettes;

        public event EventHandler<ThemeColorChangedEventArgs>? AccentColorChanged;

        public ThemeStudioService(ILogService logService)
        {
            _logService = logService;
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "theme-studio.json");

            _palettes = InitializePalettes();

            // Default to golden yellow (current theme)
            _currentAccentColor = _palettes[ColorPaletteType.Standard]
                .First(c => c.Name == "Gold");
            _currentPalette = ColorPaletteType.Standard;

            LoadSettings();
        }

        public ThemeColor CurrentAccentColor
        {
            get => _currentAccentColor;
            set
            {
                var oldColor = _currentAccentColor;
                _currentAccentColor = value;
                AccentColorChanged?.Invoke(this, new ThemeColorChangedEventArgs
                {
                    OldColor = oldColor,
                    NewColor = value,
                });
            }
        }

        public ColorPaletteType CurrentPalette
        {
            get => _currentPalette;
            set => _currentPalette = value;
        }

        public IEnumerable<ThemeColor> GetPaletteColors(ColorPaletteType paletteType)
        {
            return _palettes.TryGetValue(paletteType, out var colors)
                ? colors.AsReadOnly()
                : Enumerable.Empty<ThemeColor>();
        }

        public IDictionary<ColorPaletteType, IEnumerable<ThemeColor>> GetAllPalettes()
        {
            return _palettes.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.AsEnumerable());
        }

        public void ApplyAccentColor(ThemeColor color)
        {
            CurrentAccentColor = color;
            CurrentPalette = color.Palette;
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"Theme Studio: Applied {color.Palette} color '{color.Name}' ({color.Hex})");
        }

        public void ApplyAccentColorHex(string hexColor)
        {
            var color = ThemeColor.FromHex("Custom", hexColor, ColorPaletteType.Custom);
            color.IsCustom = true;
            ApplyAccentColor(color);
        }

        public void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new ThemeStudioSettings
                {
                    AccentColorHex = _currentAccentColor.Hex,
                    AccentColorName = _currentAccentColor.Name,
                    PaletteType = _currentPalette.ToString(),
                    IsCustomColor = _currentAccentColor.IsCustom,
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                    $"Failed to save theme settings: {ex.Message}");
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return;
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<ThemeStudioSettings>(json);

                if (settings != null)
                {
                    if (Enum.TryParse<ColorPaletteType>(settings.PaletteType, out var palette))
                    {
                        _currentPalette = palette;
                    }

                    if (settings.IsCustomColor)
                    {
                        _currentAccentColor = ThemeColor.FromHex(
                            settings.AccentColorName ?? "Custom",
                            settings.AccentColorHex ?? "#FFDE00",
                            ColorPaletteType.Custom);
                        _currentAccentColor.IsCustom = true;
                    }
                    else
                    {
                        // Find the color in the palette
                        var colors = GetPaletteColors(_currentPalette);
                        var found = colors.FirstOrDefault(c =>
                            c.Hex.Equals(settings.AccentColorHex, StringComparison.OrdinalIgnoreCase));
                        if (found != null)
                        {
                            _currentAccentColor = found;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                    $"Failed to load theme settings: {ex.Message}");
            }
        }

        public void ResetToDefaults()
        {
            _currentPalette = ColorPaletteType.Standard;
            _currentAccentColor = _palettes[ColorPaletteType.Standard]
                .First(c => c.Name == "Gold");

            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                }
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        public ColorContrastInfo GetColorContrast(ThemeColor color)
        {
            // Calculate against dark background (#202020)
            const int bgR = 32, bgG = 32, bgB = 32;

            // Calculate relative luminance
            double RelativeLuminance(int r, int g, int b)
            {
                double Rs = r / 255.0;
                double Gs = g / 255.0;
                double Bs = b / 255.0;

                Rs = Rs <= 0.03928 ? Rs / 12.92 : Math.Pow((Rs + 0.055) / 1.055, 2.4);
                Gs = Gs <= 0.03928 ? Gs / 12.92 : Math.Pow((Gs + 0.055) / 1.055, 2.4);
                Bs = Bs <= 0.03928 ? Bs / 12.92 : Math.Pow((Bs + 0.055) / 1.055, 2.4);

                return (0.2126 * Rs) + (0.7152 * Gs) + (0.0722 * Bs);
            }

            var colorLum = RelativeLuminance(color.R, color.G, color.B);
            var bgLum = RelativeLuminance(bgR, bgG, bgB);

            // Contrast ratio
            var lighter = Math.Max(colorLum, bgLum);
            var darker = Math.Min(colorLum, bgLum);
            var contrastRatio = (lighter + 0.05) / (darker + 0.05);

            // Perceived brightness (0-255)
            var brightness = (int)((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000;

            return new ColorContrastInfo
            {
                ContrastRatio = Math.Round(contrastRatio, 2),
                MeetsWcagAA = contrastRatio >= 4.5,
                MeetsWcagAAA = contrastRatio >= 7.0,
                Brightness = brightness,
                IsReadableOnDark = brightness >= 125 && contrastRatio >= 4.5,
            };
        }

        private static Dictionary<ColorPaletteType, List<ThemeColor>> InitializePalettes()
        {
            return new Dictionary<ColorPaletteType, List<ThemeColor>>
            {
                // ═══════════════════════════════════════════════════════════════
                // STANDARD PALETTE - Bright, vibrant colors for dark backgrounds
                // ═══════════════════════════════════════════════════════════════
                [ColorPaletteType.Standard] = new List<ThemeColor>
                {
                    // Current default (golden yellow)
                    ThemeColor.FromHex("Gold", "#FFDE00", ColorPaletteType.Standard),

                    // Yellows
                    ThemeColor.FromHex("Bright Yellow", "#FFFF00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Canary", "#FFEF00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Lemon", "#FFF44F", ColorPaletteType.Standard),

                    // Oranges
                    ThemeColor.FromHex("Orange", "#FF8C00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Tangerine", "#FF9F00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Amber", "#FFBF00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Coral", "#FF7F50", ColorPaletteType.Standard),

                    // Pinks/Magentas
                    ThemeColor.FromHex("Hot Pink", "#FF69B4", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Magenta", "#FF00FF", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Pink", "#FF77FF", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Rose", "#FF66CC", ColorPaletteType.Standard),

                    // Reds
                    ThemeColor.FromHex("Scarlet", "#FF2400", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Tomato", "#FF6347", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Vermilion", "#FF4D00", ColorPaletteType.Standard),

                    // Greens (bright only)
                    ThemeColor.FromHex("Lime", "#00FF00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Spring Green", "#00FF7F", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Chartreuse", "#7FFF00", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Mint", "#98FF98", ColorPaletteType.Standard),

                    // Cyans/Teals (bright only)
                    ThemeColor.FromHex("Cyan", "#00FFFF", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Aqua", "#00FFEF", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Turquoise", "#40E0D0", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Teal Blue", "#00CED1", ColorPaletteType.Standard),

                    // Light Blues (bright only - no dark blues!)
                    ThemeColor.FromHex("Sky Blue", "#87CEEB", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Electric Blue", "#7DF9FF", ColorPaletteType.Standard),

                    // Purples (bright only)
                    ThemeColor.FromHex("Violet", "#EE82EE", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Orchid", "#DA70D6", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Lavender", "#E6E6FA", ColorPaletteType.Standard),

                    // White/Light
                    ThemeColor.FromHex("White", "#FFFFFF", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Snow", "#FFFAFA", ColorPaletteType.Standard),
                    ThemeColor.FromHex("Ivory", "#FFFFF0", ColorPaletteType.Standard),
                },

                // ═══════════════════════════════════════════════════════════════
                // NEON PALETTE - Fluorescent/glowing colors
                // ═══════════════════════════════════════════════════════════════
                [ColorPaletteType.Neon] = new List<ThemeColor>
                {
                    // Neon Yellows
                    ThemeColor.CreateNeon("Neon Yellow", "#FFFF00", "#FFFF66"),
                    ThemeColor.CreateNeon("Electric Yellow", "#FFFF33", "#FFFF99"),

                    // Neon Oranges
                    ThemeColor.CreateNeon("Neon Orange", "#FF6600", "#FF9933"),
                    ThemeColor.CreateNeon("Safety Orange", "#FF7700", "#FFA366"),

                    // Neon Pinks
                    ThemeColor.CreateNeon("Neon Pink", "#FF1493", "#FF69B4"),
                    ThemeColor.CreateNeon("Hot Magenta", "#FF00FF", "#FF66FF"),
                    ThemeColor.CreateNeon("Neon Rose", "#FF007F", "#FF66B2"),
                    ThemeColor.CreateNeon("Fuchsia Glow", "#FF00FF", "#FF99FF"),

                    // Neon Reds
                    ThemeColor.CreateNeon("Neon Red", "#FF0000", "#FF6666"),
                    ThemeColor.CreateNeon("Electric Red", "#FF3333", "#FF9999"),

                    // Neon Greens
                    ThemeColor.CreateNeon("Neon Green", "#39FF14", "#7FFF66"),
                    ThemeColor.CreateNeon("Electric Lime", "#CCFF00", "#E6FF66"),
                    ThemeColor.CreateNeon("UFO Green", "#7FFF00", "#B3FF66"),
                    ThemeColor.CreateNeon("Laser Lime", "#AAFF32", "#CCFF7F"),

                    // Neon Cyans
                    ThemeColor.CreateNeon("Neon Cyan", "#00FFFF", "#66FFFF"),
                    ThemeColor.CreateNeon("Electric Cyan", "#00FFEF", "#66FFF7"),
                    ThemeColor.CreateNeon("Aqua Glow", "#00FFCC", "#66FFE5"),

                    // Neon Blues (light/bright only!)
                    ThemeColor.CreateNeon("Electric Blue", "#7DF9FF", "#B3FCFF"),
                    ThemeColor.CreateNeon("Neon Sky", "#00BFFF", "#66D9FF"),

                    // Neon Purples/Violets
                    ThemeColor.CreateNeon("Neon Purple", "#BF00FF", "#D966FF"),
                    ThemeColor.CreateNeon("Electric Violet", "#8B00FF", "#B366FF"),
                    ThemeColor.CreateNeon("Psychedelic Purple", "#DF00FF", "#EB66FF"),

                    // Neon White
                    ThemeColor.CreateNeon("Neon White", "#F8F8FF", "#FFFFFF"),
                },

                // ═══════════════════════════════════════════════════════════════
                // CHROME/METAL PALETTE - Metallic gradient colors
                // ═══════════════════════════════════════════════════════════════
                [ColorPaletteType.ChromeMetal] = new List<ThemeColor>
                {
                    // Gold Metals
                    ThemeColor.CreateChrome("Chrome Gold", "#FFD700", "#FFA500"),
                    ThemeColor.CreateChrome("Polished Gold", "#FFE55C", "#FFB347"),
                    ThemeColor.CreateChrome("Rose Gold", "#FFB4A2", "#E8A598"),
                    ThemeColor.CreateChrome("Champagne Gold", "#F7E7CE", "#D4AF37"),

                    // Silver/Chrome
                    ThemeColor.CreateChrome("Chrome Silver", "#E8E8E8", "#C0C0C0"),
                    ThemeColor.CreateChrome("Polished Chrome", "#F0F0F0", "#D8D8D8"),
                    ThemeColor.CreateChrome("Platinum", "#E5E4E2", "#BCC6CC"),
                    ThemeColor.CreateChrome("Bright Silver", "#F5F5F5", "#E0E0E0"),

                    // Copper/Bronze
                    ThemeColor.CreateChrome("Copper", "#FFB08A", "#E67E33"),
                    ThemeColor.CreateChrome("Polished Copper", "#FFB87A", "#DA8A67"),
                    ThemeColor.CreateChrome("Bronze", "#E5C08A", "#CD7F32"),
                    ThemeColor.CreateChrome("Rose Bronze", "#FFB5A7", "#CC8A7F"),

                    // Colorful Chromes (bright!)
                    ThemeColor.CreateChrome("Chrome Cyan", "#7FFFFF", "#00E5E5"),
                    ThemeColor.CreateChrome("Chrome Pink", "#FFB6C1", "#FF69B4"),
                    ThemeColor.CreateChrome("Chrome Lime", "#BFFF00", "#7FBF00"),
                    ThemeColor.CreateChrome("Chrome Orange", "#FFBF40", "#FF8C00"),
                    ThemeColor.CreateChrome("Chrome Magenta", "#FF77FF", "#FF00FF"),
                    ThemeColor.CreateChrome("Chrome Yellow", "#FFFF7F", "#FFD700"),
                    ThemeColor.CreateChrome("Chrome Aqua", "#7FFFD4", "#00CED1"),

                    // Pearl Effects
                    ThemeColor.CreateChrome("Pearl White", "#FFFEF0", "#FFF5E0"),
                    ThemeColor.CreateChrome("Pearl Pink", "#FFE4E9", "#FFB6C1"),
                    ThemeColor.CreateChrome("Pearl Cream", "#FFFDD0", "#FFEFD5"),
                },

                // ═══════════════════════════════════════════════════════════════
                // PASTEL PALETTE - Soft, muted tones (bright enough for dark bg)
                // ═══════════════════════════════════════════════════════════════
                [ColorPaletteType.Pastel] = new List<ThemeColor>
                {
                    ThemeColor.FromHex("Pastel Yellow", "#FFFAA0", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Peach", "#FFCBA4", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Orange", "#FFCC99", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Pink", "#FFD1DC", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Coral", "#FFB7B2", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Rose", "#FFC0CB", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Lavender", "#E6E6FA", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Lilac", "#DCD0FF", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Mint", "#BDFCC9", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Green", "#C1F0C1", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Cyan", "#AFEEEE", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Aqua", "#B0E0E6", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Blue", "#B8D4E3", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Sky", "#87CEEB", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Cream", "#FFFDD0", ColorPaletteType.Pastel),
                    ThemeColor.FromHex("Pastel Ivory", "#FFFFF0", ColorPaletteType.Pastel),
                },

                // Custom palette starts empty - users add colors
                [ColorPaletteType.Custom] = new List<ThemeColor>(),
            };
        }

        private class ThemeStudioSettings
        {
            public string? AccentColorHex { get; set; }

            public string? AccentColorName { get; set; }

            public string? PaletteType { get; set; }

            public bool IsCustomColor { get; set; }
        }
    }
}
