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
            Recording = false;
            ReachedLimit = false;
        }

        public void Dispose() { recording.Dispose(); }
    }

    internal enum Phase { Idle, Paused, Moderated, Meter, Tone, Connecting, Listening, Recording, Thinking, Speaking, Cooldown, WaitingForQuiet }

    internal sealed class SessionOptions
    {
        public bool Automatic = true;
        public double Threshold = -40;
        public int GuardMs = 900;
        // Fairness: NOVA lets the partner finish. A ready answer waits for a pause, and if
        // the partner keeps talking, the unheard answer is dropped and the new words are queued.
        public bool Fairness = true;
        public int TurnSeconds = 30;
        public int PauseMs;   // 0 = default (1200 ms with fairness, 760 ms without)
    }

    // All state and device access belongs to the WinForms thread. Network work is awaited;
    // streamed answer audio arrives through a thread-safe AudioSink and is pumped in Tick.
    internal sealed class DebateSession : IDisposable
    {
        public const int StartBufferMs = 240;
        public const int MaximumHoldMs = 6000;
        public const int MaximumRequeues = 2;
        // Speech needed while NOVA thinks before her unheard answer is dropped (a cough is not enough).
        public const int RequeueVoiceMs = 600;

        private readonly TurnDetector detector = new TurnDetector();
        private readonly Func<long> milliseconds;
        private ICapture capture;
        private IPlayback playback;
        private IDebater debater;
        private CancellationTokenSource lifetime;
        private SessionOptions options = new SessionOptions();
        private AudioSink sink;
        private Task replyTask;
        private bool replyDone, owesAnswer;
        private Exception replyError;
        private int turn, droppedTurn = -1, requeues;
        private long listenAfter, holdUntil;
        private int quietMs;

        public event Action Changed = delegate { };
        public event Action<string> Log = delegate { };
        // Transcript lines (speaker, text). Kept in memory only, never written to disk.
        public event Action<string, string> Line = delegate { };
        public Phase State { get; private set; }
        public string Note { get; private set; }
        public double Level { get; private set; }
        public int Answers { get; private set; }
        public bool ReadyForStart { get { return State == Phase.Idle || State == Phase.Paused || State == Phase.Moderated; } }
        public bool CanModerate { get { return !ReadyForStart && State != Phase.Meter && State != Phase.Tone; } }
        public bool CanPush
        {
            get { return options.Automatic ? State == Phase.Recording : State == Phase.Listening || State == Phase.Recording; }
        }
        public bool CanInterrupt { get { return State == Phase.Speaking || State == Phase.Thinking || State == Phase.WaitingForQuiet; } }

        public DebateSession(Func<long> milliseconds)
        {
            this.milliseconds = milliseconds;
            Level = -90;
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
            lifetime = new CancellationTokenSource();
            CancellationToken token = lifetime.Token;
            replyTask = null; sink = null; replyDone = false; replyError = null; requeues = 0; Answers = 0; owesAnswer = false;
            Set(Phase.Connecting, "Audiogeräte und Sitzung werden geöffnet …");
            try
            {
                playback = openOutput();
                capture = openInput();
                debater = create();
                byte[] intro = await debater.Start(token);
                if (token.IsCancellationRequested) return;
                if (debater.LastUsage != null) Log(debater.LastUsage);
                TakeLines();
                if (intro != null && intro.Length != 0)
                {
                    sink = new AudioSink();
                    replyDone = true;
                    playback.Play(intro);
                    Set(Phase.Speaking, "Nova spricht den KI-Hinweis.");
                }
                else EnterCooldown();
            }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error); }
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

        // Manual mode: start/stop recording. Automatic mode: "Jetzt antworten" sends the running turn.
        public Task Push()
        {
            if (!CanPush) return Task.FromResult(0);
            if (State == Phase.Recording) return FinishTurn();
            try
            {
                capture.Drain(null);
                detector.BeginManual();
                Set(Phase.Recording, "Sprich jetzt. Zum Senden erneut klicken.");
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
                int played = State == Phase.Speaking ? playback.PlayedMs : -1;
                playback.Stop();
                if (debater != null) debater.Cancel(played);
                droppedTurn = turn;
                owesAnswer = false;
                detector.Reset();
                sink = null;
                Log("Unterbrochen nach " + Math.Max(0, played) + " ms");
                EnterCooldown("NOVA wurde unterbrochen. Der Gesprächspartner hat das Wort.");
            }
            catch (Exception error) { Fail(error); }
        }

        public async Task Tick()
        {
            try
            {
                TakeLines();
                bool endTurn = false;
                if (capture != null)
                {
                    bool listenWhileThinking = options.Automatic && options.Fairness &&
                        (State == Phase.Thinking || State == Phase.WaitingForQuiet);
                    if (State == Phase.Meter || State == Phase.Listening || State == Phase.Recording)
                    {
                        capture.Drain(frame =>
                        {
                            Level = TurnDetector.Level(frame);
                            if (State != Phase.Meter && !endTurn) endTurn = detector.Add(frame, Level, options.Automatic);
                        });
                        if (State == Phase.Listening && detector.Recording)
                            Set(Phase.Recording, "Gesprächspartner spricht. NOVA hört zu und wartet auf eine Sprechpause.");
                    }
                    else if (listenWhileThinking)
                    {
                        capture.Drain(frame =>
                        {
                            ObserveQuiet(frame);
                            // Short sounds that end again are ignored; only real speech requeues.
                            if (requeues < MaximumRequeues && detector.Add(frame, Level, true)) detector.Reset();
                        });
                        if (requeues < MaximumRequeues && detector.VoiceMs >= RequeueVoiceMs) Requeue();
                    }
                    else capture.Drain(null);
                }

                if (endTurn && State == Phase.Recording)
                {
                    if (detector.ReachedLimit) Log("Redezeit erreicht, Beitrag wird gesendet");
                    await FinishTurn();
                    return;
                }
                Advance();
            }
            catch (Exception error) { Fail(error); }
        }

        // The partner kept talking while NOVA prepared an answer that nobody heard yet:
        // drop that answer (also from the server context) and keep recording the new words.
        private void Requeue()
        {
            requeues++;
            droppedTurn = turn;
            owesAnswer = true;
            if (debater != null) debater.Cancel(-1);
            sink = null;
            Log("Gast spricht weiter, Antwort verworfen, Beitrag wird fortgesetzt");
            Set(Phase.Recording, "Du sprichst weiter. NOVA verwirft ihre ungehörte Antwort und hört weiter zu.");
        }

        private void Advance()
        {
            if (State == Phase.Thinking || State == Phase.WaitingForQuiet)
            {
                if (replyError != null) { Exception error = replyError; replyError = null; Fail(error); return; }
                long buffered = sink == null ? 0 : sink.TotalBytes * 1000 / WinAudio.BytesPerSecond;
                bool ready = sink != null && (buffered >= StartBufferMs || (replyDone && buffered > 0));
                if (ready && State == Phase.Thinking)
                {
                    if (options.Automatic && options.Fairness && quietMs < detector.EndSilenceMs)
                    {
                        holdUntil = milliseconds() + MaximumHoldMs;
                        Set(Phase.WaitingForQuiet, "NOVA ist bereit und wartet, bis du ausgeredet hast.");
                    }
                    else StartSpeaking();
                }
                else if (State == Phase.WaitingForQuiet && sink != null &&
                    (quietMs >= detector.EndSilenceMs || milliseconds() >= holdUntil))
                    StartSpeaking();
                else if (State == Phase.WaitingForQuiet && sink == null && quietMs >= detector.EndSilenceMs)
                    Set(Phase.Listening, options.Automatic ? "Bereit. NOVA hört zu." : "Bereit. Aufnahme für einen neuen Beitrag starten.");
            }
            if (State == Phase.Speaking)
            {
                if (replyError != null) { Exception error = replyError; replyError = null; Fail(error); return; }
                Pump();
                if (replyDone && (sink == null || sink.Empty) && playback.Finished)
                {
                    requeues = 0;
                    EnterCooldown();
                }
            }
            if (State == Phase.Tone && playback.Finished) Stop(false);
            if (State == Phase.Meter && playback != null && playback.Finished)
            {
                playback.Dispose();
                playback = null;
                Set(Phase.Meter, "Nur Pegel. Der Testton ist beendet.");
            }
            if (State == Phase.Cooldown && milliseconds() >= listenAfter)
            {
                detector.Reset();
                Set(Phase.Listening, options.Automatic ? "Bereit. NOVA hört zu und antwortet nach einer Sprechpause." :
                    "Bereit. Für einen Beitrag auf „Aufnahme starten“ klicken.");
            }
        }

        private void StartSpeaking()
        {
            playback.Stop();
            Pump();
            Answers++;
            Set(Phase.Speaking, "NOVA spricht. Eingang gesperrt (Echo-Schutz).");
        }

        private void Pump()
        {
            if (sink == null) return;
            byte[] chunk;
            while (sink.TryRead(out chunk)) playback.Append(chunk);
        }

        private void ObserveQuiet(byte[] frame)
        {
            Level = TurnDetector.Level(frame);
            quietMs = Level >= detector.Threshold ? 0 :
                Math.Min(detector.EndSilenceMs, quietMs + frame.Length * 1000 / WinAudio.BytesPerSecond);
        }

        private void TakeLines()
        {
            if (debater == null) return;
            TranscriptLine line;
            while (debater.TryTakeLine(out line)) Line(line.Speaker, line.Text);
        }

        private async Task FinishTurn()
        {
            byte[] pcm = detector.Finish();
            // After a dropped answer NOVA still owes a reply, even if the follow-up was too short.
            if (pcm == null && owesAnswer) pcm = new byte[0];
            if (pcm == null)
            {
                Set(Phase.Listening, "Zu leise oder zu kurz. Pegel prüfen und erneut sprechen.");
                return;
            }
            var token = lifetime.Token;
            int current = ++turn;
            owesAnswer = false;
            quietMs = detector.EndSilenceMs;
            Log("Beitrag: " + (pcm.Length * 1000 / WinAudio.BytesPerSecond) + " ms");
            Set(Phase.Thinking, "NOVA denkt nach …");
            try
            {
                // Only one answer may use the connection at a time. A dropped answer ends quickly.
                Task previous = replyTask;
                if (previous != null && !previous.IsCompleted)
                {
                    await Task.WhenAny(previous, Task.Delay(8000, token));
                    if (token.IsCancellationRequested) return;
                    if (!previous.IsCompleted) throw new AppError("OpenAI beendet die vorige Antwort nicht. Sitzung neu starten.");
                }
                var elapsed = Stopwatch.StartNew();
                sink = new AudioSink();
                AudioSink target = sink;
                replyDone = false;
                replyError = null;
                Task reply = debater.Reply(pcm, target, token);
                replyTask = reply;
                try { await reply; }
                catch (Exception error)
                {
                    if (token.IsCancellationRequested || current != turn || current == droppedTurn) return;
                    replyError = error;
                    Advance();
                    return;
                }
                if (token.IsCancellationRequested || current != turn || current == droppedTurn) return;
                replyDone = true;
                if (debater.LastUsage != null) Log(debater.LastUsage);
                Log("Antwort vollständig nach " + elapsed.ElapsedMilliseconds + " ms");
                Advance();
            }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error); }
        }

        private void EnterCooldown(string note = null)
        {
            listenAfter = milliseconds() + options.GuardMs;
            Set(Phase.Cooldown, note ?? "Kurze Nachlaufzeit. Eingang gesperrt.");
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
            sink = null;
            replyTask = null;
            replyDone = false;
            owesAnswer = false;
            replyError = null;
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
            if (phase != Phase.Meter && phase != Phase.Listening && phase != Phase.Recording) Level = -90;
            Changed();
        }

        public void Dispose() { Release(); detector.Dispose(); }
    }
}
