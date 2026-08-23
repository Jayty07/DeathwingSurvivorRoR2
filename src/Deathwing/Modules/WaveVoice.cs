using System;
using System.Runtime.InteropServices;

namespace Deathwing.Modules
{
    /// <summary>
    /// Plays 16 bit PCM straight through the operating system's audio device.
    ///
    /// The game ships with Unity's own audio system switched off - it mixes everything through Wwise,
    /// and the log reports an output rate of 0Hz - so an <see cref="UnityEngine.AudioSource"/> is
    /// always silent and <c>AudioClip.SetData</c> is refused outright. Short of authoring a Wwise
    /// bank, handing the samples to the OS is the only route left for a custom sound. There is no 3D
    /// audio down this road, so distance and direction are applied by setting the device's left and
    /// right volume from <see cref="DeathwingVoiceMixer"/>.
    /// </summary>
    internal sealed class WaveVoice
    {
        private const uint waveMapper = 0xFFFFFFFF;
        private const ushort pcmFormat = 1;
        private const uint beginLoop = 0x04;
        private const uint endLoop = 0x08;

        private IntPtr device;
        private IntPtr header;
        private IntPtr samples;

        private WaveVoice()
        {
        }

        /// <summary>Seconds the clip runs for, so a one-shot can be cleaned up when it is done.</summary>
        internal float Duration { get; private set; }

        /// <summary>True while the device is still open.</summary>
        internal bool Open => device != IntPtr.Zero;

        /// <summary>
        /// Starts a clip, or returns null if the OS refuses it - a missing winmm on a non-Windows
        /// install, or no free output device.
        /// </summary>
        internal static WaveVoice Play(byte[] pcm, int channels, int rate, bool loop)
        {
            WaveFormat format = new WaveFormat
            {
                formatTag = pcmFormat,
                channels = (ushort)channels,
                samplesPerSecond = (uint)rate,
                averageBytesPerSecond = (uint)(rate * channels * 2),
                blockAlign = (ushort)(channels * 2),
                bitsPerSample = 16,
                size = 0
            };

            IntPtr device;
            try
            {
                if (waveOutOpen(out device, waveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                {
                    return null;
                }
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }

            WaveVoice voice = new WaveVoice
            {
                device = device,
                samples = Marshal.AllocHGlobal(pcm.Length),
                header = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WaveHeader))),
                Duration = pcm.Length / (float)(2 * channels * rate)
            };

            Marshal.Copy(pcm, 0, voice.samples, pcm.Length);
            Marshal.StructureToPtr(
                new WaveHeader
                {
                    data = voice.samples,
                    bufferLength = (uint)pcm.Length,
                    flags = loop ? beginLoop | endLoop : 0,
                    loops = loop ? uint.MaxValue : 0
                },
                voice.header,
                false);

            uint size = (uint)Marshal.SizeOf(typeof(WaveHeader));
            if (waveOutPrepareHeader(device, voice.header, size) != 0
                || waveOutWrite(device, voice.header, size) != 0)
            {
                voice.Stop();
                return null;
            }

            return voice;
        }

        /// <summary>Sets each speaker's level, which is how distance and direction are applied.</summary>
        internal void SetVolume(float left, float right)
        {
            if (device == IntPtr.Zero)
            {
                return;
            }

            uint packed = ((uint)(Clamp(right) * ushort.MaxValue) << 16) | (uint)(Clamp(left) * ushort.MaxValue);
            waveOutSetVolume(device, packed);
        }

        /// <summary>Stops playback and releases the device and both unmanaged buffers.</summary>
        internal void Stop()
        {
            if (device != IntPtr.Zero)
            {
                waveOutReset(device);
                if (header != IntPtr.Zero)
                {
                    waveOutUnprepareHeader(device, header, (uint)Marshal.SizeOf(typeof(WaveHeader)));
                }

                waveOutClose(device);
                device = IntPtr.Zero;
            }

            if (header != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(header);
                header = IntPtr.Zero;
            }

            if (samples != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(samples);
                samples = IntPtr.Zero;
            }
        }

        private static float Clamp(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
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
        private static extern int waveOutSetVolume(IntPtr device, uint volume);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr device);
    }
}
