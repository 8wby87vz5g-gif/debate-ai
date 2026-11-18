using System;
using System.Collections.Generic;

namespace DebateAI
{
    // Ready-made configurations for the common live setups. Indices refer to the
    // choices in the main window (Sprecherwechsel, Geduld, Unterbrechung, Antwortlänge).
    internal sealed class ScenarioPreset
    {
        public string Name, Summary, Routing;
        public string[] Inputs, Outputs, AvoidOutputs = new string[0];
        public int Mode, Patience, BargeIn, Length, Proactive, BargeInMs = 500;
        public bool Fairness = true, EchoGuard = true, Memory = true;
    }

    internal static class Setup
    {
        private static readonly string[] VirtualInputs = { "Voicemeeter Out B2", "Voicemeeter Out 7", "Voicemeeter AUX Output", "Voicemeeter AUX Out", "Voicemeeter Out B1", "Voicemeeter Out 6", "Voicemeeter Output", "CABLE Output", "VB-Audio Virtual Cable" };
        private static readonly string[] VirtualOutputs = { "Voicemeeter AUX Input", "Voicemeeter AUX In", "Voicemeeter VAIO3", "CABLE Input" };

        public static readonly ScenarioPreset[] Presets =
        {
            new ScenarioPreset
            {
                Name = "TikTok LIVE Studio", Inputs = VirtualInputs, Outputs = VirtualOutputs,
                Mode = 0, Patience = 1, BargeIn = 0, Length = 0, Proactive = 10,
                Summary = "Gast aus LIVE Studio hören, NOVA als Mikrofon in den Stream. Kurze, schlagfertige Antworten; NOVA stoppt, wenn der Gast unterbricht; nach 10 s Stille fragt sie nach.",
                Routing = "Voicemeeter Banana: LIVE Studio gibt auf „Voicemeeter Input“ aus (A1 + B2 an, B1 aus). DEBATE AI hört B2, spricht auf „Voicemeeter AUX Input“ (B1 an, B2 aus). In LIVE Studio „Voicemeeter Out B1“ als Mikrofon wählen."
            },
            new ScenarioPreset
            {
                Name = "OBS Studio", Inputs = VirtualInputs, Outputs = VirtualOutputs,
                Mode = 0, Patience = 0, BargeIn = 0, Length = 1, Proactive = 0,
                Summary = "Gast aus einer Anruf-App hören, NOVA als eigene Audioquelle in OBS. Mittlere Antwortlänge, keine automatischen Nachfragen.",
                Routing = "Anruf-App gibt auf „Voicemeeter Input“ aus (B2 an, B1 aus). DEBATE AI hört B2, spricht auf „Voicemeeter AUX Input“ (B1 an). In OBS „Voicemeeter Out B1“ als Audio-Eingangsquelle hinzufügen."
            },
            new ScenarioPreset
            {
                Name = "Discord", Inputs = VirtualInputs, Outputs = VirtualOutputs,
                Mode = 0, Patience = 1, BargeIn = 1, Length = 1, Proactive = 0,
                Summary = "Sprachkanal mit mehreren Menschen. NOVA beendet ihren Satz, wenn jemand dazwischenredet, und antwortet danach.",
                Routing = "Discord-Ausgabegerät „Voicemeeter Input“, Discord-Eingabegerät „Voicemeeter Out B1“. DEBATE AI hört B2 und spricht auf „Voicemeeter AUX Input“. Discords eigene Echounterdrückung kann aus bleiben."
            },
            new ScenarioPreset
            {
                Name = "Lokal (Mikrofon und Kopfhörer)",
                Inputs = new[] { "Mikrofon", "Microphone", "Headset", "Mic" },
                Outputs = new[] { "Kopfhörer", "Headphones", "Headset", "Lautsprecher", "Speakers", "Realtek" },
                AvoidOutputs = new[] { "Voicemeeter", "CABLE", "Virtual" },
                Mode = 0, Patience = 0, BargeIn = 0, Length = 1, Proactive = 0,
                Summary = "Du debattierst selbst mit NOVA. Mit Kopfhörern ist Vollduplex möglich; mit Lautsprechern wird Halbduplex empfohlen.",
                Routing = "Kein virtuelles Routing nötig. Mikrofon als Eingang, Kopfhörer als Ausgang."
            }
        };

        // Returns the index of the best matching device name, or -1. Hints are tried in order.
        public static int Pick(IList<string> names, string[] hints, string[] avoid)
        {
            foreach (string hint in hints)
                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i] ?? "";
                    if (name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (Contains(name, avoid)) continue;
                    return i;
                }
            return -1;
        }

        // Plausibility check of a device pair. Returns warnings in plain German, empty if fine.
        public static List<string> Check(string input, string output)
        {
            var warnings = new List<string>();
            input = input ?? ""; output = output ?? "";
            if (input.Length == 0) warnings.Add("Kein Eingang gewählt.");
            if (output.Length == 0) warnings.Add("Kein Ausgang für NOVA gewählt.");
            if (input.Length == 0 || output.Length == 0) return warnings;
            bool vmIn = input.IndexOf("Voicemeeter", StringComparison.OrdinalIgnoreCase) >= 0;
            bool vmOut = Virtual(output);
            bool aux = output.IndexOf("AUX", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("VAIO3", StringComparison.OrdinalIgnoreCase) >= 0;
            // "Lautsprecher (VB-Audio Voicemeeter VAIO)" does not say which virtual input it is; the routing measurement decides.
            bool unnamed = output.IndexOf("Voicemeeter", StringComparison.OrdinalIgnoreCase) < 0;
            if (vmIn && vmOut && !aux && !unnamed)
                warnings.Add("NOVA spricht auf den Haupteingang von Voicemeeter, den DEBATE AI auch hört. Das erzeugt fast sicher ein Echo. Für NOVA „Voicemeeter AUX Input“ verwenden.");
            if ((input.IndexOf("B1", StringComparison.OrdinalIgnoreCase) >= 0 || VirtualDevices.Matches(input, "Voicemeeter Out 6")) && vmOut)
                warnings.Add("Eingang B1 ist bei Banana der Bus zum Stream. Für den Gast besser B2 verwenden, sonst hört NOVA sich selbst.");
            if (input.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0 && output.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0)
                warnings.Add("Ein- und Ausgang nutzen dasselbe virtuelle Kabel. NOVA würde sich selbst hören.");
            if (Speakers(output))
                warnings.Add("Lautsprecher am NOVA-Ausgang: Das Mikrofon hört NOVA mit. Kopfhörer verwenden oder „Ignorieren (Halbduplex)“ wählen.");
            return warnings;
        }

        // Virtual audio devices (Voicemeeter, VB-Cable) are never room speakers, even if Windows names them "Lautsprecher".
        public static bool Virtual(string name)
        {
            name = name ?? "";
            return name.IndexOf("Voicemee", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool Speakers(string output)
        {
            output = output ?? "";
            if (Virtual(output)) return false;
            return output.IndexOf("Lautsprecher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   output.IndexOf("Speaker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Contains(string name, string[] tokens)
        {
            foreach (string token in tokens ?? new string[0])
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
