using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DebateAI
{
    internal sealed class RouteResult
    {
        public string From, To;
        public bool Expected, Arrived;
        public int Hits;
        public double Peak = -90;
        public bool Ok { get { return Expected == Arrived; } }

        public string Describe()
        {
            string state = Arrived ? "kommt an" : "kommt nicht an";
            return (Ok ? "✓ " : "✗ ") + From + " → " + To + ": " + state + (Expected ? " (soll ankommen)" : " (darf nicht ankommen)") +
                " · Treffer " + Hits + " · Spitze " + (Peak <= -90 ? "—" : ((int)Peak) + " dBFS");
        }
    }

    internal sealed class Listener
    {
        public string Name;
        public Func<ICapture> Open;
        public bool Expected;
    }

    // Real hardware measurement of the audio routes: plays a speech-like signal on one output
    // and checks on every listed input whether exactly that signal arrives (envelope correlation).
    internal static class RoutingMeasurement
    {
        public const int SignalMs = 3500;
        public const int ListenMs = SignalMs + 900;
        public const int MinimumHits = 10;

        public static async Task<List<RouteResult>> Measure(string source, Func<IPlayback> openOutput, IList<Listener> listeners,
            Func<long> clock, Func<int, Task> delay)
        {
            var captures = new List<ICapture>();
            var guards = new List<EchoGuard>();
            var results = new List<RouteResult>();
            IPlayback playback = null;
            try
            {
                foreach (Listener listener in listeners)
                {
                    captures.Add(listener.Open());
                    guards.Add(new EchoGuard());
                    results.Add(new RouteResult { From = source, To = listener.Name, Expected = listener.Expected });
                }
                playback = openOutput();
                byte[] signal = DebateSession.TestSignal(SignalMs);
                foreach (ICapture capture in captures) capture.Drain(null);
                long start = clock();
                playback.Play(signal);
                foreach (EchoGuard guard in guards) guard.AddReference(signal, start);
                while (clock() - start < ListenMs)
                {
                    await delay(40);
                    long now = clock();
                    for (int i = 0; i < captures.Count; i++)
                    {
                        RouteResult result = results[i];
                        EchoGuard guard = guards[i];
                        captures[i].Drain(frame =>
                        {
                            result.Peak = Math.Max(result.Peak, TurnDetector.Level(frame));
                            if (guard.IsEcho(frame, now)) result.Hits++;
                        });
                    }
                }
                foreach (RouteResult result in results) result.Arrived = result.Hits >= MinimumHits;
                return results;
            }
            finally
            {
                if (playback != null) try { playback.Dispose(); } catch (Exception) { }
                foreach (ICapture capture in captures) try { capture.Dispose(); } catch (Exception) { }
            }
        }
    }

    // Test 1 and 2: guest audio reaches Voicemeeter VAIO and DEBATE AI (B2) while a guest or LIVE Studio plays.
    internal sealed class GuestProbe
    {
        public const double Speech = -45;
        public const int RequiredMs = 1500;
        public int VaioMs, InputMs, BothMs;
        public double VaioPeak = -90, InputPeak = -90;

        public void Add(double vaioDb, double inputDb, int ms)
        {
            VaioPeak = Math.Max(VaioPeak, vaioDb);
            InputPeak = Math.Max(InputPeak, inputDb);
            bool vaio = vaioDb >= Speech, input = inputDb >= Speech;
            if (vaio) VaioMs += ms;
            if (input) InputMs += ms;
            if (vaio && input) BothMs += ms;
        }

        public bool VaioOk { get { return VaioMs >= RequiredMs; } }
        public bool InputOk { get { return BothMs >= RequiredMs; } }

        public string Describe()
        {
            if (VaioOk && InputOk) return "✓ Gastton kommt in Voicemeeter (VAIO) und bei DEBATE AI (B2) an.";
            if (!VaioOk && VaioMs == 0 && InputMs == 0)
                return "✗ Kein Gastton gemessen. Läuft ein Gast oder Ton in LIVE Studio? Ist in LIVE Studio „Voicemeeter Input“ als Lautsprecher/Audiogerät gewählt?";
            if (!VaioOk) return "✗ Am Voicemeeter-Eingang (VAIO) kommt kaum Ton an. LIVE-Studio-Ausgabe auf „Voicemeeter Input“ stellen.";
            return "✗ VAIO bekommt Ton, aber B2 nicht. In Voicemeeter am Kanal „Voicemeeter Input“ B2 einschalten (oder Routing einrichten lassen).";
        }
    }

    internal static class LiveStudio
    {
        public static bool IsLiveStudio(string processName)
        {
            string name = processName ?? "";
            return name.IndexOf("TikTok", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (name.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("LIVE", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Only reports that the program runs. DEBATE AI has no connection to TikTok itself.
        public static bool Running()
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    using (process) if (IsLiveStudio(process.ProcessName)) return true;
                }
            }
            catch (Exception) { }
            return false;
        }
    }
}
