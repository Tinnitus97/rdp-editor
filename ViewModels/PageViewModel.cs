using System;
using System.Collections.Generic;
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

/// <summary>Ein Knopf, der einen ganzen Satz Werte auf einmal setzt.</summary>
public sealed partial class PresetViewModel : ObservableObject
{
    private readonly RdpPreset _preset;
    private readonly Action<RdpPreset> _apply;

    public PresetViewModel(RdpPreset preset, Action<RdpPreset> apply)
    {
        _preset = preset;
        _apply = apply;
    }

    public string Title => _preset.Title;

    /// <summary>Der Hinweis samt der Zeilen, die der Knopf schreibt - als Tipp am Mauszeiger.</summary>
    public string Tip => _preset.Hint + "\n\n"
        + string.Join("\n", _preset.Values.Select(v => $"{v.Key}:{v.Value}"));

    [RelayCommand]
    private void Apply() => _apply(_preset);
}

/// <summary>
/// Eine Gruppe zusammengehörender Zeilen innerhalb eines Reiters - eine Karte
/// mit Überschrift, wie die Kästen im Dialog von mstsc.
/// </summary>
public sealed partial class SettingGroupViewModel : ObservableObject
{
    public SettingGroupViewModel(string title, string hint)
    {
        Title = title;
        Hint = hint;
    }

    public string Title { get; }
    public string Hint { get; }
    public bool HasHint => Hint.Length > 0;

    public ObservableCollection<SettingViewModel> Settings { get; } = new();
    public ObservableCollection<PresetViewModel> Presets { get; } = new();

    public bool HasPresets => Presets.Count > 0;

    /// <summary>Blendet die ganze Gruppe aus, wenn die Suche in ihr nichts findet.</summary>
    [ObservableProperty] private bool _isVisible = true;
}

/// <summary>
/// Ein Reiter voller Einstellungen - alles außer den Sonderreitern
/// "Bildschirme", "Transport" und "Rohansicht".
/// </summary>
public sealed partial class SettingsPageViewModel : PageViewModel
{
    public SettingsPageViewModel(string header) : base(header) { }

    public ObservableCollection<SettingGroupViewModel> Groups { get; } = new();

    public IEnumerable<SettingViewModel> AllSettings => Groups.SelectMany(g => g.Settings);

    [ObservableProperty] private bool _isEmpty;

    /// <summary>
    /// Blendet aus, was nicht zum Suchbegriff passt. Die Zeilen bleiben stehen,
    /// nur unsichtbar - so behält jeder Reiter seine Reihenfolge, sobald das
    /// Suchfeld wieder leer ist. Eine Gruppe ohne sichtbare Zeile verschwindet
    /// samt Überschrift; sonst stünden lauter leere Kästen im Fenster.
    /// </summary>
    public void ApplyFilter(string? needle)
    {
        var text = needle?.Trim() ?? "";

        foreach (var group in Groups)
        {
            var sichtbar = false;
            foreach (var setting in group.Settings)
            {
                setting.IsVisible = text.Length == 0 || setting.Matches(text);
                sichtbar |= setting.IsVisible;
            }
            group.IsVisible = sichtbar;
        }

        IsEmpty = Groups.All(g => !g.IsVisible);
    }

    /// <summary>Nach einer Änderung anderswo - etwa im Reiter "Bildschirme" - die Werte neu einlesen.</summary>
    public void RefreshAll()
    {
        foreach (var setting in AllSettings)
            setting.Refresh();
    }
}

/// <summary>
/// Die Rohansicht: die Datei als Text, so wie sie gespeichert wird.
///
/// Sie ist die Notausgabe für alles, was die Masken nicht abdecken - und die
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

    /// <summary>Übernimmt den Text als neuen Inhalt der Datei.</summary>
    [RelayCommand]
    private void Apply()
    {
        _apply(RawText);
        Status = "Übernommen.";
    }

    /// <summary>Verwirft die Änderungen im Textfeld und holt den Stand der Datei zurück.</summary>
    [RelayCommand]
    private void Revert()
    {
        RawText = _current();
        Status = "Zurückgesetzt.";
    }

    /// <summary>Wird nach jeder Änderung in den anderen Reitern aufgerufen.</summary>
    public void Reload()
    {
        RawText = _current();
        Status = "";
    }
}
