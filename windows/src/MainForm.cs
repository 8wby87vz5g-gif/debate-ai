using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace DebateAI
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly ComboBox input = Combo(), output = Combo(), mode = Combo();
        private readonly CheckBox offline = new CheckBox { Text = "Offline-Probe mit Antwortton", AutoSize = true, Checked = true };
        private readonly CheckBox fairness = new CheckBox { Text = "Fairness-Modus", AutoSize = true, Checked = true };
        private readonly CheckBox informed = new CheckBox { Text = "Gäste sind über KI und Audioübermittlung informiert und einverstanden.", AutoSize = true };
        private readonly NumericUpDown threshold = new NumericUpDown { Minimum = -60, Maximum = -15, Value = -40, Width = 60 };
        private readonly NumericUpDown guard = new NumericUpDown { Minimum = 400, Maximum = 2500, Value = 900, Increment = 100, Width = 65 };
        private readonly NumericUpDown turnSeconds = new NumericUpDown { Minimum = 5, Maximum = 60, Value = 12, Width = 55, AccessibleName = "Maximale Redezeit in Sekunden" };
        private readonly Button refresh = Button("Geräte neu laden"), setup = Button("Voicemeeter-Geräte finden"), routing = Button("Routing-Hilfe"), key = Button("API-Schlüssel"), log = Button("Protokollordner");
        private readonly Button start = Button("Start"), pause = Button("Pause"), stop = Button("Not-Aus (Esc)");
        private readonly Button moderate = Button("Ruhe in der Debatte");
        private readonly Button push = Button("Aufnahme starten"), monitor = Button("Pegel prüfen"), tone = Button("Testton");
        private readonly Label state = new Label { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        private readonly Label note = new Label { AutoSize = true, MaximumSize = new Size(630, 0) };
        private readonly Label levelText = new Label { AutoSize = true, Text = "—" };
        private readonly ProgressBar meter = new ProgressBar { Minimum = 0, Maximum = 60, Width = 260, Height = 18, AccessibleName = "Eingangspegel" };
        private readonly Timer tick = new Timer { Interval = 25 };
        private readonly DebateSession session;
        private bool loggingFailed;

        public MainForm()
        {
            Text = "DEBATE AI · Windows-MVP";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(740, 620);
            MinimumSize = new Size(690, 555);
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(245, 247, 252);
            ForeColor = Color.FromArgb(27, 38, 63);
            KeyPreview = true;

            var clock = Stopwatch.StartNew();
            session = new DebateSession(() => clock.ElapsedMilliseconds);
            session.Log += message => { if (!LocalData.WriteLog(message)) loggingFailed = true; };
            session.Changed += Render;
            Compose();

            refresh.Click += delegate { RefreshDevices(); };
            setup.Click += delegate { SelectTikTokDevices(); };
            routing.Click += delegate { ShowRoutingHelp(); };
            key.Click += delegate { EditKey(); };
            log.Click += delegate { OpenLog(); };
            start.Click += async delegate
            {
                if (!offline.Checked && !informed.Checked) return;
                await session.Start(OpenInput, OpenOutput, CreateDebater, mode.SelectedIndex == 1,
                    (double)threshold.Value, (int)guard.Value, fairness.Checked, (int)turnSeconds.Value);
            };
            pause.Click += delegate { session.Stop(true); };
            stop.Click += delegate { session.Stop(false); };
            moderate.Click += delegate { session.Moderate(); };
            push.Click += async delegate { await session.Push(); };
            monitor.Click += delegate { session.CheckLevel(OpenInput); };
            tone.Click += delegate { session.TestTone(OpenOutput); };
            input.SelectedIndexChanged += delegate { Render(); };
            output.SelectedIndexChanged += delegate { Render(); };
            offline.CheckedChanged += delegate { informed.Checked = false; Render(); };
            informed.CheckedChanged += delegate { Render(); };
            tick.Tick += async delegate { await session.Tick(); if (!IsDisposed) RenderLevel(); };
            RefreshDevices();
            tick.Start();
        }

        private void Compose()
        {
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Padding = new Padding(22, 14, 22, 14) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(main);
            var title = new Label { Text = "DEBATE AI", AutoSize = true, Font = new Font("Segoe UI", 24, FontStyle.Bold) };
            main.Controls.Add(title);
            main.Controls.Add(Label("NOVA  ·  DEUTSCHE KI-DEBATTENPARTNERIN  ·  SYNTHETISCHE STIMME"));
            var devices = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 12, 0, 5) };
            devices.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            devices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            input.AccessibleName = "Eingang des Gesprächspartners";
            output.AccessibleName = "Ausgang der KI-Stimme";
            mode.AccessibleName = "Art der Sprecherkennung";
            threshold.AccessibleName = "Eingangsschwelle in dBFS";
            guard.AccessibleName = "Nachlaufzeit in Millisekunden";
            devices.Controls.Add(Label("Eingang · Gesprächspartner"), 0, 0); devices.Controls.Add(input, 1, 0);
            devices.Controls.Add(Label("Ausgang · KI-Stimme"), 0, 1); devices.Controls.Add(output, 1, 1);
            mode.Items.AddRange(new object[] { "Push-to-test · Start und Senden per Klick", "Automatisch · Ende nach Sprechpause" });
            mode.SelectedIndex = 0;
            devices.Controls.Add(Label("Sprecherwechsel"), 0, 2); devices.Controls.Add(mode, 1, 2);
            main.Controls.Add(devices);
            main.Controls.Add(Row(refresh, setup, routing));
            main.Controls.Add(Row(monitor, tone));
            main.Controls.Add(Row(Label("Schwelle (dBFS)"), threshold, Label("Nachlauf (ms)"), guard));
            main.Controls.Add(Row(fairness, Label("Redezeit (Sek.)"), turnSeconds));
            main.Controls.Add(Row(offline, key, log));
            main.Controls.Add(Row(informed));
            main.Controls.Add(Row(start, push, pause, stop));
            main.Controls.Add(Row(moderate));
            main.Controls.Add(state);
            main.Controls.Add(note);
            main.Controls.Add(Row(Label("Eingangspegel"), meter, levelText));
        }

        private void RefreshDevices()
        {
            input.Items.Clear(); output.Items.Clear();
            foreach (Device device in WinAudio.Devices(true)) input.Items.Add(device);
            foreach (Device device in WinAudio.Devices(false)) output.Items.Add(device);
            Render();
        }

        // Selects device names, not Voicemeeter routes. The user must still configure
        // buses in Voicemeeter and verify separation before going live.
        private static int FindDevice(ComboBox devices, params string[] names)
        {
            for (int n = 0; n < names.Length; n++)
                for (int i = 0; i < devices.Items.Count; i++)
                {
                    Device candidate = devices.Items[i] as Device;
                    if (candidate != null && candidate.Name.IndexOf(names[n], StringComparison.OrdinalIgnoreCase) >= 0)
                        return i;
                }
            return -1;
        }

        // Normal Voicemeeter provides only B1. Banana supports a separate B2 bus.
        // Never silently pretend that B1 alone provides two independent live paths.
        private void SelectTikTokDevices()
        {
            RefreshDevices();
            int bananaInput = FindDevice(input, "Voicemeeter Out B2", "Voicemeeter AUX Output", "Voicemeeter AUX Out");
            int bananaOutput = FindDevice(output, "Voicemeeter AUX Input", "Voicemeeter AUX In");
            bool banana = bananaInput >= 0 && bananaOutput >= 0;
            if (banana)
            {
                input.SelectedIndex = bananaInput;
                output.SelectedIndex = bananaOutput;
                MessageBox.Show(this, "Banana-Geräte erkannt und ausgewählt.\n\n" + RoutingInstructionsBanana(),
                    "DEBATE AI · Banana", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int standardInput = FindDevice(input, "Voicemeeter Out B1", "Voicemeeter Output");
            if (standardInput >= 0) input.SelectedIndex = standardInput;
            // In the one-bus edition an automatic NOVA output selection can create a loop.
            // Require deliberate selection of physical headphones/speakers for local tests.
            output.SelectedIndex = -1;
            MessageBox.Show(this,
                (standardInput >= 0 ? "Normales Voicemeeter erkannt: B1 als Eingang ausgewählt.\n\n" :
                    "Kein passender Voicemeeter-B1-Eingang gefunden. Geräte neu laden und Eingang von Hand wählen.\n\n") +
                RoutingInstructionsStandard(), "DEBATE AI · Standard-Voicemeeter",
                MessageBoxButtons.OK, standardInput >= 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static string RoutingInstructionsStandard()
        {
            return "STANDARD-VOICEMEETER (nur A / B): NUR LOKAL TESTEN!\n\n" +
                   "1. NOVA-Eingang: Voicemeeter Out B1 wählen.\n" +
                   "2. In Voicemeeter am YouTube-/Gast-Kanal B AN; A nach Bedarf.\n" +
                   "3. NOVA-Ausgang: deine echten Kopfhörer/Lautsprecher wählen, NICHT Voicemeeter Input/VAIO.\n" +
                   "4. YouTube starten, selbst still sein, dann 'Pegel prüfen'.\n" +
                   "5. Testton prüfen. Falls NOVA sich selbst hört, sofort 'Not-Aus' drücken.\n\n" +
                   "ACHTUNG: Ein einzelner B-Bus trennt Gast und NOVA nicht zuverlässig für TikTok LIVE. " +
                   "Für einen sicheren Livestream sind getrennte Audiowege nötig, z. B. Banana oder ein geeigneter virtueller Audiotreiber.";
        }

        private static string RoutingInstructionsBanana()
        {
            return "BANANA (getrennte Busse):\n\n" +
                   "1. Gast-Audio → Voicemeeter VAIO: A1 und B2 AN, B1 AUS.\n" +
                   "2. NOVA-Ausgabe → Voicemeeter AUX: B1 AN, B2 AUS; A1 nach Bedarf.\n" +
                   "3. LIVE Studio: B1 genau einmal als KI-Mikrofonquelle.\n" +
                   "4. Gasttest: DEBATE-AI-Eingangspegel reagiert, B1 bleibt still.\n" +
                   "5. Testton: B1 reagiert, B2 und DEBATE-AI-Eingang bleiben still.\n\n" +
                   "Nicht live gehen, solange ein Signal zum eigenen Eingang zurückläuft. " +
                   "TikTok muss den Gastton separat ausgeben können; dies ist nicht bei jedem Setup möglich.";
        }

        private void ShowRoutingHelp()
        {
            bool banana = FindDevice(input, "Voicemeeter Out B2", "Voicemeeter AUX Output") >= 0 &&
                          FindDevice(output, "Voicemeeter AUX Input") >= 0;
            MessageBox.Show(this, banana ? RoutingInstructionsBanana() : RoutingInstructionsStandard(),
                "DEBATE AI · Echo vermeiden", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private ICapture OpenInput() { return new WaveCapture(WinAudio.Resolve(true, input.SelectedItem as Device)); }
        private IPlayback OpenOutput() { return new WavePlayback(WinAudio.Resolve(false, output.SelectedItem as Device)); }
        private IDebater CreateDebater()
        {
            if (offline.Checked) return new OfflineDebater();
            return new RealtimeDebater(new WebSocketWire(LocalData.LoadKey()));
        }

        private void Render()
        {
            bool ready = session.ReadyForStart;
            bool hasInput = input.SelectedItem != null;
            bool hasOutput = output.SelectedItem != null;
            bool measuring = session.State == Phase.Meter;
            input.Enabled = output.Enabled = mode.Enabled = threshold.Enabled = guard.Enabled = refresh.Enabled = setup.Enabled = offline.Enabled = key.Enabled = ready;
            routing.Enabled = true;
            fairness.Enabled = turnSeconds.Enabled = ready;
            informed.Enabled = ready && !offline.Checked;
            start.Enabled = ready && hasInput && hasOutput && (offline.Checked || informed.Checked && LocalData.HasKey);
            start.Text = session.State == Phase.Paused || session.State == Phase.Moderated ? "Fortsetzen" : "Start";
            monitor.Enabled = ready && hasInput;
            tone.Enabled = hasOutput && (ready || measuring);
            push.Enabled = session.CanPush;
            push.Text = session.CanPush && session.State == Phase.Recording ? "Beitrag senden" : "Aufnahme starten";
            pause.Enabled = !ready && !measuring && session.State != Phase.Tone;
            stop.Enabled = session.State != Phase.Idle;
            moderate.Enabled = session.CanModerate;
            key.Text = LocalData.HasKey ? "API-Schlüssel ändern" : "API-Schlüssel";
            switch (session.State)
            {
                case Phase.Paused: state.Text = "Pausiert"; break;
                case Phase.Moderated: state.Text = "Ruhe in der Debatte · Gast hat das Wort"; break;
                case Phase.Meter: state.Text = "Routing prüfen"; break;
                case Phase.Tone: state.Text = "Testton"; break;
                case Phase.Connecting: state.Text = offline.Checked ? "Audiogeräte öffnen" : "OpenAI verbinden und KI-Hinweis vorbereiten"; break;
                case Phase.Listening: state.Text = "Bereit für den Gast"; break;
                case Phase.Recording: state.Text = "Beitrag aufnehmen"; break;
                case Phase.Thinking: state.Text = offline.Checked ? "Antwortton vorbereiten" : "KI verarbeitet den Beitrag"; break;
                case Phase.Speaking: state.Text = offline.Checked ? "Antwortton läuft" : "Nova spricht"; break;
                case Phase.Cooldown: state.Text = "Nachlauf · Eingang gesperrt"; break;
                case Phase.WaitingForQuiet: state.Text = "Nova wartet auf eine Sprechpause"; break;
                default: state.Text = "Bereit zur Einrichtung"; break;
            }
            string message = session.Note ?? "Wähle Eingang und Ausgang. Prüfe zuerst beide mit der Offline-Probe.";
            if (ready && (input.Items.Count == 0 || output.Items.Count == 0))
                message += " Windows meldet keinen Eingang oder Ausgang. Geräte anschließen und neu laden.";
            if (loggingFailed) message += " Das lokale Protokoll ist derzeit nicht beschreibbar.";
            note.Text = message;
            RenderLevel();
        }

        private void RenderLevel()
        {
            int db = (int)Math.Round(session.Level);
            int value = Math.Max(0, Math.Min(60, db + 60));
            if (meter.Value != value) meter.Value = value;
            string text = db <= -90 ? "—" : db + " dBFS";
            if (levelText.Text != text) levelText.Text = text;
        }

        private void EditKey()
        {
            using (var dialog = new Form { Text = "OpenAI-Projekt-API-Schlüssel", StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(510, 124), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Font })
            {
                var area = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), FlowDirection = FlowDirection.TopDown, WrapContents = false };
                var secret = new TextBox { Width = 472, MaxLength = 1024, UseSystemPasswordChar = true, AccessibleName = "Neuer API-Schlüssel" };
                Button save = Button("Speichern"), remove = Button("Schlüssel löschen"), cancel = Button("Abbrechen");
                remove.Enabled = LocalData.HasKey;
                area.Controls.Add(Label("Windows speichert den Schlüssel verschlüsselt für dein Benutzerkonto."));
                area.Controls.Add(secret);
                area.Controls.Add(Row(save, remove, cancel));
                dialog.Controls.Add(area);
                dialog.AcceptButton = save; dialog.CancelButton = cancel;
                save.Click += delegate { ChangeKey(dialog, () => LocalData.SaveKey(secret.Text)); };
                remove.Click += delegate { ChangeKey(dialog, LocalData.DeleteKey); };
                cancel.Click += delegate { dialog.Close(); };
                dialog.ShowDialog(this);
                secret.Clear();
            }
            Render();
        }

        private static void ChangeKey(Form dialog, Action action)
        {
            try { action(); dialog.Close(); }
            catch (AppError error) { MessageBox.Show(dialog, error.Message, "API-Schlüssel"); }
            catch (Exception) { MessageBox.Show(dialog, "Windows konnte den Schlüssel nicht speichern oder löschen.", "API-Schlüssel"); }
        }

        private void OpenLog()
        {
            try
            {
                System.IO.Directory.CreateDirectory(LocalData.Folder);
                Process.Start(new ProcessStartInfo { FileName = LocalData.Folder, UseShellExecute = true });
            }
            catch (Exception) { MessageBox.Show(this, "Der Protokollordner ist nicht zugänglich.", "DEBATE AI"); }
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (keys == Keys.Escape) { session.Stop(false); return true; }
            return base.ProcessCmdKey(ref message, keys);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tick.Dispose(); session.Dispose(); }
            base.Dispose(disposing);
        }

        private static Label Label(string text) { return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }; }
        private static Button Button(string text) { return new Button { Text = text, AutoSize = true, MinimumSize = new Size(82, 32) }; }
        private static ComboBox Combo() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; }

        private static FlowLayoutPanel Row(params Control[] items)
        {
            var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top,
                WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            row.Controls.AddRange(items);
            return row;
        }
    }
}
