using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;

namespace RdpEditor;

internal static class Program
{
    /// <summary>
    /// Die Datei, die beim Start mitgegeben wurde - erster Parameter der
    /// Befehlszeile. Damit laesst sich der Editor als "Oeffnen mit" fuer
    /// .rdp-Dateien eintragen.
    /// </summary>
    public static string? StartupFile { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith('-') || arg.StartsWith('/')) continue;
            if (!File.Exists(arg)) continue;
            StartupFile = Path.GetFullPath(arg);
            break;
        }

        // Globale Absturz-Faenger: schreiben jede unbehandelte Ausnahme in eine
        // Logdatei, statt die Anwendung wortlos zu beenden.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog("Main", ex);
            throw;
        }
    }

    // Wird auch vom Oberflaechen-Entwurf benutzt - nicht entfernen.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Schreibt einen Absturzbericht nach %TEMP%\RdpEditor_Absturz.log.
    /// </summary>
    public static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine($"Zeit:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Quelle:  {source}");
            sb.AppendLine($"Typ:     {ex?.GetType().FullName ?? "(unbekannt)"}");
            sb.AppendLine($"Meldung: {ex?.Message}");
            sb.AppendLine("StackTrace:");
            sb.AppendLine(ex?.StackTrace ?? "(keiner)");
            if (ex?.InnerException is { } inner)
            {
                sb.AppendLine("--- InnerException ---");
                sb.AppendLine($"Typ:     {inner.GetType().FullName}");
                sb.AppendLine($"Meldung: {inner.Message}");
                sb.AppendLine(inner.StackTrace);
            }
            sb.AppendLine();

            File.AppendAllText(Path.Combine(Path.GetTempPath(), "RdpEditor_Absturz.log"),
                               sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Wenn selbst das Schreiben scheitert, gibt es nichts mehr zu tun.
        }
    }
}
