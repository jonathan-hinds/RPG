using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Quests;
using RPGClone.Trainers;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOClassTrainerPresenter : MonoBehaviour
    {
        private const float Width = 560f;
        private const float Height = 500f;
        private const float CanvasPadding = 8f;

        private MMOClassTrainerNpc trainer;
        private GameObject player;
        private MMOTrainerOfferEntry selectedOffer;
        private RectTransform root;
        private RectTransform listRoot;
        private RectTransform detailRoot;
        private Text titleText;
        private Text moneyText;
        private Text statusText;

        public static MMOClassTrainerPresenter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            BuildIfNeeded();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void Open(MMOClassTrainerNpc trainer, GameObject player, Vector2 screenPosition)
        {
            ResolvePresenter()?.Show(trainer, player, screenPosition);
        }

        public void Show(MMOClassTrainerNpc newTrainer, GameObject newPlayer, Vector2 screenPosition)
        {
            trainer = newTrainer;
            player = newPlayer;
            selectedOffer = trainer != null && trainer.Offers.Count > 0 ? trainer.Offers[0] : null;
            BuildIfNeeded();
            gameObject.SetActive(true);
            Position(screenPosition);
            TrackNpcDistance();
            Refresh();
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void TrackNpcDistance()
        {
            if (trainer == null || player == null)
            {
                return;
            }

            MMONpcPanelDistanceCloser closer = gameObject.GetComponent<MMONpcPanelDistanceCloser>();
            if (closer == null)
            {
                closer = gameObject.AddComponent<MMONpcPanelDistanceCloser>();
            }

            closer.Track(trainer.transform, player.transform, trainer.InteractionDistance + 0.75f, Close);
        }

        private static MMOClassTrainerPresenter ResolvePresenter()
        {
            if (Instance != null)
            {
                return Instance;
            }

            MMOClassTrainerPresenter presenter = FindAnyObjectByType<MMOClassTrainerPresenter>();
            if (presenter != null)
            {
                return presenter;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            GameObject trainerObject = new("Class Trainer Window", typeof(RectTransform));
            trainerObject.transform.SetParent(canvas.transform, false);
            return trainerObject.AddComponent<MMOClassTrainerPresenter>();
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(Width, Height);
            MMONpcWindowFrame.Apply(gameObject);
            titleText = MMONpcWindowFrame.CreateTitle(transform, "Class Trainer");
            MMONpcWindowFrame.CreateCloseButton(transform, Close);

            moneyText = MMOUiFactory.CreateText("Money", transform, 11, FontStyle.Bold, TextAnchor.MiddleRight);
            moneyText.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            moneyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            moneyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            moneyText.rectTransform.pivot = new Vector2(1f, 1f);
            moneyText.rectTransform.anchoredPosition = new Vector2(-46f, -44f);
            moneyText.rectTransform.sizeDelta = new Vector2(-28f, 22f);

            Text header = MMOUiFactory.CreateText("Header", transform, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            header.text = "Name                                      Level       Cost";
            header.color = new Color(0.86f, 0.78f, 0.64f, 1f);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(0f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(18f, -76f);
            header.rectTransform.sizeDelta = new Vector2(330f, 20f);

            listRoot = MMOUiFactory.CreateRect("Lessons", transform);
            listRoot.anchorMin = new Vector2(0f, 0f);
            listRoot.anchorMax = new Vector2(0f, 1f);
            listRoot.offsetMin = new Vector2(18f, 68f);
            listRoot.offsetMax = new Vector2(350f, -100f);

            detailRoot = MMOUiFactory.CreateRect("Details", transform);
            detailRoot.anchorMin = new Vector2(1f, 0f);
            detailRoot.anchorMax = new Vector2(1f, 1f);
            detailRoot.pivot = new Vector2(1f, 0.5f);
            detailRoot.offsetMin = new Vector2(-190f, 68f);
            detailRoot.offsetMax = new Vector2(-18f, -76f);

            statusText = MMOUiFactory.CreateText("Status", transform, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            statusText.color = MMONpcWindowFrame.BodyColor;
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(18f, 18f);
            statusText.rectTransform.sizeDelta = new Vector2(-180f, 34f);
        }

        private void Refresh()
        {
            titleText.text = trainer != null ? $"{trainer.DisplayName} - {trainer.TrainerClass} Trainer" : "Class Trainer";
            MMOCurrencyWallet wallet = player != null ? player.GetComponent<MMOCurrencyWallet>() : null;
            moneyText.text = wallet != null ? $"Money: {MMOCurrencyWallet.FormatCopper(wallet.Copper)}" : "Money: 0c";
            RefreshList();
            RefreshDetails();
        }

        private void RefreshList()
        {
            MMOUiFactory.DestroyChildren(listRoot);
            int count = trainer != null ? trainer.Offers.Count : 0;
            for (int i = 0; i < count; i++)
            {
                CreateOfferRow(trainer.Offers[i], i);
            }
        }

        private void CreateOfferRow(MMOTrainerOfferEntry offer, int index)
        {
            Button row = MMOUiFactory.CreateTextButton($"Lesson {index + 1}", listRoot, string.Empty, new Vector2(330f, 32f), GetRowColor(offer));
            row.onClick.AddListener(() =>
            {
                selectedOffer = offer;
                RefreshDetails();
                RefreshList();
            });

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * 36f);

            MMOAbilityDefinition ability = offer.Ability;
            Text name = MMOUiFactory.CreateText("Name", rowRect, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            name.text = ability != null ? ability.DisplayName : "Unknown";
            name.color = GetRowTextColor(offer);
            name.rectTransform.anchorMin = Vector2.zero;
            name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(9f, 0f);
            name.rectTransform.offsetMax = new Vector2(-122f, 0f);

            Text level = MMOUiFactory.CreateText("Level", rowRect, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
            level.text = offer.RequiredLevel.ToString();
            level.color = name.color;
            level.rectTransform.anchorMin = new Vector2(1f, 0f);
            level.rectTransform.anchorMax = new Vector2(1f, 1f);
            level.rectTransform.pivot = new Vector2(1f, 0.5f);
            level.rectTransform.anchoredPosition = new Vector2(-78f, 0f);
            level.rectTransform.sizeDelta = new Vector2(34f, 0f);

            Text cost = MMOUiFactory.CreateText("Cost", rowRect, 11, FontStyle.Bold, TextAnchor.MiddleRight);
            cost.text = MMOCurrencyWallet.FormatCopper(offer.PriceCopper);
            cost.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            cost.rectTransform.anchorMin = new Vector2(1f, 0f);
            cost.rectTransform.anchorMax = new Vector2(1f, 1f);
            cost.rectTransform.pivot = new Vector2(1f, 0.5f);
            cost.rectTransform.anchoredPosition = new Vector2(-9f, 0f);
            cost.rectTransform.sizeDelta = new Vector2(64f, 0f);

            if (ability != null)
            {
                MMOAbilityTooltipTrigger tooltip = row.gameObject.AddComponent<MMOAbilityTooltipTrigger>();
                tooltip.Configure(ability);
            }
        }

        private void RefreshDetails()
        {
            MMOUiFactory.DestroyChildren(detailRoot);
            if (selectedOffer == null || selectedOffer.Ability == null)
            {
                return;
            }

            MMOAbilityDefinition ability = selectedOffer.Ability;
            Text name = MMOUiFactory.CreateText("Ability Name", detailRoot, 16, FontStyle.Bold, TextAnchor.UpperLeft);
            name.text = ability.DisplayName;
            name.color = MMONpcWindowFrame.TitleColor;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.sizeDelta = new Vector2(0f, 28f);

            Text details = MMOUiFactory.CreateText("Ability Details", detailRoot, 12, FontStyle.Normal, TextAnchor.UpperLeft);
            string trainState = string.Empty;
            bool canTrain = trainer != null && trainer.CanTrain(selectedOffer, player, out trainState);
            string availability = canTrain || string.IsNullOrWhiteSpace(trainState) || trainState == "Available." ? string.Empty : $"\n\n{trainState}";
            details.text = $"{ability.Description}\n\nRequires: Level {selectedOffer.RequiredLevel} {selectedOffer.RequiredClass}\nCost: {MMOCurrencyWallet.FormatCopper(selectedOffer.PriceCopper)}{availability}";
            details.color = MMONpcWindowFrame.BodyColor;
            details.horizontalOverflow = HorizontalWrapMode.Wrap;
            details.verticalOverflow = VerticalWrapMode.Overflow;
            details.rectTransform.anchorMin = new Vector2(0f, 1f);
            details.rectTransform.anchorMax = new Vector2(1f, 1f);
            details.rectTransform.pivot = new Vector2(0f, 1f);
            details.rectTransform.anchoredPosition = new Vector2(0f, -36f);
            details.rectTransform.sizeDelta = new Vector2(0f, 200f);

            Button train = MMOUiFactory.CreateTextButton("Train", detailRoot, "Train", new Vector2(118f, 34f), MMONpcWindowFrame.AccentButtonColor);
            train.interactable = canTrain;
            statusText.text = canTrain ? string.Empty : trainState;
            train.onClick.AddListener(() =>
            {
                string result;
                if (trainer != null)
                {
                    trainer.TryTrain(selectedOffer, player, out result);
                }
                else
                {
                    result = "Trainer unavailable.";
                }

                statusText.text = result;
                Refresh();
            });

            RectTransform trainRect = train.GetComponent<RectTransform>();
            trainRect.anchorMin = new Vector2(1f, 0f);
            trainRect.anchorMax = new Vector2(1f, 0f);
            trainRect.pivot = new Vector2(1f, 0f);
            trainRect.anchoredPosition = Vector2.zero;
        }

        private bool CanTrain(MMOTrainerOfferEntry offer)
        {
            return trainer != null && trainer.CanTrain(offer, player, out _);
        }

        private Color GetRowColor(MMOTrainerOfferEntry offer)
        {
            if (offer == selectedOffer)
            {
                return new Color(0.16f, 0.11f, 0.055f, 1f);
            }

            return MMONpcWindowFrame.PanelColor;
        }

        private Color GetRowTextColor(MMOTrainerOfferEntry offer)
        {
            if (CanTrain(offer))
            {
                return new Color(0.18f, 1f, 0.18f, 1f);
            }

            MMOAbilitySystem abilitySystem = player != null ? player.GetComponent<MMOAbilitySystem>() : null;
            if (abilitySystem != null && abilitySystem.KnowsAbility(offer.Ability))
            {
                return new Color(0.62f, 0.62f, 0.62f, 1f);
            }

            return new Color(1f, 0.35f, 0.24f, 1f);
        }

        private void Position(Vector2 screenPosition)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            if (canvasRect == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPosition);

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = ClampToCanvas(localPosition + new Vector2(24f, -20f), canvasRect);
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            Rect rect = canvasRect.rect;
            Vector2 size = root.sizeDelta;
            position.x = Mathf.Clamp(position.x, rect.xMin + CanvasPadding, rect.xMax - size.x - CanvasPadding);
            position.y = Mathf.Clamp(position.y, rect.yMin + size.y + CanvasPadding, rect.yMax - CanvasPadding);
            return position;
        }
    }
}
