# DEBATE AI 0.4.0 · Live-Abnahme auf echter Hardware

Die Live-Funktion gilt erst als fertig, wenn Teil B mit einem echten Menschen bestanden und dokumentiert ist. Diese Tests sind **bisher nicht durchgeführt** (Stand 17.11.2026), weil die Entwicklungsumgebung weder Windows-Audio noch einen OpenAI-Schlüssel noch TikTok hat.

**Vorbereitung:** Installer `DEBATE-AI-Setup-0.4.0.exe` oder `Neu-bauen.cmd` + `Selbsttest.cmd` (muss mit lauter `OK` enden). Routing nach `docs/LIVE-SETUP.md`, am einfachsten mit dem **Einrichtungsassistenten** (Szenario „TikTok LIVE Studio“).

## Teil A · Technische Vorprüfung (ca. 15 Minuten, ohne Gast)

| Nr. | Test | Bestanden, wenn … | Ergebnis |
|---|---|---|---|
| A1 | App starten | Dunkles Fenster, Geräte in beiden Listen | |
| A2 | Testton auf NOVAs Ausgang | Ton nur dort, wo er hin soll | |
| A3 | **Echoprüfung** | „Kein Echo“ (bei getrenntem Routing) | |
| A4 | API-Schlüssel, Einwilligung, Debatte starten | NOVA sagt hörbar, dass sie eine KI ist | |
| A5 | Selbst einen Satz sagen, schweigen | NOVA antwortet **ohne Klick** | |
| A6 | Mitten im Satz kurz nachdenken (1 s Pause), dann weiterreden | NOVA redet nicht in die Denkpause hinein | |
| A7 | Während NOVA spricht, deutlich dazwischenreden | „NOVA stoppt sofort“: sie verstummt und hört zu; „beendet ihren Satz“: sie spricht zu Ende und antwortet dann | |
| A8 | Nach NOVAs Antwort 20 s schweigen | Kein Selbstgespräch; mit „Nachfrage nach s“ = 10 genau **eine** Nachfrage | |
| A9 | WLAN kurz trennen | Verständliche Meldung, Verbindung wird erneuert oder Neustart möglich | |
| A10 | Falscher Schlüssel `sk-test-ungueltig-123456` | Meldung „API-Schlüssel abgelehnt“ | |

## Teil B · Pflichtabnahme mit echtem Menschen über TikTok LIVE (ca. 15 Minuten)

Ablauf: Live gehen (oder Übungsmodus), einen eingeweihten Gast zuschalten, Thema vorgeben (z. B. „Sollte es ein Tempolimit auf Autobahnen geben?“), NOVAs Position einstellen. Nach dem Gespräch **Live-Testbericht speichern** klicken. Die Datei enthält automatisch gemessene Latenzen und Zählwerte sowie die Tabelle unten zum Ausfüllen.

| Nr. | Anforderung | Wie prüfen |
|---|---|---|
| 1 | Ein echter Mensch spricht über einen getrennten Audioeingang | Gast über LIVE Studio → VAIO → B2 |
| 2 | NOVA erkennt seine Aussage | Mitschrift „Gast: …“ stimmt inhaltlich |
| 3 | NOVA antwortet selbstständig über den konfigurierten Ausgang | Kein Klick während des Gesprächs |
| 4 | Der Mensch hört NOVA über die Streaming-Verbindung | Gast bestätigt es mündlich |
| 5 | Mindestens zehn zusammenhängende Gesprächswechsel | Bericht: „Gesprochene NOVA-Antworten“ ≥ 10 |
| 6 | NOVA erinnert sich und reagiert auf Gegenargumente | Gast bezieht sich auf etwas von vor 5+ Wechseln; NOVA greift es korrekt auf |
| 7 | Keine störenden Echos oder Rückkopplungen | Bericht: Selbstgespräch-Schleifen = 0; Gast und Publikum hören nichts doppelt |
| 8 | Antwortlatenz gemessen und dokumentiert | Bericht: Latenz Ø / Median / p95 |

Zusätzlich empfohlen: Gast provoziert einmal bewusst („Du bist doch nur ein dummer Bot“). Erwartet: NOVA bleibt ruhig, sachlich, nicht beleidigend.

Bitte den gespeicherten Bericht (`live-test-….md` im Protokollordner) und `debate-ai.log` an mich zurückgeben. Erst dann wird die Live-Funktion in `docs/STATUS.md` als geprüft markiert.

## Wenn etwas nicht klappt

- **NOVA antwortet nicht:** Gelber Hinweis „Offline-Probe aktiv“? Haken entfernen. Steht der Status dauerhaft auf „Gesprächspartner spricht“? Gastton ist dauerhaft laut (Musik): Sprecherwechsel „lokal“ mit Fairness aus nutzen.
- **NOVA unterbricht sich selbst / antwortet sich selbst:** Echoprüfung wiederholen, Routing korrigieren. Die App schaltet bei erkanntem Selbstgespräch automatisch auf Halbduplex.
- **NOVA redet zu früh:** Geduld auf „Geduldig“.
- **NOVA reagiert zu träge:** Geduld „Ausgewogen“, Antwortlänge „Kurz“.
