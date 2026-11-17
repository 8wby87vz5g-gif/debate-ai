using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DebateAI
{
    internal sealed class ChatMessage
    {
        public string Role, Content;
        public ChatMessage(string role, string content) { Role = role; Content = content; }
    }

    internal interface ISpeechToText { Task<string> Transcribe(byte[] pcm24k, CancellationToken token); }
    internal interface IChatModel
    {
        // Streams the answer; onDelta receives text pieces as they are generated.
        // Returns false when the answer was cut off by the token limit (unfinished last sentence).
        Task<bool> Chat(IList<ChatMessage> messages, int maxTokens, Action<string> onDelta, CancellationToken token);
        // Loads the model and pre-processes the fixed instructions so the first answer starts sooner.
        Task Warm(string system, CancellationToken token);
    }
    internal interface ITextToSpeech { Task<byte[]> Speak(string text, CancellationToken token); }

    internal static class Pcm
    {
        // Linear resampling of 16-bit mono PCM.
        public static byte[] Resample(byte[] pcm, int fromRate, int toRate)
        {
            if (fromRate == toRate || pcm.Length < 4) return pcm;
            int inSamples = pcm.Length / 2;
            int outSamples = (int)((long)inSamples * toRate / fromRate);
            var output = new byte[outSamples * 2];
            for (int i = 0; i < outSamples; i++)
            {
                double position = (double)i * fromRate / toRate;
                int left = (int)position;
                int right = Math.Min(inSamples - 1, left + 1);
                double fraction = position - left;
                double value = WinAudio.ReadSample(pcm, left * 2) * (1 - fraction) + WinAudio.ReadSample(pcm, right * 2) * fraction;
                WinAudio.WriteSample(output, i * 2, (short)Math.Round(value));
            }
            return output;
        }

        public static byte[] Wav(byte[] pcm, int rate)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length); writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
                writer.Flush();
                return stream.ToArray();
            }
        }

        // Reads 16-bit PCM WAV (mono or stereo) and returns mono samples and the sample rate.
        public static byte[] ReadWav(byte[] wav, out int rate)
        {
            rate = 0;
            if (wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF") throw new AppError("Ungültige WAV-Datei der Sprachausgabe.");
            int channels = 1, offset = 12;
            while (offset + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, offset, 4);
                int size = BitConverter.ToInt32(wav, offset + 4);
                if (id == "fmt ") { channels = BitConverter.ToInt16(wav, offset + 10); rate = BitConverter.ToInt32(wav, offset + 12); }
                if (id == "data")
                {
                    size = Math.Min(size, wav.Length - offset - 8);
                    var data = new byte[size - size % 2];
                    Array.Copy(wav, offset + 8, data, 0, data.Length);
                    if (channels <= 1) return data;
                    var mono = new byte[data.Length / channels / 2 * 2];
                    for (int i = 0, j = 0; j < mono.Length; i += 2 * channels, j += 2) WinAudio.WriteSample(mono, j, WinAudio.ReadSample(data, i));
                    return mono;
                }
                offset += 8 + size + (size % 2);
            }
            throw new AppError("WAV-Datei ohne Audiodaten.");
        }
    }

    // Splits streamed text into speakable sentences, so speech starts after the first sentence.
    internal sealed class SentenceSplitter
    {
        private readonly StringBuilder buffer = new StringBuilder();
        public bool Emitted { get; private set; }

        public List<string> Add(string delta)
        {
            buffer.Append(delta);
            var sentences = new List<string>();
            while (true)
            {
                string text = buffer.ToString();
                int cut = -1;
                for (int i = 0; i < text.Length - 1; i++)
                {
                    char c = text[i];
                    if ((c == '.' || c == '!' || c == '?' || c == ':' || c == ';') && char.IsWhiteSpace(text[i + 1]) && i >= 12) { cut = i + 1; break; }
                }
                if (cut < 0 && text.Length > 180)
                {
                    int comma = text.LastIndexOf(", ", 180, StringComparison.Ordinal);
                    cut = comma > 40 ? comma + 1 : 180;
                }
                if (cut < 0) break;
                string sentence = text.Substring(0, cut).Trim();
                buffer.Remove(0, cut);
                if (sentence.Length != 0) { sentences.Add(sentence); Emitted = true; }
            }
            return sentences;
        }

        public string Rest()
        {
            string rest = buffer.ToString().Trim();
            buffer.Clear();
            return rest;
        }
    }

    internal static class LocalGuard
    {
        // The local mode may only ever talk to this PC. Paid cloud APIs are unreachable by design.
        public static Uri Loopback(string url)
        {
            var uri = new Uri(url);
            if (!uri.IsLoopback) throw new AppError("Sicherheitssperre: Der lokale Modus darf nur Dienste auf diesem PC verwenden (" + uri.Host + ").");
            return uri;
        }
    }

    // NOVA without cloud: whisper.cpp (speech recognition), a local LLM via Ollama, Piper (speech).
    // Turn detection is the local level/pause detection of DebateSession.
    internal sealed class LocalDebater : IDebater
    {
        public const int HistoryMessages = 16;
        private readonly ISpeechToText stt;
        private readonly IChatModel llm;
        private readonly ITextToSpeech tts;
        private readonly Persona persona;
        private readonly ConcurrentQueue<LiveEvent> events = new ConcurrentQueue<LiveEvent>();
        private readonly List<ChatMessage> history = new List<ChatMessage>();
        private readonly List<byte[]> pendingTurns = new List<byte[]>();
        private readonly object gate = new object();
        private MemoryStream turn = new MemoryStream();
        private CancellationTokenSource answerCancel;
        private ChatMessage answerMessage;
        private List<KeyValuePair<string, int>> spokenSentences;
        private string memory;
        private readonly CancellationTokenSource life = new CancellationTokenSource();

        public LocalDebater(ISpeechToText stt, IChatModel llm, ITextToSpeech tts, Persona persona, string memory)
        {
            this.stt = stt; this.llm = llm; this.tts = tts;
            this.persona = persona ?? new Persona();
            this.memory = memory ?? "";
        }

        public bool ServerTurns { get { return false; } }
        public bool TryTake(out LiveEvent item) { return events.TryDequeue(out item); }

        public async Task<byte[]> Start(bool disclosure, CancellationToken token)
        {
            Warming = WarmUp(token);
            if (!disclosure) return null;
            byte[] notice = await tts.Speak(persona.Disclosure(), token).ConfigureAwait(false);
            events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "KI-Hinweis", Text = persona.Disclosure() });
            return notice;
        }

        // Finished when the model is loaded and the instructions are pre-processed.
        public Task Warming { get; private set; }

        private Task WarmUp(CancellationToken token)
        {
            string system;
            lock (gate) system = persona.LocalInstructions(memory);
            return Task.Run(async () =>
            {
                try { await llm.Warm(system, token).ConfigureAwait(false); }
                catch (Exception error) { events.Enqueue(new LiveEvent(LiveKind.Warning) { Text = "Vorwärmen des lokalen Modells fehlgeschlagen: " + error.GetBaseException().Message }); }
            });
        }

        // The window moves in steps of 12 messages, so the conversation prefix stays the same
        // between answers and Ollama can reuse its cache instead of re-reading everything.
        internal static int WindowStart(int count)
        {
            return count <= HistoryMessages ? 0 : ((count - 8) / 12) * 12;
        }

        public void Append(byte[] pcm) { lock (gate) turn.Write(pcm, 0, pcm.Length); }

        public void Commit()
        {
            lock (gate)
            {
                if (turn.Length > 0) pendingTurns.Add(turn.ToArray());
                turn = new MemoryStream();
            }
        }

        public void Clear() { lock (gate) turn = new MemoryStream(); }

        public void Respond(AudioSink sink, string extra)
        {
            var cancel = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
            List<byte[]> turns;
            lock (gate)
            {
                turns = new List<byte[]>(pendingTurns);
                pendingTurns.Clear();
                answerCancel = cancel;
                answerMessage = null;
                spokenSentences = new List<KeyValuePair<string, int>>();
            }
            Task.Run(() => Answer(turns, sink, extra, cancel));
        }

        private async Task Answer(List<byte[]> turns, AudioSink sink, string extra, CancellationTokenSource cancel)
        {
            CancellationToken token = cancel.Token;
            var spoken = new StringBuilder();
            int transcribed = 0;
            try
            {
                foreach (byte[] audio in turns)
                {
                    string heard = (await stt.Transcribe(audio, token).ConfigureAwait(false) ?? "").Trim();
                    if (heard.Length != 0) events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "Gast", Text = heard });
                    lock (gate) history.Add(new ChatMessage("user", heard.Length == 0 ? "(unverständlich oder nur Geräusche)" : heard));
                    transcribed++;
                }
                events.Enqueue(new LiveEvent(LiveKind.AnswerStarted));
                var messages = new List<ChatMessage>();
                lock (gate)
                {
                    // Fixed system text first; a one-off instruction goes to the end so the cached prefix stays valid.
                    messages.Add(new ChatMessage("system", persona.LocalInstructions(memory)));
                    for (int i = WindowStart(history.Count); i < history.Count; i++) messages.Add(history[i]);
                    if (messages.Count == 1) messages.Add(new ChatMessage("user", "(Das Gespräch beginnt. Eröffne die Debatte.)"));
                    if (!string.IsNullOrEmpty(extra))
                    {
                        ChatMessage last = messages[messages.Count - 1];
                        string hint = "(Hinweis an dich: " + extra + ")";
                        if (last.Role == "user") messages[messages.Count - 1] = new ChatMessage("user", last.Content + "\n" + hint);
                        else messages.Add(new ChatMessage("user", hint));
                    }
                    answerMessage = new ChatMessage("assistant", "");
                    history.Add(answerMessage);
                }
                var splitter = new SentenceSplitter();
                var speaking = new BlockingCollection<string>();
                Task voice = Task.Run(async () =>
                {
                    foreach (string sentence in speaking.GetConsumingEnumerable(token))
                    {
                        byte[] pcm = await tts.Speak(sentence, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;
                        sink.Write(pcm);
                        lock (gate)
                        {
                            spokenSentences.Add(new KeyValuePair<string, int>(sentence, pcm.Length * 1000 / WinAudio.BytesPerSecond));
                            spoken.Append(spoken.Length == 0 ? "" : " ").Append(sentence);
                            answerMessage.Content = spoken.ToString();
                        }
                    }
                }, token);
                try
                {
                    bool finished = await llm.Chat(messages, persona.LocalMaxTokens, delta =>
                    {
                        foreach (string sentence in splitter.Add(delta)) speaking.Add(sentence);
                    }, token).ConfigureAwait(false);
                    string rest = splitter.Rest();
                    // A sentence cut off by the length limit is not spoken (unless it is the only one).
                    if (rest.Length != 0 && (finished || !splitter.Emitted)) speaking.Add(rest);
                }
                finally { speaking.CompleteAdding(); }
                await voice.ConfigureAwait(false);
                if (spoken.Length != 0) events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = persona.Name, Text = spoken.ToString() });
                events.Enqueue(new LiveEvent(LiveKind.AnswerDone) { Completed = sink.TotalBytes > 0, Text = sink.TotalBytes > 0 ? "completed" : "empty" });
            }
            catch (OperationCanceledException) { Requeue(turns, transcribed); events.Enqueue(new LiveEvent(LiveKind.AnswerDone) { Completed = false, Text = "cancelled" }); }
            catch (Exception error)
            {
                if (token.IsCancellationRequested) { Requeue(turns, transcribed); events.Enqueue(new LiveEvent(LiveKind.AnswerDone) { Completed = false, Text = "cancelled" }); return; }
                events.Enqueue(new LiveEvent(LiveKind.Warning) { Text = error is AppError ? error.Message : error.GetType().Name + ": " + error.Message, Error = error });
                events.Enqueue(new LiveEvent(LiveKind.AnswerDone) { Completed = false, Text = "failed" });
            }
        }

        // Turns that were not transcribed before a cancel are answered with the next reply.
        private void Requeue(List<byte[]> turns, int transcribed)
        {
            if (transcribed >= turns.Count) return;
            lock (gate) pendingTurns.InsertRange(0, turns.GetRange(transcribed, turns.Count - transcribed));
        }

        // playedMs < 0: the answer was never heard → forget it. Otherwise keep only fully played sentences.
        public void Cancel(int playedMs)
        {
            lock (gate)
            {
                if (answerCancel != null) answerCancel.Cancel();
                if (answerMessage == null) return;
                if (playedMs < 0) { history.Remove(answerMessage); answerMessage = null; return; }
                var kept = new StringBuilder();
                int at = 0;
                foreach (KeyValuePair<string, int> sentence in spokenSentences)
                {
                    at += sentence.Value;
                    if (at > playedMs) break;
                    kept.Append(kept.Length == 0 ? "" : " ").Append(sentence.Key);
                }
                if (kept.Length == 0) history.Remove(answerMessage);
                else answerMessage.Content = kept + " (unterbrochen)";
                answerMessage = null;
            }
        }

        public void Summarize(string material)
        {
            if (string.IsNullOrEmpty(material)) return;
            Task.Run(async () =>
            {
                try
                {
                    var text = new StringBuilder();
                    await llm.Chat(new List<ChatMessage>
                    {
                        new ChatMessage("system", "Aktualisiere das Debattenprotokoll in höchstens 120 Wörtern als Stichpunkte: Thema, Behauptungen des Gastes, Gegenargumente der KI, Zugeständnisse, offene Fragen. Nur Gesagtes."),
                        new ChatMessage("user", "Bisher: " + (memory.Length == 0 ? "(leer)" : memory) + "\nNeu:\n" + material)
                    }, 300, delta => text.Append(delta), life.Token).ConfigureAwait(false);
                    string result = text.ToString().Trim();
                    if (result.Length != 0) events.Enqueue(new LiveEvent(LiveKind.Memory) { Text = result.Length > 2000 ? result.Substring(0, 2000) : result });
                }
                catch (Exception) { }
            });
        }

        public void Remember(string text) { lock (gate) memory = text ?? ""; }

        public List<ChatMessage> History { get { lock (gate) return new List<ChatMessage>(history); } }

        public void Dispose() { life.Cancel(); }
    }

    // ---------------------------------------------------------------------------------------
    // Real local backends.

    internal sealed class OllamaChat : IChatModel
    {
        public const string Url = "http://127.0.0.1:11434";
        private readonly string model;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };

        public OllamaChat(string model) { this.model = model; }

        public static bool Thinks(string model) { return model.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase) || model.StartsWith("deepseek-r1", StringComparison.OrdinalIgnoreCase); }

        public object Request(IList<ChatMessage> messages, int maxTokens, bool stream)
        {
            var list = new List<object>();
            foreach (ChatMessage message in messages) list.Add(new { role = message.Role, content = message.Content });
            var body = new Dictionary<string, object>();
            body["model"] = model; body["messages"] = list; body["stream"] = stream; body["keep_alive"] = "30m";
            body["options"] = new { temperature = 0.7, num_predict = Math.Max(1, maxTokens), num_ctx = 4096 };
            if (Thinks(model)) body["think"] = false;
            return body;
        }

        public async Task<bool> Chat(IList<ChatMessage> messages, int maxTokens, Action<string> onDelta, CancellationToken token)
        {
            bool finished = true;
            var request = (HttpWebRequest)WebRequest.Create(LocalGuard.Loopback(Url + "/api/chat"));
            request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = 120000;
            byte[] body = Encoding.UTF8.GetBytes(json.Serialize(Request(messages, maxTokens, true)));
            using (token.Register(request.Abort))
            {
                using (Stream stream = await request.GetRequestStreamAsync().ConfigureAwait(false)) await stream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
                try
                {
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            token.ThrowIfCancellationRequested();
                            if (line.Length == 0) continue;
                            var item = json.Deserialize<Dictionary<string, object>>(line);
                            object error;
                            if (item.TryGetValue("error", out error)) throw new AppError("Ollama: " + error);
                            object message;
                            if (item.TryGetValue("message", out message))
                            {
                                var content = message as Dictionary<string, object>;
                                object text;
                                if (content != null && content.TryGetValue("content", out text) && text is string && ((string)text).Length != 0) onDelta((string)text);
                            }
                            object done;
                            if (item.TryGetValue("done", out done) && done is bool && (bool)done)
                            {
                                LastStats = Stats(item);
                                object reason;
                                finished = !(item.TryGetValue("done_reason", out reason) && reason as string == "length");
                                break;
                            }
                        }
                    }
                }
                catch (WebException error)
                {
                    token.ThrowIfCancellationRequested();
                    throw new AppError(Describe(error, model));
                }
            }
            return finished;
        }

        // Timing reported by Ollama for the last answer (prompt reading vs. writing).
        public string LastStats { get; private set; }

        internal static string Stats(Dictionary<string, object> done)
        {
            Func<string, double> get = key => { object v; return done.TryGetValue(key, out v) && v != null ? Convert.ToDouble(v) : 0; };
            double promptN = get("prompt_eval_count"), promptMs = get("prompt_eval_duration") / 1e6, outN = get("eval_count"), outMs = get("eval_duration") / 1e6, loadMs = get("load_duration") / 1e6;
            return "Prompt " + promptN + " Tokens in " + (int)promptMs + " ms" + (promptMs > 0 ? " (" + (promptN * 1000 / promptMs).ToString("0") + "/s)" : "") +
                " · Antwort " + outN + " Tokens in " + (int)outMs + " ms" + (outMs > 0 ? " (" + (outN * 1000 / outMs).ToString("0.0") + "/s)" : "") +
                " · Laden " + (int)loadMs + " ms";
        }

        public async Task Warm(string system, CancellationToken token)
        {
            await Chat(new List<ChatMessage> { new ChatMessage("system", system) }, 1, delta => { }, token).ConfigureAwait(false);
        }

        internal static string Describe(WebException error, string model)
        {
            var response = error.Response as HttpWebResponse;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                return "Das lokale Modell „" + model + "“ ist nicht geladen. Unter „Lokale KI einrichten“ herunterladen.";
            return "Ollama ist nicht erreichbar. Ollama installieren bzw. starten (Symbol im Infobereich).";
        }

        public static List<string> InstalledModels()
        {
            var names = new List<string>();
            var request = (HttpWebRequest)WebRequest.Create(LocalGuard.Loopback(Url + "/api/tags"));
            request.Timeout = 3000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                object models;
                if (data.TryGetValue("models", out models) && models is System.Collections.ArrayList)
                    foreach (object item in (System.Collections.ArrayList)models)
                    {
                        var entry = item as Dictionary<string, object>;
                        object name;
                        if (entry != null && entry.TryGetValue("name", out name) && name is string) names.Add((string)name);
                    }
            }
            return names;
        }

        public static bool HasModel(List<string> installed, string model)
        {
            foreach (string name in installed)
                if (string.Equals(name, model, StringComparison.OrdinalIgnoreCase) || string.Equals(name, model + ":latest", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Downloads a model through the local Ollama service (Ollama fetches it from its official registry).
        public static async Task Pull(string model, Action<string> progress, CancellationToken token)
        {
            var request = (HttpWebRequest)WebRequest.Create(LocalGuard.Loopback(Url + "/api/pull"));
            request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = Timeout.Infinite; request.ReadWriteTimeout = 600000;
            byte[] body = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new { model = model, stream = true }));
            using (token.Register(request.Abort))
            {
                using (Stream stream = await request.GetRequestStreamAsync().ConfigureAwait(false)) await stream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string line;
                    var json = new JavaScriptSerializer();
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (line.Length == 0) continue;
                        var item = json.Deserialize<Dictionary<string, object>>(line);
                        object error, status, total, completed;
                        if (item.TryGetValue("error", out error)) throw new AppError("Ollama: " + error);
                        item.TryGetValue("status", out status);
                        string text = status as string ?? "";
                        if (item.TryGetValue("total", out total) && item.TryGetValue("completed", out completed))
                        {
                            double all = Convert.ToDouble(total), done = Convert.ToDouble(completed);
                            if (all > 0) text += " " + (int)(done * 100 / all) + " %";
                        }
                        progress(text);
                    }
                }
            }
        }
    }

    internal sealed class WhisperServer : ISpeechToText, IDisposable
    {
        public const int Port = 18178;
        private Process process;

        public static string Url { get { return "http://127.0.0.1:" + Port; } }

        public void Ensure(string exe, string model, int threads)
        {
            if (process != null && !process.HasExited) return;
            if (!File.Exists(exe)) throw new AppError("whisper-server.exe fehlt. Unter „Lokale KI einrichten“ herunterladen.");
            if (!File.Exists(model)) throw new AppError("Das Spracherkennungsmodell fehlt. Unter „Lokale KI einrichten“ herunterladen.");
            process = Process.Start(new ProcessStartInfo
            {
                FileName = exe, WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true,
                Arguments = "-m \"" + model + "\" -l de -t " + threads + " --host 127.0.0.1 --port " + Port
            });
            process.ErrorDataReceived += delegate { };
            process.OutputDataReceived += delegate { };
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 30000)
            {
                if (process.HasExited) throw new AppError("whisper-server wurde beendet (Code " + process.ExitCode + "). Modell oder Programmdateien prüfen.");
                try
                {
                    var probe = (HttpWebRequest)WebRequest.Create(LocalGuard.Loopback(Url + "/"));
                    probe.Timeout = 1000;
                    using (probe.GetResponse()) return;
                }
                catch (WebException error) { if (error.Response != null) { error.Response.Close(); return; } }
                Thread.Sleep(300);
            }
            throw new AppError("whisper-server startet nicht rechtzeitig.");
        }

        public async Task<string> Transcribe(byte[] pcm24k, CancellationToken token)
        {
            byte[] wav = Pcm.Wav(Pcm.Resample(pcm24k, WinAudio.Rate, 16000), 16000);
            string boundary = "----debateai" + Guid.NewGuid().ToString("N");
            var body = new MemoryStream();
            Action<string> text = s => { byte[] b = Encoding.UTF8.GetBytes(s); body.Write(b, 0, b.Length); };
            text("--" + boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"turn.wav\"\r\nContent-Type: audio/wav\r\n\r\n");
            body.Write(wav, 0, wav.Length);
            text("\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"response_format\"\r\n\r\njson");
            text("\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"temperature\"\r\n\r\n0.0");
            text("\r\n--" + boundary + "--\r\n");
            var request = (HttpWebRequest)WebRequest.Create(LocalGuard.Loopback(Url + "/inference"));
            request.Method = "POST"; request.ContentType = "multipart/form-data; boundary=" + boundary; request.Timeout = 60000;
            byte[] data = body.ToArray();
            using (token.Register(request.Abort))
            {
                try
                {
                    using (Stream stream = await request.GetRequestStreamAsync().ConfigureAwait(false)) await stream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(await reader.ReadToEndAsync().ConfigureAwait(false));
                        object value;
                        return result != null && result.TryGetValue("text", out value) ? Clean(value as string) : "";
                    }
                }
                catch (WebException) { token.ThrowIfCancellationRequested(); throw new AppError("Die lokale Spracherkennung (whisper-server) antwortet nicht."); }
            }
        }

        // whisper marks non-speech like "[Musik]" or "(Applaus)"; such turns count as unintelligible.
        internal static string Clean(string text)
        {
            text = (text ?? "").Replace("\n", " ").Trim();
            string bare = text.Trim('[', ']', '(', ')', '*', ' ', '.');
            if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal) && bare.IndexOf(' ') < 0) return "";
            if (text.StartsWith("(", StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal) && bare.IndexOf(' ') < 0) return "";
            return text;
        }

        public void Dispose()
        {
            if (process == null) return;
            try { if (!process.HasExited) process.Kill(); } catch (Exception) { }
            process.Dispose();
            process = null;
        }
    }

    // Piper keeps the voice loaded: each input line becomes one WAV file whose path it prints.
    internal sealed class PiperVoice : ITextToSpeech, IDisposable
    {
        private readonly SemaphoreSlim busy = new SemaphoreSlim(1, 1);
        private readonly string exe, voice, folder;
        private Process process;

        public PiperVoice(string exe, string voice)
        {
            this.exe = exe; this.voice = voice;
            folder = Path.Combine(Path.GetTempPath(), "DebateAI-piper-" + Process.GetCurrentProcess().Id);
        }

        private void Ensure()
        {
            if (process != null && !process.HasExited) return;
            if (!File.Exists(exe)) throw new AppError("piper.exe fehlt. Unter „Lokale KI einrichten“ herunterladen.");
            if (!File.Exists(voice) || !File.Exists(voice + ".json")) throw new AppError("Die lokale NOVA-Stimme fehlt. Unter „Lokale KI einrichten“ herunterladen.");
            Directory.CreateDirectory(folder);
            process = Process.Start(new ProcessStartInfo
            {
                FileName = exe, WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                Arguments = "--model \"" + voice + "\" --output_dir \"" + folder + "\" --sentence_silence 0.15"
            });
            process.ErrorDataReceived += delegate { };
            process.BeginErrorReadLine();
        }

        public async Task<byte[]> Speak(string text, CancellationToken token)
        {
            string line = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (line.Length == 0) return new byte[0];
            await busy.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Ensure();
                byte[] input = Encoding.UTF8.GetBytes(line + "\n");
                await process.StandardInput.BaseStream.WriteAsync(input, 0, input.Length, token).ConfigureAwait(false);
                await process.StandardInput.BaseStream.FlushAsync(token).ConfigureAwait(false);
                Task<string> read = process.StandardOutput.ReadLineAsync();
                if (await Task.WhenAny(read, Task.Delay(20000, token)).ConfigureAwait(false) != read)
                    throw new AppError("Die lokale Sprachausgabe (Piper) antwortet nicht.");
                string path = (read.Result ?? "").Trim();
                if (path.Length == 0 || !File.Exists(path)) throw new AppError("Piper hat keine Audiodatei erzeugt.");
                byte[] wav = await ReadWhenFree(path, token).ConfigureAwait(false);
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                int rate;
                byte[] pcm = Pcm.ReadWav(wav, out rate);
                return Pcm.Resample(pcm, rate, WinAudio.Rate);
            }
            finally { busy.Release(); }
        }

        // Windows may still hold the new file (Piper closing it, virus scanner): retry briefly.
        internal static async Task<byte[]> ReadWhenFree(string path, CancellationToken token)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        var data = new byte[file.Length];
                        int at = 0, read;
                        while (at < data.Length && (read = file.Read(data, at, data.Length - at)) > 0) at += read;
                        if (at >= 44 && at == data.Length) return data;
                    }
                }
                catch (IOException) { if (attempt >= 40) throw; }
                catch (UnauthorizedAccessException) { if (attempt >= 40) throw; }
                if (attempt >= 40) throw new AppError("Piper-Audiodatei unvollständig: " + path);
                await Task.Delay(25, token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(); } catch (Exception) { }
                process.Dispose();
                process = null;
            }
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (Exception) { }
        }
    }
}
