# DEBATE AI für Windows

**Version 0.2.0 · NOVA, KI-Debattenpartnerin mit synthetischer Stimme**

> Neu in 0.2.0: NOVA antwortet im Automatikmodus selbstständig und beginnt zu sprechen, sobald die ersten Antwortteile da sind. Details stehen in `CHANGELOG.md`, der Prüfstand in `docs/STATUS.md`.

## 0. Installation

- **Installer:** `DEBATE-AI-Setup-0.2.0.exe` ausführen. Es sind keine Administratorrechte nötig; die App landet unter `%LOCALAPPDATA%\Programs\DEBATE AI` und im Startmenü.
- **Ohne Installer:** `DEBATE-AI-portable.zip` entpacken und `DEBATE-AI.exe` starten (`DEBATE-AI.exe.config` muss daneben liegen).
- **Selbst bauen:** `Neu-bauen.cmd` ausführen (nutzt den in Windows enthaltenen C#-Compiler von .NET Framework 4.8), danach `Selbsttest.cmd`.
- **Systemanforderungen:** Windows 10 (ab 1903) oder Windows 11, .NET Framework 4.8 (bereits enthalten), Internetverbindung, ein OpenAI-API-Projekt mit Realtime-Zugriff und Guthaben.
- **SmartScreen:** Solange der Installer nicht code-signiert ist, zeigt Windows „Der Computer wurde durch Windows geschützt“. Über „Weitere Informationen → Trotzdem ausführen“ geht es weiter. Für den Verkauf wird ein Code-Signing-Zertifikat benötigt (siehe `docs/STATUS.md`).

## 1. Erste Probe ohne KI (Offline-Probe)

1. Wähle unter **1 · Audio** dein Mikrofon als **Eingang** und deinen Kopfhörer als **Ausgang**.
2. **Testton**: Der Ton soll nur am gewählten Ausgang ankommen.
3. **Pegel prüfen** und einen Satz sprechen: Pegel und Wellenform reagieren. Bei Stille liegt der Pegel unter der Schwelle (Standard −40 dBFS).
4. Haken **Offline-Probe** setzen und **Debatte starten**. Sprich einen Satz und mache eine Pause: Ein Piepton bestätigt, dass Aufnahme, Sprecherwechsel und Ausgabe funktionieren.

**Wichtig:** Solange die Offline-Probe aktiv ist, antwortet NOVA **nur mit einem Piepton**, nie mit Sprache. Die App zeigt das gelb an. Das war in 0.1.x die Werkseinstellung und eine Hauptursache für „NOVA hört, antwortet aber nicht“.

## 2. Erste echte Debatte mit NOVA

1. **API-Schlüssel hinterlegen** (OpenAI-Projekt mit Realtime-Freigabe). Der Schlüssel wird mit Windows-DPAPI verschlüsselt nur für dein Benutzerkonto gespeichert. Danach wird die Offline-Probe automatisch ausgeschaltet.
2. Unter **2 · NOVA-Persönlichkeit** Stil, Intensität, Antwortlänge, Stimme, Sprache und optional Thema und NOVAs Position wählen.
3. Alle Sprecher informieren, dass NOVA eine KI ist und ihre Beiträge an OpenAI gehen, und die Einwilligung bestätigen.
4. **Sprecherwechsel: Automatisch** (Standard) und **Debatte starten**. NOVA spricht zuerst ihren KI-Hinweis.
5. Sprich einen Satz, z. B. „Schulunterricht sollte morgens später beginnen.“ Nach einer Sprechpause (Standard 1,2 s) zeigt die App **NOVA denkt nach** und dann **NOVA spricht**. Mit eingeschalteter Live-Mitschrift erscheinen deine Worte und NOVAs Antwort rechts im Fenster.

### Die Bedienknöpfe

| Knopf | Wirkung |
|---|---|
| **Debatte starten / Fortsetzen** | Öffnet Geräte und eine neue OpenAI-Sitzung. |
| **Pause** | Beendet die Sitzung; „Fortsetzen“ startet eine neue Sitzung mit frischem Kontext. |
| **Jetzt antworten** | Beendet den laufenden Beitrag sofort und lässt NOVA antworten, ohne auf die Pause zu warten. Im Push-to-talk-Modus heißt er „Aufnahme starten“. |
| **NOVA unterbrechen** | Stoppt NOVA sofort. Im Gesprächskontext bleibt nur der Teil, der tatsächlich zu hören war. |
| **Ruhe in der Debatte** | Schließt die KI-Sitzung; der Gast hat das Wort, bis du fortsetzt. |
| **Not-Aus (Esc)** | Beendet alles und gibt beide Audiogeräte frei. |

### Sprecherwechsel und „ausreden lassen“

- Ein Beitrag beginnt nach etwa 120 ms Signal über der Schwelle und endet nach der eingestellten **Sprechpause**.
- **Max. Redezeit** (Standard 30 s): Spricht jemand länger ohne Pause, wird der Beitrag an dieser Stelle **gesendet** (in 0.1.x wurde er verworfen, sodass NOVA bei durchgehend laufenden Videos nie antwortete).
- **Gesprächspartner ausreden lassen (Fairness):** Ist NOVAs Antwort fertig, während noch jemand spricht, wartet sie bis zu 6 s auf eine Pause. Spricht die Person weiter (mehr als 0,6 s Sprache), verwirft NOVA ihre noch ungehörte Antwort, hört weiter zu und beantwortet danach beides zusammen. Kein Beitrag geht verloren.
- **Bei Videos oder Streams ohne Pausen** Fairness ausschalten. NOVA antwortet dann nach jedem Beitrag sofort.
- Während NOVA spricht und in der Nachlaufzeit (Standard 900 ms) ist der Eingang gesperrt (**Echo-Schutz**). Was in dieser Zeit gesagt wird, hört NOVA nicht. Mit **NOVA unterbrechen** gibst du sofort das Wort zurück.

**Pegel einstellen.** Spricht der Gast leise, die Schwelle z. B. auf −48 dBFS senken. Löst Raumrauschen aus, auf z. B. −32 dBFS erhöhen. Die Erkennung reagiert auf Lautstärke und kann Sprache nicht von Musik unterscheiden.

## 3. Voicemeeter Banana und TikTok LIVE Studio

Installiere [Voicemeeter Banana vom Hersteller](https://vb-audio.com/Voicemeeter/banana.htm), starte Windows nach der Installation neu und öffne Banana. Wähle oben rechts bei **A1** deinen Kopfhörer. Die Namen der virtuellen Geräte unterscheiden sich je nach Treiberversion. Im Windows-Aufnahmedialog heißt **B1** z. B. `Voicemeeter Out B1` oder `Voicemeeter Output`. **B2** heißt z. B. `Voicemeeter Out B2` oder `Voicemeeter AUX Output`. [Virtuelle Geräte und Busse laut VB-Audio](https://voicemeeter.com/quick-tips-voicemeeter-virtual-inputs-and-outputs-windows-10-and-up/).

| Signal | Wiedergabegerät der Quelle | Schalter im Voicemeeter-Kanal | Empfänger |
|---|---|---|---|
| Gastton aus LIVE Studio oder separatem Gast-Client | `Voicemeeter Input` (VAIO) | **A1 an**, **B2 an**, B1 aus | DEBATE AI nimmt **B2** auf. Du hörst den Gast im Kopfhörer. |
| Stimme von Nova | `Voicemeeter AUX Input` | **B1 an**, **B2 aus**, A1 nach Bedarf | LIVE Studio nimmt **B1** als Mikrofonquelle auf. |

```text
Gast-Wiedergabe → VAIO → B2 → DEBATE AI (Eingang)
                           DEBATE AI (Ausgang) → AUX → B1 → LIVE Studio (Mikrofon)
                      A1 → Kopfhörer              A1 optional → Kopfhörer
```

1. Stelle am Gast-Client, wenn möglich, `Voicemeeter Input` als Lautsprecher- oder Wiedergabegerät ein. Alternativ kannst du unter **Windows → Einstellungen → System → Sound → Lautstärkemixer** für die laufende App den Ausgang wählen. Manche Apps erscheinen erst, wenn sie Ton abspielen. Bei LIVE Studio ist diese Zuordnung nur brauchbar, wenn dessen Wiedergabe tatsächlich den Gast getrennt von der eigenen Sendung und dem Mikrofonmonitoring liefert. [Windows-Lautstärkemixer und Ausgabegerät pro App](https://support.microsoft.com/en-us/windows/hardware/audio/fix-app-audio-not-working-while-system-sounds-work-in-windows).
2. Wähle in DEBATE AI **B2 als Eingang** und **Voicemeeter AUX Input als Ausgang**. Wenn die App vor Banana offen war, klicke **Geräte neu laden** und wähle beide Geräte erneut.
3. Wähle in LIVE Studio im Audiomixer oder bei der Mikrofonquelle **B1** als Aufnahmeeingang. Die Bezeichnungen der Menüs können je nach Version variieren. Lass die Mikrofonquelle nur einmal im Mixer. Schalte Monitoring des B1-Signals über die Gast-Wiedergabe und doppelte Desktop-Audioquellen aus. Lass eine auf einem zweiten Gerät geöffnete Zuschaueransicht stumm.
4. Prüfe in der Vorschau oder im Übungsmodus, soweit dein Konto das anbietet. Für die Offline-Routingprobe reicht ein lokaler Ton oder Sprachclip, dessen Wiedergabe-App vorübergehend auf `Voicemeeter Input` gestellt wird. Stelle ihre Ausgabeeinstellung danach wieder zurück.
5. Falls TikTok den Gastton nur gemeinsam mit der eigenen Sendung ausgibt, brich die native Gastprobe ab. Für eine getrennte Probe kann ein Gast über eine eigene Anruf-App zugeschaltet werden, deren Wiedergabe auf VAIO und deren Mikrofon für Nova auf B1 liegt. Damit das Publikum den Gast in diesem Aufbau ebenfalls hört, füge **B2** einmal als getrennte Tonquelle in LIVE Studio hinzu. Nimm B2 dabei nicht in den Mikrofonmix des Anrufs auf, sonst hört der Gast sich selbst. Falls TikTok den Gast bereits intern in den Stream mischt, entfällt die zusätzliche B2-Quelle in LIVE Studio.

## 4. Abnahme vor einer öffentlichen Sendung

| Test | Ablauf | Bestanden, wenn … |
|---|---|---|
| Gastweg | **Pegel prüfen**. Gast oder Testclip spricht, Nova ist stumm. | VAIO, B2 und der Eingangspegel der App reagieren. B1 bleibt ruhig. |
| KI-Weg | Gast schweigt. Klicke während **Pegel prüfen** auf **Testton**. | AUX, B1 und die KI-Mikrofonquelle in LIVE Studio reagieren. B2 und App-Eingangspegel bleiben beim Grundpegel. |
| Beide hören sich | Zwei kurze Beiträge mit einem eingeweihten Gast. Frage ihn, ob er Novas Stimme hört. Prüfe die Studio-Vorschau oder eine zweite Abhörposition mit Kopfhörern. | Gast und Publikum hören Nova jeweils einmal. Das Publikum hört den Gast einmal und der Gast hört sich nicht selbst. |
| Kein Selbstgespräch | Automatik starten, einen Gastbeitrag beantworten lassen, danach zehn Sekunden Stille. | Nova beantwortet den Gast genau einmal. Der Eingang öffnet erst nach dem Nachlauf, ohne einen weiteren Beitrag auszulösen. |
| Stopp im Betrieb | Während **KI verarbeitet** pausieren. Danach wiederholen und **Stopp** während der Wiedergabe drücken. | Die laufende Wiedergabe endet. Eine späte Antwort wird nicht mehr abgespielt. Nach dem Fortsetzen ist ein neuer Beitrag möglich. |
| Verbindung und Geräte | In einer lokalen Probe die Netzwerkverbindung unterbrechen oder ein Testgerät abziehen. Stelle die Verbindung wieder her und starte neu. | Die App zeigt einen verständlichen Fehler, gibt die Geräte frei und lässt sich neu starten. |
| Kennzeichnung | Lausche beim Sitzungsstart und kontrolliere die LIVE-Szene. | Die Ansage stellt Nova als KI vor. Der eingeblendete KI-Hinweis bleibt sichtbar. |

Wenn beim KI-Test auch B2 ausschlägt, stoppe und korrigiere zuerst die Bus-Schalter und das Monitoring. Die App-Sperre schützt während der Verarbeitung, Wiedergabe und Nachlaufzeit. Ein verzögert zurückgespielter Stream kann trotzdem nach dem Nachlauf einen neuen Beitrag auslösen. Nutze bei getrennten Geräten möglichst Kopfhörer. Verwende **Push-to-test**, solange die automatische Trennung nicht bestätigt ist.

## 5. Architektur und Daten

```text
WinMM-Eingang → PCM 24 kHz mono → Pegel / Sprecherwechsel (lokal)
                                   ↓ abgeschlossener Beitrag
                    OpenAI Realtime (WebSocket), gpt-realtime-2.1-mini
                                   ↓ Antwort-Audio als Stream
WinMM-Ausgang ← Wiedergabe ab ca. 240 ms Puffer ← Eingangssperre + Nachlauf
```

- **Audio:** Ein- und Ausgang werden ausdrücklich gewählt; WinMM mit 48 kHz (Fallback 24 kHz), Umrechnung auf 24 kHz mono. Halbduplex: Während NOVA spricht, wird der Eingang verworfen.
- **Kontext:** Innerhalb einer Sitzung behält das Modell den Gesprächsverlauf. Ungehörte Antworten werden aus dem Verlauf gelöscht, unterbrochene auf den gehörten Teil gekürzt. „Fortsetzen“ startet mit frischem Kontext.
- **Mitschrift:** Optional über `gpt-4o-mini-transcribe` (zusätzliche Kosten). Sie bleibt nur im Arbeitsspeicher und wird nie auf die Festplatte geschrieben.
- **Schlüssel:** `%LOCALAPPDATA%\DebateAI\api-key.dpapi`, DPAPI-verschlüsselt für das aktuelle Benutzerkonto.
- **Einstellungen:** `%LOCALAPPDATA%\DebateAI\settings.json` (nur Einstellungen, kein Schlüssel, keine Gesprächsinhalte).
- **Protokoll:** `debate-ai.log` mit Status, Laufzeiten, Beitragsdauer, Fehlerklassen und Tokenzahlen; keine Audiodaten, keine Transkripte, keine Schlüssel.
- **Lokale Daten löschen:** Der gleichnamige Knopf entfernt Schlüssel, Einstellungen und Protokolle dieser App.
- **Hinweis für den Verkauf:** In dieser Version nutzt jeder Kunde seinen eigenen OpenAI-Schlüssel. Für Abos wird ein Server benötigt, der kurzlebige Sitzungsschlüssel ausgibt (siehe `docs/STATUS.md`, Phase 6).

## 6. Projektdateien und lokale Prüfung

- `src/MainForm.cs` Oberfläche · `src/Turns.cs` Gesprächssteuerung · `src/Realtime.cs` OpenAI Realtime und Offline-Modus · `src/Persona.cs` Persönlichkeiten · `src/Audio.cs` Windows-Audio · `src/LocalData.cs` Schlüssel, Einstellungen, Protokoll.
- `Neu-bauen.cmd` baut `DEBATE-AI.exe`, `Selbsttest.cmd` baut und startet `tests/Tests.cs` (simulierte Audiosignale und Realtime-Ereignisse; kein API-Schlüssel nötig).
- `installer/DebateAI.iss` erzeugt den Installer mit Inno Setup 6.
- Hardware- und Live-Abnahme: `docs/HARDWARE-TEST.md`.
