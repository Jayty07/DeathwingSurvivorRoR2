using R2API;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Builds the survivor's body prefab. The commando body is used as a chassis so that everything a
    /// playable survivor needs (networking, state machines, ragdoll, footsteps, camera rig) is already
    /// wired up; the clone is then rescaled, restatted, retinted and given Deathwing's hitboxes.
    /// Swap the model out by dropping a real mesh into <see cref="ReplaceModel"/>.
    /// </summary>
    internal static class DeathwingBody
    {
        internal const string bodyPrefabName = "DeathwingBody";
        internal const string clawHitBoxGroupName = "DeathwingClaw";
        internal const string chargeHitBoxGroupName = "DeathwingCharge";

        internal static GameObject bodyPrefab;
        internal static GameObject displayPrefab;

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
            body.portraitIcon = DeathwingAssets.Load<Texture>("RoR2/Base/Commando/texCommandoIcon.png");

            body.autoCalculateLevelStats = false;
            body.baseMaxHealth = Tuning.baseHealth.Value;
            body.levelMaxHealth = Tuning.levelHealth.Value;
            body.baseRegen = 1.5f;
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
            // screen and hides his own attacks.
            CharacterCameraParams cameraParams = ScriptableObject.CreateInstance<CharacterCameraParams>();
            cameraParams.name = "ccpDeathwing";
            cameraParams.data = cameraTargetParams.cameraParams ? cameraTargetParams.cameraParams.data : CharacterCameraParamsData.basic;
            cameraParams.data.idealLocalCameraPos = new HG.BlendableTypes.BlendableVector3
            {
                value = new Vector3(0f, 3.2f, -16f),
                alpha = 1f
            };
            cameraParams.data.pivotVerticalOffset = new HG.BlendableTypes.BlendableFloat
            {
                value = 2.2f,
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
            modelTransform.localScale = Vector3.one * Tuning.modelScale.Value;

            ReplaceModel(modelTransform);
            AddHitBoxes(modelTransform);
        }

        /// <summary>
        /// Applies Deathwing's look to whatever model is present. With the stock chassis this means
        /// tinting the borrowed mesh molten black-and-orange; point this at a custom mesh to use one.
        /// </summary>
        private static void ReplaceModel(Transform modelTransform)
        {
            CharacterModel characterModel = modelTransform.GetComponent<CharacterModel>();
            if (!characterModel)
            {
                return;
            }

            // Skins would re-apply the borrowed survivor's materials on spawn and overwrite the tint.
            Object.Destroy(modelTransform.GetComponent<ModelSkinController>());

            Material template = null;
            foreach (CharacterModel.RendererInfo rendererInfo in characterModel.baseRendererInfos)
            {
                if (rendererInfo.defaultMaterial)
                {
                    template = rendererInfo.defaultMaterial;
                    break;
                }
            }

            DeathwingAssets.CreateMaterials(template);
            if (!DeathwingAssets.moltenSkinMaterial)
            {
                return;
            }

            CharacterModel.RendererInfo[] rendererInfos = characterModel.baseRendererInfos;
            for (int i = 0; i < rendererInfos.Length; i++)
            {
                rendererInfos[i].defaultMaterial = i % 2 == 0 ? DeathwingAssets.rockSkinMaterial : DeathwingAssets.moltenSkinMaterial;
                rendererInfos[i].defaultMaterialAddress = null;
                if (rendererInfos[i].renderer)
                {
                    rendererInfos[i].renderer.sharedMaterial = rendererInfos[i].defaultMaterial;
                }
            }

            characterModel.baseRendererInfos = rendererInfos;
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

            // The menu only needs the mesh and its idle animation; gameplay logic on the clone would
            // otherwise run without a body to belong to. Animator is not a MonoBehaviour, so it stays.
            // ChildLocator is kept because components like FootstepHandler declare it as required, and
            // Unity refuses to remove a component another one depends on.
            foreach (MonoBehaviour behaviour in display.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is CharacterModel || behaviour is ChildLocator)
                {
                    continue;
                }

                Object.Destroy(behaviour);
            }

            return display;
        }
    }
}
