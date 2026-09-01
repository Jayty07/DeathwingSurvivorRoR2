using Deathwing.Modules;
using EntityStates;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His X. Swapping between Destroyer and World Breaker is instant, but it is still a skill so that
    /// it has a cooldown the HUD can draw and cannot be strobed every frame.
    /// </summary>
    public class SwitchForm : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();

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
