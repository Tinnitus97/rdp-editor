using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RdpEditor.Services;

/// <summary>
/// Fragt Windows nach den angeschlossenen Bildschirmen.
///
/// Warum nicht über Avalonia: Die Nummer, die in <c>selectedmonitors</c>
/// gehört, ist die Stelle in der Reihenfolge von <c>EnumDisplayMonitors</c> -
/// derselben Reihenfolge, aus der auch mstsc seine Nummern nimmt. Eine
/// Bildschirmliste aus einem anderen Rahmenwerk kann dieselben Geräte in
/// anderer Reihenfolge liefern, und dann wählt man am Ende den falschen.
///
/// Die Avalonia-Liste steht trotzdem als Rückfall bereit - für den Fall,
/// dass die Windows-Aufrufe nichts liefern, und damit sich das Fenster auch
/// auf einem Linux-Rechner ansehen lässt.
/// </summary>
public static class MonitorScan
{
    public static IReadOnlyList<MonitorEntry> Enumerate()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<MonitorEntry>();

        try
        {
            return EnumerateWindows();
        }
        catch
        {
            // Kein Grund abzustürzen: Ohne Liste bleibt der Monitorplan leer,
            // die Nummern lassen sich weiterhin von Hand eintragen.
            return Array.Empty<MonitorEntry>();
        }
    }

    /// <summary>
    /// Rückfall über Avalonia. Die Reihenfolge muss nicht die von mstsc
    /// sein - deshalb sagt der Monitorplan in diesem Fall dazu, dass die
    /// Nummern gegen "mstsc /l" zu prüfen sind.
    /// </summary>
    public static IReadOnlyList<MonitorEntry> FromAvalonia(Avalonia.Controls.Screens? screens)
    {
        var list = new List<MonitorEntry>();
        if (screens is null) return list;

        var id = 0;
        foreach (var screen in screens.All)
        {
            var bounds = screen.Bounds;
            list.Add(new MonitorEntry(
                Id: id,
                DeviceName: $"Screen{id + 1}",
                FriendlyName: screen.IsPrimary ? "Hauptbildschirm" : $"Bildschirm {id + 1}",
                X: bounds.X,
                Y: bounds.Y,
                Width: bounds.Width,
                Height: bounds.Height,
                IsPrimary: screen.IsPrimary,
                Dpi: (int)Math.Round(screen.Scaling * 96)));
            id++;
        }

        return list;
    }

    // ======================================================== Windows-Aufrufe

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<MonitorEntry> EnumerateWindows()
    {
        var handles = new List<IntPtr>();

        // Der Delegat muss den Aufruf überleben - als lokale Variable tut er
        // das, als Ausdruck im Aufruf nicht zwingend.
        MonitorEnumProc callback = (IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
        {
            handles.Add(monitor);
            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        var list = new List<MonitorEntry>();

        for (var id = 0; id < handles.Count; id++)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(handles[id], ref info)) continue;

            var device = info.szDevice ?? $"\\\\.\\DISPLAY{id + 1}";

            list.Add(new MonitorEntry(
                Id: id,
                DeviceName: device,
                FriendlyName: FriendlyName(device, id),
                X: info.rcMonitor.Left,
                Y: info.rcMonitor.Top,
                Width: info.rcMonitor.Right - info.rcMonitor.Left,
                Height: info.rcMonitor.Bottom - info.rcMonitor.Top,
                IsPrimary: (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                Dpi: EffectiveDpi(handles[id])));
        }

        return list;
    }

    /// <summary>Der Name, den Windows selbst anzeigt - meist "Generic PnP Monitor".</summary>
    [SupportedOSPlatform("windows")]
    private static string FriendlyName(string device, int id)
    {
        try
        {
            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (EnumDisplayDevices(device, 0, ref dd, 0) && !string.IsNullOrWhiteSpace(dd.DeviceString))
                return dd.DeviceString;
        }
        catch
        {
            // Der Name ist Beiwerk - ohne ihn bleibt die Nummer.
        }

        return $"Bildschirm {id + 1}";
    }

    /// <summary>
    /// Die tatsächliche Punktdichte des Bildschirms. Gibt es die Funktion
    /// nicht (Windows vor 8.1), bleibt es bei 96 - also 100 %.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static int EffectiveDpi(IntPtr monitor)
    {
        try
        {
            if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0)
                return (int)dpiX;
        }
        catch (DllNotFoundException)
        {
            // Shcore.dll gibt es erst ab Windows 8.1.
        }
        catch (EntryPointNotFoundException)
        {
        }

        return 96;
    }

    private const uint MONITORINFOF_PRIMARY = 1;
    private const int MDT_EFFECTIVE_DPI = 0;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    private static extern bool EnumDisplayDevices(string? device, uint devNum, ref DISPLAY_DEVICE displayDevice, uint flags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
