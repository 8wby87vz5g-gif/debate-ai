# DEBATE AI 0.5.0 · TikTok-Live-Abnahme

Stand 17.11.2026: Lokale Debatten mit echter OpenAI-Verbindung funktionieren laut deinem Test (Reaktionszeit ca. 1–2 s). **Die TikTok-Kette ist noch nicht auf echter Hardware getestet.**

## Die 10 Tests und wer sie prüft

| Nr. | Test | Wie geprüft | Art |
|---|---|---|---|
| 1 | Gastton erreicht Voicemeeter VAIO | „TikTok LIVE verbinden“ → Schritt 7: Pegel am VAIO-Kanal über die Voicemeeter-Fernsteuerung | automatische Messung, du lieferst den Ton |
| 2 | Gastton erreicht DEBATE AI über B2 | Schritt 4 (Testsignal auf Gast-Weg → B2) und Schritt 7 (echter Gastton an B2) | automatisch |
| 3 | NOVAs Stimme erreicht B1 | Schritt 4 (Testsignal auf NOVA-Weg → B1) | automatisch |
| 4 | LIVE Studio empfängt NOVAs Stimme | Schritt 3 der Assistenten-Stimmprobe bzw. Debatte: Mikrofonanzeige in LIVE Studio schlägt aus | **du** |
| 5 | NOVAs Stimme gelangt nicht auf ihren Eingang | Schritt 4 (NOVA-Weg darf nicht auf B2 ankommen) + Echo-Schutz während der Debatte | automatisch |
| 6 | Menschliche Sprache wird erkannt | Mitschrift „Gast: …“ während der Debatte | live, du |
| 7 | NOVA antwortet automatisch | Debatte ohne Klick | live, du |
| 8 | TikTok-Gast hört NOVA und kann antworten | Gast bestätigt | **live mit echtem Gast** |
| 9 | Mindestens zehn Gesprächswechsel | Live-Testbericht: „Gesprochene NOVA-Antworten“ ≥ 10 | live, Bericht |
| 10 | NOVA kann unterbrochen werden und hört weiter zu | Gast redet dazwischen; Bericht: „Unterbrechungen durch den Gast“ ≥ 1, danach weitere Antworten | live, Bericht |

## Ablauf (ca. 20 Minuten)

1. Voicemeeter Banana installieren, Windows neu starten, Banana starten, LIVE Studio einstellen (`docs/LIVE-SETUP.md`).
2. DEBATE AI 0.5.0 → **TikTok LIVE verbinden** → **Alles prüfen** → bei Schritt 2 **Routing einrichten** → erneut **Routing messen**.
3. In LIVE Studio Vorschau/Übungsmodus oder live mit eingeweihtem Gast: Schritt 7 **Gastton prüfen**.
4. Schritt 6 und 9 bestätigen → **Live-Debatte starten**.
5. Mindestens zehn Wechsel, einmal bewusst dazwischenreden, einmal provozieren.
6. **Live-Testbericht speichern**, dazu `debate-ai.log` (Protokollordner).

Bitte schicke mir: die Ergebnisse aus dem Dialog (Schritt 4 und 7 stehen auch im Log), den Live-Testbericht, `debate-ai.log` und Beobachtungen zu Test 4 und 8.
