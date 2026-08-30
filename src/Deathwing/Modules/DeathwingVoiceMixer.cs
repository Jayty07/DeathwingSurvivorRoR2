using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Does the job the engine's audio system would have done for the voices played through the OS
    /// device: attenuates them by how far away the dragon is, pans them by which side of the screen he
    /// is on, and drops each one when its clip has finished.
    /// </summary>
    public class DeathwingVoiceMixer : MonoBehaviour
    {
        /// <summary>How many of his sounds may overlap.</summary>
        private const int maxVoices = 16;

        private static readonly List<DeathwingVoice.Voice> playing = new List<DeathwingVoice.Voice>();

        private static DeathwingVoiceMixer instance;

        /// <summary>Takes over a voice, keeping it mixed until it ends or is stopped.</summary>
        internal static void Add(DeathwingVoice.Voice voice)
        {
            if (!instance)
            {
                GameObject host = new GameObject("DeathwingVoiceMixer");
                host.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(host);
                instance = host.AddComponent<DeathwingVoiceMixer>();
            }

            // The oldest one-shot gives way rather than letting a long fight pile his voice up over
            // itself; a looping voice is a sound being held down, so it is only dropped if that is all
            // there is.
            while (playing.Count >= maxVoices)
            {
                int index = playing.FindIndex(other => !other.loop);
                Remove(playing[index < 0 ? 0 : index]);
            }

            playing.Add(voice);
        }

        /// <summary>Stops a voice and forgets it. Safe to call on one that has already ended.</summary>
        internal static void Remove(DeathwingVoice.Voice voice)
        {
            if (voice == null)
            {
                return;
            }

            voice.wave?.Stop();
            playing.Remove(voice);
        }

        private void OnApplicationQuit()
        {
            // The output device is fed by a background thread, which has to be told to stop before the
            // process tears the unmanaged buffers out from under it.
            WaveOutput.Shutdown();
        }

        private void Update()
        {
            Transform listener = ListenerTransform();
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                DeathwingVoice.Voice voice = playing[i];
                bool ended = !voice.loop && Time.time >= voice.started + voice.wave.Duration + 0.1f;
                if (ended || !voice.at || !voice.wave.Open)
                {
                    voice.wave.Stop();
                    playing.RemoveAt(i);
                    continue;
                }

                Mix(voice, listener);
            }
        }

        /// <summary>
        /// Where the player hears from. The scene camera is used rather than the body so that a
        /// spectated or zoomed-out view still puts the dragon at the right distance.
        /// </summary>
        private static Transform ListenerTransform()
        {
            CameraRigController rig = CameraRigController.readOnlyInstancesList.Count > 0
                ? CameraRigController.readOnlyInstancesList[0]
                : null;
            if (rig && rig.sceneCam)
            {
                return rig.sceneCam.transform;
            }

            Camera camera = Camera.main;
            return camera ? camera.transform : null;
        }

        private static void Mix(DeathwingVoice.Voice voice, Transform listener)
        {
            float level = voice.volume;
            float pan = 0f;

            if (listener)
            {
                Vector3 offset = voice.at.position - listener.position;
                float distance = offset.magnitude;
                float far = Mathf.Max(voice.maxDistance, 1f);
                level *= 1f - Mathf.Clamp01((distance - 8f) / far);
                if (distance > 0.01f)
                {
                    pan = Mathf.Clamp(Vector3.Dot(listener.right, offset / distance), -1f, 1f);
                }
            }

            // A constant-power pan, so swinging the camera does not make him louder or quieter.
            voice.wave.SetVolume(level * Mathf.Sqrt((1f - pan) * 0.5f), level * Mathf.Sqrt((1f + pan) * 0.5f));
        }
    }
}
