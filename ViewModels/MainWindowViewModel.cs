using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpEditor.Services;
using RdpEditor.Views;

namespace RdpEditor.ViewModels;

/// <summary>
/// Das Fenster: oben die Werkzeugleiste mit Datei und Suche, darunter die
/// Reiter.
///
/// Der Aufbau folgt der Datei, nicht dem Verbindungsdialog von mstsc: Jeder
/// Schluessel, der in der Datei stehen kann, hat hier eine Zeile - und was der
/// Katalog nicht kennt, steht unter "Unbekannt" und in der Rohansicht. Eine
/// .rdp-Datei, die durch diesen Editor gelaufen ist, soll ausser den bewusst
/// geaenderten Zeilen nichts verloren haben.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private RdpDocument _doc = RdpDocument.CreateDefault();
    private readonly List<SettingsPageViewModel> _settingPages = new();

    private MonitorsPageViewModel _monitorPage = null!;
    private RawPageViewModel _rawPage = null!;

    public MainWindowViewModel() : this(null) { }

    public MainWindowViewModel(string? startupFile)
    {
        _isLightTheme = false;
        BuildPages();

        if (!string.IsNullOrWhiteSpace(startupFile) && File.Exists(startupFile))
            LoadFile(startupFile);
        else
            Status = "Neue Datei mit den Voreinstellungen von mstsc. Oben links laesst sich eine "
                   + "vorhandene .rdp-Datei oeffnen.";
    }

    // ============================================================== Zustand

    public ObservableCollection<PageViewModel> Pages { get; } = new();

    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLightTheme;
    [ObservableProperty] private string _signatureWarning = "";
    [ObservableProperty] private bool _hasSignature;

    public string WindowTitle
    {
        get
        {
            var name = FilePath.Length > 0 ? Path.GetFileName(FilePath) : "Neue Verbindung";
            return IsDirty ? $"RDP-Editor - {name} *" : $"RDP-Editor - {name}";
        }
    }

    public string SubTitle => FilePath.Length > 0
        ? FilePath
        : "Noch nicht gespeichert";

    public string ThemeIcon => IsLightTheme ? "☀" : "☽";

    /// <summary>Wie viele Schluessel die Datei enthaelt - die Zahl unten rechts.</summary>
    public string CountText
    {
        get
        {
            var keys = _doc.Lines.Count(l => l.IsSetting);
            var unknown = _doc.Lines.Count(l => l.IsSetting && !RdpCatalog.Knows(l.Key!));
            return unknown > 0
                ? $"{keys} Schluessel, davon {unknown} nicht im Katalog - Kodierung {_doc.SourceEncoding}"
                : $"{keys} Schluessel - Kodierung {_doc.SourceEncoding}";
        }
    }

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(SubTitle));
    }

    partial void OnSearchTextChanged(string value)
    {
        foreach (var page in _settingPages)
            page.ApplyFilter(value);
    }

    // ============================================================== Aufbau

    /// <summary>
    /// Baut die Reiter neu auf. Noetig nach jedem Laden: Welche Zeilen unter
    /// "Unbekannt" stehen, haengt am Inhalt der Datei.
    /// </summary>
    private void BuildPages()
    {
        Pages.Clear();
        _settingPages.Clear();

        var byCategory = new Dictionary<string, SettingsPageViewModel>();

        foreach (var category in RdpCatalog.Categories)
        {
            var page = new SettingsPageViewModel(category);
            byCategory[category] = page;
            _settingPages.Add(page);
        }

        foreach (var definition in RdpCatalog.All)
            if (byCategory.TryGetValue(definition.Category, out var page))
                page.Settings.Add(new SettingViewModel(definition, _doc, OnDocumentChanged));

        // Was in der Datei steht, aber im Katalog fehlt: eigener Reiter, damit
        // niemand raten muss, wo eine Zeile geblieben ist.
        var unknownPage = new SettingsPageViewModel(RdpCatalog.CatUnknown);
        foreach (var line in _doc.Lines.Where(l => l.IsSetting && !RdpCatalog.Knows(l.Key!)))
            unknownPage.Settings.Add(
                new SettingViewModel(RdpCatalog.Unknown(line.Key!, line.Type), _doc, OnDocumentChanged));

        _monitorPage = new MonitorsPageViewModel(_doc, OnDocumentChanged);
        _rawPage = new RawPageViewModel(() => _doc.ToText(), ApplyRawText);

        // Reihenfolge der Reiter: erst die Bildschirme - deswegen gibt es das
        // Programm -, dann die Masken, am Ende die Rohansicht.
        Pages.Add(_monitorPage);
        foreach (var page in _settingPages)
            Pages.Add(page);
        if (unknownPage.Settings.Count > 0)
        {
            _settingPages.Add(unknownPage);
            Pages.Add(unknownPage);
        }
        Pages.Add(_rawPage);

        foreach (var page in _settingPages)
            page.ApplyFilter(SearchText);

        UpdateSignatureWarning();
    }

    /// <summary>
    /// Wird nach jeder Aenderung aufgerufen, gleich aus welchem Reiter: Sie
    /// arbeiten alle auf derselben Datei und muessen einander sehen.
    /// </summary>
    private void OnDocumentChanged()
    {
        IsDirty = true;

        foreach (var page in _settingPages)
            page.RefreshAll();

        _monitorPage.SyncFromDocument();
        _rawPage.Reload();

        OnPropertyChanged(nameof(CountText));
        UpdateSignatureWarning();
    }

    private void UpdateSignatureWarning()
    {
        HasSignature = _doc.Contains("signature") || _doc.Contains("signscope");
        SignatureWarning = HasSignature
            ? "Diese Datei ist signiert. Jede Aenderung macht die Unterschrift ungueltig - mstsc "
            + "zeigt die Verbindung danach als \"unbekannter Herausgeber\" an."
            : "";
    }

    private void ApplyRawText(string text)
    {
        _doc = RdpDocument.Parse(text);
        BuildPages();
        IsDirty = true;
        Status = "Der Text aus der Rohansicht ist uebernommen.";
        OnPropertyChanged(nameof(CountText));
    }

    // ============================================================== Datei

    private void LoadFile(string path)
    {
        try
        {
            _doc = RdpDocument.Load(path);
            FilePath = path;
            IsDirty = false;
            BuildPages();
            Status = $"Geladen: {path}";
            OnPropertyChanged(nameof(CountText));
        }
        catch (Exception ex)
        {
            Status = $"Konnte die Datei nicht lesen: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task New()
    {
        if (!await ConfirmDiscard()) return;

        _doc = RdpDocument.CreateDefault();
        FilePath = "";
        IsDirty = false;
        BuildPages();
        Status = "Neue Datei mit den Voreinstellungen von mstsc.";
        OnPropertyChanged(nameof(CountText));
    }

    [RelayCommand]
    private async Task Open()
    {
        if (!await ConfirmDiscard()) return;

        var window = HostWindow();
        if (window is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "RDP-Datei oeffnen",
            AllowMultiple = false,
            FileTypeFilter = new[] { RdpFileType, FilePickerFileTypes.All },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        LoadFile(path);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (FilePath.Length == 0)
        {
            await SaveAs();
            return;
        }

        WriteFile(FilePath);
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        var window = HostWindow();
        if (window is null) return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "RDP-Datei speichern",
            SuggestedFileName = FilePath.Length > 0 ? Path.GetFileName(FilePath) : "Verbindung.rdp",
            DefaultExtension = "rdp",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { RdpFileType },
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        WriteFile(path);
    }

    private void WriteFile(string path)
    {
        try
        {
            _doc.Save(path);
            FilePath = path;
            IsDirty = false;
            Status = $"Gespeichert: {path}";
        }
        catch (Exception ex)
        {
            Status = $"Konnte nicht speichern: {ex.Message}";
        }
    }

    /// <summary>
    /// Startet mstsc mit der bearbeiteten Datei - die Probe aufs Exempel,
    /// ohne den Umweg ueber den Explorer.
    /// </summary>
    [RelayCommand]
    private async Task StartMstsc()
    {
        if (!OperatingSystem.IsWindows())
        {
            Status = "mstsc gibt es nur unter Windows.";
            return;
        }

        if (FilePath.Length == 0 || IsDirty)
        {
            await Save();
            if (FilePath.Length == 0 || IsDirty) return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("mstsc.exe", $"\"{FilePath}\"") { UseShellExecute = true });
            Status = $"mstsc gestartet mit {Path.GetFileName(FilePath)}.";
        }
        catch (Exception ex)
        {
            Status = $"mstsc liess sich nicht starten: {ex.Message}";
        }
    }

    private static readonly FilePickerFileType RdpFileType = new("Remotedesktopverbindung")
    {
        Patterns = new[] { "*.rdp" },
        MimeTypes = new[] { "application/x-rdp" },
    };

    // ============================================================== Beiwerk

    [RelayCommand]
    private void ToggleTheme()
    {
        IsLightTheme = !IsLightTheme;
        ApplyTheme();
        OnPropertyChanged(nameof(ThemeIcon));
    }

    private void ApplyTheme()
    {
        var app = Avalonia.Application.Current;
        if (app is not null)
            app.RequestedThemeVariant = IsLightTheme
                ? Avalonia.Styling.ThemeVariant.Light
                : Avalonia.Styling.ThemeVariant.Dark;
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    /// <summary>
    /// Fragt vor dem Verwerfen nach. Gibt true zurueck, wenn es weitergehen
    /// darf.
    /// </summary>
    public async Task<bool> ConfirmDiscard()
    {
        if (!IsDirty) return true;

        var window = HostWindow();
        if (window is null) return true;

        return await MessageBox.ShowYesNo(window, "Aenderungen verwerfen?",
            "Die Datei wurde geaendert und ist nicht gespeichert. Trotzdem fortfahren?");
    }

    private static Window? HostWindow()
        => (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
