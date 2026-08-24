using System.Collections.Generic;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Bellowing Roar's rout. Risk of Rain 2 has no Fear, so this is the closest honest equivalent: the
    /// monster's brain is switched off for the duration and it is walked directly away from Deathwing,
    /// which stops it attacking and scatters it, without pretending to be Heroes of the Storm's Fear.
    /// </summary>
    public class DeathwingFear : MonoBehaviour
    {
        private readonly List<BaseAI> silencedBrains = new List<BaseAI>();

        private CharacterBody body;
        private CharacterMotor motor;
        private CharacterDirection direction;
        private Vector3 fleeFrom;
        private float endTime;

        /// <summary>Routs a monster away from a point, refreshing the rout if it is already fleeing.</summary>
        internal static void Apply(CharacterBody target, Vector3 fleeFrom, float duration)
        {
            if (!target || !target.characterMotor || target.isPlayerControlled)
            {
                return;
            }

            DeathwingFear fear = target.GetComponent<DeathwingFear>();
            if (!fear)
            {
                fear = target.gameObject.AddComponent<DeathwingFear>();
            }

            fear.fleeFrom = fleeFrom;
            fear.endTime = Mathf.Max(fear.endTime, Time.time + duration);
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            motor = GetComponent<CharacterMotor>();
            direction = GetComponent<CharacterDirection>();

            if (body && body.master)
            {
                foreach (BaseAI brain in body.master.GetComponents<BaseAI>())
                {
                    if (brain.enabled)
                    {
                        brain.enabled = false;
                        silencedBrains.Add(brain);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (Time.time >= endTime)
            {
                Destroy(this);
                return;
            }

            if (!motor)
            {
                return;
            }

            Vector3 away = transform.position - fleeFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = transform.forward;
            }

            away = away.normalized;
            motor.moveDirection = away;
            if (direction)
            {
                direction.moveVector = away;
            }
        }

        private void OnDestroy()
        {
            foreach (BaseAI brain in silencedBrains)
            {
                if (brain)
                {
                    brain.enabled = true;
                }
            }

            silencedBrains.Clear();

            if (motor)
            {
                motor.moveDirection = Vector3.zero;
            }
        }
    }
}
