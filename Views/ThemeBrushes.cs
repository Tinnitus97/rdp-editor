using Avalonia;
using Avalonia.Media;

namespace RdpEditor.Views;

/// <summary>
/// Holt die Farben aus der Farbtafel, damit auch Dialoge, die im Code
/// entstehen, dem Hell-/Dunkelmodus folgen statt feste Werte zu benutzen.
/// </summary>
internal static class ThemeBrushes
{
    public static IBrush Get(string key, string fallbackHex)
    {
        var app = Application.Current;
        if (app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    public static IBrush Base     => Get("BaseBrush", "#1E1E2E");
    public static IBrush Crust    => Get("CrustBrush", "#11111B");
    public static IBrush Surface0 => Get("Surface0Brush", "#313244");
    public static IBrush Text     => Get("TextBrush", "#CDD6F4");
    public static IBrush Blue     => Get("BlueBrush", "#89B4FA");
    public static IBrush Green    => Get("GreenBrush", "#A6E3A1");
}
