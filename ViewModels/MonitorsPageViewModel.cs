using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpEditor.Services;

namespace RdpEditor.ViewModels;

/// <summary>
/// Eine Kachel im Monitorplan - ein Bildschirm, maßstäblich an seiner Stelle.
/// </summary>
public sealed partial class MonitorTileViewModel : ObservableObject
{
    private readonly Action<MonitorTileViewModel> _toggle;
    private readonly Action<MonitorTileViewModel> _makeFirst;

    public MonitorTileViewModel(MonitorEntry monitor,
                                Action<MonitorTileViewModel> toggle,
                                Action<MonitorTileViewModel> makeFirst)
    {
        Monitor = monitor;
        _toggle = toggle;
        _makeFirst = makeFirst;
    }

    public MonitorEntry Monitor { get; }

    public int Id => Monitor.Id;
    public string IdText => Monitor.Id.ToString();
    public string Geometry => Monitor.Geometry;
    public string FriendlyName => Monitor.FriendlyName;

    public string Details => Monitor.IsPrimary
        ? $"{Monitor.Width} x {Monitor.Height} - Windows-Hauptbildschirm - {Monitor.ScalePercent} %"
        : $"{Monitor.Width} x {Monitor.Height} - Lage {Monitor.X},{Monitor.Y} - {Monitor.ScalePercent} %";

    [ObservableProperty] private bool _isSelected;

    /// <summary>Die Stelle in der Auswahl: "1." ist der Hauptbildschirm der Sitzung.</summary>
    [ObservableProperty] private string _orderText = "";

    // Lage der Kachel im Plan, in Bildpunkten des Fensters.
    [ObservableProperty] private double _mapX;
    [ObservableProperty] private double _mapY;
    [ObservableProperty] private double _mapWidth = 40;
    [ObservableProperty] private double _mapHeight = 30;

    /// <summary>Wird vom Klick auf die Kachel aufgerufen.</summary>
    public void Toggle() => _toggle(this);

    [RelayCommand]
    private void MakeFirst() => _makeFirst(this);
}

/// <summary>
/// Der Reiter "Bildschirme" - der Grund, aus dem es dieses Programm gibt.
///
/// mstsc kennt nur die Frage "alle Bildschirme benutzen: ja oder nein". Welche
/// Bildschirme, steht in <c>selectedmonitors</c>, und diese Zeile schreibt
/// mstsc niemals selbst. Wer sie von Hand einfügt, übersieht meist, dass sie
/// ohne <c>use multimon:i:1</c> und <c>screen mode id:i:2</c> wirkungslos ist -
/// beides setzt dieser Reiter mit.
/// </summary>
public sealed partial class MonitorsPageViewModel : PageViewModel
{
    private readonly RdpDocument _doc;
    private readonly Action _changed;

    /// <summary>Die gewählten Nummern in der Reihenfolge der Auswahl.</summary>
    private readonly List<int> _order = new();

    private bool _loading;

    /// <summary>Größe des Monitorplans im Fenster.</summary>
    public const double MapWidth = 640;
    public const double MapHeight = 250;

    public MonitorsPageViewModel(RdpDocument doc, Action changed) : base("Bildschirme")
    {
        _doc = doc;
        _changed = changed;
        Rescan();
    }

    public ObservableCollection<MonitorTileViewModel> Monitors { get; } = new();

    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private string _scanText = "";
    [ObservableProperty] private string _warningText = "";
    [ObservableProperty] private bool _hasWarning;
    [ObservableProperty] private bool _hasMonitors;
    [ObservableProperty] private string _manualText = "";

    // ======================================================== Bildschirme holen

    [RelayCommand]
    public void Rescan()
    {
        var found = MonitorScan.Enumerate();
        var fromWindows = found.Count > 0;

        if (!fromWindows)
        {
            // Rückfall über Avalonia - unter Windows nur, wenn die Aufrufe
            // nichts geliefert haben, sonst immer.
            var window = (Avalonia.Application.Current?.ApplicationLifetime
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            found = MonitorScan.FromAvalonia(window?.Screens);
        }

        Monitors.Clear();
        foreach (var monitor in found)
            Monitors.Add(new MonitorTileViewModel(monitor, Toggle, MakeFirst));

        HasMonitors = Monitors.Count > 0;

        ScanText = Monitors.Count switch
        {
            0 => "Es liess sich kein Bildschirm ermitteln. Die Nummern lassen sich unten von Hand "
               + "eintragen - \"mstsc /l\" zeigt, welche Nummer zu welchem Bildschirm gehört.",
            _ when fromWindows =>
                 $"{Monitors.Count} Bildschirme, in der Reihenfolge, aus der auch mstsc seine "
               + "Nummern nimmt. Zur Probe zeigt \"mstsc /l\" dieselbe Liste.",
            _ => $"{Monitors.Count} Bildschirme, über Avalonia ermittelt. Diese Reihenfolge muss "
               + "nicht die von mstsc sein - bitte mit \"mstsc /l\" vergleichen.",
        };

        Layout();
        SyncFromDocument();
    }

    /// <summary>
    /// Rechnet die Bildschirmkoordinaten in den Plan um: ein Rechteck je
    /// Bildschirm, maßstäblich und an seiner Stelle zueinander.
    /// </summary>
    private void Layout()
    {
        if (Monitors.Count == 0) return;

        const double padding = 10;

        var left = Monitors.Min(m => m.Monitor.X);
        var top = Monitors.Min(m => m.Monitor.Y);
        var right = Monitors.Max(m => m.Monitor.Right);
        var bottom = Monitors.Max(m => m.Monitor.Bottom);

        var width = Math.Max(1, right - left);
        var height = Math.Max(1, bottom - top);

        var scale = Math.Min((MapWidth - 2 * padding) / width, (MapHeight - 2 * padding) / height);

        var offsetX = padding + (MapWidth - 2 * padding - width * scale) / 2;
        var offsetY = padding + (MapHeight - 2 * padding - height * scale) / 2;

        foreach (var tile in Monitors)
        {
            tile.MapX = offsetX + (tile.Monitor.X - left) * scale;
            tile.MapY = offsetY + (tile.Monitor.Y - top) * scale;

            // Zwei Bildpunkte Luft, damit benachbarte Kacheln nicht zu einer
            // Fläche verschmelzen.
            tile.MapWidth = Math.Max(30, tile.Monitor.Width * scale - 2);
            tile.MapHeight = Math.Max(24, tile.Monitor.Height * scale - 2);
        }
    }

    // ======================================================== Auswahl

    /// <summary>Liest die Auswahl aus der Datei - beim Laden und nach der Rohansicht.</summary>
    public void SyncFromDocument()
    {
        _loading = true;
        try
        {
            _order.Clear();

            var selected = MonitorSelection.Parse(_doc.Get(MonitorSelection.KeySelected));
            if (selected.Count > 0)
            {
                _order.AddRange(selected);
            }
            else if (_doc.GetInt(MonitorSelection.KeyMultimon, 0) == 1)
            {
                // "Alle Bildschirme" - ohne selectedmonitors nimmt die Sitzung
                // jeden, den sie findet.
                _order.AddRange(Monitors.Select(m => m.Id));
            }

            ManualText = MonitorSelection.Format(_order);
            UpdateTiles();
        }
        finally
        {
            _loading = false;
        }
    }

    private void Toggle(MonitorTileViewModel tile)
    {
        if (_order.Contains(tile.Id)) _order.Remove(tile.Id);
        else _order.Add(tile.Id);

        Apply();
    }

    private void MakeFirst(MonitorTileViewModel tile)
    {
        if (!_order.Remove(tile.Id)) return;
        _order.Insert(0, tile.Id);
        Apply();
    }

    [RelayCommand]
    private void SelectAll()
    {
        _order.Clear();
        _order.AddRange(Monitors.Select(m => m.Id));
        Apply();
    }

    [RelayCommand]
    private void SelectNone()
    {
        _order.Clear();
        Apply();
    }

    [RelayCommand]
    private void SelectPrimary()
    {
        _order.Clear();
        var primary = Monitors.FirstOrDefault(m => m.Monitor.IsPrimary) ?? Monitors.FirstOrDefault();
        if (primary is not null) _order.Add(primary.Id);
        Apply();
    }

    /// <summary>Übernimmt die von Hand eingetippten Nummern.</summary>
    [RelayCommand]
    private void ApplyManual()
    {
        _order.Clear();
        _order.AddRange(MonitorSelection.Parse(ManualText));
        Apply();
    }

    /// <summary>Zeigt die Liste an, die Windows selbst kennt - zum Abgleich der Nummern.</summary>
    [RelayCommand]
    private void ShowWindowsList()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            Process.Start(new ProcessStartInfo("mstsc.exe", "/l") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WarningText = $"\"mstsc /l\" liess sich nicht starten: {ex.Message}";
            HasWarning = true;
        }
    }

    /// <summary>Schreibt die Auswahl in die Datei und sagt darunter, was daraus folgt.</summary>
    private void Apply()
    {
        if (_loading) return;

        MonitorSelection.Apply(_doc, _order, Monitors.Count);
        ManualText = MonitorSelection.Format(_order);
        UpdateTiles();
        _changed();
    }

    private void UpdateTiles()
    {
        foreach (var tile in Monitors)
        {
            var index = _order.IndexOf(tile.Id);
            tile.IsSelected = index >= 0;
            tile.OrderText = index switch
            {
                < 0 => "",
                0 => "1. - Hauptbildschirm der Sitzung",
                _ => $"{index + 1}.",
            };
        }

        ResultText = MonitorSelection.Describe(_order, Monitors.Count);

        var chosen = _order
            .Select(id => Monitors.FirstOrDefault(m => m.Id == id)?.Monitor)
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        var unknown = _order.Where(id => Monitors.All(m => m.Id != id)).ToList();

        if (unknown.Count > 0 && Monitors.Count > 0)
        {
            WarningText = $"Bildschirm {MonitorSelection.Format(unknown)} ist an diesem Rechner "
                        + "nicht angeschlossen. Auf dem Rechner, der die Datei benutzt, mag es ihn "
                        + "geben - hier lässt sich die Auswahl nicht prüfen.";
            HasWarning = true;
        }
        else if (chosen.Count > 1 && !MonitorSelection.IsContiguous(chosen))
        {
            WarningText = "Die gewählten Bildschirme hängen nicht zusammen. mstsc verlangt eine "
                        + "zusammenhängende Fläche und fällt sonst wortlos auf alle Bildschirme "
                        + "zurück - die Datei bliebe gültig, nur täte sie nicht, was darin steht.";
            HasWarning = true;
        }
        else
        {
            WarningText = "";
            HasWarning = false;
        }
    }
}
