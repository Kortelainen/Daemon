using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Daemon;

public static class AccentTheme
{
    public static readonly (string Name, Color Color)[] Presets =
    [
        ("Green",      Color.FromRgb(0x66, 0xFF, 0x99)),
        ("White",      Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("Light blue", Color.FromRgb(0x88, 0xCC, 0xFF)),
        ("Cyan",       Color.FromRgb(0x00, 0xFF, 0xFF)),
        ("Yellow",     Color.FromRgb(0xFF, 0xFF, 0x66)),
        ("Orange",     Color.FromRgb(0xFF, 0xAA, 0x44)),
    ];

    public static Color Default => Presets[0].Color;
}
