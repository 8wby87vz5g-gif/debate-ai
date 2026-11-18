# Änderungsprotokoll

## 0.7.2 · 17.11.2026 · Neue Voicemeeter-Gerätenamen (Befund vom echten PC)

Befund beim Einrichten auf einem Windows-11-PC mit Voicemeeter Banana 2.1.3: Windows nannte die Voicemeeter-Aufnahmegeräte „Voicemeeter Out 1“ bis „Out 8“ und alle acht virtuellen Wiedergabegeräte gleich („Lautsprecher (VB-Audio Voicemeeter VAIO)“). DEBATE AI fand dadurch weder B2 noch AUX und hätte nach einem Neustart einen beliebigen der gleichnamigen Ausgänge gewählt.

- **Gerätewahl wird über die Windows-Endpunkt-ID gespeichert**, nicht mehr nur über den (auf 31 Zeichen gekürzten) Namen. Ein mehrdeutiger Name wird nie geraten; alte Einstellungsdateien mit eindeutigem Namen funktionieren weiter.
- **Neue Voicemeeter-Namen erkannt:** „Voicemeeter Out 7“ = B2, „Voicemeeter Out 6“ = B1 (Reihenfolge A1–A5, B1–B3; auf dem PC per Pegel und Testton gemessen). Namensvergleich mit Wortgrenzen.
- **„TikTok LIVE verbinden“, Schritt 3: „Ausgänge per Testton zuordnen“.** Bei unbenannten Voicemeeter-Ausgängen spielt DEBATE AI nacheinander ein Testsignal und hört auf B1 und B2 mit: nur B2 = Voicemeeter Input (Gast), nur B1 = Voicemeeter AUX Input (NOVA). Ein Ausgang, der auf beiden ankommt (Rückweg), wird abgelehnt. Ergebnis wird gespeichert und in Schritt 4 (Routing-Messung) verwendet.
- **Kostenlimit wieder in Cent-Schritten** (0,10 $), Standard 1,00 $, z. B. 0,50 $ für den ersten Test. In 0.7.0 angekündigt, das Eingabefeld nahm aber nur ganze Dollar an.
- Einrichtungs-Prüfung: virtuelle Voicemeeter-Geräte gelten nicht mehr als Raumlautsprecher; „Out 6“ (= B1) als Gast-Eingang wird gewarnt.
- Anzeige: Während Pegel-, Testton- und Echoprüfung steht „API: nicht verbunden (Audioprüfung, keine Kosten)“ statt fälschlich „Verbindung aktiv“; nach bestandener Echoprüfung steht nicht mehr „Fehler“.
- **Neu: „NOVA-Fenster für TikTok öffnen“.** Befund: TikTok LIVE Studio 1.40.2 lehnt in der Quelle „Link“ lokale Adressen ab („Korrekte URL eingeben“), der Umweg über ein Edge-Fenster war für Kunden zu fummelig. DEBATE AI öffnet jetzt selbst ein randloses 9:16-Fenster „NOVA LIVE (für TikTok)“ mit derselben Szene (DU GEGEN NOVA, dauerhafter KI-Hinweis, Thema, NOVA-Figur nach echtem Ausgabepegel, Gastrahmen), ohne Browser und ohne Titelleiste. In LIVE Studio: Quelle hinzufügen → Fenster → „NOVA LIVE (für TikTok)“. Verschieben per Maus, Rechtsklick für Größe/Schließen; minimieren wird verhindert, weil LIVE Studio sonst kein Bild bekommt.
- Drei neue automatische Tests (Gerätenamen und Wiederherstellung, Zuordnung per Testsignal mit simuliertem Mischpult, NOVA-Fenster zeichnen). **Auf echter Hardware ungetestet:** die Zuordnung per Testton und die Fensteraufnahme des NOVA-Fensters in LIVE Studio.

## 0.7.1 · 17.11.2026

- „TikTok LIVE verbinden“: neuer Knopf **Diagnose kopieren**. Fasst Windows-Version, KI-Modus, alle Audiogeräte, Voicemeeter, LIVE Studio, Overlay und das Ergebnis jeder Prüfung als Text zusammen (ohne Schlüssel, ohne Aufnahmen), zum Einfügen in den Support-Chat.

## 0.7.0 · 17.11.2026 · Live-Szene und Kostenschutz

- **Stream-Overlay für TikTok LIVE Studio:** DEBATE AI liefert eine 9:16-Szene „DU GEGEN NOVA“ über `http://127.0.0.1:18190/nova` (Link-Quelle): KI-Hinweis, Thema, Gastrahmen, NOVA-Figur, die auf NOVAs echte Ausgabelautstärke reagiert; durchsichtige Variante `?view=badge`. Nur lokal erreichbar, nur GET, fremde Hosts abgelehnt.
- **Kostenlimit beendet auf Wunsch die OpenAI-Sitzung vollständig** (Standard an), Limit jetzt in 0,10-$-Schritten, Standard 1,00 $.
- NOVAs Ausgabepegel wird gemessen (Sprechanzeige).

## 0.6.2 · 17.11.2026

- Behoben: Direkt nach einem Modell-Download meldete die App fälschlich „Ollama nicht erreichbar“ (Ollama antwortete langsamer als 3 s). Jetzt längere Wartezeit mit Wiederholung.
- Zweite echte Messung mit Satzteil-Beschleunigung (schnellerer Testrechner): llama3.2:3b 2,6 s, gemma3:1b 2,5 s, qwen2.5:3b 3,5 s Reaktionszeit.

## 0.6.1 · 17.11.2026 · Lokaler Modus unter echtem Windows getestet und beschleunigt

- **Erstmals echt getestet** (Windows-VM bei GitHub, 4 Threads, ohne Grafikkarte): Download, whisper.cpp, Ollama, Piper und drei Debattenrunden je Modell. Läuft jetzt bei jedem Build mit.
- Behoben: Absturz beim Lesen der Piper-Audiodatei während der Debatte (Datei noch von Windows gesperrt).
- Behoben: whisper.cpp-Download schlug fehl, weil das neueste Release keine Windows-Datei enthält.
- Schneller: kurze Anweisungen für lokale Modelle, feste Wortgrenze, Vorwärmen beim Start, Verlauf cachefreundlich, NOVA beginnt nach dem ersten Satzteil zu sprechen. gemma3:4b: 16–23 s → ca. 8 s.
- Abgeschnittene Satzreste werden nicht mehr gesprochen.
- Empfehlung ohne Grafikkarte jetzt llama3.2:3b (ca. 5 s auf dem Testrechner); Auswahl um qwen2.5:3b und gemma3:1b erweitert. Ollama-Zeiten (Lesen/Schreiben) im Protokoll.

## 0.6.0 · 17.11.2026 · Lokaler KI-Modus und Eigentümer-Lizenz

- **KI-Modus wählbar:** OpenAI Realtime (kostenpflichtig, Premium) · Lokal auf diesem PC (kostenlos) · Offline-Probe. Deutliche Modusanzeige mit Kosten.
- **Lokaler Modus:** whisper.cpp (Spracherkennung) + Ollama (Sprachmodell) + Piper (Stimme), satzweise Ausgabe, Gesprächsverlauf, Gedächtnis, Unterbrechen und Warteschlange wie im OpenAI-Modus. Sicherheitssperre: nur Dienste auf 127.0.0.1, nie OpenAI.
- **„Lokale KI einrichten“:** Hardwareprüfung, Modellempfehlung, Downloads aus offiziellen Quellen nach Bestätigung, Geschwindigkeitsmessung auf dem PC.
- **Eigentümer-Lizenz:** signierte, an PCs gebundene Lizenzen; Lizenzwerkzeug für den Eigentümer; Anzeige von Lizenz und Geräte-ID.
- TikTok-Prüfdialog und Stimmprobe nutzen im lokalen Modus die lokale KI.
- CI baut das Lizenzwerkzeug und prüft die Download-Quellen der lokalen KI.
- Der bisherige OpenAI-Realtime-Modus ist unverändert enthalten.

## 0.5.0 · 17.11.2026 · TikTok-LIVE-Integration

- **„TikTok LIVE verbinden“**: Prüfdialog für die gesamte Audiokette mit Startfreigabe erst nach bestandenen Prüfungen. Behauptet keine Verbindung zu TikTok.
- **Voicemeeter-Fernsteuerung** über die offizielle VoicemeeterRemote-API: erkennt Standard/Banana/Potato, startet Banana auf Wunsch, richtet das TikTok-Routing ein (Gast → A1 + B2, NOVA → B1, Mikrofon → B1, A1-Kopfhörer, Hardware-Mikrofon), zeigt Änderungen vorher an und liest sie zurück.
- **Routing-Messung**: Testsignale auf Gast-Weg und NOVA-Weg; prüft per Korrelation, dass sie genau auf B2 bzw. B1 ankommen und nicht zurücklaufen.
- **Gastton-Test** über die Voicemeeter-Pegel (VAIO) und den DEBATE-AI-Eingang (B2); ehrlicher Hinweis, wenn LIVE Studio den Gast nicht getrennt ausgibt, mit dokumentierter Alternative (Anruf-App).
- **OpenAI-Verbindungstest** mit gemessener Aufbauzeit; Erkennung, ob TikTok LIVE Studio läuft.
- **Live-Kontrollzeile**: Eingang, NOVA-Ausgang, OpenAI, Gast spricht, Echo-Warnung, geschätzte Kosten.
- **Kostenschätzung und Kostenlimit** (Standard 5 $ pro Debatte; danach ist NOVA stumm).
- Knopf **„Gast ausreden lassen“** schaltet während der Debatte um.
- TikTok-Profil nutzt jetzt „Geduld: ausgewogen“ (wie dein funktionierendes lokales Setup).

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
