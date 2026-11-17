using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DebateAI
{
    // "Lokale KI einrichten": hardware check, model recommendation, downloads from official sources
    // (only after confirmation) and a real speed measurement on this PC.
    internal sealed partial class MainForm
    {
        private readonly LocalEngine localEngine = new LocalEngine();
        private LocalModels localModels = new LocalModels();
        private string lastBenchmark = "";

        private void ShowLocalSetup()
        {
            if (!session.ReadyForStart) { MessageBox.Show(this, "Bitte zuerst die laufende Debatte beenden.", "Lokale KI"); return; }
            using (var dialog = new Form
            {
                Text = "DEBATE AI · Lokale KI einrichten (kostenlos, ohne OpenAI)", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(820, 700),
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Font, BackColor = Theme.Background, ForeColor = Theme.Text
            })
            {
                var body = new FlowLayoutPanel { Location = new Point(16, 16), Size = new Size(788, 620), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
                Button close = Button("Schließen");
                close.Location = new Point(16, 652);
                dialog.Controls.AddRange(new Control[] { body, close });

                HardwareInfo hw = HardwareInfo.Detect();
                string reason;
                LocalModels recommended = ModelAdvisor.Recommend(hw, out reason);
                var hwText = Hint(760, Theme.Text, hw.Describe());
                var recText = Hint(760, Theme.Muted, "Empfehlung vor der Messung: " + recommended.Llm + " · Whisper " + recommended.Whisper + ". " + reason);
                var llmBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 260, BackColor = Theme.Field, ForeColor = Theme.Text };
                llmBox.Items.AddRange(new object[] { "gemma3:4b", "qwen3:4b", "llama3.2:3b", "qwen3:8b", "gemma3:12b", "mistral" });
                llmBox.Text = localModels.Llm;
                ComboBox whisperBox = Combo(), voiceBox = Combo();
                whisperBox.Width = 120; voiceBox.Width = 360;
                whisperBox.Items.AddRange(LocalSources.WhisperModels);
                whisperBox.SelectedItem = localModels.Whisper;
                foreach (VoiceChoice voice in LocalSources.Voices) voiceBox.Items.Add(voice);
                voiceBox.SelectedItem = LocalSources.Voice(localModels.Voice);
                Button useRecommended = Button("Empfehlung übernehmen");
                var status = Hint(760, Theme.Text, "");
                var progress = Hint(760, Theme.Cyan, "");
                Button ollamaPage = Button("Ollama-Downloadseite öffnen"), download = Big("Fehlendes herunterladen", Theme.Cyan),
                    bench = Big("Geschwindigkeit messen", Theme.Green), cancelWork = Button("Abbrechen");
                cancelWork.Enabled = false;
                var benchText = Hint(760, Theme.Text, lastBenchmark.Length == 0 ? "Noch nicht gemessen." : lastBenchmark);
                CancellationTokenSource work = null;

                body.Controls.Add(Section("HARDWARE", hwText, recText));
                body.Controls.Add(Section("MODELLE", Row(Caption("Sprachmodell (Ollama)"), llmBox), Row(Caption("Spracherkennung (Whisper)"), whisperBox),
                    Row(Caption("Stimme (Piper)"), voiceBox), useRecommended));
                body.Controls.Add(Section("STATUS", status, Row(ollamaPage, download, cancelWork), progress));
                body.Controls.Add(Section("GESCHWINDIGKEIT AUF DIESEM PC", Hint(760, Theme.Muted,
                    "Misst Spracherkennung, Sprachmodell und Sprachausgabe mit einem deutschen Testsatz und schätzt die Reaktionszeit. Alles läuft lokal; es entstehen keine API-Kosten."),
                    Row(bench), benchText));
                foreach (Control section in body.Controls) { section.Width = 770; section.MaximumSize = new Size(770, 0); section.MinimumSize = new Size(770, 0); }

                Action read = () =>
                {
                    localModels.Llm = (llmBox.Text ?? "").Trim().Length == 0 ? "gemma3:4b" : llmBox.Text.Trim();
                    localModels.Whisper = whisperBox.SelectedItem as string ?? "base";
                    var voice = voiceBox.SelectedItem as VoiceChoice;
                    localModels.Voice = voice == null ? LocalSources.Voices[0].Id : voice.Id;
                    localModels.Threads = recommended.Threads;
                };
                Func<List<string>> missing = null;
                Action refresh = () =>
                {
                    read();
                    var lines = new List<string>();
                    List<string> installed = null;
                    try { installed = OllamaChat.InstalledModels(); lines.Add("✓ Ollama läuft (" + installed.Count + " Modelle)"); }
                    catch (Exception) { lines.Add("✗ Ollama nicht erreichbar: bitte von ollama.com installieren und starten"); }
                    if (installed != null) lines.Add((OllamaChat.HasModel(installed, localModels.Llm) ? "✓ " : "✗ ") + "Sprachmodell " + localModels.Llm);
                    lines.Add((File.Exists(LocalPaths.WhisperExe) ? "✓ " : "✗ ") + "whisper.cpp (Spracherkennung)");
                    lines.Add((File.Exists(LocalPaths.WhisperModel(localModels.Whisper)) ? "✓ " : "✗ ") + "Whisper-Modell " + localModels.Whisper + " (mehrsprachig)");
                    lines.Add((File.Exists(LocalPaths.PiperExe) ? "✓ " : "✗ ") + "Piper (Sprachausgabe)");
                    lines.Add((File.Exists(LocalPaths.VoiceModel(localModels.Voice)) ? "✓ " : "✗ ") + "Stimme " + localModels.Voice);
                    status.Text = string.Join("\n", lines);
                    bool ready = missing().Count == 0 && installed != null && OllamaChat.HasModel(installed, localModels.Llm);
                    bench.Enabled = ready && work == null;
                    download.Enabled = work == null && (missing().Count != 0 || (installed != null && !OllamaChat.HasModel(installed, localModels.Llm)));
                };
                missing = () =>
                {
                    var list = new List<string>();
                    if (!File.Exists(LocalPaths.WhisperExe)) list.Add("whisper");
                    if (!File.Exists(LocalPaths.WhisperModel(localModels.Whisper))) list.Add("whisperModel");
                    if (!File.Exists(LocalPaths.PiperExe)) list.Add("piper");
                    if (!File.Exists(LocalPaths.VoiceModel(localModels.Voice))) list.Add("voice");
                    return list;
                };
                Action<bool> busy = on =>
                {
                    download.Enabled = bench.Enabled = useRecommended.Enabled = close.Enabled = !on;
                    cancelWork.Enabled = on;
                };
                Action<string> report = text => { if (!dialog.IsDisposed) dialog.BeginInvoke(new Action(() => progress.Text = text)); };

                useRecommended.Click += delegate
                {
                    llmBox.Text = recommended.Llm; whisperBox.SelectedItem = recommended.Whisper; refresh();
                };
                foreach (Control box in new Control[] { whisperBox, voiceBox }) ((ComboBox)box).SelectedIndexChanged += delegate { refresh(); };
                llmBox.TextChanged += delegate { refresh(); };
                ollamaPage.Click += delegate
                {
                    try { Process.Start(new ProcessStartInfo { FileName = LocalSources.OllamaDownload, UseShellExecute = true }); } catch (Exception) { }
                };
                cancelWork.Click += delegate { if (work != null) work.Cancel(); };
                download.Click += async delegate
                {
                    read();
                    List<string> todo = missing();
                    bool needModel = false;
                    try { needModel = !OllamaChat.HasModel(OllamaChat.InstalledModels(), localModels.Llm); } catch (Exception) { }
                    var plan = new List<string>();
                    if (todo.Contains("whisper")) plan.Add("• whisper.cpp für Windows (GitHub ggml-org/whisper.cpp, MIT-Lizenz)");
                    if (todo.Contains("whisperModel")) plan.Add("• Whisper-Modell " + localModels.Whisper + " (Hugging Face ggerganov/whisper.cpp)");
                    if (todo.Contains("piper")) plan.Add("• Piper 2023.11.14-2 für Windows (GitHub rhasspy/piper, MIT-Lizenz)");
                    if (todo.Contains("voice")) plan.Add("• Stimme " + localModels.Voice + " (Hugging Face rhasspy/piper-voices; Lizenz siehe MODEL_CARD)");
                    if (needModel) plan.Add("• Sprachmodell " + localModels.Llm + " über Ollama (Lizenz des Modellherstellers)");
                    if (plan.Count == 0) { refresh(); return; }
                    if (MessageBox.Show(dialog, "DEBATE AI lädt aus den offiziellen Quellen herunter und speichert unter\n" + LocalPaths.Root + ":\n\n" + string.Join("\n", plan) +
                        "\n\nJe nach Modell sind das mehrere Gigabyte. Fortfahren?", "Herunterladen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    work = new CancellationTokenSource();
                    busy(true);
                    try
                    {
                        CancellationToken token = work.Token;
                        LocalModels target = localModels;
                        await Task.Run(async () =>
                        {
                            if (todo.Contains("whisper"))
                                await Downloader.Zip(Downloader.AssetUrl(LocalSources.WhisperReleases, "whisper-bin-x64.zip", new[] { "bin", "x64" }, new[] { "blas", "cuda", "vulkan", "openvino", "cublas", "arm", "win32" }),
                                    LocalPaths.WhisperFolder, report, token);
                            if (todo.Contains("whisperModel"))
                                await Downloader.File(LocalSources.WhisperModelBase + target.Whisper + ".bin", LocalPaths.WhisperModel(target.Whisper), report, token);
                            if (todo.Contains("piper"))
                                await Downloader.Zip(Downloader.AssetUrl(LocalSources.PiperRelease, "piper_windows_amd64.zip", new[] { "windows", "amd64" }, new string[0]),
                                    LocalPaths.PiperFolder, report, token);
                            if (todo.Contains("voice"))
                            {
                                VoiceChoice voice = LocalSources.Voice(target.Voice);
                                await Downloader.File(LocalSources.VoiceBase + voice.Path + voice.Id + ".onnx", LocalPaths.VoiceModel(voice.Id), report, token);
                                await Downloader.File(LocalSources.VoiceBase + voice.Path + voice.Id + ".onnx.json", LocalPaths.VoiceModel(voice.Id) + ".json", report, token);
                            }
                            if (needModel) await OllamaChat.Pull(target.Llm, text => report("Ollama: " + text), token);
                        });
                        progress.Text = "Fertig heruntergeladen.";
                        LocalData.WriteLog("Lokale KI heruntergeladen: " + string.Join(", ", todo) + (needModel ? ", " + localModels.Llm : ""));
                    }
                    catch (OperationCanceledException) { progress.Text = "Abgebrochen."; }
                    catch (Exception error) { progress.Text = "Fehler: " + (error is AppError ? error.Message : error.GetBaseException().Message); }
                    finally { work.Dispose(); work = null; busy(false); refresh(); }
                };
                bench.Click += async delegate
                {
                    read();
                    work = new CancellationTokenSource();
                    busy(true);
                    benchText.Text = "Messe …";
                    try
                    {
                        CancellationToken token = work.Token;
                        LocalModels target = localModels;
                        Persona persona = CurrentPersona();
                        BenchmarkResult result = await Task.Run(() =>
                        {
                            ISpeechToText stt = localEngine.Stt(target);
                            ITextToSpeech tts = localEngine.Tts(target);
                            return LocalBenchmark.Run(stt, new OllamaChat(target.Llm), tts, persona, report, token);
                        });
                        lastBenchmark = "Gemessen am " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + " mit " + target.Llm + ", Whisper " + target.Whisper + ", Stimme " + target.Voice + ":\n" + result.Describe();
                        benchText.Text = lastBenchmark;
                        progress.Text = "";
                        LocalData.WriteLog("Lokale Messung: " + target.Llm + " · Whisper " + target.Whisper + " · " + target.Voice + " · " + result.Describe().Replace("\n", " · "));
                    }
                    catch (OperationCanceledException) { benchText.Text = "Abgebrochen."; }
                    catch (Exception error) { benchText.Text = "Messung fehlgeschlagen: " + (error is AppError ? error.Message : error.GetBaseException().Message); }
                    finally { work.Dispose(); work = null; busy(false); refresh(); }
                };
                close.Click += delegate { dialog.Close(); };
                dialog.FormClosing += (s, e) => { if (work != null) { work.Cancel(); e.Cancel = true; } };
                dialog.Shown += delegate { refresh(); };
                dialog.ShowDialog(this);
                read();
            }
            SaveSettings();
            Render();
        }
    }
}
