using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DebateAI
{
    // Guided setup in eight steps: scenario, partner input, NOVA output, level, voice,
    // echo check, settings, start. It drives the same session as the main window.
    internal sealed partial class MainForm
    {
        private const int WizardSteps = 8;

        private void ShowWizard()
        {
            if (!session.ReadyForStart) { MessageBox.Show(this, "Bitte zuerst die laufende Debatte beenden.", "Einrichtungsassistent"); return; }
            RefreshDevices();
            bool startNow = false;
            using (var dialog = new Form
            {
                Text = "DEBATE AI · Einrichtungsassistent", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(680, 560),
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Font,
                BackColor = Theme.Background, ForeColor = Theme.Text, KeyPreview = true
            })
            {
                int step = 0, scenario = 0;
                double peak = -90;
                bool levelOk = false, echoStarted = false;
                var stepLabel = new Label { AutoSize = true, ForeColor = Theme.Magenta, Location = new Point(24, 16) };
                var heading = new Label { AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Theme.Cyan, Location = new Point(22, 38) };
                var body = new FlowLayoutPanel { Location = new Point(24, 84), Size = new Size(632, 400), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
                Button back = Button("Zurück"), next = Big("Weiter", Theme.Cyan), cancel = Button("Abbrechen");
                back.Location = new Point(24, 500); cancel.Location = new Point(130, 500); next.Location = new Point(500, 494);
                dialog.Controls.AddRange(new Control[] { stepLabel, heading, body, back, cancel, next });

                var choices = new List<RadioButton>();
                var summary = Hint(600, Theme.Muted);
                for (int i = 0; i < Setup.Presets.Length; i++)
                {
                    int index = i;
                    var choice = new RadioButton { Text = Setup.Presets[i].Name, AutoSize = true, ForeColor = Theme.Text, Checked = i == 0, Margin = new Padding(3, 6, 3, 2) };
                    choice.CheckedChanged += delegate { if (choice.Checked) { scenario = index; summary.Text = Setup.Presets[index].Summary; } };
                    choices.Add(choice);
                }
                summary.Text = Setup.Presets[0].Summary;
                ComboBox inBox = Combo(), outBox = Combo();
                inBox.Width = outBox.Width = 600;
                foreach (object item in input.Items) inBox.Items.Add(item);
                foreach (object item in output.Items) outBox.Items.Add(item);
                var routingText = Hint(600, Theme.Muted);
                var deviceWarning = Hint(600, Theme.Amber);
                var meterBar = new ProgressBar { Minimum = 0, Maximum = 60, Width = 600, Height = 22 };
                var meterText = Hint(600, Theme.Muted);
                Button measure = Button("Messung starten"), voiceButton = Button("Stimmprobe abspielen"), echoButton = Button("Echoprüfung starten");
                var heard = new CheckBox { Text = "Ich höre NOVA genau dort, wo sie zu hören sein soll.", AutoSize = true, ForeColor = Theme.Text };
                var echoText = Hint(600, Theme.Muted);
                var halfDuplex = new CheckBox { Text = "Halbduplex verwenden (NOVA hört nicht zu, während sie spricht)", AutoSize = true, ForeColor = Theme.Text, Visible = false };
                var settingsText = Hint(600, Theme.Text);
                var applySettings = new CheckBox { Text = "Diese Einstellungen übernehmen", AutoSize = true, ForeColor = Theme.Text, Checked = true };
                var consent = Wide("Alle Sprecher wissen, dass NOVA eine KI ist und ihre Beiträge an OpenAI gehen, und sind einverstanden.");
                consent.Width = 600;

                Func<string> inputName = () => { var d = inBox.SelectedItem as Device; return d == null ? "" : d.Name; };
                Func<string> outputName = () => { var d = outBox.SelectedItem as Device; return d == null ? "" : d.Name; };
                Action updateWarnings = () =>
                {
                    List<string> warnings = Setup.Check(inputName(), outputName());
                    deviceWarning.Text = warnings.Count == 0 ? "" : "Achtung: " + string.Join(" ", warnings);
                };
                inBox.SelectedIndexChanged += delegate { input.SelectedItem = inBox.SelectedItem; updateWarnings(); };
                outBox.SelectedIndexChanged += delegate { output.SelectedItem = outBox.SelectedItem; updateWarnings(); };

                Action stopTests = () =>
                {
                    if (session.State == Phase.Meter || session.State == Phase.Tone || session.State == Phase.EchoTest || session.State == Phase.Connecting)
                        session.Stop(false);
                };
                measure.Click += delegate
                {
                    stopTests();
                    peak = -90; levelOk = false;
                    session.CheckLevel(OpenInput);
                };
                voiceButton.Click += async delegate
                {
                    stopTests();
                    if (!offline.Checked && (LocalMode || LocalData.HasKey)) await session.VoiceTest(OpenOutput, CreateVoiceDebater);
                    else session.TestTone(OpenOutput);
                };
                echoButton.Click += delegate
                {
                    stopTests();
                    echoStarted = true;
                    session.CheckEcho(OpenInput, OpenOutput);
                };

                Action show = null;
                show = () =>
                {
                    stepLabel.Text = "Schritt " + (step + 1) + " von " + WizardSteps;
                    body.Controls.Clear();
                    back.Enabled = step > 0;
                    next.Text = step == WizardSteps - 1 ? "Debatte starten" : "Weiter";
                    ScenarioPreset preset = Setup.Presets[scenario];
                    switch (step)
                    {
                        case 0:
                            heading.Text = "Wofür richtest du NOVA ein?";
                            foreach (RadioButton choice in choices) body.Controls.Add(choice);
                            body.Controls.Add(summary);
                            break;
                        case 1:
                            heading.Text = "Woher hört NOVA den Gesprächspartner?";
                            if (inBox.SelectedIndex < 0) inBox.SelectedIndex = Setup.Pick(Names(inBox), preset.Inputs, null);
                            routingText.Text = preset.Routing;
                            body.Controls.Add(Hint(600, Theme.Text, "Wähle den Eingang, auf dem nur der Gesprächspartner zu hören ist, nicht NOVA."));
                            body.Controls.Add(inBox); body.Controls.Add(routingText);
                            if (inBox.Items.Count == 0) body.Controls.Add(Hint(600, Theme.Amber, "Windows meldet keine Eingänge. Gerät anschließen und „Geräte neu laden“."));
                            break;
                        case 2:
                            heading.Text = "Wo soll NOVA sprechen?";
                            if (outBox.SelectedIndex < 0) outBox.SelectedIndex = Setup.Pick(Names(outBox), preset.Outputs, preset.AvoidOutputs);
                            updateWarnings();
                            body.Controls.Add(Hint(600, Theme.Text, scenario == 3 ? "Wähle deine Kopfhörer." : "Wähle den virtuellen Ausgang, den das Streaming-Programm als Mikrofon aufnimmt."));
                            body.Controls.Add(outBox); body.Controls.Add(deviceWarning);
                            break;
                        case 3:
                            heading.Text = "Pegel prüfen";
                            body.Controls.Add(Hint(600, Theme.Text, scenario == 3 ? "Starte die Messung und sprich ein paar Sätze." :
                                "Starte die Messung und lass den Gast sprechen oder spiele ein Video auf dem Gast-Weg ab. NOVA bleibt dabei still."));
                            body.Controls.Add(measure); body.Controls.Add(meterBar); body.Controls.Add(meterText);
                            break;
                        case 4:
                            heading.Text = "NOVAs Stimme testen";
                            body.Controls.Add(Hint(600, Theme.Text, LocalMode ? "NOVA spricht ihren KI-Hinweis einmal mit der lokalen Stimme (keine Kosten)." : !offline.Checked && LocalData.HasKey ?
                                "NOVA spricht ihren KI-Hinweis einmal auf dem gewählten Ausgang (wenige Cent API-Kosten)." :
                                "Ohne API-Schlüssel wird ein Testton gespielt. Hinterlege den Schlüssel im Hauptfenster, um NOVAs echte Stimme zu hören."));
                            body.Controls.Add(voiceButton); body.Controls.Add(heard);
                            body.Controls.Add(Hint(600, Theme.Muted, scenario == 3 ? "Du solltest NOVA im Kopfhörer hören." :
                                "Prüfe die Mikrofonanzeige im Streaming-Programm. Sie muss ausschlagen; der DEBATE-AI-Pegel darf dabei nicht reagieren."));
                            break;
                        case 5:
                            heading.Text = "Echoprüfung";
                            body.Controls.Add(Hint(600, Theme.Text, "DEBATE AI spielt 4 Sekunden ein Testsignal auf NOVAs Ausgang und prüft, ob es am Eingang wieder ankommt. Bitte währenddessen nicht sprechen."));
                            body.Controls.Add(echoButton); body.Controls.Add(echoText); body.Controls.Add(halfDuplex);
                            break;
                        case 6:
                            heading.Text = "Empfohlene Einstellungen";
                            bool forceHalf = halfDuplex.Checked || (scenario == 3 && Setup.Speakers(outputName()));
                            settingsText.Text =
                                "• Sprecherwechsel: " + mode.Items[preset.Mode] + "\n" +
                                "• Geduld beim Satzende: " + patience.Items[preset.Patience] + "\n" +
                                "• Wenn der Gast unterbricht: " + barge.Items[forceHalf ? 2 : preset.BargeIn] + "\n" +
                                "• Antwortlänge: " + length.Items[preset.Length] + "\n" +
                                "• Nachfrage nach Stille: " + (preset.Proactive == 0 ? "aus" : preset.Proactive + " s") + "\n" +
                                "• Ausreden lassen, Echo-Schutz, Debattengedächtnis, Live-Mitschrift: an";
                            body.Controls.Add(settingsText); body.Controls.Add(applySettings);
                            body.Controls.Add(Hint(600, Theme.Muted, "Persönlichkeit, Thema und Position stellst du danach im Hauptfenster unter „2 · NOVA-Persönlichkeit“ ein."));
                            break;
                        case 7:
                            heading.Text = "Bereit für die Live-Debatte";
                            if (offline.Checked)
                                body.Controls.Add(Hint(600, Theme.Amber, "Offline-Probe ist aktiv: NOVA antwortet nur mit einem Piepton. Für echte Debatten im Hauptfenster den API-Schlüssel hinterlegen."));
                            else body.Controls.Add(consent);
                            body.Controls.Add(Hint(600, Theme.Muted, "Blende im Stream dauerhaft „NOVA · KI-Debattenpartnerin · synthetische Stimme“ ein. " +
                                "Nach dem Gespräch kannst du mit „Live-Testbericht speichern“ Latenz und Gesprächswechsel dokumentieren."));
                            break;
                    }
                };

                var timer = new Timer { Interval = 100 };
                timer.Tick += delegate
                {
                    if (step == 3 && session.State == Phase.Meter)
                    {
                        peak = Math.Max(peak, session.Level);
                        meterBar.Value = Math.Max(0, Math.Min(60, (int)Math.Round(session.Level) + 60));
                        levelOk = peak >= (double)threshold.Value + 6;
                        meterText.Text = "Aktuell " + (session.Level <= -90 ? "—" : (int)session.Level + " dBFS") + " · Höchstwert " + (peak <= -90 ? "—" : (int)peak + " dBFS") +
                            (levelOk ? " · Signal erkannt ✓" : " · noch kein deutliches Signal");
                        meterText.ForeColor = levelOk ? Theme.Green : Theme.Muted;
                    }
                    if (step == 5)
                    {
                        if (session.State == Phase.EchoTest) echoText.Text = "Prüfung läuft …";
                        else if (echoStarted)
                        {
                            echoText.Text = session.Note ?? "";
                            echoText.ForeColor = session.EchoCheckPassed == true ? Theme.Green : session.EchoCheckPassed == false ? Theme.Red : Theme.Amber;
                            halfDuplex.Visible = session.EchoCheckPassed == false;
                            if (session.EchoCheckPassed == false && !halfDuplex.Tag.Equals(true)) { halfDuplex.Checked = true; halfDuplex.Tag = true; }
                        }
                    }
                };
                halfDuplex.Tag = false;
                timer.Start();

                back.Click += delegate { stopTests(); step = Math.Max(0, step - 1); show(); };
                cancel.Click += delegate { dialog.Close(); };
                next.Click += delegate
                {
                    if (step == 1 && inBox.SelectedItem == null) { MessageBox.Show(dialog, "Bitte einen Eingang wählen.", "Einrichtungsassistent"); return; }
                    if (step == 2 && outBox.SelectedItem == null) { MessageBox.Show(dialog, "Bitte einen Ausgang für NOVA wählen.", "Einrichtungsassistent"); return; }
                    if (step == 3 && !levelOk && MessageBox.Show(dialog, "Es wurde noch kein deutliches Signal gemessen. Trotzdem weiter?", "Einrichtungsassistent",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    if (step == 5 && session.State == Phase.EchoTest) return;
                    if (step == WizardSteps - 1)
                    {
                        if (!offline.Checked && !consent.Checked) { MessageBox.Show(dialog, "Bitte die Einwilligung bestätigen.", "Einrichtungsassistent"); return; }
                        stopTests();
                        if (applySettings.Checked) ApplyPreset(Setup.Presets[scenario], halfDuplex.Checked || (scenario == 3 && Setup.Speakers(outputName())));
                        if (!offline.Checked) informed.Checked = true;
                        startNow = true;
                        dialog.Close();
                        return;
                    }
                    stopTests();
                    step++;
                    show();
                };
                dialog.FormClosing += delegate { timer.Stop(); timer.Dispose(); stopTests(); };
                show();
                dialog.ShowDialog(this);
            }
            SaveSettings();
            Render();
            if (startNow) BeginInvoke(new Action(async () => await StartDebate()));
        }

        private void ApplyPreset(ScenarioPreset preset, bool halfDuplex)
        {
            mode.SelectedIndex = preset.Mode;
            patience.SelectedIndex = preset.Patience;
            barge.SelectedIndex = halfDuplex ? 2 : preset.BargeIn;
            length.SelectedIndex = preset.Length;
            proactive.Value = preset.Proactive;
            bargeMs.Value = preset.BargeInMs;
            fairness.Checked = preset.Fairness;
            echoGuard.Checked = preset.EchoGuard;
            memory.Checked = preset.Memory;
            transcribe.Checked = true;
            AddLine("System", "Einrichtung übernommen: " + preset.Name + (halfDuplex ? " (Halbduplex)" : "") + ".");
        }

        private IDebater CreateVoiceDebater()
        {
            if (LocalMode) return localEngine.Create(localModels, CurrentPersona(), "");
            return new RealtimeDebater(new WebSocketWire(LocalData.LoadKey()), new LiveOptions { Persona = CurrentPersona(), Transcribe = false, ServerTurns = false });
        }

        private static List<string> Names(ComboBox box)
        {
            var names = new List<string>();
            foreach (object item in box.Items) { var device = item as Device; names.Add(device == null ? "" : device.Name); }
            return names;
        }

        private static Label Hint(int width, Color color, string text = "")
        {
            return new Label { AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = color, Text = text, Margin = new Padding(3, 6, 3, 6) };
        }
    }
}
