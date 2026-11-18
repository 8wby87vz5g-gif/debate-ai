using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DebateAI
{
    // Voicemeeter endpoint names differ between driver versions and Windows setups:
    // - older drivers: "Voicemeeter Out B1", "Voicemeeter Out B2", "Voicemeeter Input", "Voicemeeter AUX Input";
    // - newer drivers number the bus outputs "Voicemeeter Out 1" … "Out 8" in the order A1–A5, B1–B3
    //   (measured on a real PC with Banana 2.1.3: Out 6 = B1, Out 7 = B2), and Windows may call every
    //   virtual playback endpoint just "Lautsprecher (VB-Audio Voicemeeter VAIO)".
    // WinMM additionally cuts names to 31 characters. Names are therefore only a first guess;
    // unnamed playback endpoints are told apart by a test signal (OutputDiscovery) and every
    // assignment is confirmed by the routing measurement before going live.
    internal static class VirtualDevices
    {
        public static readonly string[] B1 = { "Voicemeeter Out B1", "Voicemeeter Out 6", "Voicemeeter Output" };
        public static readonly string[] B2 = { "Voicemeeter Out B2", "Voicemeeter Out 7", "Voicemeeter AUX Output", "Voicemeeter AUX Out" };
        public static readonly string[] Aux = { "Voicemeeter AUX Input", "Voicemeeter AUX In" };
        public static readonly string[] Vaio = { "Voicemeeter Input" };

        // True if the name contains the hint as a whole word sequence ("Out 6" must not match "Out 60",
        // "AUX In" must not match "AUX Input" when a separate, longer hint exists).
        public static bool Matches(string name, string hint)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(hint)) return false;
            int at = name.IndexOf(hint, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                int end = at + hint.Length;
                bool startOk = at == 0 || !char.IsLetterOrDigit(name[at - 1]);
                bool endOk = end >= name.Length || !char.IsLetterOrDigit(name[end]);
                if (startOk && endOk) return true;
                at = name.IndexOf(hint, at + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public static Device Find(IEnumerable<Device> devices, string[] hints, string avoid)
        {
            foreach (string hint in hints)
                foreach (Device device in devices)
                {
                    if (device == null || !Matches(device.Name, hint)) continue;
                    if (avoid != null && device.Name.IndexOf(avoid, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    return device;
                }
            return null;
        }

        public static Device ByEndpoint(IEnumerable<Device> devices, string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return null;
            foreach (Device device in devices) if (device != null && device.Endpoint == endpoint) return device;
            return null;
        }

        // A Voicemeeter playback endpoint whose name does not say which virtual input it is.
        public static bool IsUnnamedVirtualOutput(Device device)
        {
            if (device == null) return false;
            string name = device.Name ?? "";
            bool virtualDevice = name.IndexOf("Voicemee", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!virtualDevice) return false;
            foreach (string hint in Aux) if (Matches(name, hint)) return false;
            foreach (string hint in Vaio) if (Matches(name, hint)) return false;
            return name.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) < 0;
        }

        // Restores a saved device: the Windows endpoint id is unique and survives restarts; the name is
        // used only if exactly one device carries it (identical truncated names must never be guessed).
        public static int Restore(IList<Device> devices, string endpoint, string name)
        {
            if (!string.IsNullOrEmpty(endpoint))
                for (int i = 0; i < devices.Count; i++) if (devices[i].Endpoint == endpoint) return i;
            if (string.IsNullOrEmpty(name)) return -1;
            int found = -1;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Name != name) continue;
                if (found >= 0) return -1;
                found = i;
            }
            return found;
        }
    }

    internal sealed class DiscoveryStep
    {
        public Device Device;
        public bool AtB1, AtB2;
        public string Describe()
        {
            return Device.Name + " [" + Device.Id + "] → " + (AtB2 && !AtB1 ? "B2 (= Voicemeeter Input, Gast-Weg)" :
                AtB1 && !AtB2 ? "B1 (= Voicemeeter AUX Input, NOVA-Weg)" : AtB1 ? "B1 und B2 (Routing prüfen)" : "weder B1 noch B2");
        }
    }

    // Tells unnamed Voicemeeter playback endpoints apart: plays the test signal on each candidate
    // and listens on B1 and B2. With the Banana routing, Voicemeeter Input (VAIO) reaches only B2
    // and Voicemeeter AUX Input reaches only B1. Stops as soon as both are found.
    internal sealed class OutputDiscovery
    {
        public Device Vaio, Aux;
        public readonly List<DiscoveryStep> Steps = new List<DiscoveryStep>();
        public bool Complete { get { return Vaio != null && Aux != null; } }

        public static async Task<OutputDiscovery> Run(IList<Device> candidates, Func<Device, Func<IPlayback>> open,
            Func<ICapture> b1, Func<ICapture> b2, Func<long> clock, Func<int, Task> delay, Action<int, int, Device> progress)
        {
            var found = new OutputDiscovery();
            for (int i = 0; i < candidates.Count && !found.Complete; i++)
            {
                Device candidate = candidates[i];
                if (progress != null) progress(i + 1, candidates.Count, candidate);
                List<RouteResult> results = await RoutingMeasurement.Measure(candidate.Name, open(candidate), new[]
                {
                    new Listener { Name = "B1", Open = b1, Expected = false },
                    new Listener { Name = "B2", Open = b2, Expected = false }
                }, clock, delay);
                var step = new DiscoveryStep { Device = candidate, AtB1 = results[0].Arrived, AtB2 = results[1].Arrived };
                found.Steps.Add(step);
                if (step.AtB2 && !step.AtB1 && found.Vaio == null) found.Vaio = candidate;
                else if (step.AtB1 && !step.AtB2 && found.Aux == null) found.Aux = candidate;
            }
            return found;
        }
    }
}
