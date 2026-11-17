# Testbericht · DEBATE AI 0.4.0

Datum: 17.11.2026 · Umgebung: Linux-Entwicklungsumgebung, Mono JIT compiler version 6.8.0.105 (Deb

## Ergebnis auf einen Blick

| Prüfung | Ergebnis |
|---|---|
| Kompilierung als C# 5 ohne Warnungen (Sprachstand des Windows-Compilers) | bestanden |
| 36 automatische Logik-, Protokoll- und Signaltests (+1 Windows-Test übersprungen) | **36/36 bestanden, 4 Wiederholungen ohne Ausfall** |
| Oberfläche startet, Anordnung lesbar; Einrichtungsassistent öffnet beim ersten Start (Mono-WinForms auf virtuellem Bildschirm) | bestanden |
| Website `typecheck` und `build` | bestanden (Stand 0.2.0, unverändert) |
| **Live-Pflichtabnahme mit echtem Menschen über TikTok LIVE** | **nicht durchgeführt, ungetestet** |
| Echte Windows-Audiogeräte, echte OpenAI-Realtime-Sitzung, Latenz | **nicht durchgeführt, ungetestet** |

## Was die automatischen Tests abdecken

- **Sprecherwechsel:** lokal (Pegel und Pause) mit Upload während des Sprechens; semantisch über OpenAI-Ereignisse; Redezeitlimit.
- **Vollduplex und Unterbrechung:** Gast unterbricht → NOVA stoppt, Kontext wird auf die gehörten Millisekunden gekürzt, Rest wird nicht abgespielt; Variante „Satz beenden“ mit Vormerkung; Halbduplex; Rückkehr zum Zuhören, wenn keine Sprache bestätigt wird.
- **Warteschlange:** Weitersprechen verwirft ungehörte Antworten (lokal und OpenAI-Modus); neue Antwort erst nach Ende der alten; beide Beiträge werden beantwortet.
- **Echo:** Echo-Erkennung mit synthetischem, sprachähnlichem Signal: Echo (200–600 ms Verzögerung, 10–30 % Pegel) ≥ 80 % erkannt; unabhängiger Gast ≤ 5 % fälschlich als Echo. NOVAs eigenes Echo löst während einer laufenden Antwort keine Unterbrechung aus. Echoprüfung meldet Echo / kein Echo / unklar korrekt. Selbstgespräch-Schutz schaltet auf Halbduplex.
- **Debattenführung:** proaktive Nachfrage genau einmal pro Stille; Stummschalten; Gedächtnis-Anforderung nach 4 Antworten und Übernahme in die Sitzung; neue Sitzung nach Verbindungsende mit Gesprächsstand.
- **Fehler:** Zeitlimit ohne Antwort, einzelne vs. wiederholte Fehlschläge, ungültiger Schlüssel, Guthaben, Warnungen ohne Abbruch, Verbindungsende, keine vertraulichen Fehlertexte in Meldungen.
- **Realtime-Protokoll** gegen einen simulierten Server: Sitzungskonfiguration (`semantic_vad`, `create_response:false`, `interrupt_response:false`, Geduld, Retention-Ratio, Transkription, Stimme), KI-Hinweis außerhalb des Verlaufs, Audio-Streaming in Stücken, Antwortzuordnung per `response_id`, Gedächtnis als Text-Antwort außerhalb des Verlaufs, `response.cancel`, `conversation.item.delete`, `conversation.item.truncate`.
- **Latenzmessung** und Live-Testbericht.
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
OK  Realtime: Sitzungsaufbau, semantische VAD, KI-Hinweis
OK  Realtime: Live-Ereignisse, Antwort-Audio, Gedächtnis, Kürzen
OK  Realtime: API-Fehler, Warnungen und Verbindungsende
OK  Nutzungsprotokoll enthält ausschließlich Tokenzahlen
OK  Windows-Verschlüsselung für aktuelles Benutzerkonto
    Übersprungen: nur unter Windows ausführbar (WinMM).
OK  Windows-Geräteliste und Fensteraufbau
```
