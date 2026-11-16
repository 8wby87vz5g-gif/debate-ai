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

    // All state and device access belongs to the WinForms thread. Network work is awaited.
    internal sealed class DebateSession : IDisposable
    {
        private readonly TurnDetector detector = new TurnDetector();
        private readonly Func<long> milliseconds;
        private ICapture capture;
        private IPlayback playback;
        private IDebater debater;
        private CancellationTokenSource lifetime;
        private long listenAfter;
        private int cooldownMs;
        private bool automatic;
        private bool fairness;
        private int quietMs;

        public event Action Changed = delegate { };
        public event Action<string> Log = delegate { };
        public Phase State { get; private set; }
        public string Note { get; private set; }
        public double Level { get; private set; }
        public bool ReadyForStart { get { return State == Phase.Idle || State == Phase.Paused || State == Phase.Moderated; } }
        public bool CanModerate { get { return !ReadyForStart && State != Phase.Meter && State != Phase.Tone; } }
        public bool CanPush { get { return !automatic && (State == Phase.Listening || State == Phase.Recording); } }

        public DebateSession(Func<long> milliseconds)
        {
            this.milliseconds = milliseconds;
            Level = -90;
        }

        public async Task Start(Func<ICapture> openInput, Func<IPlayback> openOutput, Func<IDebater> create,
            bool automaticMode, double threshold, int guardMs, bool fairnessMode = true, int turnSeconds = 12)
        {
            if (!ReadyForStart) return;
            detector.Reset();
            detector.Threshold = threshold;
            detector.MaximumTurnMs = Math.Max(5, Math.Min(60, turnSeconds)) * 1000;
            detector.EndSilenceMs = fairnessMode ? 1200 : 760;
            fairness = fairnessMode;
            automatic = automaticMode;
            cooldownMs = guardMs;
            lifetime = new CancellationTokenSource();
            CancellationToken token = lifetime.Token;
            Set(Phase.Connecting, "Audiogeräte und Sitzung werden geöffnet …");
            try
            {
                playback = openOutput();
                capture = openInput();
                debater = create();
                byte[] intro = await debater.Start(token);
                if (token.IsCancellationRequested) return;
                if (debater.LastUsage != null) Log(debater.LastUsage);
                if (intro != null) Speak(intro, "Nova spricht den KI-Hinweis.");
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

        public async Task Tick()
        {
            try
            {
                bool endTurn = false;
                if (capture != null)
                {
                    if (State == Phase.Meter || State == Phase.Listening || State == Phase.Recording)
                    {
                        capture.Drain(frame =>
                        {
                            Level = TurnDetector.Level(frame);
                            if (State != Phase.Meter && !endTurn) endTurn = detector.Add(frame, Level, automatic);
                        });
                        if (State == Phase.Listening && detector.Recording)
                            Set(Phase.Recording, "Gast spricht. Die KI wartet auf eine Sprechpause.");
                    }
                    else if (State == Phase.WaitingForQuiet || (State == Phase.Thinking && fairness && automatic))
                        capture.Drain(ObserveQuiet);
                    else capture.Drain(null);
                }

                if (endTurn)
                {
                    if (fairness && detector.ReachedLimit)
                    {
                        detector.Reset();
                        quietMs = 0;
                        Set(Phase.WaitingForQuiet, "Redezeit erreicht. Der Beitrag wurde verworfen. Nova wartet, bis du fertig bist.");
                    }
                    else await FinishTurn();
                    return;
                }
                if (State == Phase.WaitingForQuiet && quietMs >= detector.EndSilenceMs)
                    Set(Phase.Listening, automatic ? "Bereit für einen neuen, kürzeren Beitrag." : "Bereit. Aufnahme für einen neuen Beitrag starten.");
                if (State == Phase.Speaking && playback.Finished) EnterCooldown();
                if (State == Phase.Tone && playback.Finished) Stop(false);
                if (State == Phase.Meter && playback != null && playback.Finished)
                {
                    playback.Dispose();
                    playback = null;
                    Set(Phase.Meter, "Nur Pegel. Der Testton ist beendet.");
                }
                if (State == Phase.Cooldown && milliseconds() >= listenAfter)
                    Set(Phase.Listening, automatic ? "Bereit. Die KI wartet auf den Gast." : "Bereit. Für einen Sprachtest auf „Aufnahme starten“ klicken.");
            }
            catch (Exception error) { Fail(error); }
        }

        private void ObserveQuiet(byte[] frame)
        {
            Level = TurnDetector.Level(frame);
            quietMs = Level >= detector.Threshold ? 0 :
                Math.Min(detector.EndSilenceMs, quietMs + frame.Length * 1000 / WinAudio.BytesPerSecond);
        }

        private async Task FinishTurn()
        {
            byte[] pcm = detector.Finish();
            if (pcm == null)
            {
                Set(Phase.Listening, "Zu leise oder zu kurz. Pegel prüfen und erneut sprechen.");
                return;
            }
            var token = lifetime.Token;
            var elapsed = Stopwatch.StartNew();
            quietMs = detector.EndSilenceMs;
            Log("Beitrag: " + (pcm.Length * 1000 / WinAudio.BytesPerSecond) + " ms");
            Set(Phase.Thinking, "Nova verarbeitet den Beitrag. Eingang gesperrt.");
            try
            {
                byte[] reply = await debater.Reply(pcm, token);
                if (token.IsCancellationRequested) return;
                if (debater.LastUsage != null) Log(debater.LastUsage);
                Log("Antwort bereit nach " + elapsed.ElapsedMilliseconds + " ms");
                if (fairness && automatic)
                {
                    capture.Drain(ObserveQuiet);
                    if (quietMs < detector.EndSilenceMs)
                    {
                        Set(Phase.WaitingForQuiet, "Du sprichst noch. Nova lässt dich ausreden und verwirft ihre wartende Antwort.");
                        return;
                    }
                }
                if (reply.Length > (long)detector.MaximumTurnMs * WinAudio.BytesPerSecond / 1000)
                {
                    Set(Phase.Listening, "Novas Antwort überschritt die Redezeit und wurde übersprungen.");
                    return;
                }
                Speak(reply, "Antwort läuft. Eingang gesperrt.");
            }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error); }
        }

        private void Speak(byte[] pcm, string note)
        {
            playback.Play(pcm);
            Set(Phase.Speaking, note);
        }

        private void EnterCooldown()
        {
            listenAfter = milliseconds() + cooldownMs;
            Set(Phase.Cooldown, "Kurze Nachlaufzeit. Eingang gesperrt.");
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
