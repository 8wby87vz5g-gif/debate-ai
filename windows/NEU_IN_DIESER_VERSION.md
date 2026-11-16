# DEBATE AI – Standard-Voicemeeter-Unterstützung

Diese Version ergänzt die bestehende automatische Geräteerkennung für **normales Voicemeeter mit A/B**. Nach Klick auf „Voicemeeter-Geräte finden“ wird bevorzugt Voicemeeter **Out B1** als Gesprächspartner-Eingang ausgewählt, wenn keine vollständige Banana-B2/AUX-Kombination vorhanden ist.

**Sicherheit:** In Standard-Voicemeeter wird die NOVA-Ausgabe absichtlich **nicht automatisch gewählt**. Für den lokalen Funktionstest wähle echte Kopfhörer/Lautsprecher als Ausgabe; wähle **nicht** den virtuellen Voicemeeter Input/VAIO, da sonst eine Rückkopplung entstehen kann. Die Standard-Version stellt nur einen virtuellen B-Ausgang bereit und ist damit **noch nicht bereit für eine vollständig getrennte TikTok-Live-Debatte**. Für Livebetrieb müssen Gast- und KI-Ausgabe nachweislich getrennt sein (z. B. mit Banana und separatem TikTok-Gast-Audio).

## So startest du die Änderung

1. ZIP vollständig entpacken; das alte Projekt als Sicherung behalten.
2. `Neu-bauen.cmd` ausführen; nur bei erfolgreichem Abschluss `DEBATE-AI.exe` starten.
3. „Voicemeeter-Geräte finden“ drücken.
4. Eingang B1 bestätigen und **physische** Kopfhörer/Lautsprecher als KI-Ausgang wählen.
5. In Voicemeeter am YouTube-Kanal **B aktivieren**, YouTube sprechen lassen und „Pegel prüfen“ klicken.
6. „Testton“ prüfen; bei Echo sofort „Not-Aus“.

Die enthaltene EXE ist die **vorherige Version** und muss lokal unter Windows neu kompiliert werden. Der Quellcode wurde auf Konsistenz geprüft, aber nicht unter Windows kompiliert/getestet.
