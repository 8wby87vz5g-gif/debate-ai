using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace DebateAI
{
    // Abstraction of the official VB-Audio Voicemeeter Remote API, so routing logic is testable.
    internal interface IVoicemeeter : IDisposable
    {
        // 0 = not available, 1 = Voicemeeter (Standard), 2 = Banana, 3 = Potato.
        int Kind { get; }
        bool Running { get; }
        float Get(string parameter);
        void Set(string parameter, float value);
        void SetText(string parameter, string value);
        // Linear level 0..1; type 0 = input pre-fader, 3 = output.
        float Level(int type, int channel);
        List<string> InputDevices();
        List<string> OutputDevices();
    }

    internal sealed class RoutingStep
    {
        public string Parameter, Text, Description;
        public float Value;
        public bool IsText { get { return Text != null; } }
    }

    // Channel layout and the TikTok routing plan for Voicemeeter Banana and Potato.
    internal sealed class VoicemeeterLayout
    {
        public string Name;
        public int Guest, Nova, Microphone;        // strip indices: VAIO, AUX, first hardware input
        public int[] GuestLevel, NovaLevel;          // input level channels (type 0) of VAIO / AUX
        public int[] B1Level, B2Level;              // output level channels (type 3) of B1 / B2

        public static VoicemeeterLayout For(int kind)
        {
            if (kind == 2)
                return new VoicemeeterLayout
                {
                    Name = "Voicemeeter Banana", Guest = 3, Nova = 4, Microphone = 0,
                    GuestLevel = Range(6, 2), NovaLevel = Range(14, 2), B1Level = Range(24, 2), B2Level = Range(32, 2)
                };
            if (kind == 3)
                return new VoicemeeterLayout
                {
                    Name = "Voicemeeter Potato", Guest = 5, Nova = 6, Microphone = 0,
                    GuestLevel = Range(10, 2), NovaLevel = Range(18, 2), B1Level = Range(40, 2), B2Level = Range(48, 2)
                };
            return null;
        }

        private static int[] Range(int start, int count)
        {
            var channels = new int[count];
            for (int i = 0; i < count; i++) channels[i] = start + i;
            return channels;
        }

        // TikTok routing: guest (VAIO) → A1 + B2, NOVA (AUX) → B1 (+ A1 to monitor), microphone → B1 only.
        public List<RoutingStep> Plan(string headphones, string microphone, bool monitorNova, bool novaHearsHost)
        {
            var steps = new List<RoutingStep>();
            Action<int, string, bool, string> route = (strip, bus, on, why) =>
                steps.Add(new RoutingStep { Parameter = "Strip[" + strip + "]." + bus, Value = on ? 1 : 0, Description = why });
            route(Guest, "A1", true, "Gast-Ton (Voicemeeter Input) → A1: du hörst den Gast");
            route(Guest, "B2", true, "Gast-Ton → B2: DEBATE AI hört den Gast");
            route(Guest, "B1", false, "Gast-Ton nicht auf B1: der Gast hört sich nicht selbst");
            route(Nova, "B1", true, "NOVA (Voicemeeter AUX Input) → B1: LIVE Studio sendet NOVA");
            route(Nova, "B2", false, "NOVA nicht auf B2: NOVA hört sich nicht selbst");
            route(Nova, "A1", monitorNova, monitorNova ? "NOVA → A1: du hörst NOVA im Kopfhörer" : "NOVA nicht auf A1");
            route(Microphone, "B1", true, "Dein Mikrofon → B1: deine Stimme geht in den Stream");
            route(Microphone, "B2", novaHearsHost, novaHearsHost ? "Dein Mikrofon → B2: NOVA hört auch dich" : "Dein Mikrofon nicht auf B2: NOVA debattiert nur mit dem Gast");
            route(Microphone, "A1", false, "Dein Mikrofon nicht auf A1: kein Mithören der eigenen Stimme");
            if (!string.IsNullOrEmpty(headphones))
                steps.Add(new RoutingStep { Parameter = "Bus[0].device.wdm", Text = headphones, Description = "A1 = " + headphones });
            if (!string.IsNullOrEmpty(microphone))
                steps.Add(new RoutingStep { Parameter = "Strip[" + Microphone + "].device.wdm", Text = microphone, Description = "Hardware-Eingang 1 = " + microphone });
            return steps;
        }
    }

    internal static class VoicemeeterSetup
    {
        public static void Apply(IVoicemeeter vm, List<RoutingStep> steps)
        {
            foreach (RoutingStep step in steps)
            {
                if (step.IsText) vm.SetText(step.Parameter, step.Text);
                else vm.Set(step.Parameter, step.Value);
            }
        }

        // Reads every switch back; device names are write-only in the API and are checked by the routing measurement.
        public static List<string> Verify(IVoicemeeter vm, List<RoutingStep> steps)
        {
            var wrong = new List<string>();
            foreach (RoutingStep step in steps)
            {
                if (step.IsText) continue;
                float actual = vm.Get(step.Parameter);
                if (Math.Abs(actual - step.Value) > 0.01f) wrong.Add(step.Description + " (ist " + actual.ToString(CultureInfo.InvariantCulture) + ")");
            }
            return wrong;
        }

        public static double Decibel(float linear) { return linear <= 1e-6f ? -90 : Math.Max(-90, 20 * Math.Log10(linear)); }

        public static double Loudest(IVoicemeeter vm, int type, int[] channels)
        {
            double loudest = -90;
            foreach (int channel in channels) loudest = Math.Max(loudest, Decibel(vm.Level(type, channel)));
            return loudest;
        }

        public static string Pick(List<string> names, params string[] hints)
        {
            foreach (string hint in hints)
                foreach (string name in names)
                    if (name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return name;
            return null;
        }
    }

    // Loads VoicemeeterRemote(64).dll from the Voicemeeter installation and calls it.
    internal sealed class VoicemeeterRemote : IVoicemeeter
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int NoArgs();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IntArg(int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IntOut(out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)] private delegate int GetFloatFn(string name, out float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)] private delegate int SetFloatFn(string name, float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)] private delegate int SetStringFn(string name, string value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LevelFn(int type, int channel, out float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DeviceDescFn(int index, out int type, [Out] byte[] name, [Out] byte[] hardwareId);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32")] private static extern bool FreeLibrary(IntPtr module);

        private const string UninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}";
        private readonly IntPtr module;
        private readonly NoArgs login, logout, dirty, outputCount, inputCount;
        private readonly IntArg run;
        private readonly IntOut kind;
        private readonly GetFloatFn getFloat;
        private readonly SetFloatFn setFloat;
        private readonly SetStringFn setString;
        private readonly LevelFn level;
        private readonly DeviceDescFn outputDesc, inputDesc;
        private bool loggedIn;

        public string Folder { get; private set; }

        public static string FindFolder()
        {
            var candidates = new List<string>();
            foreach (string key in new[] { UninstallKey, UninstallKey.Replace(@"WOW6432Node\", "") })
            {
                try
                {
                    using (RegistryKey entry = Registry.LocalMachine.OpenSubKey(key))
                    {
                        string uninstall = entry == null ? null : entry.GetValue("UninstallString") as string;
                        if (!string.IsNullOrEmpty(uninstall)) candidates.Add(Path.GetDirectoryName(uninstall.Trim('"')));
                    }
                }
                catch (Exception) { }
            }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"VB\Voicemeeter"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VB\Voicemeeter"));
            foreach (string folder in candidates)
                if (!string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, DllName))) return folder;
            return null;
        }

        private static string DllName { get { return IntPtr.Size == 8 ? "VoicemeeterRemote64.dll" : "VoicemeeterRemote.dll"; } }

        public VoicemeeterRemote()
        {
            Folder = FindFolder();
            if (Folder == null) throw new AppError("Voicemeeter ist nicht installiert. Bitte Voicemeeter Banana von vb-audio.com installieren und Windows neu starten.");
            module = LoadLibrary(Path.Combine(Folder, DllName));
            if (module == IntPtr.Zero) throw new AppError("Die Voicemeeter-Fernsteuerung konnte nicht geladen werden.");
            login = Bind<NoArgs>("VBVMR_Login"); logout = Bind<NoArgs>("VBVMR_Logout");
            run = Bind<IntArg>("VBVMR_RunVoicemeeter"); kind = Bind<IntOut>("VBVMR_GetVoicemeeterType");
            dirty = Bind<NoArgs>("VBVMR_IsParametersDirty");
            getFloat = Bind<GetFloatFn>("VBVMR_GetParameterFloat"); setFloat = Bind<SetFloatFn>("VBVMR_SetParameterFloat");
            setString = Bind<SetStringFn>("VBVMR_SetParameterStringA"); level = Bind<LevelFn>("VBVMR_GetLevel");
            outputCount = Bind<NoArgs>("VBVMR_Output_GetDeviceNumber"); outputDesc = Bind<DeviceDescFn>("VBVMR_Output_GetDeviceDescA");
            inputCount = Bind<NoArgs>("VBVMR_Input_GetDeviceNumber"); inputDesc = Bind<DeviceDescFn>("VBVMR_Input_GetDeviceDescA");
            int result = login();
            if (result < 0) { Dispose(); throw new AppError("Anmeldung an Voicemeeter fehlgeschlagen (Code " + result + ")."); }
            loggedIn = true;
        }

        private T Bind<T>(string name) where T : class
        {
            IntPtr address = GetProcAddress(module, name);
            if (address == IntPtr.Zero) throw new AppError("Die installierte Voicemeeter-Version unterstützt " + name + " nicht. Bitte Voicemeeter aktualisieren.");
            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }

        public int Kind
        {
            get
            {
                int value;
                return kind(out value) == 0 ? value : 0;
            }
        }

        public bool Running { get { return Kind != 0; } }

        // Starts the installed Voicemeeter application (2 = Banana). Only on the user's request.
        public bool Launch(int type) { return run(type) == 0; }

        public float Get(string parameter)
        {
            dirty();
            float value;
            int result = getFloat(parameter, out value);
            if (result != 0) throw new AppError("Voicemeeter-Parameter " + parameter + " nicht lesbar (Code " + result + ").");
            return value;
        }

        public void Set(string parameter, float value)
        {
            int result = setFloat(parameter, value);
            if (result != 0) throw new AppError("Voicemeeter-Parameter " + parameter + " nicht änderbar (Code " + result + ").");
        }

        public void SetText(string parameter, string value)
        {
            int result = setString(parameter, value);
            if (result != 0) throw new AppError("Voicemeeter-Parameter " + parameter + " nicht änderbar (Code " + result + ").");
        }

        public float Level(int type, int channel)
        {
            float value;
            return level(type, channel, out value) == 0 ? value : 0;
        }

        public List<string> InputDevices() { return Devices(inputCount, inputDesc); }
        public List<string> OutputDevices() { return Devices(outputCount, outputDesc); }

        // WDM devices only; Voicemeeter selects hardware by this name.
        private static List<string> Devices(NoArgs count, DeviceDescFn describe)
        {
            var names = new List<string>();
            int total = count();
            for (int i = 0; i < total; i++)
            {
                var name = new byte[512]; var id = new byte[512];
                int type;
                if (describe(i, out type, name, id) != 0 || type != 3) continue;
                string text = Encoding.Default.GetString(name).TrimEnd('\0');
                int end = text.IndexOf('\0');
                if (end >= 0) text = text.Substring(0, end);
                if (text.Length != 0 && !names.Contains(text)) names.Add(text);
            }
            return names;
        }

        public void Dispose()
        {
            if (loggedIn) { try { logout(); } catch (Exception) { } loggedIn = false; }
            if (module != IntPtr.Zero) FreeLibrary(module);
        }
    }
}
