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
