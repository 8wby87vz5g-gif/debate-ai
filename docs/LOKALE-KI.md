# Lokaler KI-Modus (kostenlos, ohne OpenAI)

Stand 0.6.2 · **Auf deinem PC noch nicht getestet.** Auf einem Windows-Testrechner bei GitHub echt getestet (siehe unten). Geschwindigkeit und Antwortqualität sind erst nach der Messung unter „Lokale KI einrichten“ bekannt. Der lokale Modus wird ausdrücklich **nicht** als gleich schnell oder gleich intelligent wie OpenAI Realtime beworben.

## Aufbau

```text
Gast-Audio → lokale Satzende-Erkennung (Pegel + Pause) → whisper.cpp (Spracherkennung, whisper-server)
          → Ollama (Sprachmodell, z. B. Gemma 3 4B) → Satz für Satz → Piper (deutsche Stimme) → NOVA-Ausgang
```

- NOVA spricht den **ersten Satz**, sobald das Sprachmodell ihn fertig hat; die weiteren Sätze werden währenddessen erzeugt.
- Gesprächsverlauf (letzte 16 Beiträge), Debattenmethode, Persönlichkeit, Debattengedächtnis, Unterbrechen, Warteschlange, Echo-Schutz und TikTok-Prüfung funktionieren wie im OpenAI-Modus.
- **Sicherheitssperre:** Der lokale Modus kann technisch nur Dienste auf `127.0.0.1` erreichen. Jede andere Adresse wird abgelehnt. Im lokalen Modus wird nie eine OpenAI-Verbindung erzeugt; der API-Schlüssel wird nicht gelesen.
- Unterschiede zum OpenAI-Modus: keine semantische Satzende-Erkennung (Pause statt Inhalt), NOVA hört Tonfall nicht, Stimme weniger natürlich, Antworten kleiner Modelle einfacher.

## Komponenten und Quellen (Download nur nach deiner Bestätigung)

| Teil | Quelle | Lizenz |
|---|---|---|
| Ollama | ollama.com (Installation durch dich) | MIT |
| Sprachmodell | über Ollama, z. B. `llama3.2:3b`, `gemma3:4b`, `qwen2.5:3b`, `qwen3:8b` | Lizenz des Herstellers (z. B. Gemma-Lizenz) |
| whisper.cpp | GitHub ggml-org/whisper.cpp, neuestes Release **mit** `whisper-bin-x64.zip` (das aktuelle v1.9.5 enthält keine Windows-Dateien; die App nimmt dann automatisch das letzte Release mit Windows-Build) | MIT |
| Whisper-Modell | Hugging Face ggerganov/whisper.cpp, `ggml-tiny/base/small.bin` (mehrsprachig) | MIT |
| Piper | GitHub rhasspy/piper, Release 2023.11.14-2, `piper_windows_amd64.zip` | MIT |
| Stimmen | Hugging Face rhasspy/piper-voices v1.0.0 (Thorsten, Kerstin, Ramona) | siehe MODEL_CARD je Stimme |

Alles wird unter `%LOCALAPPDATA%\DebateAI\local` gespeichert. Die CI prüft bei jedem Build, ob diese Quellen erreichbar sind.

## Echte Messung (GitHub-Windows-VM, nicht dein PC)

AMD EPYC, **4 Threads**, 16 GB RAM, **ohne Grafikkarte**, Whisper base, Stimme Thorsten. Drei Debattenrunden je Modell, Zeit vom Ende des Gastbeitrags bis zum ersten Ton inklusive 760 ms Sprechpause (Version 0.6.1 ohne die Satzteil-Beschleunigung):

| Modell | Median | langsamste | Ø Wörter | Eindruck |
|---|---|---|---|---|
| qwen2.5:3b | 3,8 s | 4,1 s | 9 | sehr knapp, Deutsch holprig |
| llama3.2:3b | 5,2 s | 6,8 s | 29 | gute Rückfragen |
| gemma3:1b | 5,3 s | 6,3 s | 45 | hält Längengrenze nicht ein, schwach |
| gemma3:4b | 8,1 s | 8,4 s | 29 | bestes Deutsch |

Zweiter Lauf mit 0.6.2 (NOVA spricht schon nach dem ersten Satzteil; anderer, schnellerer Testrechner, ebenfalls 4 Threads ohne Grafikkarte):

| Modell | Median | langsamste | Ø Wörter |
|---|---|---|---|
| gemma3:1b | 2,5 s | 4,2 s | 41 |
| llama3.2:3b | 2,6 s | 3,0 s | 38 |
| qwen2.5:3b | 3,5 s | 3,8 s | 8 |

Die Testrechner schwanken stark (gleiches Modell 5,2 s bzw. 2,6 s). Entscheidend ist deshalb die Messung auf deinem PC.

Zum Vergleich OpenAI Realtime auf deinem PC: ca. 1–2 s. **Der lokale Modus ist deutlich langsamer und inhaltlich einfacher.** Whisper base verhört sich bei Fremdwörtern spürbar („Social Media“ → „Xerl-Midija“). Ein Laptop mit mehr Kernen oder eine NVIDIA-Grafikkarte kann schneller sein; das zeigt erst „Geschwindigkeit messen“ auf deinem PC.

## Hardware und Empfehlung

„Lokale KI einrichten“ liest Prozessor, Arbeitsspeicher und Grafikkarte und schlägt vor:

| Hardware | Sprachmodell | Whisper |
|---|---|---|
| NVIDIA mit ≥ 8 GB und ≥ 16 GB RAM | qwen3:8b (auf der Grafikkarte) | small |
| ≥ 8 GB RAM, ohne starke Grafikkarte (typischer Laptop) | llama3.2:3b (alternativ gemma3:4b für besseres Deutsch, qwen2.5:3b für Tempo) | base |
| weniger RAM | qwen2.5:3b | tiny |

Das ist nur ein Startpunkt. **„Geschwindigkeit messen“** misst auf deinem PC: Sprachausgabe eines Testsatzes, Spracherkennung, Sprachmodell bis zum ersten Satz und gesamt, Sprachausgabe des ersten Satzes. Daraus wird die Reaktionszeit geschätzt (inklusive 760 ms Sprechpause). Das Ergebnis steht im Dialog und im Protokoll.

## Einrichtung (ca. 15 Minuten, mehrere GB Download)

1. Ollama von ollama.com installieren und starten.
2. DEBATE AI → Gesprächssteuerung → **KI-Modus: Lokal auf diesem PC · kostenlos** → **Lokale KI einrichten**.
3. **Empfehlung übernehmen** → **Fehlendes herunterladen** (Liste der Quellen wird vorher angezeigt).
4. **Geschwindigkeit messen** und Ergebnis notieren.
5. Debatte starten wie gewohnt; für TikTok „TikTok LIVE verbinden“ (Schritt 5 prüft dann die lokale KI statt OpenAI).
