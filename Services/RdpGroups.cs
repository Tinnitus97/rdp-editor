using System;
using System.Collections.Generic;
using System.Linq;

namespace RdpEditor.Services;

/// <summary>
/// Eine Gruppe zusammengehörender Einstellungen innerhalb eines Reiters.
///
/// Warum getrennt vom Katalog: Der Katalog beschreibt, was ein Schlüssel
/// bedeutet - die Gruppe beschreibt, was zusammengehört. Beides in einer
/// Zeile hieße, die Reihenfolge im Fenster über neunzig verstreute Einträge
/// zu verwalten; hier steht sie an einer Stelle und ist als Ganzes zu lesen.
///
/// Die Reihenfolge der Schlüssel in <paramref name="Keys"/> ist zugleich die
/// Reihenfolge im Fenster.
/// </summary>
public sealed record RdpGroup(
    string Category,
    string Title,
    string Hint,
    IReadOnlyList<string> Keys,
    IReadOnlyList<RdpPreset>? Presets = null);

/// <summary>
/// Ein Satz Werte, den ein Knopf auf einmal setzt.
///
/// mstsc macht dasselbe unsichtbar: Wer im Reiter "Leistung" die
/// Übertragungsrate umstellt, dem setzt es die sechs Haken darunter mit um -
/// ohne zu sagen, welche. Hier steht es am Knopf.
/// </summary>
public sealed record RdpPreset(string Title, string Hint, IReadOnlyDictionary<string, string> Values);

public static class RdpGroups
{
    // ---------------------------------------------------------- Kurzschreibweise

    private static RdpPreset Preset(string title, string hint, params string[] pairs)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < pairs.Length; i += 2)
            values[pairs[i]] = pairs[i + 1];
        return new RdpPreset(title, hint, values);
    }

    /// <summary>
    /// Die Übertragungsraten aus dem Dialog von mstsc - mit genau den Haken,
    /// die Windows dabei setzt.
    ///
    /// Die Zuordnung ist abgelesen, nicht geraten: Für jede der sechs Raten
    /// wurde der Dialog geöffnet und notiert, welche der sechs Kästchen danach
    /// gefüllt sind. Der Test dazu führt dieselbe Tabelle noch einmal in der
    /// Sicht des Dialogs; wer hier etwas ändert, muss sie dort mitändern.
    ///
    /// ACHTUNG, umgekehrte Zählweise: Vier der sechs Schlüssel heißen in der
    /// Datei "disable ...". Ein gefülltes Kästchen im Dialog ist dort eine 0.
    ///
    ///                                     Modem  Breit-   Satel-  Breit-  WAN  LAN
    ///                                            band     lit     band
    ///                                            niedrig          hoch
    ///   Desktophintergrund                  -      -        -       -      x    x
    ///   Schriftartglättung                  -      -        -       -      x    x
    ///   Desktopgestaltung                   -      -        x       x      x    x
    ///   Fensterinhalt beim Ziehen           -      -        -       -      x    x
    ///   Menü- und Fensteranimation          -      -        -       -      x    x
    ///   Visuelle Stile                      -      x        x       x      x    x
    ///
    /// Nicht dabei sind "Dauerhafte Bitmapzwischenspeicherung" und
    /// "Verbindung erneut herstellen": Die stehen im Dialog unterhalb des
    /// Kastens und bleiben von der Rate unberührt. Ebenso "disable cursor
    /// setting" - dafür hat der Dialog gar kein Kästchen.
    /// </summary>
    private static RdpPreset Rate(string title, string hint, int connectionType,
                                  bool wallpaper, bool fontSmoothing, bool composition,
                                  bool fullWindowDrag, bool menuAnims, bool themes)
        => Preset(title, hint,
                  "connection type", connectionType.ToString(),
                  // Eine feste Rate schließt das Messen aus - sonst überschriebe
                  // die Messung beim Verbinden gerade das, was hier gewählt wurde.
                  "networkautodetect", "0",
                  "bandwidthautodetect", "0",
                  // Erlaubt heißt in der Datei: NICHT abgeschaltet.
                  "disable wallpaper", wallpaper ? "0" : "1",
                  "allow font smoothing", fontSmoothing ? "1" : "0",
                  "allow desktop composition", composition ? "1" : "0",
                  "disable full window drag", fullWindowDrag ? "0" : "1",
                  "disable menu anims", menuAnims ? "0" : "1",
                  "disable themes", themes ? "0" : "1");

    private static readonly RdpPreset[] LeistungsPresets =
    {
        Rate("Modem (56 kBit/s)",
             "Wie Windows es setzt: keine einzige Zugabe.",
             1, false, false, false, false, false, false),

        Rate("Breitband niedrig (256 kBit/s - 2 MBit/s)",
             "Wie Windows es setzt: nur die visuellen Stile, sonst nichts.",
             2, false, false, false, false, false, true),

        Rate("Satellit (2 - 16 MBit/s)",
             "Wie Windows es setzt: Desktopgestaltung und visuelle Stile. "
           + "Für Leitungen mit viel Bandbreite und langer Laufzeit.",
             3, false, false, true, false, false, true),

        Rate("Breitband hoch (2 - 10 MBit/s)",
             "Wie Windows es setzt: Desktopgestaltung und visuelle Stile - "
           + "derselbe Satz wie beim Satelliten.",
             4, false, false, true, false, false, true),

        Rate("WAN (10 MBit/s oder höher, hohe Latenz)",
             "Wie Windows es setzt: alle sechs erlaubt.",
             5, true, true, true, true, true, true),

        Rate("LAN (10 MBit/s oder höher)",
             "Wie Windows es setzt: alle sechs erlaubt.",
             6, true, true, true, true, true, true),

        Preset("Automatisch erkennen",
               "mstsc misst die Leitung beim Verbinden selbst. Die sechs Schalter darunter "
             + "bleiben, wie sie sind - im Dialog von Windows sind sie in diesem Fall nicht "
             + "die Auskunft, sondern die Messung ist es.",
               "connection type", "7",
               "networkautodetect", "1",
               "bandwidthautodetect", "1"),
    };

    private static readonly RdpPreset[] GeraetePresets =
    {
        Preset("Nichts weiterreichen",
               "Kein Laufwerk, kein Drucker, keine Zwischenablage - die Sitzung bleibt für sich.",
               "redirectclipboard", "0",
               "redirectprinters", "0",
               "redirectcomports", "0",
               "redirectsmartcards", "0",
               "redirectposdevices", "0",
               "redirectlocation", "0",
               "redirectwebauthn", "0",
               "drivestoredirect", ""),

        Preset("Nur Zwischenablage",
               "Kopieren und Einfügen ja, Geräte und Laufwerke nein. Der Zuschnitt für eine "
             + "Sitzung auf einem fremden Server.",
               "redirectclipboard", "1",
               "redirectprinters", "0",
               "redirectcomports", "0",
               "redirectsmartcards", "0",
               "redirectposdevices", "0",
               "drivestoredirect", ""),

        Preset("Alle Laufwerke",
               "Auch später angesteckte. Bequem im eigenen Netz, heikel auf fremden Servern - "
             + "wer dort Administrator ist, sieht Ihre Festplatte.",
               "drivestoredirect", "*"),
    };

    // ---------------------------------------------------------- Die Gruppen

    public static readonly IReadOnlyList<RdpGroup> All = new List<RdpGroup>
    {
        // ======================================================== Verbindung
        new(RdpCatalog.CatConnection, "Server und Anmeldung",
            "Wohin die Verbindung geht und mit wem sie sich anmeldet.",
            new[] { "full address", "server port", "username", "domain", "alternate full address" }),

        new(RdpCatalog.CatConnection, "Sitzung",
            "Wie sich die Verbindung verhält, während sie steht.",
            new[] { "administrative session", "disableconnectionsharing",
                    "autoreconnection enabled", "autoreconnect max retries" }),

        new(RdpCatalog.CatConnection, "Verbindungsbroker und Arbeitsbereich",
            "Nur belegt, wenn die Datei aus einer Sitzungssammlung oder einem RemoteApp-Portal "
          + "stammt. Von Hand braucht man hier nichts einzutragen.",
            new[] { "loadbalanceinfo", "workspaceid", "enableworkspacereconnect",
                    "use redirection server name" }),

        // =========================================================== Anzeige
        new(RdpCatalog.CatDisplay, "Fenster und Vollbild",
            "Ob die Sitzung im Fenster oder auf dem ganzen Schirm läuft.",
            new[] { "screen mode id", "winposstr", "displayconnectionbar", "pinconnectionbar",
                    "smart sizing", "dynamic resolution" }),

        new(RdpCatalog.CatDisplay, "Auflösung und Farben",
            "Wie groß die Sitzung ist. Im Vollbild überschreibt die Bildschirmgröße diese Werte.",
            new[] { "desktopwidth", "desktopheight", "desktopscalefactor", "desktop size id",
                    "session bpp" }),

        new(RdpCatalog.CatDisplay, "Mehrere Bildschirme",
            "Bequemer zu setzen im Reiter \"Bildschirme\" - dort steht der Plan dazu.",
            new[] { "use multimon", "selectedmonitors", "maximizetocurrentdisplays",
                    "singlemoninwindowedmode", "span monitors" }),

        // ==================================================== Lokale Geräte
        new(RdpCatalog.CatDevices, "Ton",
            "Wo der Ton der Sitzung landet.",
            new[] { "audiomode", "audiocapturemode", "audioqualitymode" }),

        new(RdpCatalog.CatDevices, "Tastatur und Zwischenablage",
            "Was zwischen beiden Rechnern hin und her geht.",
            new[] { "keyboardhook", "redirectclipboard" }),

        new(RdpCatalog.CatDevices, "Geräte",
            "Was von diesem Rechner in der Sitzung erscheint.",
            new[] { "redirectprinters", "redirectsmartcards", "redirectcomports",
                    "redirectposdevices", "redirectlocation", "redirectwebauthn",
                    "redirectdirectx" }),

        new(RdpCatalog.CatDevices, "Laufwerke und USB",
            "Die Felder nehmen Listen: \"*\" für alle, sonst Laufwerksbuchstaben oder "
          + "Gerätekennungen mit Semikolon getrennt.",
            new[] { "drivestoredirect", "usbdevicestoredirect", "camerastoredirect",
                    "devicestoredirect" },
            GeraetePresets),

        // ========================================================== Leistung
        new(RdpCatalog.CatPerformance, "Übertragungsrate",
            "Ein Knopf setzt die Rate und die sechs Darstellungsschalter darunter in einem Zug - "
          + "mit genau den Haken, die auch Windows bei dieser Rate setzt. Was der Knopf "
          + "schreibt, steht am Mauszeiger.",
            new[] { "connection type", "networkautodetect", "bandwidthautodetect" },
            LeistungsPresets),

        new(RdpCatalog.CatPerformance, "Folgendes zulassen",
            "ACHTUNG, umgekehrte Zählweise: In der Datei heißen die meisten dieser Schlüssel "
          + "\"disable ...\". Ein Haken hier bedeutet deshalb ABGESCHALTET, im mstsc-Dialog "
          + "bedeutet ein Haken das Gegenteil. Die Beschriftung sagt jeweils, was der Haken tut.",
            new[] { "disable wallpaper", "allow font smoothing", "allow desktop composition",
                    "disable full window drag", "disable menu anims", "disable themes",
                    "disable cursor setting" }),

        new(RdpCatalog.CatPerformance, "Zwischenspeicher und Video",
            "Was nicht die Darstellung betrifft, sondern den Weg der Daten.",
            new[] { "compression", "bitmapcachepersistenable", "bitmapcachesize",
                    "videoplaybackmode" }),

        // ========================================================= Sicherheit
        new(RdpCatalog.CatSecurity, "Serverprüfung",
            "Was geschieht, wenn sich der Server nicht ausweisen kann.",
            new[] { "authentication level", "enablecredsspsupport", "negotiate security layer" }),

        new(RdpCatalog.CatSecurity, "Anmeldung",
            "Wann und wo nach Benutzername und Kennwort gefragt wird.",
            new[] { "prompt for credentials", "promptcredentialonce",
                    "prompt for credentials on client", "disable ctrl+alt+del",
                    "enablerdsaadauth" }),

        // ============================================================ Gateway
        new(RdpCatalog.CatGateway, "Gatewayserver",
            "Der Umweg über ein RD-Gateway, wenn der Server nicht unmittelbar erreichbar ist.",
            new[] { "gatewayhostname", "gatewayusagemethod", "gatewaycredentialssource",
                    "gatewayprofileusagemethod", "gatewaybrokeringtype", "gatewayaccesstoken" }),

        new(RdpCatalog.CatGateway, "Kerberos-Proxy",
            "Nur nötig, wenn die Anmeldung mit Kerberos über dasselbe Gateway laufen soll.",
            new[] { "rdgiskdcproxy", "kdcproxyname" }),

        // ========================================================== RemoteApp
        new(RdpCatalog.CatRemoteApp, "Programm",
            "Statt des ganzen Desktops erscheint ein einzelnes Fenster.",
            new[] { "remoteapplicationmode", "remoteapplicationname", "remoteapplicationprogram",
                    "remoteapplicationcmdline", "remoteapplicationfile", "remoteapplicationicon" }),

        new(RdpCatalog.CatRemoteApp, "Verhalten und Shell",
            "Beiwerk, das erst bei eigenen RemoteApp-Aufbauten gebraucht wird.",
            new[] { "remoteapplicationexpandcmdline", "remoteapplicationexpandworkingdir",
                    "disableremoteappcapscheck", "remoteappmousemoveinject",
                    "alternate shell", "shell working directory" }),

        // ========================================================== Erweitert
        new(RdpCatalog.CatAdvanced, "Signatur und Kennwort",
            "Beides lässt sich hier nicht erzeugen, nur ansehen und entfernen.",
            new[] { "password 51", "signature", "signscope" }),

        new(RdpCatalog.CatAdvanced, "Alte Schreibweisen und Seltenes",
            "Schlüssel aus früheren Windows-Fassungen. Sie stören nicht, wirken aber meist auch "
          + "nicht mehr.",
            new[] { "connect to console", "allow smart sizing", "redirectdrives",
                    "keyboardlayout", "public mode" }),
    };

    /// <summary>Die Gruppen eines Reiters, in ihrer Reihenfolge.</summary>
    public static IEnumerable<RdpGroup> ForCategory(string category)
        => All.Where(g => g.Category == category);

    /// <summary>
    /// Alle Schlüssel, die irgendeine Gruppe nennt - für die Prüfung, dass
    /// Katalog und Gruppen zueinander passen.
    /// </summary>
    public static IEnumerable<string> AllKeys => All.SelectMany(g => g.Keys);
}
