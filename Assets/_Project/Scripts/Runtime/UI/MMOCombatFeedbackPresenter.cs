using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOCombatFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private MMOAbilitySystem playerAbilitySystem;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Color outgoingDamageColor = new(1f, 0.82f, 0.2f, 1f);
        [SerializeField] private Color incomingDamageColor = new(1f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color healingColor = new(0.2f, 1f, 0.35f, 1f);
        [SerializeField] private Color errorColor = new(1f, 0.08f, 0.04f, 1f);
        [SerializeField, Min(0.2f)] private float floatingTextLifetime = 1.15f;
        [SerializeField, Min(0.2f)] private float errorTextLifetime = 1.45f;
        [SerializeField, Min(0f)] private float verticalScreenDrift = 58f;
        [SerializeField, Min(0f)] private float combatantScanInterval = 1f;
        [SerializeField, Min(0f)] private float repeatedErrorThrottleSeconds = 0.75f;

        private readonly List<MMOCombatant> subscribedCombatants = new();
        private readonly List<FloatingEntry> activeEntries = new();
        private readonly Stack<Text> textPool = new();
        private RectTransform canvasRect;
        private MMOCharacterIdentity playerIdentity;
        private float nextCombatantScanTime;
        private string lastErrorMessage;
        private float lastErrorTime;
        private static Font cachedFont;

        private void Awake()
        {
            canvasRect = (RectTransform)transform;
            ResolveReferences();
        }

        private void OnEnable()
        {
            SubscribePlayerAbilitySystem();
            ScanCombatants();
        }

        private void OnDisable()
        {
            if (playerAbilitySystem != null)
            {
                playerAbilitySystem.AbilityFailed -= OnAbilityFailed;
            }

            foreach (MMOCombatant combatant in subscribedCombatants)
            {
                if (combatant == null)
                {
                    continue;
                }

                combatant.Damaged -= OnCombatantDamaged;
                combatant.Healed -= OnCombatantHealed;
            }

            subscribedCombatants.Clear();
        }

        private void Update()
        {
            if (Time.time >= nextCombatantScanTime)
            {
                ScanCombatants();
            }

            UpdateEntries();
        }

        public void Configure(MMOAbilitySystem newPlayerAbilitySystem, Camera newWorldCamera)
        {
            if (playerAbilitySystem != null)
            {
                playerAbilitySystem.AbilityFailed -= OnAbilityFailed;
            }

            playerAbilitySystem = newPlayerAbilitySystem;
            worldCamera = newWorldCamera;
            ResolvePlayerIdentity();

            if (isActiveAndEnabled)
            {
                SubscribePlayerAbilitySystem();
            }
        }

        private void ResolveReferences()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (playerAbilitySystem == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerAbilitySystem = player.GetComponent<MMOAbilitySystem>();
                }
            }

            ResolvePlayerIdentity();
        }

        private void ResolvePlayerIdentity()
        {
            playerIdentity = playerAbilitySystem != null ? playerAbilitySystem.Identity : null;
            if (playerIdentity == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerIdentity = player.GetComponent<MMOCharacterIdentity>();
                }
            }
        }

        private void SubscribePlayerAbilitySystem()
        {
            if (playerAbilitySystem == null)
            {
                return;
            }

            playerAbilitySystem.AbilityFailed -= OnAbilityFailed;
            playerAbilitySystem.AbilityFailed += OnAbilityFailed;
        }

        private void ScanCombatants()
        {
            nextCombatantScanTime = Time.time + combatantScanInterval;
            MMOCombatant[] combatants = FindObjectsByType<MMOCombatant>(FindObjectsInactive.Exclude);
            foreach (MMOCombatant combatant in combatants)
            {
                if (combatant == null || subscribedCombatants.Contains(combatant))
                {
                    continue;
                }

                combatant.Damaged += OnCombatantDamaged;
                combatant.Healed += OnCombatantHealed;
                subscribedCombatants.Add(combatant);
            }
        }

        private void OnAbilityFailed(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message == lastErrorMessage && Time.time - lastErrorTime < repeatedErrorThrottleSeconds)
            {
                return;
            }

            lastErrorMessage = message;
            lastErrorTime = Time.time;
            SpawnScreenText(message, errorColor, new Vector2(0f, 205f), errorTextLifetime, 0f);
        }

        private void OnCombatantDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (target == null || target.Identity == null)
            {
                return;
            }

            Color color = target.Identity == playerIdentity ? incomingDamageColor : outgoingDamageColor;
            SpawnWorldText(target.transform, amount.ToString(), color, floatingTextLifetime);
        }

        private void OnCombatantHealed(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (target == null)
            {
                return;
            }

            SpawnWorldText(target.transform, $"+{amount}", healingColor, floatingTextLifetime);
        }

        private void SpawnWorldText(Transform target, string message, Color color, float lifetime)
        {
            Text text = GetText();
            text.text = message;
            text.color = color;
            text.fontSize = 28;

            activeEntries.Add(new FloatingEntry(
                text,
                target,
                new Vector3(0f, 2.25f, 0f),
                Vector2.zero,
                new Vector2(Random.Range(-18f, 18f), verticalScreenDrift),
                color,
                lifetime,
                Time.time));
        }

        private void SpawnScreenText(string message, Color color, Vector2 anchoredPosition, float lifetime, float drift)
        {
            Text text = GetText();
            text.text = message;
            text.color = color;
            text.fontSize = 20;

            activeEntries.Add(new FloatingEntry(
                text,
                null,
                Vector3.zero,
                anchoredPosition,
                new Vector2(0f, drift),
                color,
                lifetime,
                Time.time));
        }

        private void UpdateEntries()
        {
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                FloatingEntry entry = activeEntries[i];
                float age = Time.time - entry.StartTime;
                float normalizedAge = Mathf.Clamp01(age / entry.Lifetime);
                if (normalizedAge >= 1f)
                {
                    Recycle(entry.Text);
                    activeEntries.RemoveAt(i);
                    continue;
                }

                Vector2 basePosition = entry.ScreenPosition;
                if (entry.Target != null)
                {
                    basePosition = WorldToCanvasPosition(entry.Target.position + entry.WorldOffset);
                }

                entry.RectTransform.anchoredPosition = basePosition + entry.Drift * normalizedAge;
                Color color = entry.Color;
                color.a = Mathf.SmoothStep(1f, 0f, normalizedAge);
                entry.Text.color = color;
            }
        }

        private Vector2 WorldToCanvasPosition(Vector3 worldPosition)
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                return Vector2.zero;
            }

            Vector2 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out Vector2 localPosition);
            return localPosition;
        }

        private Text GetText()
        {
            Text text = textPool.Count > 0 ? textPool.Pop() : CreateText();
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            return text;
        }

        private Text CreateText()
        {
            GameObject textObject = new("Combat Text", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);

            RectTransform rectTransform = (RectTransform)textObject.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(360f, 64f);

            Text text = textObject.AddComponent<Text>();
            text.font = GetFont();
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.supportRichText = false;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private void Recycle(Text text)
        {
            text.gameObject.SetActive(false);
            textPool.Push(text);
        }

        private static Font GetFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Liberation Sans" }, 24);
            }

            return cachedFont;
        }

        private sealed class FloatingEntry
        {
            public readonly Text Text;
            public readonly RectTransform RectTransform;
            public readonly Transform Target;
            public readonly Vector3 WorldOffset;
            public readonly Vector2 ScreenPosition;
            public readonly Vector2 Drift;
            public readonly Color Color;
            public readonly float Lifetime;
            public readonly float StartTime;

            public FloatingEntry(
                Text text,
                Transform target,
                Vector3 worldOffset,
                Vector2 screenPosition,
                Vector2 drift,
                Color color,
                float lifetime,
                float startTime)
            {
                Text = text;
                RectTransform = text.rectTransform;
                Target = target;
                WorldOffset = worldOffset;
                ScreenPosition = screenPosition;
                Drift = drift;
                Color = color;
                Lifetime = lifetime;
                StartTime = startTime;
            }
        }
    }
}
