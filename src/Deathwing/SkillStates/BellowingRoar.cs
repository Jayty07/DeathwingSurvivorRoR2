using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His second heroic. A second of drawing breath, then a roar that routs everything around him. The
    /// damage is incidental; the point is that nothing nearby is fighting him for a while. Risk of Rain 2
    /// has no Fear, so the rout is done by taking the monsters' brains off them and walking them away.
    /// </summary>
    public class BellowingRoar : BaseDeathwingSkillState
    {
        public static float baseWindupDuration = 1f;
        public static float recoveryDuration = 0.6f;
        public static float radius = 30f;
        public static float fearDuration = 1.5f;
        public static float force = 900f;

        private float windupDuration;
        private bool roared;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;

            Util.PlaySound(Sounds.cataclysmChannel, gameObject);
            DeathwingVoice.Play(DeathwingVoice.bellowingRoar, gameObject, 1f, false, 140f);
            PlayDragonAnimation(DeathwingClips.taunt, windupDuration, "Gesture, Override", "ThrowGrenade",
                "ThrowGrenade.playbackRate");
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor && fixedAge < windupDuration)
            {
                characterMotor.velocity = new Vector3(0f, characterMotor.velocity.y, 0f);
                characterMotor.moveDirection = Vector3.zero;
            }

            if (!roared && fixedAge >= windupDuration)
            {
                roared = true;
                Roar();
            }

            if (isAuthority && fixedAge >= windupDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void Roar()
        {
            Vector3 center = characterBody ? characterBody.corePosition : transform.position;
            float scaledRadius = radius * characterScale;

            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, center, 5f * characterScale, gameObject);
            ShakeCamera(center, 9f, 0.8f, scaledRadius + 40f);

            if (!isAuthority)
            {
                return;
            }

            BlastAttack blast = CreateFireBlast(
                center, scaledRadius, Tuning.bellowingRoarDamageCoefficient.Value, force);
            blast.bonusForce = Vector3.up * (force * 0.3f);
            blast.Fire();

            Rout(center, scaledRadius);
        }

        /// <summary>Sends every monster caught in the roar running, for as long as his Fear lasts.</summary>
        private void Rout(Vector3 center, float scaledRadius)
        {
            TeamIndex team = GetTeam();
            float sqrRadius = scaledRadius * scaledRadius;

            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(
                team == TeamIndex.Player ? TeamIndex.Monster : TeamIndex.Player))
            {
                CharacterBody target = teamComponent ? teamComponent.body : null;
                if (!target || (target.corePosition - center).sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                DeathwingFear.Apply(target, center, fearDuration);
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
