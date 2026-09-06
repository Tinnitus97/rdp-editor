using System;
using System.Collections.Generic;
using System.Text;

namespace RdpEditor.Services;

/// <summary>Wo eine Einstellung steht und was dort gefunden wurde.</summary>
public sealed record TransportState(string Scope, string Path, int? Value, bool NeedsAdmin)
{
    public string ValueText => Value switch
    {
        null => "nicht gesetzt",
        0 => "0 - UDP erlaubt",
        1 => "1 - UDP abgeschaltet, nur TCP",
        _ => $"{Value} (unerwarteter Wert)",
    };
}

/// <summary>
/// TCP oder UDP - und warum das nicht in der .rdp-Datei steht.
///
/// Es gibt keinen Schlüssel dafür. Der Transport ist keine Eigenschaft der
/// Verbindung, sondern eine Entscheidung zwischen Client und Server: Der
/// Client bietet UDP an, wenn es ihm nicht verboten ist, der Server nimmt es
/// an, wenn seine Richtlinie es zulässt. Verboten wird es beim Client über
/// den Richtlinienwert "fClientDisableUDP", beim Server über die
/// Gruppenrichtlinie "RDP-Transportprotokolle auswählen".
///
/// Deshalb kann dieser Reiter nicht die Datei ändern, sondern nur den
/// Rechner - und sagt das auch.
///
/// Diese Klasse ist der Teil ohne Windows-Aufrufe: Texte und die .reg-Datei.
/// Das Lesen und Schreiben der Registrierung steht in UdpTransportRegistry.
/// </summary>
public static class UdpTransport
{
    /// <summary>Die Richtlinie. Das ist der Ort, den auch die Gruppenrichtlinie beschreibt.</summary>
    public const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services\Client";

    /// <summary>Die Einstellung des Clients selbst - ohne Richtlinie, aber wirksam.</summary>
    public const string ClientPath = @"SOFTWARE\Microsoft\Terminal Server Client";

    public const string ValueName = "fClientDisableUDP";

    /// <summary>
    /// Der Text unter der Zustandsliste: was aus den gefundenen Werten folgt.
    ///
    /// Gewinnt die Richtlinie unter HKLM, ist alles andere gleichgültig -
    /// deshalb wird sie zuerst gefragt.
    /// </summary>
    public static string Describe(IReadOnlyList<TransportState> states)
    {
        foreach (var state in states)
        {
            if (state.Value is null) continue;

            return state.Value == 1
                ? $"UDP ist abgeschaltet ({state.Scope}). mstsc verbindet sich auf diesem Rechner "
                + "ausschließlich über TCP."
                : $"UDP ist ausdrücklich erlaubt ({state.Scope}). Ob es zustande kommt, entscheidet "
                + "zusätzlich der Server und die Leitung dazwischen.";
        }

        return "Nichts gesetzt - Windows entscheidet. In der Voreinstellung versucht mstsc UDP und "
             + "fällt auf TCP zurück, wenn es nicht durchkommt.";
    }

    /// <summary>
    /// Der Inhalt einer .reg-Datei für denselben Zweck - zum Weitergeben an
    /// Rechner, auf denen dieses Programm nicht liegt.
    /// </summary>
    public static string RegFileContent(int? value)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine("; RDP-Transport des Clients.");
        sb.AppendLine("; 1 = UDP abgeschaltet (nur TCP), 0 = UDP erlaubt.");
        sb.AppendLine("; Nach dem Einspielen mstsc neu starten.");
        sb.AppendLine();
        sb.AppendLine($@"[HKEY_LOCAL_MACHINE\{PolicyPath}]");
        sb.AppendLine(value is null
            ? $"\"{ValueName}\"=-"
            : $"\"{ValueName}\"=dword:{value.Value:x8}");
        return sb.ToString();
    }

    /// <summary>Die Befehlszeile für reg.exe, die den Wert setzt oder entfernt.</summary>
    public static string RegArguments(int? value)
        => value is null
            ? $"delete \"HKLM\\{PolicyPath}\" /v {ValueName} /f"
            : $"add \"HKLM\\{PolicyPath}\" /v {ValueName} /t REG_DWORD /d {value.Value} /f";
}
