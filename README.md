# DEBATE AI · NOVA

Windows-App für Live-Debatten mit der KI-Debattenpartnerin **NOVA** (TikTok LIVE, YouTube, Podcasts, Interviews) und die dazugehörige Website.

| Ordner | Inhalt |
|---|---|
| `windows/` | Windows-Client (C#, .NET Framework 4.8, WinForms, WinMM, OpenAI Realtime). Anleitung: [`windows/ANLEITUNG.md`](windows/ANLEITUNG.md) |
| `website/` | Produktwebsite (Next.js, TypeScript, Tailwind) |
| `docs/` | [Projektstatus](docs/STATUS.md) · [TikTok-Live-Einrichtung](docs/LIVE-SETUP.md) · [Live-Abnahme](docs/HARDWARE-TEST.md) · [Testbericht](docs/TESTBERICHT.md) |
| `.github/workflows/` | Automatischer Windows-Build, Selbsttest, Installer und Website-Build |

## Schnellstart Windows

```bat
cd windows
Neu-bauen.cmd
Selbsttest.cmd
DEBATE-AI.exe
```

## Download

- **Releases** (empfohlen): Bei jedem Versions-Tag (`v0.4.0` …) legt GitHub Actions automatisch ein Release mit `DEBATE-AI-Setup-<Version>.exe` und einer portablen ZIP an.
- **Build-Artefakte:** Jeder Push auf `main` baut unter Windows, führt den Selbsttest aus, erstellt den Installer, installiert ihn testweise, startet die App und deinstalliert sie wieder. Ergebnis: Artefakt `DEBATE-AI-Windows-<Version>` im Actions-Lauf.

## Sicherheit

- Keine API-Schlüssel oder Stripe-Geheimnisse im Repository. Der OpenAI-Schlüssel wird nur lokal per Windows-DPAPI gespeichert.
- Website-Geheimnisse ausschließlich über Umgebungsvariablen (`website/.env.example`).

Änderungen: [`CHANGELOG.md`](CHANGELOG.md)
