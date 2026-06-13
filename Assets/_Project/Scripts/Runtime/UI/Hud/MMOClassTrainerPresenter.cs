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
        private const float RowHeight = 46f;
        private const float RowSpacing = 6f;
        private const float IconSize = 38f;

        private MMOClassTrainerNpc trainer;
        private GameObject player;
        private MMOTrainerOfferEntry selectedOffer;
        private RectTransform root;
        private RectTransform listRoot;
        private Text titleText;
        private Text moneyText;
        private Text statusText;
        private Button trainButton;

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
            Position();
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
            if (presenter != null && MMOStandardWindow.HasAuthoredWindowLayout(presenter.gameObject))
            {
                return presenter;
            }

            Canvas canvas = presenter != null ? presenter.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
            if (presenter != null)
            {
                Destroy(presenter.gameObject);
            }

            if (canvas == null)
            {
                return null;
            }

            GameObject trainerObject = MMOWindowPrefabResolver.Instantiate(MMOWindowPrefabId.Training, canvas.transform, "Class Trainer Window");
            MMOClassTrainerPresenter createdPresenter = trainerObject.GetComponent<MMOClassTrainerPresenter>();
            return createdPresenter != null ? createdPresenter : trainerObject.AddComponent<MMOClassTrainerPresenter>();
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            bool hasStandardWindow = TryGetComponent(out MMOStandardWindow _);
            if (!hasStandardWindow && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            root = (RectTransform)transform;
            MMOStandardWindow.ApplyDefaultPlacementIfGenerated(root);

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Class Trainer", Close);
            RectTransform content = window.ContentRoot;
            titleText = window.TitleText;

            moneyText = window.FindText("Money");
            bool createdMoneyText = moneyText == null;
            if (createdMoneyText)
            {
                moneyText = MMOUiFactory.CreateText("Money", content, 11, FontStyle.Bold, TextAnchor.MiddleRight);
            }

            moneyText.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            if (createdMoneyText)
            {
                moneyText.rectTransform.anchorMin = new Vector2(0f, 1f);
                moneyText.rectTransform.anchorMax = new Vector2(1f, 1f);
                moneyText.rectTransform.pivot = new Vector2(1f, 1f);
                moneyText.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                moneyText.rectTransform.sizeDelta = new Vector2(-28f, 22f);
            }

            listRoot = window.FindRect("Lessons");
            bool createdListRoot = listRoot == null;
            if (createdListRoot)
            {
                listRoot = MMOUiFactory.CreateRect("Lessons", content);
                listRoot.anchorMin = new Vector2(0f, 0f);
                listRoot.anchorMax = new Vector2(1f, 1f);
                listRoot.offsetMin = new Vector2(0f, 52f);
                listRoot.offsetMax = new Vector2(0f, -34f);
            }

            statusText = window.FindText("Status");
            bool createdStatusText = statusText == null;
            if (createdStatusText)
            {
                statusText = MMOUiFactory.CreateText("Status", content, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            }

            statusText.color = MMONpcWindowFrame.BodyColor;
            if (createdStatusText)
            {
                statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
                statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
                statusText.rectTransform.pivot = new Vector2(0f, 0f);
                statusText.rectTransform.anchoredPosition = Vector2.zero;
                statusText.rectTransform.sizeDelta = new Vector2(-180f, 34f);
            }

            trainButton = window.FindButton("Train Button");
            bool createdTrainButton = trainButton == null;
            if (createdTrainButton)
            {
                trainButton = MMOUiFactory.CreateTextButton("Train Button", content, "Train", MMOStandardWindow.QuestButtonSize, MMONpcWindowFrame.AccentButtonColor);
                RectTransform trainRect = trainButton.GetComponent<RectTransform>();
                trainRect.anchorMin = new Vector2(1f, 0f);
                trainRect.anchorMax = new Vector2(1f, 0f);
                trainRect.pivot = new Vector2(1f, 0f);
                trainRect.anchoredPosition = MMOStandardWindow.DefaultActionButtonPosition;
                trainRect.sizeDelta = MMOStandardWindow.QuestButtonSize;
            }

            trainButton.onClick.RemoveAllListeners();
            trainButton.onClick.AddListener(TrainSelectedOffer);
        }

        private void Refresh()
        {
            titleText.text = trainer != null ? $"{trainer.DisplayName} - {trainer.TrainerClass} Trainer" : "Class Trainer";
            MMOCurrencyWallet wallet = player != null ? player.GetComponent<MMOCurrencyWallet>() : null;
            moneyText.text = wallet != null ? $"Money: {MMOCurrencyWallet.FormatCopper(wallet.Copper)}" : "Money: 0c";
            RefreshList();
            RefreshTrainState();
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
            Button row = MMOUiFactory.CreateTextButton($"Lesson {index + 1}", listRoot, string.Empty, new Vector2(0f, RowHeight), GetRowColor(offer));
            row.onClick.AddListener(() =>
            {
                selectedOffer = offer;
                RefreshTrainState();
                RefreshList();
            });

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowSpacing));
            rowRect.sizeDelta = new Vector2(0f, RowHeight);

            MMOAbilityDefinition ability = offer.Ability;
            CreateAbilityIcon(rowRect, ability);

            Text name = MMOUiFactory.CreateText("Name", rowRect, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            name.text = ability != null ? ability.DisplayName : "Unknown";
            name.color = GetRowTextColor(offer);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(54f, -6f);
            name.rectTransform.sizeDelta = new Vector2(-66f, 20f);

            Text requirement = MMOUiFactory.CreateText("Requirement", rowRect, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            requirement.text = $"Requires Level {offer.RequiredLevel}    Cost: {MMOCurrencyWallet.FormatCopper(offer.PriceCopper)}";
            requirement.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            requirement.rectTransform.anchorMin = new Vector2(0f, 0f);
            requirement.rectTransform.anchorMax = new Vector2(1f, 0f);
            requirement.rectTransform.pivot = new Vector2(0f, 0f);
            requirement.rectTransform.anchoredPosition = new Vector2(54f, 6f);
            requirement.rectTransform.sizeDelta = new Vector2(-66f, 18f);

            if (ability != null)
            {
                MMOAbilityTooltipTrigger tooltip = row.gameObject.AddComponent<MMOAbilityTooltipTrigger>();
                tooltip.Configure(ability);
            }
        }

        private void RefreshTrainState()
        {
            if (trainButton == null)
            {
                return;
            }

            string trainState = string.Empty;
            bool canTrain = selectedOffer != null && trainer != null && trainer.CanTrain(selectedOffer, player, out trainState);
            trainButton.interactable = canTrain;
            statusText.text = canTrain ? string.Empty : trainState;
        }

        private void TrainSelectedOffer()
        {
            string result;
            if (trainer != null && selectedOffer != null)
            {
                trainer.TryTrain(selectedOffer, player, out result);
            }
            else
            {
                result = "Trainer unavailable.";
            }

            statusText.text = result;
            Refresh();
        }

        private void CreateAbilityIcon(RectTransform rowRect, MMOAbilityDefinition ability)
        {
            Image icon = MMOUiFactory.CreateImage("Icon", rowRect, ability != null && ability.Icon != null ? Color.white : new Color(0.18f, 0.12f, 0.055f, 1f), false);
            icon.sprite = ability != null ? ability.Icon : null;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);

            Outline outline = icon.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.45f, 0.24f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            if (ability == null || ability.Icon != null)
            {
                return;
            }

            Text placeholder = MMOUiFactory.CreateText("Icon Placeholder", icon.transform, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            placeholder.text = BuildFallbackLabel(ability.DisplayName);
            placeholder.color = MMONpcWindowFrame.TitleColor;
            MMOUiFactory.Stretch(placeholder.rectTransform);
        }

        private static string BuildFallbackLabel(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            string[] words = displayName.Split(' ');
            if (words.Length == 1)
            {
                return displayName.Length <= 2 ? displayName.ToUpperInvariant() : displayName[..2].ToUpperInvariant();
            }

            char first = words[0].Length > 0 ? words[0][0] : '?';
            char second = words[^1].Length > 0 ? words[^1][0] : '?';
            return $"{char.ToUpperInvariant(first)}{char.ToUpperInvariant(second)}";
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

        private void Position()
        {
            MMOStandardWindow.ApplyDefaultPlacementIfGenerated(root);
        }
    }
}
