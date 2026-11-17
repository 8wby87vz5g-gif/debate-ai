using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DebateAI
{
    internal sealed class TurnDetector : IDisposable
    {
        private const int MinimumVoiceMs = 200;
        private readonly MemoryStream recording = new MemoryStream();
        private readonly Queue<byte[]> lead = new Queue<byte[]>();
        private int onsetMs, voiceMs, silenceMs;
        private long taken;

        public bool Recording { get; private set; }
        public double Threshold { get; set; }
        public int EndSilenceMs { get; set; }
        public int MaximumTurnMs { get; set; }
        public bool ReachedLimit { get; private set; }
        public int VoiceMs { get { return Recording ? voiceMs : 0; } }

        public TurnDetector() { EndSilenceMs = 760; MaximumTurnMs = 12000; }

        public static double Level(byte[] frame)
        {
            if (frame.Length < 2) return -90;
            double squares = 0;
            for (int i = 0; i < frame.Length; i += 2)
            {
                double sample = WinAudio.ReadSample(frame, i) / 32768.0;
                squares += sample * sample;
            }
            return 10 * Math.Log10(Math.Max(1e-9, squares / (frame.Length / 2)));
        }

        public void BeginManual()
        {
            Reset();
            Recording = true;
        }

        public bool Add(byte[] frame, double level, bool automatic)
        {
            int ms = frame.Length * 1000 / WinAudio.BytesPerSecond;
            bool voiced = level >= Threshold;
            if (!Recording)
            {
                if (!automatic) return false;
                lead.Enqueue(frame);
                if (lead.Count > 6) lead.Dequeue();
                onsetMs = voiced ? onsetMs + ms : 0;
                if (onsetMs < 120) return false;
                foreach (byte[] prefix in lead) recording.Write(prefix, 0, prefix.Length);
                lead.Clear();
                voiceMs = onsetMs;
                Recording = true;
                return false;
            }

            recording.Write(frame, 0, frame.Length);
            if (voiced) { voiceMs += ms; silenceMs = 0; }
            else { silenceMs += ms; }
            if (automatic && silenceMs >= EndSilenceMs) return true;
            ReachedLimit = recording.Length >= (long)MaximumTurnMs * WinAudio.BytesPerSecond / 1000;
            return ReachedLimit;
        }

        // Bytes recorded since the last call, so a running turn can stream to the server.
        public byte[] TakeNew()
        {
            if (!Recording || recording.Length <= taken) return null;
            var chunk = new byte[recording.Length - taken];
            Array.Copy(recording.GetBuffer(), taken, chunk, 0, chunk.Length);
            taken = recording.Length;
            return chunk;
        }

        public byte[] Finish()
        {
            byte[] result = voiceMs >= MinimumVoiceMs ? recording.ToArray() : null;
            Reset();
            return result;
        }

        public void Reset()
        {
            recording.SetLength(0);
            lead.Clear();
            onsetMs = voiceMs = silenceMs = 0;
            taken = 0;
            Recording = false;
            ReachedLimit = false;
        }

        public void Dispose() { recording.Dispose(); }
    }

    internal enum Phase { Idle, Paused, Moderated, Meter, Tone, EchoTest, Connecting, Listening, Recording, Thinking, Speaking, Cooldown, WaitingForQuiet }

    // What happens when the partner starts talking while NOVA speaks.
    internal enum BargeIn { Stop, Finish, Ignore }

    internal sealed class SessionOptions
    {
        public bool Automatic = true;        // false = push-to-talk
        public bool ServerTurns = true;      // OpenAI semantic end-of-turn detection (if the debater supports it)
        public double Threshold = -40;
        public int GuardMs = 900;            // input lock after NOVA when echo cannot be ruled out
        public bool Fairness = true;         // ready answers wait for a pause; unheard answers yield to new speech
        public int TurnSeconds = 30;         // local turn detection: maximum turn
        public int PauseMs;                  // local turn detection: end pause (0 = default)
        public BargeIn BargeIn = BargeIn.Stop;
        public int BargeInMs = 500;          // speech needed before an interruption counts
        public bool EchoGuard = true;
        public int ProactiveSeconds;         // 0 = off; NOVA moves the debate on after this much silence
        public bool Memory = true;           // debate log for long debates (needs transcription)
        public bool RoutingVerified;         // echo check passed: full duplex from the first word
        public double CostLimit;             // USD per debate; 0 = no limit. Reaching it mutes NOVA.
        public bool StopAtCostLimit;         // Reaching the limit also ends the paid session completely.
    }

    // Live debate control. All state and device access belongs to the WinForms thread.
    // Network results arrive as LiveEvents and in the AudioSink of the current answer and are
    // processed in Tick, so the logic is deterministic and testable with a fake clock.
    internal sealed class DebateSession : IDisposable
    {
        public const int StartBufferMs = 240;
        public const int MaximumHoldMs = 6000;
        public const int MaximumRequeues = 2;
        public const int RequeueVoiceMs = 600;
        public const int AnswerTimeoutMs = 20000;
        public const int SessionRenewMs = 55 * 60 * 1000;
        public const int MemoryEveryAnswers = 4;
        public const string ProactiveText = "Der Gesprächspartner schweigt seit einigen Sekunden. Bringe die Debatte selbst voran: " +
            "mit einer kurzen, zugespitzten Rückfrage zu seinem letzten Argument oder einem neuen Gesichtspunkt. Höchstens zwei Sätze.";
        public const string TakeFloorText = "Ergreife jetzt selbst das Wort und bringe die Debatte mit einem pointierten Gedanken oder einer Rückfrage voran. Höchstens zwei Sätze.";

        private readonly TurnDetector detector = new TurnDetector();
        private readonly Func<long> milliseconds;
        private readonly List<string> newLines = new List<string>();
        private readonly List<string> recentLines = new List<string>();
        private ICapture capture;
        private IPlayback playback;
        private IDebater debater;
        private Func<IDebater> factory;
        private CancellationTokenSource lifetime;
        private SessionOptions options = new SessionOptions();
        private EchoGuard echo = new EchoGuard();
        private AudioSink sink;
        private bool serverTurns, answerActive, answerDone, dropped, pendingRespond, guestSpeaking, proactiveUsed, reconnecting;
        private int quietMs, bargeMs, requeues, failures, answersSinceMemory, reconnects, echoTestFrames, echoTestHits, echoTestLoud;
        private long listenAfter, holdUntil, turnEndAt, requestAt, firstAudioAt, lastActivity, sessionStarted, skipCommitUntil, echoTestEnd;
        private string lastNovaLine;
        private bool muted;
        private Task renewal;
        public Task Renewal { get { return renewal; } }

        public event Action Changed = delegate { };
        public event Action<string> Log = delegate { };
        // Transcript lines (speaker, text). Kept in memory only, never written to disk.
        public event Action<string, string> Line = delegate { };
        public Phase State { get; private set; }
        public string Note { get; private set; }
        public double Level { get; private set; }
        public double EchoScore { get { return echo.Score; } }
        public string Memory { get; private set; }
        public bool? EchoCheckPassed { get; private set; }
        public readonly LiveMetrics Metrics = new LiveMetrics();
        public int Answers { get { return Metrics.Answers; } }
        public BargeIn ActiveBargeIn { get { return options.BargeIn; } }
        public bool ReadyForStart { get { return State == Phase.Idle || State == Phase.Paused || State == Phase.Moderated; } }
        public bool CanModerate { get { return !ReadyForStart && State != Phase.Meter && State != Phase.Tone && State != Phase.EchoTest; } }
        public bool CanPush
        {
            get
            {
                if (!options.Automatic) return State == Phase.Listening || State == Phase.Recording;
                return !muted && (State == Phase.Recording || State == Phase.Listening);
            }
        }
        public bool CanInterrupt { get { return State == Phase.Speaking || State == Phase.Thinking || State == Phase.WaitingForQuiet; } }
        // "Gast ausreden lassen" can be switched during a running debate.
        public bool Fairness
        {
            get { return options.Fairness; }
            set { options.Fairness = value; Log(value ? "Gast ausreden lassen: an" : "Gast ausreden lassen: aus"); Changed(); }
        }

        public bool CostLimitReached { get; private set; }

        public bool Muted
        {
            get { return muted; }
            set { muted = value; Log(value ? "NOVA stummgeschaltet" : "NOVA antwortet wieder"); Changed(); }
        }

        public DebateSession(Func<long> milliseconds)
        {
            this.milliseconds = milliseconds;
            Level = -90;
            Memory = "";
        }

        // Memory plus the latest lines, used to seed a renewed connection.
        public string MemoryForReconnect
        {
            get
            {
                if (recentLines.Count == 0) return Memory;
                return (Memory.Length == 0 ? "" : Memory + " ") + "Zuletzt gesagt: " + string.Join(" | ", recentLines);
            }
        }

        public Task Start(Func<ICapture> openInput, Func<IPlayback> openOutput, Func<IDebater> create,
            bool automaticMode, double threshold, int guardMs, bool fairnessMode = true, int turnSeconds = 12)
        {
            return Start(openInput, openOutput, create, new SessionOptions
            {
                Automatic = automaticMode, Threshold = threshold, GuardMs = guardMs, Fairness = fairnessMode, TurnSeconds = turnSeconds
            });
        }

        public async Task Start(Func<ICapture> openInput, Func<IPlayback> openOutput, Func<IDebater> create, SessionOptions settings)
        {
            if (!ReadyForStart) return;
            options = settings ?? new SessionOptions();
            detector.Reset();
            detector.Threshold = options.Threshold;
            detector.MaximumTurnMs = Math.Max(5, Math.Min(120, options.TurnSeconds)) * 1000;
            detector.EndSilenceMs = options.PauseMs > 0 ? Math.Max(300, Math.Min(3000, options.PauseMs)) : options.Fairness ? 1200 : 760;
            echo = new EchoGuard();
            Metrics.Reset();
            factory = create;
            CostLimitReached = false;
            muted = false;
            ResetConversation();
            Memory = "";
            recentLines.Clear(); newLines.Clear();
            reconnects = failures = answersSinceMemory = 0;
            lastNovaLine = null;
            lifetime = new CancellationTokenSource();
            CancellationToken token = lifetime.Token;
            Set(Phase.Connecting, "Audiogeräte und Sitzung werden geöffnet …");
            try
            {
                playback = openOutput();
                capture = openInput();
                debater = create();
                serverTurns = options.Automatic && options.ServerTurns && debater.ServerTurns;
                byte[] intro = await debater.Start(true, token);
                if (token.IsCancellationRequested) return;
                long now = milliseconds();
                sessionStarted = lastActivity = now;
                Log("Sitzung offen · Sprecherwechsel " + (serverTurns ? "semantisch (OpenAI)" : options.Automatic ? "lokal (Pegel)" : "Push-to-talk") +
                    " · Unterbrechung " + options.BargeIn + (options.RoutingVerified ? " · Routing geprüft" : ""));
                if (intro != null && intro.Length != 0)
                {
                    answerDone = true;
                    playback.Play(intro);
                    echo.AddReference(intro, now);
                    Set(Phase.Speaking, "NOVA spricht den KI-Hinweis.");
                }
                else AfterSpeaking(now);
            }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error); }
        }

        private void ResetConversation()
        {
            sink = null;
            answerActive = answerDone = dropped = pendingRespond = guestSpeaking = proactiveUsed = reconnecting = false;
            quietMs = detector.EndSilenceMs;
            bargeMs = requeues = 0;
            turnEndAt = requestAt = firstAudioAt = skipCommitUntil = 0;
        }

        public void CheckLevel(Func<ICapture> openInput)
        {
            if (!ReadyForStart) return;
            try
            {
                capture = openInput();
                Set(Phase.Meter, "Nur Pegel. Sprich oder spiele den Testton ab, um die Trennung zu prüfen.");
            }
            catch (Exception error) { Fail(error); }
        }

        public void TestTone(Func<IPlayback> openOutput)
        {
            if (!ReadyForStart && State != Phase.Meter) return;
            if (playback != null) return;
            try
            {
                playback = openOutput();
                playback.Play(WinAudio.Tone(1200, 550));
                if (State != Phase.Meter) Set(Phase.Tone, "Testton läuft auf dem gewählten Ausgang.");
                else Set(Phase.Meter, "Testton läuft. Der Eingangspegel soll dabei unten bleiben.");
            }
            catch (Exception error) { Fail(error); }
        }

        // Lets NOVA speak her AI disclosure once on the chosen output, so the user hears the real voice.
        public async Task VoiceTest(Func<IPlayback> openOutput, Func<IDebater> create)
        {
            if (!ReadyForStart) return;
            lifetime = new CancellationTokenSource();
            CancellationToken token = lifetime.Token;
            Set(Phase.Connecting, "NOVAs Stimmprobe wird erzeugt …");
            try
            {
                playback = openOutput();
                IDebater voice = create();
                byte[] sample;
                try { sample = await voice.Start(true, token); }
                finally { Close(voice); }
                if (token.IsCancellationRequested) return;
                playback.Play(sample != null && sample.Length != 0 ? sample : WinAudio.Tone(1200, 550));
                Set(Phase.Tone, "Stimmprobe läuft auf dem gewählten Ausgang.");
            }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error); }
        }

        // Plays a speech-like test signal on NOVA's output and checks whether it returns on the input.
        public void CheckEcho(Func<ICapture> openInput, Func<IPlayback> openOutput)
        {
            if (!ReadyForStart) return;
            try
            {
                detector.Threshold = options.Threshold;
                capture = openInput();
                playback = openOutput();
                echo = new EchoGuard();
                echoTestFrames = echoTestHits = echoTestLoud = 0;
                byte[] signal = TestSignal(4000);
                long now = milliseconds();
                playback.Play(signal);
                echo.AddReference(signal, now);
                echoTestEnd = now + 4000 + 800;
                Set(Phase.EchoTest, "Echoprüfung läuft (4 s). Bitte nicht sprechen.");
            }
            catch (Exception error) { Fail(error); }
        }

        public static byte[] TestSignal(int ms)
        {
            var random = new Random(4711);
            int samples = WinAudio.Rate * ms / 1000;
            var data = new byte[samples * 2];
            double amplitude = 0, target = 0;
            int left = 0;
            for (int i = 0; i < samples; i++)
            {
                if (left-- <= 0)
                {
                    target = random.NextDouble() < 0.35 ? 0 : 0.25 + 0.6 * random.NextDouble();
                    left = WinAudio.Rate * (60 + random.Next(180)) / 1000;
                }
                amplitude += (target - amplitude) * 0.002;
                double value = amplitude * 9000 * (Math.Sin(2 * Math.PI * 180 * i / WinAudio.Rate) + 0.4 * Math.Sin(2 * Math.PI * 540 * i / WinAudio.Rate));
                WinAudio.WriteSample(data, i * 2, (short)Math.Max(-32768, Math.Min(32767, value)));
            }
            return data;
        }

        // Push-to-talk: start/stop recording. Automatic: end the running turn now, or let NOVA take the floor.
        public Task Push()
        {
            if (!CanPush) return Task.FromResult(0);
            long now = milliseconds();
            try
            {
                if (!options.Automatic)
                {
                    if (State == Phase.Recording) FinishLocalTurn(now);
                    else
                    {
                        capture.Drain(null);
                        detector.BeginManual();
                        Set(Phase.Recording, "Sprich jetzt. Zum Senden erneut klicken.");
                    }
                }
                else if (State == Phase.Recording)
                {
                    if (serverTurns)
                    {
                        debater.Commit();
                        skipCommitUntil = now + 1500;
                        guestSpeaking = false;
                        turnEndAt = now;
                        OnTurnComplete(now);
                    }
                    else FinishLocalTurn(now);
                }
                else if (!answerActive)
                {
                    Metrics.Proactive++;
                    RequestAnswer(TakeFloorText, now);
                }
            }
            catch (Exception error) { Fail(error); }
            return Task.FromResult(0);
        }

        // "NOVA unterbrechen": stops NOVA at once and gives the floor back to the partner.
        public void Interrupt()
        {
            if (!CanInterrupt) return;
            try
            {
                long now = milliseconds();
                StopNova(now);
                pendingRespond = false;
                Metrics.Interrupts++;
                EnterCooldown(now, "NOVA wurde unterbrochen. Der Gesprächspartner hat das Wort.");
            }
            catch (Exception error) { Fail(error); }
        }

        private void StopNova(long now)
        {
            int played = State == Phase.Speaking ? playback.PlayedMs : -1;
            playback.Stop();
            echo.StopReference(now);
            if (debater != null) debater.Cancel(played);
            if (answerActive) dropped = true;
            sink = null;
            Log("NOVA gestoppt nach " + Math.Max(0, played) + " ms");
        }

        public async Task Tick()
        {
            try
            {
                long now = milliseconds();
                if (stopForCost) { stopForCost = false; EndForCost(); return; }
                await TakeEvents(now);
                if (capture != null) DrainInput(now);
                Advance(now);
            }
            catch (Exception error) { Fail(error); }
        }

        private async Task TakeEvents(long now)
        {
            LiveEvent item;
            while (debater != null && debater.TryTake(out item))
            {
                switch (item.Kind)
                {
                    case LiveKind.Line: OnLine(item.Speaker, item.Text, now); break;
                    case LiveKind.SpeechStarted:
                        if (!serverTurns) break;
                        guestSpeaking = true;
                        OnGuestStarted(now);
                        break;
                    case LiveKind.SpeechStopped:
                        if (!serverTurns) break;
                        guestSpeaking = false;
                        turnEndAt = now;
                        break;
                    case LiveKind.Committed:
                        if (!serverTurns || now < skipCommitUntil) break;
                        if (turnEndAt == 0) turnEndAt = now;
                        OnTurnComplete(now);
                        break;
                    case LiveKind.AnswerDone: OnAnswerDone(item); break;
                    case LiveKind.Memory:
                        Memory = item.Text;
                        debater.Remember(Memory);
                        Metrics.Memory++;
                        Log("Debattenprotokoll aktualisiert (" + Memory.Length + " Zeichen)");
                        break;
                    case LiveKind.Warning: Log("OpenAI-Hinweis: " + item.Text); break;
                    case LiveKind.Usage: CountCost(item.Tokens); break;
                    case LiveKind.Closed: await Reconnect(now, "Verbindung beendet"); return;
                    case LiveKind.Fatal: Fail(item.Error); return;
                }
            }
        }

        private void OnLine(string speaker, string text, long now)
        {
            Line(speaker, text);
            string entry = speaker + ": " + text;
            newLines.Add(entry);
            recentLines.Add(entry);
            if (recentLines.Count > 6) recentLines.RemoveAt(0);
            if (newLines.Count > 60) newLines.RemoveAt(0);
            if (speaker == "Gast")
            {
                // Safety net: the "guest" repeats what NOVA just said, so NOVA hears herself.
                if (lastNovaLine != null && Similarity(text, lastNovaLine) >= 0.6)
                {
                    Metrics.EchoLoops++;
                    options.BargeIn = BargeIn.Ignore;
                    options.RoutingVerified = false;
                    if (answerActive && State != Phase.Speaking) { debater.Cancel(-1); dropped = true; sink = null; }
                    pendingRespond = false;
                    Log("Selbstgespräch erkannt, Halbduplex aktiviert");
                    EnterCooldown(now, "NOVA hat ihre eigene Stimme gehört. Zum Schutz ist Halbduplex aktiv. Bitte Routing prüfen (Echoprüfung).");
                }
            }
            else if (speaker != "KI-Hinweis" && speaker != "System") lastNovaLine = text;
        }

        internal static double Similarity(string heard, string said)
        {
            var words = new HashSet<string>();
            foreach (string word in Words(said)) words.Add(word);
            int total = 0, shared = 0;
            foreach (string word in Words(heard)) { total++; if (words.Contains(word)) shared++; }
            return total < 4 ? 0 : (double)shared / total;
        }

        private static IEnumerable<string> Words(string text)
        {
            foreach (string part in (text ?? "").ToLowerInvariant().Split(' ', ',', '.', '!', '?', ';', ':', '-', '"', '„', '“'))
                if (part.Length >= 3) yield return part;
        }

        private void OnGuestStarted(long now)
        {
            lastActivity = now;
            proactiveUsed = false;
            if (State == Phase.Listening) Set(Phase.Recording, "Gesprächspartner spricht. NOVA hört zu.");
            else if ((State == Phase.Thinking || State == Phase.WaitingForQuiet) && options.Fairness && requeues < MaximumRequeues && answerActive)
                Requeue();
        }

        // The partner kept talking while NOVA prepared an answer that nobody heard yet:
        // drop that answer (also from the server context) and keep listening.
        private void Requeue()
        {
            requeues++;
            Metrics.Requeues++;
            debater.Cancel(-1);
            dropped = true;
            sink = null;
            Log("Gast spricht weiter, ungehörte Antwort verworfen");
            Set(Phase.Recording, "Du sprichst weiter. NOVA verwirft ihre ungehörte Antwort und hört weiter zu.");
        }

        private void OnTurnComplete(long now)
        {
            Metrics.Turns++;
            lastActivity = now;
            proactiveUsed = false;
            if (muted)
            {
                if (State == Phase.Recording) Set(Phase.Listening, "NOVA ist stummgeschaltet und hört nur zu.");
                return;
            }
            if (answerActive || State == Phase.Speaking || State == Phase.WaitingForQuiet)
            {
                pendingRespond = true;
                Log("Beitrag vorgemerkt");
                if (State == Phase.Recording) Set(Phase.Listening, "Beitrag vorgemerkt. NOVA antwortet gleich.");
                return;
            }
            RequestAnswer(null, now);
        }

        private void RequestAnswer(string extra, long now)
        {
            pendingRespond = false;
            sink = new AudioSink();
            answerActive = true;
            answerDone = dropped = false;
            requestAt = now;
            firstAudioAt = 0;
            debater.Respond(sink, extra);
            Set(Phase.Thinking, "NOVA denkt nach …");
        }

        private void OnAnswerDone(LiveEvent item)
        {
            answerActive = false;
            answerDone = true;
            if (item.Usage != null) Log(item.Usage);
            CountCost(item.Tokens);
            if (dropped) { dropped = false; return; }
            if (item.Completed) { failures = 0; answersSinceMemory++; return; }
            if (sink != null && sink.TotalBytes > 0) return;
            Metrics.Failures++;
            failures++;
            Log("Antwort ohne Audio beendet: " + item.Text);
            if (failures >= 3) Fail(new AppError("OpenAI liefert wiederholt keine Antworten. Verbindung und API-Projekt prüfen und neu starten."));
        }

        private void CountCost(TokenUsage tokens)
        {
            Metrics.AddUsage(tokens);
            if (options.CostLimit > 0 && Metrics.Cost >= options.CostLimit && !CostLimitReached)
            {
                CostLimitReached = true;
                muted = true;
                Log("Kostenlimit erreicht: " + Metrics.CostText());
                Note = "Kostenlimit von " + options.CostLimit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                    " $ erreicht. NOVA ist stummgeschaltet und hört nur noch zu.";
                // Ending the session is deferred to the next tick, outside the event loop that is still using the debater.
                if (options.StopAtCostLimit) stopForCost = true;
                Changed();
            }
        }

        private bool stopForCost;

        private void EndForCost()
        {
            string cleanupError = Release();
            Log("Kostenlimit: Sitzung beendet bei " + Metrics.CostText());
            Set(Phase.Idle, cleanupError ?? "Kostenlimit von " + options.CostLimit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                " $ erreicht. Die OpenAI-Sitzung wurde vollständig beendet, es entstehen keine weiteren Kosten. Bisher ca. " + Metrics.CostText() + ".");
        }

        private void DrainInput(long now)
        {
            bool endTurn = false, wasRecording = detector.Recording;
            capture.Drain(frame =>
            {
                Level = TurnDetector.Level(frame);
                if (State == Phase.Meter) return;
                if (State == Phase.EchoTest)
                {
                    echoTestFrames++;
                    if (echo.IsEcho(frame, now)) echoTestHits++;
                    if (Level >= options.Threshold) echoTestLoud++;
                    return;
                }
                bool isEcho = options.EchoGuard && echo.Active(now) && echo.IsEcho(frame, now);
                if (isEcho) Metrics.EchoFrames++;
                if (isEcho || !Forward(now)) { bargeMs = 0; return; }
                bool voiced = Level >= options.Threshold;
                quietMs = voiced ? 0 : Math.Min(detector.EndSilenceMs, quietMs + frame.Length * 1000 / WinAudio.BytesPerSecond);
                if (State == Phase.Speaking)
                {
                    bargeMs = voiced ? bargeMs + WinAudio.FrameMs : Math.Max(0, bargeMs - WinAudio.FrameMs);
                    if (bargeMs >= options.BargeInMs) OnBargeIn(now);
                }
                if (serverTurns) debater.Append(frame);
                else if (options.Automatic || detector.Recording)
                {
                    if (!endTurn && detector.Add(frame, Level, options.Automatic)) endTurn = true;
                    byte[] fresh = detector.TakeNew();
                    if (fresh != null) debater.Append(fresh);
                }
            });
            if (serverTurns || State == Phase.Meter || State == Phase.EchoTest) return;
            if (!wasRecording && detector.Recording && options.Automatic)
            {
                if (State == Phase.Listening || State == Phase.Cooldown) { lastActivity = now; proactiveUsed = false; Set(Phase.Recording, "Gesprächspartner spricht. NOVA hört zu."); }
            }
            if ((State == Phase.Thinking || State == Phase.WaitingForQuiet) && options.Fairness && answerActive &&
                requeues < MaximumRequeues && detector.VoiceMs >= RequeueVoiceMs)
                Requeue();
            if (endTurn) FinishLocalTurn(now);
        }

        // Whether the partner's audio reaches turn detection and OpenAI right now.
        private bool Forward(long now)
        {
            switch (State)
            {
                case Phase.Listening: case Phase.Recording: case Phase.Thinking: case Phase.WaitingForQuiet:
                    return true;
                case Phase.Speaking:
                    if (options.BargeIn == BargeIn.Ignore) return false;
                    // Without a passed echo check, listen during NOVA's speech only once the echo
                    // detector has a full window of her voice and can tell her apart from the partner.
                    return options.RoutingVerified || !options.EchoGuard || echo.Warm(now);
                default:
                    return false;
            }
        }

        private void OnBargeIn(long now)
        {
            bargeMs = 0;
            Metrics.BargeIns++;
            lastActivity = now;
            if (options.BargeIn == BargeIn.Stop)
            {
                StopNova(now);
                Set(Phase.Recording, "Gesprächspartner unterbricht. NOVA hört zu.");
            }
            else Note = "Gesprächspartner möchte sprechen. NOVA beendet ihren Satz und antwortet danach.";
            Changed();
        }

        private void FinishLocalTurn(long now)
        {
            byte[] rest = detector.TakeNew();
            if (rest != null) debater.Append(rest);
            bool limit = detector.ReachedLimit;
            byte[] pcm = detector.Finish();
            if (pcm == null)
            {
                debater.Clear();
                if (State == Phase.Recording) Set(Phase.Listening, "Zu leise oder zu kurz. Pegel prüfen und erneut sprechen.");
                return;
            }
            if (limit) Log("Redezeit erreicht, Beitrag wird gesendet");
            Log("Beitrag: " + (pcm.Length * 1000 / WinAudio.BytesPerSecond) + " ms");
            debater.Commit();
            turnEndAt = now;
            OnTurnComplete(now);
        }

        private void Advance(long now)
        {
            if (State == Phase.Thinking || State == Phase.WaitingForQuiet)
            {
                if (sink != null && firstAudioAt == 0 && sink.TotalBytes > 0)
                {
                    firstAudioAt = now;
                    Metrics.FirstAudio.Add((int)(now - requestAt));
                }
                long buffered = sink == null ? 0 : sink.TotalBytes * 1000 / WinAudio.BytesPerSecond;
                bool ready = sink != null && (buffered >= StartBufferMs || (answerDone && buffered > 0));
                bool partnerTalking = serverTurns ? guestSpeaking : quietMs < detector.EndSilenceMs;
                if (ready && State == Phase.Thinking)
                {
                    if (options.Automatic && options.Fairness && partnerTalking)
                    {
                        holdUntil = now + MaximumHoldMs;
                        Set(Phase.WaitingForQuiet, "NOVA ist bereit und wartet, bis du ausgeredet hast.");
                    }
                    else StartSpeaking(now);
                }
                else if (ready && State == Phase.WaitingForQuiet && (!partnerTalking || now >= holdUntil))
                    StartSpeaking(now);
                else if (State == Phase.Thinking && !answerActive && (sink == null || sink.TotalBytes == 0))
                {
                    if (!pendingRespond) Set(Phase.Listening, failures > 0 ? "NOVA konnte nicht antworten. Bitte erneut sprechen." : "Bereit. NOVA hört zu.");
                }
                else if (State == Phase.Thinking && answerActive && firstAudioAt == 0 && now - requestAt > AnswerTimeoutMs)
                {
                    debater.Cancel(-1);
                    dropped = true;
                    sink = null;
                    Metrics.Failures++;
                    failures++;
                    Set(Phase.Listening, "NOVA hat nicht rechtzeitig geantwortet. Bitte erneut sprechen.");
                }
            }
            // OpenAI turns: if no speech was confirmed (e.g. a noise caused an interruption), return to listening.
            if (State == Phase.Recording && serverTurns && !guestSpeaking && quietMs >= detector.EndSilenceMs && now - lastActivity >= 2 * detector.EndSilenceMs)
                Set(Phase.Listening, ListeningNote());
            if (State == Phase.Speaking)
            {
                Pump(now);
                if ((answerDone || sink == null) && (sink == null || sink.Empty) && playback.Finished) AfterSpeaking(now);
            }
            if (pendingRespond && !answerActive && !muted &&
                (State == Phase.Listening || State == Phase.Cooldown || (State == Phase.Thinking && sink == null)))
                RequestAnswer(null, now);
            if (State == Phase.Tone && playback.Finished) Stop(false);
            if (State == Phase.Meter && playback != null && playback.Finished)
            {
                playback.Dispose();
                playback = null;
                Set(Phase.Meter, "Nur Pegel. Der Testton ist beendet.");
            }
            if (State == Phase.EchoTest && now >= echoTestEnd) FinishEchoTest();
            if (State == Phase.Cooldown && now >= listenAfter)
            {
                detector.Reset();
                Set(Phase.Listening, ListeningNote());
            }
            if (State == Phase.Listening && debater != null && !answerActive)
            {
                if (options.ProactiveSeconds > 0 && !proactiveUsed && !muted && Metrics.Answers > 0 &&
                    now - lastActivity >= options.ProactiveSeconds * 1000L)
                {
                    proactiveUsed = true;
                    Metrics.Proactive++;
                    Log("Proaktive Nachfrage");
                    RequestAnswer(ProactiveText, now);
                }
                else if (options.Memory && answersSinceMemory >= MemoryEveryAnswers && newLines.Count != 0)
                {
                    debater.Summarize(string.Join("\n", newLines));
                    newLines.Clear();
                    answersSinceMemory = 0;
                }
                else if (now - sessionStarted >= SessionRenewMs && !reconnecting)
                {
                    renewal = Reconnect(now, "Sitzungsdauer");
                }
            }
        }

        private string ListeningNote()
        {
            if (muted) return "NOVA ist stummgeschaltet und hört nur zu.";
            if (!options.Automatic) return "Bereit. Für einen Beitrag auf „Aufnahme starten“ klicken.";
            return serverTurns ? "Bereit. NOVA hört zu und erkennt selbst, wann du fertig bist." : "Bereit. NOVA hört zu und antwortet nach einer Sprechpause.";
        }

        // NOVA's own output level, frame by frame, for the speaking indicator (overlay, orb).
        private const int EnvelopeMs = 40;
        private readonly List<float> envelope = new List<float>();
        private int envelopeStartMs;
        private double envelopeSum;
        private int envelopeSamples;

        public double NovaLevel
        {
            get
            {
                if (State != Phase.Speaking || playback == null) return -90;
                int index = (playback.PlayedMs - envelopeStartMs) / EnvelopeMs;
                return index >= 0 && index < envelope.Count ? envelope[index] : -90;
            }
        }

        private void AddEnvelope(byte[] pcm)
        {
            int frame = WinAudio.Rate * EnvelopeMs / 1000;
            for (int i = 0; i + 1 < pcm.Length; i += 2)
            {
                double sample = (short)(pcm[i] | pcm[i + 1] << 8) / 32768.0;
                envelopeSum += sample * sample;
                if (++envelopeSamples < frame) continue;
                double rms = Math.Sqrt(envelopeSum / envelopeSamples);
                envelope.Add((float)(rms > 1e-5 ? 20 * Math.Log10(rms) : -90));
                envelopeSum = 0; envelopeSamples = 0;
            }
        }

        private void StartSpeaking(long now)
        {
            playback.Stop();
            envelope.Clear(); envelopeSum = 0; envelopeSamples = 0;
            envelopeStartMs = playback.PlayedMs;
            Pump(now);
            Metrics.Answers++;
            if (turnEndAt > 0) { Metrics.Latency.Add((int)(now - turnEndAt)); Log("Latenz " + (now - turnEndAt) + " ms"); }
            turnEndAt = 0;
            requeues = 0;
            bargeMs = 0;
            Set(Phase.Speaking, "NOVA spricht.");
        }

        private void Pump(long now)
        {
            if (sink == null) return;
            byte[] chunk;
            while (sink.TryRead(out chunk))
            {
                playback.Append(chunk);
                echo.AddReference(chunk, now);
                AddEnvelope(chunk);
            }
        }

        private void AfterSpeaking(long now)
        {
            sink = null;
            lastActivity = now;
            if (pendingRespond && !answerActive && !muted) { RequestAnswer(null, now); return; }
            bool partnerTalking = serverTurns ? guestSpeaking : detector.Recording;
            if (partnerTalking) { Set(Phase.Recording, "Gesprächspartner spricht. NOVA hört zu."); return; }
            // Half duplex, or echo could not yet be ruled out: short input lock against NOVA's echo tail.
            if (options.BargeIn == BargeIn.Ignore || !(options.RoutingVerified || !options.EchoGuard || echo.Warm(now)))
                EnterCooldown(now, null);
            else Set(Phase.Listening, ListeningNote());
        }

        private void FinishEchoTest()
        {
            string message;
            if (echoTestHits >= 10)
            {
                EchoCheckPassed = false;
                message = "Echo erkannt: NOVAs Stimme kommt am Eingang wieder an. Routing korrigieren (siehe Routing-Hilfe). Bis dahin „Ignorieren (Halbduplex)“ verwenden.";
            }
            else if (echoTestLoud > echoTestFrames / 4)
            {
                EchoCheckPassed = null;
                message = "Echoprüfung nicht eindeutig: Am Eingang war während des Tests Ton. Bitte wiederholen, wenn niemand spricht.";
            }
            else
            {
                EchoCheckPassed = true;
                message = "Kein Echo: NOVAs Ausgabe kommt nicht am Eingang an. Vollduplex und Unterbrechungen sind sicher nutzbar.";
            }
            Log("Echoprüfung: Treffer " + echoTestHits + ", laut " + echoTestLoud + " von " + echoTestFrames + " Frames → " +
                (EchoCheckPassed == true ? "bestanden" : EchoCheckPassed == false ? "Echo" : "unklar"));
            string cleanup = Release();
            Set(Phase.Idle, cleanup == null ? message : message + " " + cleanup);
        }

        private async Task Reconnect(long now, string reason)
        {
            if (reconnecting || factory == null) return;
            reconnects++;
            Metrics.Reconnects++;
            if (reconnects > 5) { Fail(new AppError("Die Verbindung zu OpenAI bricht wiederholt ab. Internet prüfen und neu starten.")); return; }
            reconnecting = true;
            Log("Verbindung wird erneuert: " + reason);
            if (State == Phase.Speaking || answerActive) { playback.Stop(); echo.StopReference(now); }
            Close(debater);
            debater = null;
            sink = null;
            answerActive = pendingRespond = false;
            detector.Reset();
            var token = lifetime.Token;
            Set(Phase.Connecting, "Verbindung zu OpenAI wird erneuert. Das Debattenprotokoll bleibt erhalten …");
            try
            {
                IDebater fresh = factory();
                debater = fresh;
                await fresh.Start(false, token);
                if (token.IsCancellationRequested) return;
                sessionStarted = lastActivity = milliseconds();
                reconnecting = false;
                Set(Phase.Listening, ListeningNote());
            }
            catch (Exception error) { reconnecting = false; if (!token.IsCancellationRequested) Fail(error); }
        }

        private void EnterCooldown(long now, string note)
        {
            listenAfter = now + options.GuardMs;
            Set(Phase.Cooldown, note ?? "Kurze Nachlaufzeit (Echo-Schutz).");
        }

        public void Stop(bool pause)
        {
            string cleanupError = Release();
            Set(pause && cleanupError == null ? Phase.Paused : Phase.Idle,
                cleanupError ?? (pause ? "Pausiert. Fortsetzen öffnet eine neue Sitzung." : "Gestoppt. Ein- und Ausgang geschlossen."));
        }

        public void Moderate()
        {
            if (!CanModerate) return;
            string cleanupError = Release();
            Set(cleanupError == null ? Phase.Moderated : Phase.Idle,
                cleanupError ?? "Ruhe in der Debatte. Jetzt hat der Gast das Wort. Erst nach „Fortsetzen“ antwortet Nova wieder.");
        }

        private void Fail(Exception error)
        {
            string message;
            if (error is AppError) message = error.Message;
            else if (error is OperationCanceledException) message = "OpenAI antwortet nicht rechtzeitig. Verbindung prüfen und neu starten.";
            else message = "Audio oder Verbindung unterbrochen. Geräte und Internet prüfen und neu starten.";
            Log("Fehler: " + error.GetType().Name);
            string cleanupError = Release();
            Set(Phase.Idle, cleanupError == null ? message : message + " " + cleanupError);
        }

        private string Release()
        {
            bool clean = Close(playback);
            playback = null;
            if (lifetime != null)
            {
                try { lifetime.Cancel(); }
                catch (AggregateException) { clean = false; }
                lifetime.Dispose();
                lifetime = null;
            }
            clean = Close(debater) & clean;
            clean = Close(capture) & clean;
            debater = null;
            capture = null;
            ResetConversation();
            detector.Reset();
            return clean ? null : "Ein Gerät wurde nicht sauber freigegeben. Ausgang im Mixer stummschalten und App neu öffnen.";
        }

        private bool Close(IDisposable resource)
        {
            if (resource == null) return true;
            try { resource.Dispose(); return true; }
            catch (Exception error) { Log("Freigabe: " + error.GetType().Name); return false; }
        }

        private void Set(Phase phase, string note)
        {
            if (phase != State) Log("Status: " + phase);
            State = phase;
            Note = note;
            if (phase != Phase.Meter && phase != Phase.Listening && phase != Phase.Recording && phase != Phase.Speaking) Level = -90;
            Changed();
        }

        public void Dispose() { Release(); detector.Dispose(); }
    }
}
