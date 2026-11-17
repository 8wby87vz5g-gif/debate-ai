using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DebateAI
{
    internal static class Tests
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                Run("PCM-Konvertierung und Testton", TestPcm);
                Run("Spracherkennung, kurze Signale und Aufnahmelimit", TestDetector);
                Run("Automatischer Dialog mit Rückkopplungssperre", TestAutomatic);
                Run("Redezeitlimit sendet den Beitrag statt ihn zu verwerfen", TestLimitSends);
                Run("Fairness: fertige Antwort wartet auf Sprechpause", TestHold);
                Run("Fairness: Weitersprechen verwirft ungehörte Antwort, Warteschlange", TestRequeue);
                Run("Streaming: NOVA spricht vor dem Ende der Antwort", TestStreaming);
                Run("NOVA unterbrechen kürzt die Antwort im Kontext", TestInterrupt);
                Run("Jetzt antworten im Automatikmodus", TestForceReply);
                Run("Persönlichkeit, Position, Antwortlänge und KI-Hinweis", TestPersona);
                Run("Ruhe bricht Antwort ab und verwirft verspätetes Audio", TestModeration);
                Run("Push-to-test, Pause und verspätete Antwort", TestPause);
                Run("Pegel und Testton ohne Debattensitzung", TestRouting);
                Run("Verbindungsfehler und anschließender Neustart", TestRetry);
                Run("Realtime-Sitzung, Chunking und Antwort-Audio mit Testserver-Ereignissen", TestRealtime);
                Run("Fehlender KI-Hinweis, API-Fehler und Abbruch", TestRealtimeErrors);
                Run("Realtime: Mitschrift, Löschen und Kürzen ungehörter Antworten", TestRealtimeCut);
                Run("Realtime: leerer Beitrag fordert nur eine Antwort an", TestRealtimeEmpty);
                Run("Nutzungsprotokoll enthält ausschließlich Tokenzahlen", TestUsage);
                Run("Windows-Verschlüsselung für aktuelles Benutzerkonto", TestDpapi);
                Run("Windows-Geräteliste und Fensteraufbau", TestWindows);
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Run(string name, Action test) { test(); Console.WriteLine("OK  " + name); }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Wait(Task task) { task.GetAwaiter().GetResult(); }
        private static byte[] Voice() { return WinAudio.Tone(40, 440); }
        private static byte[] Silence() { return new byte[WinAudio.BytesPerSecond * 40 / 1000]; }
        private static byte[] Long(int ms) { return WinAudio.Tone(ms, 330); }

        private static void TestPcm()
        {
            var mono = new byte[4];
            WinAudio.WriteSample(mono, 0, -4000); WinAudio.WriteSample(mono, 2, 6000);
            byte[] stereo = WinAudio.ToStereo48(mono);
            Check(stereo.Length == 16 && WinAudio.ReadSample(stereo, 0) == -4000 &&
                WinAudio.ReadSample(stereo, 2) == -4000 && WinAudio.ReadSample(stereo, 4) == 1000, "Stereo-Interpolation");
            byte[] reduced = WinAudio.ToMono24(stereo, new WinAudio.Format(48000, 2));
            Check(reduced.Length == 4 && WinAudio.ReadSample(reduced, 0) == -1500 && WinAudio.ReadSample(reduced, 2) == 6000, "Downmix");
            Check(TurnDetector.Level(Voice()) > -40 && TurnDetector.Level(Silence()) == -90, "RMS-Pegel");
        }

        private static void TestDetector()
        {
            using (var detector = new TurnDetector { Threshold = -40 })
            {
                for (int i = 0; i < 40; i++) Check(!detector.Add(Silence(), -90, true), "Stille löst Antwort aus");
                for (int i = 0; i < 3; i++) detector.Add(Voice(), -22, true);
                Check(detector.Recording, "Sprache startet nicht");
                for (int i = 0; i < 18; i++) Check(!detector.Add(Silence(), -90, true), "Zu früher Abschluss");
                Check(detector.Add(Silence(), -90, true) && detector.Finish() == null, "Zu kurzer Impuls muss verworfen werden");
                detector.BeginManual();
                byte[] frame = Voice();
                for (int i = 0; i < 299; i++) Check(!detector.Add(frame, -22, false), "Limit zu früh");
                Check(detector.Add(frame, -22, false), "Aufnahmelimit fehlt");
                Check(detector.Finish().Length == 12 * WinAudio.BytesPerSecond, "Aufnahme zu lang oder abgeschnitten");
            }
        }

        private static void TestAutomatic()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback(); var ai = new FakeDebater();
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, false));
                time = 900; Wait(session.Tick());
                Check(session.State == Phase.Listening, "Nachlauf endet nicht");
                input.Add(Voice(), 10); Wait(session.Tick());
                Check(session.State == Phase.Recording && ai.Replies == 0, "Automatische Aufnahme startet nicht");
                input.Add(Silence(), 19); Wait(session.Tick());
                Check(session.State == Phase.Speaking && ai.Replies == 1 && output.Plays == 1, "Antwort nach Pause fehlt");
                input.Add(Voice(), 45); Wait(session.Tick());
                Check(ai.Replies == 1 && input.Discarded == 45, "KI-Ausgabe gelangt zum Turn-Detektor");
                output.Done = true; Wait(session.Tick());
                time += 899; input.Add(Voice(), 10); Wait(session.Tick());
                Check(session.State == Phase.Cooldown && input.Discarded == 55, "Nachlauf verwirft Eingang nicht");
                time++; Wait(session.Tick()); input.Add(Silence(), 30); Wait(session.Tick());
                Check(session.State == Phase.Listening && ai.Replies == 1, "Echo löst neuen Turn aus");
                session.Stop(false);
                Check(input.Closed && output.Closed && ai.Closed && session.State == Phase.Idle, "Stopp gibt Geräte nicht frei");
            }
        }

        private static void TestLimitSends()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback(); var ai = new FakeDebater();
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, true, 5));
                time = 900; Wait(session.Tick());
                input.Add(Voice(), 124); Wait(session.Tick());
                Check(session.State == Phase.Recording && ai.Replies == 0, "Limit zu früh");
                input.Add(Voice(), 1); Wait(session.Tick());
                Check(ai.Replies == 1 && ai.LastLength == 5 * WinAudio.BytesPerSecond, "Am Redezeitlimit muss der Beitrag gesendet werden");
                Check(session.State == Phase.Speaking && output.Plays == 1, "Antwort nach Redezeitlimit fehlt");
            }
        }

        private static void TestHold()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                var ai = new FakeDebater { Pending = new TaskCompletionSource<byte[]>() };
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, true));
                time = 900; Wait(session.Tick());
                input.Add(Voice(), 6); input.Add(Silence(), 29); Wait(session.Tick());
                Check(ai.Replies == 0, "Fairness braucht 1200 ms Pause");
                input.Add(Silence(), 1); Task thinking = session.Tick();
                Check(ai.Replies == 1 && session.State == Phase.Thinking, "Beitrag nach Pause fehlt");
                input.Add(Voice(), 3); Wait(session.Tick());
                ai.Pending.SetResult(Long(400)); Wait(thinking);
                Check(session.State == Phase.WaitingForQuiet && output.Plays == 0, "Fertige Antwort fällt Gast ins Wort");
                input.Add(Silence(), 30); Wait(session.Tick());
                Check(session.State == Phase.Speaking && output.Plays == 1, "Nach der Pause spricht NOVA nicht");
                Check(ai.Cancels.Count == 0, "Kurzes Geräusch darf die Antwort nicht verwerfen");
            }
        }

        private static void TestRequeue()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                var first = new TaskCompletionSource<byte[]>();
                var ai = new FakeDebater { Pending = first };
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, true));
                time = 900; Wait(session.Tick());
                input.Add(Voice(), 10); input.Add(Silence(), 30); Task thinking = session.Tick();
                Check(ai.Replies == 1 && session.State == Phase.Thinking, "Erster Beitrag fehlt");
                input.Add(Voice(), 20); Wait(session.Tick());
                Check(session.State == Phase.Recording && ai.Cancels.Count == 1 && ai.Cancels[0] == -1, "Weitersprechen verwirft die ungehörte Antwort nicht");
                ai.Pending = null;
                first.SetResult(Long(400)); Wait(thinking);
                Check(output.Plays == 0 && session.State == Phase.Recording, "Verworfene Antwort wurde abgespielt");
                input.Add(Silence(), 30); Wait(session.Tick());
                Check(ai.Replies == 2 && session.State == Phase.Speaking && output.Plays == 1, "Fortgesetzter Beitrag wird nicht beantwortet");
            }
        }

        private static void TestStreaming()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                var ai = new FakeDebater { Pending = new TaskCompletionSource<byte[]>(), Early = Long(300) };
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, false));
                time = 900; Wait(session.Tick());
                input.Add(Voice(), 10); input.Add(Silence(), 19); Task thinking = session.Tick();
                Wait(session.Tick());
                Check(session.State == Phase.Speaking && output.Plays == 1 && !thinking.IsCompleted, "NOVA wartet auf die vollständige Antwort");
                output.Done = true; Wait(session.Tick());
                Check(session.State == Phase.Speaking, "Wiedergabe endet vor dem Ende der Antwort");
                ai.Pending.SetResult(Long(200)); Wait(thinking); Wait(session.Tick());
                Check(output.AppendedMs == 500, "Gestreamtes Audio unvollständig");
                output.Done = true; Wait(session.Tick());
                Check(session.State == Phase.Cooldown && session.Answers == 1, "Nach gestreamter Antwort kein Nachlauf");
            }
        }

        private static void TestInterrupt()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                var ai = new FakeDebater { Pending = new TaskCompletionSource<byte[]>(), Early = Long(1000) };
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, false));
                time = 900; Wait(session.Tick());
                input.Add(Voice(), 10); input.Add(Silence(), 19); Task thinking = session.Tick();
                Wait(session.Tick());
                Check(session.CanInterrupt && session.State == Phase.Speaking, "Unterbrechen nicht möglich");
                output.Played = 420;
                session.Interrupt();
                Check(output.Stopped && ai.Cancels.Count == 1 && ai.Cancels[0] == 420 && session.State == Phase.Cooldown, "Unterbrechen stoppt oder kürzt nicht");
                ai.Pending.SetResult(Long(200)); Wait(thinking); Wait(session.Tick());
                Check(session.State == Phase.Cooldown && output.AppendedMs == 1000, "Rest der Antwort läuft nach dem Unterbrechen weiter");
                time += 900; Wait(session.Tick());
                Check(session.State == Phase.Listening, "Nach dem Unterbrechen hört NOVA nicht wieder zu");
            }
        }

        private static void TestForceReply()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback(); var ai = new FakeDebater();
                Wait(session.Start(() => input, () => output, () => ai, true, -40, 900, true));
                time = 900; Wait(session.Tick());
                Check(!session.CanPush, "Jetzt antworten ohne Beitrag");
                input.Add(Voice(), 20); Wait(session.Tick());
                Check(session.CanPush && session.State == Phase.Recording, "Jetzt antworten fehlt während des Beitrags");
                Wait(session.Push());
                Check(ai.Replies == 1 && session.State == Phase.Speaking, "Jetzt antworten sendet nicht");
            }
        }

        private static void TestPersona()
        {
            var persona = new Persona { Name = "Nova", Style = 1, Intensity = 2, Length = 0, Topic = "Tempolimit", Position = "Dafür\u0007" };
            string text = persona.Instructions();
            Check(text.Contains("kritisch und herausfordernd") && text.Contains("leidenschaftlich") && text.Contains("höchstens 2 Sätze") &&
                text.Contains("Tempolimit") && text.Contains("Dafür") && !text.Contains("\u0007"), "Persona-Anweisungen unvollständig");
            Check(text.EndsWith("personenbezogene Details wiederholen.", StringComparison.Ordinal), "Sicherheitsregeln müssen am Ende stehen");
            Check(persona.IsDisclosure("Hallo, ich bin Nova, eine künstliche Intelligenz. Meine Stimme wird von KI erzeugt."), "Wortlaut nicht erkannt");
            Check(persona.IsDisclosure("Hallo! Ich bin Nova – eine KI."), "Sinngemäßer Hinweis nicht erkannt");
            Check(!persona.IsDisclosure("Ich bin Nova."), "Hinweis ohne KI akzeptiert");
            Check(!persona.IsDisclosure("Ich bin eine künstliche Intelligenz."), "Hinweis ohne Namen akzeptiert");
            Check(new Persona { Language = 1 }.Instructions().Contains("Englisch") && new Persona { Language = 1 }.LanguageCode == "en", "Sprache fehlt");
            Check(new Persona { Name = "   " }.Disclosure().Contains("Nova") && new Persona { Length = 9 }.MaxOutputTokens == 3200, "Grenzwerte falsch");
        }

        private static void TestModeration()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                var ai = new FakeDebater { Pending = new TaskCompletionSource<byte[]>() };
                Wait(session.Start(() => input, () => output, () => ai, false, -40, 900));
                time = 900; Wait(session.Tick()); Wait(session.Push());
                input.Add(Voice(), 6); Wait(session.Tick());
                Task thinking = session.Push();
                session.Moderate();
                ai.Pending.SetResult(Voice()); Wait(thinking);
                Check(session.State == Phase.Moderated && ai.Token.IsCancellationRequested &&
                    input.Closed && output.Closed && output.Plays == 0 && !session.CanPush,
                    "Ruhe lässt Aufnahme oder späte Ausgabe weiterlaufen");
                var restartedInput = new FakeCapture(); var restartedOutput = new FakePlayback();
                Wait(session.Start(() => restartedInput, () => restartedOutput, () => new FakeDebater(), false, -40, 900));
                time += 900; Wait(session.Tick()); Wait(session.Push());
                restartedInput.Add(Voice(), 6); Wait(session.Tick()); Wait(session.Push());
                Check(restartedOutput.Plays == 1 && session.State == Phase.Speaking, "Wiedergabe nach Fortsetzen fehlt");
                session.Moderate();
                Check(restartedOutput.Closed && session.State == Phase.Moderated, "Ruhe stoppt laufende Wiedergabe nicht");
                session.Stop(false);
                Check(session.State == Phase.Idle, "Not-Aus aus Moderation fehlt");
            }
        }

        private static void TestPause()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                var ai = new FakeDebater { Pending = new TaskCompletionSource<byte[]>() };
                Wait(session.Start(() => input, () => output, () => ai, false, -40, 900));
                time = 900; Wait(session.Tick()); Wait(session.Push());
                input.Add(Voice(), 6); Wait(session.Tick());
                Task lateReply = session.Push();
                Check(session.State == Phase.Thinking && ai.Replies == 1, "Manuelles Senden fehlt");
                session.Stop(true);
                Check(ai.Token.IsCancellationRequested && input.Closed && output.Closed && session.State == Phase.Paused, "Pause bricht nicht ab");
                var newOutput = new FakePlayback();
                Wait(session.Start(() => new FakeCapture(), () => newOutput, () => new FakeDebater(), false, -40, 900));
                ai.Pending.SetResult(Voice()); Wait(lateReply);
                Check(newOutput.Plays == 0 && output.Plays == 0 && session.State == Phase.Cooldown, "Verspätete Antwort überschreibt neue Sitzung");
            }
        }

        private static void TestRouting()
        {
            using (var session = new DebateSession(() => 0))
            {
                var input = new FakeCapture(); var output = new FakePlayback();
                session.CheckLevel(() => input);
                session.TestTone(() => output);
                input.Add(Voice(), 1); Wait(session.Tick());
                Check(session.State == Phase.Meter && output.Plays == 1 && session.Level > -40, "Pegeltest und Ausgang laufen nicht parallel");
                output.Done = true; Wait(session.Tick());
                Check(output.Closed && !input.Closed && session.State == Phase.Meter, "Pegeltest stoppt vorzeitig");
            }
        }

        private static void TestRetry()
        {
            using (var session = new DebateSession(() => 0))
            {
                var failed = new FakePlayback();
                Wait(session.Start(() => { throw new AppError("Eingang fehlt"); }, () => failed, () => new FakeDebater(), false, -40, 900));
                Check(failed.Closed && session.State == Phase.Idle && session.Note == "Eingang fehlt", "Fehler lässt Ausgang geöffnet");
                var broken = new FakeDebater { StartError = new OperationCanceledException() };
                var input = new FakeCapture();
                Wait(session.Start(() => input, () => new FakePlayback(), () => broken, true, -40, 900));
                Check(input.Closed && broken.Closed && session.Note.Contains("nicht rechtzeitig"), "Timeout ohne Aufräumen");
                Wait(session.Start(() => new FakeCapture(), () => new FakePlayback(), () => new FakeDebater(), true, -40, 900));
                Check(session.State == Phase.Cooldown, "Wiederholung nach Fehler scheitert");
            }
        }

        private static void TestRealtime()
        {
            var wire = new FakeWire();
            wire.Startup(RealtimeDebater.Disclosure);
            wire.In.Enqueue("{\"type\":\"input_audio_buffer.committed\"}");
            wire.Audio(null, "completed");
            using (var ai = new RealtimeDebater(wire))
            {
                byte[] intro = ai.Start(CancellationToken.None).GetAwaiter().GetResult();
                Check(intro.Length == Voice().Length, "Intro-PCM stimmt nicht");
                Check(wire.Out[0].Contains("\"turn_detection\":null") && wire.Out[0].Contains("24000"), "Sessionformat oder VAD falsch");
                Check(wire.Out[1].Contains("\"conversation\":\"none\""), "KI-Hinweis sollte nicht zum Debattenverlauf zählen");
                var sink = new AudioSink();
                ai.Reply(new byte[14000], sink, CancellationToken.None).GetAwaiter().GetResult();
                Check(sink.ReadAll().Length == Voice().Length && wire.Out.Count == 6 && wire.Out[2].Contains(".append") && wire.Out[3].Contains(".append") &&
                    wire.Out[4].Contains(".commit") && wire.Out[5].Contains("response.create"), "Falsche Ereignisreihenfolge oder PCM-Chunking");
            }
        }

        private static void TestRealtimeErrors()
        {
            var wire = new FakeWire(); wire.Startup("Ich bin Nova."); wire.Audio("Ich bin Nova.", "completed");
            using (var ai = new RealtimeDebater(wire)) Expect<AppError>(() => Wait(ai.Start(CancellationToken.None)), "KI-Hinweis");
            wire = new FakeWire();
            wire.In.Enqueue("{\"type\":\"error\",\"error\":{\"code\":\"insufficient_quota\",\"message\":\"SECRET\"}}");
            using (var ai = new RealtimeDebater(wire)) Expect<AppError>(() => Wait(ai.Start(CancellationToken.None)), "OpenAI-Limit");
            wire = new FakeWire();
            wire.In.Enqueue("{\"type\":\"error\",\"error\":{\"type\":\"insufficient_quota\",\"code\":\"credit_balance_exhausted\",\"message\":\"SECRET\"}}");
            using (var ai = new RealtimeDebater(wire)) Expect<AppError>(() => Wait(ai.Start(CancellationToken.None)), "API-Guthaben ist aufgebraucht");
            wire = new FakeWire(); wire.Startup(RealtimeDebater.Disclosure);
            wire.In.Enqueue("{\"type\":\"input_audio_buffer.committed\"}"); wire.Audio(null, "incomplete");
            using (var ai = new RealtimeDebater(wire))
            {
                Wait(ai.Start(CancellationToken.None));
                Expect<AppError>(() => Wait(ai.Reply(Voice(), new AudioSink(), CancellationToken.None)), "abgebrochen");
            }
            using (var token = new CancellationTokenSource())
            using (var ai = new RealtimeDebater(new FakeWire()))
            {
                token.Cancel();
                Expect<OperationCanceledException>(() => Wait(ai.Start(token.Token)), null);
            }
        }

        private static void TestRealtimeCut()
        {
            var wire = new FakeWire(); wire.Startup(RealtimeDebater.Disclosure);
            wire.In.Enqueue("{\"type\":\"input_audio_buffer.committed\"}");
            wire.In.Enqueue("{\"type\":\"conversation.item.input_audio_transcription.completed\",\"transcript\":\" Schule sollte später beginnen. \"}");
            wire.Audio("Das ist ein interessanter Punkt.", "completed");
            using (var ai = new RealtimeDebater(wire, new Persona(), true))
            {
                Wait(ai.Start(CancellationToken.None));
                Check(wire.Out[0].Contains("gpt-4o-mini-transcribe") && wire.Out[0].Contains("\"language\":\"de\"") && wire.Out[0].Contains("marin"),
                    "Transkription oder Stimme nicht konfiguriert");
                Wait(ai.Reply(Voice(), new AudioSink(), CancellationToken.None));
                var lines = new List<string>(); TranscriptLine line;
                while (ai.TryTakeLine(out line)) lines.Add(line.Speaker + ":" + line.Text);
                Check(lines.Contains("Gast:Schule sollte später beginnen.") && lines.Contains("Nova:Das ist ein interessanter Punkt."), "Mitschrift fehlt");
                int before = wire.Out.Count;
                ai.Cancel(-1);
                Check(wire.Out.Count == before + 1 && wire.Out[before].Contains("conversation.item.delete") && wire.Out[before].Contains("item_test"),
                    "Ungehörte Antwort wird nicht aus dem Kontext gelöscht");
                ai.Cancel(500);
                Check(wire.Out.Count == before + 1, "Bereits gelöschte Antwort darf nicht erneut bearbeitet werden");
            }
            wire = new FakeWire(); wire.Startup(RealtimeDebater.Disclosure);
            wire.In.Enqueue("{\"type\":\"input_audio_buffer.committed\"}");
            wire.Audio(null, "completed");
            using (var ai = new RealtimeDebater(wire))
            {
                Wait(ai.Start(CancellationToken.None));
                Wait(ai.Reply(Voice(), new AudioSink(), CancellationToken.None));
                ai.Cancel(500);
                Check(wire.Out[wire.Out.Count - 1].Contains("conversation.item.truncate") && wire.Out[wire.Out.Count - 1].Contains("\"audio_end_ms\":500"),
                    "Unterbrochene Antwort wird nicht gekürzt");
            }
        }

        private static void TestRealtimeEmpty()
        {
            var wire = new FakeWire(); wire.Startup(RealtimeDebater.Disclosure);
            wire.Audio(null, "completed");
            using (var ai = new RealtimeDebater(wire))
            {
                Wait(ai.Start(CancellationToken.None));
                int before = wire.Out.Count;
                var sink = new AudioSink();
                Wait(ai.Reply(new byte[0], sink, CancellationToken.None));
                Check(wire.Out.Count == before + 1 && wire.Out[before].Contains("response.create") && sink.TotalBytes > 0, "Leerer Beitrag falsch behandelt");
            }
        }

        private static void Expect<T>(Action action, string expected) where T : Exception
        {
            try { action(); }
            catch (T error)
            {
                Check(!error.Message.Contains("SECRET") && (expected == null || error.Message.Contains(expected)), "Fehlermeldung falsch oder vertrauliche Daten enthalten");
                return;
            }
            throw new Exception("Erwarteter Fehler fehlt: " + typeof(T).Name);
        }

        private static void TestDpapi()
        {
            byte[] sample = { 13, 17, 22 };
            byte[] cipher = ProtectedData.Protect(sample, null, DataProtectionScope.CurrentUser);
            byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            Check(cipher.Length > sample.Length && plain.Length == 3 && plain[1] == 17, "DPAPI-Rundlauf gescheitert");
        }

        private static void TestUsage()
        {
            var serializer = new JavaScriptSerializer();
            var sample = serializer.Deserialize<Dictionary<string, object>>(
                "{\"input_token_details\":{\"audio_tokens\":40,\"text_tokens\":11,\"cached_tokens_details\":{\"audio_tokens\":20,\"text_tokens\":5}},\"output_token_details\":{\"audio_tokens\":32,\"text_tokens\":8,\"untrusted\":\"SECRET\"}}");
            Check(RealtimeDebater.FormatUsage(sample) == "Usage audioIn=40 audioCached=20 audioOut=32 textIn=11 textCached=5 textOut=8",
                "Nutzungswerte falsch oder unzulässige Felder übernommen");
            Check(RealtimeDebater.FormatUsage(null) == null, "Fehlende Nutzungsdaten müssen erkennbar bleiben");
        }

        private static void TestWindows()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("    Übersprungen: nur unter Windows ausführbar (WinMM).");
                return;
            }
            var inputs = WinAudio.Devices(true); var outputs = WinAudio.Devices(false);
            Console.WriteLine("    Windows meldet {0} Eingang/Eingänge und {1} Ausgang/Ausgänge.", inputs.Count, outputs.Count);
            foreach (var device in inputs) Check(WinAudio.Resolve(true, device) == device.Id, "Eingangs-ID stimmt nicht");
            foreach (var device in outputs) Check(WinAudio.Resolve(false, device) == device.Id, "Ausgangs-ID stimmt nicht");
            Application.EnableVisualStyles();
            using (var form = new MainForm())
            {
                Check(form.Handle != IntPtr.Zero && form.Text.Contains("DEBATE AI"), "Fenster konnte nicht erstellt werden");
                form.PerformLayout();
            }
        }

        private sealed class FakeCapture : ICapture
        {
            private readonly Queue<byte[]> frames = new Queue<byte[]>();
            public int Discarded;
            public bool Closed;
            public void Add(byte[] frame, int count) { for (int i = 0; i < count; i++) frames.Enqueue(frame); }
            public void Drain(Action<byte[]> onFrame)
            {
                while (frames.Count != 0)
                {
                    byte[] frame = frames.Dequeue();
                    if (onFrame == null) Discarded++;
                    else onFrame(frame);
                }
            }
            public void Dispose() { Closed = true; }
        }

        private sealed class FakePlayback : IPlayback
        {
            public bool Done = true, Closed, Stopped;
            public int Plays, AppendedMs, Played;
            public bool Finished { get { return Done; } }
            public int PlayedMs { get { return Played; } }
            public void Play(byte[] pcm) { Check(pcm.Length > 0 && !Closed, "Ungültige Wiedergabe"); Plays++; Done = false; }
            public void Append(byte[] pcm)
            {
                Check(pcm.Length > 0 && !Closed, "Ungültige Wiedergabe");
                if (Done) Plays++;
                AppendedMs += pcm.Length * 1000 / WinAudio.BytesPerSecond;
                Done = false;
            }
            public void Stop() { Stopped = true; Done = true; }
            public void Dispose() { Closed = true; }
        }

        private sealed class FakeDebater : IDebater
        {
            public string LastUsage { get { return null; } }
            public TaskCompletionSource<byte[]> Pending;
            public byte[] Early;
            public Exception StartError;
            public CancellationToken Token;
            public int Replies, LastLength;
            public readonly List<int> Cancels = new List<int>();
            public bool Closed;
            public Task<byte[]> Start(CancellationToken token)
            {
                if (StartError != null) throw StartError;
                return Task.FromResult<byte[]>(null);
            }
            public async Task Reply(byte[] pcm, AudioSink sink, CancellationToken token)
            {
                Replies++; Token = token; LastLength = pcm.Length;
                if (Early != null) sink.Write(Early);
                if (Pending == null) { sink.Write(Voice()); return; }
                byte[] rest = await Pending.Task;
                sink.Write(rest);
            }
            public void Cancel(int playedMs) { Cancels.Add(playedMs); }
            public bool TryTakeLine(out TranscriptLine line) { line = null; return false; }
            public void Dispose() { Closed = true; }
        }

        private sealed class FakeWire : IRealtimeWire
        {
            public readonly Queue<string> In = new Queue<string>();
            public readonly List<string> Out = new List<string>();
            private readonly JavaScriptSerializer json = new JavaScriptSerializer();
            public Task Open(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(0); }
            public Task Send(string message, CancellationToken token) { token.ThrowIfCancellationRequested(); Out.Add(message); return Task.FromResult(0); }
            public Task<string> Receive(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(In.Dequeue()); }
            public void Dispose() { }
            public void Startup(string transcript)
            {
                In.Enqueue("{\"type\":\"session.created\"}"); In.Enqueue("{\"type\":\"session.updated\"}");
                Audio(transcript, "completed");
            }
            public void Audio(string transcript, string status)
            {
                In.Enqueue(json.Serialize(new { type = "response.output_item.added", item = new { id = "item_test" } }));
                In.Enqueue(json.Serialize(new { type = "response.output_audio.delta", delta = Convert.ToBase64String(Voice()) }));
                if (transcript != null) In.Enqueue(json.Serialize(new { type = "response.output_audio_transcript.done", transcript = transcript }));
                In.Enqueue(json.Serialize(new { type = "response.done", response = new { status = status } }));
            }
        }
    }
}
