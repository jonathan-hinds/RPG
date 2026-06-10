using System.Text;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOWorldHoverTooltipPresenter : MonoBehaviour
    {
        [SerializeField] private Camera hoverCamera;
        [SerializeField] private LayerMask hoverMask = ~0;
        [SerializeField, Min(1f)] private float maxHoverDistance = 250f;

        private Image background;
        private Text nameText;
        private Text titleText;
        private Text levelText;
        private Image healthBackground;
        private Image healthFill;
        private Text healthText;
        private Text questText;
        private CanvasGroup canvasGroup;
        private MMOQuestLog questLog;

        private void Awake()
        {
            BuildIfNeeded();
            ResolveReferences();
            SetVisible(false);
        }

        private void OnEnable()
        {
            SetVisible(false);
        }

        private void Update()
        {
            ResolveReferences();
            RefreshHover();
        }

        public void Configure(Camera newHoverCamera, MMOQuestLog newQuestLog)
        {
            hoverCamera = newHoverCamera;
            questLog = newQuestLog;
            BuildIfNeeded();
            SetVisible(false);
        }

        public void RebuildVisuals()
        {
            MMOUiFactory.DestroyChildren(transform);
            nameText = null;
            titleText = null;
            levelText = null;
            healthBackground = null;
            healthFill = null;
            healthText = null;
            questText = null;
            BuildIfNeeded();
            SetVisible(false);
        }

        private void ResolveReferences()
        {
            if (hoverCamera == null)
            {
                hoverCamera = MMORuntimeSceneReferences.MainCamera;
            }

            if (questLog == null)
            {
                MMORuntimeSceneReferences.TryGetPlayerComponent(out questLog);
            }
        }

        private void RefreshHover()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || hoverCamera == null)
            {
                SetVisible(false);
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                SetVisible(false);
                return;
            }

            Ray ray = hoverCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, maxHoverDistance, hoverMask, QueryTriggerInteraction.Collide))
            {
                SetVisible(false);
                return;
            }

            if (!TryBuildTooltip(hit.collider, out HoverTooltipData tooltipData))
            {
                SetVisible(false);
                return;
            }

            ApplyTooltip(tooltipData);
        }

        private bool TryBuildTooltip(Collider hoveredCollider, out HoverTooltipData tooltipData)
        {
            tooltipData = default;
            if (hoveredCollider == null)
            {
                return false;
            }

            MMOQuestWorldInteractable worldObject = hoveredCollider.GetComponentInParent<MMOQuestWorldInteractable>();
            if (worldObject != null)
            {
                tooltipData = BuildWorldObjectTooltip(worldObject);
                return true;
            }

            MMOCharacterIdentity identity = hoveredCollider.GetComponentInParent<MMOCharacterIdentity>();
            if (identity != null)
            {
                tooltipData = BuildCharacterTooltip(identity);
                return true;
            }

            tooltipData = new HoverTooltipData
            {
                Name = hoveredCollider.gameObject.name
            };
            return true;
        }

        private HoverTooltipData BuildCharacterTooltip(MMOCharacterIdentity identity)
        {
            HoverTooltipData tooltipData = new()
            {
                Name = identity.DisplayName,
                Title = GetCharacterTitle(identity),
                LevelText = $"Level {identity.Level}",
                HasHealth = identity.Health.MaxValue > 0,
                HealthCurrent = identity.Health.CurrentValue,
                HealthMax = identity.Health.MaxValue,
                QuestContext = BuildCharacterQuestContext(identity)
            };

            return tooltipData;
        }

        private HoverTooltipData BuildWorldObjectTooltip(MMOQuestWorldInteractable worldObject)
        {
            return new HoverTooltipData
            {
                Name = worldObject.DisplayName,
                QuestContext = BuildWorldObjectQuestContext(worldObject)
            };
        }

        private string BuildCharacterQuestContext(MMOCharacterIdentity identity)
        {
            if (questLog == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            MMOQuestNpc npc = identity.GetComponent<MMOQuestNpc>();
            if (npc != null)
            {
                AppendNpcQuestContext(builder, npc);
            }

            MMOEnemyController enemy = identity.GetComponent<MMOEnemyController>();
            if (enemy != null)
            {
                AppendEnemyQuestContext(builder, enemy);
            }

            return builder.ToString();
        }

        private string BuildWorldObjectQuestContext(MMOQuestWorldInteractable worldObject)
        {
            if (questLog == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.RequiredWorldObjectId != worldObject.WorldObjectId || state.GetProgress(i) >= objective.RequiredCount)
                    {
                        continue;
                    }

                    int remaining = objective.RequiredCount - state.GetProgress(i);
                    if (objective.ObjectiveType == MMOQuestObjectiveType.CollectQuestItem && objective.RequiredItem == worldObject.LootItem)
                    {
                        AppendQuestLine(builder, quest, $"Loot {objective.RequiredItem.DisplayName}: {remaining} remaining");
                    }
                    else if (objective.ObjectiveType == MMOQuestObjectiveType.UseItemOnWorldObject)
                    {
                        string itemName = objective.UsableItem != null ? objective.UsableItem.DisplayName : "quest item";
                        AppendQuestLine(builder, quest, $"Use {itemName} here: {remaining} remaining");
                    }
                }
            }

            return builder.ToString();
        }

        private void AppendNpcQuestContext(StringBuilder builder, MMOQuestNpc npc)
        {
            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                if (quest.TurnedInToNpcId == npc.NpcId && questLog.IsReadyToTurnIn(state))
                {
                    AppendQuestLine(builder, quest, "Ready to turn in");
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.ObjectiveType != MMOQuestObjectiveType.SpeakToNpc
                        || objective.RequiredNpcId != npc.NpcId
                        || state.GetProgress(i) >= objective.RequiredCount)
                    {
                        continue;
                    }

                    AppendQuestLine(builder, quest, $"Speak to {npc.DisplayName}: needed");
                }
            }
        }

        private void AppendEnemyQuestContext(StringBuilder builder, MMOEnemyController enemy)
        {
            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (state.GetProgress(i) >= objective.RequiredCount)
                    {
                        continue;
                    }

                    bool matchesCreature = objective.RequiredCreature != null && objective.RequiredCreature == enemy.Definition;
                    if (matchesCreature && objective.ObjectiveType == MMOQuestObjectiveType.KillCreature)
                    {
                        int remaining = objective.RequiredCount - state.GetProgress(i);
                        AppendQuestLine(builder, quest, $"Kill objective: {remaining} remaining");
                    }

                    if (matchesCreature && IsCollectObjective(objective) && objective.RequiredItem != null)
                    {
                        int remaining = objective.RequiredCount - state.GetProgress(i);
                        AppendQuestLine(builder, quest, $"Drops {objective.RequiredItem.DisplayName}: {remaining} remaining");
                    }
                }
            }

            AppendLootTableQuestContext(builder, enemy);
        }

        private void AppendLootTableQuestContext(StringBuilder builder, MMOEnemyController enemy)
        {
            MMOLootTable lootTable = enemy.Definition != null ? enemy.Definition.LootTable : null;
            if (lootTable == null)
            {
                return;
            }

            foreach (MMOLootTableEntry entry in lootTable.Entries)
            {
                if (entry == null || entry.Item == null)
                {
                    continue;
                }

                foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
                {
                    MMOQuestDefinition quest = state.Quest;
                    if (quest == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < quest.Objectives.Count; i++)
                    {
                        MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                        if (!IsCollectObjective(objective)
                            || objective.RequiredItem != entry.Item
                            || state.GetProgress(i) >= objective.RequiredCount)
                        {
                            continue;
                        }

                        int remaining = objective.RequiredCount - state.GetProgress(i);
                        AppendQuestLine(builder, quest, $"Loot {entry.Item.DisplayName}: {remaining} remaining");
                    }
                }
            }
        }

        private static bool IsCollectObjective(MMOQuestObjectiveDefinition objective)
        {
            return objective != null
                && (objective.ObjectiveType == MMOQuestObjectiveType.CollectItem
                    || objective.ObjectiveType == MMOQuestObjectiveType.CollectQuestItem);
        }

        private static void AppendQuestLine(StringBuilder builder, MMOQuestDefinition quest, string context)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(quest.DisplayName);
            builder.Append(": ");
            builder.Append(context);
        }

        private void ApplyTooltip(HoverTooltipData tooltipData)
        {
            BuildIfNeeded();
            nameText.text = tooltipData.Name;
            titleText.text = tooltipData.Title;
            titleText.enabled = !string.IsNullOrWhiteSpace(tooltipData.Title);
            levelText.text = tooltipData.LevelText;
            levelText.enabled = !string.IsNullOrWhiteSpace(tooltipData.LevelText);

            bool hasHealth = tooltipData.HasHealth && tooltipData.HealthMax > 0;
            healthBackground.enabled = hasHealth;
            healthFill.enabled = hasHealth;
            healthText.enabled = hasHealth;
            if (hasHealth)
            {
                float normalized = Mathf.Clamp01(tooltipData.HealthCurrent / (float)tooltipData.HealthMax);
                healthFill.rectTransform.anchorMax = new Vector2(normalized, 1f);
                healthText.text = $"{tooltipData.HealthCurrent}/{tooltipData.HealthMax}";
            }

            questText.text = tooltipData.QuestContext;
            questText.enabled = !string.IsNullOrWhiteSpace(tooltipData.QuestContext);

            int questLineCount = string.IsNullOrWhiteSpace(tooltipData.QuestContext) ? 0 : tooltipData.QuestContext.Split('\n').Length;
            float height = 58f + (!string.IsNullOrWhiteSpace(tooltipData.Title) ? 18f : 0f) + (hasHealth ? 28f : 0f) + questLineCount * 18f;
            ((RectTransform)transform).sizeDelta = new Vector2(330f, Mathf.Clamp(height, 58f, 220f));
            SetVisible(true);
        }

        private void BuildIfNeeded()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-24f, 132f);
            root.sizeDelta = new Vector2(330f, 90f);

            background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.02f, 0.016f, 0.012f, 0.96f);
            background.raycastTarget = false;

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (nameText == null && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
                titleText = null;
                levelText = null;
                healthBackground = null;
                healthFill = null;
                healthText = null;
                questText = null;
            }

            if (nameText == null)
            {
                nameText = CreateText("Name", 15, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(10f, -8f), new Vector2(-20f, 22f));
            }

            if (levelText == null)
            {
                levelText = CreateText("Level", 12, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(10f, -30f), new Vector2(-20f, 18f));
            }

            if (titleText == null)
            {
                titleText = CreateText("Title", 12, FontStyle.Italic, TextAnchor.UpperLeft, new Vector2(10f, -48f), new Vector2(-20f, 18f));
                titleText.color = new Color(1f, 0.82f, 0.34f, 1f);
            }

            if (healthBackground == null)
            {
                healthBackground = MMOUiFactory.CreateImage("Health Bar Background", transform, new Color(0.12f, 0.025f, 0.02f, 1f), false);
                ConfigureRect(healthBackground.rectTransform, new Vector2(10f, -70f), new Vector2(-20f, 18f));
            }

            if (healthFill == null)
            {
                healthFill = MMOUiFactory.CreateImage("Health Bar Fill", healthBackground.transform, new Color(0.04f, 0.62f, 0.08f, 1f), false);
                healthFill.rectTransform.anchorMin = Vector2.zero;
                healthFill.rectTransform.anchorMax = Vector2.one;
                healthFill.rectTransform.offsetMin = Vector2.zero;
                healthFill.rectTransform.offsetMax = Vector2.zero;
            }

            if (healthText == null)
            {
                healthText = CreateText("Health Text", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(10f, -70f), new Vector2(-20f, 18f));
            }

            if (questText == null)
            {
                questText = CreateText("Quest Context", 12, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(10f, -96f), new Vector2(-20f, 128f));
            }
        }

        private static string GetCharacterTitle(MMOCharacterIdentity identity)
        {
            MMOStandardNpcIdentity standardNpc = identity.GetComponent<MMOStandardNpcIdentity>();
            if (standardNpc == null)
            {
                return string.Empty;
            }

            return standardNpc.Role == MMONpcIdentityRole.Vendor || standardNpc.Role == MMONpcIdentityRole.Trainer
                ? standardNpc.Title
                : string.Empty;
        }

        private Text CreateText(string objectName, int fontSize, FontStyle style, TextAnchor alignment, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            Text text = MMOUiFactory.CreateText(objectName, transform, fontSize, style, alignment);
            text.color = Color.white;
            ConfigureRect(text.rectTransform, anchoredPosition, sizeDelta);
            return text;
        }

        private static void ConfigureRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private void SetVisible(bool visible)
        {
            BuildIfNeeded();
            canvasGroup.alpha = visible ? 1f : 0f;
            background.enabled = visible;
                nameText.enabled = visible;
            if (!visible)
            {
                levelText.enabled = false;
                titleText.enabled = false;
                healthBackground.enabled = false;
                healthFill.enabled = false;
                healthText.enabled = false;
                questText.enabled = false;
                nameText.text = string.Empty;
                levelText.text = string.Empty;
                titleText.text = string.Empty;
                healthText.text = string.Empty;
                questText.text = string.Empty;
            }
        }

        private struct HoverTooltipData
        {
            public string Name;
            public string Title;
            public string LevelText;
            public bool HasHealth;
            public int HealthCurrent;
            public int HealthMax;
            public string QuestContext;
        }
    }
}
