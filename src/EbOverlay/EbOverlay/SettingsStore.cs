using System.IO;
using System.Text.Json;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace EbOverlay;

public sealed class SettingsStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EbOverlay", "settings.json");

    private sealed class Data
    {
        public string  AccentColor          { get; set; } = "#66FF99";
        public bool    FullscreenHideEnabled { get; set; } = false;
        public bool    SpriteVisible         { get; set; } = true;
    }

    private Data _data = new();

    public Color  AccentColor          => ParseColor(_data.AccentColor);
    public bool   FullscreenHideEnabled { get => _data.FullscreenHideEnabled; set { _data.FullscreenHideEnabled = value; Save(); } }
    public bool   SpriteVisible         { get => _data.SpriteVisible;         set { _data.SpriteVisible = value;         Save(); } }

    public void SetAccentColor(Color c)
    {
        _data.AccentColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        Save();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(Path))
                _data = JsonSerializer.Deserialize<Data>(File.ReadAllText(Path)) ?? new Data();
        }
        catch { _data = new Data(); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        catch { return AccentTheme.Default; }
    }
}
