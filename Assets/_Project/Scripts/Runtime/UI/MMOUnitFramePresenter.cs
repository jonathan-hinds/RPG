using RPGClone.Characters;
using RPGClone.Services;
using RPGClone.Targeting;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.UI
{
    public sealed class MMOUnitFramePresenter : MonoBehaviour
    {
        private const string ThemeResourcePath = "RPGClone/UI/UnitFrames/ClassicUnitFrameTheme";

        [SerializeField] private MMOCharacterIdentity playerIdentity;
        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private MMOUnitFrameView playerFrame;
        [SerializeField] private MMOUnitFrameView targetFrame;
        [SerializeField] private bool autoResolveReferences = true;
        [SerializeField] private bool showPartyFrames = true;

        private readonly List<MMOUnitFrameView> partyFrames = new();
        private RectTransform partyFrameRoot;
        private MMOUnitFrameCastBarPresenter targetCastBar;
        private MMOUnitFrameTheme unitFrameTheme;

        private void Start()
        {
            ResolveReferences();
            BindFrames();
            RebuildPartyFrames();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged += OnTargetChanged;
            }

            MMOGameplaySessionService.Players.Changed -= OnPlayersChanged;
            MMOGameplaySessionService.Players.Changed += OnPlayersChanged;
            MMOGameplaySessionService.LocalPlayer.Changed -= OnLocalPlayerChanged;
            MMOGameplaySessionService.LocalPlayer.Changed += OnLocalPlayerChanged;
            BindFrames();
            RebuildPartyFrames();
        }

        private void OnDisable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
            }

            UnsubscribePartyFrames();
            MMOGameplaySessionService.Players.Changed -= OnPlayersChanged;
            MMOGameplaySessionService.LocalPlayer.Changed -= OnLocalPlayerChanged;
        }

        public void Configure(
            MMOCharacterIdentity newPlayerIdentity,
            MMOTargetSelectionController newTargetSelectionController,
            MMOUnitFrameView newPlayerFrame,
            MMOUnitFrameView newTargetFrame)
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
            }

            playerIdentity = newPlayerIdentity;
            targetSelectionController = newTargetSelectionController;
            playerFrame = newPlayerFrame;
            targetFrame = newTargetFrame;
            ConfigurePrimaryFrameStyles();

            if (isActiveAndEnabled && targetSelectionController != null)
            {
                targetSelectionController.TargetChanged += OnTargetChanged;
            }

            BindFrames();
            RebuildPartyFrames();
        }

        private void ResolveReferences()
        {
            if (!autoResolveReferences)
            {
                return;
            }

            if (playerIdentity == null)
            {
                playerIdentity = MMOGameplaySessionService.LocalPlayer.Identity;
            }

            if (targetSelectionController == null)
            {
                targetSelectionController = FindAnyObjectByType<MMOTargetSelectionController>();
            }

            if (playerFrame == null)
            {
                Transform playerFrameTransform = transform.Find("Player Unit Frame");
                playerFrame = playerFrameTransform != null ? playerFrameTransform.GetComponent<MMOUnitFrameView>() : null;
            }

            if (targetFrame == null)
            {
                Transform targetFrameTransform = transform.Find("Target Unit Frame");
                targetFrame = targetFrameTransform != null ? targetFrameTransform.GetComponent<MMOUnitFrameView>() : null;
            }

            ConfigurePrimaryFrameStyles();
            EnsureTargetCastBar();
        }

        private void ConfigurePrimaryFrameStyles()
        {
            MMOUnitFrameTheme resolvedTheme = ResolveTheme();
            playerFrame?.ConfigureStyle(MMOUnitFrameStyle.Player, resolvedTheme);
            targetFrame?.ConfigureStyle(MMOUnitFrameStyle.Target, resolvedTheme);
        }

        private void BindFrames()
        {
            if (playerFrame != null)
            {
                playerFrame.Bind(playerIdentity);
            }

            if (targetFrame != null)
            {
                MMOCharacterIdentity target = targetSelectionController != null ? targetSelectionController.CurrentTarget : null;
                targetFrame.Bind(target);
                targetCastBar?.Bind(target);
            }
        }

        private void OnPlayersChanged()
        {
            ResolveReferences();
            BindFrames();
            RebuildPartyFrames();
        }

        private void OnLocalPlayerChanged()
        {
            ResolveReferences();
            BindFrames();
            RebuildPartyFrames();
        }

        private void OnTargetChanged(MMOCharacterIdentity target)
        {
            if (targetFrame != null)
            {
                targetFrame.Bind(target);
                targetCastBar?.Bind(target);
            }
        }

        private void EnsureTargetCastBar()
        {
            if (targetFrame == null || targetCastBar != null)
            {
                return;
            }

            Transform existing = targetFrame.transform.Find("Target Cast Bar");
            if (existing != null)
            {
                targetCastBar = existing.GetComponent<MMOUnitFrameCastBarPresenter>();
            }

            if (targetCastBar == null)
            {
                GameObject castBarObject = new("Target Cast Bar", typeof(RectTransform));
                castBarObject.transform.SetParent(targetFrame.transform, false);
                targetCastBar = castBarObject.AddComponent<MMOUnitFrameCastBarPresenter>();
            }
        }

        private void RebuildPartyFrames()
        {
            if (!showPartyFrames || playerFrame == null)
            {
                return;
            }

            EnsurePartyFrameRoot();
            UnsubscribePartyFrames();
            foreach (MMOUnitFrameView frame in partyFrames)
            {
                if (frame != null)
                {
                    Destroy(frame.gameObject);
                }
            }

            partyFrames.Clear();
            for (int i = partyFrameRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(partyFrameRoot.GetChild(i).gameObject);
            }

            int index = 0;
            MMOPartySnapshot party = MMOGameplaySessionService.Party.GetCurrentParty();
            foreach (MMOPartyMember member in party.members)
            {
                if (member == null
                    || member.isLocal
                    || !MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(member.characterId, out MMOPlayerParticipant participant)
                    || !participant.IsValid)
                {
                    continue;
                }

                MMOUnitFrameView frame = CreatePartyFrame(index);
                frame.Bind(participant.Identity);
                frame.Clicked += OnPartyFrameClicked;
                partyFrames.Add(frame);
                index++;
            }
        }

        private void OnPartyFrameClicked(MMOUnitFrameView frame, MMOCharacterIdentity character)
        {
            if (targetSelectionController == null || character == null || !character.Selectable)
            {
                return;
            }

            targetSelectionController.SelectTarget(character);
        }

        private void UnsubscribePartyFrames()
        {
            foreach (MMOUnitFrameView frame in partyFrames)
            {
                if (frame != null)
                {
                    frame.Clicked -= OnPartyFrameClicked;
                }
            }
        }

        private void EnsurePartyFrameRoot()
        {
            if (partyFrameRoot != null)
            {
                return;
            }

            RectTransform playerRect = playerFrame.transform as RectTransform;
            Transform parent = playerFrame.transform.parent;
            Transform existing = parent != null ? parent.Find("Party Frames") : null;
            GameObject rootObject = existing != null ? existing.gameObject : new GameObject("Party Frames", typeof(RectTransform));
            partyFrameRoot = (RectTransform)rootObject.transform;
            partyFrameRoot.SetParent(parent, false);
            partyFrameRoot.anchorMin = playerRect != null ? playerRect.anchorMin : new Vector2(0f, 1f);
            partyFrameRoot.anchorMax = playerRect != null ? playerRect.anchorMax : new Vector2(0f, 1f);
            partyFrameRoot.pivot = playerRect != null ? playerRect.pivot : new Vector2(0f, 1f);
            partyFrameRoot.anchoredPosition = playerRect != null
                ? playerRect.anchoredPosition + new Vector2(0f, -108f)
                : new Vector2(18f, -128f);
            Vector2 frameSize = GetPartyFrameSize();
            partyFrameRoot.sizeDelta = new Vector2(frameSize.x, (frameSize.y + 10f) * 4f);
        }

        private MMOUnitFrameView CreatePartyFrame(int index)
        {
            GameObject frameObject = new($"Party Frame {index + 1}", typeof(RectTransform));
            frameObject.SetActive(false);
            RectTransform rect = (RectTransform)frameObject.transform;
            rect.SetParent(partyFrameRoot, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            Vector2 frameSize = GetPartyFrameSize();
            rect.anchoredPosition = new Vector2(0f, -index * (frameSize.y + 10f));
            rect.sizeDelta = frameSize;

            MMOUnitFrameView frame = frameObject.AddComponent<MMOUnitFrameView>();
            frame.ConfigureStyle(MMOUnitFrameStyle.Party, ResolveTheme());
            frameObject.SetActive(true);
            return frame;
        }

        private Vector2 GetPartyFrameSize()
        {
            MMOUnitFrameTheme resolvedTheme = ResolveTheme();
            return resolvedTheme != null
                ? resolvedTheme.GetFrameSize(MMOUnitFrameStyle.Party)
                : new Vector2(250f, 68f);
        }

        private MMOUnitFrameTheme ResolveTheme()
        {
            if (unitFrameTheme == null)
            {
                unitFrameTheme = Resources.Load<MMOUnitFrameTheme>(ThemeResourcePath);
            }

            return unitFrameTheme;
        }
    }
}
