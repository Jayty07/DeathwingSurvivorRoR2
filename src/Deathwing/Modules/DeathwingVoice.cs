using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Deathwing's own voice, played over the borrowed Wwise events rather than instead of them: the
    /// vanilla sounds carry the impact, these carry the dragon. The clips ship in the DLL as mono
    /// 16 bit PCM and are decoded into <see cref="AudioClip"/>s the first time they are asked for,
    /// which is the only route to a custom sound without an authored Wwise bank.
    /// </summary>
    internal static class DeathwingVoice
    {
        internal const string roar = "Deathwing_Roar";
        internal const string bellowingRoar = "Deathwing_BellowingRoar";
        internal const string distant = "Deathwing_Distant";
        internal const string flameBreath = "Deathwing_FlameBreath";
        internal const string flameLoop = "Deathwing_FlameLoop";
        internal const string taunt = "Deathwing_Taunt";

        /// <summary>How many numbered variants each family of clips ships with.</summary>
        private static readonly Dictionary<string, int> variants = new Dictionary<string, int>
        {
            { roar, 4 },
            { distant, 7 },
            { flameBreath, 3 }
        };

        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        /// <summary>Plays one clip - or one at random from a numbered family - on a body.</summary>
        internal static AudioSource Play(string name, GameObject at, float volume = 1f,
            bool loop = false, float rolloffDistance = 60f)
        {
            if (!at || !Tuning.realModelVoice.Value)
            {
                return null;
            }

            AudioClip clip = Resolve(name);
            if (!clip)
            {
                return null;
            }

            GameObject speaker = new GameObject("DeathwingVoice");
            speaker.transform.SetParent(at.transform, false);

            AudioSource source = speaker.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = Mathf.Clamp01(volume * Tuning.realModelVoiceVolume.Value);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8f;
            source.maxDistance = rolloffDistance;
            source.dopplerLevel = 0f;
            source.Play();

            if (!loop)
            {
                UnityEngine.Object.Destroy(speaker, clip.length + 0.2f);
            }

            return source;
        }

        /// <summary>Stops and cleans up a looping voice returned by <see cref="Play"/>.</summary>
        internal static void Stop(AudioSource source)
        {
            if (!source)
            {
                return;
            }

            source.Stop();
            UnityEngine.Object.Destroy(source.gameObject);
        }

        private static AudioClip Resolve(string name)
        {
            if (variants.TryGetValue(name, out int count))
            {
                name += Random.Range(1, count + 1);
            }

            if (clips.TryGetValue(name, out AudioClip cached))
            {
                return cached;
            }

            AudioClip clip = Decode(name);
            clips[name] = clip;
            if (!clip)
            {
                Log.Warning($"Voice clip '{name}' is not in the DLL; that sound will be silent.");
            }

            return clip;
        }

        private static AudioClip Decode(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("Deathwing.Sounds." + name + ".wav"))
            {
                if (stream == null)
                {
                    return null;
                }

                byte[] wav = new byte[stream.Length];
                stream.Read(wav, 0, wav.Length);
                return Decode(name, wav);
            }
        }

        /// <summary>
        /// Reads a mono or stereo 16 bit PCM RIFF file. Chunks are walked rather than assumed: the
        /// pack's files carry fact and PEAK chunks ahead of the samples.
        /// </summary>
        private static AudioClip Decode(string name, byte[] wav)
        {
            int channels = 1;
            int frequency = 22050;
            int offset = 12;

            while (offset + 8 <= wav.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(wav, offset, 4);
                int size = System.BitConverter.ToInt32(wav, offset + 4);
                int body = offset + 8;
                if (size < 0 || body + size > wav.Length)
                {
                    size = wav.Length - body;
                }

                if (id == "fmt ")
                {
                    channels = System.BitConverter.ToInt16(wav, body + 2);
                    frequency = System.BitConverter.ToInt32(wav, body + 4);
                }
                else if (id == "data")
                {
                    int samples = size / 2;
                    float[] pcm = new float[samples];
                    for (int i = 0; i < samples; i++)
                    {
                        pcm[i] = System.BitConverter.ToInt16(wav, body + i * 2) / 32768f;
                    }

                    AudioClip clip = AudioClip.Create(name, samples / channels, channels, frequency, false);
                    clip.SetData(pcm, 0);
                    clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    return clip;
                }

                offset = body + size + (size & 1);
            }

            return null;
        }
    }
}
