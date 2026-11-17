# Lokaler KI-Modus (kostenlos, ohne OpenAI)

Stand 0.6.0 · **Auf deinem PC noch nicht getestet.** Geschwindigkeit und Antwortqualität sind erst nach der Messung unter „Lokale KI einrichten“ bekannt. Der lokale Modus wird ausdrücklich **nicht** als gleich schnell oder gleich intelligent wie OpenAI Realtime beworben.

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
| Sprachmodell | über Ollama, z. B. `gemma3:4b`, `qwen3:8b`, `llama3.2:3b` | Lizenz des Herstellers (z. B. Gemma-Lizenz) |
| whisper.cpp | GitHub ggml-org/whisper.cpp, aktuelles Release, `whisper-bin-x64.zip` | MIT |
| Whisper-Modell | Hugging Face ggerganov/whisper.cpp, `ggml-tiny/base/small.bin` (mehrsprachig) | MIT |
| Piper | GitHub rhasspy/piper, Release 2023.11.14-2, `piper_windows_amd64.zip` | MIT |
| Stimmen | Hugging Face rhasspy/piper-voices v1.0.0 (Thorsten, Kerstin, Ramona) | siehe MODEL_CARD je Stimme |

Alles wird unter `%LOCALAPPDATA%\DebateAI\local` gespeichert. Die CI prüft bei jedem Build, ob diese Quellen erreichbar sind.

## Hardware und Empfehlung

„Lokale KI einrichten“ liest Prozessor, Arbeitsspeicher und Grafikkarte und schlägt vor:

| Hardware | Sprachmodell | Whisper |
|---|---|---|
| NVIDIA mit ≥ 8 GB und ≥ 16 GB RAM | qwen3:8b (auf der Grafikkarte) | small |
| ≥ 8 GB RAM, ohne starke Grafikkarte (typischer Laptop) | gemma3:4b | base |
| weniger RAM | llama3.2:3b | tiny |

Das ist nur ein Startpunkt. **„Geschwindigkeit messen“** misst auf deinem PC: Sprachausgabe eines Testsatzes, Spracherkennung, Sprachmodell bis zum ersten Satz und gesamt, Sprachausgabe des ersten Satzes. Daraus wird die Reaktionszeit geschätzt (inklusive 760 ms Sprechpause). Das Ergebnis steht im Dialog und im Protokoll.

## Einrichtung (ca. 15 Minuten, mehrere GB Download)

1. Ollama von ollama.com installieren und starten.
2. DEBATE AI → Gesprächssteuerung → **KI-Modus: Lokal auf diesem PC · kostenlos** → **Lokale KI einrichten**.
3. **Empfehlung übernehmen** → **Fehlendes herunterladen** (Liste der Quellen wird vorher angezeigt).
4. **Geschwindigkeit messen** und Ergebnis notieren.
5. Debatte starten wie gewohnt; für TikTok „TikTok LIVE verbinden“ (Schritt 5 prüft dann die lokale KI statt OpenAI).
