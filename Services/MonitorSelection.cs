using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RdpEditor.Services;

/// <summary>
/// Ein Bildschirm, wie Windows ihn meldet.
///
/// <paramref name="Id"/> ist die Nummer, unter der mstsc den Bildschirm kennt:
/// die Stelle in der Reihenfolge von EnumDisplayMonitors, beginnend bei 0.
/// Dieselbe Nummer zeigt "mstsc /l" an, und genau diese Nummern stehen in
/// <c>selectedmonitors</c>.
/// </summary>
public sealed record MonitorEntry(
    int Id,
    string DeviceName,
    string FriendlyName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary,
    int Dpi)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    /// <summary>Die Skalierung in Prozent - 96 dpi sind 100 %.</summary>
    public int ScalePercent => Dpi <= 0 ? 100 : (int)Math.Round(Dpi * 100.0 / 96.0);

    public string Geometry => $"{Width} x {Height} @ {X},{Y}";
}

/// <summary>
/// Alles, was mit <c>selectedmonitors</c> zu tun hat - ohne Windows-Aufrufe,
/// damit es sich auf jedem Laeufer pruefen laesst.
/// </summary>
public static class MonitorSelection
{
    public const string KeySelected = "selectedmonitors";
    public const string KeyMultimon = "use multimon";
    public const string KeyScreenMode = "screen mode id";

    /// <summary>Liest "0,2" als [0, 2]. Alles, was keine Zahl ist, faellt weg.</summary>
    public static List<int> Parse(string? value)
    {
        var ids = new List<int>();
        if (string.IsNullOrWhiteSpace(value)) return ids;

        foreach (var part in value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                continue;
            if (id < 0 || ids.Contains(id)) continue;
            ids.Add(id);
        }

        return ids;
    }

    /// <summary>Schreibt [0, 2] als "0,2" - die Reihenfolge bleibt erhalten.</summary>
    public static string Format(IEnumerable<int> ids) => string.Join(",", ids);

    /// <summary>
    /// Haengen die gewaehlten Bildschirme zusammen?
    ///
    /// mstsc verlangt eine zusammenhaengende Flaeche. Waehlt man den linken
    /// und den rechten Bildschirm, laesst den mittleren aber aus, faellt die
    /// Sitzung wortlos auf alle Bildschirme zurueck - die Datei ist gueltig,
    /// nur tut sie nicht, was darin steht. Deshalb pruefen wir es hier und
    /// sagen es vorher.
    ///
    /// Zwei Bildschirme gelten als benachbart, wenn sich ihre Rechtecke
    /// beruehren oder ueberlappen. Von dort aus wird der Graph durchlaufen;
    /// bleibt einer uebrig, haengt die Auswahl nicht zusammen.
    /// </summary>
    public static bool IsContiguous(IReadOnlyList<MonitorEntry> selected)
    {
        if (selected.Count <= 1) return true;

        var seen = new HashSet<int> { 0 };
        var queue = new Queue<int>();
        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            for (var j = 0; j < selected.Count; j++)
            {
                if (seen.Contains(j)) continue;
                if (!Touches(selected[i], selected[j])) continue;
                seen.Add(j);
                queue.Enqueue(j);
            }
        }

        return seen.Count == selected.Count;
    }

    private static bool Touches(MonitorEntry a, MonitorEntry b)
        => a.X <= b.Right && b.X <= a.Right && a.Y <= b.Bottom && b.Y <= a.Bottom;

    /// <summary>
    /// Schreibt die Auswahl in die Datei - samt der beiden Schalter, ohne die
    /// sie wirkungslos bliebe.
    ///
    /// <c>selectedmonitors</c> greift nur, wenn die Sitzung ueberhaupt mehrere
    /// Bildschirme benutzt (<c>use multimon:i:1</c>) und im Vollbild laeuft
    /// (<c>screen mode id:i:2</c>). Das ist die Falle, in die jeder tappt, der
    /// die Zeile von Hand einfuegt: Sie steht da, sie ist richtig geschrieben -
    /// und passiert tut nichts.
    ///
    /// Sind alle oder gar keine Bildschirme gewaehlt, verschwindet die Zeile
    /// wieder: "alle" ist genau das, was ohne sie geschieht.
    /// </summary>
    public static void Apply(RdpDocument doc, IReadOnlyList<int> ids, int monitorCount)
    {
        if (ids.Count == 0 || (monitorCount > 0 && ids.Count >= monitorCount))
        {
            doc.Remove(KeySelected);
            doc.SetInt(KeyMultimon, ids.Count == 0 ? 0 : 1);
            if (ids.Count > 0) doc.SetInt(KeyScreenMode, 2);
            return;
        }

        doc.Set(KeySelected, 's', Format(ids));
        doc.SetInt(KeyMultimon, 1);
        doc.SetInt(KeyScreenMode, 2);
    }

    /// <summary>
    /// Der Satz, der unter dem Monitorplan steht: was die Datei nach dieser
    /// Auswahl tatsaechlich tut.
    /// </summary>
    public static string Describe(IReadOnlyList<int> ids, int monitorCount)
    {
        if (ids.Count == 0)
            return "Ein Bildschirm. \"use multimon\" steht auf 0, \"selectedmonitors\" entfaellt.";

        if (monitorCount > 0 && ids.Count >= monitorCount)
            return "Alle Bildschirme. \"use multimon\" steht auf 1, \"selectedmonitors\" entfaellt - "
                 + "ohne die Zeile nimmt die Sitzung ohnehin alle.";

        var list = Format(ids);
        return $"Nur Bildschirm {list}. Geschrieben werden \"selectedmonitors:s:{list}\", "
             + "\"use multimon:i:1\" und \"screen mode id:i:2\".";
    }
}
