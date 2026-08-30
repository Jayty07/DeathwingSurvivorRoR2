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

        /// <summary>Damage he has taken since his plates were last whole, in health points.</summary>
        private float damageTaken;

        private float lastHealth;

        internal int platesRemaining => plates;

        /// <summary>How far the damage he has taken has eaten into the plate he is about to lose, 0 to 1.</summary>
        internal float plateWear
        {
            get
            {
                if (plates <= 0 || !health || health.fullCombinedHealth <= 0f)
                {
                    return 0f;
                }

                float perPlate = health.fullCombinedHealth / maxPlates;
                return Mathf.Clamp01(damageTaken % perPlate / perPlate);
            }
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            health = GetComponent<HealthComponent>();
        }

        private void Start()
        {
            lastHealth = health ? health.combinedHealth : 0f;
        }

        private void FixedUpdate()
        {
            if (!health || health.fullCombinedHealth <= 0f)
            {
                return;
            }

            // Damage is accumulated rather than read off his current health, so healing back up cannot
            // rebuild a plate and taking the same quarter twice costs him two.
            float current = health.combinedHealth;
            if (current < lastHealth)
            {
                damageTaken += lastHealth - current;
            }

            lastHealth = current;

            int shed = Mathf.FloorToInt(damageTaken / (health.fullCombinedHealth / maxPlates));
            SetPlates(Mathf.Clamp(maxPlates - shed, 0, maxPlates));
        }

        /// <summary>Rebuilds every plate. Only Dragonflight calls this.</summary>
        internal void Reforge()
        {
            damageTaken = 0f;
            lastHealth = health ? health.combinedHealth : 0f;
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
