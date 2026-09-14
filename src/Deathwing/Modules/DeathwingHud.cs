using System.Text;
using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deathwing.Modules
{
    /// <summary>
    /// Draws the four abilities that have no slot - Dragonflight, his form toggle and his two heroics -
    /// as a row above the ordinary skill bar. The icons are clones of the game's own skill icon, so they
    /// keep its frame, scaling and placement, but they are driven from Deathwing's off-slot skills
    /// rather than from a slot the HUD knows about.
    ///
    /// One of these lives on every HUD, but it only builds and shows the row while that HUD is looking
    /// at a Deathwing: in a lobby the other players, and anyone spectating him, see nothing.
    /// </summary>
    public class DeathwingHud : MonoBehaviour
    {
        private static readonly Color plateColor = new Color(1f, 0.5f, 0.18f, 0.9f);
        private static readonly Color plateWornColor = new Color(1f, 0.28f, 0.12f, 0.9f);

        private static bool warned;

        private HUD hud;
        private DeathwingForms forms;
        private AspectOfDeath aspect;
        private Entry[] entries;
        private Image[] plates;
        private RectTransform plateRow;
        private bool failed;

        internal static void Init()
        {
            HUD.onHudTargetChangedGlobal += OnHudTargetChanged;
        }

        private static void OnHudTargetChanged(HUD changedHud)
        {
            if (!changedHud || changedHud.GetComponent<DeathwingHud>())
            {
                return;
            }

            changedHud.gameObject.AddComponent<DeathwingHud>().hud = changedHud;
        }

        /// <summary>
        /// Late, so the row is placed after the HUD's own layout has run for the frame and cannot be
        /// left a frame behind when the resolution or the HUD scale changes.
        /// </summary>
        private void LateUpdate()
        {
            if (failed || !hud)
            {
                return;
            }

            GameObject target = hud.targetBodyObject;
            DeathwingForms targetForms = target ? target.GetComponent<DeathwingForms>() : null;

            if (targetForms != forms)
            {
                Teardown();
                forms = targetForms;
                aspect = target ? target.GetComponent<AspectOfDeath>() : null;
            }

            if (!forms)
            {
                return;
            }

            if (entries == null && !Build())
            {
                failed = true;
                return;
            }

            foreach (Entry entry in entries)
            {
                Place(entry);
                Refresh(entry);
            }

            RefreshPlates();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Teardown()
        {
            if (plateRow)
            {
                Destroy(plateRow.gameObject);
            }

            plateRow = null;
            plates = null;

            if (entries == null)
            {
                return;
            }

            foreach (Entry entry in entries)
            {
                if (entry.root)
                {
                    Destroy(entry.root.gameObject);
                }
            }

            entries = null;
        }

        /// <summary>Builds the row from clones of the vanilla icons. False if the HUD has none to clone.</summary>
        private bool Build()
        {
            SkillIcon template = Template();
            if (!template || !template.transform.parent)
            {
                Warn("HUD has no skill icon to build Deathwing's extra abilities from.");
                return false;
            }

            entries = new[]
            {
                Create(template, forms.dragonflightSkill, Tuning.dragonflightKey, DeathwingIcons.dragonflight, true),
                Create(template, forms.formSwitchSkill, Tuning.formSwitchKey, DeathwingIcons.formSwitch, false),
                Create(template, forms.cataclysmSkill, Tuning.heroicCataclysmKey, DeathwingIcons.cataclysm, false),
                Create(template, forms.bellowingRoarSkill, Tuning.bellowingRoarKey, DeathwingIcons.aspectOfDeath, false)
            };

            // Each extra sits directly above one of the vanilla icons where there is one to sit above,
            // so the row lines up with the bar whatever the player's HUD scale is.
            for (int i = 0; i < entries.Length; i++)
            {
                SkillIcon anchor = Icon(i);
                entries[i].anchor = anchor ? (RectTransform)anchor.transform : (RectTransform)template.transform;
                entries[i].anchorIndex = anchor ? 0 : i;
            }

            BuildPlates();

            return true;
        }

        /// <summary>
        /// His plates as a row of pips over the health bar. The plate he is closest to losing empties as
        /// he takes damage, so the trait reads as a second health bar rather than as a number that
        /// silently drops.
        /// </summary>
        private void BuildPlates()
        {
            RectTransform bar = HealthBar();
            if (!aspect || !bar || !bar.parent)
            {
                return;
            }

            plateRow = (RectTransform)new GameObject("DeathwingPlates", typeof(RectTransform)).transform;
            plateRow.SetParent(bar.parent, false);

            plates = new Image[AspectOfDeath.maxPlates];
            for (int i = 0; i < plates.Length; i++)
            {
                RectTransform pip = (RectTransform)new GameObject($"Plate{i}", typeof(RectTransform), typeof(Image)).transform;
                pip.SetParent(plateRow, false);
                pip.pivot = Vector2.zero;
                pip.anchorMin = Vector2.zero;
                pip.anchorMax = Vector2.zero;

                Image backdrop = pip.GetComponent<Image>();
                backdrop.color = new Color(0.07f, 0.05f, 0.05f, 0.75f);
                backdrop.raycastTarget = false;

                RectTransform fillRect = (RectTransform)new GameObject("Fill", typeof(RectTransform), typeof(Image)).transform;
                fillRect.SetParent(pip, false);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                Image fill = fillRect.GetComponent<Image>();
                fill.color = plateColor;
                fill.raycastTarget = false;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;

                plates[i] = fill;
            }
        }

        private RectTransform HealthBar()
        {
            HealthBar bar = hud.healthBar;
            return bar ? (RectTransform)bar.transform : null;
        }

        private void RefreshPlates()
        {
            RectTransform bar = HealthBar();
            if (plates == null || !aspect || !bar)
            {
                return;
            }

            Rect rect = bar.rect;
            float gap = rect.width * 0.008f;
            float width = (rect.width - gap * (plates.Length - 1)) / plates.Length;
            float height = Mathf.Max(2f, rect.height * 0.22f);
            Vector3 topLeft = bar.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0f));
            int remaining = aspect.platesRemaining;
            float wear = aspect.plateWear;

            for (int i = 0; i < plates.Length; i++)
            {
                Image fill = plates[i];
                if (!fill)
                {
                    continue;
                }

                RectTransform pip = (RectTransform)fill.transform.parent;

                // Plates are shed from the right, so the leftmost is the last one standing. A shed plate
                // is taken off the bar outright, backdrop and all: an empty socket left behind reads as a
                // plate he still has, which is the opposite of what the trait is telling him.
                bool intact = i < remaining - 1;
                bool wearing = i == remaining - 1;
                if (pip.gameObject.activeSelf != (intact || wearing))
                {
                    pip.gameObject.SetActive(intact || wearing);
                }

                if (!intact && !wearing)
                {
                    continue;
                }

                pip.sizeDelta = new Vector2(width, height);
                ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
                pip.position = topLeft + bar.TransformVector(new Vector3(i * (width + gap), height * 0.6f, 0f));

                fill.fillAmount = intact ? 1f : 1f - wear;
                fill.color = wearing && wear > 0.75f ? plateWornColor : plateColor;
            }
        }

        private SkillIcon Template()
        {
            SkillIcon special = Icon(3);
            if (special)
            {
                return special;
            }

            for (int i = 0; i < 4; i++)
            {
                SkillIcon icon = Icon(i);
                if (icon)
                {
                    return icon;
                }
            }

            return null;
        }

        private SkillIcon Icon(int index)
        {
            SkillIcon[] icons = hud.skillIcons;
            if (icons == null || index >= icons.Length)
            {
                return null;
            }

            SkillIcon icon = icons[index];
            return icon && icon.transform ? icon : null;
        }

        private Entry Create(
            SkillIcon template, GenericSkill skill, ConfigEntry<KeyboardShortcut> key, string iconName, bool isFlight)
        {
            GameObject clone = Instantiate(template.gameObject, template.transform.parent);
            clone.name = $"DeathwingExtraSkill{iconName}";

            Transform templateRoot = template.transform;
            Transform cloneRoot = clone.transform;

            Entry entry = new Entry
            {
                skill = skill,
                key = key,
                isFlight = isFlight,
                root = (RectTransform)cloneRoot,
                icon = Match(templateRoot, cloneRoot, template.iconImage),
                cooldownText = Match(templateRoot, cloneRoot, template.cooldownText),
                keyText = null,
                tooltip = Match(templateRoot, cloneRoot, template.tooltipProvider),
                readyPanel = Match(templateRoot, cloneRoot, template.isReadyPanelObject)
            };

            // The keybind label under the icon is driven by the game from the slot the template lives in,
            // so left alone every clone reads as the Special key. Taken over, and its driver removed so it
            // cannot write over the binding these are actually on.
            foreach (InputBindingDisplayController binding in
                clone.GetComponentsInChildren<InputBindingDisplayController>(true))
            {
                if (!entry.keyText && binding.guiLabel)
                {
                    entry.keyText = binding.guiLabel;
                }

                Destroy(binding);
            }

            if (!entry.keyText)
            {
                entry.keyText = Match(templateRoot, cloneRoot, template.stockText);
            }

            // The vanilla component would immediately blank everything out: it reads its slot from the
            // skill locator, and these abilities are in no slot. The row is driven by hand instead.
            SkillIcon cloneIcon = clone.GetComponent<SkillIcon>();
            if (cloneIcon)
            {
                Destroy(cloneIcon);
            }

            Animator animator = clone.GetComponent<Animator>();
            if (animator)
            {
                animator.enabled = false;
            }

            GameObject flash = Match(templateRoot, cloneRoot, template.flashPanelObject);
            if (flash)
            {
                flash.SetActive(false);
            }

            RawImage remap = Match(templateRoot, cloneRoot, template.cooldownRemapPanel);
            if (remap)
            {
                remap.enabled = false;
            }

            // Layout groups on the skill bar would drag the clones back into the bar itself.
            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (!layout)
            {
                layout = clone.AddComponent<LayoutElement>();
            }

            layout.ignoreLayout = true;

            if (entry.icon)
            {
                entry.icon.sprite = DeathwingIcons.Get(iconName, entry.icon.sprite);
                entry.sweep = CreateSweep(entry.icon);
            }

            if (entry.keyText)
            {
                entry.keyText.enableWordWrapping = false;
                entry.keyText.text = key.Value.MainKey.ToString();
            }

            if (entry.cooldownText)
            {
                entry.cooldownText.text = string.Empty;
            }

            if (entry.tooltip && skill && skill.skillDef)
            {
                entry.tooltip.titleToken = skill.skillDef.skillNameToken;
                entry.tooltip.bodyToken = skill.skillDef.skillDescriptionToken;
            }

            return entry;
        }

        /// <summary>
        /// The darkening sweep over an icon on cooldown. Vanilla drives a remap texture through a
        /// material this mod has no handle on, so the clones get a plain radial fill of their own.
        /// </summary>
        private static Image CreateSweep(Image icon)
        {
            GameObject sweepObject = new GameObject("Cooldown", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)sweepObject.transform;
            RectTransform iconRect = (RectTransform)icon.transform;

            rect.SetParent(iconRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image sweep = sweepObject.GetComponent<Image>();
            sweep.sprite = icon.sprite;
            sweep.color = new Color(0f, 0f, 0f, 0.75f);
            sweep.type = Image.Type.Filled;
            sweep.fillMethod = Image.FillMethod.Radial360;
            sweep.fillOrigin = (int)Image.Origin360.Top;
            sweep.fillClockwise = false;
            sweep.raycastTarget = false;
            sweep.enabled = false;

            return sweep;
        }

        private void Place(Entry entry)
        {
            if (!entry.root || !entry.anchor)
            {
                return;
            }

            float height = entry.anchor.rect.height;
            float width = entry.anchor.rect.width;

            // Above the bar, and to the right of the anchor by a slot for each icon that had no vanilla
            // icon of its own to stand on.
            Vector3 offset = entry.anchor.TransformVector(
                new Vector3(entry.anchorIndex * width * 1.1f, height * (1f + Tuning.extraRowGap.Value), 0f));

            entry.root.position = entry.anchor.position + offset;
        }

        private void Refresh(Entry entry)
        {
            if (!entry.skill)
            {
                return;
            }

            float cooldown = entry.skill.cooldownRemaining;
            bool hasStock = entry.skill.stock > 0;
            bool locked = entry.isFlight && forms.flightLockoutRemaining > 0f;
            float shown = locked ? Mathf.Max(cooldown, forms.flightLockoutRemaining) : cooldown;
            bool ready = hasStock && shown <= 0f;

            // The longest wait seen since it was last ready is the sweep's full circle: the game keeps
            // the interval it recharges over to itself.
            if (shown > entry.longestWait)
            {
                entry.longestWait = shown;
            }
            else if (shown <= 0f)
            {
                entry.longestWait = 0f;
            }

            if (entry.sweep)
            {
                entry.sweep.enabled = shown > 0f;
                entry.sweep.fillAmount = entry.longestWait > 0f ? Mathf.Clamp01(shown / entry.longestWait) : 0f;
            }

            if (entry.cooldownText)
            {
                entry.cooldownText.text = shown > 0f ? shown.ToString(shown >= 10f ? "0" : "0.0") : string.Empty;
            }

            if (entry.icon)
            {
                // Barred by his own blood rather than by a cooldown, so it reads differently.
                entry.icon.color = ready ? Color.white
                    : locked ? new Color(1f, 0.45f, 0.4f, 1f)
                    : new Color(0.55f, 0.55f, 0.55f, 1f);

                if (entry.skill == forms.formSwitchSkill)
                {
                    // Shows the form it would put him in, which is the thing worth knowing.
                    entry.icon.sprite = DeathwingIcons.Get(
                        forms.currentForm == DeathwingForms.Form.Destroyer
                            ? DeathwingIcons.worldBreaker
                            : DeathwingIcons.destroyer,
                        entry.icon.sprite);
                }
            }

            if (entry.readyPanel)
            {
                entry.readyPanel.SetActive(ready);
            }

            if (entry.keyText)
            {
                entry.keyText.text = entry.key.Value.MainKey.ToString();
            }
        }

        /// <summary>Finds a clone's copy of one of the template's parts, by where it sits in the template.</summary>
        private static T Match<T>(Transform templateRoot, Transform cloneRoot, T templatePart) where T : Component
        {
            Transform found = Match(templateRoot, cloneRoot, templatePart ? templatePart.transform : null);
            return found ? found.GetComponent<T>() : null;
        }

        private static GameObject Match(Transform templateRoot, Transform cloneRoot, GameObject templatePart)
        {
            Transform found = Match(templateRoot, cloneRoot, templatePart ? templatePart.transform : null);
            return found ? found.gameObject : null;
        }

        private static Transform Match(Transform templateRoot, Transform cloneRoot, Transform templatePart)
        {
            if (!templatePart)
            {
                return null;
            }

            if (templatePart == templateRoot)
            {
                return cloneRoot;
            }

            StringBuilder path = new StringBuilder();
            for (Transform step = templatePart; step && step != templateRoot; step = step.parent)
            {
                if (path.Length > 0)
                {
                    path.Insert(0, '/');
                }

                path.Insert(0, step.name);

                if (!step.parent)
                {
                    return null;
                }
            }

            return cloneRoot.Find(path.ToString());
        }

        private static void Warn(string message)
        {
            if (warned)
            {
                return;
            }

            warned = true;
            Log.Warning(message);
        }

        private class Entry
        {
            internal GenericSkill skill;
            internal ConfigEntry<KeyboardShortcut> key;
            internal bool isFlight;
            internal RectTransform root;
            internal RectTransform anchor;
            internal int anchorIndex;
            internal Image icon;
            internal Image sweep;
            internal TextMeshProUGUI cooldownText;
            internal TextMeshProUGUI keyText;
            internal GameObject readyPanel;
            internal TooltipProvider tooltip;
            internal float longestWait;
        }
    }
}
