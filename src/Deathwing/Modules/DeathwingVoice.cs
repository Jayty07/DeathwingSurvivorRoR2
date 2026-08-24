using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Deathwing's own voice, played over the borrowed Wwise events rather than instead of them: the
    /// vanilla sounds carry the impact, these carry the dragon. The clips ship in the DLL as mono
    /// 16 bit PCM.
    ///
    /// Two routes are kept. Unity's own audio is preferred, since it gives real 3D sound for free -
    /// but this game disables Unity audio entirely (it mixes through Wwise, and reports an output rate
    /// of 0Hz), which makes every <see cref="AudioSource"/> silent. When that is the case the samples
    /// go to the OS audio device instead, mixed by hand in <see cref="DeathwingVoiceMixer"/>.
    /// </summary>
    internal static class DeathwingVoice
    {
        internal const string roar = "Deathwing_Roar";
        internal const string bellowingRoar = "Deathwing_BellowingRoar";
        internal const string distant = "Deathwing_Distant";
        internal const string flameBreath = "Deathwing_FlameBreath";
        internal const string flameLoop = "Deathwing_FlameLoop";
        internal const string taunt = "Deathwing_Taunt";
        /// <summary>Stone impacts, for the weight of a claw or a boulder landing rather than his voice.</summary>
        internal const string stoneImpact = "Deathwing_StoneImpact";

        /// <summary>How many numbered variants each family of clips ships with.</summary>
        private static readonly Dictionary<string, int> variants = new Dictionary<string, int>
        {
            { roar, 4 },
            { distant, 7 },
            { flameBreath, 3 },
            { stoneImpact, 7 }
        };

        /// <summary>
        /// Per-family gain, applied on top of the caller's level. His roars are recorded far hotter than
        /// the rest of the pack, so they are held back here rather than at every call site.
        /// </summary>
        private static readonly Dictionary<string, float> gains = new Dictionary<string, float>
        {
            { roar, 0.6f },
            { bellowingRoar, 0.6f },
            { distant, 0.6f },
            { taunt, 0.6f }
        };

        private static readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();

        private static bool described;
        private static bool unityAudio;

        /// <summary>A sound in flight, and the handle callers pass back to <see cref="Stop"/>.</summary>
        internal class Voice
        {
            internal AudioSource source;
            internal WaveVoice wave;
            internal Transform at;
            internal float volume;
            internal float maxDistance;
            internal float started;
            internal bool loop;
        }

        /// <summary>Plays one clip - or one at random from a numbered family - on a body.</summary>
        internal static Voice Play(string name, GameObject at, float volume = 1f,
            bool loop = false, float rolloffDistance = 60f)
        {
            if (!at || !Tuning.realModelVoice.Value)
            {
                return null;
            }

            Describe();

            Sound sound = Resolve(name);
            if (sound == null)
            {
                return null;
            }

            float gain = gains.TryGetValue(name, out float family) ? family : 1f;
            float level = Mathf.Clamp01(volume * gain * Tuning.realModelVoiceVolume.Value);
            return unityAudio
                ? PlayThroughUnity(sound, at, level, loop, rolloffDistance)
                : PlayThroughDevice(sound, at, level, loop, rolloffDistance);
        }

        /// <summary>Stops and cleans up a looping voice returned by <see cref="Play"/>.</summary>
        internal static void Stop(Voice voice)
        {
            if (voice == null)
            {
                return;
            }

            if (voice.source)
            {
                voice.source.Stop();
                UnityEngine.Object.Destroy(voice.source.gameObject);
            }

            DeathwingVoiceMixer.Remove(voice);
        }

        private static Voice PlayThroughUnity(Sound sound, GameObject at, float level, bool loop,
            float rolloffDistance)
        {
            AudioClip clip = sound.Clip();
            if (!clip)
            {
                return null;
            }

            GameObject speaker = new GameObject("DeathwingVoice");
            speaker.transform.SetParent(at.transform, false);

            AudioSource source = speaker.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = level;
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

            return new Voice { source = source, at = speaker.transform, loop = loop };
        }

        private static Voice PlayThroughDevice(Sound sound, GameObject at, float level, bool loop,
            float rolloffDistance)
        {
            WaveVoice wave = WaveVoice.Play(sound.pcm, sound.channels, sound.rate, loop);
            if (wave == null)
            {
                Log.Warning($"The OS refused to play '{sound.name}'; custom voices will be silent.");
                return null;
            }

            Voice voice = new Voice
            {
                wave = wave,
                at = at.transform,
                volume = level,
                maxDistance = rolloffDistance,
                started = Time.time,
                loop = loop
            };

            DeathwingVoiceMixer.Add(voice);
            return voice;
        }

        /// <summary>
        /// Works out - once - whether Unity's audio system is alive, and says so in the log. A custom
        /// sound goes missing for several reasons that look identical in game, so the state is written
        /// down rather than guessed at.
        /// </summary>
        private static void Describe()
        {
            if (described)
            {
                return;
            }

            described = true;
            unityAudio = AudioSettings.outputSampleRate > 0;
            Log.Info($"Voice audio: Unity output {AudioSettings.outputSampleRate}Hz "
                + $"{AudioSettings.speakerMode}, playing through "
                + (unityAudio ? "Unity" : "the OS audio device"));

            if (!unityAudio)
            {
                return;
            }

            if (!UnityEngine.Object.FindObjectOfType<AudioListener>())
            {
                // Without a listener a Unity source is silent however it is configured, and a game
                // that mixes through Wwise has no reason to carry one.
                Camera camera = Camera.main;
                GameObject host = camera ? camera.gameObject : new GameObject("DeathwingAudioListener");
                host.AddComponent<AudioListener>();
                Log.Info($"Added a Unity audio listener to '{host.name}'.");
            }
        }

        private static Sound Resolve(string name)
        {
            if (variants.TryGetValue(name, out int count))
            {
                name += UnityEngine.Random.Range(1, count + 1);
            }

            if (sounds.TryGetValue(name, out Sound cached))
            {
                return cached;
            }

            Sound sound = Decode(name);
            sounds[name] = sound;
            if (sound == null)
            {
                Log.Warning($"Voice clip '{name}' is not in the DLL; that sound will be silent.");
            }

            return sound;
        }

        private static Sound Decode(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("Deathwing.Sounds." + name + ".wav"))
            {
                if (stream == null)
                {
                    return null;
                }

                using (MemoryStream copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                    return Decode(name, copy.ToArray());
                }
            }
        }

        /// <summary>
        /// Reads a mono or stereo 16 bit PCM RIFF file. Chunks are walked rather than assumed: the
        /// pack's files carry fact and PEAK chunks ahead of the samples.
        /// </summary>
        private static Sound Decode(string name, byte[] wav)
        {
            int channels = 1;
            int rate = 22050;
            int offset = 12;

            while (offset + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, offset, 4);
                int size = BitConverter.ToInt32(wav, offset + 4);
                int body = offset + 8;
                if (size < 0 || body + size > wav.Length)
                {
                    size = wav.Length - body;
                }

                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, body + 2);
                    rate = BitConverter.ToInt32(wav, body + 4);
                }
                else if (id == "data")
                {
                    byte[] pcm = new byte[size];
                    Array.Copy(wav, body, pcm, 0, size);
                    return new Sound { name = name, pcm = pcm, channels = channels, rate = rate };
                }

                offset = body + size + (size & 1);
            }

            return null;
        }

        /// <summary>Decoded samples, kept as bytes so either playback route can use them.</summary>
        private class Sound
        {
            internal string name;
            internal byte[] pcm;
            internal int channels;
            internal int rate;

            private AudioClip clip;

            /// <summary>The samples as a Unity clip, built on first use.</summary>
            internal AudioClip Clip()
            {
                if (clip)
                {
                    return clip;
                }

                int count = pcm.Length / 2;
                float[] data = new float[count];
                for (int i = 0; i < count; i++)
                {
                    data[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
                }

                clip = AudioClip.Create(name, count / channels, channels, rate, false);
                if (!clip.SetData(data, 0))
                {
                    Log.Warning($"Unity refused the samples for '{name}'.");
                }

                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return clip;
            }
        }
    }
}
