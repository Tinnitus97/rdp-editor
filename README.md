# RDP-Editor

Ändert **alle** Einstellungen einer `.rdp`-Datei – auch die, für die mstsc
keine Schaltfläche hat.

Der Anlass ist `selectedmonitors`. Die Verbindungsanzeige von mstsc kennt nur
die Frage „alle Monitore verwenden: ja oder nein". *Welche* Monitore die
Sitzung benutzt, steht ausschließlich in der Datei – und diese Zeile schreibt
mstsc niemals von selbst hinein.

Gebaut mit denselben Mitteln wie
[CLInstall](https://github.com/Tinnitus97/CLInstall): **C# auf .NET 10 mit
Avalonia**, veröffentlicht als eine einzige, eigenständige `RdpEditor.exe`
ohne Installation und ohne vorausgesetzte Laufzeitumgebung.

---

## Der Reiter „Bildschirme"

Ein Plan der angeschlossenen Bildschirme, maßstäblich und an ihrer Stelle
zueinander – so, wie sie in den Anzeigeeinstellungen von Windows liegen. Ein
Klick wählt einen Bildschirm aus oder ab.

Drei Dinge nimmt der Reiter einem ab:

**Die Nummern stimmen.** Die Nummer, die in `selectedmonitors` gehört, ist die
Stelle in der Reihenfolge von `EnumDisplayMonitors` – derselben Reihenfolge,
aus der auch mstsc seine Nummern nimmt. Genau so werden die Bildschirme hier
ermittelt, nicht über eine Bildschirmliste des Oberflächen-Rahmenwerks, die
dieselben Geräte in anderer Reihenfolge liefern kann. Zur Probe steht ein Knopf
daneben, der `mstsc /l` öffnet: dieselbe Liste, aus Windows' eigenem Mund.

**Die beiden Schalter kommen mit.** `selectedmonitors` ist wirkungslos ohne
`use multimon:i:1` und `screen mode id:i:2`. Das ist die Falle, in die jeder
tappt, der die Zeile von Hand einfügt – sie steht da, sie ist richtig
geschrieben, und passiert tut nichts. Der Reiter setzt beides mit und schreibt
unter den Plan, was aus der Auswahl in der Datei wird.

**Zusammenhängen wird geprüft.** mstsc verlangt eine zusammenhängende Fläche.
Wer den linken und den rechten Bildschirm wählt, den mittleren aber auslässt,
bekommt eine gültige Datei, die trotzdem alle Bildschirme nimmt. Der Editor
sagt es vorher.

Der erste gewählte Bildschirm wird der **Hauptbildschirm der Sitzung** – dort
erscheinen Anmeldung und Taskleiste. Die Reihenfolge lässt sich mit „Als erster"
ändern.

Und wer eine Datei für einen *anderen* Arbeitsplatz vorbereitet, tippt die
Nummern unten von Hand ein; geprüft wird dann nur, was sich prüfen lässt.

---

## Die übrigen Reiter

| Reiter | was darin steht |
|---|---|
| Bildschirme | `selectedmonitors`, `use multimon`, `screen mode id` – siehe oben |
| Verbindung | Server, Anschluss, Benutzer, Domäne, Lastverteilung, Wiederverbinden |
| Anzeige | Auflösung, Skalierung, Farbtiefe, Fensterlage, Verbindungsleiste |
| Lokale Geräte | Ton, Mikrofon, Zwischenablage, Drucker, Smartcards, Laufwerke, USB, Kameras |
| Leistung | Verbindungsart, Komprimierung, Hintergrundbild, Designs, Bildspeicher |
| Sicherheit | Serverprüfung, NLA, Anmeldeabfrage, Entra ID |
| Gateway | RD-Gateway, Anmeldeart, KDC-Proxy |
| RemoteApp | Programm, Befehlszeile, Symbol, alternative Shell |
| Erweitert | Signatur, gespeichertes Kennwort, alte Schreibweisen |
| Unbekannt | jeder Schlüssel der Datei, den der Katalog nicht kennt |
| Transport | TCP oder UDP - der einzige Reiter, der nicht die Datei ändert |
| Rohansicht | die Datei als Text, so wie sie gespeichert wird |

**Zusammengehörendes steht beieinander.** Jeder Reiter ist in Kästen mit
Überschrift geteilt – „Übertragungsrate", „Folgendes zulassen", „Ton",
„Laufwerke und USB" – statt neunzig Zeilen als eine Kolonne. Die Aufteilung
steht an einer Stelle (`Services/RdpGroups.cs`); ein Test prüft, dass jeder
Schlüssel des Katalogs in genau einem Kasten liegt und kein Kasten einen
Schlüssel nennt, den es nicht gibt.

**Vorgabeknöpfe über der Gruppe – mit den Haken, die Windows setzt.** Ein Knopf
je Übertragungsrate, und dahinter steht nicht meine Schätzung, sondern die
abgelesene Zuordnung aus dem Dialog von mstsc:

| Übertragungsrate | Hintergrund | Schrift­glättung | Desktop­gestaltung | Fenster­inhalt | Animation | Visuelle Stile |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Modem (56 kBit/s) | – | – | – | – | – | – |
| Breitband niedrig (256 kBit/s – 2 MBit/s) | – | – | – | – | – | ✓ |
| Satellit (2 – 16 MBit/s) | – | – | ✓ | – | – | ✓ |
| Breitband hoch (2 – 10 MBit/s) | – | – | ✓ | – | – | ✓ |
| WAN (10 MBit/s+, hohe Latenz) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| LAN (10 MBit/s oder höher) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Genau das tut mstsc auch, wenn man die Rate umstellt – nur ungefragt und ohne
zu sagen, welche Zeilen es dabei ändert. Hier steht am Mauszeiger jede Zeile,
die der Knopf schreibt. „Dauerhafte Bitmapzwischenspeicherung" und „Verbindung
erneut herstellen" bleiben unberührt: Die stehen im Dialog unterhalb des
Kastens und gehören nicht zur Rate. Ein Test führt dieselbe Tabelle in der
Sicht des Dialogs noch einmal – wer die Vorgaben ändert, muss ihn mitändern.

Für die Geräte gibt es dieselben Knöpfe: „Nichts weiterreichen", „Nur
Zwischenablage", „Alle Laufwerke".

**Achtung, umgekehrte Zählweise.** Im mstsc-Dialog heißt es „Desktophintergrund
[x] zulassen"; in der Datei steht dafür `disable wallpaper:i:0`. Die Gruppe
„Folgendes zulassen" sagt das dazu, und jede Zeile ist so beschriftet, wie die
Datei zählt – ein Haken bedeutet dort *abgeschaltet*.

**Neunzig Schlüssel mit Klartext daneben.** In der Datei steht
`authentication level:i:2` – was diese 2 bedeutet, steht nirgends. Ohne den
Klartext ist ein Editor für `.rdp`-Dateien nur ein Texteditor mit
Zeilennummern.

**Der Haken rechts sagt, ob der Schlüssel überhaupt in der Datei steht.** Das
ist nicht dasselbe wie „aus": Fehlt er, entscheidet mstsc, und je nach
Windows-Fassung fällt diese Entscheidung anders aus. Wer eine Einstellung
festnageln will, muss sie hineinschreiben – auch wenn ihr Wert der
Voreinstellung entspricht. Zeilen, die nicht in der Datei stehen, sind deshalb
blaß dargestellt.

**Nichts geht verloren.** Was der Katalog nicht kennt, steht unter „Unbekannt"
und lässt sich dort ebenso ändern. Zeilen, die dem Muster `schlüssel:typ:wert`
gar nicht folgen, bleiben im Wortlaut erhalten. Der Katalog entscheidet über
die Bequemlichkeit, nicht über den Umfang.

**Die Reihenfolge bleibt.** Ein geänderter Wert bleibt an seiner Stelle
stehen, neue Schlüssel kommen ans Ende. Eine Datei, die nach dem Speichern
völlig anders sortiert wäre, ließe sich mit der vorigen Fassung nicht mehr
vergleichen.

---

## Kodierung

Gelesen wird, was kommt: UTF-16 LE oder BE mit Vorzeichen, UTF-8 mit oder ohne,
und UTF-16 ohne Vorzeichen an der Bytefolge erkannt. Wer eine von Hand
angelegte UTF-8-Datei mit einem reinen UTF-16-Leser öffnet, bekommt
chinesische Schriftzeichen statt seiner Serveradresse.

Geschrieben wird immer **UTF-16 LE mit Byte-Order-Mark und CRLF** – genau die
Form, die mstsc selbst schreibt, und die einzige, bei der auch Umlaute in
Benutzernamen und RemoteApp-Titeln zuverlässig ankommen.

---

## Bedienung

| | |
|---|---|
| Öffnen / Speichern / Speichern unter | oben rechts |
| Suchen | über allen Reitern; sucht im Schlüssel, in der Beschriftung und im Hinweis |
| Mit mstsc öffnen | speichert und startet die Verbindung – die Probe aufs Exempel |
| Hell/Dunkel | der Knopf ganz rechts |

Die EXE nimmt einen Dateinamen als Parameter entgegen und lässt sich damit als
„Öffnen mit" für `.rdp`-Dateien eintragen.

Sie fordert **keine** Administratorrechte an: Der Editor schreibt eine
Textdatei, sonst nichts.

---

## TCP oder UDP

**In der `.rdp`-Datei geht das nicht** – es gibt keinen Schlüssel dafür. Der
Transport ist keine Eigenschaft der Verbindung, sondern eine Abmachung zwischen
Client und Server: Der Client bietet UDP an, solange es ihm nicht verboten ist,
der Server nimmt es an, solange seine Richtlinie es zulässt. Kommt UDP nicht
durch, fällt mstsc von selbst auf TCP zurück.

Verboten wird es an zwei Stellen, beide außerhalb der Datei:

| Seite | wo |
|---|---|
| Client | Richtlinienwert `fClientDisableUDP` unter `HKLM\SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services\Client`, Gruppenrichtlinie „UDP auf Client deaktivieren" |
| Server | Gruppenrichtlinie „RDP-Transportprotokolle auswählen" auf dem Sitzungshost |

Der Reiter **Transport** zeigt, was an den drei fraglichen Stellen dieses
Rechners steht, und setzt die Client-Seite auf Wunsch um – mit sichtbarer
UAC-Abfrage, denn dafür braucht es Administratorrechte. Er sagt auch dazu, dass
das den **ganzen Rechner** betrifft und nicht die geöffnete Datei. Wer es
verteilen will, lässt sich dieselbe Änderung als `.reg`-Datei ausgeben.

---

## Was der Editor nicht kann

**Ein Kennwort speichern.** `password 51:b:…` ist mit DPAPI verschlüsselt und
nur auf dem Rechner und im Konto lesbar, wo es gespeichert wurde. Der Editor
zeigt an, ob eines vorhanden ist, und entfernt es auf Wunsch – setzen lässt es
sich nur in mstsc selbst.

**Eine Signatur erneuern.** Ist die Datei signiert, macht jede Änderung die
Unterschrift ungültig; mstsc zeigt die Verbindung danach als „unbekannter
Herausgeber". Der Editor warnt, sobald er eine Signatur sieht.

---

## Bauen

```
dotnet build RdpEditor.csproj -c Release
dotnet run --project tests/RdpEditorTests -c Release
```

Eine einzelne, eigenständige EXE:

```
dotnet publish RdpEditor.csproj -c Release -r win-x64 -o publish \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Beides läuft auch in der Werkbank unter `.github/workflows/build.yml`; die
fertige `RdpEditor.exe` hängt dort an jedem Lauf.

Die Prüfungen kommen ohne Testrahmen und ohne Windows aus – deshalb steht die
Auswahllogik (`MonitorSelection`) getrennt von der Bildschirmermittlung
(`MonitorScan`): Was entschieden wird, ist prüfbar, ohne dass ein Bildschirm
angeschlossen sein muss.
