using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace DebateAI
{
    internal sealed class LocalModels
    {
        public string Llm = "llama3.2:3b";
        public string Whisper = "base";
        public string Voice = "de_DE-thorsten-medium";
        public int Threads = 4;
    }

    internal sealed class VoiceChoice
    {
        public string Id, Label, Path;
        public VoiceChoice(string id, string label, string path) { Id = id; Label = label; Path = path; }
        public override string ToString() { return Label; }
    }

    // Official download sources. Nothing is downloaded without the user's click.
    internal static class LocalSources
    {
        public const string WhisperReleases = "https://api.github.com/repos/ggml-org/whisper.cpp/releases?per_page=15";
        public const string PiperRelease = "https://api.github.com/repos/rhasspy/piper/releases/tags/2023.11.14-2";
        public const string WhisperModelBase = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-";
        public const string VoiceBase = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/de/de_DE/";
        public const string OllamaDownload = "https://ollama.com/download/windows";

        public static readonly VoiceChoice[] Voices =
        {
            new VoiceChoice("de_DE-thorsten-medium", "Thorsten · männlich · mittlere Qualität", "thorsten/medium/"),
            new VoiceChoice("de_DE-kerstin-low", "Kerstin · weiblich · einfache Qualität", "kerstin/low/"),
            new VoiceChoice("de_DE-ramona-low", "Ramona · weiblich · einfache Qualität", "ramona/low/"),
            new VoiceChoice("de_DE-thorsten-high", "Thorsten · männlich · hohe Qualität, langsamer", "thorsten/high/")
        };

        public static readonly string[] WhisperModels = { "base", "small", "tiny" };

        public static VoiceChoice Voice(string id)
        {
            foreach (VoiceChoice voice in Voices) if (voice.Id == id) return voice;
            return Voices[0];
        }

        // Picks the plain CPU build for Windows x64 from a release asset list.
        public static string PickAsset(IList<string> names, string preferred, string[] required, string[] avoid)
        {
            foreach (string name in names) if (string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase)) return name;
            foreach (string name in names)
            {
                bool ok = true;
                foreach (string part in required) if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) ok = false;
                foreach (string part in avoid) if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) ok = false;
                if (ok) return name;
            }
            return null;
        }
    }

    internal static class LocalPaths
    {
        public static string Root { get { return Path.Combine(LocalData.Folder, "local"); } }
        public static string WhisperFolder { get { return Path.Combine(Root, "whisper"); } }
        public static string PiperFolder { get { return Path.Combine(Root, "piper"); } }
        public static string ModelFolder { get { return Path.Combine(Root, "models"); } }
        public static string WhisperExe { get { return Find(WhisperFolder, "whisper-server.exe"); } }
        public static string PiperExe { get { return Find(PiperFolder, "piper.exe"); } }
        public static string WhisperModel(string size) { return Path.Combine(ModelFolder, "ggml-" + size + ".bin"); }
        public static string VoiceModel(string id) { return Path.Combine(ModelFolder, id + ".onnx"); }

        public static string Find(string folder, string file)
        {
            if (!Directory.Exists(folder)) return Path.Combine(folder, file);
            string[] found = Directory.GetFiles(folder, file, SearchOption.AllDirectories);
            return found.Length != 0 ? found[0] : Path.Combine(folder, file);
        }
    }

    // Shared by "Lokale KI einrichten" and the Windows end-to-end check in CI.
    internal static class LocalInstaller
    {
        public static List<string> Missing(LocalModels models)
        {
            var list = new List<string>();
            if (!File.Exists(LocalPaths.WhisperExe)) list.Add("whisper");
            if (!File.Exists(LocalPaths.WhisperModel(models.Whisper))) list.Add("whisperModel");
            if (!File.Exists(LocalPaths.PiperExe)) list.Add("piper");
            if (!File.Exists(LocalPaths.VoiceModel(models.Voice))) list.Add("voice");
            return list;
        }

        public static async Task Download(LocalModels target, List<string> todo, bool needModel, Action<string> report, CancellationToken token)
        {
            if (todo.Contains("whisper"))
                await Downloader.Zip(Downloader.AssetUrl(LocalSources.WhisperReleases, "whisper-bin-x64.zip", new[] { "bin", "x64" }, new[] { "blas", "cuda", "vulkan", "openvino", "cublas", "arm", "win32" }),
                    LocalPaths.WhisperFolder, report, token).ConfigureAwait(false);
            if (todo.Contains("whisperModel"))
                await Downloader.File(LocalSources.WhisperModelBase + target.Whisper + ".bin", LocalPaths.WhisperModel(target.Whisper), report, token).ConfigureAwait(false);
            if (todo.Contains("piper"))
                await Downloader.Zip(Downloader.AssetUrl(LocalSources.PiperRelease, "piper_windows_amd64.zip", new[] { "windows", "amd64" }, new string[0]),
                    LocalPaths.PiperFolder, report, token).ConfigureAwait(false);
            if (todo.Contains("voice"))
            {
                VoiceChoice voice = LocalSources.Voice(target.Voice);
                await Downloader.File(LocalSources.VoiceBase + voice.Path + voice.Id + ".onnx", LocalPaths.VoiceModel(voice.Id), report, token).ConfigureAwait(false);
                await Downloader.File(LocalSources.VoiceBase + voice.Path + voice.Id + ".onnx.json", LocalPaths.VoiceModel(voice.Id) + ".json", report, token).ConfigureAwait(false);
            }
            if (needModel) await OllamaChat.Pull(target.Llm, text => report("Ollama: " + text), token).ConfigureAwait(false);
        }
    }

    internal sealed class HardwareInfo
    {
        public double RamGb;
        public int Cores;
        public string Cpu = "unbekannt";
        public readonly List<string> Gpus = new List<string>();
        public double NvidiaVramGb;

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExtended;
        }

        [DllImport("kernel32", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        public static HardwareInfo Detect()
        {
            var info = new HardwareInfo { Cores = Environment.ProcessorCount };
            try
            {
                var status = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
                if (GlobalMemoryStatusEx(ref status)) info.RamGb = status.TotalPhys / 1073741824.0;
            }
            catch (Exception) { }
            try
            {
                using (RegistryKey cpu = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    if (cpu != null) info.Cpu = ((cpu.GetValue("ProcessorNameString") as string) ?? info.Cpu).Trim();
            }
            catch (Exception) { }
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    foreach (System.Management.ManagementObject gpu in searcher.Get())
                    {
                        string name = (gpu["Name"] as string ?? "").Trim();
                        if (name.Length == 0) continue;
                        info.Gpus.Add(name);
                        if (name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            object ram = gpu["AdapterRAM"];
                            // AdapterRAM is a 32-bit value and saturates at 4 GB; large cards are estimated from the name.
                            double gb = ram == null ? 0 : Convert.ToDouble(ram) / 1073741824.0;
                            if (gb >= 3.9) gb = Math.Max(gb, VramFromName(name));
                            info.NvidiaVramGb = Math.Max(info.NvidiaVramGb, gb);
                        }
                    }
            }
            catch (Exception) { }
            return info;
        }

        internal static double VramFromName(string name)
        {
            string[] big = { "4090", "3090", "5090" }, twelve = { "4070", "3080", "4080", "5070", "5080", "3060" };
            foreach (string id in big) if (name.Contains(id)) return 24;
            foreach (string id in twelve) if (name.Contains(id)) return 12;
            if (name.Contains("4060") || name.Contains("3070") || name.Contains("2080") || name.Contains("5060")) return 8;
            return 4;
        }

        public string Describe()
        {
            return "Prozessor: " + Cpu + " (" + Cores + " Threads) · Arbeitsspeicher: " + RamGb.ToString("0.0") + " GB · Grafik: " +
                (Gpus.Count == 0 ? "unbekannt" : string.Join(", ", Gpus)) + (NvidiaVramGb > 0 ? " (NVIDIA, ca. " + NvidiaVramGb.ToString("0") + " GB)" : "");
        }
    }

    internal static class ModelAdvisor
    {
        // A first recommendation from the hardware. Only the measurement on the PC decides whether it is fast enough.
        public static LocalModels Recommend(HardwareInfo hw, out string reason)
        {
            var models = new LocalModels { Threads = Math.Max(2, Math.Min(8, hw.Cores - 1)) };
            if (hw.NvidiaVramGb >= 8 && hw.RamGb >= 15)
            {
                models.Llm = "qwen3:8b"; models.Whisper = "small";
                reason = "NVIDIA-Grafikkarte mit mindestens 8 GB: größeres Sprachmodell (Qwen3 8B) auf der Grafikkarte, genauere Spracherkennung.";
            }
            else if (hw.RamGb <= 0)
            {
                models.Llm = "llama3.2:3b"; models.Whisper = "base";
                reason = "Der Arbeitsspeicher konnte nicht ermittelt werden. Standardempfehlung; bitte die Geschwindigkeit messen.";
            }
            else if (hw.RamGb >= 7)
            {
                models.Llm = "llama3.2:3b"; models.Whisper = "base";
                reason = "Ohne starke Grafikkarte läuft alles auf dem Prozessor. Llama 3.2 3B antwortet dort am ausgewogensten zwischen Tempo und Inhalt " +
                    "(Testrechner mit 4 Threads: ca. 5 s Reaktionszeit). Besseres Deutsch, aber langsamer: gemma3:4b (ca. 8 s). Schneller, aber sehr knapp: qwen2.5:3b (ca. 4 s).";
            }
            else
            {
                models.Llm = "qwen2.5:3b"; models.Whisper = "tiny";
                reason = "Wenig Arbeitsspeicher: schnelles kleines Modell (Qwen 2.5 3B) und schnellste Spracherkennung. Deutlich einfachere, knappe Antworten zu erwarten.";
            }
            return models;
        }
    }

    internal static class Downloader
    {
        public static HttpWebRequest Request(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "DEBATE-AI/" + MainForm.Version + " (+https://github.com/8wby87vz5g-gif/debate-ai)";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 120000;
            request.AllowAutoRedirect = true;
            return request;
        }

        // A single release object or a release list (newest first). For a list,
        // drafts and pre-releases are skipped; every release keeps its assets so
        // AssetUrl can fall back to an older release when the newest has no files yet.
        public static List<List<KeyValuePair<string, string>>> Releases(string json)
        {
            var result = new List<List<KeyValuePair<string, string>>>();
            object parsed = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.DeserializeObject(json);
            var list = parsed as object[];
            if (list == null) list = new[] { parsed };
            foreach (object item in list)
            {
                var release = item as Dictionary<string, object>;
                if (release == null) continue;
                object flag;
                if (release.TryGetValue("draft", out flag) && flag is bool && (bool)flag) continue;
                if (release.TryGetValue("prerelease", out flag) && flag is bool && (bool)flag) continue;
                var assets = new List<KeyValuePair<string, string>>();
                object files;
                if (release.TryGetValue("assets", out files) && files is object[])
                    foreach (object entry in (object[])files)
                    {
                        var asset = entry as Dictionary<string, object>;
                        object name, url;
                        if (asset != null && asset.TryGetValue("name", out name) && asset.TryGetValue("browser_download_url", out url))
                            assets.Add(new KeyValuePair<string, string>(name as string, url as string));
                    }
                result.Add(assets);
            }
            return result;
        }

        public static string PickUrl(string json, string preferred, string[] required, string[] avoid)
        {
            foreach (List<KeyValuePair<string, string>> assets in Releases(json))
            {
                var names = new List<string>();
                foreach (KeyValuePair<string, string> asset in assets) names.Add(asset.Key);
                string pick = LocalSources.PickAsset(names, preferred, required, avoid);
                foreach (KeyValuePair<string, string> asset in assets) if (asset.Key == pick) return asset.Value;
            }
            return null;
        }

        public static string AssetUrl(string apiUrl, string preferred, string[] required, string[] avoid)
        {
            string json;
            using (var response = (HttpWebResponse)Request(apiUrl).GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                json = reader.ReadToEnd();
            string url = PickUrl(json, preferred, required, avoid);
            if (url != null) return url;
            throw new AppError("In den offiziellen Releases wurde keine passende Windows-Datei gefunden (" + apiUrl + ").");
        }

        public static async Task File(string url, string target, Action<string> progress, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temp = target + ".download";
            HttpWebRequest request = Request(url);
            using (token.Register(request.Abort))
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (Stream input = response.GetResponseStream())
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                long total = response.ContentLength, done = 0, shown = -1;
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    done += read;
                    long mb = done / (1024 * 1024);
                    if (mb != shown) { shown = mb; progress(Path.GetFileName(target) + ": " + mb + (total > 0 ? " von " + total / (1024 * 1024) : "") + " MB"); }
                }
            }
            if (System.IO.File.Exists(target)) System.IO.File.Delete(target);
            System.IO.File.Move(temp, target);
        }

        public static async Task Zip(string url, string folder, Action<string> progress, CancellationToken token)
        {
            string zip = Path.Combine(LocalPaths.Root, "download-" + Guid.NewGuid().ToString("N") + ".zip");
            await File(url, zip, progress, token).ConfigureAwait(false);
            try
            {
                progress("Entpacke " + Path.GetFileName(url) + " …");
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                ZipFile.ExtractToDirectory(zip, folder);
            }
            finally { if (System.IO.File.Exists(zip)) System.IO.File.Delete(zip); }
        }
    }

    internal sealed class BenchmarkResult
    {
        public int WarmMs, TtsMs, TtsAudioMs, SttMs, LlmFirstSentenceMs, LlmTotalMs, FirstSentenceTtsMs, Words;
        public string Heard = "", Answer = "";
        public int PauseMs = 760;

        // Time from the end of the guest's sentence until NOVA starts speaking, as measured on this PC.
        public int EstimatedResponseMs { get { return PauseMs + SttMs + LlmFirstSentenceMs + FirstSentenceTtsMs; } }

        public string Describe()
        {
            return "Laden und Vorbereiten (einmalig beim Start): " + WarmMs + " ms\n" + "Spracherkennung (5 s Sprache): " + SttMs + " ms · Sprachmodell bis zum ersten Satz: " + LlmFirstSentenceMs + " ms, gesamt " + LlmTotalMs +
                " ms · Sprachausgabe erster Satz: " + FirstSentenceTtsMs + " ms (Echtzeitfaktor " +
                (TtsAudioMs > 0 ? ((double)TtsMs / TtsAudioMs).ToString("0.00") : "?") + ")\n" +
                "Geschätzte Reaktionszeit lokal (inkl. " + PauseMs + " ms Sprechpause): " + EstimatedResponseMs + " ms\n" +
                "Erkannt: „" + Heard + "“\nAntwort: „" + Answer + "“";
        }
    }

    internal static class LocalBenchmark
    {
        public const string Sample = "Ich halte ein Tempolimit auf Autobahnen für sinnvoll, weil es Unfälle verhindert und Sprit spart.";

        public static async Task<BenchmarkResult> Run(ISpeechToText stt, IChatModel llm, ITextToSpeech tts, Persona persona, Action<string> progress, CancellationToken token)
        {
            var result = new BenchmarkResult();
            var clock = Stopwatch.StartNew();
            progress("Sprachmodell wird geladen und vorbereitet …");
            await llm.Warm(persona.LocalInstructions(null), token).ConfigureAwait(false);
            result.WarmMs = (int)clock.ElapsedMilliseconds;
            clock.Restart();
            progress("Sprachausgabe erzeugt Testsatz …");
            byte[] sample = await tts.Speak(Sample, token).ConfigureAwait(false);
            result.TtsMs = (int)clock.ElapsedMilliseconds;
            result.TtsAudioMs = sample.Length * 1000 / WinAudio.BytesPerSecond;
            progress("Spracherkennung …");
            clock.Restart();
            result.Heard = await stt.Transcribe(sample, token).ConfigureAwait(false);
            result.SttMs = (int)clock.ElapsedMilliseconds;
            progress("Sprachmodell denkt …");
            var splitter = new SentenceSplitter();
            var answer = new StringBuilder();
            string first = null;
            clock.Restart();
            await llm.Chat(new List<ChatMessage> { new ChatMessage("system", persona.LocalInstructions(null)), new ChatMessage("user", result.Heard.Length == 0 ? Sample : result.Heard) },
                persona.LocalMaxTokens, delta =>
                {
                    answer.Append(delta);
                    if (first == null)
                    {
                        List<string> done = splitter.Add(delta);
                        if (done.Count != 0) { first = done[0]; result.LlmFirstSentenceMs = (int)clock.ElapsedMilliseconds; }
                    }
                }, token).ConfigureAwait(false);
            result.LlmTotalMs = (int)clock.ElapsedMilliseconds;
            result.Answer = answer.ToString().Trim();
            if (first == null) { first = splitter.Rest(); result.LlmFirstSentenceMs = result.LlmTotalMs; }
            progress("Sprachausgabe erster Satz …");
            clock.Restart();
            await tts.Speak(first.Length == 0 ? "Test." : first, token).ConfigureAwait(false);
            result.FirstSentenceTtsMs = (int)clock.ElapsedMilliseconds;
            result.Words = result.Answer.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return result;
        }
    }

    // Keeps the local speech services running for the lifetime of the app.
    internal sealed class LocalEngine : IDisposable
    {
        private readonly WhisperServer whisper = new WhisperServer();
        private PiperVoice piper;
        private string piperVoice;

        public ISpeechToText Stt(LocalModels models)
        {
            whisper.Ensure(LocalPaths.WhisperExe, LocalPaths.WhisperModel(models.Whisper), models.Threads);
            return whisper;
        }

        public ITextToSpeech Tts(LocalModels models)
        {
            if (piper == null || piperVoice != models.Voice)
            {
                if (piper != null) piper.Dispose();
                piper = new PiperVoice(LocalPaths.PiperExe, LocalPaths.VoiceModel(models.Voice));
                piperVoice = models.Voice;
            }
            return piper;
        }

        public IDebater Create(LocalModels models, Persona persona, string memory)
        {
            List<string> installed;
            try { installed = OllamaChat.InstalledModels(); }
            catch (Exception) { throw new AppError("Ollama ist nicht erreichbar. Ollama installieren bzw. starten und „Lokale KI einrichten“ öffnen."); }
            if (!OllamaChat.HasModel(installed, models.Llm))
                throw new AppError("Das lokale Sprachmodell „" + models.Llm + "“ fehlt. Unter „Lokale KI einrichten“ herunterladen.");
            return new LocalDebater(Stt(models), new OllamaChat(models.Llm), Tts(models), persona, memory);
        }

        public void Dispose()
        {
            whisper.Dispose();
            if (piper != null) piper.Dispose();
        }
    }
}
