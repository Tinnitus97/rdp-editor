using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpEditor.Services;
using RdpEditor.Views;

namespace RdpEditor.ViewModels;

/// <summary>
/// Der Reiter "Transport": TCP oder UDP.
///
/// Er gehört als einziger nicht zur Datei - und genau das ist seine Auskunft.
/// Es gibt keinen .rdp-Schlüssel für den Transport; wer danach sucht, sucht
/// lange. Entschieden wird es zwischen Client und Server: Der Client bietet
/// UDP an, solange es ihm nicht verboten ist, der Server nimmt es an, solange
/// seine Richtlinie es zulässt.
///
/// Verboten wird es hier - am Rechner, für alle Verbindungen. Deshalb steht
/// die Warnung darüber und nicht im Kleingedruckten.
/// </summary>
public sealed partial class TransportPageViewModel : PageViewModel
{
    public TransportPageViewModel() : base("Transport")
    {
        Rescan();
    }

    public ObservableCollection<TransportState> States { get; } = new();

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _status = "";

    public bool IsWindows => OperatingSystem.IsWindows();

    public string Explanation =>
        "Eine .rdp-Datei kann den Transport nicht festlegen - es gibt keinen Schlüssel dafür. "
      + "mstsc versucht von sich aus UDP und fällt auf TCP zurück, wenn es nicht durchkommt. "
      + "Abschalten lässt sich UDP nur am Rechner: beim Client über den Richtlinienwert "
      + "\"fClientDisableUDP\", beim Server über die Gruppenrichtlinie "
      + "\"RDP-Transportprotokolle auswählen\". Der Server-Teil gehört dem Server - hier steht "
      + "die Seite des Clients.";

    public string Warning =>
        "Diese Schaltflächen ändern den RECHNER, nicht die geöffnete Datei: Sie gelten für jede "
      + "Remotedesktopverbindung dieses Rechners. Windows fragt dafür nach Administratorrechten, "
      + "und mstsc muss danach neu gestartet werden.";

    [RelayCommand]
    private void Rescan()
    {
        States.Clear();
        foreach (var state in UdpTransportRegistry.Read())
            States.Add(state);

        Summary = OperatingSystem.IsWindows()
            ? UdpTransport.Describe(States.ToList())
            : "Der Zustand lässt sich nur unter Windows ablesen.";
    }

    [RelayCommand]
    private Task DisableUdp() => Set(1,
        "UDP abschalten und nur noch TCP zulassen?\n\n"
      + "Das gilt für alle Remotedesktopverbindungen dieses Rechners. Es hilft gegen "
      + "einfrierende Sitzungen in unruhigen Netzen und kostet die Vorteile von UDP bei "
      + "Video und hoher Verzögerung.");

    [RelayCommand]
    private Task EnableUdp() => Set(0,
        "UDP ausdrücklich erlauben?\n\n"
      + "Das setzt den Richtlinienwert auf 0. Ob UDP zustande kommt, entscheidet danach der "
      + "Server und die Leitung dazwischen.");

    [RelayCommand]
    private Task RemoveSetting() => Set(null,
        "Den Eintrag ganz entfernen?\n\n"
      + "Danach entscheidet Windows wie ab Werk: erst UDP versuchen, sonst TCP.");

    /// <summary>
    /// Fragt nach, ruft reg.exe mit Rechteanhebung auf und liest danach neu
    /// ein. Bricht der Benutzer die Abfrage von Windows ab, steht das in der
    /// Statuszeile - und in der Registrierung nichts.
    /// </summary>
    private async Task Set(int? value, string question)
    {
        if (!OperatingSystem.IsWindows())
        {
            Status = "Das geht nur unter Windows.";
            return;
        }

        var window = AppWindow.Current;
        if (window is null) return;

        if (!await MessageBox.ShowYesNo(window, "Transport ändern", question)) return;

        try
        {
            var process = UdpTransportRegistry.Write(value);
            if (process is not null)
                await process.WaitForExitAsync();

            Rescan();
            Status = value switch
            {
                1 => "UDP ist abgeschaltet. mstsc neu starten, damit es greift.",
                0 => "UDP ist erlaubt. mstsc neu starten, damit es greift.",
                _ => "Der Eintrag ist entfernt. mstsc neu starten, damit es greift.",
            };
        }
        catch (Win32Exception ex)
        {
            // 1223: Der Benutzer hat die Abfrage von Windows abgebrochen.
            Status = ex.NativeErrorCode == 1223
                ? "Abgebrochen - es wurde nichts geändert."
                : $"Ließ sich nicht ändern: {ex.Message}";
        }
        catch (Exception ex)
        {
            Status = $"Ließ sich nicht ändern: {ex.Message}";
        }
    }

    /// <summary>
    /// Schreibt dieselbe Änderung als .reg-Datei - für Rechner, auf denen
    /// dieses Programm nicht liegt, und für die Verteilung im Netz.
    /// </summary>
    [RelayCommand]
    private async Task SaveRegFile()
    {
        var window = AppWindow.Current;
        if (window is null) return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Registrierungsdatei speichern",
            SuggestedFileName = "RDP-nur-TCP.reg",
            DefaultExtension = "reg",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Registrierungsdatei") { Patterns = new[] { "*.reg" } },
            },
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            System.IO.File.WriteAllText(path, UdpTransport.RegFileContent(1),
                                        new System.Text.UnicodeEncoding(false, true));
            Status = $"Gespeichert: {path}";
        }
        catch (Exception ex)
        {
            Status = $"Konnte nicht speichern: {ex.Message}";
        }
    }
}
