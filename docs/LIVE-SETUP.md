# NOVA live auf TikTok: Audio-Pipeline einrichten

Ziel: Ein echter Mensch im TikTok-LIVE spricht, NOVA hört zu, antwortet selbstständig, und der Mensch sowie das Publikum hören NOVA über den Stream.

```text
TikTok-Gast ──► LIVE Studio (Wiedergabe) ──► Voicemeeter VAIO ──► Bus B2 ──► DEBATE AI (Eingang)
                                                                               │
                                       OpenAI Realtime (Erkennung, Debatte, Stimme)
                                                                               │
TikTok-Gast ◄── LIVE-Stream ◄── LIVE Studio (Mikrofon = B1) ◄── Bus B1 ◄── Voicemeeter AUX ◄── DEBATE AI (Ausgang)
```

**Grundregel für Vollduplex:** NOVAs Ausgabe darf **nie** am Eingang von DEBATE AI ankommen. Dann kann NOVA zuhören, während sie spricht, und sich unterbrechen lassen. Den Rückweg über das Telefon des Gastes übernimmt dessen eingebaute Echounterdrückung.

## 1. Voicemeeter Banana

| Kanal in Voicemeeter | Gerät, das dort abspielt | A1 (Kopfhörer) | B1 (zu LIVE Studio) | B2 (zu DEBATE AI) |
|---|---|---|---|---|
| Voicemeeter Input (VAIO) | TikTok LIVE Studio (Gast-Ton) | an | **aus** | **an** |
| Voicemeeter AUX Input | DEBATE AI (NOVA) | an | **an** | **aus** |
| Hardware-Eingang 1 | dein Mikrofon (falls du mitredest) | aus | an | an (nur wenn NOVA dich auch hören soll) |

1. In LIVE Studio unter Einstellungen → Audio den **Lautsprecher/Ausgang** auf `Voicemeeter Input` stellen. Falls LIVE Studio keine Ausgabeauswahl hat: Windows → System → Sound → Lautstärkemixer → LIVE Studio → Ausgabegerät `Voicemeeter Input`.
2. In LIVE Studio als **Mikrofon** `Voicemeeter Out B1` wählen (genau einmal). Kein zusätzliches „Desktop-Audio“, sonst hört der Gast sich selbst.
3. In DEBATE AI: **Voicemeeter finden** (wählt B2 als Eingang und AUX als Ausgang) oder von Hand wählen.

## 2. Prüfen, bevor du live gehst

1. **Echoprüfung** in DEBATE AI. Erwartet: „Kein Echo“. Dann sind Vollduplex und Unterbrechungen vom ersten Wort an aktiv.
2. Bei „Echo erkannt“: Bus-Schalter oben prüfen. Bis das behoben ist, unter „Wenn der Gast unterbricht“ **Ignorieren (Halbduplex)** wählen.
3. **Pegel prüfen** mit einem Testgast oder einem Video auf `Voicemeeter Input`: Pegel reagiert, B1 bleibt still.
4. **Testton**: B1 und die LIVE-Studio-Mikrofonanzeige reagieren, der DEBATE-AI-Pegel bleibt unten.

## 3. Empfohlene Einstellungen für TikTok LIVE

| Einstellung | Wert |
|---|---|
| Sprecherwechsel | Automatisch · OpenAI erkennt das Satzende |
| Geduld beim Satzende | Geduldig (lässt Denkpausen zu) |
| Wenn der Gast unterbricht | NOVA stoppt sofort (lebendig) oder NOVA beendet ihren Satz (ruhiger) |
| Gesprächspartner ausreden lassen | an |
| Echo-Schutz | an |
| Live-Mitschrift, Debattengedächtnis | an |
| Nachfrage nach s | 8–12 (NOVA belebt Pausen), 0 für reine Duelle |
| Antwortlänge | Kurz oder Mittel (Live wirkt kurz und schlagfertig besser) |

## 4. Kennzeichnung

- NOVA sagt beim Start hörbar, dass sie eine KI ist.
- Blende im Stream dauerhaft „NOVA · KI-Debattenpartnerin · synthetische Stimme“ ein und nutze die KI-Kennzeichnung von TikTok, falls angeboten.
- Informiere Gäste vor dem Zuschalten, dass ihre Beiträge zur Verarbeitung an OpenAI gehen.

## 5. Andere Programme

- **OBS Studio:** wie LIVE Studio: Mikrofon/Aux-Quelle = `Voicemeeter Out B1`, Gast-Anruf-App gibt auf `Voicemeeter Input` aus.
- **Discord:** Discord-Ausgabegerät `Voicemeeter Input`, Discord-Eingabegerät `Voicemeeter Out B1`. Discords Echounterdrückung für dieses Profil ausschalten (das Routing ist bereits getrennt).
- **Lokal am Tisch:** Mikrofon als Eingang, Kopfhörer als Ausgang. Lautsprecher nur mit Halbduplex.
