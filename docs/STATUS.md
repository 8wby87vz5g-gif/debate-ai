# DEBATE AI · Projektstatus

Stand: 17.11.2026 · Windows-Client 0.2.0

Legende: ✅ fertig und automatisch getestet · 🟡 teilweise fertig · ⚪ implementiert, aber auf echter Hardware oder mit echter API **ungeprüft** · ❌ fehlt noch

## Ursache „NOVA hört, antwortet aber nicht“ (0.1.x)

Die Analyse des Quellcodes 0.1.2 ergab fünf Ursachen, die einzeln oder zusammen dazu führten, dass NOVA Audio empfing, aber nicht hörbar antwortete:

1. **Offline-Probe war standardmäßig an.** Damit antwortet NOVA nur mit einem Piepton, nie mit Sprache. Die Teststimme funktionierte, die KI wurde aber nie angesprochen.
2. **Push-to-test war der Standardmodus.** Ohne Klick auf „Aufnahme starten“ und „Beitrag senden“ ging nie ein Beitrag an OpenAI.
3. **Fairness verwarf Beiträge am Redezeitlimit (12 s).** Bei YouTube-Ton ohne Sprechpausen von 1,2 s wurde jeder Beitrag nach 12 s gelöscht, danach wartete NOVA auf Ruhe, die nie kam.
4. **Antworten länger als die Redezeit wurden stumm übersprungen.** Eine Antwort von drei Sätzen kann über 12 s dauern.
5. **Wortgenaue Prüfung des KI-Hinweises.** Wich das Modell beim Vorlesen minimal ab, brach die Sitzung schon beim Start ab.

Dazu kam die Latenz: Die komplette Antwort wurde gepuffert, bevor NOVA sprach.

## Windows-Client

| Komponente | Status | Hinweis |
|---|---|---|
| Automatische Antwort nach Sprechpause | ✅ Logik · ⚪ live | Standardmodus „Automatisch“, Redezeitlimit sendet statt verwirft |
| Gestreamte Antwortwiedergabe (Start nach ca. 240 ms) | ✅ Logik · ⚪ WinMM | Mehrpuffer-Wiedergabe neu; auf echter Soundkarte ungeprüft |
| OpenAI Realtime `gpt-realtime-2.1-mini` | ✅ Protokoll simuliert · ⚪ echte API | Modellname und Ereignisse laut aktueller Doku geprüft; kein echter API-Lauf möglich (kein Schlüssel in der Entwicklungsumgebung) |
| Live-Mitschrift (Gast und NOVA) | ✅ simuliert · ⚪ echte API | `gpt-4o-mini-transcribe`, nur im Arbeitsspeicher |
| Fairness, Warteschlange, Ausreden lassen | ✅ | Ungehörte Antwort wird verworfen und aus dem Kontext gelöscht |
| NOVA unterbrechen (Kontext auf Gehörtes kürzen) | ✅ simuliert · ⚪ echte API | `conversation.item.truncate` |
| Jetzt antworten | ✅ | |
| Echo-Schutz (Halbduplex + Nachlauf) | ✅ Logik · ⚪ Hardware | Kein akustischer Echo-Canceller; Kopfhörer empfohlen |
| KI-Persönlichkeiten (5 Stile, Intensität, Länge, Stimme, Sprache, Thema, Position) | ✅ | Eigene Persönlichkeiten speichern als Profile: ❌ |
| KI-Hinweis beim Start (Transparenz) | ✅ | Sinngemäße Prüfung + 1 Wiederholung |
| Dunkle Oberfläche, NOVA-Statussymbol, Wellenform, Statusanzeigen | 🟡 | Unter Mono/Linux gerendert geprüft; Darstellung unter Windows ungeprüft |
| Einstellungen speichern, lokale Daten löschen | ✅ Logik · ⚪ Windows | `settings.json`, kein Schlüssel darin |
| Geräteerkennung, Voicemeeter Standard/Banana | 🟡 | Aus 0.1.2 übernommen; Hardwaretest ausstehend |
| Audio-Einrichtungsassistent (geführt, 8 Schritte) | ❌ | Bisher Einzelknöpfe; Assistent folgt in Phase 3/4 |
| Modus A Mensch gegen NOVA | ✅ Logik · ⚪ live | |
| Modus B Livestream-Debatte | 🟡 | Funktioniert mit Fairness aus; getrennte Busse nötig |
| Modus C KI gegen KI | ❌ | Phase 3 |
| Modus D Moderation | ❌ | „Ruhe in der Debatte“ vorhanden; echte Moderatorrolle fehlt |
| NOVA stummschalten (zuhören ohne Antwort) | ❌ | Phase 3 |
| Lange Debatten > Sitzungsdauer, automatischer Neuaufbau | ❌ | Bei Sitzungsende Fehlermeldung, Neustart per Klick |
| Streaming-Profile TikTok LIVE Studio, OBS, Discord, lokal | 🟡 | Routing-Hilfe für Banana/Standard; feste Profile fehlen |
| Installer (Inno Setup, ohne Adminrechte, .NET-Prüfung) | ⚪ | Skript und CI-Job fertig; Ergebnis hängt vom ersten CI-Lauf ab |
| Update-Mechanismus | ❌ | Phase 7 |
| Code-Signierung (SmartScreen) | ❌ | Benötigt ein gekauftes Zertifikat (OV/EV) |

## Website und SaaS

| Komponente | Status | Hinweis |
|---|---|---|
| Landingpage, Demo (gescriptet), Preise, FAQ | 🟡 | Aus Vorversion; `typecheck` und `build` erfolgreich |
| Warteliste (Supabase) | 🟡 | Code vorhanden, nicht verbunden |
| Registrierung, Login, Kundenkonto | ❌ | Phase 6 |
| Stripe-Abos (Free / Creator / Pro) | ❌ | Phase 6 |
| Lizenz- und Kontingentprüfung, Kostenkontrolle | ❌ | Phase 6; Server gibt kurzlebige Realtime-Sitzungsschlüssel aus |
| Impressum, Datenschutzerklärung | ❌ | Platzhalter fehlen noch |

## Wichtige Architekturentscheidung für den Verkauf

Heute trägt jeder Nutzer seinen **eigenen** OpenAI-Schlüssel ein. Für Abos darf kein gemeinsamer Schlüssel im Client stecken. Geplant: Die Windows-App meldet sich am Kundenkonto an, der Server prüft Abo und Restkontingent und stellt pro Debatte ein kurzlebiges Realtime-Client-Secret aus. Damit bleiben Schlüssel, Abrechnung und Limits serverseitig.

## Nächste Schritte

1. **Du:** Hardwaretest nach `docs/HARDWARE-TEST.md` (ca. 20 Minuten) und Rückmeldung mit `debate-ai.log`.
2. Phase 3: Modus C (KI gegen KI), Modus D (Moderation), Stummschalten, Persönlichkeitsprofile, Einrichtungsassistent.
3. Phase 5: feste Routing-Profile für TikTok LIVE Studio, OBS, Discord.
4. Phase 6: Konten, Stripe, Lizenzserver mit kurzlebigen Sitzungsschlüsseln.
5. Phase 7: Update-Mechanismus, Code-Signierung.
