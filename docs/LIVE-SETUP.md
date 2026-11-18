# NOVA live auf TikTok: Einrichtung mit „TikTok LIVE verbinden“ (ab 0.5.0)

```text
TikTok-Gast → LIVE Studio (Ausgabe: Voicemeeter Input) → Banana VAIO → A1 (Kopfhörer) + B2 → DEBATE AI → OpenAI Realtime
TikTok-Gast ← LIVE-Stream ← LIVE Studio (Mikrofon: Voicemeeter Out B1) ← B1 ← Banana AUX ← DEBATE AI (NOVA)
                                                   Dein Mikrofon (Hardware-Eingang 1) → B1
```

**DEBATE AI verbindet sich nicht selbst mit TikTok.** Es nutzt die Windows-Audiogeräte, aus denen LIVE Studio aufnimmt und auf die es ausgibt. Die App zeigt deshalb nie „mit TikTok verbunden“ an, sondern prüft jedes Glied der Audiokette auf deinem PC.

## Einmalig vorbereiten

1. **Voicemeeter Banana** von [vb-audio.com](https://vb-audio.com/Voicemeeter/banana.htm) installieren (ersetzt Voicemeeter Standard) und **Windows neu starten**.
2. Voicemeeter Banana starten.
3. In **TikTok LIVE Studio**, Einstellungen → Audio:
   - **Mikrofon:** `Voicemeeter Out B1` (genau eine Mikrofonquelle, kein zusätzliches Desktop-Audio).
   - **Lautsprecher / Audiogerät:** `Voicemeeter Input`.

## In DEBATE AI: „TikTok LIVE verbinden“

| Schritt | Was passiert | Wer |
|---|---|---|
| 1 · Voicemeeter Banana | Erkennt Installation und Typ (Standard reicht nicht), startet Banana auf Knopfdruck | automatisch |
| 2 · Routing in Voicemeeter | Stellt über die offizielle Voicemeeter-Fernsteuerung ein: Gast-Ton (VAIO) → A1 + B2, nicht B1 · NOVA (AUX) → B1 (+ A1 zum Mithören), nicht B2 · dein Mikrofon → B1 · A1 = Borofone-Kopfhörer · Hardware-Eingang 1 = Mikrofonarray. Zeigt vorher alle Änderungen, ändert nur diese Schalter und liest sie zurück | automatisch nach deiner Bestätigung |
| 3 · DEBATE-AI-Geräte | Eingang `Voicemeeter Out B2`, Ausgang `Voicemeeter AUX Input` | automatisch |
| 4 · Routing-Messung | Spielt Testsignale auf den Gast-Weg und den NOVA-Weg und misst, ob sie genau auf B2 bzw. B1 ankommen und nicht zurücklaufen | automatisch (du hörst Testsignale) |
| 5 · OpenAI | Öffnet eine Realtime-Sitzung und misst die Aufbauzeit | automatisch |
| 6 · LIVE Studio | Erkennt, ob LIVE Studio läuft; Mikrofon- und Ausgabewahl bestätigst du | du |
| 7 · Gastton | 15 s lang: Gast spricht oder Ton läuft in LIVE Studio → Pegel an VAIO (über Voicemeeter) und B2 (DEBATE AI) | du lässt Ton laufen, Messung automatisch |
| 8 · NOVA | Zeigt Persönlichkeit, Position und Kostenlimit | Anzeige |
| 9 · Einwilligung | Gäste informiert, KI-Hinweis im Stream eingeblendet | du |

**Startfreigabe** gibt es erst, wenn 3, 4, 5, 6, 7 und 9 bestanden sind. Dann startet „Live-Debatte starten“ NOVA mit dem TikTok-Profil (OpenAI-Satzende, Geduld ausgewogen, kurze Antworten, NOVA stoppt bei Unterbrechung, Vollduplex ab dem ersten Wort, weil die Messung kein Echo gezeigt hat).

## Wenn Windows die Voicemeeter-Geräte anders benennt (ab 0.7.2)

Neuere Voicemeeter-Treiber unter Windows 11 heißen oft nicht „Voicemeeter Out B1/B2“ und „Voicemeeter AUX Input“, sondern:

- Aufnahme: „Voicemeeter Out 1“ … „Out 8“ (Reihenfolge A1–A5, B1–B3). **B1 = „Out 6“, B2 = „Out 7“.** In LIVE Studio als Mikrofon also „Voicemeeter Out 6“ wählen, wenn es kein „Out B1“ gibt.
- Wiedergabe: acht gleichnamige „Lautsprecher (VB-Audio Voicemeeter VAIO)“. Welcher davon „Voicemeeter Input“ (Gast) und welcher „AUX Input“ (NOVA) ist, sieht man am Namen nicht.

DEBATE AI erkennt B1/B2 automatisch. Für die Wiedergabegeräte in „TikTok LIVE verbinden“ → Schritt 3 **„Ausgänge per Testton zuordnen“** drücken (Voicemeeter muss laufen, Routing aus Schritt 2 aktiv). Danach Schritt 4 messen. Für LIVE Studio („Lautsprecher / Audiogerät“) den Ausgang nehmen, den die Zuordnung als „Voicemeeter Input“ meldet; im Windows-Sounddialog (`mmsys.cpl`) kann man ihn zur Übersicht in „Voicemeeter Input“ umbenennen.

**Bluetooth-Kopfhörer als A1:** Nutzt Windows oder eine App gleichzeitig das Headset-Mikrofon, schaltet der Kopfhörer in den Freisprech-Modus (16 kHz, mono) und Voicemeeter meldet „Failed to Open Main Output Device (A1)“. Dann läuft kein Ton durch Voicemeeter. Abhilfe: In Voicemeeter A1 über **MME** statt WDM wählen, als Eingang 1 das eingebaute Mikrofon (nicht das Headset-Mikrofon) nehmen, im Sounddialog unter „Aufnahme“ das Headset-Mikrofon deaktivieren und unter „Kommunikation“ „Nichts unternehmen“ wählen.

## Wenn LIVE Studio den Gast nicht getrennt ausgibt

Der Gastton-Test (Schritt 7) zeigt das ehrlich: Kommt bei VAIO nichts an oder hörst du dort auch dich selbst und den Stream, gibt LIVE Studio den Gast nicht getrennt aus. **Unterstützte Alternative:** Den Gast über eine Anruf-App (z. B. Discord, Zoom) zuschalten, deren **Lautsprecher** `Voicemeeter Input` und deren **Mikrofon** `Voicemeeter Out B1` ist. In LIVE Studio bleibt `Voicemeeter Out B1` das Mikrofon; damit das Publikum den Gast hört, in LIVE Studio zusätzlich `Voicemeeter Out B2` als zweite Audioquelle hinzufügen. In Schritt 6 „Stattdessen: Gast über eine Anruf-App“ ankreuzen.

## Szene „DEBATE AI – NOVA LIVE“ (Overlay, ab 0.7.0)

DEBATE AI liefert die Live-Szene selbst aus, nur auf deinem PC (`127.0.0.1`), ohne Internet und ohne TikTok-Zugangsdaten.

**Empfohlen ab 0.7.2 (LIVE Studio 1.40.2 und neuer):** DEBATE AI → „3 · Gesprächssteuerung“ → **NOVA-Fenster für TikTok öffnen**. In LIVE Studio **Quelle hinzufügen → Fenster** → **„NOVA LIVE (für TikTok)“** wählen (nicht das DEBATE-AI-Bedienfenster), auf volle Höhe ziehen, Kamera darüber in den Rahmen „GAST“. Das Fenster offen lassen; es darf hinter anderen Fenstern liegen, aber nicht minimiert werden. Grund: LIVE Studio 1.40.2 lehnt in der Quelle „Link“ lokale Adressen wie `http://127.0.0.1:18190/nova` ab („Korrekte URL eingeben“, am 18.11.2026 auf einem echten PC beobachtet).

Der Link-Weg unten bleibt für ältere LIVE-Studio-Versionen und OBS.

![Szene](overlay-szene.png)

1. DEBATE AI starten → „3 · Gesprächssteuerung“ → **Stream-Overlay (Link kopieren)**. Der Link lautet normalerweise `http://127.0.0.1:18190/nova`. Ist der Port belegt (z. B. weil DEBATE AI mehrfach läuft), nimmt die App 18191–18194; dann nur eine DEBATE-AI-Instanz laufen lassen, damit der Link in der Szene stimmt.
2. TikTok LIVE Studio → neue **vertikale** Szene „DEBATE AI – NOVA LIVE“ → Quelle hinzufügen → **Link** → Link einfügen → Größe **1080 × 1920**.
3. Die Link-Quelle **ganz nach unten** legen. Kamera (oder Gast-Fenster) darüber in den Rahmen „GAST“ ziehen.
4. Alternative ohne Hintergrund: `http://127.0.0.1:18190/nova?view=badge` ist durchsichtig und zeigt nur Titel, KI-Hinweis und Sprechanzeige über der Kamera.

Die Szene zeigt: „DU GEGEN NOVA“, den dauerhaften Hinweis „NOVA · KI-Debattenpartnerin · synthetische Stimme“, das Debattenthema aus DEBATE AI und eine NOVA-Figur, deren Mund und Leuchten **NOVAs echter Ausgabelautstärke** folgen (keine Lippensynchronisation). Status: spricht / denkt nach / hört zu.

Bekannte Grenzen (laut Anbietern anderer Overlays): Die Link-Quelle von LIVE Studio nutzt einen älteren eingebauten Browser, der manchmal einfriert oder die Quelle winzig darstellt. Abhilfe: in den Quelleneigenschaften `?x=1` an den Link anhängen (lädt neu) oder die Quelle löschen und neu hinzufügen. Das Overlay ist deshalb bewusst schlicht programmiert. **In deiner LIVE-Studio-Version ungetestet.**

## Live-Titel und Beschreibung (zum Einfügen)

- Titel: **Traust du dich gegen eine KI? 🤖 | Debattiere LIVE mit NOVA**
- Beschreibung: *Diskutiere live mit NOVA, unserer künstlichen Intelligenz. Du vertrittst deine Meinung, NOVA antwortet mit eigenen Argumenten. Traust du dich?*
- Gäste: Mehrgast-/Gast-Anfragen in LIVE Studio erlauben. Profil, Benutzername und Kontoeinstellungen bleiben unverändert.

## Kosten beim ersten Test

Unter „3 · Gesprächssteuerung“: **Kostenlimit** auf z. B. **0,50 $** stellen und **„Bei Kostenlimit OpenAI-Sitzung ganz beenden“** angehakt lassen (Standard). Beim Erreichen schließt DEBATE AI die OpenAI-Verbindung vollständig; es entstehen danach keine weiteren Kosten. Die Kosten sind eine Schätzung aus den von OpenAI gemeldeten Tokens; maßgeblich ist die Abrechnung im OpenAI-Konto. Für reine Audiotests den Modus „Offline-Probe“ oder „Lokal“ verwenden (kostenlos).

## Während der Live-Debatte

Die Kontrollzeile im Hauptfenster zeigt: ● Eingang (Gastton kommt an) · ● NOVA-Ausgang · ● OpenAI · ● Gast spricht · Echo-Warnung · geschätzte Kosten. Darüber: Latenz der letzten Antwort, Durchschnitt und p95.

Knöpfe: Debatte starten · Pause · NOVA soll sprechen / Jetzt antworten · NOVA unterbrechen · NOVA stumm · **Gast ausreden lassen** (an/aus, wirkt sofort) · Ruhe in der Debatte · **Not-Aus (Esc)**.

**Kostenlimit:** Standard 5 $ pro Debatte. Ist es erreicht, schaltet sich NOVA stumm und hört nur noch zu.

## Kennzeichnung und Einwilligung

- NOVA sagt beim Start hörbar, dass sie eine KI ist.
- Blende im Stream dauerhaft „NOVA · KI-Debattenpartnerin · synthetische Stimme“ ein und nutze die KI-Kennzeichnung von TikTok, falls angeboten.
- Informiere Gäste vor dem Zuschalten, dass ihre Beiträge an OpenAI gehen. DEBATE AI zeichnet keine Gespräche auf; die Mitschrift bleibt im Arbeitsspeicher.
