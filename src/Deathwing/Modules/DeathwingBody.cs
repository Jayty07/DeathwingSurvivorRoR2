using System;
using R2API;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Builds the survivor's body prefab. The commando body is used as a chassis so that everything a
    /// playable survivor needs (networking, state machines, ragdoll, footsteps, camera rig) is already
    /// wired up; the clone is then rescaled, restatted, retinted and given Deathwing's hitboxes.
    /// The real Deathwing model is then parented under it by <see cref="ReplaceModel"/>.
    /// </summary>
    internal static class DeathwingBody
    {
        internal const string bodyPrefabName = "DeathwingBody";
        internal const string clawHitBoxGroupName = "DeathwingClaw";
        internal const string chargeHitBoxGroupName = "DeathwingCharge";

        /// <summary>Name of the mouth locator the breath is fired from, added when the real model loads.</summary>
        internal const string mouthChildName = "DeathwingMouth";

        internal static GameObject bodyPrefab;
        internal static GameObject displayPrefab;

        /// <summary>True when the real model replaced the placeholder chassis mesh.</summary>
        internal static bool usingRealModel { get; private set; }

        internal static void Init()
        {
            GameObject commandoBody = DeathwingAssets.Load<GameObject>(DeathwingAssets.commandoBodyKey);
            if (!commandoBody)
            {
                Log.Error("Could not load the base body prefab; Deathwing will not be registered.");
                return;
            }

            bodyPrefab = PrefabAPI.InstantiateClone(commandoBody, bodyPrefabName);

            ConfigureBody(bodyPrefab.GetComponent<CharacterBody>());
            ConfigureMotor(bodyPrefab);
            ConfigureCamera(bodyPrefab.GetComponent<CameraTargetParams>());
            ConfigureModel(bodyPrefab.GetComponent<ModelLocator>());
            ConfigureDurability(bodyPrefab);
            bodyPrefab.AddComponent<DeathwingPresence>();
            // His trait and his form toggle: the plates have to exist before the stat hook reads them,
            // and the form component owns the keys his off-slot abilities are cast from.
            bodyPrefab.AddComponent<AspectOfDeath>();
            bodyPrefab.AddComponent<DeathwingForms>();

            displayPrefab = CreateDisplayPrefab();

            ContentAddition.AddBody(bodyPrefab);
        }

        private static void ConfigureBody(CharacterBody body)
        {
            if (!body)
            {
                return;
            }

            body.baseNameToken = Tokens.bodyName;
            body.subtitleNameToken = Tokens.bodySubtitle;
            body.bodyColor = new Color(0.42f, 0.11f, 0.07f);
            body.portraitIcon = DeathwingIcons.GetTexture(
                DeathwingIcons.worldBreaker,
                DeathwingAssets.Load<Texture>("RoR2/Base/Commando/texCommandoIcon.png"));

            body.autoCalculateLevelStats = false;
            body.baseMaxHealth = Tuning.baseHealth.Value;
            body.levelMaxHealth = Tuning.levelHealth.Value;
            // A slow character cannot outrun a bad opening, so the durability he trades speed for has to
            // be there from the first stage rather than waiting on items.
            body.baseRegen = Tuning.baseRegen.Value;
            body.levelRegen = 0.3f;
            body.baseArmor = Tuning.baseArmor.Value;
            body.levelArmor = 1.5f;
            body.baseDamage = Tuning.baseDamage.Value;
            body.levelDamage = Tuning.baseDamage.Value * 0.2f;
            body.baseAttackSpeed = Tuning.baseAttackSpeed.Value;
            body.baseCrit = 1f;
            body.baseMoveSpeed = Tuning.baseMoveSpeed.Value;
            body.baseAcceleration = 25f;
            body.baseJumpPower = 14f;
            body.baseJumpCount = 1;
            body.sprintingSpeedMultiplier = 1.3f;

            // A golem hull keeps spawn pods, pathing and hull-sized interactions honest about his size.
            body.hullClassification = HullClassification.Golem;
            body.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;

            if (!Tuning.dropPod.Value)
            {
                // Without a pod the game places him straight on the stage, which is what his footing is
                // read from; inside the pod there is nothing under him but the pod itself.
                body.preferredPodPrefab = null;
            }
        }

        private static void ConfigureMotor(GameObject prefab)
        {
            float scale = Tuning.modelScale.Value;

            if (prefab.TryGetComponent(out CharacterMotor motor))
            {
                motor.mass = 300f;
                motor.airControl = 0.2f;
                motor.jumpCount = 1;
            }

            if (prefab.TryGetComponent(out KinematicCharacterController.KinematicCharacterMotor kinematicMotor))
            {
                kinematicMotor.SetCapsuleDimensions(0.9f * scale, 3.6f * scale, 1.8f * scale);
            }

            if (prefab.TryGetComponent(out CapsuleCollider capsule))
            {
                capsule.radius = 0.9f * scale;
                capsule.height = 3.6f * scale;
                capsule.center = Vector3.up * (1.8f * scale);
            }
        }

        private static void ConfigureCamera(CameraTargetParams cameraTargetParams)
        {
            if (!cameraTargetParams)
            {
                return;
            }

            // A character this large needs the camera pulled back and raised, otherwise he fills the
            // screen and hides his own attacks. The framing is worked out from the height he is actually
            // built at, so resizing him in the config reframes him too, and each figure can be pinned by
            // hand if the player wants him closer or further out.
            float height = Tuning.useRealModel.Value && Tuning.realModelHeight.Value > 0f
                ? Tuning.realModelHeight.Value
                : 3f * Tuning.modelScale.Value;

            float distance = Tuning.cameraDistance.Value > 0f ? Tuning.cameraDistance.Value : height * 1.8f;
            float pivot = Tuning.cameraPivotHeight.Value > 0f ? Tuning.cameraPivotHeight.Value : height * 0.5f;

            CharacterCameraParams cameraParams = ScriptableObject.CreateInstance<CharacterCameraParams>();
            cameraParams.name = "ccpDeathwing";
            cameraParams.data = cameraTargetParams.cameraParams ? cameraTargetParams.cameraParams.data : CharacterCameraParamsData.basic;
            cameraParams.data.idealLocalCameraPos = new HG.BlendableTypes.BlendableVector3
            {
                value = new Vector3(0f, height * 0.25f, -distance),
                alpha = 1f
            };
            // Raised to the middle of his body, so he sits in the frame rather than towering out of it.
            cameraParams.data.pivotVerticalOffset = new HG.BlendableTypes.BlendableFloat
            {
                value = pivot,
                alpha = 1f
            };

            cameraTargetParams.cameraParams = cameraParams;
        }

        private static void ConfigureModel(ModelLocator modelLocator)
        {
            if (!modelLocator || !modelLocator.modelTransform)
            {
                Log.Warning("Body has no model transform; skipping visual setup.");
                return;
            }

            Transform modelTransform = modelLocator.modelTransform;

            // Scaling the model base rather than the model itself keeps the model's own transform (and
            // with it the aim origin, muzzles and animation root motion) in the layout the chassis
            // expects, while still enlarging everything attached below it.
            Transform scaleTarget = modelLocator.modelBaseTransform ? modelLocator.modelBaseTransform : modelTransform;
            scaleTarget.localScale = Vector3.one * Tuning.modelScale.Value;

            ReplaceModel(modelTransform);
            AddHitBoxes(modelTransform);
        }

        /// <summary>
        /// Gives the body its look: the real Deathwing model when its art payload is available,
        /// otherwise the borrowed chassis mesh tinted molten black-and-orange.
        /// </summary>
        private static void ReplaceModel(Transform modelTransform)
        {
            CharacterModel characterModel = modelTransform.GetComponent<CharacterModel>();
            if (!characterModel)
            {
                return;
            }

            if (Tuning.useRealModel.Value && AttachRealModel(modelTransform))
            {
                usingRealModel = true;
                return;
            }

            // The skin controller is left intact: the chassis relies on it to assign its materials on
            // spawn, so removing it leaves the mesh unrendered. The tint is applied afterwards instead.
            if (!modelTransform.GetComponent<DeathwingTint>())
            {
                modelTransform.gameObject.AddComponent<DeathwingTint>();
            }
        }

        /// <summary>
        /// Parents the real model under the chassis' model transform rather than replacing it: the
        /// chassis skeleton keeps carrying the hurtboxes, footsteps, ragdoll and aim rig a survivor
        /// needs, and merely stops being drawn once <see cref="DeathwingCustomModel"/> hands rendering
        /// over on spawn.
        /// </summary>
        private static bool AttachRealModel(Transform modelTransform)
        {
            GameObject model;
            try
            {
                model = DeathwingModel.Build("mdlDeathwing");
            }
            catch (Exception exception)
            {
                // A half-built dragon must not cost the survivor its body: fall back to the chassis mesh.
                Log.Error($"Could not build the real model, keeping the placeholder: {exception}");
                model = null;
            }

            if (!model)
            {
                return false;
            }

            model.transform.SetParent(modelTransform, false);

            if (!modelTransform.GetComponent<DeathwingCustomModel>())
            {
                modelTransform.gameObject.AddComponent<DeathwingCustomModel>();
            }

            AddMouthLocator(modelTransform, model.transform);
            return true;
        }

        /// <summary>
        /// Publishes the dragon's jaw as a named child so the breath can be sprayed from his mouth. It
        /// gets its own name rather than overwriting the chassis' MuzzleCenter, which borrowed
        /// components still fire from.
        /// </summary>
        private static void AddMouthLocator(Transform modelTransform, Transform model)
        {
            Transform jaw = DeathwingModel.FindMouth(model);
            ChildLocator childLocator = modelTransform.GetComponent<ChildLocator>();
            if (!jaw || !childLocator)
            {
                Log.Warning("No jaw bone or child locator; the breath will be emitted from the body's core.");
                return;
            }

            GameObject mouth = new GameObject(mouthChildName);
            mouth.transform.SetParent(jaw, false);
            // Just past the teeth, in the jaw bone's own (source-rig) units.
            mouth.transform.localPosition = new Vector3(0f, 0f, 40f);

            ChildLocator.NameTransformPair[] pairs = childLocator.transformPairs;
            int count = pairs != null ? pairs.Length : 0;
            ChildLocator.NameTransformPair[] extended = new ChildLocator.NameTransformPair[count + 1];
            for (int i = 0; i < count; i++)
            {
                extended[i] = pairs[i];
            }

            extended[count] = new ChildLocator.NameTransformPair
            {
                name = mouthChildName,
                transform = mouth.transform
            };
            childLocator.transformPairs = extended;
        }

        /// <summary>
        /// Melee hitboxes. Dimensions are pre-scale because the model transform multiplies them by the
        /// character's scale.
        /// </summary>
        private static void AddHitBoxes(Transform modelTransform)
        {
            CreateHitBoxGroup(modelTransform, clawHitBoxGroupName, new Vector3(0f, 0.9f, 1.9f), new Vector3(3.6f, 2.4f, 3.4f));
            CreateHitBoxGroup(modelTransform, chargeHitBoxGroupName, new Vector3(0f, 0.9f, 1.2f), new Vector3(3.2f, 2.6f, 3.2f));
        }

        private static void CreateHitBoxGroup(Transform modelTransform, string groupName, Vector3 localPosition, Vector3 localScale)
        {
            GameObject hitBoxObject = new GameObject(groupName + "HitBox");
            hitBoxObject.transform.SetParent(modelTransform, false);
            hitBoxObject.transform.localPosition = localPosition;
            hitBoxObject.transform.localScale = localScale;
            hitBoxObject.transform.localRotation = Quaternion.identity;

            HitBox hitBox = hitBoxObject.AddComponent<HitBox>();

            HitBoxGroup hitBoxGroup = modelTransform.gameObject.AddComponent<HitBoxGroup>();
            hitBoxGroup.hitBoxes = new[] { hitBox };
            hitBoxGroup.groupName = groupName;
        }

        private static void ConfigureDurability(GameObject prefab)
        {
            // Deathwing shrugs off the flinches that stagger lesser survivors.
            foreach (SetStateOnHurt setStateOnHurt in prefab.GetComponents<SetStateOnHurt>())
            {
                setStateOnHurt.canBeStunned = false;
                setStateOnHurt.canBeFrozen = false;
                setStateOnHurt.canBeHitStunned = false;
            }
        }

        /// <summary>Menu/character-select model. It is a display-only clone with no logic attached.</summary>
        private static GameObject CreateDisplayPrefab()
        {
            ModelLocator modelLocator = bodyPrefab.GetComponent<ModelLocator>();
            if (!modelLocator || !modelLocator.modelTransform)
            {
                return null;
            }

            GameObject display = PrefabAPI.InstantiateClone(modelLocator.modelTransform.gameObject, "DeathwingDisplay", false);
            display.transform.localScale = Vector3.one * Tuning.modelScale.Value * 0.6f;

            // Gameplay logic on the clone would run without a body to belong to, so only the components
            // the menu actually renders through are kept: the model, its skin controller (which assigns
            // the materials), the tint, and ChildLocator, which other components declare as required and
            // Unity therefore refuses to remove. Animator is not a MonoBehaviour, so it stays regardless.
            foreach (MonoBehaviour behaviour in display.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is CharacterModel || behaviour is ModelSkinController
                    || behaviour is ChildLocator || behaviour is DeathwingTint
                    || behaviour is DeathwingCustomModel || behaviour is DeathwingAnimator)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(behaviour);
            }

            return display;
        }
    }
}
