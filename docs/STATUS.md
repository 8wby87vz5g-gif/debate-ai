# DEBATE AI · Projektstatus

Stand: 17.11.2026 · Windows-Client 0.7.1

Legende: ✅ umgesetzt und automatisch getestet · ⚪ **ungetestet auf echter Hardware / mit echter OpenAI-Verbindung / mit TikTok** · 🟡 teilweise · ❌ fehlt

> **Fortschritt:** Lokale Debatte mit echter OpenAI-Verbindung auf deinem Windows-PC funktioniert (dein Test, Reaktionszeit ca. 1–2 s).
> **TikTok-Kette (Voicemeeter Banana, LIVE Studio, echter Gast): ungetestet.** Ablauf: `docs/HARDWARE-TEST.md`.

> **Wichtig:** Die Live-Pflichtabnahme (echter Mensch, TikTok LIVE, ≥ 10 Wechsel, gemessene Latenz) ist **noch nicht durchgeführt**. Alle Live-Funktionen gelten bis dahin als ungetestet. Ablauf: `docs/HARDWARE-TEST.md`.

## Priorität 1: Live-Debatte Mensch gegen NOVA

| Anforderung | Umsetzung in 0.3.0 | Automatischer Test | Live |
|---|---|---|---|
| Live zuhören, ohne Klick antworten | Audio wird fortlaufend zu OpenAI gestreamt; Antwort nach erkanntem Satzende | ✅ | ⚪ |
| Erkennen, wann der Mensch wirklich fertig ist; bei Unsicherheit warten | Semantische Satzende-Erkennung von OpenAI (`semantic_vad`), Geduld einstellbar; NOVA löst Antworten selbst aus | ✅ Protokoll | ⚪ |
| Kurze Pausen führen nicht zum Dazwischenreden | Geduld „geduldig“; Fairness hält fertige Antworten zurück; Weitersprechen verwirft ungehörte Antworten | ✅ | ⚪ |
| Vollduplex | Eingang bleibt offen, während NOVA spricht (außer Halbduplex-Einstellung) | ✅ | ⚪ |
| Unterbrechung einstellbar (stoppen / Satz beenden / ignorieren) | Lokale Erkennung ab einstellbarer Sprechdauer; Kontext wird auf Gehörtes gekürzt | ✅ | ⚪ |
| Echovermeidung | Echoprüfung im Setup; Echo-Erkennung per Hüllkurven-Korrelation; Selbstgespräch-Schutz per Mitschrift → automatisch Halbduplex | ✅ (simulierte Signale) | ⚪ |
| Gesprächswechsel ohne Verlust | Warteschlange: Beiträge während NOVAs Rede oder einer laufenden Antwort werden vorgemerkt und beantwortet | ✅ | ⚪ |
| Fortlaufender Kontext, Langzeitkontext | Gesprächsverlauf in der Sitzung; Debattenprotokoll alle 4 Antworten; Kürzung alter Teile mit Retention-Ratio; neue Sitzung nach 55 min oder Abbruch mit Protokoll | ✅ | ⚪ |
| Debattenintelligenz (Kernbehauptung, Gegenargumente, Widersprüche, Position anpassen, Provokationen, keine Floskeln, Übergänge) | Feste Debattenmethode in den Anweisungen, Intensität einstellbar | ✅ Text vorhanden | ⚪ Qualität ungeprüft |
| Gespräch selbst voranbringen | Optional: Nachfrage nach X s Stille, höchstens einmal pro Pause; Knopf „NOVA soll sprechen“ | ✅ | ⚪ |
| Flüssig, geringe Verzögerung | Streaming in beide Richtungen; Wiedergabe ab 240 ms Puffer | ✅ | ⚪ Latenz ungemessen |
| Latenz messen und dokumentieren | Messung pro Antwort (Satzende → hörbar, Anforderung → erstes Audio), Anzeige, Live-Testbericht | ✅ | ⚪ |
| Klar als KI erkennbar | Gesprochener Hinweis beim Start, Dauerhinweis in der App, Anleitung für Stream-Einblendung | ✅ | ⚪ |
| TikTok-LIVE-Pipeline | „TikTok LIVE verbinden“: Voicemeeter-Fernsteuerung, Routing-Messung, Gastton-Test, OpenAI-Test, Startfreigabe | ✅ mit simuliertem Mischpult | ⚪ |
| Lokale Debatte mit echter OpenAI-API | – | – | ✅ **von dir bestätigt**, ca. 1–2 s |
| Lokaler KI-Modus (whisper.cpp, Ollama, Piper), kostenlos | Satzweise Ausgabe, Verlauf, Gedächtnis, Unterbrechen, Sicherheitssperre nur 127.0.0.1 | ✅ simuliert **und echt auf GitHub-Windows-VM** (2,5–8 s Reaktionszeit ohne GPU) | ⚪ **auf deinem PC ungetestet** |
| Eigentümer-Lizenz | Signiert, PC-gebunden; öffentlicher Schlüssel muss noch eingebaut werden | ✅ | ⚪ |
| Stream-Overlay „DU GEGEN NOVA“ (LIVE-Studio-Link-Quelle) | Lokaler Server, 9:16-Szene, Sprechanzeige nach NOVAs Pegel | ✅ (Server echt über TCP, Darstellung in Chrome geprüft) | ⚪ **in LIVE Studio ungetestet** |
| Kosten und Kostenlimit | Schätzung aus Token-Angaben von OpenAI, Limit schaltet NOVA stumm oder beendet die Sitzung ganz (Standard) | ✅ | ⚪ Preise ohne Mitschrift |

### Bekannte Grenzen

- **Echo-Erkennung braucht Anlauf:** Ohne bestandene Echoprüfung hört NOVA in den ersten ca. 1,7 s jeder eigenen Antwort nicht zu. Mit bestandener Echoprüfung gilt Vollduplex ab dem ersten Wort.
- **Echo so laut wie der Gast:** Spricht der Gast über ein gleich lautes Echo, wird ein Teil seiner Worte ausgeblendet. Abhilfe ist getrenntes Routing.
- **Kein akustischer Echo-Canceller:** Bei Lautsprechern im Raum Halbduplex verwenden.
- **Kosten:** Im OpenAI-Satzende-Modus wird das Eingangssignal fortlaufend gestreamt. Wie viel davon abgerechnet wird, ist am echten Konto zu prüfen.
- **Gleichzeitige Gedächtnis- und Debattenantwort:** Laut Doku zulässig (Gedächtnis läuft außerhalb des Gesprächs), aber ungetestet.

## Weitere Komponenten

| Komponente | Status |
|---|---|
| Persönlichkeiten (5 Stile, Intensität, Länge, Stimme, Sprache, Thema, Position), eigene Profile | ✅ |
| Dunkle Oberfläche, Statussymbol, Wellenform, Statusanzeigen | 🟡 startet unter Windows stabil; Darstellung noch von niemandem gesehen |
| Einstellungen speichern, lokale Daten löschen | ✅ |
| Geräteerkennung Voicemeeter Standard/Banana | 🟡 aus 0.1.2 |
| Geführter Einrichtungsassistent mit Profilen für TikTok LIVE Studio, OBS, Discord, lokal | ✅ Logik · ⚪ auf Windows ungesehen |
| Modus C KI gegen KI | ❌ nach Live-Abnahme |
| Modus D Moderation | ❌ nach Live-Abnahme |
| Windows-Build, Selbsttest, Installer, Installationstest, Release (GitHub Actions) | ✅ auf echtem Windows (Build-Server ohne Audiogeräte) |
| Update-Mechanismus, Code-Signierung | ❌ |
| Website, Konten, Stripe, Lizenzserver | 🟡 Landingpage / ❌ Rest, bewusst nach der Live-Abnahme |

## Wichtige Architekturentscheidung für den Verkauf

Heute trägt jeder Nutzer seinen eigenen OpenAI-Schlüssel ein. Für Abos darf kein gemeinsamer Schlüssel im Client stecken: Die App soll sich am Kundenkonto anmelden und pro Debatte ein kurzlebiges Realtime-Client-Secret vom Server erhalten (Abo- und Kontingentprüfung serverseitig).

## Reihenfolge

1. **Live-Pflichtabnahme** nach `docs/HARDWARE-TEST.md` (du, mit einem Gast).
2. Fehler aus der Abnahme beheben, Latenz optimieren.
3. ~~Geführter Einrichtungsassistent, Persönlichkeitsprofile~~ (erledigt in 0.4.0).
4. Danach: KI gegen KI, Moderation, Website-Konten, Stripe, Lizenzserver, Updates, Signierung.
