using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using RdpEditor.Services;

namespace RdpEditor.ViewModels;

/// <summary>
/// Eine Zeile im Fenster: eine Einstellung der Datei.
///
/// Der Haken rechts ist der eigentliche Unterschied zu einem Einstellungs-
/// dialog: Er sagt, ob der Schluessel ueberhaupt in der Datei steht. Das ist
/// nicht dasselbe wie "aus" - fehlt er, entscheidet mstsc, und je nach
/// Windows-Fassung faellt diese Entscheidung anders aus. Wer eine Einstellung
/// festnageln will, muss sie hineinschreiben, auch wenn ihr Wert der
/// Voreinstellung entspricht.
/// </summary>
public sealed partial class SettingViewModel : ObservableObject
{
    private readonly RdpDocument _doc;
    private readonly Action _changed;

    /// <summary>Waehrend des Einlesens aus der Datei nicht zurueckschreiben.</summary>
    private bool _loading;

    public SettingViewModel(RdpSetting definition, RdpDocument doc, Action changed)
    {
        Definition = definition;
        _doc = doc;
        _changed = changed;

        if (definition.Choices is not null)
            foreach (var choice in definition.Choices)
                Choices.Add(choice);

        Refresh();
    }

    public RdpSetting Definition { get; }

    public string Key => Definition.Key;
    public string Label => Definition.Label;
    public string Hint => Definition.Hint;

    /// <summary>Der Schluessel so, wie er in der Datei steht - klein und einfarbig unter der Beschriftung.</summary>
    public string KeyLine => $"{Definition.Key}:{Definition.Type}:";

    public bool IsToggle => Definition.Kind == RdpEditorKind.Toggle;
    public bool IsChoice => Definition.Kind == RdpEditorKind.Choice;
    public bool IsNumber => Definition.Kind == RdpEditorKind.Number;
    public bool IsText   => Definition.Kind == RdpEditorKind.Text;
    public bool IsUnknown => Definition.Category == RdpCatalog.CatUnknown;

    public ObservableCollection<RdpChoice> Choices { get; } = new();

    [ObservableProperty] private bool _isPresent;
    [ObservableProperty] private string _textValue = "";
    [ObservableProperty] private bool _boolValue;
    [ObservableProperty] private RdpChoice? _selectedChoice;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _hasError;

    // ======================================================== Lesen und Schreiben

    /// <summary>Holt den Wert erneut aus der Datei - nach dem Laden und nach jeder Aenderung anderswo.</summary>
    public void Refresh()
    {
        _loading = true;
        try
        {
            var line = _doc.Find(Key);
            IsPresent = line is not null;

            var raw = line?.Value ?? "";
            TextValue = raw;
            BoolValue = raw.Trim() == "1";
            HasError = IsNumber && raw.Length > 0
                    && !int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

            if (IsChoice)
            {
                var match = Choices.FirstOrDefault(c => c.Value == raw.Trim());

                // Ein Wert, den der Katalog nicht vorsieht - etwa eine Farbtiefe
                // von 24 in einer alten Datei. Er kommt in die Liste, statt beim
                // naechsten Speichern still auf den ersten Eintrag zu fallen.
                if (match is null && raw.Trim().Length > 0)
                {
                    match = new RdpChoice(raw.Trim(), $"{raw.Trim()} (unbekannter Wert)");
                    Choices.Add(match);
                }

                SelectedChoice = match;
            }
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(StateText));
    }

    /// <summary>Steht der Schluessel in der Datei, oder ueberlaesst die Datei ihn mstsc?</summary>
    public string StateText => IsPresent ? "steht in der Datei" : "nicht gesetzt";

    partial void OnIsPresentChanged(bool value)
    {
        if (_loading) return;

        if (value)
        {
            Write();
            return;
        }

        _doc.Remove(Key);
        OnPropertyChanged(nameof(StateText));
        _changed();
    }

    partial void OnTextValueChanged(string value)
    {
        if (_loading || IsToggle || IsChoice) return;
        Write();
    }

    partial void OnBoolValueChanged(bool value)
    {
        if (_loading || !IsToggle) return;
        Write();
    }

    partial void OnSelectedChoiceChanged(RdpChoice? value)
    {
        if (_loading || !IsChoice || value is null) return;
        Write();
    }

    /// <summary>
    /// Schreibt den Wert in die Datei. Wer an einer Zeile etwas aendert, will
    /// sie in der Datei haben - der Haken setzt sich deshalb von selbst.
    /// </summary>
    private void Write()
    {
        var value = Definition.Kind switch
        {
            RdpEditorKind.Toggle => BoolValue ? "1" : "0",
            RdpEditorKind.Choice => SelectedChoice?.Value ?? "",
            _ => TextValue ?? "",
        };

        HasError = IsNumber && value.Length > 0
                && !int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        _doc.Set(Key, Definition.Type, value);

        if (!IsPresent)
        {
            _loading = true;
            IsPresent = true;
            _loading = false;
        }

        OnPropertyChanged(nameof(StateText));
        _changed();
    }

    // ======================================================== Suche

    /// <summary>Passt die Zeile zum Suchbegriff? Gesucht wird im Schluessel, in der Beschriftung und im Hinweis.</summary>
    public bool Matches(string needle)
        => Key.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || Hint.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || TextValue.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
