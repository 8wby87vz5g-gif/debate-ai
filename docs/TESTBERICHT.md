# Testbericht · DEBATE AI 0.2.0

Datum: 17.11.2026 · Umgebung: Linux-Entwicklungsumgebung, Mono JIT compiler version 6.8.0.105 (Deb

## Was geprüft wurde

| Prüfung | Werkzeug | Ergebnis |
|---|---|---|
| Ausgangsstand 0.1.2 kompiliert als C# 5 (Sprachstand des Windows-.NET-Framework-Compilers) | `mcs -langversion:5 -warnaserror+` | bestanden |
| Ausgangsstand 0.1.2: 12 von 13 Selbsttests | Mono | bestanden (WinMM-Test nur unter Windows möglich) |
| Neuer Stand 0.2.0 kompiliert als C# 5 ohne Warnungen | `mcs -langversion:5 -warnaserror+` | bestanden |
| Neuer Stand 0.2.0: 21 Logik- und Protokolltests | Mono, `tests/Tests.cs` | **21/21 bestanden**, Windows-Gerätetest übersprungen |
| Oberfläche startet und ist lesbar angeordnet | Mono WinForms auf virtuellem Bildschirm (Xvfb), Screenshot | bestanden; Darstellung unter Windows kann abweichen |
| Website `npm run typecheck` und `npm run build` | Node 22 | bestanden |

## Abgedeckte Testfälle (Auszug)

- Automatischer Dialog: Sprache → Pause → Antwort → Nachlauf → zuhören; NOVAs eigene Ausgabe erreicht den Sprecherwechsel nicht (Echo-Sperre).
- Redezeitlimit sendet den Beitrag (Regression des Hauptfehlers aus 0.1.x).
- Fairness: fertige Antwort wartet auf Pause; kurzes Geräusch verwirft sie nicht; Weitersprechen verwirft die ungehörte Antwort, setzt die Aufnahme fort und beantwortet sie danach.
- Streaming: Wiedergabe beginnt vor dem Ende der Antwort; das gesamte Audio wird wiedergegeben.
- Unterbrechen: Wiedergabe stoppt, Kontext wird auf die gehörten Millisekunden gekürzt, Rest der Antwort wird nicht abgespielt.
- Realtime-Protokoll mit simuliertem Server: Sitzungskonfiguration (Format 24 kHz, Stimme, Transkription), Chunking, Mitschrift-Ereignisse, `conversation.item.delete` und `conversation.item.truncate`, leerer Beitrag, API-Fehler ohne Weitergabe vertraulicher Fehlertexte, Abbruch.
- Persona: Stil, Intensität, Länge, Thema, Position, Steuerzeichen-Filter, Sicherheitsregeln am Ende, KI-Hinweis-Erkennung.
- Pause, Ruhe, Not-Aus, Fehler mit anschließendem Neustart, verspätete Antworten nach Pause.

## Was NICHT geprüft wurde

Diese Punkte sind implementiert, aber **ungeprüft**, weil die Entwicklungsumgebung weder Windows-Audio noch einen OpenAI-Schlüssel hat:

- Echte Windows-Kompilierung mit `csc.exe` und Ausführung von `Selbsttest.cmd` → erfolgt automatisch im GitHub-Actions-Job `windows`, sobald das Repository hochgeladen ist.
- WinMM-Aufnahme und gestreamte Wiedergabe auf echter Soundkarte, Voicemeeter, virtuelle Kabel.
- Echte OpenAI-Realtime-Sitzung: Antwortqualität, Latenz, Kosten, Verhalten bei Netzwerkabbruch.
- DPAPI wurde nur mit Monos Ersatzimplementierung getestet; der Windows-Test läuft in der CI.
- Installer (Inno Setup) → CI-Job.
- TikTok LIVE Studio, OBS, Discord.

Für diese Punkte gibt es die Schritt-für-Schritt-Abnahme in `docs/HARDWARE-TEST.md`.

## Rohausgabe des Testlaufs

```text
OK  PCM-Konvertierung und Testton
OK  Spracherkennung, kurze Signale und Aufnahmelimit
OK  Automatischer Dialog mit Rückkopplungssperre
OK  Redezeitlimit sendet den Beitrag statt ihn zu verwerfen
OK  Fairness: fertige Antwort wartet auf Sprechpause
OK  Fairness: Weitersprechen verwirft ungehörte Antwort, Warteschlange
OK  Streaming: NOVA spricht vor dem Ende der Antwort
OK  NOVA unterbrechen kürzt die Antwort im Kontext
OK  Jetzt antworten im Automatikmodus
OK  Persönlichkeit, Position, Antwortlänge und KI-Hinweis
OK  Ruhe bricht Antwort ab und verwirft verspätetes Audio
OK  Push-to-test, Pause und verspätete Antwort
OK  Pegel und Testton ohne Debattensitzung
OK  Verbindungsfehler und anschließender Neustart
OK  Realtime-Sitzung, Chunking und Antwort-Audio mit Testserver-Ereignissen
OK  Fehlender KI-Hinweis, API-Fehler und Abbruch
OK  Realtime: Mitschrift, Löschen und Kürzen ungehörter Antworten
OK  Realtime: leerer Beitrag fordert nur eine Antwort an
OK  Nutzungsprotokoll enthält ausschließlich Tokenzahlen
OK  Windows-Verschlüsselung für aktuelles Benutzerkonto
    Übersprungen: nur unter Windows ausführbar (WinMM).
OK  Windows-Geräteliste und Fensteraufbau
Exit: 0
```
