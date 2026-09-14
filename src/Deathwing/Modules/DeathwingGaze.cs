using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Turns his head to where the player is looking. The aim is shared out along the neck and head so
    /// the whole neck curves rather than the skull snapping round, and it is layered over whatever
    /// clip is playing so the walk and idles keep their own motion underneath. Rotations are applied
    /// about world axes at each joint, so nothing is assumed about which way the rig's bones point.
    /// </summary>
    public class DeathwingGaze : MonoBehaviour
    {
        private const float maxYaw = 70f;
        private const float maxPitch = 40f;
        private const float turnSpeed = 6f;

        private static readonly string[] chainNames =
        {
            "Bone_Neck_Main_00_M",
            "Bone_Neck_Main_01_M",
            "Bone_Head_Main_00_M",
        };

        private static readonly float[] chainWeights = { 0.3f, 0.3f, 0.4f };

        private Transform[] chain;
        private CharacterBody body;
        private DeathwingAnimator animator;
        private float yaw;
        private float pitch;

        private void Awake()
        {
            animator = GetComponent<DeathwingAnimator>();
            body = GetComponentInParent<CharacterBody>();

            chain = new Transform[chainNames.Length];
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < chainNames.Length; i++)
                {
                    if (child.name == chainNames[i])
                    {
                        chain[i] = child;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            float weight = Tuning.headTracking?.Value ?? 1f;
            if (!body)
            {
                return;
            }

            // Eased back to the clip's own pose rather than held wherever he last looked when a skill
            // takes the animation over or tracking is switched off.
            bool tracking = weight > 0f && (!animator || animator.InLocomotion);
            float targetYaw = 0f;
            float targetPitch = 0f;
            if (tracking)
            {
                Aim(out targetYaw, out targetPitch);
                targetYaw *= weight;
                targetPitch *= weight;
            }

            float blend = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            yaw = Mathf.Lerp(yaw, targetYaw, blend);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            if (Mathf.Abs(yaw) < 0.01f && Mathf.Abs(pitch) < 0.01f)
            {
                return;
            }

            Vector3 up = body.transform.up;
            Vector3 facing = Facing();
            Vector3 right = Vector3.Cross(up, facing).normalized;
            for (int i = 0; i < chain.Length; i++)
            {
                Transform joint = chain[i];
                if (!joint)
                {
                    continue;
                }

                // Pitch is taken about the direction his gaze has already turned to, so a look to the
                // side and down nods along the line of sight rather than rolling the head.
                Quaternion turn = Quaternion.AngleAxis(yaw * chainWeights[i], up);
                joint.rotation = turn * Quaternion.AngleAxis(pitch * chainWeights[i], turn * right) * joint.rotation;
            }
        }

        /// <summary>
        /// Which way he is facing. The body's transform never turns - his facing is the direction the
        /// model is spun to - so it is read from that, not the body.
        /// </summary>
        private Vector3 Facing()
        {
            if (body.characterDirection)
            {
                return body.characterDirection.forward;
            }

            return transform.forward;
        }

        /// <summary>How far off his body's facing the player is aiming, clamped to what a neck can do.</summary>
        private void Aim(out float targetYaw, out float targetPitch)
        {
            Vector3 forward = Facing();
            Vector3 aim = body.inputBank ? body.inputBank.aimDirection : forward;
            Vector3 up = body.transform.up;

            Vector3 flatAim = Vector3.ProjectOnPlane(aim, up);
            targetYaw = flatAim.sqrMagnitude > 1e-6f
                ? Mathf.Clamp(Vector3.SignedAngle(forward, flatAim, up), -maxYaw, maxYaw)
                : 0f;
            targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(Vector3.Dot(aim, up), -1f, 1f)) * Mathf.Rad2Deg,
                -maxPitch, maxPitch);
        }
    }
}
