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

        private static bool described;
        private static bool warned;

        /// <summary>
        /// Turns decoded samples into a clip. The straightforward route - allocate the clip and fill
        /// it with <see cref="AudioClip.SetData(float[], int)"/> - is refused by this game's audio
        /// system ("AudioClip contains no data"), so a clip that streams from the decoded samples
        /// through a read callback is used instead: it never needs a filled buffer.
        /// </summary>
        private static AudioClip Build(string name, float[] pcm, int lengthSamples, int channels, int frequency)
        {
            AudioClip clip = AudioClip.Create(name, lengthSamples, channels, frequency, false);
            if (clip && clip.SetData(pcm, 0))
            {
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return clip;
            }

            if (clip)
            {
                UnityEngine.Object.Destroy(clip);
            }

            int cursor = 0;
            AudioClip streamed = AudioClip.Create(name, lengthSamples, channels, frequency, true,
                data =>
                {
                    int count = Mathf.Min(data.Length, pcm.Length - cursor);
                    for (int i = 0; i < count; i++)
                    {
                        data[i] = pcm[cursor + i];
                    }

                    for (int i = count; i < data.Length; i++)
                    {
                        data[i] = 0f;
                    }

                    cursor += count;
                },
                position => cursor = Mathf.Clamp(position * channels, 0, pcm.Length));

            if (streamed)
            {
                streamed.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }

            return streamed;
        }

        /// <summary>
        /// What the engine's audio system will actually do with a clip, logged once. A custom sound is
        /// silent for several reasons that look identical in game - no Unity listener in a Wwise title,
        /// a refused buffer, a disabled audio device - so the state is written down rather than guessed.
        /// </summary>
        private static void Describe()
        {
            if (described)
            {
                return;
            }

            described = true;
            AudioListener listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
            Log.Info($"Voice audio: output {AudioSettings.outputSampleRate}Hz {AudioSettings.speakerMode}, "
                + $"driver {AudioSettings.driverCapabilities}, listener "
                + (listener ? "on '" + listener.gameObject.name + "'" : "missing"));

            if (listener)
            {
                return;
            }

            // The game mixes through Wwise and so carries no Unity listener; without one, a Unity
            // AudioSource is silent no matter how it is configured.
            Camera camera = Camera.main;
            GameObject host = camera ? camera.gameObject : new GameObject("DeathwingAudioListener");
            host.AddComponent<AudioListener>();
            Log.Info($"Added a Unity audio listener to '{host.name}' so custom voices can be heard.");
        }

        /// <summary>Plays one clip - or one at random from a numbered family - on a body.</summary>
        internal static AudioSource Play(string name, GameObject at, float volume = 1f,
            bool loop = false, float rolloffDistance = 60f)
        {
            if (!at || !Tuning.realModelVoice.Value)
            {
                return null;
            }

            Describe();

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
            if (!source.isPlaying && !warned)
            {
                warned = true;
                Log.Warning($"'{clip.name}' would not play: the engine's Unity audio system is refusing "
                    + "sources, so custom voices need a Wwise bank instead.");
            }

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

                    return Build(name, pcm, samples / channels, channels, frequency);
                }

                offset = body + size + (size & 1);
            }

            return null;
        }
    }
}
