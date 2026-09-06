using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RdpEditor.Services;

/// <summary>
/// Eine Zeile einer .rdp-Datei.
///
/// Entweder eine Einstellung (<c>schluessel:typ:wert</c>) oder eine Zeile, die
/// diesem Muster nicht folgt - eine leere Zeile etwa. Solche Zeilen behalten
/// wir im Wortlaut und geben sie beim Speichern unveraendert zurueck: Was der
/// Editor nicht versteht, darf er auch nicht wegwerfen.
/// </summary>
public sealed class RdpLine
{
    /// <summary>Der Schluessel in der Schreibweise der Datei, oder null fuer eine unverstandene Zeile.</summary>
    public string? Key { get; init; }

    /// <summary>'i' fuer Zahl, 's' fuer Text, 'b' fuer Binaerwert (Hex).</summary>
    public char Type { get; set; }

    public string Value { get; set; } = "";

    /// <summary>Der Wortlaut einer Zeile, die keine Einstellung ist.</summary>
    public string RawText { get; init; } = "";

    public bool IsSetting => Key is not null;

    public string ToLine() => IsSetting ? $"{Key}:{Type}:{Value}" : RawText;
}

/// <summary>
/// Der Inhalt einer .rdp-Datei: eine Liste von Zeilen in der Reihenfolge, in
/// der sie in der Datei stehen.
///
/// Bewusst kein Woerterbuch: mstsc schreibt seine Schluessel in einer festen
/// Reihenfolge, und eine Datei, die nach dem Speichern voellig anders sortiert
/// ist, laesst sich mit der vorigen Fassung nicht mehr vergleichen. Geaenderte
/// Werte bleiben deshalb an ihrer Stelle stehen, neue kommen ans Ende.
/// </summary>
public sealed class RdpDocument
{
    private readonly List<RdpLine> _lines = new();

    public IReadOnlyList<RdpLine> Lines => _lines;

    /// <summary>Die Kodierung, in der die Datei gelesen wurde - nur zur Anzeige.</summary>
    public string SourceEncoding { get; private set; } = "UTF-16 LE";

    // ============================================================ Lesen

    public static RdpDocument Parse(string text)
    {
        var doc = new RdpDocument();

        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (TrySplit(raw, out var key, out var type, out var value))
                doc._lines.Add(new RdpLine { Key = key, Type = type, Value = value });
            else if (raw.Length > 0)
                doc._lines.Add(new RdpLine { RawText = raw });
            // Leere Zeilen fallen weg - mstsc schreibt keine, und die letzte
            // Zeile jeder Datei erzeugte sonst bei jedem Speichern eine mehr.
        }

        return doc;
    }

    public static RdpDocument Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var doc = Parse(Decode(bytes, out var encodingName));
        doc.SourceEncoding = encodingName;
        return doc;
    }

    /// <summary>
    /// Zerlegt eine Zeile in Schluessel, Typ und Wert.
    ///
    /// Getrennt wird nur an den ersten beiden Doppelpunkten: Der Wert selbst
    /// darf welche enthalten, etwa bei <c>full address:s:server:3390</c>.
    /// </summary>
    public static bool TrySplit(string line, out string key, out char type, out string value)
    {
        key = ""; type = 's'; value = "";

        var first = line.IndexOf(':');
        if (first <= 0 || first + 2 >= line.Length) return false;
        if (line[first + 2] != ':') return false;

        var t = char.ToLowerInvariant(line[first + 1]);
        if (t != 'i' && t != 's' && t != 'b') return false;

        key = line[..first].Trim();
        if (key.Length == 0) return false;

        type = t;
        value = line[(first + 3)..];
        return true;
    }

    /// <summary>
    /// Erkennt die Kodierung an der Bytefolge.
    ///
    /// mstsc schreibt UTF-16 LE mit Byte-Order-Mark. Von Hand angelegte oder
    /// aus einem Skript erzeugte Dateien sind haeufig UTF-8 oder ANSI - und
    /// wer eine solche Datei mit einem UTF-16-Leser oeffnet, bekommt
    /// chinesische Schriftzeichen statt seiner Serveradresse.
    /// </summary>
    private static string Decode(byte[] bytes, out string encodingName)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encodingName = "UTF-16 LE (BOM)";
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encodingName = "UTF-16 BE (BOM)";
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingName = "UTF-8 (BOM)";
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        // Ohne Vorzeichen: In UTF-16 ist bei lateinischer Schrift jedes zweite
        // Byte null. In UTF-8 kommt eine Null ueberhaupt nicht vor.
        var nullBytes = bytes.Count(b => b == 0);
        if (bytes.Length >= 4 && nullBytes > bytes.Length / 4)
        {
            if (bytes[1] == 0)
            {
                encodingName = "UTF-16 LE";
                return Encoding.Unicode.GetString(bytes);
            }
            encodingName = "UTF-16 BE";
            return Encoding.BigEndianUnicode.GetString(bytes);
        }

        encodingName = "UTF-8";
        return Encoding.UTF8.GetString(bytes);
    }

    // ============================================================ Schreiben

    /// <summary>Der Inhalt als Text, mit CRLF am Zeilenende wie bei mstsc.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        foreach (var line in _lines)
        {
            sb.Append(line.ToLine());
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Schreibt die Datei als UTF-16 LE mit Byte-Order-Mark.
    ///
    /// Immer in dieser Kodierung, gleichgueltig wie die gelesene Datei
    /// aussah: Genau so legt mstsc seine Dateien ab, und es ist die einzige,
    /// bei der auch Umlaute in Benutzernamen und RemoteApp-Titeln zuverlaessig
    /// ankommen.
    /// </summary>
    public void Save(string path)
        => File.WriteAllText(path, ToText(), new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

    // ============================================================ Zugriff

    private static bool SameKey(string? a, string b)
        => a is not null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public RdpLine? Find(string key) => _lines.FirstOrDefault(l => SameKey(l.Key, key));

    public bool Contains(string key) => Find(key) is not null;

    public string? Get(string key) => Find(key)?.Value;

    public int GetInt(string key, int fallback)
        => int.TryParse(Get(key), out var value) ? value : fallback;

    /// <summary>
    /// Setzt einen Wert. Steht der Schluessel schon in der Datei, aendert sich
    /// nur sein Wert an Ort und Stelle; sonst kommt er ans Ende.
    /// </summary>
    public void Set(string key, char type, string value)
    {
        var line = Find(key);
        if (line is null)
        {
            _lines.Add(new RdpLine { Key = key, Type = type, Value = value });
            return;
        }

        line.Type = type;
        line.Value = value;
    }

    public void SetInt(string key, int value) => Set(key, 'i', value.ToString());

    public bool Remove(string key)
    {
        var line = Find(key);
        if (line is null) return false;
        _lines.Remove(line);
        return true;
    }

    /// <summary>Alle Schluessel in der Reihenfolge der Datei.</summary>
    public IEnumerable<string> Keys => _lines.Where(l => l.IsSetting).Select(l => l.Key!);

    /// <summary>
    /// Die Voreinstellung fuer eine neue Datei: der Satz, den mstsc selbst
    /// schreibt, wenn man eine Verbindung ohne weitere Angaben speichert.
    /// </summary>
    public static RdpDocument CreateDefault() => Parse(string.Join("\r\n", new[]
    {
        "screen mode id:i:2",
        "use multimon:i:0",
        "desktopwidth:i:1920",
        "desktopheight:i:1080",
        "session bpp:i:32",
        "compression:i:1",
        "keyboardhook:i:2",
        "audiocapturemode:i:0",
        "videoplaybackmode:i:1",
        "connection type:i:7",
        "networkautodetect:i:1",
        "bandwidthautodetect:i:1",
        "displayconnectionbar:i:1",
        "enableworkspacereconnect:i:0",
        "disable wallpaper:i:0",
        "allow font smoothing:i:0",
        "allow desktop composition:i:0",
        "disable full window drag:i:1",
        "disable menu anims:i:1",
        "disable themes:i:0",
        "disable cursor setting:i:0",
        "bitmapcachepersistenable:i:1",
        "full address:s:",
        "audiomode:i:0",
        "redirectprinters:i:1",
        "redirectcomports:i:0",
        "redirectsmartcards:i:1",
        "redirectclipboard:i:1",
        "redirectposdevices:i:0",
        "autoreconnection enabled:i:1",
        "authentication level:i:2",
        "prompt for credentials:i:0",
        "negotiate security layer:i:1",
        "remoteapplicationmode:i:0",
        "alternate shell:s:",
        "shell working directory:s:",
        "gatewayhostname:s:",
        "gatewayusagemethod:i:4",
        "gatewaycredentialssource:i:4",
        "gatewayprofileusagemethod:i:0",
        "promptcredentialonce:i:0",
        "gatewaybrokeringtype:i:0",
        "use redirection server name:i:0",
        "rdgiskdcproxy:i:0",
        "kdcproxyname:s:",
        "drivestoredirect:s:",
    }));
}
