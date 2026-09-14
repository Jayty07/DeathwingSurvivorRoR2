using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His X. Swapping between Destroyer and World Breaker takes effect at once, but he plays his
    /// transformation clip and flares molten while it runs so the change can be seen, as it is in
    /// Heroes. The clip does not hold him: he can move and cast straight through it.
    /// </summary>
    public class SwitchForm : BaseDeathwingSkillState
    {
        public static float shiftDuration = 1.2f;

        public override void OnEnter()
        {
            base.OnEnter();

            PlayDragonAnimation(DeathwingClips.formShift, shiftDuration, "Gesture, Override", "ThrowGrenade",
                "ThrowGrenade.playbackRate");
            dragonModel?.Silhouette(shiftDuration);
            Util.PlaySound(Sounds.chargeStart, gameObject);
            DeathwingEffects.DustPuff(characterBody ? characterBody.footPosition : transform.position,
                6f * characterScale);

            if (isAuthority)
            {
                DeathwingForms forms = GetComponent<DeathwingForms>();
                if (forms)
                {
                    forms.Toggle();
                }

                outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Any;
    }
}
