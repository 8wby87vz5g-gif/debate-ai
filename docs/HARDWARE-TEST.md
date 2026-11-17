# DEBATE AI 0.2.0 · Hardware- und Live-Test

Diese Tests kann nur ein echter Windows-PC mit Audiogeräten und deinem OpenAI-Schlüssel durchführen. Dauer: etwa 20 Minuten. Trage bei jedem Test **bestanden** oder **nicht bestanden** ein und schicke mir am Ende die Tabelle und die Datei `debate-ai.log` (Knopf **Protokollordner**). Die Datei enthält keine Gesprächsinhalte und keinen Schlüssel.

**Vorbereitung:** Kopfhörer verwenden (verhindert Echo). Installer `DEBATE-AI-Setup-0.2.0.exe` ausführen oder `Neu-bauen.cmd` und danach `Selbsttest.cmd` starten. Der Selbsttest muss mit lauter `OK`-Zeilen enden.

| Nr. | Test | Ablauf | Bestanden, wenn … | Ergebnis |
|---|---|---|---|---|
| 1 | App-Start | App öffnen | Dunkles Fenster mit NOVA-Symbol, keine Fehlermeldung, Geräte in beiden Listen | |
| 2 | Geräte | Mikrofon als Eingang, Kopfhörer als Ausgang wählen | Beide Auswahlen möglich | |
| 3 | Testton | **Testton** | Ton nur im Kopfhörer | |
| 4 | Pegel | **Pegel prüfen**, sprechen, dann still sein | Wellenform und dBFS reagieren beim Sprechen, fallen bei Stille unter −40 | |
| 5 | Offline-Probe | Haken **Offline-Probe**, **Debatte starten**, Satz sprechen, Pause | Nach der Pause Piepton, danach wieder „NOVA hört zu“ | |
| 6 | Schlüssel | **API-Schlüssel hinterlegen**, speichern | Offline-Haken geht aus, „API: Schlüssel hinterlegt“ | |
| 7 | KI-Hinweis | Einwilligung bestätigen, **Debatte starten** | NOVA sagt hörbar, dass sie eine KI ist | |
| 8 | **Automatische Antwort** | „Schulunterricht sollte morgens später beginnen.“ sagen, dann schweigen | Ohne Klick: „NOVA denkt nach“, dann **NOVA spricht hörbar** auf Deutsch | |
| 9 | Reaktionszeit | Zeit vom Ende deines Satzes bis NOVAs erstem Wort schätzen | Unter ca. 3 Sekunden (bitte Wert notieren) | |
| 10 | Kontext | Auf NOVAs Antwort direkt eingehen | NOVA bezieht sich auf das vorher Gesagte | |
| 11 | Mitschrift | Rechts mitlesen | Deine Worte als „Gast“, NOVAs Antwort als „Nova“ | |
| 12 | Kein Selbstgespräch | Nach NOVAs Antwort 15 s still sein | NOVA antwortet nicht auf sich selbst | |
| 13 | Ausreden lassen | Satz sagen, kurz Pause, dann sofort weitersprechen, während „NOVA denkt nach“ angezeigt wird | NOVA wartet oder verwirft ihre Antwort und antwortet danach auf beides | |
| 14 | Jetzt antworten | Lange sprechen und mittendrin **Jetzt antworten** | NOVA antwortet sofort auf das bisher Gesagte | |
| 15 | Unterbrechen | Während NOVA spricht **NOVA unterbrechen** | NOVA verstummt sofort; danach hört sie wieder zu | |
| 16 | Persönlichkeit | Stil „Humorvoll und schlagfertig“, Position z. B. „Für ein Tempolimit“, neu starten | Tonfall und Position ändern sich hörbar | |
| 17 | Redezeit | Video ohne Pausen auf den Eingang legen, Fairness **aus** | NOVA antwortet spätestens nach der max. Redezeit | |
| 18 | Not-Aus | Während NOVA spricht **Esc** | Ton stoppt sofort, Status „Gestoppt“ | |
| 19 | Netzwerkfehler | WLAN während einer Debatte trennen | Verständliche Fehlermeldung, nach Neustart wieder nutzbar | |
| 20 | Falscher Schlüssel | Ungültigen Schlüssel `sk-test-ungueltig-123456` speichern, starten | Meldung „API-Schlüssel abgelehnt“ | |
| 21 | Neustart | App schließen und neu öffnen | Geräte und Persönlichkeit sind wieder eingestellt | |
| 22 | Voicemeeter | **Voicemeeter finden** mit installiertem Voicemeeter | Passender Eingang gewählt, Routing-Hilfe erscheint | |

## Wenn Test 8 nicht besteht

- Steht oben gelb „Offline-Probe aktiv“? Haken entfernen.
- Steht der Status dauerhaft auf „Gesprächspartner spricht“? Raumgeräusch liegt über der Schwelle: Schwelle auf −32 dBFS erhöhen.
- Steht er auf „NOVA wartet, bis du ausgeredet hast“? Fairness ausschalten oder leiser werden.
- Fehlermeldung? Wortlaut notieren und `debate-ai.log` schicken.
