using System;
using System.Collections.Generic;
using System.Linq;

namespace RdpEditor.Services;

/// <summary>
/// Eine Übertragungsrate aus dem Dialog von mstsc samt der Schalter, die
/// Windows beim Umstellen mitsetzt.
/// </summary>
public sealed record RdpRate(int ConnectionType, string Title, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Was beim Umstellen der Übertragungsrate sonst noch geschieht.
///
/// In mstsc ist die Auswahlliste keine Angabe, sondern ein Schalter: Wer sie
/// umstellt, dem setzt Windows die sechs Kästchen darunter mit um - ohne zu
/// sagen, welche. Dieser Editor tut dasselbe, nur sichtbar: Die Haken springen
/// vor den Augen des Benutzers auf ihren neuen Stand, und in der Fußzeile
/// steht, dass es geschehen ist.
///
/// Die Zuordnung ist abgelesen, nicht geraten - für jede der sechs Raten wurde
/// der Dialog geöffnet und notiert, welche Kästchen danach gefüllt sind:
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
/// ACHTUNG, umgekehrte Zählweise: Vier der sechs Schlüssel heißen in der Datei
/// "disable ...". Ein gefülltes Kästchen im Dialog ist dort eine 0. Der Test
/// führt dieselbe Tabelle noch einmal in der Sicht des Dialogs und rechnet
/// selbst um; wer hier etwas ändert, muss ihn mitändern.
///
/// Nicht dabei sind "Dauerhafte Bitmapzwischenspeicherung" und "Verbindung
/// erneut herstellen": Sie stehen im Dialog unterhalb des Kastens und bleiben
/// von der Rate unberührt. Ebenso "disable cursor setting" - dafür hat der
/// Dialog gar kein Kästchen.
/// </summary>
public static class RdpRates
{
    public const string KeyConnectionType = "connection type";

    /// <summary>Die sechs Kästchen des Dialogs, in seiner Reihenfolge.</summary>
    public static readonly (string Key, bool Inverted)[] Switches =
    {
        ("disable wallpaper", true),
        ("allow font smoothing", false),
        ("allow desktop composition", false),
        ("disable full window drag", true),
        ("disable menu anims", true),
        ("disable themes", true),
    };

    private static RdpRate Rate(int type, string title,
                                bool wallpaper, bool fontSmoothing, bool composition,
                                bool fullWindowDrag, bool menuAnims, bool themes)
    {
        var haken = new[] { wallpaper, fontSmoothing, composition, fullWindowDrag, menuAnims, themes };

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Eine feste Rate schließt das Messen aus - sonst überschriebe die
            // Messung beim Verbinden gerade das, was hier gewählt wurde.
            ["networkautodetect"] = "0",
            ["bandwidthautodetect"] = "0",
        };

        for (var i = 0; i < Switches.Length; i++)
        {
            var (key, inverted) = Switches[i];
            // Erlaubt heißt in der Datei: NICHT abgeschaltet.
            values[key] = haken[i] ^ inverted ? "1" : "0";
        }

        return new RdpRate(type, title, values);
    }

    public static readonly IReadOnlyList<RdpRate> All = new[]
    {
        Rate(1, "Modem (56 kBit/s)",
             false, false, false, false, false, false),

        Rate(2, "Breitband mit niedriger Übertragungsrate (256 kBit/s - 2 MBit/s)",
             false, false, false, false, false, true),

        Rate(3, "Satellit (2 MBit/s - 16 MBit/s mit häufiger Latenz)",
             false, false, true, false, false, true),

        Rate(4, "Breitband mit hoher Übertragungsrate (2 MBit/s - 10 MBit/s)",
             false, false, true, false, false, true),

        Rate(5, "WAN (10 MBit/s oder höher mit häufiger Latenz)",
             true, true, true, true, true, true),

        Rate(6, "LAN (10 MBit/s oder höher)",
             true, true, true, true, true, true),

        // Die Messung entscheidet - die sechs Kästchen bleiben, wie sie sind.
        new RdpRate(7, "Automatisch erkennen", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["networkautodetect"] = "1",
            ["bandwidthautodetect"] = "1",
        }),
    };

    public static RdpRate? Find(int connectionType)
        => All.FirstOrDefault(r => r.ConnectionType == connectionType);
}
