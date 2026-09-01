using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Deathwing.Modules
{
    /// <summary>
    /// Plays 16 bit PCM straight through the operating system's audio device.
    ///
    /// The game ships with Unity's own audio system switched off - it mixes everything through Wwise,
    /// and the log reports an output rate of 0Hz - so an <see cref="UnityEngine.AudioSource"/> is
    /// always silent and <c>AudioClip.SetData</c> is refused outright. Short of authoring a Wwise
    /// bank, handing the samples to the OS is the only route left for a custom sound.
    ///
    /// Every voice is mixed into <see cref="WaveOutput"/>'s single stereo device rather than opening
    /// one of its own. Two reasons: a device per sound runs out during a fight, and
    /// <c>waveOutSetVolume</c> is documented to act on the device rather than the handle on drivers
    /// that do not support per-stream volume - which silenced the game's own audio whenever a voice
    /// was attenuated for distance. Distance and direction are applied to the samples instead.
    /// </summary>
    internal sealed class WaveVoice
    {
        private readonly byte[] pcm;
        private readonly int channels;
        private readonly int frames;

        /// <summary>Source frames consumed per output frame, so any clip rate can be played.</summary>
        private readonly double step;

        private readonly bool loop;

        private double position;
        private float left = 1f;
        private float right = 1f;

        private WaveVoice(byte[] pcm, int channels, int rate, bool loop)
        {
            this.pcm = pcm;
            this.channels = channels;
            this.loop = loop;
            frames = pcm.Length / (2 * channels);
            step = rate / (double)WaveOutput.rate;
            Duration = frames / (float)rate;
        }

        /// <summary>Seconds the clip runs for, so a one-shot can be cleaned up when it is done.</summary>
        internal float Duration { get; }

        /// <summary>True while the voice still has samples left to play.</summary>
        internal bool Open { get; private set; } = true;

        /// <summary>True for a sustained sound, which is kept when the mix has to make room.</summary>
        internal bool Looping => loop;

        /// <summary>
        /// Starts a clip, or returns null if the OS refuses it - a missing winmm on a non-Windows
        /// install, or no output device at all.
        /// </summary>
        internal static WaveVoice Play(byte[] pcm, int channels, int rate, bool loop)
        {
            if (pcm == null || pcm.Length < 2 * channels || rate <= 0)
            {
                return null;
            }

            WaveVoice voice = new WaveVoice(pcm, channels, rate, loop);
            return WaveOutput.Add(voice) ? voice : null;
        }

        /// <summary>Sets each speaker's level, which is how distance and direction are applied.</summary>
        internal void SetVolume(float left, float right)
        {
            // Written without a lock: the mixer thread reads each of these as a single float, so the
            // worst a race can do is mix one buffer with one channel's old level.
            this.left = Clamp(left);
            this.right = Clamp(right);
        }

        /// <summary>Ends the voice. Safe to call on one that has already finished.</summary>
        internal void Stop()
        {
            Open = false;
            WaveOutput.Remove(this);
        }

        /// <summary>
        /// Adds this voice's contribution to a stereo output buffer, advancing its own playhead.
        /// Runs on the mixer thread.
        /// </summary>
        internal void Mix(float[] output)
        {
            for (int i = 0; i < output.Length; i += 2)
            {
                if (position >= frames)
                {
                    if (!loop)
                    {
                        Open = false;
                        return;
                    }

                    position -= frames;
                }

                int frame = (int)position;
                int sample = frame * channels;
                float mono = BitConverter.ToInt16(pcm, sample * 2) / 32768f;
                float other = channels > 1 ? BitConverter.ToInt16(pcm, (sample + 1) * 2) / 32768f : mono;

                output[i] += mono * left;
                output[i + 1] += other * right;
                position += step;
            }
        }

        private static float Clamp(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }

    /// <summary>
    /// The mod's single output device: one stereo stream that every voice is mixed into, refilled by
    /// its own thread so a frame spike in the game cannot starve it.
    /// </summary>
    internal static class WaveOutput
    {
        /// <summary>Output rate. Fixed rather than negotiated; clips are resampled to it.</summary>
        internal const int rate = 44100;

        private const uint waveMapper = 0xFFFFFFFF;
        private const ushort pcmFormat = 1;
        private const uint headerDone = 0x01;
        private const int channels = 2;
        private const int bufferCount = 4;

        /// <summary>Frames per buffer. Four of these is ~93ms queued, which the thread refills well ahead of.</summary>
        private const int bufferFrames = 1024;

        /// <summary>Voices past this are dropped oldest first, so a fight cannot bury the mix.</summary>
        private const int maxVoices = 16;

        private static readonly List<WaveVoice> voices = new List<WaveVoice>();
        private static readonly object gate = new object();

        private static IntPtr device;
        private static Buffer[] buffers;
        private static Thread thread;
        private static bool running;
        private static bool refused;

        /// <summary>Takes on a voice, opening the device on first use. False if the OS has no device for us.</summary>
        internal static bool Add(WaveVoice voice)
        {
            lock (gate)
            {
                if (!Open())
                {
                    return false;
                }

                while (voices.Count >= maxVoices)
                {
                    // The oldest one-shot goes first: a looping voice is something being held down, like
                    // the flame breath, and cutting that off is far more obvious than losing an impact.
                    int index = voices.FindIndex(voice => !voice.Looping);
                    if (index < 0)
                    {
                        index = 0;
                    }

                    WaveVoice dropped = voices[index];
                    voices.RemoveAt(index);
                    dropped.Stop();
                }

                voices.Add(voice);
                return true;
            }
        }

        internal static void Remove(WaveVoice voice)
        {
            lock (gate)
            {
                voices.Remove(voice);
            }
        }

        /// <summary>Closes the device and stops the mixer thread, for when the game shuts down.</summary>
        internal static void Shutdown()
        {
            Thread mixer;
            lock (gate)
            {
                voices.Clear();
                running = false;
                mixer = thread;
                thread = null;
            }

            mixer?.Join(200);

            lock (gate)
            {
                if (device == IntPtr.Zero)
                {
                    return;
                }

                waveOutReset(device);
                uint size = (uint)Marshal.SizeOf(typeof(WaveHeader));
                foreach (Buffer buffer in buffers)
                {
                    waveOutUnprepareHeader(device, buffer.header, size);
                    Marshal.FreeHGlobal(buffer.header);
                    Marshal.FreeHGlobal(buffer.samples);
                }

                waveOutClose(device);
                device = IntPtr.Zero;
                buffers = null;
            }
        }

        private static bool Open()
        {
            if (device != IntPtr.Zero)
            {
                return true;
            }

            if (refused)
            {
                return false;
            }

            WaveFormat format = new WaveFormat
            {
                formatTag = pcmFormat,
                channels = channels,
                samplesPerSecond = rate,
                averageBytesPerSecond = rate * channels * 2,
                blockAlign = channels * 2,
                bitsPerSample = 16,
                size = 0
            };

            try
            {
                if (waveOutOpen(out device, waveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                {
                    device = IntPtr.Zero;
                    refused = true;
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                refused = true;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                refused = true;
                return false;
            }

            int bytes = bufferFrames * channels * 2;
            uint size = (uint)Marshal.SizeOf(typeof(WaveHeader));
            buffers = new Buffer[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                Buffer buffer = new Buffer
                {
                    samples = Marshal.AllocHGlobal(bytes),
                    header = Marshal.AllocHGlobal((int)size),
                    pcm = new byte[bytes],
                    mix = new float[bufferFrames * channels]
                };

                Marshal.StructureToPtr(
                    new WaveHeader { data = buffer.samples, bufferLength = (uint)bytes },
                    buffer.header,
                    false);
                waveOutPrepareHeader(device, buffer.header, size);
                buffers[i] = buffer;
                buffer.queued = Write(buffer);
            }

            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "DeathwingVoiceOutput" };
            thread.Start();
            return true;
        }

        /// <summary>
        /// Keeps the device fed. Silence is written when nothing is playing rather than stopping, so a
        /// new voice never waits on the device restarting.
        /// </summary>
        private static void Run()
        {
            while (true)
            {
                bool wrote = false;
                lock (gate)
                {
                    if (!running || device == IntPtr.Zero)
                    {
                        return;
                    }

                    foreach (Buffer buffer in buffers)
                    {
                        if (buffer.queued && !Done(buffer))
                        {
                            continue;
                        }

                        Fill(buffer);
                        buffer.queued = Write(buffer);
                        wrote = true;
                    }
                }

                if (!wrote)
                {
                    Thread.Sleep(2);
                }
            }
        }

        private static void Fill(Buffer buffer)
        {
            Array.Clear(buffer.mix, 0, buffer.mix.Length);

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                WaveVoice voice = voices[i];
                voice.Mix(buffer.mix);
                if (!voice.Open)
                {
                    voices.RemoveAt(i);
                }
            }

            for (int i = 0; i < buffer.mix.Length; i++)
            {
                float value = buffer.mix[i];
                short sample = (short)(value <= -1f ? short.MinValue
                    : value >= 1f ? short.MaxValue
                    : value * short.MaxValue);
                buffer.pcm[i * 2] = (byte)(sample & 0xFF);
                buffer.pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
        }

        private static bool Write(Buffer buffer)
        {
            Marshal.Copy(buffer.pcm, 0, buffer.samples, buffer.pcm.Length);
            uint size = (uint)Marshal.SizeOf(typeof(WaveHeader));
            return waveOutWrite(device, buffer.header, size) == 0;
        }

        private static bool Done(Buffer buffer)
        {
            WaveHeader header = (WaveHeader)Marshal.PtrToStructure(buffer.header, typeof(WaveHeader));
            return (header.flags & headerDone) != 0;
        }

        /// <summary>One of the device's queued blocks, with the managed staging it is mixed in.</summary>
        private class Buffer
        {
            internal IntPtr samples;
            internal IntPtr header;
            internal byte[] pcm;
            internal float[] mix;
            internal bool queued;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WaveFormat
        {
            public ushort formatTag;
            public ushort channels;
            public uint samplesPerSecond;
            public uint averageBytesPerSecond;
            public ushort blockAlign;
            public ushort bitsPerSample;
            public ushort size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr data;
            public uint bufferLength;
            public uint bytesRecorded;
            public IntPtr user;
            public uint flags;
            public uint loops;
            public IntPtr next;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format,
            IntPtr callback, IntPtr instance, uint flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr device);
    }
}
