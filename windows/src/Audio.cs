using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DebateAI
{
    internal sealed class Device
    {
        public readonly uint Id;
        public readonly string Name;
        public readonly string Endpoint;

        public Device(uint id, string name, string endpoint) { Id = id; Name = name; Endpoint = endpoint; }
        public override string ToString() { return Name + " [" + Id + "]"; }
    }

    internal static class WinAudio
    {
        public const int Rate = 24000;
        public const int FrameMs = 40;
        public const int BytesPerSecond = Rate * 2;

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        public struct Format
        {
            public ushort Tag, Channels;
            public uint Rate, BytesPerSecond;
            public ushort Align, Bits, Extra;

            public Format(uint rate, ushort channels)
            {
                Tag = 1; Channels = channels; Rate = rate; Align = (ushort)(2 * channels);
                BytesPerSecond = rate * Align; Bits = 16; Extra = 0;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Caps
        {
            public ushort Manufacturer, Product;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint Formats;
            public ushort Channels, Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OutputCaps { public Caps Common; public uint Support; }

        [StructLayout(LayoutKind.Sequential)]
        public struct Header
        {
            public IntPtr Data;
            public uint Length, Recorded;
            public UIntPtr User;
            public uint Flags, Loops;
            public IntPtr Next;
            public UIntPtr Reserved;
        }

        private const uint QueryFunctionIdSize = 0x0800 + 18;
        private const uint QueryFunctionId = 0x0800 + 17;
        public const uint Done = 1;
        public static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));

        [DllImport("winmm.dll")] private static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint waveOutGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint waveInGetDevCapsW(UIntPtr id, out Caps caps, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutGetDevCapsW(UIntPtr id, out OutputCaps caps, uint size);
        [DllImport("winmm.dll")] private static extern uint waveInMessage(IntPtr device, uint message, IntPtr first, IntPtr second);
        [DllImport("winmm.dll")] private static extern uint waveOutMessage(IntPtr device, uint message, IntPtr first, IntPtr second);
        [DllImport("winmm.dll")] public static extern uint waveInOpen(out IntPtr handle, uint id, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] public static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] public static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] public static extern uint waveInStart(IntPtr handle);
        [DllImport("winmm.dll")] public static extern uint waveInReset(IntPtr handle);
        [DllImport("winmm.dll")] public static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] public static extern uint waveInClose(IntPtr handle);
        [DllImport("winmm.dll")] public static extern uint waveOutOpen(out IntPtr handle, uint id, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] public static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] public static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] public static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] public static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] public static extern uint waveOutClose(IntPtr handle);

        public static List<Device> Devices(bool input)
        {
            var devices = new List<Device>();
            uint count = input ? waveInGetNumDevs() : waveOutGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                string name;
                if (input)
                {
                    Caps caps;
                    if (waveInGetDevCapsW(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(Caps))) != 0) continue;
                    name = caps.Name;
                }
                else
                {
                    OutputCaps caps;
                    if (waveOutGetDevCapsW(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(OutputCaps))) != 0) continue;
                    name = caps.Common.Name;
                }
                devices.Add(new Device(i, name, EndpointId(input, i)));
            }
            return devices;
        }

        public static uint Resolve(bool input, Device selection)
        {
            if (selection == null) throw new AppError("Bitte das Audiogerät ausdrücklich auswählen.");
            foreach (Device current in Devices(input))
            {
                if (selection.Endpoint != null && current.Endpoint == selection.Endpoint) return current.Id;
                if (selection.Endpoint == null && current.Id == selection.Id && current.Name == selection.Name) return current.Id;
            }
            throw new AppError("Die Geräteauswahl hat sich geändert. Liste aktualisieren und neu auswählen.");
        }

        // Used to detect changed WinMM indices between selection and opening a device.
        public static string EndpointId(bool input, uint id)
        {
            IntPtr device = new IntPtr((long)id);
            IntPtr size = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                uint result = input ? waveInMessage(device, QueryFunctionIdSize, size, IntPtr.Zero)
                                    : waveOutMessage(device, QueryFunctionIdSize, size, IntPtr.Zero);
                if (result != 0) return null;
                int bytes = Marshal.ReadInt32(size);
                if (bytes < 2 || bytes > 4096) return null;
                IntPtr value = Marshal.AllocHGlobal(bytes);
                try
                {
                    result = input ? waveInMessage(device, QueryFunctionId, value, new IntPtr(bytes))
                                   : waveOutMessage(device, QueryFunctionId, value, new IntPtr(bytes));
                    return result == 0 ? Marshal.PtrToStringUni(value, bytes / 2).TrimEnd('\0') : null;
                }
                finally { Marshal.FreeHGlobal(value); }
            }
            finally { Marshal.FreeHGlobal(size); }
        }

        public static void Check(uint result, string action)
        {
            if (result == 0) return;
            throw new AppError("Audio: " + action + " (Windows-Code " + result + "). Gerät prüfen und Liste aktualisieren.");
        }

        public static byte[] ToMono24(byte[] data, Format format)
        {
            int block = format.Align * (int)(format.Rate / Rate);
            byte[] output = new byte[data.Length / block * 2];
            for (int i = 0, j = 0; j < output.Length; i += block, j += 2)
            {
                int sum = 0;
                for (int k = 0; k < block; k += 2) sum += ReadSample(data, i + k);
                WriteSample(output, j, (short)(sum / (block / 2)));
            }
            return output;
        }

        public static byte[] ToStereo48(byte[] mono)
        {
            byte[] output = new byte[mono.Length * 4];
            for (int i = 0; i < mono.Length; i += 2)
            {
                short current = ReadSample(mono, i);
                short next = i + 2 < mono.Length ? ReadSample(mono, i + 2) : current;
                short middle = (short)((current + next) / 2);
                int offset = i * 4;
                WriteSample(output, offset, current); WriteSample(output, offset + 2, current);
                WriteSample(output, offset + 4, middle); WriteSample(output, offset + 6, middle);
            }
            return output;
        }

        public static short ReadSample(byte[] data, int offset)
        {
            return (short)(data[offset] | data[offset + 1] << 8);
        }

        public static void WriteSample(byte[] data, int offset, short sample)
        {
            data[offset] = (byte)sample;
            data[offset + 1] = (byte)(sample >> 8);
        }

        public static byte[] Tone(int milliseconds, double frequency)
        {
            int samples = Rate * milliseconds / 1000;
            byte[] data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                double fade = Math.Min(1, Math.Min(i, samples - i - 1) / 480.0);
                WriteSample(data, i * 2, (short)(fade * 4200 * Math.Sin(2 * Math.PI * frequency * i / Rate)));
            }
            return data;
        }
    }

    internal sealed class WaveBuffer
    {
        public readonly IntPtr Pointer;
        private readonly IntPtr data;
        public bool Prepared;

        public WaveBuffer(int size)
        {
            data = Marshal.AllocHGlobal(size);
            try { Pointer = Marshal.AllocHGlobal((int)WinAudio.HeaderSize); }
            catch { Marshal.FreeHGlobal(data); throw; }
            Marshal.StructureToPtr(new WinAudio.Header { Data = data, Length = (uint)size }, Pointer, false);
        }

        public WinAudio.Header Read() { return (WinAudio.Header)Marshal.PtrToStructure(Pointer, typeof(WinAudio.Header)); }
        public void Fill(byte[] bytes) { Marshal.Copy(bytes, 0, data, bytes.Length); }
        public void Free() { Marshal.FreeHGlobal(Pointer); Marshal.FreeHGlobal(data); }
    }

    internal interface ICapture : IDisposable
    {
        void Drain(Action<byte[]> onFrame);
    }

    internal interface IPlayback : IDisposable
    {
        void Play(byte[] pcm);
        bool Finished { get; }
    }

    internal sealed class WaveCapture : ICapture
    {
        private IntPtr handle;
        private readonly List<WaveBuffer> buffers = new List<WaveBuffer>();
        private readonly Stopwatch lastFrame = Stopwatch.StartNew();
        private readonly WinAudio.Format format;
        private int cursor;

        public WaveCapture(uint id)
        {
            format = new WinAudio.Format(48000, 2);
            uint result = Open(id, ref format);
            if (result != 0) { format = new WinAudio.Format(48000, 1); result = Open(id, ref format); }
            if (result != 0) { format = new WinAudio.Format(24000, 1); result = Open(id, ref format); }
            WinAudio.Check(result, "Eingang öffnen");
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    var buffer = new WaveBuffer((int)format.BytesPerSecond * WinAudio.FrameMs / 1000);
                    buffers.Add(buffer);
                    WinAudio.Check(WinAudio.waveInPrepareHeader(handle, buffer.Pointer, WinAudio.HeaderSize), "Eingang vorbereiten");
                    buffer.Prepared = true;
                    Queue(buffer);
                }
                WinAudio.Check(WinAudio.waveInStart(handle), "Eingang starten");
            }
            catch { Dispose(); throw; }
        }

        private uint Open(uint id, ref WinAudio.Format candidate)
        {
            return WinAudio.waveInOpen(out handle, id, ref candidate, IntPtr.Zero, IntPtr.Zero, 0);
        }

        private void Queue(WaveBuffer buffer)
        {
            WinAudio.Check(WinAudio.waveInAddBuffer(handle, buffer.Pointer, WinAudio.HeaderSize), "Eingangspuffer bereitstellen");
        }

        public void Drain(Action<byte[]> onFrame)
        {
            for (int i = 0; i < buffers.Count; i++)
            {
                WaveBuffer buffer = buffers[cursor];
                WinAudio.Header header = buffer.Read();
                if ((header.Flags & WinAudio.Done) == 0) break;
                byte[] bytes = null;
                if (onFrame != null && header.Recorded > 0)
                {
                    bytes = new byte[Math.Min(header.Recorded, header.Length)];
                    Marshal.Copy(header.Data, bytes, 0, bytes.Length);
                }
                Queue(buffer);
                cursor = (cursor + 1) % buffers.Count;
                if (header.Recorded > 0) lastFrame.Restart();
                if (bytes != null) onFrame(WinAudio.ToMono24(bytes, format));
            }
            if (lastFrame.ElapsedMilliseconds > 2500)
                throw new AppError("Der Eingang liefert keine Audioframes. Gerät und Windows-Mikrofonzugriff prüfen.");
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            uint reset = WinAudio.waveInReset(handle);
            uint unprepare = 0;
            foreach (WaveBuffer buffer in buffers)
            {
                // On a broken driver, leave memory owned by the driver allocated.
                uint result = buffer.Prepared ? WinAudio.waveInUnprepareHeader(handle, buffer.Pointer, WinAudio.HeaderSize) : 0;
                if (result == 0) buffer.Free();
                else unprepare = result;
            }
            buffers.Clear();
            uint close = WinAudio.waveInClose(handle);
            handle = IntPtr.Zero;
            WinAudio.Check(reset, "Eingang stoppen");
            WinAudio.Check(unprepare, "Eingangspuffer freigeben");
            WinAudio.Check(close, "Eingang schließen");
        }
    }

    internal sealed class WavePlayback : IPlayback
    {
        private IntPtr handle;
        private WaveBuffer buffer;
        private readonly bool stereo;
        private readonly Stopwatch elapsed = new Stopwatch();
        private int expectedMs;

        public WavePlayback(uint id)
        {
            var format = new WinAudio.Format(48000, 2);
            uint result = WinAudio.waveOutOpen(out handle, id, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            stereo = result == 0;
            if (!stereo)
            {
                format = new WinAudio.Format(24000, 1);
                result = WinAudio.waveOutOpen(out handle, id, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            }
            WinAudio.Check(result, "Ausgang öffnen");
        }

        public void Play(byte[] pcm)
        {
            ReleaseBuffer();
            expectedMs = pcm.Length * 1000 / WinAudio.BytesPerSecond;
            byte[] data = stereo ? WinAudio.ToStereo48(pcm) : pcm;
            buffer = new WaveBuffer(data.Length);
            buffer.Fill(data);
            WinAudio.Check(WinAudio.waveOutPrepareHeader(handle, buffer.Pointer, WinAudio.HeaderSize), "Ausgang vorbereiten");
            buffer.Prepared = true;
            WinAudio.Check(WinAudio.waveOutWrite(handle, buffer.Pointer, WinAudio.HeaderSize), "Wiedergabe starten");
            elapsed.Restart();
        }

        public bool Finished
        {
            get
            {
                if (buffer == null) return true;
                if ((buffer.Read().Flags & WinAudio.Done) == 0)
                {
                    if (elapsed.ElapsedMilliseconds > expectedMs + 3000)
                        throw new AppError("Der Ausgang meldet kein Wiedergabeende. Gerät prüfen.");
                    return false;
                }
                ReleaseBuffer();
                return true;
            }
        }

        private void ReleaseBuffer()
        {
            if (buffer == null) return;
            if (buffer.Prepared)
            {
                if ((buffer.Read().Flags & WinAudio.Done) == 0) WinAudio.waveOutReset(handle);
                WinAudio.Check(WinAudio.waveOutUnprepareHeader(handle, buffer.Pointer, WinAudio.HeaderSize), "Ausgangspuffer freigeben");
            }
            buffer.Free();
            buffer = null;
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            uint reset = WinAudio.waveOutReset(handle);
            uint close = 0;
            try { ReleaseBuffer(); }
            finally { close = WinAudio.waveOutClose(handle); handle = IntPtr.Zero; }
            WinAudio.Check(reset, "Ausgang stoppen");
            WinAudio.Check(close, "Ausgang schließen");
        }
    }
}
