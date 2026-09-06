using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RdpEditor.Services;

namespace RdpEditorTests;

internal static class Program
{
    private static int _failed;
    private static int _passed;

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (condition) { _passed++; Console.WriteLine($"  [OK]   {name}"); }
        else { _failed++; Console.WriteLine($"  [FAIL] {name}{(detail is null ? "" : $"  -> {detail}")}"); }
    }

    private static int Main()
    {
        Console.WriteLine("Zeilen lesen und schreiben");
        PruefeParser();

        Console.WriteLine();
        Console.WriteLine("Kodierung");
        PruefeKodierung();

        Console.WriteLine();
        Console.WriteLine("Monitorauswahl");
        PruefeMonitorauswahl();

        Console.WriteLine();
        Console.WriteLine("Katalog");
        PruefeKatalog();

        Console.WriteLine();
        Console.WriteLine($"{_passed} bestanden, {_failed} gescheitert.");
        return _failed == 0 ? 0 : 1;
    }

    // ============================================================ Parser

    /// <summary>Die Beispieldatei aus samples\Server.rdp, gekuerzt.</summary>
    private const string Beispiel =
        "screen mode id:i:2\r\n" +
        "use multimon:i:0\r\n" +
        "desktopwidth:i:3840\r\n" +
        "winposstr:s:0,1,1344,63,3700,1591\r\n" +
        "full address:s:192.168.219.250\r\n" +
        "alternate shell:s:\r\n" +
        "drivestoredirect:s:\r\n";

    private static void PruefeParser()
    {
        var doc = RdpDocument.Parse(Beispiel);

        Check("alle sieben Zeilen sind Einstellungen",
            doc.Lines.Count == 7 && doc.Lines.All(l => l.IsSetting),
            $"{doc.Lines.Count} Zeilen");

        Check("Zahlen kommen als Zahl an", doc.GetInt("desktopwidth", -1) == 3840);
        Check("Text kommt als Text an", doc.Get("full address") == "192.168.219.250");
        Check("ein leerer Wert bleibt leer und verschwindet nicht",
            doc.Contains("alternate shell") && doc.Get("alternate shell") == "");

        // Der Wert darf selbst Doppelpunkte enthalten.
        var mitPort = RdpDocument.Parse("full address:s:server.example.com:3390\r\n");
        Check("ein Anschluss im Wert bleibt am Wert",
            mitPort.Get("full address") == "server.example.com:3390",
            mitPort.Get("full address"));

        // Und Kommas erst recht.
        Check("Kommas im Wert bleiben erhalten",
            doc.Get("winposstr") == "0,1,1344,63,3700,1591");

        Check("Zeile fuer Zeile derselbe Text wie vorher",
            doc.ToText() == Beispiel, doc.ToText().Replace("\r\n", "|"));

        // Aendern an Ort und Stelle, nicht ans Ende.
        doc.SetInt("use multimon", 1);
        Check("ein geaenderter Wert bleibt an seiner Stelle",
            doc.Lines[1].Key == "use multimon" && doc.Lines[1].Value == "1");

        doc.Set("selectedmonitors", 's', "0,2");
        Check("ein neuer Schluessel kommt ans Ende",
            doc.Lines[^1].Key == "selectedmonitors" && doc.Lines[^1].Value == "0,2");

        Check("Gross- und Kleinschreibung ist gleichgueltig",
            doc.Get("Full Address") == "192.168.219.250");

        Check("Entfernen entfernt genau eine Zeile",
            doc.Remove("selectedmonitors") && !doc.Contains("selectedmonitors")
            && doc.Lines.Count == 7);

        Check("Entfernen meldet, wenn es nichts zu entfernen gab",
            !doc.Remove("gibtesnicht"));

        // Was der Editor nicht versteht, wirft er nicht weg.
        var fremd = RdpDocument.Parse("kein doppelpunkt hier\r\nfull address:s:srv\r\n");
        Check("eine unverstandene Zeile bleibt im Wortlaut erhalten",
            fremd.ToText().StartsWith("kein doppelpunkt hier\r\n"), fremd.ToText());

        // Ein unbekannter Typbuchstabe ist keine Einstellung, sondern Text.
        var komisch = RdpDocument.Parse("etwas:x:1\r\n");
        Check("ein unbekannter Typ zaehlt nicht als Einstellung",
            !komisch.Lines[0].IsSetting);

        Check("die Voreinstellung enthaelt die Schluessel, die mstsc schreibt",
            RdpDocument.CreateDefault().Contains("screen mode id")
            && RdpDocument.CreateDefault().Contains("full address"));
    }

    // ============================================================ Kodierung

    private static void PruefeKodierung()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "rdpeditor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);

        try
        {
            // So schreibt mstsc: UTF-16 LE mit Vorzeichen.
            var utf16 = Path.Combine(ordner, "utf16.rdp");
            File.WriteAllText(utf16, Beispiel, new UnicodeEncoding(false, true));
            var gelesen = RdpDocument.Load(utf16);
            Check("UTF-16 mit BOM wird gelesen",
                gelesen.Get("full address") == "192.168.219.250", gelesen.SourceEncoding);

            // So schreiben Skripte und Texteditoren.
            var utf8 = Path.Combine(ordner, "utf8.rdp");
            File.WriteAllText(utf8, Beispiel, new UTF8Encoding(false));
            var gelesen8 = RdpDocument.Load(utf8);
            Check("UTF-8 ohne BOM wird gelesen",
                gelesen8.Get("full address") == "192.168.219.250", gelesen8.SourceEncoding);

            // Ein Umlaut ueberlebt den Weg durch UTF-8 hinein und UTF-16 hinaus.
            var umlaut = Path.Combine(ordner, "umlaut.rdp");
            File.WriteAllText(umlaut, "username:s:Kroeger\u00e4\r\n", new UTF8Encoding(true));
            var geladen = RdpDocument.Load(umlaut);
            Check("Umlaute ueberstehen das Lesen", geladen.Get("username") == "Kroeger\u00e4");

            var ziel = Path.Combine(ordner, "gespeichert.rdp");
            geladen.Save(ziel);

            var bytes = File.ReadAllBytes(ziel);
            Check("gespeichert wird UTF-16 LE mit BOM",
                bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xFE);
            Check("Umlaute ueberstehen auch das Speichern",
                RdpDocument.Load(ziel).Get("username") == "Kroeger\u00e4");
            Check("die Zeilen enden mit CRLF",
                Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2).EndsWith("\r\n"));
        }
        finally
        {
            try { Directory.Delete(ordner, recursive: true); } catch { }
        }
    }

    // ============================================================ Monitore

    private static MonitorEntry Schirm(int id, int x, int y, int w = 1920, int h = 1080, bool primaer = false)
        => new(id, $"\\\\.\\DISPLAY{id + 1}", $"Bildschirm {id + 1}", x, y, w, h, primaer, 96);

    private static void PruefeMonitorauswahl()
    {
        Check("eine leere Angabe ergibt keine Auswahl",
            MonitorSelection.Parse("").Count == 0 && MonitorSelection.Parse(null).Count == 0);

        Check("\"0,2\" ergibt zwei Nummern",
            MonitorSelection.Parse("0,2").SequenceEqual(new[] { 0, 2 }));

        Check("die Reihenfolge bleibt, wie sie kommt",
            MonitorSelection.Parse("2,0,1").SequenceEqual(new[] { 2, 0, 1 }));

        Check("Doppelte und Unsinn fallen weg",
            MonitorSelection.Parse("1, 1, x, 3").SequenceEqual(new[] { 1, 3 }));

        Check("geschrieben wird mit Komma ohne Leerzeichen",
            MonitorSelection.Format(new[] { 0, 2 }) == "0,2");

        // Zusammenhang: 0 und 1 liegen nebeneinander, 2 steht abseits.
        var links = Schirm(0, 0, 0);
        var mitte = Schirm(1, 1920, 0, primaer: true);
        var abseits = Schirm(2, 6000, 0);

        Check("nebeneinanderliegende Bildschirme haengen zusammen",
            MonitorSelection.IsContiguous(new[] { links, mitte }));
        Check("ein Bildschirm allein haengt immer zusammen",
            MonitorSelection.IsContiguous(new[] { abseits }));
        Check("eine Luecke dazwischen faellt auf",
            !MonitorSelection.IsContiguous(new[] { links, abseits }));
        Check("ueber den mittleren Bildschirm haengt alles wieder zusammen",
            MonitorSelection.IsContiguous(new[] { links, mitte, Schirm(3, 3840, 0) }));

        // Und das Schreiben in die Datei.
        var doc = RdpDocument.CreateDefault();

        MonitorSelection.Apply(doc, new[] { 0, 1 }, 3);
        Check("die Auswahl steht in der Datei", doc.Get("selectedmonitors") == "0,1");
        Check("mehrere Bildschirme werden eingeschaltet", doc.GetInt("use multimon", -1) == 1);
        Check("und das Vollbild dazu", doc.GetInt("screen mode id", -1) == 2);

        MonitorSelection.Apply(doc, new[] { 0, 1, 2 }, 3);
        Check("sind alle gewaehlt, entfaellt die Zeile wieder",
            !doc.Contains("selectedmonitors") && doc.GetInt("use multimon", -1) == 1);

        MonitorSelection.Apply(doc, Array.Empty<int>(), 3);
        Check("ohne Auswahl bleibt ein Bildschirm",
            !doc.Contains("selectedmonitors") && doc.GetInt("use multimon", -1) == 0);

        // Fuer eine Datei, die auf einem anderen Rechner benutzt wird, ist die
        // Zahl der Bildschirme hier unbekannt - dann wird immer geschrieben.
        MonitorSelection.Apply(doc, new[] { 0, 1 }, 0);
        Check("ohne bekannte Bildschirmzahl wird die Auswahl geschrieben",
            doc.Get("selectedmonitors") == "0,1");

        Check("der Klartext nennt die Nummern",
            MonitorSelection.Describe(new[] { 0, 2 }, 3).Contains("0,2"));
    }

    // ============================================================ Katalog

    private static void PruefeKatalog()
    {
        var doppelt = RdpCatalog.All
            .GroupBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Check("kein Schluessel steht zweimal im Katalog",
            doppelt.Count == 0, string.Join(", ", doppelt));

        var fremdeKategorie = RdpCatalog.All
            .Where(s => !RdpCatalog.Categories.Contains(s.Category))
            .Select(s => $"{s.Key} -> {s.Category}")
            .ToList();

        Check("jede Einstellung liegt in einem Reiter, den es gibt",
            fremdeKategorie.Count == 0, string.Join(", ", fremdeKategorie));

        var ohneAuswahl = RdpCatalog.All
            .Where(s => s.Kind == RdpEditorKind.Choice && (s.Choices is null || s.Choices.Count == 0))
            .Select(s => s.Key)
            .ToList();

        Check("jede Auswahlliste hat Eintraege",
            ohneAuswahl.Count == 0, string.Join(", ", ohneAuswahl));

        var falscherTyp = RdpCatalog.All
            .Where(s => (s.Kind == RdpEditorKind.Toggle || s.Kind == RdpEditorKind.Number) && s.Type != 'i')
            .Select(s => s.Key)
            .ToList();

        Check("Haken und Zahlen stehen als Zahl in der Datei",
            falscherTyp.Count == 0, string.Join(", ", falscherTyp));

        var ohneHinweis = RdpCatalog.All.Where(s => s.Hint.Length < 20).Select(s => s.Key).ToList();
        Check("jede Einstellung hat einen Hinweis im Klartext",
            ohneHinweis.Count == 0, string.Join(", ", ohneHinweis));

        // Die Probe aufs Ganze: Jeder Schluessel der Beispieldatei soll im
        // Katalog stehen. Was fehlt, landet zwar unter "Unbekannt" und geht
        // nicht verloren - aber ohne Klartext daneben.
        var beispiel = RdpDocument.Parse(BeispielVollstaendig);
        var unbekannt = beispiel.Keys.Where(k => !RdpCatalog.Knows(k)).ToList();
        Check("der Katalog kennt jeden Schluessel der Beispieldatei",
            unbekannt.Count == 0, string.Join(", ", unbekannt));

        Check("die Bildschirmauswahl selbst steht auch im Katalog",
            RdpCatalog.Knows("selectedmonitors") && RdpCatalog.Knows("use multimon"));

        Check("ein unbekannter Schluessel bekommt einen Ersatzeintrag",
            RdpCatalog.Unknown("irgendwas", 's').Category == RdpCatalog.CatUnknown);
    }

    /// <summary>Die Beispieldatei, wie mstsc sie geschrieben hat.</summary>
    private const string BeispielVollstaendig = """
        screen mode id:i:2
        use multimon:i:0
        desktopwidth:i:3840
        desktopheight:i:2160
        session bpp:i:32
        winposstr:s:0,1,1344,63,3700,1591
        compression:i:1
        keyboardhook:i:2
        audiocapturemode:i:0
        videoplaybackmode:i:1
        connection type:i:7
        networkautodetect:i:1
        bandwidthautodetect:i:1
        displayconnectionbar:i:1
        enableworkspacereconnect:i:0
        disable wallpaper:i:0
        allow font smoothing:i:0
        allow desktop composition:i:0
        disable full window drag:i:1
        disable menu anims:i:1
        disable themes:i:0
        disable cursor setting:i:0
        bitmapcachepersistenable:i:1
        full address:s:192.168.219.250
        audiomode:i:0
        redirectprinters:i:1
        redirectcomports:i:0
        redirectsmartcards:i:1
        redirectclipboard:i:1
        redirectposdevices:i:0
        autoreconnection enabled:i:1
        authentication level:i:2
        prompt for credentials:i:0
        negotiate security layer:i:1
        remoteapplicationmode:i:0
        alternate shell:s:
        shell working directory:s:
        gatewayhostname:s:
        gatewayusagemethod:i:4
        gatewaycredentialssource:i:4
        gatewayprofileusagemethod:i:0
        promptcredentialonce:i:0
        gatewaybrokeringtype:i:0
        use redirection server name:i:0
        rdgiskdcproxy:i:0
        kdcproxyname:s:
        redirectlocation:i:0
        remoteappmousemoveinject:i:1
        redirectwebauthn:i:1
        enablerdsaadauth:i:0
        drivestoredirect:s:
        """;
}
