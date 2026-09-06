using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace RdpEditor.Views;

/// <summary>
/// Das Hauptfenster, von dort aus, wo kein Fenster zur Hand ist - für
/// Dateiauswahl und Meldungsboxen aus einem ViewModel heraus.
/// </summary>
internal static class AppWindow
{
    public static Window? Current
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
