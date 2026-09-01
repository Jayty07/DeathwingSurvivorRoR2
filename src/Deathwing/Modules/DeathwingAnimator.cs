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
        internal const string takeoff = "Spell Z Start";
        internal const string flightLoop = "Spell H";
        internal const string flightLand = "Spell H End";
        internal const string dive = "Spell Z End";
        internal const string airborne = "Spell B";
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
        /// <summary>
        /// Ground speed the walk clip was authored at, in rig units per second: the source model states
        /// it on the sequence itself. Multiplied by the model's world scale it gives the metres per
        /// second his stride actually covers, which is what keeps his feet in step with the ground -
        /// a fixed figure makes the cycle race at a size the rig was never drawn at.
        /// </summary>
        private const float authoredWalkUnits = 270f;
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

        /// <summary>
        /// Authored length of a clip in seconds, or 0 if the rig has no such clip. States that have to
        /// hold him still for exactly as long as an animation runs ask for it rather than guessing.
        /// </summary>
        internal float ClipLength(string clip)
        {
            AnimationClip found = legacyAnimation ? legacyAnimation.GetClip(clip) : null;
            return found ? found.length : 0f;
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
                // A jump or a fall is wings thrown open to catch himself, not the soaring cycle:
                // beating his way across the sky reads as flight, which he is not doing.
                return DeathwingClips.airborne;
            }

            Vector3 velocity = motor ? motor.velocity : Vector3.zero;
            float groundSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            if (groundSpeed > 0.6f)
            {
                float stride = authoredWalkUnits * Mathf.Max(transform.lossyScale.x, 1e-5f);
                speed = Mathf.Clamp(groundSpeed / stride * Tuning.walkCycleSpeed.Value, 0.25f, 3f);
                return DeathwingClips.walk;
            }

            return DeathwingClips.idleReady;
        }
    }
}
