using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DebateAI
{
    // Detects NOVA's own voice on the input by correlating the loudness envelope of the input
    // with the envelope of what was played, over delays of 0 to 1 s. It is a safety net for
    // full duplex, not a replacement for separated audio routes: pure echo correlates strongly,
    // a real person talking (alone or over the echo) does not.
    internal sealed class EchoGuard
    {
        public const int BlockMs = 20;
        public const int BlockBytes = WinAudio.BytesPerSecond * BlockMs / 1000;
        public const int Window = 75;      // 1.5 s compared at a time (speech has few syllables per second)
        public const int MaximumLag = 50;  // up to 1000 ms device and routing delay
        public const double Limit = 0.55;
        private const int Size = 1024;

        private readonly float[] reference = new float[Size], heard = new float[Size];
        private readonly long[] referenceAt = new long[Size], heardAt = new long[Size];
        private readonly List<byte> referenceCarry = new List<byte>(), heardCarry = new List<byte>();
        private readonly double[] votes = new double[MaximumLag + 1];
        private long referenceEnd = -1, lastHeard = -1, referenceSince = -1;

        public double Score { get; private set; }
        public int Lag { get; private set; }

        public EchoGuard()
        {
            for (int i = 0; i < Size; i++) { referenceAt[i] = -1; heardAt[i] = -1; }
            Lag = -1;
        }

        // Called with every chunk handed to the playback device. Audio queues behind audio
        // that is still scheduled, so the envelope lands where it will actually be audible.
        public void AddReference(byte[] pcm, long nowMs)
        {
            long block = Math.Max(nowMs / BlockMs, referenceEnd);
            if (referenceEnd < nowMs / BlockMs)
            {
                referenceCarry.Clear();
                if (!Active(nowMs)) referenceSince = nowMs;
            }
            foreach (float value in Blocks(pcm, referenceCarry))
            {
                reference[block % Size] = value;
                referenceAt[block % Size] = block;
                block++;
            }
            referenceEnd = block;
        }

        // Playback stopped: nothing scheduled after now will be heard.
        public void StopReference(long nowMs)
        {
            referenceEnd = Math.Min(referenceEnd, nowMs / BlockMs);
            referenceCarry.Clear();
        }

        public bool Active(long nowMs) { return referenceEnd >= 0 && nowMs - referenceEnd * BlockMs < 1200; }

        // Reliable decisions need a full window of NOVA's voice; before that, callers stay cautious.
        public bool Warm(long nowMs) { return Active(nowMs) && referenceSince >= 0 && nowMs - referenceSince >= Window * BlockMs + 200; }

        // Returns true when this input frame is most likely NOVA's own voice. An echo path has a
        // fixed delay, so a lag only counts once it has won repeatedly; chance matches wander.
        public bool IsEcho(byte[] frame, long nowMs)
        {
            long now = nowMs / BlockMs;
            var values = new List<float>(Blocks(frame, heardCarry));
            long block = Math.Max(now - values.Count + 1, lastHeard + 1);
            foreach (float value in values)
            {
                heard[block % Size] = value;
                heardAt[block % Size] = block;
                lastHeard = block;
                block++;
            }
            Score = 0;
            if (!Active(nowMs) || lastHeard < Window + 1) return false;
            var x = new double[Window];
            double loudest = -90;
            for (int k = 0; k < Window; k++)
            {
                long at = lastHeard - Window + 1 + k;
                loudest = Math.Max(loudest, Heard(at));
                x[k] = Step(Heard(at) - Heard(at - 1));
            }
            if (loudest < -60) return false;
            var y = new double[Window];
            double best = 0; int bestLag = -1;
            var scores = new double[MaximumLag + 1];
            for (int lag = 0; lag <= MaximumLag; lag++)
            {
                double peak = -90;
                for (int k = 0; k < Window; k++)
                {
                    long at = lastHeard - Window + 1 + k - lag;
                    peak = Math.Max(peak, Reference(at));
                    y[k] = Step(Reference(at) - Reference(at - 1));
                }
                if (peak < -55) continue;
                scores[lag] = Correlation(x, y);
                if (scores[lag] > best) { best = scores[lag]; bestLag = lag; }
            }
            for (int lag = 0; lag <= MaximumLag; lag++) votes[lag] *= 0.9;
            if (bestLag >= 0 && best >= Limit) votes[bestLag] += 1;
            int stable = 0;
            for (int lag = 1; lag <= MaximumLag; lag++) if (votes[lag] > votes[stable]) stable = lag;
            double near = votes[stable] + (stable > 0 ? votes[stable - 1] : 0) + (stable < MaximumLag ? votes[stable + 1] : 0);
            Lag = near >= 2.5 ? stable : -1;
            if (Lag < 0) return false;
            for (int lag = Math.Max(0, Lag - 1); lag <= Math.Min(MaximumLag, Lag + 1); lag++) Score = Math.Max(Score, scores[lag]);
            return Score >= Limit;
        }

        // Onsets jump by 40 dB and would dominate the correlation; capping keeps every block relevant.
        private static double Step(double db) { return Math.Max(-4, Math.Min(4, db)); }

        private double Heard(long at) { return at >= 0 && heardAt[at % Size] == at ? heard[at % Size] : -90; }
        private double Reference(long at) { return at >= 0 && at < referenceEnd && referenceAt[at % Size] == at ? reference[at % Size] : -90; }

        private static IEnumerable<float> Blocks(byte[] pcm, List<byte> carry)
        {
            carry.AddRange(pcm);
            int used = 0;
            while (carry.Count - used >= BlockBytes)
            {
                double squares = 0;
                for (int i = 0; i < BlockBytes; i += 2)
                {
                    double sample = (short)(carry[used + i] | carry[used + i + 1] << 8) / 32768.0;
                    squares += sample * sample;
                }
                used += BlockBytes;
                yield return (float)(10 * Math.Log10(Math.Max(1e-9, squares / (BlockBytes / 2))));
            }
            carry.RemoveRange(0, used);
        }

        private static double Correlation(double[] x, double[] y)
        {
            double mx = 0, my = 0;
            for (int i = 0; i < x.Length; i++) { mx += x[i]; my += y[i]; }
            mx /= x.Length; my /= y.Length;
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < x.Length; i++)
            {
                sxy += (x[i] - mx) * (y[i] - my);
                sxx += (x[i] - mx) * (x[i] - mx);
                syy += (y[i] - my) * (y[i] - my);
            }
            // A flat envelope (steady noise or tone) carries no timing information.
            if (sxx < Window * 1.0 || syy < Window * 1.0) return 0;
            return sxy / Math.Sqrt(sxx * syy);
        }
    }

    // Measurements for the mandatory live acceptance test. Numbers only, no conversation content.
    internal sealed class LiveMetrics
    {
        public readonly List<int> Latency = new List<int>();     // end of guest turn → NOVA audible
        public readonly List<int> FirstAudio = new List<int>();  // answer requested → first audio received
        public int Turns, Answers, BargeIns, Interrupts, Requeues, EchoFrames, EchoLoops, Reconnects, Failures, Proactive, Memory;
        public DateTime Started = DateTime.Now;

        public void Reset()
        {
            Latency.Clear(); FirstAudio.Clear();
            Turns = Answers = BargeIns = Interrupts = Requeues = EchoFrames = EchoLoops = Reconnects = Failures = Proactive = Memory = 0;
            Started = DateTime.Now;
        }

        public string Short()
        {
            if (Latency.Count == 0) return "Latenz: noch keine Messung";
            return "Latenz: zuletzt " + Latency[Latency.Count - 1] + " ms · Ø " + Average(Latency) + " ms · p95 " + Percentile(Latency, 95) + " ms";
        }

        public static int Average(List<int> values)
        {
            if (values.Count == 0) return 0;
            long sum = 0;
            foreach (int value in values) sum += value;
            return (int)(sum / values.Count);
        }

        public static int Percentile(List<int> values, int percent)
        {
            if (values.Count == 0) return 0;
            var sorted = new List<int>(values);
            sorted.Sort();
            int index = (int)Math.Ceiling(percent / 100.0 * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
        }

        public string Report(string version, string settings)
        {
            var text = new StringBuilder();
            var culture = CultureInfo.InvariantCulture;
            text.AppendLine("# DEBATE AI · Live-Testprotokoll");
            text.AppendLine();
            text.AppendLine("- Version: " + version);
            text.AppendLine("- Beginn: " + Started.ToString("yyyy-MM-dd HH:mm", culture) + " · Ende: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", culture));
            text.AppendLine("- Einstellungen: " + settings);
            text.AppendLine();
            text.AppendLine("## Automatisch gemessen");
            text.AppendLine();
            text.AppendLine("| Wert | Ergebnis |");
            text.AppendLine("|---|---|");
            text.AppendLine("| Abgeschlossene Beiträge des Gesprächspartners | " + Turns + " |");
            text.AppendLine("| Gesprochene NOVA-Antworten | " + Answers + " |");
            text.AppendLine("| Latenz Ende Beitrag → NOVA hörbar (Ø / Median / p95 / max) | " + Stats(Latency) + " |");
            text.AppendLine("| Latenz Antwort angefordert → erstes Audio (Ø / Median / p95 / max) | " + Stats(FirstAudio) + " |");
            text.AppendLine("| Unterbrechungen durch den Gast (Barge-in) | " + BargeIns + " |");
            text.AppendLine("| Manuelle Unterbrechungen | " + Interrupts + " |");
            text.AppendLine("| Verworfene ungehörte Antworten (Gast sprach weiter) | " + Requeues + " |");
            text.AppendLine("| Als Echo blockierte Eingangsframes (40 ms) | " + EchoFrames + " |");
            text.AppendLine("| Erkannte Selbstgespräch-Schleifen | " + EchoLoops + " |");
            text.AppendLine("| Proaktive Nachfragen | " + Proactive + " |");
            text.AppendLine("| Gedächtnis-Aktualisierungen | " + Memory + " |");
            text.AppendLine("| Verbindungs-Erneuerungen | " + Reconnects + " |");
            text.AppendLine("| Fehlgeschlagene Antworten | " + Failures + " |");
            text.AppendLine();
            text.AppendLine("Einzelwerte Latenz (ms): " + (Latency.Count == 0 ? "—" : string.Join(", ", Latency)));
            text.AppendLine();
            text.AppendLine("## Pflichtabnahme (von Hand ausfüllen)");
            text.AppendLine();
            text.AppendLine("| Nr. | Anforderung | Bestanden? | Bemerkung |");
            text.AppendLine("|---|---|---|---|");
            text.AppendLine("| 1 | Ein echter Mensch spricht über einen getrennten Audioeingang | | |");
            text.AppendLine("| 2 | NOVA erkennt seine Aussage | | |");
            text.AppendLine("| 3 | NOVA antwortet selbstständig über den konfigurierten Ausgang | | |");
            text.AppendLine("| 4 | Der Mensch hört NOVA über die tatsächliche Streaming-Verbindung | | |");
            text.AppendLine("| 5 | Mindestens zehn zusammenhängende Gesprächswechsel (gemessen: " + Answers + ") | | |");
            text.AppendLine("| 6 | NOVA erinnert sich an frühere Aussagen und reagiert auf Gegenargumente | | |");
            text.AppendLine("| 7 | Keine störenden Echos oder Rückkopplungen (Selbstgespräch-Schleifen: " + EchoLoops + ") | | |");
            text.AppendLine("| 8 | Antwortlatenz gemessen und dokumentiert (siehe oben) | | |");
            return text.ToString();
        }

        private static string Stats(List<int> values)
        {
            if (values.Count == 0) return "keine Messung";
            int max = 0;
            foreach (int value in values) max = Math.Max(max, value);
            return Average(values) + " / " + Percentile(values, 50) + " / " + Percentile(values, 95) + " / " + max + " ms (n=" + values.Count + ")";
        }
    }
}
