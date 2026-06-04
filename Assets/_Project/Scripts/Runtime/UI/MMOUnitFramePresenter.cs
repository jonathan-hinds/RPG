using RPGClone.Characters;
using RPGClone.Targeting;
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

        private void Start()
        {
            ResolveReferences();
            BindFrames();
        }

        private void OnEnable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged += OnTargetChanged;
            }
        }

        private void OnDisable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
            }
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
        }

        private void ResolveReferences()
        {
            if (!autoResolveReferences)
            {
                return;
            }

            if (playerIdentity == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerIdentity = player.GetComponentInChildren<MMOCharacterIdentity>();
                }
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

        private void OnTargetChanged(MMOCharacterIdentity target)
        {
            if (targetFrame != null)
            {
                targetFrame.Bind(target);
            }
        }
    }
}
