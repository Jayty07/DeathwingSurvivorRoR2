using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// His trait. He wears four plates of elementium, each worth a slab of armor, and loses one for every
    /// quarter of his health that is taken off him. Nothing gives them back except landing from
    /// Dragonflight - healing does not - so his defence degrades over a fight and has to be flown back.
    /// </summary>
    public class AspectOfDeath : MonoBehaviour
    {
        internal const int maxPlates = 4;

        private CharacterBody body;
        private HealthComponent health;
        private int plates = maxPlates;

        /// <summary>
        /// The lowest fraction of his health he has been down to since his plates were last whole. It is
        /// held at that low mark rather than following his health back up, because healing does not
        /// rebuild a plate; only landing from Dragonflight does.
        /// </summary>
        private float lowMark = 1f;

        internal int platesRemaining => plates;

        /// <summary>How far into the plate he is about to lose his health has fallen, 0 to 1.</summary>
        internal float plateWear => plates <= 0
            ? 0f
            : Mathf.Repeat((1f - lowMark) * maxPlates, 1f);

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            health = GetComponent<HealthComponent>();
        }

        private void Start()
        {
            lowMark = HealthFraction();
        }

        private void FixedUpdate()
        {
            if (!health || health.fullHealth <= 0f)
            {
                return;
            }

            // Plates come off at quarters of his health bar and nothing else. Totalling the damage done
            // to him also counted barrier, shields and the overkill on a big hit, so an item that gave
            // him barrier stripped plates his health says he still has.
            lowMark = Mathf.Min(lowMark, HealthFraction());

            int shed = Mathf.FloorToInt((1f - lowMark) * maxPlates);
            SetPlates(Mathf.Clamp(maxPlates - shed, 0, maxPlates));
        }

        /// <summary>His health alone: barrier and shields are not what the plates are measured against.</summary>
        private float HealthFraction() => health && health.fullHealth > 0f
            ? Mathf.Clamp01(health.health / health.fullHealth)
            : 1f;

        /// <summary>Rebuilds every plate. Only Dragonflight calls this.</summary>
        internal void Reforge()
        {
            lowMark = HealthFraction();
            SetPlates(maxPlates);
        }

        private void SetPlates(int count)
        {
            if (count == plates)
            {
                return;
            }

            plates = count;
            if (body)
            {
                body.MarkAllStatsDirty();
            }
        }
    }
}
