using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOCastBarPresenter : MonoBehaviour
    {
        [SerializeField] private MMOAbilitySystem abilitySystem;
        [SerializeField] private bool autoBuild = true;

        private Image fill;
        private Text label;
        private CanvasGroup canvasGroup;
        private MMOAbilityDefinition currentAbility;

        private void Awake()
        {
            ResolveAbilitySystem();
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(MMOAbilitySystem newAbilitySystem)
        {
            Unsubscribe();
            abilitySystem = newAbilitySystem;
            BuildIfNeeded();
            Subscribe();
        }

        private void ResolveAbilitySystem()
        {
            if (abilitySystem != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                abilitySystem = player.GetComponent<MMOAbilitySystem>();
            }
        }

        private void Subscribe()
        {
            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.CastStarted -= OnCastStarted;
            abilitySystem.CastProgressed -= OnCastProgressed;
            abilitySystem.CastInterrupted -= OnCastInterrupted;
            abilitySystem.CastCompleted -= OnCastCompleted;
            abilitySystem.CastStarted += OnCastStarted;
            abilitySystem.CastProgressed += OnCastProgressed;
            abilitySystem.CastInterrupted += OnCastInterrupted;
            abilitySystem.CastCompleted += OnCastCompleted;
        }

        private void Unsubscribe()
        {
            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.CastStarted -= OnCastStarted;
            abilitySystem.CastProgressed -= OnCastProgressed;
            abilitySystem.CastInterrupted -= OnCastInterrupted;
            abilitySystem.CastCompleted -= OnCastCompleted;
        }

        private void BuildIfNeeded()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 142f);
            root.sizeDelta = new Vector2(360f, 28f);

            if (fill != null && label != null)
            {
                return;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            MMOUiFactory.DestroyChildren(transform);
            Image background = MMOUiFactory.CreateImage("Background", transform, new Color(0.025f, 0.02f, 0.015f, 0.92f), false);
            MMOUiFactory.Stretch(background.rectTransform);

            fill = MMOUiFactory.CreateImage("Fill", transform, new Color(0.95f, 0.62f, 0.18f, 0.95f), false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            label = MMOUiFactory.CreateText("Label", transform, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            MMOUiFactory.Stretch(label.rectTransform);
        }

        private void OnCastStarted(MMOAbilitySystem system, MMOAbilityDefinition ability, MMOCharacterIdentity target, float duration)
        {
            currentAbility = ability;
            SetVisible(true);
            label.text = ability != null ? ability.DisplayName : "Casting";
            SetFill(0f);
        }

        private void OnCastProgressed(MMOAbilitySystem system, MMOAbilityDefinition ability, MMOCharacterIdentity target, float normalizedProgress)
        {
            if (ability != currentAbility)
            {
                return;
            }

            SetFill(normalizedProgress);
        }

        private void OnCastInterrupted(MMOAbilitySystem system, MMOAbilityDefinition ability, MMOCharacterIdentity target, string reason)
        {
            Hide();
        }

        private void OnCastCompleted(MMOAbilitySystem system, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            Hide();
        }

        private void Hide()
        {
            currentAbility = null;
            SetFill(0f);
            SetVisible(false);
        }

        private void SetFill(float normalized)
        {
            if (fill == null)
            {
                return;
            }

            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
