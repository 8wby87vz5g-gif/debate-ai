using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace DebateAI
{
    // End-to-end check of the free local mode on a real Windows machine:
    // official downloads → whisper.cpp → Ollama → Piper, measured like "Geschwindigkeit messen",
    // then three real debate exchanges through LocalDebater. Used by CI; can also be run by hand.
    // Usage: LocalE2E.exe [ollama-model] [whisper-size]
    internal static class LocalE2E
    {
        private static readonly string[] Guest =
        {
            "Ich finde, Social Media macht Jugendliche einsam, weil sie sich nur noch mit anderen vergleichen.",
            "Aber die Zahlen zeigen doch, dass Depressionen bei Jugendlichen stark gestiegen sind.",
            "Dann sollten Smartphones an Schulen eben ganz verboten werden."
        };

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                string reason;
                HardwareInfo hardware = HardwareInfo.Detect();
                LocalModels recommended = ModelAdvisor.Recommend(hardware, out reason);
                Console.WriteLine("Hardware: " + hardware.Describe());
                Console.WriteLine("Empfehlung: " + recommended.Llm + " · Whisper " + recommended.Whisper + " · " + reason);
                var models = new LocalModels
                {
                    Llm = args.Length > 0 ? args[0] : recommended.Llm,
                    Whisper = args.Length > 1 ? args[1] : recommended.Whisper,
                    Voice = recommended.Voice,
                    Threads = recommended.Threads
                };
                Console.WriteLine("Getestet: " + models.Llm + " · Whisper " + models.Whisper + " · " + models.Voice + " · " + models.Threads + " Threads");

                var clock = Stopwatch.StartNew();
                List<string> todo = LocalInstaller.Missing(models);
                bool needModel = !OllamaChat.HasModel(OllamaChat.InstalledModels(), models.Llm);
                string last = "";
                LocalInstaller.Download(models, todo, needModel, text =>
                {
                    string head = text.Length > 18 ? text.Substring(0, 18) : text;
                    if (head != last) { last = head; Console.WriteLine("  " + text); }
                }, CancellationToken.None).GetAwaiter().GetResult();
                Require(LocalInstaller.Missing(models).Count == 0, "Nach dem Download fehlt noch: " + string.Join(", ", LocalInstaller.Missing(models)));
                Require(OllamaChat.HasModel(OllamaChat.InstalledModels(), models.Llm), "Ollama-Modell fehlt nach dem Download");
                Console.WriteLine("Download und Einrichtung: " + clock.ElapsedMilliseconds / 1000 + " s");
                Console.WriteLine("whisper-server: " + LocalPaths.WhisperExe);
                Console.WriteLine("piper: " + LocalPaths.PiperExe);

                var persona = new Persona { Name = "Nova" };
                using (var engine = new LocalEngine())
                {
                    ISpeechToText stt = engine.Stt(models);
                    ITextToSpeech tts = engine.Tts(models);
                    var llm = new OllamaChat(models.Llm);
                    for (int round = 1; round <= 2; round++)
                    {
                        BenchmarkResult result = LocalBenchmark.Run(stt, llm, tts, persona, text => { }, CancellationToken.None).GetAwaiter().GetResult();
                        Console.WriteLine("Messung " + round + (round == 1 ? " (Kaltstart)" : " (warm)") + ":\n  " + result.Describe().Replace("\n", "\n  "));
                        Require(result.Heard.ToLowerInvariant().Contains("tempolimit"), "Spracherkennung hat den Testsatz nicht verstanden: " + result.Heard);
                        Require(result.Answer.Length > 10, "Sprachmodell hat nicht geantwortet");
                    }

                    using (IDebater nova = engine.Create(models, persona, ""))
                    {
                        byte[] intro = nova.Start(true, CancellationToken.None).GetAwaiter().GetResult();
                        Require(intro.Length > WinAudio.BytesPerSecond, "KI-Hinweis wurde nicht gesprochen");
                        Drain(nova, LiveKind.Line, 30000);
                        var times = new List<long>();
                        for (int i = 0; i < Guest.Length; i++)
                        {
                            byte[] speech = tts.Speak(Guest[i], CancellationToken.None).GetAwaiter().GetResult();
                            nova.Append(speech);
                            nova.Commit();
                            var sink = new AudioSink();
                            var turn = Stopwatch.StartNew();
                            nova.Respond(sink, null);
                            long first = -1;
                            string heard = null, said = null;
                            while (turn.ElapsedMilliseconds < 180000)
                            {
                                if (first < 0 && sink.TotalBytes > 0) first = turn.ElapsedMilliseconds;
                                LiveEvent item;
                                if (!nova.TryTake(out item)) { Thread.Sleep(5); continue; }
                                if (item.Kind == LiveKind.Fatal || item.Kind == LiveKind.Warning) throw new Exception("NOVA meldet: " + item.Text + " " + item.Error);
                                if (item.Kind == LiveKind.Line && item.Speaker == "Gast") heard = item.Text;
                                if (item.Kind == LiveKind.Line && item.Speaker != "Gast") said = item.Text;
                                if (item.Kind == LiveKind.AnswerDone) break;
                            }
                            if (first < 0 && sink.TotalBytes > 0) first = turn.ElapsedMilliseconds;
                            Require(said != null && sink.TotalBytes > 0, "Keine hörbare Antwort in Runde " + (i + 1));
                            times.Add(first);
                            Console.WriteLine("Runde " + (i + 1) + ": Gast (erkannt) „" + heard + "“");
                            Console.WriteLine("         NOVA „" + said + "“");
                            Console.WriteLine("         erster Ton nach " + first + " ms (+ 760 ms Sprechpause in echt), Antwort " +
                                sink.TotalBytes * 1000 / WinAudio.BytesPerSecond + " ms lang");
                        }
                        times.Sort();
                        Console.WriteLine("Reaktionszeit ohne Sprechpause: Median " + times[times.Count / 2] + " ms, langsamste " + times[times.Count - 1] + " ms");
                    }
                }
                Console.WriteLine("ERGEBNIS: lokaler Modus funktioniert auf dieser Windows-Maschine.");
                return 0;
            }
            catch (Exception error)
            {
                Console.WriteLine("FEHLER: " + (error is AppError ? error.Message : error.ToString()));
                return 1;
            }
        }

        private static void Drain(IDebater ai, LiveKind until, int timeoutMs)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                LiveEvent item;
                if (ai.TryTake(out item)) { if (item.Kind == until) return; }
                else Thread.Sleep(5);
            }
        }

        private static void Require(bool ok, string message) { if (!ok) throw new AppError(message); }
    }
}
