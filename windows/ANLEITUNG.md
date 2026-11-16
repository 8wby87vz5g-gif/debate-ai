# DEBATE AI für Windows

**MVP 0.1.2 · Nova, deutsche KI-Debattenpartnerin mit synthetischer Stimme**

## 1. In fünf Minuten zur Offline-Probe

1. Entpacke die gesamte ZIP-Datei und öffne `DEBATE-AI.exe`. Die App läuft unter Windows mit .NET Framework 4.8 oder 4.8.1. `DEBATE-AI.exe.config` bleibt neben der EXE. Für die App sind weder Administratorrechte noch Python, Node oder zusätzliche Programmbibliotheken nötig.
2. Wähle für die erste Probe dein Mikrofon als **Eingang** und deinen Kopfhörer als **Ausgang**. Wähle **Testton**. Der Ton soll nur am ausgewählten Ausgang ankommen.
3. Wähle **Pegel prüfen** und sprich einen Satz. Der Eingangspegel soll reagieren. Bei Stille sollte er unter der eingestellten Schwelle liegen. Beende den Pegeltest mit **Stopp**.
4. Lass **Offline-Probe mit Antwortton** aktiv. Wähle **Push-to-test**, klicke **Start** und warte auf **Bereit für den Gast**. Klicke **Aufnahme starten**, sprich einen Satz und klicke **Beitrag senden**. Ein Antwortton bestätigt die Verarbeitung des Beitrags und die Ausgabe. Für diese Probe bleiben alle Daten auf dem Rechner.
5. Teste **Pause** während des Antworttons. Die Wiedergabe soll sofort stoppen. **Fortsetzen** öffnet eine neue Sitzung. **Not-Aus (Esc)** beendet die Sitzung, gibt beide Geräte frei und verwirft eine noch laufende Antwort. Die Esc-Taste wirkt, wenn das Fenster aktiv ist. Prüfe für die Live-Probe zusätzlich die Stummschaltung des KI-Mikrofons in LIVE Studio.

## 2. Erste deutsche KI-Debatte

1. Erstelle einen Schlüssel in einem [OpenAI-API-Projekt](https://platform.openai.com/api-keys), das für Realtime freigeschaltet ist. Die Nutzung wird über das API-Projekt abgerechnet. Gib den Schlüssel unter **API-Schlüssel** direkt in der Windows-App ein.
2. Entferne den Haken bei **Offline-Probe**. Informiere alle Sprecher darüber, dass Nova eine KI ist, und dass die App ihre erkannten Beiträge an OpenAI sendet. Lass sie zustimmen und aktiviere das Bestätigungsfeld. Für einen Stream verwende zusätzlich einen dauerhaft sichtbaren Text wie **„Nova · KI-Debattenpartnerin · synthetische Stimme“** und die passende TikTok-Kennzeichnung, falls sie in deinem LIVE-Setup angeboten wird.
3. Wähle zunächst **Push-to-test** und klicke **Start**. Nach dem Verbindungsaufbau spielt die App Novas gesprochenen KI-Hinweis ab. Sobald **Bereit für den Gast** angezeigt wird, teste etwa „Schulunterricht sollte morgens später beginnen, weil Jugendliche mehr Schlaf brauchen.“ Nova soll auf Deutsch mit einem kurzen Einwand oder einer gezielten Rückfrage antworten. Greife die Antwort im nächsten Beitrag auf und prüfe, ob Nova den Zusammenhang behält.
4. Stoppe die Sitzung. Wähle **Automatisch** und starte erneut. Ein Beitrag beginnt nach ungefähr 120 ms ausreichend lautem Eingangssignal. Im Fairness-Modus beendet eine Pause von ungefähr 1,2 Sekunden den Beitrag. Die einstellbare Redezeit (5–60 Sekunden, Standard 12) begrenzt Aufnahme und KI-Debattenantwort. Überschreitet der Gast das Limit vor einer Pause, verwirft Nova die Aufnahme und wartet auf Ruhe. Ist Fairness ausgeschaltet, gelten rund 760 ms und bei Erreichen der Redezeit wird der aufgezeichnete Teil gesendet. Der Realtime-Audiopuffer hat unabhängig davon ein 30-Sekunden-Sicherheitslimit für die KI-Ausgabe. Nach der Antwort hält die App den Eingang standardmäßig weitere 900 ms gesperrt. Warte für deinen nächsten Beitrag auf **Bereit für den Gast**.
5. Miss die Zeit zwischen dem Ende der Aussage und dem Beginn der Antwort. Das Protokoll enthält die Zeit von der Übermittlung bis zum Eintreffen der vollständigen Antwort. Die App puffert eine vollständige KI-Antwort, bevor sie spricht. Die hörbare Verzögerung umfasst daher auch Sprechpausenerkennung, Netzwerk, Modell und Audioausgabe.

**Pegel einstellen.** Die Schwelle startet bei −40 dBFS. Spricht der Gast leise, senke den Wert zum Beispiel auf −48 dBFS. Löst Raumrauschen aus, erhöhe ihn zum Beispiel auf −32 dBFS und prüfe zuerst, ob Musik oder Desktop-Ton versehentlich auf dem Eingang liegen. Die Erkennung reagiert auf Lautstärke und Sprechpausen. Sie kann Sprache, Musik und andere laute Signale nicht zuverlässig unterscheiden. Push-to-test ist für die Erstprobe der einfachere Kontrollpunkt.


### Moderieren

- **Fairness-Modus** ist beim Start aktiv. Wenn der Gast während der Berechnung weiterspricht und die Pause noch zu kurz ist, spielt Nova die vorbereitete Antwort nicht ab. Die Pegelerkennung ist eine Heuristik. Prüfe sie mit der tatsächlichen Stimme und Geräuschkulisse.
- **Ruhe in der Debatte** schließt die aktuelle KI-Sitzung und gibt dem Gast das Wort. Der Hinweis ist im Fenster sichtbar. Wenn Nova gerade spricht, stoppt ihre Wiedergabe. Klicke erst nach dem Gastbeitrag auf **Fortsetzen**. Dabei öffnet sich eine frische Sitzung mit neuer KI-Kennzeichnung.
- **Not-Aus** beendet die gesamte KI-Sitzung. Die Esc-Taste wirkt bei aktivem Fenster. In LIVE Studio ist die Stummschaltung der B1-Quelle die zweite Abschaltmöglichkeit. Der Gastbus wird von der App nicht stummgeschaltet.
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
WinMM-Eingang → PCM 24 kHz mono → lokaler Pegel / Turn-Erkennung
                                    ↓ abgeschlossener Beitrag
                       OpenAI Realtime über WSS, gpt-realtime-2.1-mini
                                    ↓ vollständige Antwort im RAM
WinMM-Ausgang ← PCM-Wiedergabe ← Antwort + Eingangssperre + Nachlauf
```

- **Audio.** Ein- und Ausgabe werden explizit gewählt. Die App nutzt WinMM, versucht für die Windows-Geräte zuerst 48 kHz und konvertiert für OpenAI in 24 kHz Mono. Die App arbeitet abwechselnd in beide Richtungen. Was während der KI-Ausgabe oder der Nachlaufzeit gesagt wird, wird verworfen. Antworten können per Pause oder Stopp unterbrochen werden. Während einer Sitzung behält das Modell Gesprächskontext. Fortsetzen startet mit frischem Kontext. Für längere Gespräche muss die begrenzte Realtime-Sitzung neu geöffnet werden. [OpenAI Realtime – Gesprächsablauf](https://developers.openai.com/api/docs/guides/realtime-conversations).
- **Netzwerk.** Im Cloud-Modus gehen abgeschlossene Audio-Beiträge direkt an OpenAI. Die Ausgabe nutzt eine synthetische Standardstimme. Der Einstiegshinweis wird aus einem festen Text erzeugt und nur abgespielt, wenn das vom Modell gelieferte Transkript zum Text passt. Der sichtbare Hinweis im Stream bleibt Aufgabe des Hosts. [OpenAI-Modell](https://developers.openai.com/api/docs/models/gpt-realtime-2.1-mini) · [OpenAI-Datenverarbeitung](https://developers.openai.com/api/docs/guides/your-data) · [TikTok-Hilfe zu KI-Inhalten](https://support.tiktok.com/en/using-tiktok/creating-videos/ai-generated-content).
- **Schlüssel.** `%LOCALAPPDATA%\DebateAI\api-key.dpapi` enthält den per Windows DPAPI für das aktuelle Benutzerkonto verschlüsselten Schlüssel. **API-Schlüssel ändern → Schlüssel löschen** entfernt die lokale Kopie. Im OpenAI-Dashboard lässt sich der Schlüssel widerrufen.
- **Protokoll.** **Protokollordner** öffnet `%LOCALAPPDATA%\DebateAI`. `debate-ai.log` und bei Rotation `debate-ai.log.1` enthalten Zeit, Status, Laufzeiten, Beitragsdauer, Fehlerklassen und die verfügbaren Audio-/Text-Tokenzahlen. Fehlende und abgebrochene Antworten können in der API-Abrechnung erscheinen. Vergleiche die Logs mit dem Anbieter-Dashboard. Audio und Transkripte werden von dieser App nicht auf Datenträger geschrieben. Die Dateien enthalten keine Schlüssel.
- **Fehler und Neustart.** Prüfe bei stummem Eingang die Windows-Mikrofonberechtigung für Desktop-Apps und die Geräteauswahl. Meldet die App aufgebrauchtes API-Guthaben, prüfe die [OpenAI-API-Abrechnung](https://platform.openai.com/settings/organization/billing/overview) oder verwende einen Schlüssel aus einem API-Projekt mit Guthaben. Bei anderen OpenAI-Fehlern prüfe Schlüssel, Modellzugriff und Netzwerk. Bei einem Geräte- oder Netzwerkfehler stoppt die Sitzung. Klicke nach der Korrektur auf **Start**. Falls Windows beim Stoppen die Freigabe eines Audiogeräts verweigert, schalte die KI-Quelle im Mixer stumm und starte die App neu.

## 6. Projektdateien und lokale Prüfung

- `DEBATE-AI.exe` und `DEBATE-AI.exe.config` bilden die startfertige App.
- `src/MainForm.cs` enthält die Bedienoberfläche, `src/Audio.cs` die Windows-Geräte, `src/Turns.cs` die Sprechlogik, `src/Realtime.cs` den OpenAI- und Offline-Modus und `src/LocalData.cs` Schlüssel und Protokoll.
- `Neu-bauen.cmd` erzeugt die EXE mit dem mit Windows/.NET Framework bereitgestellten C#-Compiler. Beende eine geöffnete App vor dem Neubau.
- `Selbsttest.cmd` kompiliert und startet die Tests aus `tests/Tests.cs`. Die Tests verwenden erzeugte Audiosignale und simulierte Realtime-Ereignisse. Sie brauchen weder einen API-Schlüssel noch ein TikTok-Konto. Am Ende melden sie die gefundenen Windows-Geräte.

Der lokale Selbsttest prüft die Audiokonvertierung, die Sprechpausenerkennung, die Eingangssperre, manuelle Beiträge, das Verwerfen verspäteter Antworten, Wiederanlauf, Realtime-Ereignisse, DPAPI und den Windows-Fensteraufbau. Ein zusätzlicher Windows-Gerätetest hat am vorhandenen Eingang 24-kHz-Frames verarbeitet und am vorhandenen Ausgang stumme Wiedergabe gestartet, bis zum Ende laufen lassen und in einem zweiten Durchlauf abgebrochen. Die Abnahme einer echten deutschen Cloud-Antwort und des Gast-Routings in LIVE Studio erfolgt nach den Schritten oben mit deinem API-Projekt, deiner Studio-Version und den installierten virtuellen Geräten.

