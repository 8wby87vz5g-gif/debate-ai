# Testbericht · DEBATE AI 0.7.1

Datum: 17.11.2026 · Umgebung: Linux-Entwicklungsumgebung, Mono JIT compiler version 6.8.0.105 (Deb

## Ergebnis auf einen Blick

| Prüfung | Ergebnis |
|---|---|
| Kompilierung als C# 5 ohne Warnungen (Sprachstand des Windows-Compilers) | bestanden |
| 49 automatische Logik-, Protokoll- und Signaltests (+1 Windows-Test übersprungen) | **49/49 bestanden, 3 Wiederholungen stabil** |
| Oberfläche startet, Anordnung lesbar; Einrichtungsassistent öffnet beim ersten Start (Mono-WinForms auf virtuellem Bildschirm) | bestanden |
| Website `typecheck` und `build` | bestanden (Stand 0.2.0, unverändert) |
| Lokale Debatte mit echter OpenAI-API auf deinem Windows-PC | **bestanden laut deinem Bericht** (ca. 1–2 s Reaktionszeit) |
| TikTok-Prüfdialog startet und zeigt ohne Voicemeeter ehrlich „nicht installiert“, keine Startfreigabe (Mono, virtueller Bildschirm) | bestanden |
| **Live-Pflichtabnahme mit echtem Menschen über TikTok LIVE** | **nicht durchgeführt, ungetestet** |
| Voicemeeter-Fernsteuerung gegen echtes Voicemeeter Banana | **ungetestet** (nur gegen simuliertes Mischpult) |
| Lokaler Modus mit echtem whisper.cpp, Ollama und Piper | **bestanden auf GitHub-Windows-VM** (4 Threads, ohne GPU): Download, Messung, je 3 Debattenrunden mit 4 Modellen, Reaktionszeit 2,5–8,1 s je nach Modell und Testrechner; Werte in `docs/LOKALE-KI.md`. **Auf deinem PC ungetestet.** |
| Lizenzwerkzeug: Schlüssel erzeugen und Eigentümer-Lizenz signieren | bestanden (Mono; unter Windows in der CI) |
| Echte Windows-Audiogeräte, echte OpenAI-Realtime-Sitzung, Latenz | **nicht durchgeführt, ungetestet** |

## Echter Windows-Build (GitHub Actions, windows-latest)

Erster Lauf nach dem Hochladen, 17.11.2026, Lauf 44065361271:

| Schritt | Ergebnis |
|---|---|
| Kompilierung mit dem in Windows enthaltenen C#-Compiler (`Neu-bauen.cmd`, Warnungen = Fehler) | bestanden |
| `Selbsttest.cmd` unter Windows: 37 Tests inklusive echter DPAPI-Verschlüsselung, WinMM-Geräteabfrage und Fensteraufbau | **37/37 bestanden** (der Build-Server hat keine Audiogeräte: „0 Eingänge, 0 Ausgänge“) |
| Installer bauen (Inno Setup 6) | `DEBATE-AI-Setup-0.4.0.exe`, 2,1 MB |
| Installer still installieren, Dateien und Version 0.4.0 prüfen | bestanden |
| Installierte App starten | läuft 8 s stabil, Fenster „DEBATE AI 0.4.0 · NOVA“ |
| Deinstallation | bestanden, App entfernt |
| Release `v0.4.0` mit Installer und portabler ZIP | angelegt |

Weiterhin **ungetestet**: echte Audiogeräte, echte OpenAI-Sitzung, TikTok LIVE Studio, Latenz.

## Was die automatischen Tests abdecken

- **Sprecherwechsel:** lokal (Pegel und Pause) mit Upload während des Sprechens; semantisch über OpenAI-Ereignisse; Redezeitlimit.
- **Vollduplex und Unterbrechung:** Gast unterbricht → NOVA stoppt, Kontext wird auf die gehörten Millisekunden gekürzt, Rest wird nicht abgespielt; Variante „Satz beenden“ mit Vormerkung; Halbduplex; Rückkehr zum Zuhören, wenn keine Sprache bestätigt wird.
- **Warteschlange:** Weitersprechen verwirft ungehörte Antworten (lokal und OpenAI-Modus); neue Antwort erst nach Ende der alten; beide Beiträge werden beantwortet.
- **Echo:** Echo-Erkennung mit synthetischem, sprachähnlichem Signal: Echo (200–600 ms Verzögerung, 10–30 % Pegel) ≥ 80 % erkannt; unabhängiger Gast ≤ 5 % fälschlich als Echo. NOVAs eigenes Echo löst während einer laufenden Antwort keine Unterbrechung aus. Echoprüfung meldet Echo / kein Echo / unklar korrekt. Selbstgespräch-Schutz schaltet auf Halbduplex.
- **Debattenführung:** proaktive Nachfrage genau einmal pro Stille; Stummschalten; Gedächtnis-Anforderung nach 4 Antworten und Übernahme in die Sitzung; neue Sitzung nach Verbindungsende mit Gesprächsstand.
- **Fehler:** Zeitlimit ohne Antwort, einzelne vs. wiederholte Fehlschläge, ungültiger Schlüssel, Guthaben, Warnungen ohne Abbruch, Verbindungsende, keine vertraulichen Fehlertexte in Meldungen.
- **Realtime-Protokoll** gegen einen simulierten Server: Sitzungskonfiguration (`semantic_vad`, `create_response:false`, `interrupt_response:false`, Geduld, Retention-Ratio, Transkription, Stimme), KI-Hinweis außerhalb des Verlaufs, Audio-Streaming in Stücken, Antwortzuordnung per `response_id`, Gedächtnis als Text-Antwort außerhalb des Verlaufs, `response.cancel`, `conversation.item.delete`, `conversation.item.truncate`.
- **Latenzmessung** und Live-Testbericht.
- **Lokaler Modus:** Release-Auswahl (neuestes whisper.cpp-Release ohne Windows-Dateien wird übersprungen, Vorabversionen ignoriert), Resampling 24/22,05/16 kHz, WAV, Satzaufteilung, Whisper-Bereinigung, Sicherheitssperre (OpenAI- und LAN-Adressen abgelehnt), Pipeline Spracherkennung → Sprachmodell → satzweise Stimme, Gesprächsverlauf und Zusatzanweisung, Kürzen auf Gehörtes, Verwerfen, kein Beitragsverlust bei Abbruch während der Spracherkennung, Gedächtnis, komplette Debatte über die Gesprächssteuerung ohne Kosten, Hardware-Empfehlung, Download-Auswahl, Ollama-Anfrage, Messung.
- **Lizenz:** gültige Eigentümer-Lizenz, fremder PC, veränderter Inhalt, fremde Signatur, fehlende Gerätebindung, Ablaufdatum, Formatfehler.
- **TikTok-Integration:** Voicemeeter-Routing-Plan (Banana- und Potato-Indizes, Pegelkanäle), Anwenden und Rücklesen mit Fehlererkennung; Routing-Messung gegen ein simuliertes Mischpult (richtige Wege bestätigt, Rückweg NOVA → B2 und fehlender Gast-Weg erkannt); Gastton-Test; LIVE-Studio-Erkennung; Kostenformel und Kostenlimit.
- **Einrichtung:** Geräteempfehlung je Szenario (Banana B2/AUX, Standard-Voicemeeter B1, Mikrofon/Kopfhörer), Warnungen bei Echo-gefährdetem Routing, Persönlichkeitsprofile (Rundlauf, ungültige Werte), Stimmprobe inklusive Fehlerfall.

## Grenzen dieser Tests

- Die Echo-Tests nutzen synthetische Signale, keine echte Raumakustik oder Voicemeeter-Latenz.
- Das Realtime-Protokoll ist gegen die aktuelle OpenAI-Dokumentation implementiert, aber nicht gegen den echten Server getestet.
- Die Qualität der Debattenführung (Gegenargumente, Gedächtnis, Umgang mit Provokationen) lässt sich nur im echten Gespräch beurteilen.

Die Live-Abnahme ist in `docs/HARDWARE-TEST.md` beschrieben; die App erzeugt dafür einen Bericht mit gemessener Latenz.

## Rohausgabe des Testlaufs

```text
OK  PCM-Konvertierung und Testton
OK  Spracherkennung, kurze Signale und Aufnahmelimit
OK  Lokaler Sprecherwechsel: Streaming-Upload, Antwort, Halbduplex
OK  Redezeitlimit sendet den Beitrag
OK  Semantischer Sprecherwechsel (OpenAI) mit Latenzmessung
OK  Fairness: fertige Antwort wartet auf Sprechpause
OK  Warteschlange: Weitersprechen verwirft ungehörte Antwort (OpenAI-Turns)
OK  Warteschlange: Weitersprechen verwirft ungehörte Antwort (lokal)
OK  Vollduplex: Gast unterbricht, NOVA stoppt und kürzt den Kontext
OK  Vollduplex: Gast unterbricht, NOVA beendet ihren Satz und antwortet danach
OK  Ohne Echoprüfung: Unterbrechung erst nach Aufwärmen der Echo-Erkennung
OK  Echo-Schutz: NOVAs eigene Stimme löst keine Unterbrechung aus
OK  Selbstgespräch-Schutz schaltet auf Halbduplex
OK  NOVA bringt die Debatte nach Stille voran
OK  NOVA stumm: hört zu, antwortet nicht
OK  Debattengedächtnis wird erstellt und übernommen
OK  Verbindungsabbruch: neue Sitzung mit Gedächtnis
OK  Fehlgeschlagene Antworten und Zeitlimit
OK  NOVA unterbrechen per Knopf
OK  Jetzt antworten und NOVA soll sprechen
OK  Push-to-talk, Pause und verspätete Antwort
OK  Ruhe in der Debatte, Not-Aus, Fehler und Neustart
OK  Pegel und Testton ohne Debattensitzung
OK  Echoprüfung: Echo, kein Echo, unklar
OK  Echo-Erkennung unterscheidet Echo und Gast
OK  Live-Messwerte und Testprotokoll
OK  Persönlichkeit, Debattenmethode, Gedächtnis, KI-Hinweis
OK  Einrichtungsassistent: Geräteempfehlung je Szenario
OK  Einrichtungsassistent: Warnungen bei Echo-gefährdetem Routing
OK  Persönlichkeitsprofile speichern und laden
OK  Stimmprobe spielt NOVAs Stimme und schließt die Sitzung
OK  Voicemeeter: TikTok-Routing-Plan, Anwenden, Rücklesen
OK  Routing-Messung erkennt richtige und falsche Wege
OK  Gastton-Test und LIVE-Studio-Erkennung
OK  Kostenschätzung, Kostenlimit und Gast ausreden lassen
OK  Stream-Overlay: lokale Seite, Zustand, Sprechpegel
OK  Lokal: Resampling, WAV, Satzaufteilung, Whisper-Bereinigung
OK  Lokal: Sicherheitssperre erlaubt nur Dienste auf diesem PC
OK  Lokal: NOVA-Pipeline Spracherkennung → Sprachmodell → Stimme
OK  Lokal: Unterbrechen, Verwerfen und Gedächtnis ohne Verlust
OK  Lokal: komplette Debatte über die Gesprächssteuerung
OK  Lokal: Hardware-Empfehlung, Downloadauswahl, Ollama-Anfrage, Messung
OK  Eigentümer-Lizenz: Signatur, Gerätebindung, Manipulation, Ablauf
OK  Realtime: Sitzungsaufbau, semantische VAD, KI-Hinweis
OK  Realtime: Live-Ereignisse, Antwort-Audio, Gedächtnis, Kürzen
OK  Realtime: API-Fehler, Warnungen und Verbindungsende
OK  Nutzungsprotokoll enthält ausschließlich Tokenzahlen
OK  Windows-Verschlüsselung für aktuelles Benutzerkonto
    Übersprungen: nur unter Windows ausführbar (WinMM).
OK  Windows-Geräteliste und Fensteraufbau
```
