using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DebateAI
{
    // "TikTok LIVE verbinden": checks every link of the audio chain on this PC and only then
    // releases the live debate. DEBATE AI never connects to TikTok itself; it uses the audio
    // devices that TikTok LIVE Studio records from and plays to.
    internal sealed partial class MainForm
    {
        private sealed class CheckRow
        {
            public Label Mark, Title, Detail;
            public bool Ok;

            public void Show(bool? ok, string detail)
            {
                Ok = ok == true;
                Mark.Text = ok == true ? "✓" : ok == false ? "✗" : "…";
                Mark.ForeColor = ok == true ? Theme.Green : ok == false ? Theme.Red : Theme.Amber;
                Detail.Text = detail;
            }
        }

        private static List<string> ItemsOf(ComboBox box)
        {
            var names = new List<string>();
            foreach (object item in box.Items) names.Add(item == null ? "" : item.ToString());
            return names;
        }

        private void ShowLiveConnect()
        {
            if (!session.ReadyForStart) { MessageBox.Show(this, "Bitte zuerst die laufende Debatte beenden.", "TikTok LIVE"); return; }
            if (offline.Checked || (CloudMode && !LocalData.HasKey))
            {
                MessageBox.Show(this, offline.Checked ? "Die Offline-Probe antwortet nur mit Piepton. Bitte unter „KI-Modus“ Lokal oder OpenAI wählen." :
                    "Für den OpenAI-Modus wird der API-Schlüssel benötigt. Alternativ unter „KI-Modus“ den kostenlosen lokalen Modus wählen.", "TikTok LIVE");
                return;
            }
            RefreshDevices();
            bool startNow = false;
            IVoicemeeter vm = null;
            VoicemeeterLayout layout = null;
            var clock = Stopwatch.StartNew();
            using (var dialog = new Form
            {
                Text = "DEBATE AI · TikTok LIVE verbinden", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(820, 720),
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Font,
                BackColor = Theme.Background, ForeColor = Theme.Text
            })
            {
                var list = new FlowLayoutPanel { Location = new Point(16, 16), Size = new Size(788, 600), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
                var verdict = new Label { Location = new Point(16, 628), AutoSize = true, MaximumSize = new Size(560, 0), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
                Button go = Big("Live-Debatte starten", Theme.Green), all = Big("Alles prüfen", Theme.Cyan), close = Button("Schließen"),
                    diagnose = Button("Diagnose kopieren");
                go.Location = new Point(600, 660); all.Location = new Point(430, 660); close.Location = new Point(16, 672); diagnose.Location = new Point(110, 672);
                go.Enabled = false;
                dialog.Controls.AddRange(new Control[] { list, verdict, go, all, close, diagnose });
                var rows = new List<CheckRow>();

                Func<string, Control[], CheckRow> row = (title, actions) =>
                {
                    var panel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Width = 760, BackColor = Theme.Panel, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(8) };
                    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
                    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 710));
                    var item = new CheckRow
                    {
                        Mark = new Label { AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold), Text = "·", ForeColor = Theme.Muted },
                        Title = new Label { AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Theme.Cyan, Text = title },
                        Detail = new Label { AutoSize = true, MaximumSize = new Size(700, 0), ForeColor = Theme.Muted, Text = "Noch nicht geprüft." }
                    };
                    var body = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 700 };
                    body.Controls.Add(item.Title); body.Controls.Add(item.Detail);
                    if (actions != null && actions.Length != 0) body.Controls.Add(Row(actions));
                    panel.Controls.Add(item.Mark, 0, 0); panel.Controls.Add(body, 1, 0);
                    list.Controls.Add(panel);
                    rows.Add(item);
                    return item;
                };

                // 1 · Voicemeeter
                Button launchVm = Button("Voicemeeter Banana starten"), downloadVm = Button("Download-Seite öffnen");
                launchVm.Visible = downloadVm.Visible = false;
                CheckRow vmRow = row("1 · Voicemeeter Banana", new Control[] { launchVm, downloadVm });

                // 2 · Routing in Voicemeeter
                ComboBox phones = Combo(), mic = Combo();
                phones.Width = mic.Width = 300;
                var monitor = new CheckBox { Text = "NOVA im Kopfhörer mithören", AutoSize = true, Checked = true, ForeColor = Theme.Text };
                var hearsHost = new CheckBox { Text = "NOVA hört auch mich", AutoSize = true, ForeColor = Theme.Text };
                Button applyRouting = Button("Routing einrichten");
                CheckRow routeRow = row("2 · Routing in Voicemeeter (Gast → A1 + B2, NOVA → B1, Mikrofon → B1)",
                    new Control[] { Caption("A1"), phones, Caption("Mikrofon"), mic });
                ((FlowLayoutPanel)routeRow.Detail.Parent).Controls.Add(Row(monitor, hearsHost, applyRouting));

                // 3 · Devices in DEBATE AI
                Button discover = Button("Ausgänge per Testton zuordnen (bis ca. 40 s, du hörst Testsignale)");
                discover.Visible = false;
                CheckRow deviceRow = row("3 · DEBATE-AI-Geräte (Eingang B2, Ausgang AUX)", new Control[] { discover });

                // 4 · Routing measurement
                Button measure = Button("Routing messen (ca. 10 s, du hörst Testsignale)");
                CheckRow measureRow = row("4 · Messung: Gast → B2, NOVA → B1, kein Rückweg (Tests 2, 3, 5)", new Control[] { measure });

                // 5 · OpenAI
                Button openAi = Button("Verbindung prüfen");
                CheckRow openAiRow = row(LocalMode ? "5 · Lokale KI (Ollama, Whisper, Piper)" : "5 · OpenAI Realtime", new Control[] { openAi });

                // 6 · LIVE Studio
                var liveMic = new CheckBox { Text = "In LIVE Studio ist „Voicemeeter Out B1“ (bei neueren Treibern „Voicemeeter Out 6“) als Mikrofon gewählt (genau einmal).", AutoSize = true, ForeColor = Theme.Text };
                var liveOut = new CheckBox { Text = "LIVE Studio gibt den Ton auf „Voicemeeter Input“ aus (Lautsprecher/Audiogerät; bei gleichnamigen Ausgängen den aus Schritt 3).", AutoSize = true, ForeColor = Theme.Text };
                var altGuest = new CheckBox { Text = "Stattdessen: Gast über eine Anruf-App, deren Ausgabe „Voicemeeter Input“ ist.", AutoSize = true, ForeColor = Theme.Text };
                CheckRow studioRow = row("6 · TikTok LIVE Studio (von dir zu bestätigen)", null);
                var studioBody = (FlowLayoutPanel)studioRow.Detail.Parent;
                studioBody.Controls.Add(liveMic); studioBody.Controls.Add(liveOut); studioBody.Controls.Add(altGuest);

                // 7 · Guest audio
                Button guestTest = Button("Gastton prüfen (15 s)");
                CheckRow guestRow = row("7 · Gastton kommt an (Tests 1, 2): Gast sprechen lassen oder Ton in LIVE Studio abspielen", new Control[] { guestTest });

                // 8 · NOVA and costs
                CheckRow novaRow = row("8 · NOVA-Persönlichkeit und Kostenlimit", null);

                // 9 · Consent
                var consentLive = Wide("Alle Gäste wissen, dass NOVA eine KI ist und ihre Beiträge an OpenAI gehen. Der Hinweis „NOVA · KI“ ist im Stream eingeblendet.");
                consentLive.Width = 700;
                CheckRow consentRow = row("9 · Einwilligung und KI-Kennzeichnung", null);
                ((FlowLayoutPanel)consentRow.Detail.Parent).Controls.Add(consentLive);
                consentRow.Detail.Text = "NOVA sagt beim Start hörbar, dass sie eine KI ist.";

                // Everything the checks found, as plain text to paste into a support chat (no keys, no recordings).
                diagnose.Click += delegate
                {
                    var text = new System.Text.StringBuilder();
                    text.AppendLine("DEBATE AI " + Version + " · Diagnose " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    text.AppendLine("Windows: " + Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " (64 Bit)" : ""));
                    text.AppendLine("KI-Modus: " + aiMode.Text + (LocalMode ? " · " + localModels.Llm + " · Whisper " + localModels.Whisper : "") +
                        " · API-Schlüssel: " + (LocalData.HasKey ? "hinterlegt" : "keiner"));
                    text.AppendLine("Gewählt: Eingang „" + input.Text + "“ · Ausgang „" + output.Text + "“");
                    text.AppendLine("Eingänge: " + string.Join(" | ", ItemsOf(input)));
                    text.AppendLine("Ausgänge: " + string.Join(" | ", ItemsOf(output)));
                    text.AppendLine("Voicemeeter: " + (vm == null ? "nicht verbunden" : "verbunden") + " · LIVE Studio läuft: " + (LiveStudio.Running() ? "ja" : "nein") +
                        " · Overlay: " + (overlay == null ? "aus" : overlay.Url));
                    text.AppendLine("Echoprüfung: " + (session.EchoCheckPassed == true ? "bestanden" : session.EchoCheckPassed == false ? "Echo erkannt" : "offen") +
                        " · Sprecherwechsel: " + mode.Text + " · Unterbrechung: " + barge.Text + " · Kostenlimit: " + costLimit.Value.ToString("0.00") + " $");
                    foreach (CheckRow item in rows)
                        text.AppendLine((item.Mark.Text == "✓" ? "[OK] " : item.Mark.Text == "✗" ? "[FEHLER] " : "[offen] ") + item.Title.Text + ": " + item.Detail.Text.Replace("\r", " ").Replace("\n", " "));
                    try { Clipboard.SetText(text.ToString()); MessageBox.Show(dialog, "Diagnose kopiert. Du kannst sie jetzt in den Chat einfügen.\nSie enthält keine Schlüssel und keine Aufnahmen.", "Diagnose"); }
                    catch (Exception) { MessageBox.Show(dialog, text.ToString(), "Diagnose"); }
                };

                Action update = () =>
                {
                    novaRow.Show(true, "NOVA: " + personaName.Text + " · " + style.Text + " · " + length.Text +
                        (position.Text.Trim().Length != 0 ? " · Position: " + position.Text.Trim() : "") +
                        " · Kostenlimit: " + (costLimit.Value == 0 ? "keins (nicht empfohlen)" : costLimit.Value.ToString("0.00") + " $ pro Debatte" + (stopAtLimit.Checked ? ", danach wird die Sitzung beendet" : ", danach nur stumm")));
                    bool studio = (liveMic.Checked && liveOut.Checked) || (liveMic.Checked && altGuest.Checked);
                    studioRow.Show(studio ? (bool?)true : null, (LiveStudio.Running() ? "TikTok LIVE Studio läuft. " : "TikTok LIVE Studio wurde nicht gefunden (bitte starten). ") +
                        "DEBATE AI verbindet sich nicht selbst mit TikTok; es nutzt die Audiogeräte, die LIVE Studio verwendet.");
                    consentRow.Ok = consentLive.Checked;
                    consentRow.Mark.Text = consentLive.Checked ? "✓" : "·";
                    consentRow.Mark.ForeColor = consentLive.Checked ? Theme.Green : Theme.Muted;
                    bool ready = deviceRow.Ok && measureRow.Ok && openAiRow.Ok && studioRow.Ok && guestRow.Ok && consentRow.Ok;
                    go.Enabled = ready;
                    var missing = new List<string>();
                    if (!deviceRow.Ok) missing.Add("3"); if (!measureRow.Ok) missing.Add("4"); if (!openAiRow.Ok) missing.Add("5");
                    if (!studioRow.Ok) missing.Add("6"); if (!guestRow.Ok) missing.Add("7"); if (!consentRow.Ok) missing.Add("9");
                    verdict.Text = ready ? "Startfreigabe erteilt: Die Audiokette auf diesem PC ist geprüft." : "Noch keine Startfreigabe. Offen: " + string.Join(", ", missing);
                    verdict.ForeColor = ready ? Theme.Green : Theme.Amber;
                };
                foreach (CheckBox box in new[] { liveMic, liveOut, altGuest, consentLive }) box.CheckedChanged += delegate { update(); };

                Action checkVoicemeeter = () =>
                {
                    if (vm != null) { vm.Dispose(); vm = null; }
                    launchVm.Visible = downloadVm.Visible = false;
                    try
                    {
                        var remote = new VoicemeeterRemote();
                        vm = remote;
                        int kind = remote.Kind;
                        layout = VoicemeeterLayout.For(kind);
                        if (kind == 0) { vmRow.Show(false, "Voicemeeter ist installiert, läuft aber nicht."); launchVm.Visible = true; }
                        else if (layout == null) { vmRow.Show(false, "Es läuft Voicemeeter Standard. Für getrennte Wege (B1 und B2) wird Voicemeeter Banana benötigt."); downloadVm.Visible = true; }
                        else
                        {
                            vmRow.Show(true, layout.Name + " läuft (" + remote.Folder + ").");
                            phones.Items.Clear(); mic.Items.Clear();
                            List<string> outs = vm.OutputDevices(), ins = vm.InputDevices();
                            foreach (string name in outs) phones.Items.Add(name);
                            foreach (string name in ins) mic.Items.Add(name);
                            phones.SelectedItem = VoicemeeterSetup.Pick(outs, "Borofone", "Kopfhörer", "Headphone", "Headset");
                            mic.SelectedItem = VoicemeeterSetup.Pick(ins, "Intel", "Mikrofonarray", "Microphone Array", "Mikrofon", "Microphone");
                        }
                    }
                    catch (AppError error) { vmRow.Show(false, error.Message); downloadVm.Visible = true; }
                    catch (Exception) { vmRow.Show(false, "Voicemeeter konnte nicht angesprochen werden."); }
                };

                Action assignDevices = () =>
                {
                    RefreshDevices();
                    Device b2 = FindB2();
                    Device aux = FindAux();
                    if (b2 != null) input.SelectedItem = b2;
                    if (aux != null) output.SelectedItem = aux;
                    bool unnamed = aux == null && DevicesOf(output).Exists(VirtualDevices.IsUnnamedVirtualOutput);
                    discover.Visible = unnamed || (FindVaio() == null && DevicesOf(output).Exists(VirtualDevices.IsUnnamedVirtualOutput));
                    deviceRow.Show(b2 != null && aux != null, b2 != null && aux != null ? "Eingang: " + b2.Name + " [" + b2.Id + "] · Ausgang: " + aux.Name + " [" + aux.Id + "]" :
                        "Nicht gefunden: " + (b2 == null ? "„Voicemeeter Out B2“ " : "") + (aux == null ? "„Voicemeeter AUX Input“" : "") +
                        (unnamed ? ". Windows nennt die Voicemeeter-Ausgänge hier nicht eindeutig. Zuerst Routing einrichten (Schritt 2), dann „Ausgänge per Testton zuordnen“." :
                        ". Voicemeeter Banana installieren, Windows neu starten und „Geräte neu laden“."));
                };

                Func<Task> runMeasure = async () =>
                {
                    Device b2 = FindB2();
                    Device b1 = FindB1();
                    Device vaio = FindVaio();
                    Device aux = FindAux();
                    if (b1 == null || b2 == null || vaio == null || aux == null)
                    {
                        measureRow.Show(false, "Für die Messung fehlen Voicemeeter-Geräte (B1, B2, Voicemeeter Input, Voicemeeter AUX Input).");
                        return;
                    }
                    measure.Enabled = all.Enabled = false;
                    measureRow.Show(null, "Messung läuft: Testsignal auf „Voicemeeter Input“ (Gast-Weg) …");
                    try
                    {
                        Func<Device, Func<ICapture>> listen = d => () => new WaveCapture(WinAudio.Resolve(true, d));
                        Func<int, Task> wait = ms => Task.Delay(ms);
                        Func<long> now = () => clock.ElapsedMilliseconds;
                        var results = new List<RouteResult>();
                        results.AddRange(await RoutingMeasurement.Measure("Gast-Weg (Voicemeeter Input)", () => new WavePlayback(WinAudio.Resolve(false, vaio)),
                            new[] { new Listener { Name = "B2 (DEBATE AI)", Open = listen(b2), Expected = true }, new Listener { Name = "B1 (Stream)", Open = listen(b1), Expected = false } }, now, wait));
                        measureRow.Show(null, "Messung läuft: Testsignal auf „Voicemeeter AUX Input“ (NOVA-Weg) …");
                        results.AddRange(await RoutingMeasurement.Measure("NOVA-Weg (Voicemeeter AUX Input)", () => new WavePlayback(WinAudio.Resolve(false, aux)),
                            new[] { new Listener { Name = "B1 (Stream)", Open = listen(b1), Expected = true }, new Listener { Name = "B2 (DEBATE AI)", Open = listen(b2), Expected = false } }, now, wait));
                        bool ok = results.TrueForAll(r => r.Ok);
                        var lines = new List<string>();
                        foreach (RouteResult result in results) { lines.Add(result.Describe()); LocalData.WriteLog("Routing-Messung: " + result.Describe()); }
                        measureRow.Show(ok, string.Join("\n", lines) + (ok ? "" : "\nRouting korrigieren (Schritt 2) und erneut messen."));
                    }
                    catch (Exception error) { measureRow.Show(false, error is AppError ? error.Message : "Messung fehlgeschlagen: " + error.GetType().Name); }
                    finally { measure.Enabled = all.Enabled = true; update(); }
                };

                Func<Task> runOpenAi = async () =>
                {
                    openAi.Enabled = false;
                    openAiRow.Show(null, "Verbinde …");
                    var started = Stopwatch.StartNew();
                    if (LocalMode)
                    {
                        try
                        {
                            LocalModels models = localModels;
                            Persona persona = CurrentPersona();
                            await Task.Run(() => { using (localEngine.Create(models, persona, "")) { } });
                            openAiRow.Show(true, "Lokale KI bereit: " + models.Llm + " · Whisper " + models.Whisper + " · " + models.Voice + " (" + started.ElapsedMilliseconds +
                                " ms). Keine API-Kosten. " + (lastBenchmark.Length == 0 ? "Tipp: unter „Lokale KI einrichten“ die Geschwindigkeit messen." : ""));
                        }
                        catch (Exception error) { openAiRow.Show(false, error is AppError ? error.Message : "Lokale KI nicht bereit: " + error.GetType().Name); }
                        finally { openAi.Enabled = true; update(); }
                        return;
                    }
                    try
                    {
                        using (var probe = new RealtimeDebater(new WebSocketWire(LocalData.LoadKey()), new LiveOptions { Persona = CurrentPersona(), Transcribe = true, ServerTurns = true, Patience = patience.SelectedIndex }))
                            await probe.Start(false, System.Threading.CancellationToken.None);
                        openAiRow.Show(true, "Sitzung mit " + RealtimeDebater.Model + " geöffnet und konfiguriert in " + started.ElapsedMilliseconds + " ms.");
                    }
                    catch (Exception error) { openAiRow.Show(false, error is AppError ? error.Message : "Keine Verbindung zu OpenAI: " + error.GetType().Name); }
                    finally { openAi.Enabled = true; update(); }
                };

                launchVm.Click += async delegate
                {
                    var remote = vm as VoicemeeterRemote;
                    if (remote == null || !remote.Launch(2)) { vmRow.Show(false, "Voicemeeter Banana konnte nicht gestartet werden. Bitte von Hand starten."); return; }
                    vmRow.Show(null, "Voicemeeter Banana wird gestartet …");
                    await Task.Delay(3000);
                    checkVoicemeeter(); update();
                };
                downloadVm.Click += delegate
                {
                    try { Process.Start(new ProcessStartInfo { FileName = "https://vb-audio.com/Voicemeeter/banana.htm", UseShellExecute = true }); } catch (Exception) { }
                    MessageBox.Show(dialog, "Nach der Installation von Voicemeeter Banana muss Windows neu gestartet werden. Danach DEBATE AI öffnen und erneut „TikTok LIVE verbinden“.", "Voicemeeter Banana");
                };
                applyRouting.Click += delegate
                {
                    if (vm == null || layout == null) { routeRow.Show(false, "Voicemeeter Banana muss laufen (Schritt 1)."); return; }
                    List<RoutingStep> plan = layout.Plan(phones.SelectedItem as string, mic.SelectedItem as string, monitor.Checked, hearsHost.Checked);
                    var preview = new List<string>();
                    foreach (RoutingStep step in plan) preview.Add("• " + step.Description);
                    if (MessageBox.Show(dialog, "DEBATE AI stellt in Voicemeeter ein:\n\n" + string.Join("\n", preview) +
                        "\n\nAndere Kanäle bleiben unverändert. Fortfahren?", "Routing einrichten", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    try
                    {
                        VoicemeeterSetup.Apply(vm, plan);
                        System.Threading.Thread.Sleep(300);
                        List<string> wrong = VoicemeeterSetup.Verify(vm, plan);
                        foreach (RoutingStep step in plan) LocalData.WriteLog("Voicemeeter: " + step.Description);
                        routeRow.Show(wrong.Count == 0, wrong.Count == 0 ? "Eingerichtet und zurückgelesen. Voicemeeter speichert die Einstellung beim Beenden automatisch." :
                            "Nicht übernommen: " + string.Join("; ", wrong));
                    }
                    catch (Exception error) { routeRow.Show(false, error is AppError ? error.Message : "Voicemeeter hat die Einstellung abgelehnt."); }
                    update();
                };
                measure.Click += async delegate { await runMeasure(); };
                discover.Click += async delegate
                {
                    Device b1 = FindB1(), b2 = FindB2();
                    if (b1 == null || b2 == null) { deviceRow.Show(false, "Für die Zuordnung fehlen die Eingänge B1 und B2 („Voicemeeter Out B1/B2“ bzw. „Out 6/7“)."); return; }
                    List<Device> candidates = DevicesOf(output).FindAll(VirtualDevices.IsUnnamedVirtualOutput);
                    if (candidates.Count == 0) { deviceRow.Show(false, "Keine unbenannten Voicemeeter-Ausgänge gefunden."); return; }
                    if (MessageBox.Show(dialog, "DEBATE AI spielt nacheinander auf bis zu " + candidates.Count + " Voicemeeter-Ausgängen ein kurzes Testsignal " +
                        "und hört auf B1 und B2 mit. Du hörst die Signale im Kopfhörer. Kein Internet, keine Kosten.\n\n" +
                        "Voraussetzung: Routing aus Schritt 2 (Gast/VAIO → A1 + B2, NOVA/AUX → B1). LIVE Studio stumm lassen. Fortfahren?",
                        "Ausgänge zuordnen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    discover.Enabled = measure.Enabled = all.Enabled = false;
                    try
                    {
                        OutputDiscovery found = await OutputDiscovery.Run(candidates,
                            d => () => new WavePlayback(WinAudio.Resolve(false, d)),
                            () => new WaveCapture(WinAudio.Resolve(true, b1)), () => new WaveCapture(WinAudio.Resolve(true, b2)),
                            () => clock.ElapsedMilliseconds, ms => Task.Delay(ms),
                            (n, total, d) => deviceRow.Show(null, "Testsignal " + n + " von " + total + ": " + d.Name + " [" + d.Id + "] …"));
                        foreach (DiscoveryStep step in found.Steps) LocalData.WriteLog("Ausgangs-Zuordnung: " + step.Describe());
                        if (found.Vaio != null) vaioEndpoint = found.Vaio.Endpoint ?? "";
                        if (found.Aux != null) auxEndpoint = found.Aux.Endpoint ?? "";
                        SaveSettings();
                        if (found.Aux != null) output.SelectedItem = found.Aux;
                        if (b2 != null) input.SelectedItem = b2;
                        var lines = found.Steps.ConvertAll(step => step.Describe());
                        deviceRow.Show(found.Complete, (found.Complete ? "Zugeordnet. Eingang: " + b2.Name + " [" + b2.Id + "] · Ausgang (AUX): " + found.Aux.Name + " [" + found.Aux.Id + "]\n" :
                            "Nicht vollständig zugeordnet. Routing in Voicemeeter prüfen (Schritt 2) und Voicemeeter-Engine läuft?\n") + string.Join("\n", lines));
                        if (found.Aux == null || found.Aux.Endpoint == null) discover.Visible = true;
                    }
                    catch (Exception error) { deviceRow.Show(false, error is AppError ? error.Message : "Zuordnung fehlgeschlagen: " + error.GetType().Name); }
                    finally { discover.Enabled = measure.Enabled = all.Enabled = true; update(); }
                };
                openAi.Click += async delegate { await runOpenAi(); };
                guestTest.Click += async delegate
                {
                    Device b2 = FindB2();
                    if (b2 == null) { guestRow.Show(false, "„Voicemeeter Out B2“ nicht gefunden."); return; }
                    guestTest.Enabled = false;
                    var probe = new GuestProbe();
                    try
                    {
                        using (var capture = new WaveCapture(WinAudio.Resolve(true, b2)))
                        {
                            var watch = Stopwatch.StartNew();
                            while (watch.ElapsedMilliseconds < 15000 && !(probe.VaioOk && probe.InputOk))
                            {
                                await Task.Delay(100);
                                double inputDb = -90;
                                capture.Drain(frame => inputDb = Math.Max(inputDb, TurnDetector.Level(frame)));
                                double vaioDb = vm != null && layout != null ? VoicemeeterSetup.Loudest(vm, 0, layout.GuestLevel) : inputDb;
                                probe.Add(vaioDb, inputDb, 100);
                                guestRow.Show(null, "Messe … VAIO " + (int)vaioDb + " dB · B2 " + (int)inputDb + " dBFS · " + (15 - watch.ElapsedMilliseconds / 1000) + " s");
                            }
                        }
                        guestRow.Show(probe.VaioOk && probe.InputOk, probe.Describe());
                        LocalData.WriteLog("Gastton-Test: " + probe.Describe());
                    }
                    catch (Exception error) { guestRow.Show(false, error is AppError ? error.Message : "Gastton-Test fehlgeschlagen."); }
                    finally { guestTest.Enabled = true; update(); }
                };
                all.Click += async delegate
                {
                    checkVoicemeeter(); assignDevices(); update();
                    await runOpenAi();
                    if (deviceRow.Ok) await runMeasure();
                    update();
                };
                close.Click += delegate { dialog.Close(); };
                go.Click += delegate
                {
                    ApplyPreset(Setup.Presets[0], false);
                    informed.Checked = true;
                    liveChecked = true;
                    startNow = true;
                    dialog.Close();
                };
                dialog.Shown += delegate { checkVoicemeeter(); assignDevices(); update(); };
                dialog.FormClosed += delegate { if (vm != null) vm.Dispose(); };
                dialog.ShowDialog(this);
            }
            SaveSettings();
            Render();
            if (startNow) BeginInvoke(new Action(async () => await StartDebate()));
        }

        private bool liveChecked;
    }
}
