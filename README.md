# DEBATE AI · NOVA

Windows-App für Live-Debatten mit der KI-Debattenpartnerin **NOVA** (TikTok LIVE, YouTube, Podcasts, Interviews) und die dazugehörige Website.

| Ordner | Inhalt |
|---|---|
| `windows/` | Windows-Client (C#, .NET Framework 4.8, WinForms, WinMM, OpenAI Realtime). Anleitung: [`windows/ANLEITUNG.md`](windows/ANLEITUNG.md) |
| `website/` | Produktwebsite (Next.js, TypeScript, Tailwind) |
| `docs/` | [Projektstatus](docs/STATUS.md) · [Testbericht](docs/TESTBERICHT.md) · [Hardware-Test](docs/HARDWARE-TEST.md) |
| `.github/workflows/` | Automatischer Windows-Build, Selbsttest, Installer und Website-Build |

## Schnellstart Windows

```bat
cd windows
Neu-bauen.cmd
Selbsttest.cmd
DEBATE-AI.exe
```

Fertige Installer und portable ZIPs entstehen bei jedem Push im GitHub-Actions-Lauf „Build und Tests“ als Artefakt **DEBATE-AI-Windows**.

## Sicherheit

- Keine API-Schlüssel oder Stripe-Geheimnisse im Repository. Der OpenAI-Schlüssel wird nur lokal per Windows-DPAPI gespeichert.
- Website-Geheimnisse ausschließlich über Umgebungsvariablen (`website/.env.example`).

Änderungen: [`CHANGELOG.md`](CHANGELOG.md)
