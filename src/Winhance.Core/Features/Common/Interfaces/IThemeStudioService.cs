using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service interface for Theme Studio - Advanced color customization system.
    /// Provides multiple color palettes (Standard, Neon, Chrome/Metal) for UI customization.
    /// </summary>
    public interface IThemeStudioService
    {
        /// <summary>
        /// Gets or sets the current accent color.
        /// </summary>
        ThemeColor CurrentAccentColor { get; set; }

        /// <summary>
        /// Gets or sets the current color palette type.
        /// </summary>
        ColorPaletteType CurrentPalette { get; set; }

        /// <summary>
        /// Event raised when the accent color changes.
        /// </summary>
        event EventHandler<ThemeColorChangedEventArgs>? AccentColorChanged;

        /// <summary>
        /// Gets all available colors for a specific palette.
        /// </summary>
        /// <param name="paletteType">The palette type.</param>
        /// <returns>Collection of theme colors.</returns>
        IEnumerable<ThemeColor> GetPaletteColors(ColorPaletteType paletteType);

        /// <summary>
        /// Gets all available palettes with their colors.
        /// </summary>
        /// <returns>Dictionary of palette types and their colors.</returns>
        IDictionary<ColorPaletteType, IEnumerable<ThemeColor>> GetAllPalettes();

        /// <summary>
        /// Applies a specific color as the accent color.
        /// </summary>
        /// <param name="color">The color to apply.</param>
        void ApplyAccentColor(ThemeColor color);

        /// <summary>
        /// Applies a color by its hex value.
        /// </summary>
        /// <param name="hexColor">Hex color string (e.g., "#FF00FF").</param>
        void ApplyAccentColorHex(string hexColor);

        /// <summary>
        /// Saves the current theme settings.
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Loads saved theme settings.
        /// </summary>
        void LoadSettings();

        /// <summary>
        /// Resets to default theme settings.
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Gets a preview of how colors would look with the current background.
        /// </summary>
        /// <param name="color">The color to preview.</param>
        /// <returns>Contrast information for the color.</returns>
        ColorContrastInfo GetColorContrast(ThemeColor color);
    }

    /// <summary>
    /// Color palette types available in Theme Studio.
    /// </summary>
    public enum ColorPaletteType
    {
        /// <summary>
        /// Standard bright colors - Yellow, Pink, Orange, Gold, Teal, etc.
        /// </summary>
        Standard,

        /// <summary>
        /// Neon/fluorescent colors - High saturation, glow effect.
        /// </summary>
        Neon,

        /// <summary>
        /// Chrome/Metallic colors - Silver, gold, bronze, platinum effects.
        /// </summary>
        ChromeMetal,

        /// <summary>
        /// Pastel colors - Soft, muted tones.
        /// </summary>
        Pastel,

        /// <summary>
        /// User's custom saved colors.
        /// </summary>
        Custom,
    }

    /// <summary>
    /// Represents a theme color with metadata.
    /// </summary>
    public class ThemeColor
    {
        /// <summary>
        /// Gets or sets the color name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hex color value (e.g., "#FFDE00").
        /// </summary>
        public string Hex { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RGB red component (0-255).
        /// </summary>
        public byte R { get; set; }

        /// <summary>
        /// Gets or sets the RGB green component (0-255).
        /// </summary>
        public byte G { get; set; }

        /// <summary>
        /// Gets or sets the RGB blue component (0-255).
        /// </summary>
        public byte B { get; set; }

        /// <summary>
        /// Gets or sets the palette this color belongs to.
        /// </summary>
        public ColorPaletteType Palette { get; set; }

        /// <summary>
        /// Gets or sets an optional glow color for neon effects.
        /// </summary>
        public string? GlowHex { get; set; }

        /// <summary>
        /// Gets or sets whether this is a gradient color.
        /// </summary>
        public bool IsGradient { get; set; }

        /// <summary>
        /// Gets or sets the secondary color for gradients.
        /// </summary>
        public string? GradientEndHex { get; set; }

        /// <summary>
        /// Gets or sets whether this is a custom user color.
        /// </summary>
        public bool IsCustom { get; set; }

        /// <summary>
        /// Creates a ThemeColor from hex string.
        /// </summary>
        /// <param name="name">Color name.</param>
        /// <param name="hex">Hex color value.</param>
        /// <param name="palette">Palette type.</param>
        /// <returns>ThemeColor instance.</returns>
        public static ThemeColor FromHex(string name, string hex, ColorPaletteType palette)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return new ThemeColor
                {
                    Name = name,
                    Hex = $"#{hex}",
                    R = Convert.ToByte(hex.Substring(0, 2), 16),
                    G = Convert.ToByte(hex.Substring(2, 2), 16),
                    B = Convert.ToByte(hex.Substring(4, 2), 16),
                    Palette = palette,
                };
            }

            throw new ArgumentException("Invalid hex color format", nameof(hex));
        }

        /// <summary>
        /// Creates a neon color with glow effect.
        /// </summary>
        /// <param name="name">Color name.</param>
        /// <param name="hex">Base hex color.</param>
        /// <param name="glowHex">Glow effect hex color.</param>
        /// <returns>ThemeColor instance.</returns>
        public static ThemeColor CreateNeon(string name, string hex, string glowHex)
        {
            var color = FromHex(name, hex, ColorPaletteType.Neon);
            color.GlowHex = glowHex.StartsWith('#') ? glowHex : $"#{glowHex}";
            return color;
        }

        /// <summary>
        /// Creates a chrome/metallic gradient color.
        /// </summary>
        /// <param name="name">Color name.</param>
        /// <param name="startHex">Gradient start hex.</param>
        /// <param name="endHex">Gradient end hex.</param>
        /// <returns>ThemeColor instance.</returns>
        public static ThemeColor CreateChrome(string name, string startHex, string endHex)
        {
            var color = FromHex(name, startHex, ColorPaletteType.ChromeMetal);
            color.IsGradient = true;
            color.GradientEndHex = endHex.StartsWith('#') ? endHex : $"#{endHex}";
            return color;
        }
    }

    /// <summary>
    /// Event args for accent color changes.
    /// </summary>
    public class ThemeColorChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the previous color.
        /// </summary>
        public ThemeColor? OldColor { get; set; }

        /// <summary>
        /// Gets or sets the new color.
        /// </summary>
        public ThemeColor NewColor { get; set; } = null!;
    }

    /// <summary>
    /// Contrast information for a color against the background.
    /// </summary>
    public class ColorContrastInfo
    {
        /// <summary>
        /// Gets or sets the contrast ratio (WCAG standard).
        /// </summary>
        public double ContrastRatio { get; set; }

        /// <summary>
        /// Gets or sets whether the contrast meets WCAG AA standard.
        /// </summary>
        public bool MeetsWcagAA { get; set; }

        /// <summary>
        /// Gets or sets whether the contrast meets WCAG AAA standard.
        /// </summary>
        public bool MeetsWcagAAA { get; set; }

        /// <summary>
        /// Gets or sets the perceived brightness (0-255).
        /// </summary>
        public int Brightness { get; set; }

        /// <summary>
        /// Gets or sets whether the color is readable on dark background.
        /// </summary>
        public bool IsReadableOnDark { get; set; }
    }
}
