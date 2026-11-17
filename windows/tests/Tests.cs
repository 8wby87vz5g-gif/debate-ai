﻿using System;
using System.Collections.Concurrent;
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
                Run("Lokaler Sprecherwechsel: Streaming-Upload, Antwort, Halbduplex", TestLocalCycle);
                Run("Redezeitlimit sendet den Beitrag", TestLimitSends);
                Run("Semantischer Sprecherwechsel (OpenAI) mit Latenzmessung", TestServerCycle);
                Run("Fairness: fertige Antwort wartet auf Sprechpause", TestHold);
                Run("Warteschlange: Weitersprechen verwirft ungehörte Antwort (OpenAI-Turns)", TestRequeueServer);
                Run("Warteschlange: Weitersprechen verwirft ungehörte Antwort (lokal)", TestRequeueLocal);
                Run("Vollduplex: Gast unterbricht, NOVA stoppt und kürzt den Kontext", TestBargeStop);
                Run("Vollduplex: Gast unterbricht, NOVA beendet ihren Satz und antwortet danach", TestBargeFinish);
                Run("Ohne Echoprüfung: Unterbrechung erst nach Aufwärmen der Echo-Erkennung", TestNoBargeBeforeWarm);
                Run("Echo-Schutz: NOVAs eigene Stimme löst keine Unterbrechung aus", TestEchoBlocked);
                Run("Selbstgespräch-Schutz schaltet auf Halbduplex", TestSelfTalk);
                Run("NOVA bringt die Debatte nach Stille voran", TestProactive);
                Run("NOVA stumm: hört zu, antwortet nicht", TestMute);
                Run("Debattengedächtnis wird erstellt und übernommen", TestMemory);
                Run("Verbindungsabbruch: neue Sitzung mit Gedächtnis", TestReconnect);
                Run("Fehlgeschlagene Antworten und Zeitlimit", TestFailures);
                Run("NOVA unterbrechen per Knopf", TestInterrupt);
                Run("Jetzt antworten und NOVA soll sprechen", TestPush);
                Run("Push-to-talk, Pause und verspätete Antwort", TestPushToTalk);
                Run("Ruhe in der Debatte, Not-Aus, Fehler und Neustart", TestModerationAndRetry);
                Run("Pegel und Testton ohne Debattensitzung", TestRouting);
                Run("Echoprüfung: Echo, kein Echo, unklar", TestEchoCheck);
                Run("Echo-Erkennung unterscheidet Echo und Gast", TestEchoGuard);
                Run("Live-Messwerte und Testprotokoll", TestMetrics);
                Run("Persönlichkeit, Debattenmethode, Gedächtnis, KI-Hinweis", TestPersona);
                Run("Einrichtungsassistent: Geräteempfehlung je Szenario", TestSetupPick);
                Run("Einrichtungsassistent: Warnungen bei Echo-gefährdetem Routing", TestSetupCheck);
                Run("Persönlichkeitsprofile speichern und laden", TestProfiles);
                Run("Stimmprobe spielt NOVAs Stimme und schließt die Sitzung", TestVoiceTest);
                Run("Voicemeeter: TikTok-Routing-Plan, Anwenden, Rücklesen", TestVoicemeeterPlan);
                Run("Routing-Messung erkennt richtige und falsche Wege", TestRoutingMeasurement);
                Run("Gastton-Test und LIVE-Studio-Erkennung", TestGuestProbe);
                Run("Kostenschätzung, Kostenlimit und Gast ausreden lassen", TestCostLimit);
                Run("Lokal: Resampling, WAV, Satzaufteilung, Whisper-Bereinigung", TestLocalBasics);
                Run("Lokal: Sicherheitssperre erlaubt nur Dienste auf diesem PC", TestLocalGuard);
                Run("Lokal: NOVA-Pipeline Spracherkennung → Sprachmodell → Stimme", TestLocalPipeline);
                Run("Lokal: Unterbrechen, Verwerfen und Gedächtnis ohne Verlust", TestLocalCancel);
                Run("Lokal: komplette Debatte über die Gesprächssteuerung", TestLocalSession);
                Run("Lokal: Hardware-Empfehlung, Downloadauswahl, Ollama-Anfrage, Messung", TestLocalSetup);
                Run("Eigentümer-Lizenz: Signatur, Gerätebindung, Manipulation, Ablauf", TestLicense);
                Run("Realtime: Sitzungsaufbau, semantische VAD, KI-Hinweis", TestRealtimeStart);
                Run("Realtime: Live-Ereignisse, Antwort-Audio, Gedächtnis, Kürzen", TestRealtimeLive);
                Run("Realtime: API-Fehler, Warnungen und Verbindungsende", TestRealtimeErrors);
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

        private static byte[] Slice(byte[] data, int fromMs, int ms, double gain)
        {
            var frame = new byte[WinAudio.BytesPerSecond * ms / 1000];
            int offset = WinAudio.BytesPerSecond / 1000 * fromMs;
            for (int i = 0; i < frame.Length; i += 2)
                if (offset + i >= 0 && offset + i + 1 < data.Length)
                    WinAudio.WriteSample(frame, i, (short)(WinAudio.ReadSample(data, offset + i) * gain));
            return frame;
        }

        // Test rig: one frame per 40 ms tick, like the real capture.
        private sealed class Rig : IDisposable
        {
            public long Time;
            public readonly DebateSession Session;
            public readonly FakeCapture Input = new FakeCapture();
            public readonly FakePlayback Output = new FakePlayback();
            public FakeDebater Ai, Next;
            public int Created;

            public Rig(SessionOptions options, bool server)
            {
                Session = new DebateSession(() => Time);
                Ai = new FakeDebater { Server = server };
                Wait(Session.Start(() => Input, () => Output, () => { Created++; return Created == 1 ? Ai : Next; }, options));
            }

            public void Tick() { Wait(Session.Tick()); }
            public void Frames(byte[] frame, int count) { for (int i = 0; i < count; i++) { Input.Add(frame, 1); Time += 40; Tick(); } }
            public void Settle() { Time += 1000; Tick(); }
            public void Emit(LiveKind kind) { Ai.Events.Enqueue(new LiveEvent(kind)); }

            // One complete server-side turn and a spoken answer.
            public void Cycle(string guest, string nova)
            {
                Emit(LiveKind.SpeechStarted); Tick();
                Frames(Voice(), 5);
                Emit(LiveKind.SpeechStopped); Emit(LiveKind.Committed); Tick();
                if (guest != null) Ai.Events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "Gast", Text = guest });
                Ai.Sink.Write(Long(300)); Tick();
                if (nova != null) Ai.Events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "Nova", Text = nova });
                Ai.Done(true); Output.Done = true; Tick();
                Settle();
            }

            public void Dispose() { Session.Dispose(); }
        }

        private static void TestLocalCycle()
        {
            using (var rig = new Rig(new SessionOptions { ServerTurns = false, Fairness = false, BargeIn = BargeIn.Ignore }, false))
            {
                rig.Settle();
                Check(rig.Session.State == Phase.Listening, "Nachlauf endet nicht");
                rig.Frames(Voice(), 10);
                Check(rig.Session.State == Phase.Recording && rig.Ai.Appended > 0 && rig.Ai.Responds == 0, "Beitrag wird nicht während des Sprechens hochgeladen");
                rig.Frames(Silence(), 19);
                Check(rig.Ai.Commits == 1 && rig.Ai.Responds == 1 && rig.Session.State == Phase.Thinking, "Antwort nach Sprechpause fehlt");
                rig.Ai.Sink.Write(Long(300)); rig.Tick();
                Check(rig.Session.State == Phase.Speaking && rig.Output.Plays == 1 && rig.Session.Metrics.Latency.Count == 1, "NOVA spricht nicht oder Latenz fehlt");
                long uploaded = rig.Ai.Appended;
                rig.Frames(Voice(), 20);
                Check(rig.Ai.Appended == uploaded && rig.Ai.Responds == 1, "Halbduplex: NOVAs Ausgabe erreicht den Eingang");
                rig.Ai.Done(true); rig.Output.Done = true; rig.Tick();
                Check(rig.Session.State == Phase.Cooldown, "Halbduplex ohne Nachlauf");
                rig.Settle(); rig.Frames(Silence(), 30);
                Check(rig.Session.State == Phase.Listening && rig.Ai.Responds == 1, "Echo löst neuen Beitrag aus");
                rig.Session.Stop(false);
                Check(rig.Input.Closed && rig.Output.Closed && rig.Ai.Closed && rig.Session.State == Phase.Idle, "Stopp gibt Geräte nicht frei");
            }
        }

        private static void TestLimitSends()
        {
            using (var rig = new Rig(new SessionOptions { ServerTurns = false, TurnSeconds = 5, RoutingVerified = true }, false))
            {
                rig.Frames(Voice(), 120);
                Check(rig.Ai.Commits == 0, "Limit zu früh");
                rig.Frames(Voice(), 5);
                Check(rig.Ai.Commits == 1 && rig.Ai.Responds == 1, "Am Redezeitlimit muss der Beitrag gesendet werden");
            }
        }

        private static void TestServerCycle()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                Check(rig.Session.State == Phase.Listening, "Mit geprüftem Routing kein Nachlauf nötig");
                rig.Frames(Silence(), 5);
                Check(rig.Ai.Appended == 5 * Silence().Length, "Audio wird nicht fortlaufend an OpenAI gestreamt");
                rig.Emit(LiveKind.SpeechStarted); rig.Tick();
                Check(rig.Session.State == Phase.Recording, "Sprachbeginn nicht angezeigt");
                rig.Frames(Voice(), 10);
                rig.Emit(LiveKind.SpeechStopped); rig.Tick();
                long stopped = rig.Time;
                Check(rig.Ai.Responds == 0, "Antwort vor dem Satzende");
                rig.Time += 300; rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 1 && rig.Session.State == Phase.Thinking && rig.Ai.Commits == 0, "Antwort nach semantischem Satzende fehlt");
                rig.Time += 400; rig.Ai.Sink.Write(Long(300)); rig.Tick();
                Check(rig.Session.State == Phase.Speaking && rig.Session.Metrics.Latency[0] == rig.Time - stopped, "Latenz falsch gemessen");
                Check(rig.Session.Metrics.FirstAudio[0] == 400, "Zeit bis zum ersten Audio falsch");
                rig.Ai.Done(true); rig.Output.Done = true; rig.Tick();
                Check(rig.Session.State == Phase.Listening && rig.Session.Answers == 1, "Nach der Antwort hört NOVA nicht wieder zu");
            }
        }

        private static void TestHold()
        {
            using (var rig = new Rig(new SessionOptions { ServerTurns = false, RoutingVerified = true }, false))
            {
                rig.Frames(Voice(), 6); rig.Frames(Silence(), 30);
                Check(rig.Ai.Responds == 1 && rig.Session.State == Phase.Thinking, "Beitrag nach 1200 ms Pause fehlt");
                rig.Frames(Voice(), 3);
                rig.Ai.Sink.Write(Long(400)); rig.Tick();
                Check(rig.Session.State == Phase.WaitingForQuiet && rig.Output.Plays == 0, "Fertige Antwort fällt dem Gast ins Wort");
                rig.Frames(Silence(), 30);
                Check(rig.Session.State == Phase.Speaking && rig.Ai.Cancels.Count == 0, "Nach der Pause spricht NOVA nicht oder Geräusch verwarf die Antwort");
            }
        }

        private static void TestRequeueServer()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                rig.Emit(LiveKind.SpeechStarted); rig.Frames(Voice(), 5); rig.Emit(LiveKind.SpeechStopped); rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 1, "Erste Antwort fehlt");
                rig.Emit(LiveKind.SpeechStarted); rig.Tick();
                Check(rig.Ai.Cancels.Count == 1 && rig.Ai.Cancels[0] == -1 && rig.Session.State == Phase.Recording && rig.Session.Metrics.Requeues == 1,
                    "Weitersprechen verwirft die ungehörte Antwort nicht");
                rig.Ai.Sink.Write(Long(400)); rig.Frames(Voice(), 5);
                Check(rig.Output.Plays == 0, "Verworfene Antwort wurde abgespielt");
                rig.Emit(LiveKind.SpeechStopped); rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 1, "Neue Antwort darf erst nach Ende der alten angefordert werden");
                rig.Ai.Done(false); rig.Tick();
                Check(rig.Ai.Responds == 2 && rig.Session.State == Phase.Thinking, "Vorgemerkter Beitrag wird nicht beantwortet");
                rig.Ai.Sink.Write(Long(300)); rig.Tick();
                Check(rig.Session.State == Phase.Speaking && rig.Session.Metrics.Failures == 0, "Antwort auf beide Beiträge fehlt");
            }
        }

        private static void TestRequeueLocal()
        {
            using (var rig = new Rig(new SessionOptions { ServerTurns = false, RoutingVerified = true }, false))
            {
                rig.Frames(Voice(), 10); rig.Frames(Silence(), 30);
                Check(rig.Ai.Responds == 1, "Erste Antwort fehlt");
                rig.Frames(Voice(), 20);
                Check(rig.Ai.Cancels.Count == 1 && rig.Ai.Cancels[0] == -1 && rig.Session.State == Phase.Recording, "Lokales Requeue fehlt");
                rig.Frames(Silence(), 30);
                Check(rig.Ai.Commits == 2 && rig.Ai.Responds == 1, "Fortsetzung nicht gesendet oder zu früh beantwortet");
                rig.Ai.Done(false); rig.Tick();
                Check(rig.Ai.Responds == 2, "Fortsetzung wird nicht beantwortet");
            }
        }

        private static Rig Speaking(SessionOptions options, bool server, byte[] answer)
        {
            var rig = new Rig(options, server);
            rig.Settle();
            rig.Emit(LiveKind.SpeechStarted); rig.Frames(Voice(), 5); rig.Emit(LiveKind.SpeechStopped); rig.Emit(LiveKind.Committed); rig.Tick();
            rig.Ai.Sink.Write(answer); rig.Tick();
            Check(rig.Session.State == Phase.Speaking, "Vorbereitung: NOVA spricht nicht");
            rig.Output.Stopped = false;
            return rig;
        }

        private static void TestBargeStop()
        {
            using (var rig = Speaking(new SessionOptions { RoutingVerified = true, BargeIn = BargeIn.Stop, BargeInMs = 500 }, true, Long(3000)))
            {
                rig.Output.Played = 700;
                long before = rig.Ai.Appended;
                rig.Frames(Voice(), 12);
                Check(rig.Session.State == Phase.Speaking && rig.Ai.Appended > before, "Vollduplex: Gast wird während NOVAs Rede nicht gehört");
                rig.Frames(Voice(), 1);
                Check(rig.Output.Stopped && rig.Ai.Cancels.Contains(700) && rig.Session.State == Phase.Recording && rig.Session.Metrics.BargeIns == 1,
                    "Unterbrechung stoppt NOVA nicht oder kürzt den Kontext nicht");
                rig.Ai.Sink.Write(Long(500)); rig.Tick();
                Check(rig.Output.AppendedMs == 3000, "Rest der unterbrochenen Antwort wird abgespielt");
                rig.Frames(Silence(), 70);
                Check(rig.Session.State == Phase.Listening, "Ohne bestätigte Sprache bleibt NOVA im Zustand Gesprächspartner spricht hängen");
                rig.Ai.Done(false); rig.Emit(LiveKind.SpeechStopped); rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 2, "Nach der Unterbrechung antwortet NOVA nicht auf den Gast");
            }
        }

        private static void TestBargeFinish()
        {
            using (var rig = Speaking(new SessionOptions { RoutingVerified = true, BargeIn = BargeIn.Finish }, true, Long(2000)))
            {
                rig.Frames(Voice(), 15);
                Check(rig.Session.State == Phase.Speaking && !rig.Output.Stopped && rig.Session.Metrics.BargeIns == 1, "NOVA soll ihren Satz beenden: " + rig.Session.State + " " + rig.Output.Stopped + " " + rig.Session.Metrics.BargeIns);
                rig.Emit(LiveKind.SpeechStopped); rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 1 && rig.Session.State == Phase.Speaking, "Beitrag während NOVAs Rede nicht vorgemerkt");
                rig.Ai.Done(true); rig.Output.Done = true; rig.Tick();
                Check(rig.Ai.Responds == 2 && rig.Session.State == Phase.Thinking, "Vorgemerkter Beitrag wird nach NOVAs Satz nicht beantwortet");
            }
        }

        private static void TestNoBargeBeforeWarm()
        {
            using (var rig = Speaking(new SessionOptions { BargeIn = BargeIn.Stop }, true, Long(6000)))
            {
                long before = rig.Ai.Appended;
                rig.Frames(Voice(), 15);
                Check(rig.Session.State == Phase.Speaking && rig.Ai.Appended == before, "Ohne Echoprüfung darf der Anfang von NOVAs Rede nicht durchgelassen werden");
                rig.Frames(Silence(), 35);
                rig.Frames(Voice(), 13);
                Check(rig.Session.State == Phase.Recording && rig.Session.Metrics.BargeIns == 1, "Nach dem Aufwärmen wird der Gast nicht gehört");
            }
        }

        private static void TestEchoBlocked()
        {
            byte[] answer = DebateSession.TestSignal(6000);
            using (var rig = Speaking(new SessionOptions { BargeIn = BargeIn.Stop }, true, answer))
            {
                long start = rig.Time;
                for (int i = 0; i < 110; i++)
                {
                    int at = (int)(rig.Time - start) - 200;
                    rig.Frames(Slice(answer, at, 40, 0.3), 1);
                }
                Check(rig.Session.State == Phase.Speaking && rig.Session.Metrics.BargeIns == 0, "NOVA unterbricht sich durch ihr eigenes Echo");
                Check(rig.Session.Metrics.EchoFrames > 40, "Echo wurde nicht erkannt: " + rig.Session.Metrics.EchoFrames);
            }
        }

        private static void TestSelfTalk()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                rig.Ai.Events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "Nova", Text = "Ein späterer Schulbeginn hilft nur, wenn Jugendliche abends früher schlafen." });
                rig.Ai.Events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "Gast", Text = "ein späterer Schulbeginn hilft nur wenn Jugendliche abends früher schlafen" });
                rig.Tick();
                Check(rig.Session.Metrics.EchoLoops == 1 && rig.Session.ActiveBargeIn == BargeIn.Ignore && rig.Session.State == Phase.Cooldown,
                    "Selbstgespräch nicht erkannt");
                Check(DebateSession.Similarity("Ich finde Schule sollte später beginnen", "Hunde sind treue Tiere und brauchen Auslauf") < 0.2, "Ähnlichkeit falsch");
            }
        }

        private static void TestProactive()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true, ProactiveSeconds = 10 }, true))
            {
                rig.Time += 20000; rig.Tick();
                Check(rig.Ai.Responds == 0, "Ohne Debatte keine proaktive Nachfrage");
                rig.Cycle(null, null);
                rig.Time += 8000; rig.Tick();
                Check(rig.Ai.Responds == 1, "Nachfrage zu früh");
                rig.Time += 1100; rig.Tick();
                Check(rig.Ai.Responds == 2 && rig.Ai.LastExtra == DebateSession.ProactiveText, "Proaktive Nachfrage fehlt");
                rig.Ai.Sink.Write(Long(300)); rig.Tick(); rig.Ai.Done(true); rig.Output.Done = true; rig.Tick(); rig.Settle();
                rig.Time += 30000; rig.Tick();
                Check(rig.Ai.Responds == 2, "NOVA darf nicht endlos nachfragen");
            }
        }

        private static void TestMute()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                rig.Session.Muted = true;
                rig.Emit(LiveKind.SpeechStarted); rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 0 && rig.Session.State == Phase.Listening && !rig.Session.CanPush, "Stummgeschaltete NOVA antwortet");
                rig.Session.Muted = false;
                rig.Emit(LiveKind.SpeechStarted); rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 1, "Nach dem Stummschalten antwortet NOVA nicht");
            }
        }

        private static void TestMemory()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                for (int i = 0; i < 4; i++) rig.Cycle("Argument " + i, "Gegenargument " + i);
                rig.Tick();
                Check(rig.Ai.Summaries == 1 && rig.Ai.Material.Contains("Gast: Argument 3") && rig.Ai.Material.Contains("Nova: Gegenargument 0"),
                    "Debattenprotokoll wird nicht angefordert");
                rig.Ai.Events.Enqueue(new LiveEvent(LiveKind.Memory) { Text = "Thema: Schulbeginn" }); rig.Tick();
                Check(rig.Ai.Remembered == "Thema: Schulbeginn" && rig.Session.Memory == "Thema: Schulbeginn", "Protokoll wird nicht übernommen");
                rig.Tick();
                Check(rig.Ai.Summaries == 1, "Protokoll zu oft angefordert");
            }
        }

        private static void TestReconnect()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                rig.Next = new FakeDebater { Server = true };
                rig.Ai.Events.Enqueue(new LiveEvent(LiveKind.Line) { Speaker = "Gast", Text = "Tempo 130 rettet Leben" });
                rig.Emit(LiveKind.Closed); rig.Tick();
                Check(rig.Created == 2 && rig.Ai.Closed && !rig.Next.DisclosureRequested && rig.Session.State == Phase.Listening &&
                    rig.Session.Metrics.Reconnects == 1, "Neue Sitzung nach Verbindungsende fehlt");
                Check(rig.Session.MemoryForReconnect.Contains("Tempo 130"), "Gesprächsstand geht beim Neuaufbau verloren");
                rig.Next.Events.Enqueue(new LiveEvent(LiveKind.SpeechStarted)); rig.Next.Events.Enqueue(new LiveEvent(LiveKind.Committed)); rig.Tick();
                Check(rig.Next.Responds == 1, "Nach dem Neuaufbau antwortet NOVA nicht");
            }
        }

        private static void TestFailures()
        {
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                rig.Emit(LiveKind.Committed); rig.Tick();
                rig.Time += DebateSession.AnswerTimeoutMs + 1; rig.Tick();
                Check(rig.Ai.Cancels.Count == 1 && rig.Session.State == Phase.Listening, "Zeitlimit ohne Antwort greift nicht");
                rig.Ai.Done(false); rig.Tick();
                for (int i = 0; i < 2; i++)
                {
                    rig.Emit(LiveKind.Committed); rig.Tick();
                    rig.Ai.Done(false); rig.Tick();
                    if (i == 0) Check(rig.Session.State == Phase.Listening && rig.Session.Note.Contains("nicht antworten"), "Einzelner Fehler beendet die Debatte");
                }
                Check(rig.Session.State == Phase.Idle && rig.Session.Note.Contains("wiederholt"), "Wiederholte Fehler werden nicht gemeldet");
            }
        }

        private static void TestInterrupt()
        {
            using (var rig = Speaking(new SessionOptions { RoutingVerified = true }, true, Long(2000)))
            {
                rig.Output.Played = 420;
                rig.Session.Interrupt();
                Check(rig.Output.Stopped && rig.Ai.Cancels.Count == 1 && rig.Ai.Cancels[0] == 420 && rig.Session.State == Phase.Cooldown &&
                    rig.Session.Metrics.Interrupts == 1, "Unterbrechen stoppt oder kürzt nicht");
                rig.Ai.Sink.Write(Long(200)); rig.Tick();
                Check(rig.Output.AppendedMs == 2000, "Rest der Antwort läuft weiter");
                rig.Settle();
                Check(rig.Session.State == Phase.Listening, "Nach dem Unterbrechen hört NOVA nicht wieder zu");
            }
        }

        private static void TestPush()
        {
            using (var rig = new Rig(new SessionOptions { ServerTurns = false, RoutingVerified = true }, false))
            {
                rig.Frames(Voice(), 20);
                Check(rig.Session.CanPush && rig.Session.State == Phase.Recording, "Jetzt antworten fehlt während des Beitrags");
                Wait(rig.Session.Push());
                Check(rig.Ai.Commits == 1 && rig.Ai.Responds == 1 && rig.Ai.LastExtra == null, "Jetzt antworten sendet nicht");
            }
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true }, true))
            {
                Wait(rig.Session.Push());
                Check(rig.Ai.Responds == 1 && rig.Ai.LastExtra == DebateSession.TakeFloorText, "NOVA soll sprechen fehlt");
                rig.Ai.Done(true); rig.Ai.Sink.Write(Long(200)); rig.Tick(); rig.Output.Done = true; rig.Tick();
                rig.Emit(LiveKind.SpeechStarted); rig.Tick();
                Wait(rig.Session.Push());
                Check(rig.Ai.Commits == 1 && rig.Ai.Responds == 2, "Jetzt antworten mit OpenAI-Turns sendet nicht");
                rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 2, "Doppelte Antwort nach manuellem Senden");
            }
        }

        private static void TestPushToTalk()
        {
            using (var rig = new Rig(new SessionOptions { Automatic = false, RoutingVerified = true }, true))
            {
                rig.Settle();
                rig.Frames(Voice(), 10);
                Check(rig.Ai.Appended == 0 && rig.Session.CanPush, "Push-to-talk lädt ohne Klick hoch");
                Wait(rig.Session.Push());
                rig.Frames(Voice(), 6);
                Wait(rig.Session.Push());
                Check(rig.Ai.Commits == 1 && rig.Ai.Responds == 1 && rig.Ai.Appended > 0, "Manuelles Senden fehlt");
                rig.Session.Stop(true);
                Check(rig.Ai.Closed && rig.Session.State == Phase.Paused, "Pause bricht nicht ab");
                AudioSink late = rig.Ai.Sink;
                var fresh = new FakeDebater();
                Wait(rig.Session.Start(() => new FakeCapture(), () => rig.Output, () => fresh, new SessionOptions { Automatic = false }));
                late.Write(Long(300)); rig.Tick();
                Check(rig.Output.AppendedMs == 0 && rig.Session.State == Phase.Cooldown, "Verspätete Antwort überschreibt neue Sitzung");
            }
        }

        private static void TestModerationAndRetry()
        {
            using (var rig = Speaking(new SessionOptions { RoutingVerified = true }, true, Long(1000)))
            {
                rig.Session.Moderate();
                Check(rig.Session.State == Phase.Moderated && rig.Output.Closed && rig.Ai.Closed && !rig.Session.CanPush, "Ruhe stoppt nicht");
                rig.Session.Stop(false);
                Check(rig.Session.State == Phase.Idle, "Not-Aus aus Moderation fehlt");
            }
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var failed = new FakePlayback();
                Wait(session.Start(() => { throw new AppError("Eingang fehlt"); }, () => failed, () => new FakeDebater(), new SessionOptions()));
                Check(failed.Closed && session.State == Phase.Idle && session.Note == "Eingang fehlt", "Fehler lässt Ausgang geöffnet");
                var broken = new FakeDebater { StartError = new OperationCanceledException() };
                var input = new FakeCapture();
                Wait(session.Start(() => input, () => new FakePlayback(), () => broken, new SessionOptions()));
                Check(input.Closed && broken.Closed && session.Note.Contains("nicht rechtzeitig"), "Timeout ohne Aufräumen");
                var intro = new FakeDebater { Intro = Long(500) };
                var output = new FakePlayback();
                Wait(session.Start(() => new FakeCapture(), () => output, () => intro, new SessionOptions()));
                Check(session.State == Phase.Speaking && output.Plays == 1 && intro.DisclosureRequested, "KI-Hinweis wird nach Neustart nicht gesprochen");
                session.Stop(false);
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

        private static bool? EchoCheck(Func<long, byte[]> input)
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var capture = new FakeCapture();
                session.CheckEcho(() => capture, () => new FakePlayback());
                Check(session.State == Phase.EchoTest, "Echoprüfung startet nicht");
                while (session.State == Phase.EchoTest && time < 6000)
                {
                    capture.Add(input(time), 1);
                    time += 40;
                    Wait(session.Tick());
                }
                Check(session.State == Phase.Idle, "Echoprüfung endet nicht");
                return session.EchoCheckPassed;
            }
        }

        private static void TestEchoCheck()
        {
            byte[] signal = DebateSession.TestSignal(4000);
            Check(EchoCheck(t => Slice(signal, (int)t - 160, 40, 0.4)) == false, "Echo nicht erkannt");
            Check(EchoCheck(t => Silence()) == true, "Getrenntes Routing nicht bestätigt");
            Check(EchoCheck(t => Voice()) == null, "Lauter Eingang muss als unklar gelten");
        }

        private static void TestEchoGuard()
        {
            byte[] nova = DebateSession.TestSignal(6000);
            var guestRandom = new Random(99);
            byte[] guest = WinAudio.Tone(6000, 200);
            for (int i = 0; i < guest.Length; i += 2)
            {
                double envelope = ((i / (WinAudio.BytesPerSecond / 5)) % 3 == 0) ? 0.05 : 1;
                WinAudio.WriteSample(guest, i, (short)(WinAudio.ReadSample(guest, i) * envelope * (0.6 + 0.4 * guestRandom.NextDouble())));
            }
            int echoHits = 0, guestHits = 0, checks = 0;
            var echoGuard = new EchoGuard(); var guestGuard = new EchoGuard();
            for (int t = 0; t < 5000; t += 40)
            {
                echoGuard.AddReference(Slice(nova, t, 40, 1), t);
                guestGuard.AddReference(Slice(nova, t, 40, 1), t);
                bool echo = echoGuard.IsEcho(Slice(nova, t - 300, 40, 0.2), t + 40);
                bool other = guestGuard.IsEcho(Slice(guest, t, 40, 1), t + 40);
                if (t < 1800) continue;
                checks++;
                if (echo) echoHits++;
                if (other) guestHits++;
            }
            Check(echoHits >= checks * 8 / 10, "Echo zu selten erkannt: " + echoHits + "/" + checks);
            Check(guestHits <= checks / 20, "Gast fälschlich als Echo erkannt: " + guestHits + "/" + checks);
        }

        private static void TestMetrics()
        {
            var metrics = new LiveMetrics();
            metrics.Latency.AddRange(new[] { 800, 1200, 1000 });
            metrics.Answers = 3;
            Check(LiveMetrics.Average(metrics.Latency) == 1000 && LiveMetrics.Percentile(metrics.Latency, 95) == 1200 && LiveMetrics.Percentile(metrics.Latency, 50) == 1000,
                "Statistik falsch");
            string report = metrics.Report("0.3.0", "Test");
            Check(report.Contains("1000 / 1000 / 1200 / 1200 ms (n=3)") && report.Contains("| 5 | Mindestens zehn") && report.Contains("gemessen: 3"),
                "Testprotokoll unvollständig");
            Check(metrics.Short().Contains("zuletzt 1000 ms"), "Kurzanzeige falsch");
        }

        private static void TestPersona()
        {
            var persona = new Persona { Name = "Nova", Style = 1, Intensity = 2, Length = 0, Topic = "Tempolimit", Position = "Dafür\u0007" };
            string text = persona.Instructions("Gast hält Tempolimit für Bevormundung.");
            Check(text.Contains("kritisch und herausfordernd") && text.Contains("leidenschaftlich") && text.Contains("höchstens 2 Sätze") &&
                text.Contains("Tempolimit") && text.Contains("Dafür") && !text.Contains("\u0007"), "Persona-Anweisungen unvollständig");
            Check(text.Contains("zentrale Behauptung") && text.Contains("Widerspruch") && text.Contains("Position nachvollziehbar an") &&
                text.Contains("Provokationen") && text.Contains("Floskeln"), "Debattenmethode fehlt");
            Check(text.Contains("DEBATTENPROTOKOLL") && text.Contains("Bevormundung") && text.IndexOf("DEBATTENPROTOKOLL") < text.IndexOf("REGELN"),
                "Gedächtnis fehlt oder steht nach den Regeln");
            Check(text.EndsWith("personenbezogene Details wiederholen.", StringComparison.Ordinal), "Sicherheitsregeln müssen am Ende stehen");
            Check(!persona.Instructions().Contains("DEBATTENPROTOKOLL"), "Leeres Gedächtnis erzeugt Abschnitt");
            Check(persona.IsDisclosure("Hallo, ich bin Nova, eine künstliche Intelligenz. Meine Stimme wird von KI erzeugt."), "Wortlaut nicht erkannt");
            Check(persona.IsDisclosure("Hallo! Ich bin Nova – eine KI."), "Sinngemäßer Hinweis nicht erkannt");
            Check(!persona.IsDisclosure("Ich bin Nova."), "Hinweis ohne KI akzeptiert");
            Check(!persona.IsDisclosure("Ich bin eine künstliche Intelligenz."), "Hinweis ohne Namen akzeptiert");
            Check(new Persona { Language = 1 }.Instructions().Contains("Englisch") && new Persona { Language = 1 }.LanguageCode == "en", "Sprache fehlt");
            Check(new Persona { Name = "   " }.Disclosure().Contains("Nova") && new Persona { Length = 9 }.MaxOutputTokens == 3200, "Grenzwerte falsch");
        }

        private static readonly string[] BananaInputs = { "Mikrofon (Realtek)", "Voicemeeter Out B1 (VB-Audio Voicemeeter VAIO)", "Voicemeeter Out B2 (VB-Audio Voicemeeter VAIO)" };
        private static readonly string[] BananaOutputs = { "Lautsprecher (Realtek)", "Kopfhörer (USB Headset)", "Voicemeeter Input (VB-Audio Voicemeeter VAIO)", "Voicemeeter AUX Input (VB-Audio Voicemeeter AUX VAIO)" };

        private static void TestSetupPick()
        {
            ScenarioPreset tiktok = Setup.Presets[0], local = Setup.Presets[3];
            Check(Setup.Pick(BananaInputs, tiktok.Inputs, null) == 2 && Setup.Pick(BananaOutputs, tiktok.Outputs, tiktok.AvoidOutputs) == 3,
                "TikTok: B2 als Eingang und AUX als Ausgang erwartet");
            Check(Setup.Pick(BananaInputs, local.Inputs, null) == 0 && Setup.Pick(BananaOutputs, local.Outputs, local.AvoidOutputs) == 1,
                "Lokal: Mikrofon und Kopfhörer erwartet");
            Check(Setup.Pick(new[] { "Voicemeeter Input (VAIO)" }, local.Outputs, local.AvoidOutputs) == -1, "Lokal darf kein virtuelles Gerät wählen");
            Check(Setup.Pick(new string[0], tiktok.Inputs, null) == -1, "Leere Geräteliste");
            Check(Setup.Pick(new[] { "Voicemeeter Out B1" }, tiktok.Inputs, null) == 0, "Standard-Voicemeeter: B1 als Rückfall");
            foreach (ScenarioPreset preset in Setup.Presets)
                Check(preset.Mode >= 0 && preset.Mode < 3 && preset.Patience < 3 && preset.BargeIn < 3 && preset.Length < Persona.Lengths.Length &&
                    preset.Summary.Length > 20 && preset.Routing.Length > 20, "Preset unvollständig: " + preset.Name);
        }

        private static void TestSetupCheck()
        {
            Check(Setup.Check("Voicemeeter Out B2", "Voicemeeter AUX Input").Count == 0, "Korrektes Banana-Routing wird bemängelt");
            Check(Setup.Check("Mikrofon", "Kopfhörer").Count == 0, "Kopfhörer werden bemängelt");
            Check(Setup.Check("Voicemeeter Out B2", "Voicemeeter Input (VAIO)").Count == 1, "Haupteingang als NOVA-Ausgang nicht gewarnt");
            Check(Setup.Check("Voicemeeter Out B1", "Voicemeeter AUX Input").Count == 1, "B1 als Gast-Eingang nicht gewarnt");
            Check(Setup.Check("CABLE Output", "CABLE Input").Count == 1, "Gleiches virtuelles Kabel nicht gewarnt");
            Check(Setup.Check("Mikrofon", "Lautsprecher (Realtek)").Count == 1 && Setup.Speakers("Speakers (USB)"), "Lautsprecher nicht gewarnt");
            Check(Setup.Check("", "").Count == 2, "Fehlende Geräte nicht gemeldet");
        }

        private static void TestProfiles()
        {
            var original = new Persona { Name = "Kira", Style = 3, Intensity = 2, Length = 2, Voice = "cedar", Language = 1, Topic = "Tempolimit", Position = "Dagegen" };
            var json = new JavaScriptSerializer();
            var stored = json.Deserialize<Dictionary<string, object>>(json.Serialize(new Dictionary<string, object> { { "Kira", original.ToValues() } }));
            Persona loaded = Persona.FromValues(stored["Kira"] as Dictionary<string, object>);
            Check(loaded.Name == "Kira" && loaded.Style == 3 && loaded.Intensity == 2 && loaded.Length == 2 && loaded.Voice == "cedar" &&
                loaded.Language == 1 && loaded.Topic == "Tempolimit" && loaded.Position == "Dagegen", "Profil-Rundlauf fehlerhaft");
            var broken = new Dictionary<string, object> { { "style", 99 }, { "voice", "unbekannt" }, { "language", -3 } };
            Persona safe = Persona.FromValues(broken);
            Check(safe.Style == Persona.Styles.Length - 1 && safe.Voice == "marin" && safe.Language == 0 && safe.Name == "Nova", "Ungültige Profilwerte nicht abgefangen");
            Check(Persona.FromValues(null).Name == "Nova", "Leeres Profil");
        }

        private static void TestVoiceTest()
        {
            long time = 0;
            using (var session = new DebateSession(() => time))
            {
                var output = new FakePlayback();
                var voice = new FakeDebater { Intro = Long(800) };
                Wait(session.VoiceTest(() => output, () => voice));
                Check(session.State == Phase.Tone && output.Plays == 1 && voice.DisclosureRequested && voice.Closed, "Stimmprobe fehlt oder Sitzung bleibt offen");
                output.Done = true; Wait(session.Tick());
                Check(session.State == Phase.Idle && output.Closed, "Stimmprobe endet nicht");
                var failing = new FakeDebater { StartError = new AppError("API-Schlüssel abgelehnt.") };
                Wait(session.VoiceTest(() => new FakePlayback(), () => failing));
                Check(session.State == Phase.Idle && session.Note.Contains("abgelehnt") && failing.Closed, "Fehler bei der Stimmprobe nicht gemeldet");
            }
        }

        private sealed class FakeVoicemeeter : IVoicemeeter
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            public readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
            public readonly HashSet<string> Ignore = new HashSet<string>();
            public int Kind { get { return 2; } }
            public bool Running { get { return true; } }
            public float Get(string parameter) { float value; return Values.TryGetValue(parameter, out value) ? value : 0; }
            public void Set(string parameter, float value) { if (!Ignore.Contains(parameter)) Values[parameter] = value; }
            public void SetText(string parameter, string value) { Texts[parameter] = value; }
            public float Level(int type, int channel) { return type == 0 && channel == 6 ? 0.1f : 0; }
            public List<string> InputDevices() { return new List<string> { "Mikrofonarray (Intel® Smart Sound Technologie für digitale Mikrofone)" }; }
            public List<string> OutputDevices() { return new List<string> { "Lautsprecher (Realtek(R) Audio)", "Kopfhörer (Borofone B035)" }; }
            public void Dispose() { }
        }

        private static void TestVoicemeeterPlan()
        {
            Check(VoicemeeterLayout.For(1) == null && VoicemeeterLayout.For(0) == null, "Standard-Voicemeeter darf nicht als getrennt gelten");
            VoicemeeterLayout banana = VoicemeeterLayout.For(2), potato = VoicemeeterLayout.For(3);
            Check(banana.Guest == 3 && banana.Nova == 4 && banana.GuestLevel[0] == 6 && banana.B1Level[0] == 24 && banana.B2Level[0] == 32, "Banana-Indizes falsch");
            Check(potato.Guest == 5 && potato.Nova == 6, "Potato-Indizes falsch");
            var vm = new FakeVoicemeeter();
            string phones = VoicemeeterSetup.Pick(vm.OutputDevices(), "Borofone", "Kopfhörer");
            string mic = VoicemeeterSetup.Pick(vm.InputDevices(), "Intel", "Mikrofon");
            Check(phones == "Kopfhörer (Borofone B035)" && mic.StartsWith("Mikrofonarray"), "Gerätevorschlag falsch");
            List<RoutingStep> plan = banana.Plan(phones, mic, true, false);
            VoicemeeterSetup.Apply(vm, plan);
            Check(vm.Get("Strip[3].A1") == 1 && vm.Get("Strip[3].B2") == 1 && vm.Get("Strip[3].B1") == 0 &&
                vm.Get("Strip[4].B1") == 1 && vm.Get("Strip[4].B2") == 0 && vm.Get("Strip[4].A1") == 1 &&
                vm.Get("Strip[0].B1") == 1 && vm.Get("Strip[0].B2") == 0 && vm.Get("Strip[0].A1") == 0, "Routing-Plan entspricht nicht der Vorgabe");
            Check(vm.Texts["Bus[0].device.wdm"] == phones && vm.Texts["Strip[0].device.wdm"] == mic, "A1 oder Mikrofon nicht gesetzt");
            Check(VoicemeeterSetup.Verify(vm, plan).Count == 0, "Rücklesen meldet Fehler");
            vm.Values["Strip[4].B2"] = 1;
            List<string> wrong = VoicemeeterSetup.Verify(vm, plan);
            Check(wrong.Count == 1 && wrong[0].Contains("NOVA nicht auf B2"), "Falscher Schalter wird nicht erkannt");
            Check(banana.Plan(null, null, false, true).Exists(step => step.Parameter == "Strip[0].B2" && step.Value == 1) &&
                !banana.Plan(null, null, false, true).Exists(step => step.IsText), "Optionen falsch umgesetzt");
            Check(VoicemeeterSetup.Decibel(0) == -90 && Math.Abs(VoicemeeterSetup.Decibel(0.1f) + 20) < 0.01 &&
                Math.Abs(VoicemeeterSetup.Loudest(vm, 0, banana.GuestLevel) + 20) < 0.01, "Pegelumrechnung falsch");
        }

        // Simulated Voicemeeter: which playback device reaches which capture bus, with delay and attenuation.
        private sealed class FakeMixer
        {
            public long Time;
            public readonly HashSet<string> Routes = new HashSet<string>();
            public string Playing;
            public byte[] Signal;
            public long Started;

            public IPlayback Output(string name) { return new MixPlayback(this, name); }
            public ICapture Input(string name) { return new MixCapture(this, name); }

            private sealed class MixPlayback : IPlayback
            {
                private readonly FakeMixer mixer; private readonly string name;
                public MixPlayback(FakeMixer mixer, string name) { this.mixer = mixer; this.name = name; }
                public void Play(byte[] pcm) { mixer.Playing = name; mixer.Signal = pcm; mixer.Started = mixer.Time; }
                public void Append(byte[] pcm) { }
                public void Stop() { mixer.Playing = null; }
                public bool Finished { get { return true; } }
                public int PlayedMs { get { return 0; } }
                public void Dispose() { mixer.Playing = null; }
            }

            private sealed class MixCapture : ICapture
            {
                private readonly FakeMixer mixer; private readonly string name;
                public MixCapture(FakeMixer mixer, string name) { this.mixer = mixer; this.name = name; }
                public void Drain(Action<byte[]> onFrame)
                {
                    if (onFrame == null) return;
                    bool routed = mixer.Playing != null && mixer.Routes.Contains(mixer.Playing + ">" + name);
                    onFrame(routed ? Slice(mixer.Signal, (int)(mixer.Time - mixer.Started) - 140, 40, 0.5) : Silence());
                }
                public void Dispose() { }
            }
        }

        private static List<RouteResult> MeasureMix(FakeMixer mixer, string source, params string[] listeners)
        {
            var list = new List<Listener>();
            foreach (string listener in listeners)
            {
                string name = listener.TrimEnd('!');
                list.Add(new Listener { Name = name, Open = () => mixer.Input(name), Expected = listener.EndsWith("!") });
            }
            return RoutingMeasurement.Measure(source, () => mixer.Output(source), list, () => mixer.Time,
                ms => { mixer.Time += ms; return Task.FromResult(0); }).GetAwaiter().GetResult();
        }

        private static void TestRoutingMeasurement()
        {
            var mixer = new FakeMixer();
            mixer.Routes.Add("VAIO>B2"); mixer.Routes.Add("AUX>B1");
            List<RouteResult> guest = MeasureMix(mixer, "VAIO", "B2!", "B1");
            List<RouteResult> nova = MeasureMix(mixer, "AUX", "B1!", "B2");
            Check(guest.TrueForAll(r => r.Ok) && nova.TrueForAll(r => r.Ok) && guest[0].Arrived && !guest[1].Arrived && nova[0].Arrived && !nova[1].Arrived,
                "Korrektes Routing nicht bestätigt: " + guest[0].Describe() + " / " + guest[1].Describe() + " / " + nova[0].Describe() + " / " + nova[1].Describe());
            Check(guest[0].Describe().StartsWith("✓") && guest[0].Peak > -40, "Ergebnistext oder Pegel falsch");
            mixer.Routes.Add("AUX>B2");
            List<RouteResult> loop = MeasureMix(mixer, "AUX", "B1!", "B2");
            Check(loop[0].Ok && !loop[1].Ok && loop[1].Arrived && loop[1].Describe().StartsWith("✗"), "Rückweg NOVA → B2 nicht erkannt");
            mixer.Routes.Clear();
            Check(!MeasureMix(mixer, "VAIO", "B2!")[0].Ok, "Fehlender Gast-Weg nicht erkannt");
        }

        private static void TestGuestProbe()
        {
            var probe = new GuestProbe();
            for (int i = 0; i < 10; i++) probe.Add(-30, -32, 100);
            Check(!probe.VaioOk && probe.Describe().StartsWith("✗"), "Zu kurzer Gastton gilt als bestanden");
            for (int i = 0; i < 10; i++) probe.Add(-30, -32, 100);
            Check(probe.VaioOk && probe.InputOk && probe.Describe().StartsWith("✓"), "Gastton nicht bestätigt");
            var noB2 = new GuestProbe();
            for (int i = 0; i < 30; i++) noB2.Add(-25, -90, 100);
            Check(noB2.VaioOk && !noB2.InputOk && noB2.Describe().Contains("B2 nicht"), "Fehlendes B2 nicht erkannt");
            var silent = new GuestProbe();
            silent.Add(-90, -90, 15000);
            Check(silent.Describe().Contains("Kein Gastton"), "Stille nicht erkannt");
            Check(LiveStudio.IsLiveStudio("TikTok LIVE Studio") && LiveStudio.IsLiveStudio("TikTokLiveStudio") && !LiveStudio.IsLiveStudio("TikTok") &&
                !LiveStudio.IsLiveStudio("obs64"), "LIVE-Studio-Erkennung falsch");
        }

        private static void TestCostLimit()
        {
            var usage = new TokenUsage { AudioIn = 100000, AudioCached = 50000, AudioOut = 10000, TextIn = 20000, TextOut = 1000 };
            double expected = (50000 * 10.0 + 50000 * 0.30 + 10000 * 20.0 + 20000 * 0.60 + 1000 * 2.40) / 1000000.0;
            Check(Math.Abs(usage.Cost - expected) < 1e-9, "Kostenformel falsch");
            using (var rig = new Rig(new SessionOptions { RoutingVerified = true, CostLimit = 1.0 }, true))
            {
                rig.Emit(LiveKind.Committed); rig.Tick();
                rig.Ai.Sink.Write(Long(300)); rig.Tick();
                rig.Ai.Events.Enqueue(new LiveEvent(LiveKind.AnswerDone) { Completed = true, Tokens = new TokenUsage { AudioIn = 30000, AudioOut = 20000 } });
                rig.Output.Done = true; rig.Tick();
                Check(Math.Abs(rig.Session.Metrics.Cost - 0.7) < 1e-9 && !rig.Session.Muted && !rig.Session.CostLimitReached, "Kosten falsch gezählt");
                rig.Ai.Events.Enqueue(new LiveEvent(LiveKind.Usage) { Tokens = new TokenUsage { AudioOut = 20000 } }); rig.Tick();
                Check(rig.Session.CostLimitReached && rig.Session.Muted && rig.Session.Note.Contains("Kostenlimit"), "Kostenlimit schaltet NOVA nicht stumm");
                rig.Emit(LiveKind.Committed); rig.Tick();
                Check(rig.Ai.Responds == 1, "Nach dem Kostenlimit antwortet NOVA weiter");
                Check(rig.Session.Metrics.Report("0.5.0", "x").Contains("Geschätzte OpenAI-Kosten"), "Kosten fehlen im Testbericht");
                Check(rig.Session.Fairness, "Fairness sollte an sein");
                rig.Session.Fairness = false;
                Check(!rig.Session.Fairness, "Gast ausreden lassen lässt sich nicht umschalten");
            }
        }

        private sealed class FakeStt : ISpeechToText
        {
            public readonly Queue<string> Texts = new Queue<string>();
            public TaskCompletionSource<bool> Gate;
            public int Calls;
            public async Task<string> Transcribe(byte[] pcm, CancellationToken token)
            {
                Calls++;
                if (Gate != null) await Gate.Task;
                token.ThrowIfCancellationRequested();
                return Texts.Count == 0 ? "" : Texts.Dequeue();
            }
        }

        private sealed class FakeLlm : IChatModel
        {
            public readonly Queue<string> Answers = new Queue<string>();
            public readonly List<List<ChatMessage>> Requests = new List<List<ChatMessage>>();
            public Task Chat(IList<ChatMessage> messages, int maxTokens, Action<string> onDelta, CancellationToken token)
            {
                lock (Requests) Requests.Add(new List<ChatMessage>(messages));
                string answer = Answers.Count == 0 ? "Gut." : Answers.Dequeue();
                foreach (string word in answer.Split(' ')) { token.ThrowIfCancellationRequested(); onDelta(word + " "); }
                return Task.FromResult(0);
            }
            public readonly List<string> Warmed = new List<string>();
            public Task Warm(string system, CancellationToken token) { lock (Warmed) Warmed.Add(system); return Task.FromResult(0); }
        }

        private sealed class FakeTts : ITextToSpeech
        {
            public readonly List<string> Spoken = new List<string>();
            public Task<byte[]> Speak(string text, CancellationToken token)
            {
                lock (Spoken) Spoken.Add(text);
                return Task.FromResult(Long(500));
            }
        }

        private static List<LiveEvent> Drain(IDebater ai, LiveKind until)
        {
            var found = new List<LiveEvent>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 3000)
            {
                LiveEvent item;
                if (ai.TryTake(out item)) { found.Add(item); if (item.Kind == until) return found; }
                else Thread.Sleep(5);
            }
            throw new Exception("Ereignis fehlt: " + until + " nach " + string.Join(",", found));
        }

        private static void TestLocalBasics()
        {
            byte[] tone = WinAudio.Tone(1000, 440);
            byte[] down = Pcm.Resample(tone, 24000, 16000);
            Check(down.Length == 32000 && Pcm.Resample(tone, 24000, 24000) == tone, "Resampling-Länge falsch");
            int rate;
            byte[] back = Pcm.ReadWav(Pcm.Wav(down, 16000), out rate);
            Check(rate == 16000 && back.Length == down.Length && WinAudio.ReadSample(back, 200) == WinAudio.ReadSample(down, 200), "WAV-Rundlauf falsch");
            byte[] up = Pcm.Resample(new byte[22050 * 2], 22050, 24000);
            Check(up.Length == 48000, "Piper-Rate 22050 → 24000 falsch");
            var splitter = new SentenceSplitter();
            var got = new List<string>();
            foreach (string piece in new[] { "Das sehe ", "ich anders. Ein Tempo", "limit rettet Leben! Und ", "was ist mit dir?" }) got.AddRange(splitter.Add(piece));
            got.Add(splitter.Rest());
            Check(got.Count == 3 && got[0] == "Das sehe ich anders." && got[1] == "Ein Tempolimit rettet Leben!" && got[2] == "Und was ist mit dir?", "Satzaufteilung falsch: " + string.Join("|", got));
            var shortOnes = new SentenceSplitter();
            Check(shortOnes.Add("Ja. Nein. ").Count == 0, "Zu kurze Satzstücke dürfen nicht einzeln gesprochen werden");
            var longOne = new SentenceSplitter();
            Check(longOne.Add(new string('a', 100) + ", " + new string('b', 100)).Count == 1, "Lange Sätze ohne Punkt werden nicht geteilt");
            Check(WhisperServer.Clean(" [Musik] ") == "" && WhisperServer.Clean("(Applaus)") == "" && WhisperServer.Clean(" Hallo Welt.\n") == "Hallo Welt.", "Whisper-Bereinigung falsch");
        }

        private static void TestLocalGuard()
        {
            Check(LocalGuard.Loopback("http://127.0.0.1:11434/api/chat").IsLoopback && LocalGuard.Loopback("http://localhost:18178/").IsLoopback, "Lokale Adressen abgelehnt");
            Expect<AppError>(() => LocalGuard.Loopback("https://api.openai.com/v1/realtime"), "Sicherheitssperre");
            Expect<AppError>(() => LocalGuard.Loopback("http://192.168.1.20:11434/"), "Sicherheitssperre");
            Check(new Uri(OllamaChat.Url).IsLoopback && new Uri(WhisperServer.Url).IsLoopback, "Lokale Dienste zeigen nicht auf diesen PC");
        }

        private static LocalDebater Local(FakeStt stt, FakeLlm llm, FakeTts tts)
        {
            return new LocalDebater(stt, llm, tts, new Persona { Name = "Nova" }, "");
        }

        private static void TestLocalPipeline()
        {
            var stt = new FakeStt(); var llm = new FakeLlm(); var tts = new FakeTts();
            stt.Texts.Enqueue("Tempo 130 reicht völlig aus.");
            llm.Answers.Enqueue("Das sehe ich anders. Weniger Tempo heißt weniger Unfälle. Was spricht für dich dagegen?");
            using (LocalDebater ai = Local(stt, llm, tts))
            {
                Check(!ai.ServerTurns, "Lokal nutzt die lokale Turn-Erkennung");
                byte[] intro = ai.Start(true, CancellationToken.None).GetAwaiter().GetResult();
                Check(intro.Length > 0 && tts.Spoken[0].Contains("künstliche Intelligenz"), "Lokaler KI-Hinweis fehlt");
                Drain(ai, LiveKind.Line);
                ai.Append(Long(800)); ai.Append(Long(400)); ai.Commit();
                var sink = new AudioSink();
                ai.Respond(sink, null);
                List<LiveEvent> events = Drain(ai, LiveKind.AnswerDone);
                Check(events[0].Kind == LiveKind.Line && events[0].Speaker == "Gast" && events[0].Text == "Tempo 130 reicht völlig aus.", "Gast-Mitschrift fehlt");
                Check(events.Exists(e => e.Kind == LiveKind.Line && e.Speaker == "Nova") && events[events.Count - 1].Completed, "NOVA-Antwort unvollständig");
                Check(tts.Spoken.Count == 4 && tts.Spoken[1] == "Das sehe ich anders." && tts.Spoken[3] == "Was spricht für dich dagegen?", "Nicht satzweise gesprochen: " + string.Join("|", tts.Spoken));
                Check(sink.TotalBytes == 3 * Long(500).Length, "Antwort-Audio fehlt");
                Check(llm.Requests[0][0].Role == "system" && llm.Requests[0][0].Content.Contains("Gegenargument") && llm.Requests[0][0].Content.Length < 1500 && llm.Requests[0][1].Content == "Tempo 130 reicht völlig aus.",
                    "Sprachmodell bekommt Persona oder Beitrag nicht");
                stt.Texts.Enqueue("Freiheit ist wichtiger.");
                ai.Append(Long(600)); ai.Commit();
                ai.Respond(new AudioSink(), "Bringe die Debatte voran.");
                Drain(ai, LiveKind.AnswerDone);
                List<ChatMessage> second = llm.Requests[1];
                Check(second.Count == 4 && second[2].Role == "assistant" && second[2].Content.StartsWith("Das sehe ich anders.") &&
                    second[3].Content == "Freiheit ist wichtiger.\n(Hinweis an dich: Bringe die Debatte voran.)" &&
                    second[0].Content == llm.Requests[0][0].Content, "Gesprächsverlauf oder Zusatzanweisung fehlt");
                Check(llm.Warmed.Count == 1 && llm.Warmed[0] == llm.Requests[0][0].Content, "Lokales Modell wurde nicht mit denselben Anweisungen vorgewärmt");
                Check(LocalDebater.WindowStart(16) == 0 && LocalDebater.WindowStart(19) == 0 && LocalDebater.WindowStart(20) == 12 && LocalDebater.WindowStart(31) == 12 &&
                    LocalDebater.WindowStart(32) == 24 && 32 - LocalDebater.WindowStart(32) >= 8, "Verlaufsfenster springt nicht in Blöcken");
                var stats = new Dictionary<string, object> { { "prompt_eval_count", 300 }, { "prompt_eval_duration", 1500000000L }, { "eval_count", 40 }, { "eval_duration", 5000000000L } };
                Check(OllamaChat.Stats(stats).StartsWith("Prompt 300 Tokens in 1500 ms (200/s) · Antwort 40 Tokens in 5000 ms (8,0/s)") ||
                    OllamaChat.Stats(stats).StartsWith("Prompt 300 Tokens in 1500 ms (200/s) · Antwort 40 Tokens in 5000 ms (8.0/s)"), "Ollama-Statistik: " + OllamaChat.Stats(stats));
            }
        }

        private static void TestLocalCancel()
        {
            var stt = new FakeStt(); var llm = new FakeLlm(); var tts = new FakeTts();
            llm.Answers.Enqueue("Erster Gedanke dazu ist klar. Zweiter Gedanke folgt jetzt hier.");
            using (LocalDebater ai = Local(stt, llm, tts))
            {
                stt.Texts.Enqueue("Argument eins.");
                ai.Append(Long(500)); ai.Commit(); ai.Respond(new AudioSink(), null);
                Drain(ai, LiveKind.AnswerDone);
                ai.Cancel(600);
                List<ChatMessage> history = ai.History;
                Check(history.Count == 2 && history[1].Content == "Erster Gedanke dazu ist klar. (unterbrochen)", "Kürzen auf Gehörtes falsch: " + history[history.Count - 1].Content);
                llm.Answers.Enqueue("Nie gehört.");
                stt.Texts.Enqueue("Argument zwei.");
                ai.Append(Long(500)); ai.Commit(); ai.Respond(new AudioSink(), null);
                Drain(ai, LiveKind.AnswerDone);
                ai.Cancel(-1);
                Check(ai.History.Count == 3 && ai.History[2].Content == "Argument zwei.", "Ungehörte Antwort wird nicht vergessen");
                stt.Gate = new TaskCompletionSource<bool>();
                stt.Texts.Enqueue("Argument drei.");
                ai.Append(Long(500)); ai.Commit(); ai.Respond(new AudioSink(), null);
                Thread.Sleep(50);
                ai.Cancel(-1);
                stt.Gate.SetResult(true);
                List<LiveEvent> cancelled = Drain(ai, LiveKind.AnswerDone);
                Check(!cancelled[cancelled.Count - 1].Completed, "Abbruch nicht gemeldet");
                stt.Gate = null;
                ai.Respond(new AudioSink(), null);
                Drain(ai, LiveKind.AnswerDone);
                Check(ai.History.Exists(m => m.Content == "Argument drei."), "Beitrag geht beim Abbruch während der Spracherkennung verloren");
                ai.Summarize("Gast: Argument eins.");
                Check(Drain(ai, LiveKind.Memory).Count >= 1, "Lokales Gedächtnis fehlt");
            }
        }

        private static void TestLocalSession()
        {
            var stt = new FakeStt(); var llm = new FakeLlm(); var tts = new FakeTts();
            stt.Texts.Enqueue("Schule sollte später beginnen.");
            llm.Answers.Enqueue("Das klingt gut, aber wer betreut dann die Kinder am Morgen?");
            long time = 0;
            var input = new FakeCapture(); var output = new FakePlayback();
            using (var session = new DebateSession(() => time))
            using (LocalDebater ai = Local(stt, llm, tts))
            {
                Wait(session.Start(() => input, () => output, () => ai, new SessionOptions { ServerTurns = true, RoutingVerified = true }));
                Check(session.State == Phase.Speaking, "Lokaler KI-Hinweis wird nicht gesprochen");
                output.Done = true; Wait(session.Tick());
                for (int i = 0; i < 10; i++) { input.Add(Voice(), 1); time += 40; Wait(session.Tick()); }
                for (int i = 0; i < 30; i++) { input.Add(Silence(), 1); time += 40; Wait(session.Tick()); }
                Check(session.State == Phase.Thinking || session.State == Phase.Speaking, "Lokal: Beitrag nach Sprechpause nicht beantwortet (" + session.State + ")");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (session.State != Phase.Speaking && clock.ElapsedMilliseconds < 3000) { Thread.Sleep(10); time += 10; Wait(session.Tick()); }
                Check(session.State == Phase.Speaking && session.Metrics.Latency.Count == 1, "Lokal: NOVA spricht nicht");
                while (session.State == Phase.Speaking && clock.ElapsedMilliseconds < 5000) { Thread.Sleep(10); output.Done = true; Wait(session.Tick()); }
                Check(session.Metrics.Cost == 0 && session.Answers == 1, "Lokal dürfen keine Kosten entstehen");
            }
        }

        private static void TestLocalSetup()
        {
            string reason;
            string releases = "[{\"tag_name\":\"v1.9.5\",\"draft\":false,\"prerelease\":false,\"assets\":[]},"
                + "{\"tag_name\":\"v1.9.4-rc\",\"prerelease\":true,\"assets\":[{\"name\":\"whisper-bin-x64.zip\",\"browser_download_url\":\"https://x/rc.zip\"}]},"
                + "{\"tag_name\":\"v1.9.3\",\"prerelease\":false,\"assets\":[{\"name\":\"whisper-cublas-12-bin-x64.zip\",\"browser_download_url\":\"https://x/cuda.zip\"},{\"name\":\"whisper-bin-x64.zip\",\"browser_download_url\":\"https://x/cpu.zip\"}]}]";
            Check(Downloader.PickUrl(releases, "whisper-bin-x64.zip", new[] { "bin", "x64" }, new[] { "cublas", "cuda" }) == "https://x/cpu.zip", "Release ohne Dateien nicht übersprungen");
            Check(Downloader.PickUrl("{\"assets\":[{\"name\":\"piper_windows_amd64.zip\",\"browser_download_url\":\"https://x/p.zip\"}]}", "piper_windows_amd64.zip", new[] { "windows" }, new string[0]) == "https://x/p.zip", "Einzelnes Release nicht gelesen");
            Check(Downloader.PickUrl("[]", "a.zip", new string[0], new string[0]) == null, "Leere Releaseliste");
            LocalModels laptop = ModelAdvisor.Recommend(new HardwareInfo { RamGb = 15.7, Cores = 8, Cpu = "Intel Core i5" }, out reason);
            Check(laptop.Llm == "gemma3:4b" && laptop.Whisper == "base" && laptop.Threads == 7 && reason.Contains("Prozessor"), "Empfehlung Laptop falsch");
            var gaming = new HardwareInfo { RamGb = 32, Cores = 16 };
            gaming.NvidiaVramGb = HardwareInfo.VramFromName("NVIDIA GeForce RTX 4070");
            Check(ModelAdvisor.Recommend(gaming, out reason).Llm == "qwen3:8b" && gaming.NvidiaVramGb == 12, "Empfehlung Grafikkarte falsch");
            Check(ModelAdvisor.Recommend(new HardwareInfo { RamGb = 4, Cores = 4 }, out reason).Llm == "llama3.2:3b", "Empfehlung wenig RAM falsch");
            Check(ModelAdvisor.Recommend(new HardwareInfo { RamGb = 0, Cores = 2 }, out reason).Llm == "gemma3:4b" && reason.Contains("nicht ermittelt"), "Unbekannte Hardware falsch behandelt");
            var assets = new List<string> { "whisper-blas-bin-x64.zip", "whisper-cublas-12.4.0-bin-x64.zip", "whisper-bin-Win32.zip", "whisper-bin-x64.zip" };
            Check(LocalSources.PickAsset(assets, "whisper-bin-x64.zip", new[] { "bin", "x64" }, new[] { "blas", "cuda" }) == "whisper-bin-x64.zip", "Download-Auswahl falsch");
            assets.RemoveAt(3);
            Check(LocalSources.PickAsset(assets, "whisper-bin-x64.zip", new[] { "bin", "x64" }, new[] { "blas", "cuda" }) == null, "Falsche Variante gewählt");
            Check(LocalSources.PickAsset(new List<string> { "piper_linux_x86_64.tar.gz", "piper_windows_amd64.zip" }, "x", new[] { "windows", "amd64" }, new string[0]) == "piper_windows_amd64.zip",
                "Piper-Auswahl falsch");
            string json = new JavaScriptSerializer().Serialize(new OllamaChat("qwen3:8b").Request(new List<ChatMessage> { new ChatMessage("user", "Hi") }, 200, true));
            Check(json.Contains("\"think\":false") && json.Contains("\"stream\":true") && json.Contains("\"num_predict\":200"), "Ollama-Anfrage falsch: " + json);
            Check(!new JavaScriptSerializer().Serialize(new OllamaChat("gemma3:4b").Request(new List<ChatMessage>(), 10, false)).Contains("think"), "think nur für Denkmodelle");
            Check(OllamaChat.HasModel(new List<string> { "gemma3:4b" }, "gemma3:4b") && OllamaChat.HasModel(new List<string> { "mistral:latest" }, "mistral") &&
                !OllamaChat.HasModel(new List<string> { "gemma3:12b" }, "gemma3:4b"), "Modellerkennung falsch");
            Check(LocalSources.Voice("de_DE-kerstin-low").Path == "kerstin/low/" && LocalSources.Voice("gibt-es-nicht").Id == "de_DE-thorsten-medium", "Stimmenauswahl falsch");
            var stt = new FakeStt(); stt.Texts.Enqueue(LocalBenchmark.Sample);
            var llm = new FakeLlm(); llm.Answers.Enqueue("Unfälle sinken kaum. Was sagst du zu Landstraßen?");
            BenchmarkResult result = LocalBenchmark.Run(stt, llm, new FakeTts(), new Persona(), text => { }, CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Heard == LocalBenchmark.Sample && result.Answer.StartsWith("Unfälle") && result.TtsAudioMs == 500 &&
                result.EstimatedResponseMs == 760 + result.SttMs + result.LlmFirstSentenceMs + result.FirstSentenceTtsMs && result.Describe().Contains("Reaktionszeit"), "Messung falsch");
        }

        private static void TestLicense()
        {
            string privateKey, publicKey;
            using (var rsa = new RSACryptoServiceProvider(2048)) { rsa.PersistKeyInCsp = false; privateKey = rsa.ToXmlString(true); publicKey = rsa.ToXmlString(false); }
            var payload = new Dictionary<string, object> { { "licensee", "Murat Lüdecke" }, { "tier", "Owner" }, { "machines", new[] { "ABC123" } } };
            string token = Licensing.Sign(payload, privateKey);
            LicenseInfo ok = Licensing.Verify(token, publicKey, "abc123", DateTime.UtcNow);
            Check(ok.Valid && ok.Tier == LicenseTier.Owner && ok.Licensee == "Murat Lüdecke" && ok.Describe().Contains("Eigentümer") && Licensing.Allows(ok, "pro"), "Gültige Eigentümer-Lizenz abgelehnt: " + ok.Describe());
            Check(!Licensing.Verify(token, publicKey, "OTHER-PC", DateTime.UtcNow).Valid, "Lizenz gilt auf fremdem PC");
            string[] parts = token.Split('.');
            string forged = Licensing.Base64Url(System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(Licensing.Base64Url(parts[0])).Replace("ABC123", "OTHER-PC"))) + "." + parts[1];
            Check(!Licensing.Verify(forged, publicKey, "OTHER-PC", DateTime.UtcNow).Valid, "Veränderte Lizenz wird akzeptiert");
            string otherKey;
            using (var rsa = new RSACryptoServiceProvider(2048)) { rsa.PersistKeyInCsp = false; otherKey = rsa.ToXmlString(true); }
            Check(!Licensing.Verify(Licensing.Sign(payload, otherKey), publicKey, "ABC123", DateTime.UtcNow).Valid, "Fremd signierte Lizenz wird akzeptiert");
            Check(!Licensing.Verify(Licensing.Sign(new Dictionary<string, object> { { "licensee", "X" }, { "tier", "Owner" } }, privateKey), publicKey, "ABC123", DateTime.UtcNow).Valid,
                "Eigentümer-Lizenz ohne Gerätebindung akzeptiert");
            var expiring = new Dictionary<string, object> { { "licensee", "Kunde" }, { "tier", "Pro" }, { "expires", "2026-12-31" } };
            Check(Licensing.Verify(Licensing.Sign(expiring, privateKey), publicKey, "X", new DateTime(2026, 11, 17)).Valid &&
                !Licensing.Verify(Licensing.Sign(expiring, privateKey), publicKey, "X", new DateTime(2027, 1, 2)).Valid, "Ablaufdatum falsch geprüft");
            Check(!Licensing.Verify(token, "", "ABC123", DateTime.UtcNow).Valid && !Licensing.Verify("kaputt", publicKey, "ABC123", DateTime.UtcNow).Valid, "Fehlerfälle akzeptiert");
            Check(Licensing.MachineId().Length == 20, "Geräte-ID falsch");
        }

        private static void TestRealtimeStart()
        {
            var wire = new FakeWire(); wire.Startup(RealtimeDebater.Disclosure);
            using (var ai = new RealtimeDebater(wire, new LiveOptions { Patience = 0, Transcribe = true }))
            {
                byte[] intro = ai.Start(true, CancellationToken.None).GetAwaiter().GetResult();
                Check(intro.Length == Voice().Length, "KI-Hinweis-Audio fehlt");
                string update = wire.Out[0];
                Check(update.Contains("\"semantic_vad\"") && update.Contains("\"eagerness\":\"low\"") && update.Contains("\"create_response\":false") &&
                    update.Contains("\"interrupt_response\":false") && update.Contains("retention_ratio") && update.Contains("gpt-4o-mini-transcribe") &&
                    update.Contains("24000") && update.Contains("marin"), "Sitzungskonfiguration falsch: " + update);
                Check(wire.Out[1].Contains("\"conversation\":\"none\"") && wire.Out[1].Contains("disclosure"), "KI-Hinweis gehört nicht in den Debattenverlauf");
                LiveEvent line;
                Check(ai.TryTake(out line) && line.Kind == LiveKind.Line && line.Speaker == "KI-Hinweis", "KI-Hinweis fehlt in der Mitschrift");
                ai.Append(new byte[30000]);
                Check(wire.WaitOut(4) && wire.Out[2].Contains("input_audio_buffer.append") && wire.Out[4].Contains("input_audio_buffer.append"),
                    "Audio wird nicht in Stücken gestreamt");
            }
            var fresh = new FakeWire(); fresh.In.Add("{\"type\":\"session.created\"}"); fresh.In.Add("{\"type\":\"session.updated\"}");
            using (var renewed = new RealtimeDebater(fresh, new LiveOptions { ServerTurns = false, Transcribe = false, Memory = "Thema: Tempolimit" }))
            {
                Check(renewed.Start(false, CancellationToken.None).GetAwaiter().GetResult() == null, "Neuaufbau darf keinen KI-Hinweis erzwingen");
                Check(fresh.Out.Count == 1 && fresh.Out[0].Contains("\"turn_detection\":null") && fresh.Out[0].Contains("Thema: Tempolimit") &&
                    fresh.Out[0].Contains("\"transcription\":null"), "Lokaler Modus oder Gedächtnis falsch konfiguriert");
            }
            wire = new FakeWire(); wire.Startup("Ich bin Nova."); wire.Audio("Ich bin Nova.", "completed", "d2");
            using (var ai = new RealtimeDebater(wire)) Expect<AppError>(() => Wait(ai.Start(true, CancellationToken.None)), "KI-Hinweis");
        }

        private static List<LiveEvent> Collect(RealtimeDebater ai, int count)
        {
            var found = new List<LiveEvent>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (found.Count < count && clock.ElapsedMilliseconds < 3000)
            {
                LiveEvent item;
                if (ai.TryTake(out item)) found.Add(item); else Thread.Sleep(5);
            }
            Check(found.Count == count, "Ereignisse fehlen: " + string.Join(",", found));
            return found;
        }

        private static void TestRealtimeLive()
        {
            var wire = new FakeWire(); wire.In.Add("{\"type\":\"session.created\"}"); wire.In.Add("{\"type\":\"session.updated\"}");
            using (var ai = new RealtimeDebater(wire, new LiveOptions()))
            {
                Check(ai.Start(false, CancellationToken.None).GetAwaiter().GetResult() == null && ai.Running, "Live-Schleifen laufen nicht");
                wire.Send("input_audio_buffer.speech_started"); wire.Send("input_audio_buffer.speech_stopped"); wire.Send("input_audio_buffer.committed");
                wire.In.Add("{\"type\":\"conversation.item.input_audio_transcription.completed\",\"transcript\":\" Tempo 130 rettet Leben. \"}");
                var first = Collect(ai, 4);
                Check(first[0].Kind == LiveKind.SpeechStarted && first[1].Kind == LiveKind.SpeechStopped && first[2].Kind == LiveKind.Committed &&
                    first[3].Kind == LiveKind.Line && first[3].Text == "Tempo 130 rettet Leben.", "Sprecher-Ereignisse falsch");
                var sink = new AudioSink();
                ai.Respond(sink, null);
                Check(wire.WaitOut(1) && wire.Out[1].Contains("response.create") && wire.Out[1].Contains("\"purpose\":\"answer\""), "Antwort wird nicht angefordert");
                wire.Created("r1", "answer"); wire.Audio("Das sehe ich anders.", "completed", "r1");
                var answer = Collect(ai, 3);
                Check(answer[0].Kind == LiveKind.AnswerStarted && answer[1].Kind == LiveKind.Line && answer[1].Speaker == "Nova" &&
                    answer[2].Kind == LiveKind.AnswerDone && answer[2].Completed && sink.TotalBytes == Voice().Length, "Antwort-Audio oder Ereignisse falsch");
                ai.Summarize("Gast: Tempo 130 rettet Leben.");
                Check(wire.WaitOut(2) && wire.Out[2].Contains("\"conversation\":\"none\"") && wire.Out[2].Contains("\"purpose\":\"memory\"") &&
                    wire.Out[2].Contains("\"text\"]"), "Gedächtnis-Anfrage falsch");
                wire.Created("r2", "memory");
                wire.In.Add(new JavaScriptSerializer().Serialize(new { type = "response.output_text.done", response_id = "r2", text = "Thema: Tempolimit" }));
                wire.In.Add(new JavaScriptSerializer().Serialize(new { type = "response.done", response = new { id = "r2", status = "completed" } }));
                var memory = Collect(ai, 1);
                Check(memory[0].Kind == LiveKind.Memory && memory[0].Text == "Thema: Tempolimit", "Gedächtnis-Antwort nicht erkannt");
                Thread.Sleep(50);
                LiveEvent extra;
                Check(!ai.TryTake(out extra), "Gedächtnis-Antwort darf keine NOVA-Antwort sein");
                ai.Remember("Thema: Tempolimit");
                Check(wire.WaitOut(3) && wire.Out[3].Contains("session.update") && wire.Out[3].Contains("Thema: Tempolimit"), "Gedächtnis nicht in die Sitzung übernommen");
                ai.Cancel(-1);
                Check(wire.WaitOut(4) && wire.Out[4].Contains("conversation.item.delete") && wire.Out[4].Contains("item_r1"), "Ungehörte Antwort wird nicht gelöscht");
                var second = new AudioSink();
                ai.Respond(second, "Bringe die Debatte voran.");
                Check(wire.WaitOut(5) && wire.Out[5].Contains("Bringe die Debatte voran.") && wire.Out[5].Contains("DEBATTENPROTOKOLL"), "Zusatzanweisung fehlt");
                wire.Created("r3", "answer"); wire.Audio(null, "unused", "r3", false);
                Collect(ai, 1);
                ai.Cancel(500);
                Check(wire.WaitOut(6) && wire.Out[6].Contains("response.cancel"), "Laufende Antwort wird nicht abgebrochen");
                wire.In.Add(new JavaScriptSerializer().Serialize(new { type = "response.done", response = new { id = "r3", status = "cancelled" } }));
                var cut = Collect(ai, 1);
                Check(cut[0].Kind == LiveKind.AnswerDone && !cut[0].Completed && wire.WaitOut(7) && wire.Out[7].Contains("conversation.item.truncate") &&
                    wire.Out[7].Contains("\"audio_end_ms\":500"), "Unterbrochene Antwort wird nicht gekürzt");
            }
        }

        private static void TestRealtimeErrors()
        {
            var wire = new FakeWire();
            wire.In.Add("{\"type\":\"error\",\"error\":{\"code\":\"insufficient_quota\",\"message\":\"SECRET\"}}");
            using (var ai = new RealtimeDebater(wire)) Expect<AppError>(() => Wait(ai.Start(true, CancellationToken.None)), "OpenAI-Limit");
            wire = new FakeWire();
            wire.In.Add("{\"type\":\"error\",\"error\":{\"type\":\"insufficient_quota\",\"code\":\"credit_balance_exhausted\",\"message\":\"SECRET\"}}");
            using (var ai = new RealtimeDebater(wire)) Expect<AppError>(() => Wait(ai.Start(true, CancellationToken.None)), "API-Guthaben ist aufgebraucht");
            using (var token = new CancellationTokenSource())
            using (var ai = new RealtimeDebater(new FakeWire()))
            {
                token.Cancel();
                Expect<OperationCanceledException>(() => Wait(ai.Start(true, token.Token)), null);
            }
            wire = new FakeWire(); wire.In.Add("{\"type\":\"session.created\"}"); wire.In.Add("{\"type\":\"session.updated\"}");
            using (var ai = new RealtimeDebater(wire, new LiveOptions()))
            {
                Wait(ai.Start(false, CancellationToken.None));
                var trace = new ConcurrentQueue<string>();
                RealtimeDebater.Trace = trace.Enqueue;
                ai.Append(new byte[4800]); ai.Commit();
                wire.In.Add("{\"type\":\"error\",\"error\":{\"code\":\"some_unknown_problem\",\"param\":\"session.truncation\",\"message\":\"Unknown parameter\"}}");
                var warning = Collect(ai, 1);
                RealtimeDebater.Trace = delegate { };
                string traced = string.Join("|", trace.ToArray());
                Check(traced.Contains("OpenAI → input_audio_buffer.commit") && !traced.Contains("append") &&
                    traced.Contains("OpenAI-Fehler: some_unknown_problem · session.truncation · Unknown parameter"), "Diagnose-Protokoll unvollständig: " + traced);
                Check(warning[0].Kind == LiveKind.Warning && warning[0].Text == "some_unknown_problem", "Unbekannter Fehler beendet die Debatte");
                wire.In.Add("{\"type\":\"error\",\"error\":{\"code\":\"invalid_api_key\",\"message\":\"SECRET\"}}");
                var fatal = Collect(ai, 1);
                Check(fatal[0].Kind == LiveKind.Fatal && fatal[0].Error.Message.Contains("abgelehnt") && !fatal[0].Error.Message.Contains("SECRET"), "Ungültiger Schlüssel nicht gemeldet");
            }
            wire = new FakeWire(); wire.In.Add("{\"type\":\"session.created\"}"); wire.In.Add("{\"type\":\"session.updated\"}");
            using (var ai = new RealtimeDebater(wire, new LiveOptions()))
            {
                Wait(ai.Start(false, CancellationToken.None));
                wire.In.Add(FakeWire.Close);
                Check(Collect(ai, 1)[0].Kind == LiveKind.Closed, "Verbindungsende nicht gemeldet");
            }
        }

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
            public readonly ConcurrentQueue<LiveEvent> Events = new ConcurrentQueue<LiveEvent>();
            public readonly List<int> Cancels = new List<int>();
            public bool Server, Closed, DisclosureRequested;
            public byte[] Intro;
            public Exception StartError;
            public int Responds, Commits, Clears, Summaries;
            public long Appended;
            public AudioSink Sink;
            public string LastExtra, Material, Remembered;
            public bool ServerTurns { get { return Server; } }
            public Task<byte[]> Start(bool disclosure, CancellationToken token)
            {
                if (StartError != null) throw StartError;
                DisclosureRequested = disclosure;
                return Task.FromResult(disclosure ? Intro : null);
            }
            public void Append(byte[] pcm) { Appended += pcm.Length; }
            public void Commit() { Commits++; }
            public void Clear() { Clears++; }
            public void Respond(AudioSink sink, string extra) { Responds++; Sink = sink; LastExtra = extra; }
            public void Cancel(int playedMs) { Cancels.Add(playedMs); }
            public void Summarize(string material) { Summaries++; Material = material; }
            public void Remember(string memory) { Remembered = memory; }
            public bool TryTake(out LiveEvent item) { return Events.TryDequeue(out item); }
            public void Done(bool completed) { Events.Enqueue(new LiveEvent(LiveKind.AnswerDone) { Completed = completed, Text = completed ? "completed" : "cancelled" }); }
            public void Dispose() { Closed = true; }
        }

        private sealed class FakeWire : IRealtimeWire
        {
            public const string Close = "__close__";
            public readonly BlockingCollection<string> In = new BlockingCollection<string>();
            private readonly List<string> sent = new List<string>();
            private readonly JavaScriptSerializer json = new JavaScriptSerializer();
            public List<string> Out { get { lock (sent) return new List<string>(sent); } }
            public Task Open(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(0); }
            public Task Send(string message, CancellationToken token) { token.ThrowIfCancellationRequested(); lock (sent) sent.Add(message); return Task.FromResult(0); }
            public Task<string> Receive(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.Run(() =>
                {
                    string message = In.Take(token);
                    if (message == Close) throw new WireClosed();
                    return message;
                });
            }
            public void Dispose() { }
            public bool WaitOut(int index)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < 3000) { if (Out.Count > index) return true; Thread.Sleep(5); }
                return false;
            }
            public void Send(string serverEvent) { In.Add(json.Serialize(new { type = serverEvent })); }
            public void Startup(string transcript)
            {
                In.Add("{\"type\":\"session.created\"}"); In.Add("{\"type\":\"session.updated\"}");
                if (transcript != null) Audio(transcript, "completed", "d1");
            }
            public void Created(string id, string purpose) { In.Add(json.Serialize(new { type = "response.created", response = new { id = id, metadata = new { purpose = purpose } } })); }
            public void Audio(string transcript, string status, string id, bool finish = true)
            {
                In.Add(json.Serialize(new { type = "response.output_item.added", response_id = id, item = new { id = "item_" + id } }));
                In.Add(json.Serialize(new { type = "response.output_audio.delta", response_id = id, delta = Convert.ToBase64String(Voice()) }));
                if (transcript != null) In.Add(json.Serialize(new { type = "response.output_audio_transcript.done", response_id = id, transcript = transcript }));
                if (finish) In.Add(json.Serialize(new { type = "response.done", response = new { id = id, status = status } }));
            }
        }
    }
}
