using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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

    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(10, 12, 24);
        public static readonly Color Panel = Color.FromArgb(18, 22, 40);
        public static readonly Color Field = Color.FromArgb(26, 31, 56);
        public static readonly Color Text = Color.FromArgb(230, 236, 255);
        public static readonly Color Muted = Color.FromArgb(140, 152, 190);
        public static readonly Color Cyan = Color.FromArgb(0, 229, 255);
        public static readonly Color Violet = Color.FromArgb(157, 92, 255);
        public static readonly Color Magenta = Color.FromArgb(255, 61, 182);
        public static readonly Color Green = Color.FromArgb(57, 255, 160);
        public static readonly Color Amber = Color.FromArgb(255, 196, 61);
        public static readonly Color Red = Color.FromArgb(255, 77, 109);

        public static Color ForPhase(Phase phase, bool error)
        {
            if (error) return Red;
            switch (phase)
            {
                case Phase.Listening: return Cyan;
                case Phase.Recording: return Green;
                case Phase.Thinking: case Phase.Connecting: return Violet;
                case Phase.WaitingForQuiet: return Amber;
                case Phase.Speaking: return Magenta;
                case Phase.Cooldown: return Color.FromArgb(90, 120, 200);
                case Phase.Meter: case Phase.Tone: return Amber;
                default: return Muted;
            }
        }
    }

    // Large animated NOVA status symbol.
    internal sealed class NovaOrb : Control
    {
        public Phase Phase;
        public bool Error;
        public double Level = -90;
        private float pulse;

        public NovaOrb() { DoubleBuffered = true; Size = new Size(150, 150); BackColor = Theme.Panel; }

        public void Animate() { pulse += 0.08f; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = Theme.ForPhase(Phase, Error);
            float size = Math.Min(Width, Height) - 16;
            float activity = Phase == Phase.Speaking || Phase == Phase.Thinking || Phase == Phase.Connecting ? 1f :
                Phase == Phase.Recording ? (float)Math.Max(0, Math.Min(1, (Level + 60) / 45)) : 0.25f;
            float wobble = (float)(Math.Sin(pulse * (Phase == Phase.Thinking ? 2.2 : 1.2)) * 0.5 + 0.5) * activity;
            for (int ring = 3; ring >= 1; ring--)
            {
                float grow = size * (0.06f * ring + 0.06f * wobble);
                var rect = new RectangleF((Width - size) / 2 - grow / 2, (Height - size) / 2 - grow / 2, size + grow, size + grow);
                using (var pen = new Pen(Color.FromArgb(40 + 25 * (3 - ring), color), 2f)) g.DrawEllipse(pen, Shrink(rect, size * 0.08f * ring));
            }
            var core = new RectangleF((Width - size * 0.62f) / 2, (Height - size * 0.62f) / 2, size * 0.62f, size * 0.62f);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(core);
                using (var brush = new PathGradientBrush(path) { CenterColor = Color.FromArgb(255, Color.White), SurroundColors = new[] { color } })
                    g.FillEllipse(brush, core);
            }
            using (var font = new Font("Segoe UI", 11, FontStyle.Bold))
            using (var brush = new SolidBrush(Theme.Background))
            {
                var text = "NOVA";
                SizeF measure = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (Width - measure.Width) / 2, (Height - measure.Height) / 2);
            }
        }

        private static RectangleF Shrink(RectangleF r, float by) { r.Inflate(-by / 2, -by / 2); return r; }
    }

    // Animated level history (input or NOVA output activity).
    internal sealed class WaveView : Control
    {
        private readonly Queue<float> values = new Queue<float>();
        public Color Accent = Theme.Cyan;

        public WaveView() { DoubleBuffered = true; Height = 54; BackColor = Theme.Panel; }

        public void Push(double db)
        {
            values.Enqueue((float)Math.Max(0, Math.Min(1, (db + 60) / 50)));
            while (values.Count > 90) values.Dequeue();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float step = Width / 90f, mid = Height / 2f;
            int i = 90 - values.Count;
            using (var brush = new SolidBrush(Accent))
                foreach (float value in values)
                {
                    float h = Math.Max(2, value * (Height - 6));
                    g.FillRectangle(brush, i * step + 1, mid - h / 2, Math.Max(1, step - 2), h);
                    i++;
                }
        }
    }

    internal sealed class MainForm : Form
    {
        public const string Version = "0.2.0";

        private readonly ComboBox input = Combo(), output = Combo(), mode = Combo();
        private readonly ComboBox style = Combo(), intensity = Combo(), length = Combo(), voice = Combo(), language = Combo();
        private readonly TextBox personaName = Field(30), topic = Field(300), position = Field(400);
        private readonly CheckBox offline = Check("Offline-Probe (nur Piepton, keine KI)");
        private readonly CheckBox fairness = Check("Gesprächspartner ausreden lassen (Fairness)");
        private readonly CheckBox transcribe = Check("Live-Mitschrift anzeigen (zusätzliche Transkriptionskosten)");
        private readonly CheckBox informed = Check("Alle Sprecher wissen, dass NOVA eine KI ist und ihre Beiträge an OpenAI gehen, und sind einverstanden.");
        private readonly NumericUpDown threshold = Number(-60, -15, -40, 1), pauseMs = Number(300, 3000, 1200, 100),
            guard = Number(400, 2500, 900, 100), turnSeconds = Number(5, 120, 30, 5);
        private readonly Button refresh = Button("Geräte neu laden"), setup = Button("Voicemeeter finden"), routing = Button("Routing-Hilfe"),
            key = Button("API-Schlüssel"), log = Button("Protokollordner"), wipe = Button("Lokale Daten löschen"), monitor = Button("Pegel prüfen"), tone = Button("Testton");
        private readonly Button start = Big("Debatte starten", Theme.Green), pause = Big("Pause", Theme.Amber),
            interrupt = Big("NOVA unterbrechen", Theme.Magenta), push = Big("Jetzt antworten", Theme.Cyan),
            moderate = Big("Ruhe in der Debatte", Theme.Violet), stop = Big("Not-Aus (Esc)", Theme.Red);
        private readonly Label state = new Label { AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Theme.Text };
        private readonly Label note = new Label { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Theme.Muted };
        private readonly Label warning = new Label { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Theme.Amber, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        private readonly Label api = new Label { AutoSize = true, ForeColor = Theme.Muted };
        private readonly Label levelText = new Label { AutoSize = true, Text = "—", ForeColor = Theme.Muted };
        private readonly NovaOrb orb = new NovaOrb();
        private readonly WaveView wave = new WaveView();
        private readonly TextBox transcript = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Theme.Field,
            ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10.5f), AccessibleName = "Gesprächsmitschrift"
        };
        private readonly Timer tick = new Timer { Interval = 25 };
        private readonly DebateSession session;
        private bool loggingFailed, failed;

        public MainForm()
        {
            Text = "DEBATE AI " + Version + " · NOVA";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1180, 780);
            MinimumSize = new Size(980, 640);
            Font = new Font("Segoe UI", 10);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            KeyPreview = true;

            var clock = Stopwatch.StartNew();
            session = new DebateSession(() => clock.ElapsedMilliseconds);
            session.Log += message => { if (!LocalData.WriteLog(message)) loggingFailed = true; };
            session.Changed += Render;
            session.Line += AddLine;
            Fill();
            Compose();
            LoadSettings();

            refresh.Click += delegate { RefreshDevices(); };
            setup.Click += delegate { SelectTikTokDevices(); };
            routing.Click += delegate { ShowRoutingHelp(); };
            key.Click += delegate { EditKey(); };
            log.Click += delegate { OpenLog(); };
            wipe.Click += delegate { WipeLocalData(); };
            start.Click += async delegate
            {
                if (!offline.Checked && !informed.Checked) return;
                wiped = false;
                SaveSettings();
                AddLine("System", offline.Checked ? "Offline-Probe gestartet (nur Piepton)." : "Debatte gestartet. " + new Persona { Name = personaName.Text }.Disclosure());
                await session.Start(OpenInput, OpenOutput, CreateDebater, Options());
            };
            pause.Click += delegate { session.Stop(true); };
            stop.Click += delegate { session.Stop(false); };
            moderate.Click += delegate { session.Moderate(); };
            interrupt.Click += delegate { session.Interrupt(); };
            push.Click += async delegate { await session.Push(); };
            monitor.Click += delegate { session.CheckLevel(OpenInput); };
            tone.Click += delegate { session.TestTone(OpenOutput); };
            foreach (Control control in new Control[] { input, output, mode })
                ((ComboBox)control).SelectedIndexChanged += delegate { Render(); };
            offline.CheckedChanged += delegate { informed.Checked = false; Render(); };
            informed.CheckedChanged += delegate { Render(); };
            tick.Tick += async delegate
            {
                await session.Tick();
                if (IsDisposed) return;
                RenderLevel();
            };
            FormClosing += delegate { SaveSettings(); };
            RefreshDevices();
            RestoreDevices();
            tick.Start();
        }

        private void Fill()
        {
            mode.Items.AddRange(new object[] { "Automatisch · NOVA antwortet nach einer Sprechpause", "Push-to-talk · Aufnahme per Klick" });
            mode.SelectedIndex = 0;
            style.Items.AddRange(Persona.Styles); style.SelectedIndex = 0;
            intensity.Items.AddRange(Persona.Intensities); intensity.SelectedIndex = 1;
            length.Items.AddRange(Persona.Lengths); length.SelectedIndex = 1;
            voice.Items.AddRange(Persona.Voices); voice.SelectedIndex = 0;
            language.Items.AddRange(Persona.Languages); language.SelectedIndex = 0;
            personaName.Text = "Nova";
            fairness.Checked = true;
            transcribe.Checked = true;
            offline.Checked = !LocalData.HasKey;
            input.AccessibleName = "Eingang des Gesprächspartners";
            output.AccessibleName = "Ausgang der NOVA-Stimme";
            mode.AccessibleName = "Sprecherwechsel";
        }

        private void Compose()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 548));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            root.Controls.Add(left, 0, 0);
            var title = new Label { Text = "DEBATE AI", AutoSize = true, Font = new Font("Segoe UI", 26, FontStyle.Bold), ForeColor = Theme.Cyan };
            left.Controls.Add(title);
            left.Controls.Add(new Label { Text = "NOVA · KI-DEBATTENPARTNERIN · SYNTHETISCHE STIMME", AutoSize = true, ForeColor = Theme.Magenta, Margin = new Padding(4, 0, 3, 10) });

            left.Controls.Add(Section("1 · AUDIO",
                Grid(Pair("Eingang · Gesprächspartner", input), Pair("Ausgang · NOVA-Stimme", output)),
                Row(refresh, setup, routing), Row(monitor, tone, Caption("Pegel"), levelText)));
            left.Controls.Add(Section("2 · NOVA-PERSÖNLICHKEIT",
                Grid(Pair("Name", personaName), Pair("Stil", style), Pair("Intensität", intensity), Pair("Antwortlänge", length),
                     Pair("Stimme", voice), Pair("Sprache", language), Pair("Thema", topic), Pair("NOVAs Position", position)),
                transcribe));
            left.Controls.Add(Section("3 · GESPRÄCHSSTEUERUNG",
                Grid(Pair("Sprecherwechsel", mode)),
                fairness,
                Row(Caption("Schwelle dBFS"), threshold, Caption("Sprechpause ms"), pauseMs),
                Row(Caption("Nachlauf ms"), guard, Caption("Max. Redezeit s"), turnSeconds),
                Row(offline), Row(key, log, wipe), informed));

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Background };
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(right, 1, 0);

            var status = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = Theme.Panel, Padding = new Padding(12) };
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var texts = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
            texts.Controls.Add(state); texts.Controls.Add(note); texts.Controls.Add(warning); texts.Controls.Add(api);
            // Status texts wrap to the space that is actually left next to the orb.
            status.SizeChanged += delegate
            {
                var wrap = new Size(Math.Max(200, status.ClientSize.Width - 200), 0);
                note.MaximumSize = warning.MaximumSize = wrap;
            };
            status.Controls.Add(orb, 0, 0);
            status.Controls.Add(texts, 1, 0);
            wave.Dock = DockStyle.Fill;
            status.Controls.Add(wave, 0, 1);
            status.SetColumnSpan(wave, 2);
            right.Controls.Add(status, 0, 0);

            var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 10, 0, 6) };
            controls.Controls.AddRange(new Control[] { start, pause, push, interrupt, moderate, stop });
            right.Controls.Add(controls, 0, 1);

            var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Theme.Field };
            logPanel.Controls.Add(transcript);
            right.Controls.Add(logPanel, 0, 2);
            right.Controls.Add(new Label
            {
                AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(3, 6, 3, 0),
                Text = "Die Mitschrift bleibt nur im Arbeitsspeicher und wird beim Schließen verworfen. Esc = Not-Aus."
            }, 0, 3);
        }

        private SessionOptions Options()
        {
            return new SessionOptions
            {
                Automatic = mode.SelectedIndex == 0, Threshold = (double)threshold.Value, GuardMs = (int)guard.Value,
                Fairness = fairness.Checked, TurnSeconds = (int)turnSeconds.Value, PauseMs = (int)pauseMs.Value
            };
        }

        private Persona CurrentPersona()
        {
            return new Persona
            {
                Name = personaName.Text, Style = style.SelectedIndex, Intensity = intensity.SelectedIndex, Length = length.SelectedIndex,
                Voice = voice.SelectedItem as string ?? "marin", Language = language.SelectedIndex, Topic = topic.Text, Position = position.Text
            };
        }

        private void AddLine(string speaker, string text)
        {
            if (IsDisposed) return;
            if (transcript.TextLength > 200000) transcript.Clear();
            transcript.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + speaker + ":  " + text + Environment.NewLine + Environment.NewLine);
        }

        private void RefreshDevices()
        {
            object inputSelection = input.SelectedItem, outputSelection = output.SelectedItem;
            input.Items.Clear(); output.Items.Clear();
            try
            {
                foreach (Device device in WinAudio.Devices(true)) input.Items.Add(device);
                foreach (Device device in WinAudio.Devices(false)) output.Items.Add(device);
            }
            catch (Exception)
            {
                AddLine("System", "Windows-Audiogeräte konnten nicht gelesen werden. Audiotreiber prüfen und „Geräte neu laden“.");
            }
            Reselect(input, inputSelection as Device);
            Reselect(output, outputSelection as Device);
            Render();
        }

        private static void Reselect(ComboBox box, Device previous)
        {
            if (previous == null) return;
            for (int i = 0; i < box.Items.Count; i++)
            {
                var device = (Device)box.Items[i];
                if (device.Name == previous.Name && (previous.Endpoint == null || device.Endpoint == previous.Endpoint)) { box.SelectedIndex = i; return; }
            }
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
            if (bananaInput >= 0 && bananaOutput >= 0)
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
                   "Bei einem Video, das ohne Pausen weiterläuft: 'Gesprächspartner ausreden lassen' ausschalten, " +
                   "sonst wartet NOVA auf eine Pause, die nie kommt.\n\n" +
                   "ACHTUNG: Ein einzelner B-Bus trennt Gast und NOVA nicht zuverlässig für TikTok LIVE. " +
                   "Für einen sicheren Livestream sind getrennte Audiowege nötig, z. B. Banana oder ein geeigneter virtueller Audiotreiber.";
        }

        private static string RoutingInstructionsBanana()
        {
            return "BANANA (getrennte Busse):\n\n" +
                   "1. Gast-Audio → Voicemeeter VAIO: A1 und B2 AN, B1 AUS.\n" +
                   "2. NOVA-Ausgabe → Voicemeeter AUX: B1 AN, B2 AUS; A1 nach Bedarf.\n" +
                   "3. LIVE Studio / OBS: B1 genau einmal als KI-Mikrofonquelle.\n" +
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
            return new RealtimeDebater(new WebSocketWire(LocalData.LoadKey()), CurrentPersona(), transcribe.Checked);
        }

        private void Render()
        {
            if (session.State == Phase.Idle && session.Note != null && session.Note.StartsWith("Gestoppt", StringComparison.Ordinal)) failed = false;
            else if (session.State == Phase.Idle && session.Note != null) failed = true;
            bool ready = session.ReadyForStart;
            bool hasInput = input.SelectedItem != null;
            bool hasOutput = output.SelectedItem != null;
            bool measuring = session.State == Phase.Meter;
            foreach (Control control in new Control[] { input, output, mode, threshold, guard, pauseMs, refresh, setup, offline, key, fairness,
                turnSeconds, style, intensity, length, voice, language, personaName, topic, position, transcribe, wipe })
                control.Enabled = ready;
            informed.Enabled = ready && !offline.Checked;
            start.Enabled = ready && hasInput && hasOutput && (offline.Checked || informed.Checked && LocalData.HasKey);
            start.Text = session.State == Phase.Paused || session.State == Phase.Moderated ? "Fortsetzen" : "Debatte starten";
            monitor.Enabled = ready && hasInput;
            tone.Enabled = hasOutput && (ready || measuring);
            push.Enabled = session.CanPush;
            push.Text = mode.SelectedIndex == 0 || session.State == Phase.Recording ? "Jetzt antworten" : "Aufnahme starten";
            interrupt.Enabled = session.CanInterrupt;
            pause.Enabled = !ready && !measuring && session.State != Phase.Tone;
            stop.Enabled = session.State != Phase.Idle;
            moderate.Enabled = session.CanModerate;
            key.Text = LocalData.HasKey ? "API-Schlüssel ändern" : "API-Schlüssel hinterlegen";
            bool error = session.State == Phase.Idle && failed;
            switch (session.State)
            {
                case Phase.Paused: state.Text = "Pausiert"; break;
                case Phase.Moderated: state.Text = "Ruhe · Gast hat das Wort"; break;
                case Phase.Meter: state.Text = "Routing prüfen"; break;
                case Phase.Tone: state.Text = "Testton"; break;
                case Phase.Connecting: state.Text = offline.Checked ? "Audiogeräte öffnen" : "Verbindung zu OpenAI wird aufgebaut"; break;
                case Phase.Listening: state.Text = "NOVA hört zu"; break;
                case Phase.Recording: state.Text = "Gesprächspartner spricht"; break;
                case Phase.Thinking: state.Text = offline.Checked ? "Antwortton vorbereiten" : "NOVA denkt nach"; break;
                case Phase.Speaking: state.Text = offline.Checked ? "Antwortton läuft" : "NOVA spricht"; break;
                case Phase.Cooldown: state.Text = "Nachlauf · Echo-Schutz"; break;
                case Phase.WaitingForQuiet: state.Text = "NOVA wartet, bis du ausgeredet hast"; break;
                default: state.Text = error ? "Fehler" : "Bereit zur Einrichtung"; break;
            }
            state.ForeColor = Theme.ForPhase(session.State, error);
            orb.Phase = session.State;
            orb.Error = error;
            wave.Accent = Theme.ForPhase(session.State, error);
            string message = session.Note ?? "Wähle Eingang und Ausgang, prüfe Pegel und Testton, dann „Debatte starten“.";
            if (ready && (input.Items.Count == 0 || output.Items.Count == 0))
                message += " Windows meldet keinen Eingang oder Ausgang. Geräte anschließen und neu laden.";
            if (loggingFailed) message += " Das lokale Protokoll ist derzeit nicht beschreibbar.";
            note.Text = message;
            warning.Text = offline.Checked
                ? "Offline-Probe aktiv: NOVA antwortet nur mit einem Piepton. Für echte Antworten den Haken entfernen und den API-Schlüssel hinterlegen."
                : !LocalData.HasKey ? "Kein API-Schlüssel hinterlegt." : !informed.Checked && ready ? "Bitte die Einwilligung der Sprecher bestätigen." : "";
            api.Text = offline.Checked ? "API: offline" :
                session.State == Phase.Idle || ready ? "API: " + (LocalData.HasKey ? "Schlüssel hinterlegt · " + RealtimeDebater.Model : "kein Schlüssel") :
                session.State == Phase.Connecting ? "API: verbindet …" : "API: Verbindung aktiv · " + RealtimeDebater.Model + " · Antworten: " + session.Answers;
            RenderLevel();
        }

        private void RenderLevel()
        {
            double level = session.State == Phase.Speaking ? -25 + 10 * Math.Sin(Environment.TickCount / 90.0) : session.Level;
            orb.Level = session.Level;
            orb.Animate();
            wave.Push(session.State == Phase.Speaking || session.State == Phase.Listening || session.State == Phase.Recording ||
                session.State == Phase.Meter ? level : -90);
            int db = (int)Math.Round(session.Level);
            string text = db <= -90 ? "—" : db + " dBFS";
            if (levelText.Text != text) levelText.Text = text;
        }

        private void EditKey()
        {
            using (var dialog = new Form { Text = "OpenAI-Projekt-API-Schlüssel", StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(530, 130), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Font,
                BackColor = Theme.Panel, ForeColor = Theme.Text })
            {
                var area = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), FlowDirection = FlowDirection.TopDown, WrapContents = false };
                var secret = new TextBox { Width = 490, MaxLength = 1024, UseSystemPasswordChar = true, AccessibleName = "Neuer API-Schlüssel" };
                Button save = Button("Speichern"), remove = Button("Schlüssel löschen"), cancel = Button("Abbrechen");
                remove.Enabled = LocalData.HasKey;
                area.Controls.Add(new Label { AutoSize = true, Text = "Windows speichert den Schlüssel verschlüsselt (DPAPI) für dein Benutzerkonto." });
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
            if (LocalData.HasKey && offline.Checked) offline.Checked = false;
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

        private void WipeLocalData()
        {
            if (MessageBox.Show(this, "API-Schlüssel, Einstellungen und Protokolle dieser App auf diesem PC löschen?",
                "DEBATE AI · Lokale Daten", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { LocalData.DeleteAll(); transcript.Clear(); wiped = true; offline.Checked = true; }
            catch (Exception) { MessageBox.Show(this, "Nicht alle Dateien konnten gelöscht werden.", "DEBATE AI"); }
            Render();
        }

        private bool wiped;

        private void SaveSettings()
        {
            if (wiped) return;
            var values = new Dictionary<string, object>();
            values["mode"] = mode.SelectedIndex; values["style"] = style.SelectedIndex; values["intensity"] = intensity.SelectedIndex;
            values["length"] = length.SelectedIndex; values["voice"] = voice.SelectedIndex; values["language"] = language.SelectedIndex;
            values["name"] = personaName.Text; values["topic"] = topic.Text; values["position"] = position.Text;
            values["fairness"] = fairness.Checked; values["transcribe"] = transcribe.Checked;
            values["threshold"] = (int)threshold.Value; values["pause"] = (int)pauseMs.Value; values["guard"] = (int)guard.Value; values["turn"] = (int)turnSeconds.Value;
            var inDevice = input.SelectedItem as Device; var outDevice = output.SelectedItem as Device;
            values["input"] = inDevice == null ? "" : inDevice.Name; values["output"] = outDevice == null ? "" : outDevice.Name;
            LocalData.SaveSettings(values);
        }

        private void LoadSettings()
        {
            var values = LocalData.LoadSettings();
            Select(mode, values, "mode"); Select(style, values, "style"); Select(intensity, values, "intensity");
            Select(length, values, "length"); Select(voice, values, "voice"); Select(language, values, "language");
            personaName.Text = LocalData.Text(values, "name", personaName.Text);
            topic.Text = LocalData.Text(values, "topic", ""); position.Text = LocalData.Text(values, "position", "");
            fairness.Checked = LocalData.Flag(values, "fairness", fairness.Checked);
            transcribe.Checked = LocalData.Flag(values, "transcribe", transcribe.Checked);
            Set(threshold, values, "threshold"); Set(pauseMs, values, "pause"); Set(guard, values, "guard"); Set(turnSeconds, values, "turn");
            savedInput = LocalData.Text(values, "input", ""); savedOutput = LocalData.Text(values, "output", "");
        }

        private string savedInput = "", savedOutput = "";

        private void RestoreDevices()
        {
            for (int i = 0; i < input.Items.Count; i++) if (((Device)input.Items[i]).Name == savedInput) input.SelectedIndex = i;
            for (int i = 0; i < output.Items.Count; i++) if (((Device)output.Items[i]).Name == savedOutput) output.SelectedIndex = i;
        }

        private static void Select(ComboBox box, Dictionary<string, object> values, string name)
        {
            int index = LocalData.Number(values, name, box.SelectedIndex);
            if (index >= 0 && index < box.Items.Count) box.SelectedIndex = index;
        }

        private static void Set(NumericUpDown box, Dictionary<string, object> values, string name)
        {
            int value = LocalData.Number(values, name, (int)box.Value);
            box.Value = Math.Max(box.Minimum, Math.Min(box.Maximum, value));
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

        private static Control Section(string heading, params Control[] items)
        {
            var box = new FlowLayoutPanel
            {
                AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Panel,
                Padding = new Padding(12, 10, 12, 10), Margin = new Padding(0, 0, 0, 12), Width = 490, MinimumSize = new Size(490, 0)
            };
            box.Controls.Add(new Label { Text = heading, AutoSize = true, ForeColor = Theme.Cyan, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(3, 0, 3, 6) });
            foreach (Control item in items) box.Controls.Add(item);
            return box;
        }

        private static Control[] Pair(string text, Control control) { return new Control[] { Caption(text), control }; }

        private static TableLayoutPanel Grid(params Control[][] pairs)
        {
            var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Width = 466 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286));
            for (int i = 0; i < pairs.Length; i++)
            {
                pairs[i][1].Width = 280;
                grid.Controls.Add(pairs[i][0], 0, i);
                grid.Controls.Add(pairs[i][1], 1, i);
            }
            return grid;
        }

        private static Label Caption(string text) { return new Label { Text = text, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(3, 7, 3, 3) }; }

        private static Button Button(string text)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(82, 32), FlatStyle = FlatStyle.Flat, BackColor = Theme.Field, ForeColor = Theme.Text };
            button.FlatAppearance.BorderColor = Theme.Violet;
            return button;
        }

        private static Button Big(string text, Color accent)
        {
            var button = new Button
            {
                Text = text, AutoSize = true, MinimumSize = new Size(150, 46), FlatStyle = FlatStyle.Flat, BackColor = Theme.Field,
                ForeColor = accent, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = new Padding(0, 0, 8, 8)
            };
            button.FlatAppearance.BorderColor = accent;
            button.FlatAppearance.BorderSize = 2;
            return button;
        }

        private static ComboBox Combo() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.Field, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat }; }
        private static TextBox Field(int max) { return new TextBox { MaxLength = max, BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle }; }
        private static CheckBox Check(string text) { return new CheckBox { Text = text, AutoSize = true, ForeColor = Theme.Text, MaximumSize = new Size(470, 0), Margin = new Padding(3, 4, 3, 4) }; }

        private static NumericUpDown Number(int min, int max, int value, int step)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Increment = step, Width = 70, BackColor = Theme.Field, ForeColor = Theme.Text };
        }

        private static FlowLayoutPanel Row(params Control[] items)
        {
            var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            row.Controls.AddRange(items);
            return row;
        }
    }
}
