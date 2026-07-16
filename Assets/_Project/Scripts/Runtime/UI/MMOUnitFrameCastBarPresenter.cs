using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOUnitFrameCastBarPresenter : MonoBehaviour
    {
        private MMOCharacterIdentity boundCharacter;
        private MMOAbilitySystem abilitySystem;
        private MMOAbilityDefinition currentAbility;
        private Image fill;
        private Text label;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            BuildIfNeeded();
            SetVisible(false);
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshCurrentCast();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Bind(MMOCharacterIdentity character)
        {
            if (boundCharacter == character)
            {
                RefreshCurrentCast();
                return;
            }

            Unsubscribe();
            boundCharacter = character;
            abilitySystem = boundCharacter != null ? boundCharacter.GetComponent<MMOAbilitySystem>() : null;
            Subscribe();
            RefreshCurrentCast();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || abilitySystem == null)
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

        private void RefreshCurrentCast()
        {
            BuildIfNeeded();
            if (abilitySystem == null || abilitySystem.CurrentCastAbility == null)
            {
                Hide();
                return;
            }

            currentAbility = abilitySystem.CurrentCastAbility;
            label.text = currentAbility.DisplayName;
            SetFill(abilitySystem.CurrentCastNormalized);
            SetVisible(true);
        }

        private void OnCastStarted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, float duration)
        {
            if (source != abilitySystem || ability == null)
            {
                return;
            }

            currentAbility = ability;
            label.text = ability.DisplayName;
            SetFill(source.CurrentCastNormalized);
            SetVisible(true);
        }

        private void OnCastProgressed(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, float normalizedProgress)
        {
            if (source == abilitySystem && ability == currentAbility)
            {
                SetFill(normalizedProgress);
            }
        }

        private void OnCastInterrupted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, string reason)
        {
            if (source == abilitySystem && ability == currentAbility)
            {
                Hide();
            }
        }

        private void OnCastCompleted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source == abilitySystem && ability == currentAbility)
            {
                Hide();
            }
        }

        private void BuildIfNeeded()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -34f);
            root.sizeDelta = new Vector2(0f, 18f);

            if (fill != null && label != null)
            {
                return;
            }

            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            MMOUiFactory.DestroyChildren(transform);

            Image border = MMOUiFactory.CreateImage("Border", transform, new Color(0.68f, 0.57f, 0.34f, 1f), false);
            MMOUiFactory.Stretch(border.rectTransform);

            Image background = MMOUiFactory.CreateImage("Background", transform, new Color(0.025f, 0.02f, 0.015f, 0.96f), false);
            MMOUiFactory.Stretch(background.rectTransform);
            background.rectTransform.offsetMin = new Vector2(2f, 2f);
            background.rectTransform.offsetMax = new Vector2(-2f, -2f);

            fill = MMOUiFactory.CreateImage("Fill", background.transform, new Color(0.93f, 0.58f, 0.12f, 0.96f), false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            label = MMOUiFactory.CreateText("Spell Name", transform, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
            MMOUiFactory.Stretch(label.rectTransform);
        }

        private void Hide()
        {
            currentAbility = null;
            SetFill(0f);
            SetVisible(false);
        }

        private void SetFill(float normalized)
        {
            if (fill != null)
            {
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
