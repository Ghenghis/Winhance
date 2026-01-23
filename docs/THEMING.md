# Borg Theme Studio Guide 🎨

The Borg Theme Studio is Winhance-FS's visual customization system. It simplifies theming to just **1-5 colors** that automatically propagate to all UI elements.

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Borg Palettes](#borg-palettes)
- [Click-to-Change UI](#click-to-change-ui)
- [Custom Themes](#custom-themes)
- [Theme Files](#theme-files)
- [Developer Guide](#developer-guide)

---

## Overview

Traditional theming requires managing 30+ colors. Borg Theme Studio reduces this to **5 core colors**:

| Color | Purpose |
|-------|---------|
| **Primary** | Main accent, borders, highlights |
| **Secondary** | Supporting backgrounds, fills |
| **Accent** | Interactive elements, buttons |
| **Background** | Window and content backgrounds |
| **Text** | All text and foreground elements |

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        BORG THEME STUDIO                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                    SIMULATED GUI PREVIEW                              │   │
│  │  ┌──────────────┐  ┌────────────────────────────────────────────┐   │   │
│  │  │  Nav Panel   │  │          Content Area                       │   │   │
│  │  │  [clickable] │  │          [clickable regions]                │   │   │
│  │  │              │  │   ┌──────────────────────────────────┐     │   │   │
│  │  │  • Item 1    │  │   │     Feature Card [clickable]     │     │   │   │
│  │  │  • Item 2    │  │   │     Accent Color, Border         │     │   │   │
│  │  │  • Item 3    │  │   └──────────────────────────────────┘     │   │   │
│  │  └──────────────┘  └────────────────────────────────────────────┘   │   │
│  │                                                                      │   │
│  │  Click any region to change its color                               │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  BORG PALETTES           │  CUSTOM COLORS (1-5 max)                  │   │
│  │  ○ Borg Green            │  [1] Primary:    #00FF41 ▼               │   │
│  │  ○ Borg Red (Tactical)   │  [2] Secondary:  #003B00 ▼               │   │
│  │  ○ Borg Blue             │  [3] Accent:     #39FF14 ▼               │   │
│  │  ○ Borg Purple           │  [4] Background: #0D0D0D ▼               │   │
│  │  ○ Borg Gold             │  [5] Text:       #00FF41 ▼               │   │
│  │  ○ Borg Orange           │                                          │   │
│  │  ○ Borg Pink             │  [Apply Theme] [Save As...] [Export]     │   │
│  │  ○ Borg Neon             │                                          │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## How It Works

### 5 Colors → 32+ Tokens

The `BorgThemeGenerator` takes your 5 colors and automatically generates all 32+ Winhance theme tokens:

```csharp
public Dictionary<string, Color> GenerateBorgTheme(BorgPalette palette)
{
    return new Dictionary<string, Color>
    {
        // Text variations
        ["PrimaryTextColor"] = palette.Text,
        ["SecondaryTextColor"] = palette.Text.WithAlpha(0.7),
        ["TertiaryTextColor"] = palette.Text.WithAlpha(0.5),

        // Background variations
        ["WindowBackground"] = palette.Background,
        ["ContentSectionBackground"] = palette.Background.Lighten(0.05),
        ["ElevatedBackground"] = palette.Background.Lighten(0.1),

        // Accent variations
        ["AccentColor"] = palette.Accent,
        ["AccentHoverColor"] = palette.Accent.Lighten(0.1),
        ["AccentPressedColor"] = palette.Accent.Darken(0.1),

        // Control colors
        ["ButtonBorderBrush"] = palette.Primary,
        ["ButtonHoverBackground"] = palette.Primary.WithAlpha(0.2),
        ["ToggleKnobBrush"] = palette.Accent,

        // ... 20+ more tokens auto-generated
    };
}
```

### Color Transformations

| Function | Description | Example |
|----------|-------------|---------|
| `WithAlpha(0.7)` | Set opacity | Text → Subtle text |
| `Lighten(0.1)` | Increase brightness | Background → Elevated |
| `Darken(0.1)` | Decrease brightness | Accent → Pressed state |

---

## Borg Palettes

Eight pre-built palettes optimized for dark themes:

### Borg Green (Default)
```
Primary:    #00FF41  (Matrix green)
Secondary:  #003B00  (Dark green)
Accent:     #39FF14  (Neon green)
Background: #0D0D0D  (Near black)
Text:       #00FF41  (Green text)
```

### Borg Red (Tactical)
```
Primary:    #FF0040  (Alert red)
Secondary:  #3B0000  (Dark red)
Accent:     #FF1744  (Bright red)
Background: #0D0D0D  (Near black)
Text:       #FF4444  (Red text)
```

### Borg Blue
```
Primary:    #00D4FF  (Cyan blue)
Secondary:  #003B4D  (Dark blue)
Accent:     #00E5FF  (Electric blue)
Background: #0D0D0D  (Near black)
Text:       #00BFFF  (Light blue)
```

### Borg Purple
```
Primary:    #9B30FF  (Deep purple)
Secondary:  #2D0047  (Dark purple)
Accent:     #BF40FF  (Bright purple)
Background: #0D0D0D  (Near black)
Text:       #AA55FF  (Light purple)
```

### Borg Gold
```
Primary:    #FFD700  (Gold)
Secondary:  #3B3000  (Dark gold)
Accent:     #FFC107  (Amber)
Background: #0D0D0D  (Near black)
Text:       #FFE066  (Light gold)
```

### Borg Orange
```
Primary:    #FF6600  (Orange)
Secondary:  #3B1F00  (Dark orange)
Accent:     #FF8C00  (Dark orange accent)
Background: #0D0D0D  (Near black)
Text:       #FF9933  (Light orange)
```

### Borg Pink
```
Primary:    #FF00FF  (Magenta)
Secondary:  #3B003B  (Dark magenta)
Accent:     #FF66FF  (Light magenta)
Background: #0D0D0D  (Near black)
Text:       #FF99FF  (Pink)
```

### Borg Neon
```
Primary:    #00FF00  (Pure green)
Secondary:  #001A00  (Very dark green)
Accent:     #66FF66  (Light green)
Background: #000000  (Pure black)
Text:       #00FF00  (Green)
```

---

## Click-to-Change UI

The Simulated GUI Preview shows a miniature version of the Winhance interface. Click any region to customize it:

### Clickable Regions

| Region | Token | What It Affects |
|--------|-------|-----------------|
| Navigation Panel | `NavBackground` | Side navigation area |
| Nav Items | `NavItemColor` | Menu item text |
| Content Area | `ContentBackground` | Main content background |
| Feature Cards | `CardBackground` | Setting group containers |
| Card Borders | `CardBorderBrush` | Card outlines |
| Buttons | `ButtonBackground` | All buttons |
| Toggles | `ToggleBrush` | Toggle switches |
| Text | `PrimaryTextColor` | Main text |

### Usage

1. Click a region in the preview
2. Color picker popup appears
3. Select a new color
4. Preview updates instantly
5. Click "Apply Theme" when satisfied

---

## Custom Themes

### Creating a Theme

1. Start with a Borg palette or from scratch
2. Adjust the 5 core colors using the color pickers
3. Use the click-to-change preview for fine-tuning
4. Click "Save As..." to name and save your theme

### Sharing Themes

Themes are saved as JSON files:

```json
{
  "name": "My Custom Theme",
  "version": "1.0",
  "author": "YourName",
  "palette": {
    "primary": "#00FF41",
    "secondary": "#003B00",
    "accent": "#39FF14",
    "background": "#0D0D0D",
    "text": "#00FF41"
  }
}
```

### Importing Themes

1. Click "Import..."
2. Select a `.json` theme file
3. Theme is added to your palette list
4. Click the theme name to apply it

---

## Theme Files

### Location

```
%APPDATA%\Winhance-FS\themes\
├── borg-green.json        (Built-in)
├── borg-red.json          (Built-in)
├── borg-blue.json         (Built-in)
├── ...
└── custom\
    ├── my-theme.json      (User-created)
    └── shared-theme.json  (Imported)
```

### File Format

```json
{
  "name": "Theme Name",
  "version": "1.0",
  "author": "Author Name",
  "description": "Optional description",
  "created": "2026-01-18T00:00:00Z",
  "palette": {
    "primary": "#RRGGBB",
    "secondary": "#RRGGBB",
    "accent": "#RRGGBB",
    "background": "#RRGGBB",
    "text": "#RRGGBB"
  },
  "overrides": {
    "AccentColor": "#CUSTOM",
    "ButtonBorderBrush": "#CUSTOM"
  }
}
```

The `overrides` section allows specific token customization beyond the 5-color system.

---

## Developer Guide

### Adding Theme Support to Views

Use `DynamicResource` for theme-aware styling:

```xaml
<Button Background="{DynamicResource ButtonBackground}"
        Foreground="{DynamicResource ButtonForeground}"
        BorderBrush="{DynamicResource ButtonBorderBrush}">
    Click Me
</Button>
```

### Theme Token Reference

| Token | Default Usage |
|-------|---------------|
| `PrimaryTextColor` | Main text |
| `SecondaryTextColor` | Subtle text (70% opacity) |
| `TertiaryTextColor` | Disabled text (50% opacity) |
| `WindowBackground` | Window background |
| `ContentSectionBackground` | Content area |
| `ElevatedBackground` | Cards, popups |
| `AccentColor` | Primary accent |
| `AccentHoverColor` | Hover state |
| `AccentPressedColor` | Pressed state |
| `ButtonBackground` | Button fills |
| `ButtonBorderBrush` | Button borders |
| `ToggleKnobBrush` | Toggle switch knob |
| `ToggleTrackBrush` | Toggle switch track |
| `ScrollBarThumbBrush` | Scrollbar thumb |
| `ShadowEffect` | Drop shadows |

### Subscribing to Theme Changes

```csharp
public class MyViewModel : BaseViewModel, IThemeAware
{
    public void OnThemeChanged(ThemeChangedEventArgs args)
    {
        // React to theme changes
        RefreshColors();
    }
}
```

---

## Tips

1. **Start with a Borg palette** - They're already balanced and tested
2. **Use high contrast** - Ensure text is readable on backgrounds
3. **Test in different views** - Check all major screens before saving
4. **Keep it simple** - The 5-color system exists for a reason
5. **Share your themes** - Export and share with the community

---

*For more information, see the [Architecture Guide](ARCHITECTURE.md).*
