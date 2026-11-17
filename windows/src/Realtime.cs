using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DebateAI
{
    internal sealed class AppError : Exception
    {
        public AppError(string message) : base(message) { }
    }

    internal sealed class TranscriptLine
    {
        public readonly string Speaker, Text;
        public TranscriptLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    // Thread-safe hand-over of streamed answer audio from the network task to the UI thread.
    internal sealed class AudioSink
    {
        private readonly ConcurrentQueue<byte[]> chunks = new ConcurrentQueue<byte[]>();
        private long total;

        public void Write(byte[] pcm)
        {
            if (pcm == null || pcm.Length == 0) return;
            chunks.Enqueue(pcm);
            Interlocked.Add(ref total, pcm.Length);
        }

        public bool TryRead(out byte[] pcm) { return chunks.TryDequeue(out pcm); }
        public long TotalBytes { get { return Interlocked.Read(ref total); } }
        public bool Empty { get { return chunks.IsEmpty; } }

        public byte[] ReadAll()
        {
            using (var all = new MemoryStream())
            {
                byte[] pcm;
                while (TryRead(out pcm)) all.Write(pcm, 0, pcm.Length);
                return all.ToArray();
            }
        }
    }

    internal interface IDebater : IDisposable
    {
        string LastUsage { get; }
        // Opens the session and returns the spoken AI disclosure (or null offline).
        Task<byte[]> Start(CancellationToken token);
        // Sends one finished guest turn and streams NOVA's answer into the sink.
        Task Reply(byte[] pcm, AudioSink sink, CancellationToken token);
        // playedMs < 0: forget the answer completely (it was never heard).
        // playedMs >= 0: NOVA was interrupted; keep only what was audible.
        void Cancel(int playedMs);
        bool TryTakeLine(out TranscriptLine line);
    }

    internal interface IRealtimeWire : IDisposable
    {
        Task Open(CancellationToken token);
        Task Send(string message, CancellationToken token);
        Task<string> Receive(CancellationToken token);
    }

    internal sealed class WebSocketWire : IRealtimeWire
    {
        private readonly ClientWebSocket socket = new ClientWebSocket();
        private readonly byte[] readBuffer = new byte[8192];

        public WebSocketWire(string key)
        {
            socket.Options.SetRequestHeader("Authorization", "Bearer " + key);
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        }

        public async Task Open(CancellationToken token)
        {
            try
            {
                await socket.ConnectAsync(new Uri("wss://api.openai.com/v1/realtime?model=" + RealtimeDebater.Model), token).ConfigureAwait(false);
            }
            catch (WebSocketException error)
            {
                token.ThrowIfCancellationRequested();
                var web = error.GetBaseException() as WebException;
                var response = web == null ? null : web.Response as HttpWebResponse;
                if (response != null) throw RealtimeDebater.HttpError((int)response.StatusCode);
                throw new AppError("OpenAI-Verbindung fehlgeschlagen. Internet, API-Schlüssel und Realtime-Freigabe im API-Projekt prüfen.");
            }
        }

        public Task Send(string message, CancellationToken token)
        {
            return socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, token);
        }

        public async Task<string> Receive(CancellationToken token)
        {
            using (var message = new MemoryStream())
            {
                WebSocketReceiveResult chunk;
                do
                {
                    chunk = await socket.ReceiveAsync(new ArraySegment<byte>(readBuffer), token).ConfigureAwait(false);
                    if (chunk.MessageType == WebSocketMessageType.Close)
                        throw new AppError("OpenAI hat die Sitzung beendet. Mit Start eine neue Sitzung öffnen.");
                    if (chunk.MessageType != WebSocketMessageType.Text || message.Length + chunk.Count > 2 * 1024 * 1024)
                        throw new AppError("Unerwartete Realtime-Nachricht. Sitzung bitte neu starten.");
                    message.Write(readBuffer, 0, chunk.Count);
                } while (!chunk.EndOfMessage);
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }
        }

        public void Dispose() { socket.Dispose(); }
    }

    internal sealed class RealtimeDebater : IDebater
    {
        public const string Model = "gpt-realtime-2.1-mini";
        public const string TranscriptionModel = "gpt-4o-mini-transcribe";
        public const int MaximumAnswerSeconds = 60;
        public static string Disclosure { get { return new Persona().Disclosure(); } }

        private readonly IRealtimeWire wire;
        private readonly Persona persona;
        private readonly bool transcribe;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 };
        private readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<TranscriptLine> lines = new ConcurrentQueue<TranscriptLine>();
        private readonly object gate = new object();
        private bool active, cutRequested, tolerateErrors;
        private int cutMs;
        private string itemId;
        public string LastUsage { get; private set; }

        public RealtimeDebater(IRealtimeWire wire) : this(wire, new Persona(), false) { }

        public RealtimeDebater(IRealtimeWire wire, Persona persona, bool transcribe)
        {
            this.wire = wire;
            this.persona = persona ?? new Persona();
            this.transcribe = transcribe;
        }

        public bool TryTakeLine(out TranscriptLine line) { return lines.TryDequeue(out line); }

        public async Task<byte[]> Start(CancellationToken token)
        {
            using (var timeout = Deadline(token, 20))
            {
                await wire.Open(timeout.Token).ConfigureAwait(false);
                await WaitFor("session.created", timeout.Token).ConfigureAwait(false);
                var format = new { type = "audio/pcm", rate = WinAudio.Rate };
                object transcription = transcribe ? (object)new { model = TranscriptionModel, language = persona.LanguageCode } : null;
                await Send(new
                {
                    type = "session.update",
                    session = new
                    {
                        type = "realtime", instructions = persona.Instructions(), output_modalities = new[] { "audio" },
                        max_output_tokens = persona.MaxOutputTokens,
                        audio = new
                        {
                            input = new { format = format, turn_detection = (object)null, transcription = transcription },
                            output = new { format = format, voice = persona.Voice }
                        }
                    }
                }, timeout.Token).ConfigureAwait(false);
                await WaitFor("session.updated", timeout.Token).ConfigureAwait(false);
            }
            // The model speaks the fixed disclosure. A paraphrase is accepted when it still
            // names NOVA and says that it is an AI; otherwise one retry, then a clear error.
            for (int attempt = 0; ; attempt++)
            {
                using (var timeout = Deadline(token, 35))
                {
                    await Send(new
                    {
                        type = "response.create",
                        response = new
                        {
                            conversation = "none", output_modalities = new[] { "audio" },
                            instructions = "Lies ausschließlich den Satz im Eingabetext wörtlich vor. Füge nichts hinzu.",
                            input = new[] { new { type = "message", role = "user", content = new[] { new { type = "input_text", text = persona.Disclosure() } } } }
                        }
                    }, timeout.Token).ConfigureAwait(false);
                    var sink = new AudioSink();
                    string transcript = await ReceiveAnswer(sink, true, timeout.Token).ConfigureAwait(false);
                    if (persona.IsDisclosure(transcript)) return sink.ReadAll();
                    if (attempt >= 1)
                        throw new AppError("Der gesprochene KI-Hinweis war unvollständig. Mit Start erneut versuchen.");
                }
            }
        }

        public async Task Reply(byte[] pcm, AudioSink sink, CancellationToken token)
        {
            LastUsage = null;
            lock (gate) { cutRequested = false; itemId = null; }
            using (var timeout = Deadline(token, 45))
            {
                // An empty turn only asks for an answer to the turns already in the conversation.
                if (pcm.Length != 0)
                {
                    for (int offset = 0; offset < pcm.Length; offset += 12000)
                    {
                        string audio = Convert.ToBase64String(pcm, offset, Math.Min(12000, pcm.Length - offset));
                        await Send(new { type = "input_audio_buffer.append", audio = audio }, timeout.Token).ConfigureAwait(false);
                    }
                    await Send(new { type = "input_audio_buffer.commit" }, timeout.Token).ConfigureAwait(false);
                    await WaitFor("input_audio_buffer.committed", timeout.Token).ConfigureAwait(false);
                }
                lock (gate) active = true;
                await Send(new { type = "response.create" }, timeout.Token).ConfigureAwait(false);
                await ReceiveAnswer(sink, false, timeout.Token).ConfigureAwait(false);
            }
        }

        public void Cancel(int playedMs)
        {
            bool sendCancel;
            lock (gate)
            {
                if (!active && itemId == null) return;
                cutRequested = true;
                cutMs = playedMs;
                tolerateErrors = true;
                sendCancel = active;
            }
            Task work = sendCancel ? Send(new { type = "response.cancel" }, CancellationToken.None) : ApplyCut();
            work.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        // Removes or shortens NOVA's last answer in the server-side conversation,
        // so the model never assumes the guest heard words that were not played.
        private Task ApplyCut()
        {
            string id; int ms;
            lock (gate)
            {
                id = itemId; ms = cutMs;
                if (!cutRequested || id == null) return Task.FromResult(0);
                itemId = null;
            }
            if (ms <= 0) return Send(new { type = "conversation.item.delete", item_id = id }, CancellationToken.None);
            return Send(new { type = "conversation.item.truncate", item_id = id, content_index = 0, audio_end_ms = ms }, CancellationToken.None);
        }

        private async Task Send(object message, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string text = json.Serialize(message);
            await sending.WaitAsync(token).ConfigureAwait(false);
            try { await wire.Send(text, token).ConfigureAwait(false); }
            finally { sending.Release(); }
        }

        private async Task WaitFor(string expected, CancellationToken token)
        {
            while (Field(await Next(token).ConfigureAwait(false), "type") != expected) { }
        }

        private async Task<string> ReceiveAnswer(AudioSink sink, bool disclosure, CancellationToken token)
        {
            string transcript = "";
            long limit = (long)MaximumAnswerSeconds * WinAudio.BytesPerSecond;
            while (true)
            {
                var message = await Next(token).ConfigureAwait(false);
                switch (Field(message, "type"))
                {
                    case "response.output_item.added":
                        string id = Field(Child(message, "item"), "id");
                        if (!disclosure && id.Length != 0) lock (gate) itemId = id;
                        break;
                    case "response.output_audio.delta":
                        byte[] data = Convert.FromBase64String(Field(message, "delta"));
                        if (sink.TotalBytes + data.Length > limit)
                        {
                            if (!disclosure) Cancel((int)(limit * 1000 / WinAudio.BytesPerSecond));
                            break;
                        }
                        sink.Write(data);
                        break;
                    case "response.output_audio_transcript.done":
                        transcript = Field(message, "transcript");
                        if (transcript.Length != 0) lines.Enqueue(new TranscriptLine(disclosure ? "KI-Hinweis" : persona.Name, transcript));
                        break;
                    case "response.done":
                        var response = Child(message, "response");
                        LastUsage = FormatUsage(Child(response, "usage"));
                        bool cut;
                        lock (gate) { active = false; cut = cutRequested; }
                        if (cut) { await ApplyCut().ConfigureAwait(false); return transcript; }
                        lock (gate) tolerateErrors = false;
                        if (Field(response, "status") != "completed")
                            throw new AppError("OpenAI hat die Antwort abgebrochen. Mit Start erneut versuchen.");
                        if (sink.TotalBytes == 0 || sink.TotalBytes % 2 != 0)
                            throw new AppError("OpenAI hat kein gültiges Antwort-Audio geliefert.");
                        return transcript;
                }
            }
        }

        internal static string FormatUsage(Dictionary<string, object> usage)
        {
            var input = Child(usage, "input_token_details");
            var output = Child(usage, "output_token_details");
            var cached = Child(input, "cached_tokens_details");
            if (input == null || output == null) return null;
            return "Usage audioIn=" + Count(input, "audio_tokens") + " audioCached=" + Count(cached, "audio_tokens") +
                " audioOut=" + Count(output, "audio_tokens") + " textIn=" + Count(input, "text_tokens") +
                " textCached=" + Count(cached, "text_tokens") + " textOut=" + Count(output, "text_tokens");
        }

        private static int Count(Dictionary<string, object> details, string key)
        {
            object value;
            return details != null && details.TryGetValue(key, out value) && value is int ? Math.Max(0, (int)value) : 0;
        }

        private async Task<Dictionary<string, object>> Next(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var message = json.Deserialize<Dictionary<string, object>>(await wire.Receive(token).ConfigureAwait(false));
                string type = Field(message, "type");
                if (type == "conversation.item.input_audio_transcription.completed")
                {
                    string heard = Field(message, "transcript").Trim();
                    if (heard.Length != 0) lines.Enqueue(new TranscriptLine("Gast", heard));
                    continue;
                }
                if (type != "error") return message;
                var error = Child(message, "error");
                string code = Field(error, "code");
                if (code == "invalid_api_key") throw HttpError(401);
                if (code == "model_not_found") throw HttpError(403);
                if (code == "credit_balance_exhausted")
                    throw new AppError("Das OpenAI-API-Guthaben ist aufgebraucht. Prüfe die API-Abrechnung oder verwende einen Schlüssel aus einem API-Projekt mit Guthaben.");
                if (code == "insufficient_quota" || code == "rate_limit_exceeded") throw HttpError(429);
                bool tolerate;
                lock (gate) tolerate = tolerateErrors;
                // A cancel can race with the natural end of an answer. Such errors are harmless.
                if (tolerate) continue;
                throw new AppError("OpenAI hat eine Realtime-Anfrage abgelehnt. API-Zugriff prüfen und Sitzung neu starten.");
            }
        }

        internal static string Normalize(string text)
        {
            var result = new StringBuilder();
            foreach (char c in text ?? "")
                if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
            return result.ToString();
        }

        private static Dictionary<string, object> Child(Dictionary<string, object> message, string field)
        {
            object value;
            return message != null && message.TryGetValue(field, out value) ? value as Dictionary<string, object> : null;
        }

        private static string Field(Dictionary<string, object> message, string field)
        {
            object value;
            return message != null && message.TryGetValue(field, out value) ? value as string ?? "" : "";
        }

        private static CancellationTokenSource Deadline(CancellationToken token, int seconds)
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
            return timeout;
        }

        public static AppError HttpError(int status)
        {
            if (status == 401) return new AppError("API-Schlüssel abgelehnt. Schlüssel im Fenster neu hinterlegen.");
            if (status == 403 || status == 404) return new AppError("Kein Zugriff auf das Realtime-Modell. API-Projekt und Modellfreigabe prüfen.");
            if (status == 429) return new AppError("OpenAI-Limit erreicht. Abrechnung, Guthaben oder Ratenlimit prüfen.");
            return new AppError("OpenAI-Verbindung fehlgeschlagen (HTTP " + status + ").");
        }

        public void Dispose() { wire.Dispose(); sending.Dispose(); }
    }

    internal sealed class OfflineDebater : IDebater
    {
        public string LastUsage { get { return null; } }
        public Task<byte[]> Start(CancellationToken token) { return Task.FromResult<byte[]>(null); }

        public async Task Reply(byte[] pcm, AudioSink sink, CancellationToken token)
        {
            await Task.Delay(250, token).ConfigureAwait(false);
            sink.Write(WinAudio.Tone(1600, 660));
        }

        public void Cancel(int playedMs) { }
        public bool TryTakeLine(out TranscriptLine line) { line = null; return false; }
        public void Dispose() { }
    }
}
