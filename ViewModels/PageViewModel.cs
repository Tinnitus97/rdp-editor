using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpEditor.Services;

namespace RdpEditor.ViewModels;

/// <summary>Ein Reiter des Fensters.</summary>
public abstract partial class PageViewModel : ObservableObject
{
    protected PageViewModel(string header) => Header = header;

    public string Header { get; }
}

/// <summary>
/// Ein Reiter voller Einstellungen - alles ausser den beiden Sonderreitern
/// "Bildschirme" und "Rohansicht".
/// </summary>
public sealed partial class SettingsPageViewModel : PageViewModel
{
    public SettingsPageViewModel(string header) : base(header) { }

    public ObservableCollection<SettingViewModel> Settings { get; } = new();

    [ObservableProperty] private bool _isEmpty;

    /// <summary>
    /// Blendet aus, was nicht zum Suchbegriff passt. Die Zeilen bleiben stehen,
    /// nur unsichtbar - so behaelt jeder Reiter seine Reihenfolge, sobald das
    /// Suchfeld wieder leer ist.
    /// </summary>
    public void ApplyFilter(string? needle)
    {
        var text = needle?.Trim() ?? "";

        foreach (var setting in Settings)
            setting.IsVisible = text.Length == 0 || setting.Matches(text);

        IsEmpty = Settings.Count == 0 || Settings.All(s => !s.IsVisible);
    }

    /// <summary>Nach einer Aenderung anderswo - etwa im Reiter "Bildschirme" - die Werte neu einlesen.</summary>
    public void RefreshAll()
    {
        foreach (var setting in Settings)
            setting.Refresh();
    }
}

/// <summary>
/// Die Rohansicht: die Datei als Text, so wie sie gespeichert wird.
///
/// Sie ist die Notausgabe fuer alles, was die Masken nicht abdecken - und die
/// Probe darauf, dass der Editor nichts verschluckt hat. Was hier steht, steht
/// danach in der Datei.
/// </summary>
public sealed partial class RawPageViewModel : PageViewModel
{
    private readonly Action<string> _apply;
    private readonly Func<string> _current;

    public RawPageViewModel(Func<string> current, Action<string> apply) : base("Rohansicht")
    {
        _current = current;
        _apply = apply;
        _rawText = current();
    }

    [ObservableProperty] private string _rawText = "";
    [ObservableProperty] private string _status = "";

    /// <summary>Uebernimmt den Text als neuen Inhalt der Datei.</summary>
    [RelayCommand]
    private void Apply()
    {
        _apply(RawText);
        Status = "Uebernommen.";
    }

    /// <summary>Verwirft die Aenderungen im Textfeld und holt den Stand der Datei zurueck.</summary>
    [RelayCommand]
    private void Revert()
    {
        RawText = _current();
        Status = "Zurueckgesetzt.";
    }

    /// <summary>Wird nach jeder Aenderung in den anderen Reitern aufgerufen.</summary>
    public void Reload()
    {
        RawText = _current();
        Status = "";
    }
}
