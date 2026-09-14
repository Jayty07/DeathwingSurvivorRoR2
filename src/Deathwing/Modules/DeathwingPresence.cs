using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Announces the Destroyer: his taunt when he arrives on a stage, and one of his distant calls
    /// now and then while he is alive, so the pack's non-combat lines are heard too.
    /// </summary>
    public class DeathwingPresence : MonoBehaviour
    {
        /// <summary>Seconds between distant calls, picked in this range.</summary>
        private const float minInterval = 45f;
        private const float maxInterval = 95f;

        private float nextCall;

        private void OnEnable()
        {
            DeathwingVoice.Play(DeathwingVoice.taunt, gameObject, 0.9f, false, 140f);
            nextCall = Time.time + Random.Range(minInterval, maxInterval);
        }

        private void Update()
        {
            if (Time.time < nextCall)
            {
                return;
            }

            nextCall = Time.time + Random.Range(minInterval, maxInterval);
            DeathwingVoice.Play(DeathwingVoice.distant, gameObject, 0.55f, false, 160f);
        }
    }
}
