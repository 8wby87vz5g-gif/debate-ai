# Änderungsprotokoll

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
