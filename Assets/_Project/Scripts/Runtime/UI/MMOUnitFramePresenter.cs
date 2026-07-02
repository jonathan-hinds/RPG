using RPGClone.Characters;
using RPGClone.Services;
using RPGClone.Targeting;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.UI
{
    public sealed class MMOUnitFramePresenter : MonoBehaviour
    {
        [SerializeField] private MMOCharacterIdentity playerIdentity;
        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private MMOUnitFrameView playerFrame;
        [SerializeField] private MMOUnitFrameView targetFrame;
        [SerializeField] private bool autoResolveReferences = true;
        [SerializeField] private bool showPartyFrames = true;

        private readonly List<MMOUnitFrameView> partyFrames = new();
        private RectTransform partyFrameRoot;

        private void Start()
        {
            ResolveReferences();
            BindFrames();
            RebuildPartyFrames();
        }

        private void OnEnable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged += OnTargetChanged;
            }

            MMOGameplaySessionService.Players.Changed -= OnPlayersChanged;
            MMOGameplaySessionService.Players.Changed += OnPlayersChanged;
        }

        private void OnDisable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
            }

            MMOGameplaySessionService.Players.Changed -= OnPlayersChanged;
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
        }

        private void BindFrames()
        {
            if (playerFrame != null)
            {
                playerFrame.Bind(playerIdentity);
            }

            if (targetFrame != null)
            {
                targetFrame.Bind(targetSelectionController != null ? targetSelectionController.CurrentTarget : null);
            }
        }

        private void OnPlayersChanged()
        {
            RebuildPartyFrames();
        }

        private void OnTargetChanged(MMOCharacterIdentity target)
        {
            if (targetFrame != null)
            {
                targetFrame.Bind(target);
            }
        }

        private void RebuildPartyFrames()
        {
            if (!showPartyFrames || playerFrame == null)
            {
                return;
            }

            EnsurePartyFrameRoot();
            foreach (MMOUnitFrameView frame in partyFrames)
            {
                if (frame != null)
                {
                    Destroy(frame.gameObject);
                }
            }

            partyFrames.Clear();
            int index = 0;
            foreach (MMOPlayerParticipant participant in MMOGameplaySessionService.Players.Participants)
            {
                if (!participant.IsValid || participant.IsLocal)
                {
                    continue;
                }

                MMOUnitFrameView frame = CreatePartyFrame(index);
                frame.Bind(participant.Identity);
                partyFrames.Add(frame);
                index++;
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
            GameObject rootObject = new("Party Frames", typeof(RectTransform));
            partyFrameRoot = (RectTransform)rootObject.transform;
            partyFrameRoot.SetParent(parent, false);
            partyFrameRoot.anchorMin = playerRect != null ? playerRect.anchorMin : new Vector2(0f, 1f);
            partyFrameRoot.anchorMax = playerRect != null ? playerRect.anchorMax : new Vector2(0f, 1f);
            partyFrameRoot.pivot = playerRect != null ? playerRect.pivot : new Vector2(0f, 1f);
            partyFrameRoot.anchoredPosition = playerRect != null
                ? playerRect.anchoredPosition + new Vector2(0f, -88f)
                : new Vector2(18f, -108f);
            partyFrameRoot.sizeDelta = new Vector2(190f, 260f);
        }

        private MMOUnitFrameView CreatePartyFrame(int index)
        {
            GameObject frameObject = new($"Party Frame {index + 1}", typeof(RectTransform));
            RectTransform rect = (RectTransform)frameObject.transform;
            rect.SetParent(partyFrameRoot, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 54f);
            rect.sizeDelta = new Vector2(190f, 48f);
            return frameObject.AddComponent<MMOUnitFrameView>();
        }
    }
}
