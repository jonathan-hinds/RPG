using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.PlayerInteraction;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [DisallowMultipleComponent]
    public sealed class MMOPlayerInteractionPresenter : MonoBehaviour
    {
        private const string TooltipPanelResourcePath = "RPGClone/UI/Tooltip/TooltipPanel";
        private const string SharedSlotResourcePath = "RPGClone/UI/SlotFramework/SharedSlot";
        private static readonly Color AcceptedColor = new(0.12f, 0.58f, 0.19f, 0.35f);
        private static readonly Color WaitingColor = new(0.05f, 0.045f, 0.035f, 0.72f);

        private Canvas canvas;
        private MMOUnitFrameView targetFrame;
        private RectTransform contextMenu;
        private Text contextTitle;
        private RectTransform duelRequestPopup;
        private Text duelRequestText;
        private Button duelAcceptButton;
        private Button duelDeclineButton;
        private RectTransform duelBanner;
        private Text duelBannerText;
        private GameObject tradeWindowObject;
        private MMOStandardWindow tradeWindow;
        private Text localTradeName;
        private Text remoteTradeName;
        private Image localTradePane;
        private Image remoteTradePane;
        private readonly MMOSlotView[] localTradeSlots = new MMOSlotView[MMOPlayerInteractionAuthority.TradeSlotCount];
        private readonly MMOSlotView[] remoteTradeSlots = new MMOSlotView[MMOPlayerInteractionAuthority.TradeSlotCount];
        private readonly Text[] localTradeLabels = new Text[MMOPlayerInteractionAuthority.TradeSlotCount];
        private readonly Text[] remoteTradeLabels = new Text[MMOPlayerInteractionAuthority.TradeSlotCount];
        private InputField localCopperInput;
        private Text remoteCopperText;
        private Button tradeButton;
        private Button tradeCancelButton;
        private string displayedDuelId = string.Empty;
        private int displayedDuelRevision = -1;
        private float duelStateArrivedAt;
        private string displayedTradeId = string.Empty;
        private int displayedTradeRevision = -1;
        private float nextReferenceResolveAt;

        private void OnEnable()
        {
            ResolveCanvas();
            ResolveTargetFrame();
        }

        private void OnDisable()
        {
            UnsubscribeTargetFrame();
        }

        private void Update()
        {
            MMOPlayerInteractionService.Tick();
            if (Time.unscaledTime >= nextReferenceResolveAt)
            {
                nextReferenceResolveAt = Time.unscaledTime + 0.5f;
                ResolveCanvas();
                ResolveTargetFrame();
            }

            RefreshDuelUi();
            RefreshTradeUi();
        }

        private void ResolveCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas candidate in canvases)
            {
                if (candidate != null && candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace)
                {
                    canvas = candidate;
                    break;
                }
            }
        }

        private void ResolveTargetFrame()
        {
            if (targetFrame != null && targetFrame.FrameStyle == MMOUnitFrameStyle.Target)
            {
                return;
            }

            UnsubscribeTargetFrame();
            MMOUnitFrameView[] frames = FindObjectsByType<MMOUnitFrameView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MMOUnitFrameView frame in frames)
            {
                if (frame != null && frame.FrameStyle == MMOUnitFrameStyle.Target)
                {
                    targetFrame = frame;
                    targetFrame.RightClicked += OnTargetFrameRightClicked;
                    break;
                }
            }
        }

        private void UnsubscribeTargetFrame()
        {
            if (targetFrame != null)
            {
                targetFrame.RightClicked -= OnTargetFrameRightClicked;
            }

            targetFrame = null;
        }

        private void OnTargetFrameRightClicked(MMOUnitFrameView frame, MMOCharacterIdentity character, Vector2 screenPosition)
        {
            if (canvas == null || character == null
                || !MMOGameplaySessionService.Players.TryGetParticipant(character, out MMOPlayerParticipant participant)
                || participant.IsLocal || string.IsNullOrWhiteSpace(participant.CharacterId))
            {
                HideContextMenu();
                return;
            }

            EnsureContextMenu();
            contextTitle.text = character.DisplayName;
            contextMenu.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform,
                screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPoint);
            contextMenu.anchoredPosition = ClampToCanvas(localPoint, contextMenu.sizeDelta);
            ConfigureContextAction("Trade", () =>
            {
                MMOPlayerInteractionService.RequestTrade(participant.CharacterId);
                HideContextMenu();
            });
            ConfigureContextAction("Duel", () =>
            {
                MMOPlayerInteractionService.RequestDuel(participant.CharacterId);
                HideContextMenu();
            });
        }

        private void EnsureContextMenu()
        {
            if (contextMenu != null || canvas == null)
            {
                return;
            }

            Image panel = CreateTooltipPanel("Player Interaction Menu", canvas.transform);
            contextMenu = panel.rectTransform;
            contextMenu.anchorMin = contextMenu.anchorMax = new Vector2(0.5f, 0.5f);
            contextMenu.pivot = new Vector2(0f, 1f);
            contextMenu.sizeDelta = new Vector2(190f, 118f);
            contextTitle = MMOUiFactory.CreateText("Player Name", contextMenu, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            contextTitle.color = new Color(0.25f, 0.9f, 1f, 1f);
            Place(contextTitle.rectTransform, new Vector2(12f, -8f), new Vector2(166f, 24f), new Vector2(0f, 1f));
            Text section = MMOUiFactory.CreateText("Section", contextMenu, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            section.text = "Interact";
            section.color = MMONpcWindowFrame.TitleColor;
            Place(section.rectTransform, new Vector2(12f, -34f), new Vector2(166f, 20f), new Vector2(0f, 1f));
            CreateContextButton("Trade", -58f);
            CreateContextButton("Duel", -84f);
            contextMenu.gameObject.SetActive(false);
        }

        private void CreateContextButton(string label, float y)
        {
            Button button = MMOUiFactory.CreateTextButton(label, contextMenu, label, new Vector2(166f, 24f), Color.clear);
            Place(button.GetComponent<RectTransform>(), new Vector2(12f, y), new Vector2(166f, 24f), new Vector2(0f, 1f));
            Text text = MMOUiFactory.FindButtonLabel(button);
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Normal;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.82f, 0.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.62f, 0.18f, 1f);
            button.colors = colors;
        }

        private void ConfigureContextAction(string buttonName, Action action)
        {
            Button button = contextMenu.Find(buttonName)?.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void HideContextMenu()
        {
            contextMenu?.gameObject.SetActive(false);
        }

        private void RefreshDuelUi()
        {
            string localId = MMOGameplaySessionService.LocalPlayer.CharacterId;
            MMODuelSessionSnapshot duel = MMOPlayerInteractionState.FindCurrentDuel(localId);
            if (duel == null)
            {
                foreach (MMODuelSessionSnapshot candidate in MMOPlayerInteractionState.DuelSessions)
                {
                    if (candidate != null && candidate.Includes(localId)
                        && candidate.duelId == displayedDuelId
                        && candidate.revision >= displayedDuelRevision)
                    {
                        duel = candidate;
                    }
                }
            }

            if (duel == null)
            {
                HideDuelUi();
                return;
            }

            if (duel.duelId != displayedDuelId || duel.revision != displayedDuelRevision)
            {
                displayedDuelId = duel.duelId;
                displayedDuelRevision = duel.revision;
                duelStateArrivedAt = Time.unscaledTime;
            }

            bool incomingRequest = duel.status == MMODuelSessionStatus.Pending
                && duel.challengedCharacterId == localId;
            if (incomingRequest)
            {
                ShowDuelRequest(duel);
            }
            else
            {
                duelRequestPopup?.gameObject.SetActive(false);
            }

            if (duel.status == MMODuelSessionStatus.Countdown)
            {
                float remaining = Mathf.Max(0f, MMOPlayerInteractionAuthority.DuelCountdownSeconds - (Time.unscaledTime - duelStateArrivedAt));
                ShowDuelBanner($"Duel begins in {Mathf.Max(1, Mathf.CeilToInt(remaining))}");
            }
            else if (duel.status == MMODuelSessionStatus.Active)
            {
                ShowDuelBanner($"Dueling {MMOPlayerInteractionService.GetPlayerName(duel.Other(localId))}");
            }
            else if (duel.status != MMODuelSessionStatus.Pending && Time.unscaledTime - duelStateArrivedAt < 4f)
            {
                string message = !string.IsNullOrWhiteSpace(duel.endReason)
                    ? duel.endReason
                    : duel.status.ToString();
                ShowDuelBanner(message);
            }
            else
            {
                duelBanner?.gameObject.SetActive(false);
            }
        }

        private void ShowDuelRequest(MMODuelSessionSnapshot duel)
        {
            EnsureDuelRequestPopup();
            duelRequestText.text = $"{MMOPlayerInteractionService.GetPlayerName(duel.challengerCharacterId)} has challenged you to a duel.";
            duelAcceptButton.onClick.RemoveAllListeners();
            duelAcceptButton.onClick.AddListener(() => MMOPlayerInteractionService.RespondToDuel(duel.duelId, true));
            duelDeclineButton.onClick.RemoveAllListeners();
            duelDeclineButton.onClick.AddListener(() => MMOPlayerInteractionService.RespondToDuel(duel.duelId, false));
            duelRequestPopup.gameObject.SetActive(true);
        }

        private void EnsureDuelRequestPopup()
        {
            if (duelRequestPopup != null || canvas == null)
            {
                return;
            }

            Image panel = CreateTooltipPanel("Duel Request", canvas.transform);
            duelRequestPopup = panel.rectTransform;
            duelRequestPopup.anchorMin = duelRequestPopup.anchorMax = new Vector2(0.5f, 0.5f);
            duelRequestPopup.pivot = new Vector2(0.5f, 0.5f);
            duelRequestPopup.anchoredPosition = new Vector2(0f, 110f);
            duelRequestPopup.sizeDelta = new Vector2(370f, 132f);
            duelRequestText = MMOUiFactory.CreateText("Message", duelRequestPopup, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            duelRequestText.color = MMONpcWindowFrame.BodyColor;
            Place(duelRequestText.rectTransform, new Vector2(16f, -14f), new Vector2(338f, 58f), new Vector2(0f, 1f));
            duelAcceptButton = MMOStandardWindow.CreateQuestActionButton("Accept", duelRequestPopup, "Accept");
            Place(duelAcceptButton.GetComponent<RectTransform>(), new Vector2(42f, 16f), new Vector2(132f, 34f), Vector2.zero);
            duelDeclineButton = MMOStandardWindow.CreateQuestActionButton("Decline", duelRequestPopup, "Decline");
            Place(duelDeclineButton.GetComponent<RectTransform>(), new Vector2(196f, 16f), new Vector2(132f, 34f), Vector2.zero);
            duelRequestPopup.gameObject.SetActive(false);
        }

        private void ShowDuelBanner(string message)
        {
            if (canvas == null)
            {
                return;
            }

            if (duelBanner == null)
            {
                Image panel = CreateTooltipPanel("Duel Status", canvas.transform);
                duelBanner = panel.rectTransform;
                duelBanner.anchorMin = duelBanner.anchorMax = new Vector2(0.5f, 1f);
                duelBanner.pivot = new Vector2(0.5f, 1f);
                duelBanner.anchoredPosition = new Vector2(0f, -110f);
                duelBanner.sizeDelta = new Vector2(360f, 42f);
                duelBannerText = MMOUiFactory.CreateText("Status", duelBanner, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
                duelBannerText.color = MMONpcWindowFrame.TitleColor;
                MMOUiFactory.Stretch(duelBannerText.rectTransform);
            }

            duelBannerText.text = message;
            duelBanner.gameObject.SetActive(true);
        }

        private void HideDuelUi()
        {
            duelRequestPopup?.gameObject.SetActive(false);
            duelBanner?.gameObject.SetActive(false);
            displayedDuelId = string.Empty;
            displayedDuelRevision = -1;
        }

        private void RefreshTradeUi()
        {
            string localId = MMOGameplaySessionService.LocalPlayer.CharacterId;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindCurrentTrade(localId);
            if (trade == null)
            {
                foreach (MMOTradeSessionSnapshot candidate in MMOPlayerInteractionState.TradeSessions)
                {
                    if (candidate != null && candidate.Includes(localId) && candidate.tradeId == displayedTradeId)
                    {
                        trade = candidate;
                    }
                }
            }

            if (trade == null || trade.status != MMOTradeSessionStatus.Open)
            {
                if (tradeWindowObject != null)
                {
                    tradeWindowObject.SetActive(false);
                }

                displayedTradeId = string.Empty;
                displayedTradeRevision = -1;
                return;
            }

            EnsureTradeWindow();
            if (tradeWindowObject == null)
            {
                return;
            }

            tradeWindowObject.SetActive(true);
            if (trade.tradeId == displayedTradeId && trade.revision == displayedTradeRevision)
            {
                return;
            }

            displayedTradeId = trade.tradeId;
            displayedTradeRevision = trade.revision;
            string remoteId = trade.Other(localId);
            tradeWindow.SetTitle($"Trade with {MMOPlayerInteractionService.GetPlayerName(remoteId)}");
            localTradeName.text = MMOPlayerInteractionService.GetPlayerName(localId);
            remoteTradeName.text = MMOPlayerInteractionService.GetPlayerName(remoteId);
            RefreshTradeColumn(localTradeSlots, localTradeLabels, trade.OffersFor(localId));
            RefreshTradeColumn(remoteTradeSlots, remoteTradeLabels, trade.OffersFor(remoteId));
            bool localAccepted = trade.IsAcceptedBy(localId);
            bool remoteAccepted = trade.IsAcceptedBy(remoteId);
            localTradePane.color = localAccepted ? AcceptedColor : WaitingColor;
            remoteTradePane.color = remoteAccepted ? AcceptedColor : WaitingColor;
            tradeButton.interactable = !localAccepted;
            MMOUiFactory.FindButtonLabel(tradeButton).text = localAccepted ? "Accepted" : "Trade";
            if (localCopperInput != null && !localCopperInput.isFocused)
            {
                localCopperInput.text = trade.CopperFor(localId).ToString();
            }

            remoteCopperText.text = $"Offer: {MMOCurrencyWallet.FormatCopper(trade.CopperFor(remoteId))}";
            tradeButton.onClick.RemoveAllListeners();
            tradeButton.onClick.AddListener(() => MMOPlayerInteractionService.SetTradeAccepted(trade.tradeId, true));
            tradeCancelButton.onClick.RemoveAllListeners();
            tradeCancelButton.onClick.AddListener(() => MMOPlayerInteractionService.CancelTrade(trade.tradeId));
        }

        private void EnsureTradeWindow()
        {
            if (tradeWindowObject != null || canvas == null)
            {
                return;
            }

            tradeWindowObject = MMOWindowPrefabResolver.Instantiate(MMOWindowPrefabId.Generic, canvas.transform, "Trade Window");
            tradeWindow = MMOStandardWindow.Ensure(tradeWindowObject, "Trade", CancelDisplayedTrade);
            RectTransform root = (RectTransform)tradeWindowObject.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(620f, 580f);
            RectTransform content = tradeWindow.ContentRoot;
            MMOUiFactory.DestroyChildren(content);

            localTradePane = CreateTradePane("Your Offer", content, new Vector2(8f, -4f), out localTradeName);
            remoteTradePane = CreateTradePane("Their Offer", content, new Vector2(290f, -4f), out remoteTradeName);
            for (int i = 0; i < MMOPlayerInteractionAuthority.TradeSlotCount; i++)
            {
                localTradeSlots[i] = CreateTradeSlot(localTradePane.transform, i, true, out localTradeLabels[i]);
                remoteTradeSlots[i] = CreateTradeSlot(remoteTradePane.transform, i, false, out remoteTradeLabels[i]);
            }

            Text copperLabel = MMOUiFactory.CreateText("Copper Label", localTradePane.transform, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            copperLabel.text = "Copper";
            copperLabel.color = MMONpcWindowFrame.TitleColor;
            Place(copperLabel.rectTransform, new Vector2(12f, 10f), new Vector2(58f, 28f), Vector2.zero);
            localCopperInput = CreateIntegerInput("Copper Input", localTradePane.transform);
            Place(localCopperInput.GetComponent<RectTransform>(), new Vector2(72f, 10f), new Vector2(176f, 28f), Vector2.zero);
            localCopperInput.onEndEdit.AddListener(OnCopperEdited);
            remoteCopperText = MMOUiFactory.CreateText("Remote Copper", remoteTradePane.transform, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            remoteCopperText.color = MMONpcWindowFrame.TitleColor;
            Place(remoteCopperText.rectTransform, new Vector2(12f, 10f), new Vector2(236f, 28f), Vector2.zero);

            tradeButton = MMOStandardWindow.CreateQuestActionButton("Trade", content, "Trade");
            Place(tradeButton.GetComponent<RectTransform>(), new Vector2(294f, 4f), new Vector2(124f, 34f), Vector2.zero);
            tradeCancelButton = MMOStandardWindow.CreateQuestActionButton("Cancel", content, "Cancel");
            Place(tradeCancelButton.GetComponent<RectTransform>(), new Vector2(430f, 4f), new Vector2(124f, 34f), Vector2.zero);
            tradeWindowObject.SetActive(false);
        }

        private Image CreateTradePane(string objectName, Transform parent, Vector2 anchoredPosition, out Text playerName)
        {
            Image pane = MMOUiFactory.CreateImage(objectName, parent, WaitingColor);
            RectTransform rect = pane.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(272f, 390f);
            Outline outline = pane.gameObject.AddComponent<Outline>();
            outline.effectColor = MMONpcWindowFrame.BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
            playerName = MMOUiFactory.CreateText("Player Name", pane.transform, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
            playerName.color = MMONpcWindowFrame.TitleColor;
            Place(playerName.rectTransform, new Vector2(10f, -6f), new Vector2(252f, 28f), new Vector2(0f, 1f));
            return pane;
        }

        private MMOSlotView CreateTradeSlot(Transform pane, int index, bool local, out Text itemName)
        {
            float y = -40f - index * 50f;
            Image row = MMOUiFactory.CreateImage(
                $"Trade Row {index + 1}",
                pane,
                new Color(1f, 1f, 1f, local ? 0.001f : 0f),
                local);
            Place(row.rectTransform, new Vector2(10f, y), new Vector2(248f, 44f), new Vector2(0f, 1f));
            if (local)
            {
                MMOTradeSlotDropTarget interaction = row.gameObject.AddComponent<MMOTradeSlotDropTarget>();
                interaction.Configure(index);
            }

            GameObject slotPrefab = Resources.Load<GameObject>(SharedSlotResourcePath);
            GameObject slotObject = slotPrefab != null
                ? Instantiate(slotPrefab, row.transform, false)
                : new GameObject($"Trade Slot {index + 1}", typeof(RectTransform), typeof(Image));
            if (slotPrefab == null)
            {
                slotObject.transform.SetParent(row.transform, false);
            }

            slotObject.name = $"Trade Slot {index + 1}";
            RectTransform slotRect = (RectTransform)slotObject.transform;
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 1f);
            slotRect.pivot = new Vector2(0f, 1f);
            slotRect.anchoredPosition = Vector2.zero;
            slotRect.sizeDelta = new Vector2(44f, 44f);
            MMOSlotView slot = slotObject.GetComponent<MMOSlotView>() ?? MMOSlotView.Attach(slotObject);
            slot.Present(MMOSlotPresentation.Empty());
            MMOItemTooltipTrigger.Bind(slotObject, null);
            if (local)
            {
                MMOTradeSlotDropTarget slotInteraction = slotObject.AddComponent<MMOTradeSlotDropTarget>();
                slotInteraction.Configure(index);
            }

            itemName = MMOUiFactory.CreateText($"Item {index + 1}", row.transform, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
            itemName.color = MMONpcWindowFrame.BodyColor;
            itemName.raycastTarget = false;
            Place(itemName.rectTransform, new Vector2(52f, 0f), new Vector2(186f, 44f), new Vector2(0f, 1f));
            return slot;
        }

        private static void RefreshTradeColumn(
            MMOSlotView[] slots,
            Text[] labels,
            IReadOnlyList<MMOTradeOfferEntry> offers)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                MMOTradeOfferEntry offer = null;
                if (offers != null)
                {
                    for (int j = 0; j < offers.Count; j++)
                    {
                        if (offers[j] != null && offers[j].offerSlotIndex == i)
                        {
                            offer = offers[j];
                            break;
                        }
                    }
                }

                MMOItemDefinition item = offer != null ? MMOItemCatalog.FindLoadedById(offer.itemId) : null;
                slots[i].Present(item != null ? MMOItemSlotAdapter.Present(item, offer.quantity) : MMOSlotPresentation.Empty());
                MMOItemTooltipTrigger.Bind(slots[i].gameObject, item);
                labels[i].text = item != null
                    ? item.DisplayName + (offer.quantity > 1 ? $" x{offer.quantity}" : string.Empty)
                    : "Empty";
                labels[i].color = item != null ? MMOItemIconView.GetQualityTextColor(item.Quality) : new Color(0.48f, 0.44f, 0.38f, 1f);
            }
        }

        private InputField CreateIntegerInput(string objectName, Transform parent)
        {
            Image background = MMOUiFactory.CreateImage(objectName, parent, new Color(0.025f, 0.022f, 0.018f, 0.98f));
            Outline outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = MMONpcWindowFrame.BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
            InputField input = background.gameObject.AddComponent<InputField>();
            Text text = MMOUiFactory.CreateText("Text", background.transform, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.color = MMONpcWindowFrame.BodyColor;
            text.rectTransform.offsetMin = new Vector2(6f, 1f);
            text.rectTransform.offsetMax = new Vector2(-6f, -1f);
            MMOUiFactory.Stretch(text.rectTransform);
            input.textComponent = text;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = "0";
            return input;
        }

        private void OnCopperEdited(string value)
        {
            if (string.IsNullOrWhiteSpace(displayedTradeId))
            {
                return;
            }

            int copper = int.TryParse(value, out int parsed) ? Mathf.Max(0, parsed) : 0;
            MMOPlayerInteractionService.SetTradeCopper(displayedTradeId, copper);
        }

        private void CancelDisplayedTrade()
        {
            if (!string.IsNullOrWhiteSpace(displayedTradeId))
            {
                MMOPlayerInteractionService.CancelTrade(displayedTradeId);
            }

            tradeWindowObject?.SetActive(false);
        }

        private Image CreateTooltipPanel(string objectName, Transform parent)
        {
            Image panel = MMOUiFactory.CreateImage(objectName, parent, MMONpcWindowFrame.BackgroundColor);
            Sprite sprite = Resources.Load<Sprite>(TooltipPanelResourcePath);
            if (sprite != null)
            {
                panel.sprite = sprite;
                panel.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                panel.color = Color.white;
            }
            else
            {
                Outline outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = MMONpcWindowFrame.BorderColor;
            }

            return panel;
        }

        private Vector2 ClampToCanvas(Vector2 position, Vector2 size)
        {
            Rect rect = ((RectTransform)canvas.transform).rect;
            return new Vector2(
                Mathf.Clamp(position.x, rect.xMin, rect.xMax - size.x),
                Mathf.Clamp(position.y, rect.yMin + size.y, rect.yMax));
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimePresenter()
        {
            if (FindAnyObjectByType<MMOPlayerInteractionPresenter>() == null)
            {
                new GameObject("Player Interaction UI").AddComponent<MMOPlayerInteractionPresenter>();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRuntimePresenter();
        }
    }

    public sealed class MMOTradeSlotDropTarget : MonoBehaviour, IDropHandler, IPointerClickHandler, IMMOSlotDropTarget
    {
        private int offerSlotIndex;

        public void Configure(int index)
        {
            offerSlotIndex = index;
        }

        public void OnDrop(PointerEventData eventData)
        {
            MMOSlotDragPayload payload = MMOSlotDragState.Current;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindCurrentTrade(MMOGameplaySessionService.LocalPlayer.CharacterId);
            if (trade != null && EvaluateDrop(payload) == MMOSlotDropState.Valid)
            {
                MMOPlayerInteractionService.SetTradeOffer(
                    trade.tradeId,
                    offerSlotIndex,
                    payload.SourceSlotIndex,
                    payload.Quantity);
            }

            MMOSlotDragState.EndDrag();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindCurrentTrade(MMOGameplaySessionService.LocalPlayer.CharacterId);
            if (trade != null)
            {
                MMOPlayerInteractionService.SetTradeOffer(trade.tradeId, offerSlotIndex, -1, 0);
            }
        }

        public MMOSlotDropState EvaluateDrop(MMOSlotDragPayload payload)
        {
            MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOInventoryContainer localInventory);
            if (!payload.FromInventory || payload.Item == null || payload.Quantity <= 0
                || payload.Item.ItemType == MMOItemType.Quest
                || payload.SourceInventory == null || payload.SourceInventory != localInventory)
            {
                return MMOSlotDropState.Invalid;
            }

            return MMOPlayerInteractionState.FindCurrentTrade(MMOGameplaySessionService.LocalPlayer.CharacterId) != null
                ? MMOSlotDropState.Valid
                : MMOSlotDropState.Invalid;
        }
    }
}
