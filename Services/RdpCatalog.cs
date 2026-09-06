using System;
using System.Collections.Generic;
using System.Linq;

namespace RdpEditor.Services;

/// <summary>Wie eine Einstellung bedient wird.</summary>
public enum RdpEditorKind
{
    /// <summary>0 oder 1 - ein Haken.</summary>
    Toggle,

    /// <summary>Eine feste Zahl von Moeglichkeiten - eine Auswahlliste.</summary>
    Choice,

    /// <summary>Eine Zahl ohne festen Vorrat - ein Feld.</summary>
    Number,

    /// <summary>Text.</summary>
    Text,
}

/// <summary>Ein Eintrag einer Auswahlliste: der Wert in der Datei und sein Klartext.</summary>
public sealed record RdpChoice(string Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Eine bekannte Einstellung einer .rdp-Datei.
///
/// <paramref name="Hint"/> ist der Grund, warum es diesen Katalog gibt: In der
/// Datei steht "authentication level:i:2" - was diese 2 bedeutet, steht
/// nirgends. Ohne den Klartext daneben ist ein Editor fuer .rdp-Dateien nur
/// ein Texteditor mit Zeilennummern.
/// </summary>
public sealed record RdpSetting(
    string Key,
    char Type,
    string Category,
    string Label,
    string Hint,
    RdpEditorKind Kind,
    IReadOnlyList<RdpChoice>? Choices = null);

/// <summary>
/// Alle Einstellungen, die mstsc kennt - mit deutscher Beschriftung.
///
/// Was hier nicht steht, geht trotzdem nicht verloren: Schluessel, die in der
/// Datei stehen und im Katalog fehlen, landen unter "Unbekannt" und lassen
/// sich dort ebenso aendern. Der Katalog entscheidet ueber die Bequemlichkeit,
/// nicht ueber den Umfang.
/// </summary>
public static class RdpCatalog
{
    public const string CatConnection = "Verbindung";
    public const string CatDisplay    = "Anzeige";
    public const string CatDevices    = "Lokale Geraete";
    public const string CatPerformance= "Leistung";
    public const string CatSecurity   = "Sicherheit";
    public const string CatGateway    = "Gateway";
    public const string CatRemoteApp  = "RemoteApp";
    public const string CatAdvanced   = "Erweitert";
    public const string CatUnknown    = "Unbekannt";

    /// <summary>Die Reihenfolge der Reiter.</summary>
    public static readonly string[] Categories =
    {
        CatConnection, CatDisplay, CatDevices, CatPerformance,
        CatSecurity, CatGateway, CatRemoteApp, CatAdvanced,
    };

    // ---------------------------------------------------------- Kurzschreibweisen

    private static RdpSetting Toggle(string key, string cat, string label, string hint)
        => new(key, 'i', cat, label, hint, RdpEditorKind.Toggle);

    private static RdpSetting Number(string key, string cat, string label, string hint)
        => new(key, 'i', cat, label, hint, RdpEditorKind.Number);

    private static RdpSetting Text(string key, string cat, string label, string hint)
        => new(key, 's', cat, label, hint, RdpEditorKind.Text);

    private static RdpSetting Pick(string key, char type, string cat, string label, string hint,
                                   params string[] pairs)
    {
        // Die Paare stehen abwechselnd: Wert, Klartext, Wert, Klartext ...
        var choices = new List<RdpChoice>();
        for (var i = 0; i + 1 < pairs.Length; i += 2)
            choices.Add(new RdpChoice(pairs[i], pairs[i + 1]));

        return new RdpSetting(key, type, cat, label, hint, RdpEditorKind.Choice, choices);
    }

    // ---------------------------------------------------------- Der Katalog

    public static readonly IReadOnlyList<RdpSetting> All = new List<RdpSetting>
    {
        // ======================================================== Verbindung
        Text("full address", CatConnection, "Server",
             "Name oder Adresse des Remotecomputers. Ein abweichender Anschluss wird angehaengt: "
           + "server:3390."),
        Text("alternate full address", CatConnection, "Ausweichadresse",
             "Wird anstelle von \"full address\" benutzt, wenn beide gesetzt sind. Kommt von "
           + "Verbindungsbrokern."),
        Number("server port", CatConnection, "Anschluss (Port)",
             "Voreinstellung 3389. Steht schon ein Anschluss hinter der Serveradresse, gilt jener."),
        Text("username", CatConnection, "Benutzername",
             "Vorbelegung des Anmeldefensters. Mit Domaene: DOMAENE\\benutzer oder "
           + "benutzer@domaene.tld."),
        Text("domain", CatConnection, "Domaene",
             "Nur noetig, wenn der Benutzername sie nicht schon enthaelt."),
        Toggle("administrative session", CatConnection, "Verwaltungssitzung",
             "Verbindet mit der Konsolensitzung des Servers (frueher \"connect to console\"). "
           + "Nur bei Servern sinnvoll."),
        Toggle("disableconnectionsharing", CatConnection, "Verbindung nicht wiederverwenden",
             "1 oeffnet auch dann ein neues Fenster, wenn zu diesem Server schon eine Sitzung "
           + "laeuft. Sonst wird die bestehende benutzt."),
        Toggle("autoreconnection enabled", CatConnection, "Automatisch neu verbinden",
             "Bei einem kurzen Netzausfall verbindet sich mstsc von selbst wieder."),
        Number("autoreconnect max retries", CatConnection, "Versuche beim Neuverbinden",
             "Wie oft es die automatische Wiederverbindung versucht. Voreinstellung 20."),
        Text("loadbalanceinfo", CatConnection, "Lastverteilung",
             "Kennung fuer den Verbindungsbroker einer Sitzungssammlung, etwa "
           + "\"tsv://MS Terminal Services Plugin.1.Sammlung\"."),
        Text("workspaceid", CatConnection, "Arbeitsbereich",
             "Kennung des RemoteApp-Arbeitsbereichs, aus dem diese Datei stammt."),
        Toggle("enableworkspacereconnect", CatConnection, "Arbeitsbereich wieder aufnehmen",
             "Nimmt beim Start die Sitzungen des Arbeitsbereichs wieder auf."),
        Toggle("use redirection server name", CatConnection, "Umleitungsnamen benutzen",
             "1 verbindet mit dem Servernamen, den der Broker meldet, statt mit dem eingetragenen. "
           + "Noetig hinter manchen Lastverteilern."),

        // =========================================================== Anzeige
        Pick("screen mode id", 'i', CatDisplay, "Fenstermodus",
             "Vollbild ist Voraussetzung dafuer, dass die Auswahl einzelner Bildschirme greift.",
             "1", "Im Fenster",
             "2", "Vollbild"),
        Toggle("use multimon", CatDisplay, "Mehrere Bildschirme benutzen",
             "Die Sitzung erstreckt sich ueber mehrere Bildschirme. Ohne \"selectedmonitors\" "
           + "ueber alle - welche es sein sollen, steht im Reiter \"Bildschirme\"."),
        Text("selectedmonitors", CatDisplay, "Gewaehlte Bildschirme",
             "Die Nummern der Bildschirme, durch Komma getrennt, etwa \"0,1\". Der erste ist der "
           + "Hauptbildschirm der Sitzung. Bequemer zu setzen im Reiter \"Bildschirme\"."),
        Number("desktopwidth", CatDisplay, "Breite",
             "Breite der Sitzung in Bildpunkten. Wird bei Vollbild von der Bildschirmgroesse "
           + "ueberschrieben."),
        Number("desktopheight", CatDisplay, "Hoehe",
             "Hoehe der Sitzung in Bildpunkten."),
        Pick("desktopscalefactor", 'i', CatDisplay, "Skalierung",
             "Vergroesserung der Sitzungsanzeige in Prozent. Wirkt nur zusammen mit "
           + "\"desktopwidth\"/\"desktopheight\" und ab Windows 8.1.",
             "100", "100 %", "125", "125 %", "150", "150 %", "175", "175 %",
             "200", "200 %", "250", "250 %", "300", "300 %"),
        Pick("desktop size id", 'i', CatDisplay, "Groesse aus Liste",
             "Alte Art, die Groesse anzugeben. \"desktopwidth\"/\"desktopheight\" gehen vor.",
             "0", "640 x 480", "1", "800 x 600", "2", "1024 x 768",
             "3", "1280 x 1024", "4", "1600 x 1200"),
        Toggle("dynamic resolution", CatDisplay, "Groesse mitwachsen lassen",
             "Die Sitzung passt ihre Aufloesung an, wenn das Fenster seine Groesse aendert."),
        Toggle("smart sizing", CatDisplay, "Inhalt einpassen",
             "Skaliert die Sitzung in das Fenster, statt Rollbalken anzuzeigen."),
        Pick("session bpp", 'i', CatDisplay, "Farbtiefe",
             "Mehr Farben brauchen mehr Bandbreite. 32 Bit ist heute die Regel.",
             "8", "8 Bit (256 Farben)", "15", "15 Bit", "16", "16 Bit",
             "24", "24 Bit", "32", "32 Bit"),
        Text("winposstr", CatDisplay, "Fensterlage",
             "Lage und Groesse des Fensters: 0,Zustand,Links,Oben,Rechts,Unten. Der Zustand ist "
           + "1 fuer normal und 3 fuer maximiert. mstsc schreibt die Zeile beim Beenden neu."),
        Toggle("maximizetocurrentdisplays", CatDisplay, "Auf aktuellem Bildschirm maximieren",
             "Beim Maximieren nimmt die Sitzung den Bildschirm, auf dem das Fenster gerade liegt "
           + "- nicht mehr alle."),
        Toggle("singlemoninwindowedmode", CatDisplay, "Im Fenster nur ein Bildschirm",
             "Eine Mehrschirmsitzung faellt beim Verlassen des Vollbilds auf einen Bildschirm "
           + "zurueck."),
        Toggle("span monitors", CatDisplay, "Bildschirme aneinanderreihen",
             "Aeltere Art, mehrere Bildschirme zu benutzen: nur nebeneinander und nur bei "
           + "gleicher Hoehe. \"use multimon\" ist der Nachfolger."),
        Toggle("displayconnectionbar", CatDisplay, "Verbindungsleiste anzeigen",
             "Die Leiste am oberen Rand im Vollbild."),
        Toggle("pinconnectionbar", CatDisplay, "Verbindungsleiste angeheftet",
             "0 laesst die Leiste nach oben wegklappen."),

        // ==================================================== Lokale Geraete
        Pick("audiomode", 'i', CatDevices, "Ton der Sitzung",
             "Wo der Ton des Remotecomputers zu hoeren ist.",
             "0", "Auf diesem Computer wiedergeben",
             "1", "Auf dem Remotecomputer wiedergeben",
             "2", "Nicht wiedergeben"),
        Toggle("audiocapturemode", CatDevices, "Mikrofon weiterreichen",
             "Die Aufnahme dieses Rechners steht in der Sitzung zur Verfuegung."),
        Pick("audioqualitymode", 'i', CatDevices, "Tonqualitaet",
             "Hohe Qualitaet kostet Bandbreite und Verzoegerung.",
             "0", "Dynamisch", "1", "Mittel", "2", "Hoch"),
        Pick("keyboardhook", 'i', CatDevices, "Windows-Tastenkombinationen",
             "Wohin Alt+Tab, die Windows-Taste und aehnliche Griffe gehen.",
             "0", "An diesen Computer", "1", "An den Remotecomputer",
             "2", "Nur im Vollbild an den Remotecomputer"),
        Toggle("redirectclipboard", CatDevices, "Zwischenablage",
             "Kopieren und Einfuegen zwischen beiden Rechnern."),
        Toggle("redirectprinters", CatDevices, "Drucker",
             "Die lokalen Drucker erscheinen in der Sitzung."),
        Toggle("redirectcomports", CatDevices, "Serielle Anschluesse",
             "COM-Anschluesse dieses Rechners in der Sitzung."),
        Toggle("redirectsmartcards", CatDevices, "Smartcards",
             "Kartenleser in der Sitzung - Voraussetzung fuer die Anmeldung mit Karte."),
        Toggle("redirectposdevices", CatDevices, "Kassengeraete",
             "Point-of-Service-Geraete nach Microsoft-PoS-Norm."),
        Toggle("redirectlocation", CatDevices, "Standort",
             "Der Standort dieses Rechners steht der Sitzung zur Verfuegung."),
        Toggle("redirectwebauthn", CatDevices, "WebAuthn / FIDO2",
             "Sicherheitsschluessel und Windows Hello wirken in der Sitzung."),
        Toggle("redirectdirectx", CatDevices, "DirectX",
             "Alte Einstellung, ohne Wirkung seit Windows 8."),
        Text("drivestoredirect", CatDevices, "Laufwerke",
             "Welche Laufwerke die Sitzung sieht. \"*\" bedeutet alle, \"C:\\;D:\\;\" nur diese, "
           + "\"DynamicDrives\" auch spaeter angesteckte. Leer heisst keine."),
        Text("usbdevicestoredirect", CatDevices, "USB-Geraete (RemoteFX)",
             "\"*\" fuer alle oder die Geraetekennungen, durch Semikolon getrennt."),
        Text("camerastoredirect", CatDevices, "Kameras",
             "\"*\" fuer alle oder die Geraetekennungen. Ab Windows 10 1903."),
        Text("devicestoredirect", CatDevices, "Weitere Geraete",
             "Plug-and-Play-Geraete ausser Laufwerken, \"*\" fuer alle."),

        // ========================================================== Leistung
        Pick("connection type", 'i', CatPerformance, "Verbindungsart",
             "Waehlt einen Satz Voreinstellungen fuer die Darstellung. Bei 7 misst mstsc selbst - "
           + "dann zaehlen \"networkautodetect\" und \"bandwidthautodetect\".",
             "1", "Modem (56 kbit/s)", "2", "Niedrig (256 kbit/s - 2 Mbit/s)",
             "3", "Satellit (2 - 16 Mbit/s, hohe Verzoegerung)",
             "4", "Hoch (2 - 10 Mbit/s)", "5", "WAN (10 Mbit/s und mehr, hohe Verzoegerung)",
             "6", "LAN (10 Mbit/s und mehr)", "7", "Automatisch erkennen"),
        Toggle("networkautodetect", CatPerformance, "Netz automatisch messen",
             "Gehoert zur Verbindungsart \"Automatisch erkennen\"."),
        Toggle("bandwidthautodetect", CatPerformance, "Bandbreite automatisch messen",
             "Ebenfalls Teil der automatischen Erkennung."),
        Toggle("compression", CatPerformance, "Komprimierung",
             "Weniger Daten, dafuer etwas mehr Rechenlast auf beiden Seiten."),
        Toggle("bitmapcachepersistenable", CatPerformance, "Bildspeicher auf Platte",
             "Haelt Bildteile ueber das Sitzungsende hinaus vor. Beschleunigt den naechsten "
           + "Verbindungsaufbau."),
        Number("bitmapcachesize", CatPerformance, "Groesse des Bildspeichers",
             "In Kilobyte. Voreinstellung 1500."),
        Pick("videoplaybackmode", 'i', CatPerformance, "Videowiedergabe",
             "1 reicht Videos als Datenstrom durch, statt Einzelbilder zu uebertragen.",
             "0", "Einzelbilder (alt)", "1", "Datenstrom (empfohlen)"),
        Toggle("disable wallpaper", CatPerformance, "Hintergrundbild abschalten",
             "1 spart Bandbreite: Der Desktop der Sitzung bleibt einfarbig."),
        Toggle("allow font smoothing", CatPerformance, "Kantenglaettung der Schrift",
             "Schoenere Schrift, mehr Daten."),
        Toggle("allow desktop composition", CatPerformance, "Desktopgestaltung",
             "Durchscheinende Fenster und aehnliche Effekte. Ohne Wirkung ab Windows 8."),
        Toggle("disable full window drag", CatPerformance, "Fensterinhalt beim Ziehen ausblenden",
             "1 zeigt beim Verschieben nur den Rahmen."),
        Toggle("disable menu anims", CatPerformance, "Menueanimationen abschalten",
             "1 laesst Menues sofort erscheinen statt aufzuklappen."),
        Toggle("disable themes", CatPerformance, "Designs abschalten",
             "1 nimmt der Sitzung das Windows-Design."),
        Toggle("disable cursor setting", CatPerformance, "Zeigerschatten abschalten",
             "1 unterdrueckt Schatten und Animation des Mauszeigers."),

        // ========================================================= Sicherheit
        Pick("authentication level", 'i', CatSecurity, "Serverpruefung",
             "Was geschehen soll, wenn sich der Server nicht ausweisen kann - etwa weil sein "
           + "Zertifikat selbst ausgestellt ist.",
             "0", "Ohne Warnung verbinden",
             "1", "Nicht verbinden",
             "2", "Warnen und nachfragen",
             "3", "Keine Vorgabe"),
        Toggle("enablecredsspsupport", CatSecurity, "Authentifizierung auf Netzwerkebene (NLA)",
             "1 meldet den Benutzer schon vor dem Aufbau der Sitzung an. 0 nur, wenn der Server "
           + "es nicht kann."),
        Toggle("negotiate security layer", CatSecurity, "Sicherheitsebene aushandeln",
             "1 laesst beide Seiten das Verfahren aushandeln (TLS, CredSSP oder RDP-eigen)."),
        Toggle("prompt for credentials", CatSecurity, "Immer nach Anmeldedaten fragen",
             "1 fragt auch dann, wenn ein Kennwort gespeichert ist."),
        Toggle("promptcredentialonce", CatSecurity, "Nur einmal fragen",
             "Dieselben Daten gelten fuer Gateway und Server."),
        Toggle("prompt for credentials on client", CatSecurity, "Abfrage auf diesem Rechner",
             "Die Anmeldedaten werden hier erfragt statt in der Sitzung."),
        Toggle("disable ctrl+alt+del", CatSecurity, "Strg+Alt+Entf nicht verlangen",
             "1 laesst den Begruessungsbildschirm der Sitzung ohne Tastendruck durch."),
        Toggle("enablerdsaadauth", CatSecurity, "Anmeldung mit Entra ID",
             "1 meldet mit einem Konto aus Microsoft Entra ID (frueher Azure AD) an."),

        // ============================================================ Gateway
        Text("gatewayhostname", CatGateway, "Gatewayserver",
             "Name des RD-Gateways, ueber das die Verbindung laeuft."),
        Pick("gatewayusagemethod", 'i', CatGateway, "Gateway benutzen",
             "Ob und wann der Umweg ueber das Gateway genommen wird.",
             "0", "Nicht benutzen",
             "1", "Immer benutzen",
             "2", "Nur wenn keine unmittelbare Verbindung moeglich ist",
             "3", "Standardeinstellung des Rechners",
             "4", "Nicht benutzen, oertliche Adressen umgehen"),
        Pick("gatewaycredentialssource", 'i', CatGateway, "Anmeldung am Gateway",
             "Womit man sich am Gateway ausweist.",
             "0", "Kennwort (NTLM)", "1", "Smartcard", "2", "Beliebig",
             "3", "Systemeigene Anmeldung", "4", "Nachfragen",
             "5", "Cookie-basiert"),
        Pick("gatewayprofileusagemethod", 'i', CatGateway, "Gatewayprofil",
             "Ob die Angaben in dieser Datei die Vorgabe des Rechners ersetzen.",
             "0", "Vorgabe des Rechners", "1", "Angaben aus dieser Datei"),
        Number("gatewaybrokeringtype", CatGateway, "Gateway-Brokerart",
             "Von Microsoft reserviert, Voreinstellung 0."),
        Text("gatewayaccesstoken", CatGateway, "Zugriffstoken",
             "Von einem Portal ausgestelltes Token fuer das Gateway."),
        Toggle("rdgiskdcproxy", CatGateway, "Gateway als KDC-Proxy",
             "1 fuehrt die Kerberos-Anfragen ueber dasselbe Gateway."),
        Text("kdcproxyname", CatGateway, "KDC-Proxy",
             "Abweichender Kerberos-Proxy, falls es nicht das Gateway selbst ist."),

        // ========================================================== RemoteApp
        Toggle("remoteapplicationmode", CatRemoteApp, "RemoteApp-Modus",
             "1 zeigt statt des ganzen Desktops nur ein einzelnes Programm."),
        Text("remoteapplicationname", CatRemoteApp, "Anzeigename",
             "Der Name, der im Fenstertitel und in der Taskleiste erscheint."),
        Text("remoteapplicationprogram", CatRemoteApp, "Programm",
             "Der Alias der RemoteApp oder der Pfad der Anwendung auf dem Server."),
        Text("remoteapplicationcmdline", CatRemoteApp, "Befehlszeile",
             "Was dem Programm als Parameter mitgegeben wird."),
        Toggle("remoteapplicationexpandcmdline", CatRemoteApp, "Umgebungsvariablen in der Befehlszeile",
             "1 loest %VARIABLE% in der Befehlszeile auf - auf diesem Rechner, nicht auf dem Server."),
        Toggle("remoteapplicationexpandworkingdir", CatRemoteApp, "Umgebungsvariablen im Arbeitsordner",
             "Dasselbe fuer den Arbeitsordner."),
        Text("remoteapplicationfile", CatRemoteApp, "Zu oeffnende Datei",
             "Datei, die die RemoteApp beim Start oeffnet."),
        Text("remoteapplicationicon", CatRemoteApp, "Symbol",
             "Symboldatei fuer das Fenster der RemoteApp."),
        Toggle("disableremoteappcapscheck", CatRemoteApp, "Faehigkeitspruefung ueberspringen",
             "1 startet die RemoteApp, ohne den Server vorher zu fragen, ob er sie anbietet."),
        Toggle("remoteappmousemoveinject", CatRemoteApp, "Mausbewegungen einspeisen",
             "Haelt die Sitzung wach und verhindert falsche Leerlauferkennung in RemoteApps."),
        Text("alternate shell", CatRemoteApp, "Alternative Shell",
             "Programm, das statt des Explorers startet. Bei RemoteApp steht hier \"rdpinit.exe\" "
           + "oder der Programmpfad."),
        Text("shell working directory", CatRemoteApp, "Arbeitsordner",
             "Startordner der alternativen Shell."),

        // ========================================================== Erweitert
        new RdpSetting("password 51", 'b', CatAdvanced, "Gespeichertes Kennwort",
             "Mit DPAPI verschluesselt und nur auf dem Rechner und im Konto lesbar, wo es "
           + "gespeichert wurde. Aendern laesst es sich hier nicht sinnvoll - der Haken rechts "
           + "entfernt es aus der Datei.",
             RdpEditorKind.Text),
        new RdpSetting("signature", 'b', CatAdvanced, "Signatur",
             "Unterschrift des Herausgebers. Jede Aenderung an dieser Datei macht sie ungueltig.",
             RdpEditorKind.Text),
        Text("signscope", CatAdvanced, "Signierte Einstellungen",
             "Die Liste der Schluessel, die von der Signatur gedeckt sind."),
        Toggle("public mode", CatAdvanced, "Oeffentlicher Modus",
             "1 verhindert, dass die Sitzung Spuren im Benutzerprofil hinterlaesst."),
        Toggle("allow smart sizing", CatAdvanced, "Einpassen erlauben",
             "Alte Schreibweise von \"smart sizing\"."),
        Toggle("connect to console", CatAdvanced, "Mit Konsole verbinden",
             "Alte Schreibweise von \"administrative session\", bis Windows Server 2003."),
        Number("keyboardlayout", CatAdvanced, "Tastaturbelegung",
             "Kennung der Belegung, die die Sitzung benutzen soll."),
        Toggle("redirectdrives", CatAdvanced, "Laufwerke (alt)",
             "Alte Schreibweise. \"drivestoredirect\" geht vor."),
    };

    private static readonly Dictionary<string, RdpSetting> ByKey =
        All.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

    public static RdpSetting? Find(string key)
        => ByKey.TryGetValue(key.Trim(), out var setting) ? setting : null;

    public static bool Knows(string key) => ByKey.ContainsKey(key.Trim());

    /// <summary>
    /// Baut den Eintrag fuer einen Schluessel, den der Katalog nicht kennt.
    /// Er landet unter "Unbekannt" und laesst sich dort als Text aendern.
    /// </summary>
    public static RdpSetting Unknown(string key, char type)
        => new(key, type, CatUnknown, key,
               type switch
               {
                   'i' => "Zahl. Dieser Schluessel steht nicht im Katalog - der Wert bleibt "
                        + "unveraendert erhalten.",
                   'b' => "Binaerwert. Dieser Schluessel steht nicht im Katalog - der Wert bleibt "
                        + "unveraendert erhalten.",
                   _   => "Text. Dieser Schluessel steht nicht im Katalog - der Wert bleibt "
                        + "unveraendert erhalten.",
               },
               RdpEditorKind.Text);
}
