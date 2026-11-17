# Änderungsprotokoll

## 0.4.1 · 17.11.2026

- Diagnose-Protokoll für den ersten Live-Test: `debate-ai.log` enthält jetzt die Art jedes OpenAI-Ereignisses (gesendet → / empfangen ←) und die Fehlermeldungen von OpenAI im Wortlaut. Keine Audiodaten, keine Gesprächsinhalte, keine Schlüssel.
- Protokoll-Schreiben ist jetzt threadsicher.

## 0.4.0 · 17.11.2026

### Neu
- **Einrichtungsassistent** in 8 Schritten: Szenario, Gast-Eingang, NOVA-Ausgang, Pegel, Stimmprobe, Echoprüfung, empfohlene Einstellungen, Start. Öffnet sich beim ersten Start automatisch.
- Fertige Profile für **TikTok LIVE Studio, OBS Studio, Discord und lokale Debatten** mit Geräteempfehlung und Routing-Anleitung.
- Warnungen bei Echo-gefährdeter Geräteauswahl (Voicemeeter-Haupteingang als NOVA-Ausgang, B1 als Gast-Eingang, gleiches virtuelles Kabel, Lautsprecher).
- **Stimmprobe** mit NOVAs echter Stimme auf dem gewählten Ausgang.
- **Persönlichkeitsprofile** speichern, laden und löschen.

## 0.3.0 · 17.11.2026

**Hauptziel: echte Live-Debatte Mensch gegen NOVA (TikTok LIVE).** Ungetestet auf echter Hardware, siehe `docs/STATUS.md`.

### Neu
- Ereignisgesteuerte Vollduplex-Verbindung zu OpenAI Realtime: Gast-Audio wird fortlaufend gestreamt, Antworten kommen gestreamt zurück.
- Semantische Satzende-Erkennung durch OpenAI (`semantic_vad`) mit einstellbarer Geduld; NOVA entscheidet selbst, wann sie antwortet (keine automatischen Server-Antworten).
- Unterbrechung durch den Gast einstellbar: NOVA stoppt sofort, beendet ihren Satz oder ignoriert (Halbduplex). Unterbrochene Antworten werden im Kontext gekürzt.
- Warteschlange: Beiträge während NOVAs Rede oder einer laufenden Antwort gehen nicht verloren.
- Echo-Schutz: Echoprüfung im Setup, Echo-Erkennung per Hüllkurven-Korrelation, Selbstgespräch-Schutz anhand der Mitschrift mit automatischem Wechsel auf Halbduplex.
- Debattenintelligenz: feste Debattenmethode (Kernbehauptung, wechselnde Gegenzüge, Widersprüche, Position anpassen, Umgang mit Provokationen, keine Floskeln).
- Debattengedächtnis: Protokoll alle 4 Antworten, wird in die Sitzung übernommen; automatische neue Sitzung bei Abbruch oder nach 55 Minuten mit erhaltenem Protokoll.
- NOVA bringt die Debatte nach einstellbarer Stille voran; Knöpfe „NOVA soll sprechen“ und „NOVA stumm“.
- Latenzmessung pro Antwort und Live-Testbericht mit Pflichtabnahme-Tabelle.
- Anleitung für die TikTok-LIVE-Pipeline (`docs/LIVE-SETUP.md`).

### Geändert
- Lokale Satzende-Erkennung lädt den Beitrag schon während des Sprechens hoch (geringere Latenz).
- Unbekannte OpenAI-Fehler während einer Debatte beenden sie nicht mehr, sondern werden protokolliert; nach drei fehlgeschlagenen Antworten in Folge wird abgebrochen.

## 0.2.0 · 17.11.2026

**Hauptziel: NOVA antwortet selbstständig und hörbar.**

### Behoben
- Offline-Probe ist nur noch ohne API-Schlüssel voreingestellt und wird deutlich gelb angezeigt („nur Piepton“).
- Standardmodus ist jetzt „Automatisch“ statt Push-to-test.
- Am Redezeitlimit wird der Beitrag gesendet statt verworfen. Dadurch antwortet NOVA auch auf Videos und Streams ohne Sprechpausen.
- Lange Antworten werden nicht mehr stumm übersprungen (Sicherheitsgrenze 60 s).
- Der KI-Hinweis wird sinngemäß geprüft und einmal wiederholt, statt die Sitzung bei kleinsten Abweichungen abzubrechen.
- Build-Skripte kompilieren alle Dateien in `src\` (vorher feste Dateiliste).

### Neu
- Gestreamte Wiedergabe: NOVA beginnt zu sprechen, sobald ca. 240 ms Antwort-Audio da sind.
- Fairness mit Warteschlange: Eine fertige Antwort wartet auf eine Sprechpause. Spricht der Partner weiter, wird die ungehörte Antwort verworfen, aus dem Kontext gelöscht, und NOVA antwortet danach auf alles zusammen.
- Knöpfe „Jetzt antworten“ und „NOVA unterbrechen“; unterbrochene Antworten werden im Kontext auf den gehörten Teil gekürzt.
- Live-Mitschrift von Gast und NOVA (optional, nur im Arbeitsspeicher).
- KI-Persönlichkeiten: Name, 5 Diskussionsstile, Intensität, Antwortlänge, Stimme, Sprache (Deutsch/Englisch), Thema, Position.
- Neue dunkle Oberfläche mit NOVA-Statussymbol, Wellenform und Statusanzeigen für Zuhören, Nachdenken, Sprechen, API-Verbindung und Fehler.
- Einstellungen werden gespeichert; Knopf „Lokale Daten löschen“.
- Versionsinformationen in der EXE, Inno-Setup-Installer ohne Adminrechte, GitHub-Actions-Build auf Windows.

## 0.1.2 · 10.11.2026
- Erkennung von Standard-Voicemeeter (B1) und Banana (B2/AUX), Routing-Hilfe.

## 0.1.1
- Windows-MVP mit WinMM-Audio, OpenAI Realtime, DPAPI-Schlüssel und Selbsttest.
