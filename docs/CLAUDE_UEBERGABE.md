# DEBATE AI / NOVA – Projektübergabe an Claude

## Inhalt
- DEBATE-AI-Windows-Standard-Voicemeeter.zip – neuester bekannter Windows-Projektstand, 10.11.2026
- DEBATE-AI-Windows.zip – vorheriger Windows-Projektstand zum Vergleich
- debate-ai-website.zip – Next.js-/TypeScript-Website mit Demo und Warteliste

## Wichtige Hinweise zum tatsächlichen Entwicklungsstand
Windows-Projekt: C#-Quelldateien und ältere DEBATE-AI.exe, .NET Framework 4.8.
Die aktuellste Windows-ZIP enthält Neu-bauen.cmd und Selbsttest.cmd.
Laut NEU_IN_DIESER_VERSION.md ist die EXE im Archiv die VORHERIGE
Version; die neueste Quellcodeänderung muss unter Windows erneut kompiliert werden.
Eine Windows-Kompilierung und ein Windows-Hardwaretest wurden nicht nachgewiesen.
Standard-Voicemeeter ist noch NICHT für eine sicher getrennte vollständige
TikTok-Live-Debatte fertig. Echo-/Loopback-Risiken sind zu beachten.

Website: Next.js + TypeScript + Tailwind, Demo und Supabase-Warteliste.
Das Vorhandensein von Code bedeutet nicht, dass alle Funktionen live oder
bereits vollständig implementiert sind.

## Bitte Claude
1. Alle ZIPs entpacken und den Code prüfen.
2. Die neuere Windows-Variante als Ausgangspunkt nehmen;
   die ältere Version nur zum Vergleich.
3. Erst Build und reproduzierbare Audiodiagnose, dann automatische
   OpenAI-Sprachantworten, danach Streaming-Routing und SaaS-Funktionen.
4. Keine Funktionen oder Tests als erfolgreich melden, die nicht geprüft wurden.
5. Keine OpenAI- oder Stripe-Geheimnisse in Client oder GitHub einchecken.
