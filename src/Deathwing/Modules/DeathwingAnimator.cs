using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// The model's animation clips, named as they are in the source rig. They are Deathwing's own
    /// Heroes of the Storm ability animations, so each skill has a clip authored for it.
    /// </summary>
    internal static class DeathwingClips
    {
        internal const string jawBoneName = "Bone_Jaw_Main_00_M";

        internal const string idle = "Stand";
        internal const string idleReady = "Stand Ready";
        internal const string walk = "Walk A";
        internal const string clawA = "Attack A";
        internal const string clawB = "Attack B";
        internal const string clawC = "Attack C";
        internal const string breathStart = "Spell A Start";
        internal const string breathLoop = "Spell A";
        internal const string breathEnd = "Spell A End";
        internal const string boulder = "Spell D";
        internal const string chargeStart = "Spell C Start";
        internal const string charge = "Spell C";
        internal const string flightStart = "Spell H Start";
        internal const string flightLoop = "Spell H";
        internal const string flightLand = "Spell H End";
        internal const string dive = "Spell Z End";
        internal const string cataclysm = "Spell J";
        internal const string death = "Death";
        internal const string taunt = "Taunt";
    }

    /// <summary>
    /// Drives the real model's clips. The chassis' own <see cref="Animator"/> still runs the borrowed
    /// skeleton the hitboxes and footsteps hang off, but it knows nothing about this rig, so skills
    /// ask for clips by name here and locomotion is chosen from the body's own movement state.
    /// </summary>
    public class DeathwingAnimator : MonoBehaviour
    {
        /// <summary>Speed the walk clip was authored at, in metres per second of the built model.</summary>
        private const float authoredWalkSpeed = 3f;
        private const float locomotionFade = 0.2f;

        private Animation legacyAnimation;
        private CharacterBody body;
        private CharacterMotor motor;

        private string overrideClip;
        private float overrideEnd;
        private bool overrideHeld;
        private string playing;

        private void Awake()
        {
            legacyAnimation = GetComponent<Animation>();
            body = GetComponentInParent<CharacterBody>();
            motor = body ? body.characterMotor : null;
        }

        /// <summary>Plays a clip once, stretched to the skill's duration, then returns to locomotion.</summary>
        internal void PlayOnce(string clip, float duration, float fade = 0.1f)
        {
            if (!Play(clip, fade, WrapMode.ClampForever, duration))
            {
                return;
            }

            overrideClip = clip;
            overrideEnd = Time.time + duration;
            overrideHeld = false;
        }

        /// <summary>Loops a clip until <see cref="Release"/> is called, for held and channelled skills.</summary>
        internal void PlayHeld(string clip, float fade = 0.1f)
        {
            if (!Play(clip, fade, WrapMode.Loop, 0f))
            {
                return;
            }

            overrideClip = clip;
            overrideEnd = 0f;
            overrideHeld = true;
        }

        /// <summary>Hands control back to locomotion, optionally after a closing clip.</summary>
        internal void Release(string closingClip = null, float duration = 0f)
        {
            overrideHeld = false;
            overrideClip = null;

            if (!string.IsNullOrEmpty(closingClip) && duration > 0f)
            {
                PlayOnce(closingClip, duration);
            }
        }

        private bool Play(string clip, float fade, WrapMode wrapMode, float duration)
        {
            if (!legacyAnimation || legacyAnimation.GetClip(clip) == null)
            {
                return false;
            }

            AnimationState state = legacyAnimation[clip];
            state.wrapMode = wrapMode;
            // A 1.5s swipe clip has to land inside a 0.6s skill, so the clip is retimed rather than
            // truncated; that keeps the whole motion visible and the impact roughly on the beat.
            state.speed = duration > 0f && state.length > 0f ? state.length / duration : 1f;

            legacyAnimation.CrossFade(clip, fade);
            playing = clip;
            return true;
        }

        private void Update()
        {
            if (overrideHeld)
            {
                return;
            }

            if (!string.IsNullOrEmpty(overrideClip))
            {
                if (Time.time < overrideEnd)
                {
                    return;
                }

                overrideClip = null;
            }

            string clip = Locomotion(out float speed);
            if (clip != playing)
            {
                Play(clip, locomotionFade, WrapMode.Loop, 0f);
            }

            if (legacyAnimation && legacyAnimation.GetClip(clip) != null)
            {
                legacyAnimation[clip].speed = speed;
            }
        }

        private string Locomotion(out float speed)
        {
            speed = 1f;

            // The menu's display model has no body, and only it plays the relaxed stand: in a run he
            // holds his combat-ready pose instead.
            if (!body)
            {
                return DeathwingClips.idle;
            }

            if (motor && !motor.isGrounded)
            {
                // Wings out and gliding covers both flight and an ordinary fall.
                return DeathwingClips.flightLoop;
            }

            Vector3 velocity = motor ? motor.velocity : Vector3.zero;
            float groundSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            if (groundSpeed > 0.6f)
            {
                speed = Mathf.Clamp(groundSpeed / authoredWalkSpeed, 0.4f, 3f);
                return DeathwingClips.walk;
            }

            return DeathwingClips.idleReady;
        }
    }
}
