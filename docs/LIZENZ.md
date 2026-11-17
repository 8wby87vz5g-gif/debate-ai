# Eigentümer-Lizenz und Lizenzprüfung

## Stand heute

DEBATE AI hat **noch keine kostenpflichtigen Tarife, Testzeiten oder Sperren**. Alle Funktionen sind frei. Kosten entstehen nur durch die OpenAI-API (mit deinem eigenen Schlüssel); der lokale Modus ist kostenlos.

## Wie die Eigentümer-Lizenz funktioniert

- Lizenzen sind **digital signiert** (RSA 3072, SHA-256). Die App enthält nur den **öffentlichen** Schlüssel; den privaten Schlüssel hast nur du. Lizenzen lassen sich deshalb weder fälschen noch verändern (z. B. „Pro“ zu „Owner“).
- Eine **Eigentümer-Lizenz muss an PCs gebunden sein** (Geräte-ID, abgeleitet aus der Windows-MachineGuid). Weitergegeben funktioniert sie auf fremden PCs nicht.
- Prüfung: Signatur, Gerätebindung, Ablaufdatum. Getestet: gültig, fremder PC, verändert, fremd signiert, ohne Gerätebindung, abgelaufen.

**Ehrliche Grenze:** Jede reine Prüfung im Windows-Programm kann mit genug Aufwand durch Verändern der EXE entfernt werden. Für kostenpflichtige Kunden wird der Schutz deshalb zusätzlich auf dem Server liegen: Der OpenAI-Zugang für Kunden läuft später über den DEBATE-AI-Server, der nur gültigen Abos kurzlebige Sitzungsschlüssel ausgibt. Du als Eigentümer nutzt weiter deinen eigenen OpenAI-Schlüssel oder den lokalen Modus.

## Einrichtung (einmalig, auf deinem PC)

1. `DEBATE-AI-<Version>-LicenseTool.zip` aus dem Release entpacken.
2. Schlüsselpaar erzeugen, den privaten Schlüssel **außerhalb** des Projektordners speichern (z. B. auf einem USB-Stick):
   `LicenseTool.exe keygen D:\debate-ai-privat.xml`
3. Den ausgegebenen **öffentlichen** Schlüssel (`<RSAKeyValue>…</RSAKeyValue>`) an mich schicken. Er wird in `Licensing.PublicKeyXml` eingebaut, danach entsteht eine neue Version.
4. In DEBATE AI „Geräte-ID kopieren“ (unter Gesprächssteuerung).
5. Eigentümer-Lizenz erzeugen: `LicenseTool.exe owner D:\debate-ai-privat.xml "Murat Lüdecke" <Geräte-ID>` (mehrere PCs mit Komma).
6. In DEBATE AI „Lizenz eingeben“ und den ausgegebenen Text einfügen. Anzeige: „Eigentümer · alle Funktionen dauerhaft frei“.

Den privaten Schlüssel **nie** in GitHub, in Chats oder E-Mails weitergeben. `.gitignore` schließt typische Dateinamen aus.
