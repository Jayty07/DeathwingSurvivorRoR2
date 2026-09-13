using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Flattens his front paws onto the ground. The rig walks him on the balls of his forefeet with the
    /// heel pitched up, so on a flat floor only the claw tips touch; after each frame's animation the
    /// forefoot bone is levelled about its ball so the whole palm sits down, with the claws left where
    /// the clip put them.
    /// </summary>
    public class DeathwingPaws : MonoBehaviour
    {
        private const string leftFoot = "Bone_LegFrt_Foot_00_L";
        private const string leftBall = "Bone_LegFrt_Ball_00_L";
        private const string rightFoot = "Bone_LegFrt_Foot_00_R";
        private const string rightBall = "Bone_LegFrt_Ball_00_R";

        private Transform leftFootBone;
        private Transform leftBallBone;
        private Transform rightFootBone;
        private Transform rightBallBone;
        private DeathwingAnimator animator;

        private void Awake()
        {
            animator = GetComponent<DeathwingAnimator>();
            leftFootBone = Find(leftFoot);
            leftBallBone = Find(leftBall);
            rightFootBone = Find(rightFoot);
            rightBallBone = Find(rightBall);
        }

        private Transform Find(string boneName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == boneName)
                {
                    return child;
                }
            }

            return null;
        }

        private void LateUpdate()
        {
            float weight = Tuning.pawFlatten?.Value ?? 1f;
            if (weight <= 0f || (animator && !animator.InLocomotion))
            {
                return;
            }

            Plant(leftFootBone, leftBallBone, weight);
            Plant(rightFootBone, rightBallBone, weight);
        }

        /// <summary>
        /// Turns the foot about the ball until heel and ball are level along the model's floor, scaled
        /// by <paramref name="weight"/>, then puts the ball back where the clip had it so the claws
        /// stay planted and only the heel comes down.
        /// </summary>
        private void Plant(Transform foot, Transform ball, float weight)
        {
            if (!foot || !ball)
            {
                return;
            }

            Vector3 up = transform.up;
            Vector3 ballPosition = ball.position;
            Vector3 toBall = ballPosition - foot.position;
            Vector3 level = Vector3.ProjectOnPlane(toBall, up);
            if (level.sqrMagnitude < 1e-8f || toBall.sqrMagnitude < 1e-8f)
            {
                return;
            }

            // The heel keeps its distance from the ball, so the foot is not shortened when it levels.
            level = level.normalized * toBall.magnitude;
            Quaternion correction = Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.FromToRotation(toBall, level),
                Mathf.Clamp01(weight));

            foot.rotation = correction * foot.rotation;
            foot.position += ballPosition - ball.position;
        }
    }
}
