using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace RdpEditor.Services;

/// <summary>
/// Liest und schreibt den Richtlinienwert, der über TCP oder UDP entscheidet.
///
/// Getrennt von UdpTransport, damit dort nur Text steht und die Prüfungen
/// ohne Windows auskommen.
/// </summary>
public static class UdpTransportRegistry
{
    /// <summary>
    /// Die drei Orte, an denen der Wert stehen kann - in der Reihenfolge,
    /// in der Windows sie gewichtet.
    /// </summary>
    public static IReadOnlyList<TransportState> Read()
    {
        var list = new List<TransportState>
        {
            new("Richtlinie für alle Benutzer", $@"HKLM\{UdpTransport.PolicyPath}",
                ReadValue(RegistryHive.LocalMachine, UdpTransport.PolicyPath), NeedsAdmin: true),
            new("Richtlinie für diesen Benutzer", $@"HKCU\{UdpTransport.PolicyPath}",
                ReadValue(RegistryHive.CurrentUser, UdpTransport.PolicyPath), NeedsAdmin: false),
            new("Einstellung des Clients", $@"HKLM\{UdpTransport.ClientPath}",
                ReadValue(RegistryHive.LocalMachine, UdpTransport.ClientPath), NeedsAdmin: true),
        };

        return list;
    }

    private static int? ReadValue(RegistryHive hive, string path)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            return ReadWindows(hive, path);
        }
        catch
        {
            // Kein Zugriff, kein Schlüssel, kein Windows - alles dasselbe
            // Ergebnis: Es steht nichts da, was uns etwas sagt.
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static int? ReadWindows(RegistryHive hive, string path)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.OpenSubKey(path);
        return key?.GetValue(UdpTransport.ValueName) as int?;
    }

    /// <summary>
    /// Setzt oder entfernt den Wert. Läuft über reg.exe mit Rechteanhebung:
    /// Der Editor selbst startet ohne Administratorrechte, und für das Ändern
    /// einer Textdatei soll er sie auch nicht bekommen - nur dieser eine
    /// Schritt fragt danach, und zwar sichtbar.
    /// </summary>
    public static Process? Write(int? value)
    {
        var info = new ProcessStartInfo("reg.exe", UdpTransport.RegArguments(value))
        {
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        return Process.Start(info);
    }
}
