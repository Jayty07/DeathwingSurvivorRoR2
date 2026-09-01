using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>Shared plumbing for Deathwing's skills: fire-flavoured blasts and effect helpers.</summary>
    public abstract class BaseDeathwingSkillState : BaseSkillState
    {
        /// <summary>Deathwing is a large character, so effects and hitboxes are scaled with him.</summary>
        protected float characterScale => Tuning.modelScale?.Value ?? 1f;

        private DeathwingAnimator dragonAnimator;
        private bool dragonAnimatorResolved;

        /// <summary>
        /// The real model's animation driver, if he is wearing it. Resolved once and cached: skills ask
        /// for it every activation, and it never moves for the life of the body.
        /// </summary>
        protected DeathwingAnimator dragon
        {
            get
            {
                if (!dragonAnimatorResolved)
                {
                    dragonAnimatorResolved = true;
                    Transform model = modelLocator ? modelLocator.modelTransform : null;
                    dragonAnimator = model ? model.GetComponentInChildren<DeathwingAnimator>() : null;
                }

                return dragonAnimator;
            }
        }

        private DeathwingCustomModel customModel;
        private bool customModelResolved;

        /// <summary>The dragon's renderers, if he is wearing the real model. Resolved once, as above.</summary>
        protected DeathwingCustomModel dragonModel
        {
            get
            {
                if (!customModelResolved)
                {
                    customModelResolved = true;
                    Transform model = modelLocator ? modelLocator.modelTransform : null;
                    customModel = model ? model.GetComponent<DeathwingCustomModel>() : null;
                }

                return customModel;
            }
        }

        /// <summary>Draws or stops drawing the dragon. Visual only: nothing else about him changes.</summary>
        protected void SetModelHidden(bool hidden)
        {
            if (dragonModel)
            {
                dragonModel.SetHidden(hidden);
            }
        }

        /// <summary>
        /// Plays one of the real model's clips, falling back to the chassis animator's own state when
        /// the model is not loaded, so both the real dragon and the placeholder mesh animate.
        /// </summary>
        protected void PlayDragonAnimation(string clip, float duration, string layer, string state, string playbackRateParam = null)
        {
            if (dragon)
            {
                dragon.PlayOnce(clip, duration);
                return;
            }

            if (string.IsNullOrEmpty(playbackRateParam))
            {
                PlayCrossfade(layer, state, 0.1f);
                return;
            }

            PlayCrossfade(layer, state, playbackRateParam, duration, 0.1f);
        }

        protected BlastAttack CreateFireBlast(Vector3 position, float radius, float damageCoefficient, float force = 0f)
        {
            return new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                attackerFiltering = AttackerFiltering.NeverHitSelf,
                baseDamage = damageStat * damageCoefficient,
                baseForce = force,
                position = position,
                radius = radius,
                procCoefficient = 1f,
                crit = RollCrit(),
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.IgniteOnHit,
                falloffModel = BlastAttack.FalloffModel.None,
                losType = BlastAttack.LoSType.NearestHit
            };
        }

        protected void SpawnFireEffect(Vector3 position, float scale)
        {
            DeathwingAssets.SpawnEffect(DeathwingAssets.explosionEffect, position, scale, gameObject);
        }

        /// <summary>
        /// Kicks the camera so Deathwing's hits land with the weight of a dragon. The shake is emitted
        /// into the world with a radius rather than applied to the local camera, so other players in a
        /// lobby feel his impacts too, falling off with distance.
        /// </summary>
        protected void ShakeCamera(Vector3 position, float magnitude, float duration, float radius)
        {
            GameObject emitterObject = new GameObject("DeathwingShakeEmitter");
            emitterObject.transform.position = position;

            ShakeEmitter emitter = emitterObject.AddComponent<ShakeEmitter>();
            emitter.wave = new Wave
            {
                amplitude = magnitude,
                frequency = 40f,
                cycleOffset = 0f
            };
            emitter.duration = duration;
            emitter.radius = radius;
            emitter.amplitudeTimeDecay = true;
            emitter.shakeOnStart = true;

            Object.Destroy(emitterObject, duration + 0.5f);
        }

        /// <summary>Projects a point onto the ground so ground-based effects do not float or sink.</summary>
        protected static Vector3 GroundPosition(Vector3 position, float maxDrop = 30f)
        {
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, maxDrop + 3f, LayerIndex.world.mask))
            {
                return hit.point;
            }

            return position;
        }
    }
}
