using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace RPGClone.Targeting
{
    [DisallowMultipleComponent]
    public sealed class MMOTargetSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera selectionCamera;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField, Min(1f)] private float maxSelectionDistance = 250f;
        [SerializeField] private bool ignorePointerOverUi = true;
        [FormerlySerializedAs("showSelectionRing")]
        [SerializeField] private bool showSelectionIndicator = true;

        [Header("Tab Targeting")]
        [SerializeField] private bool enableTabTargeting = true;
        [SerializeField, Min(1f)] private float tabTargetMaxDistance = 45f;
        [SerializeField, Range(0f, 0.5f)] private float tabTargetViewportPadding = 0.08f;
        [SerializeField, Min(0f)] private float tabTargetScreenCenterWeight = 2.25f;
        [SerializeField, Min(0f)] private float tabTargetDistanceWeight = 1f;

        private readonly List<TabTargetCandidate> tabCandidates = new(32);

        public event Action<MMOCharacterIdentity> TargetChanged;

        public MMOCharacterIdentity CurrentTarget { get; private set; }
        public MMOTargetContext CurrentTargetContext => new(CurrentTarget);

        public void SetSelectionCamera(Camera newSelectionCamera)
        {
            selectionCamera = newSelectionCamera;
        }

        private void Awake()
        {
            ResolveSelectionCamera();
            EnsureSelectionIndicatorPresenter();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClearTarget();
                return;
            }

            if (enableTabTargeting
                && keyboard != null
                && keyboard.tabKey.wasPressedThisFrame
                && !IsKeyboardInputCaptured()
                && !MMOGroundTargetingController.IsAnyTargeting)
            {
                bool reverse = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                CycleHostileTarget(reverse ? -1 : 1);
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || mouse.rightButton.isPressed)
            {
                return;
            }

            if (MMOGroundTargetingController.IsAnyTargeting)
            {
                return;
            }

            if (ignorePointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            TrySelectFromPointer(mouse.position.ReadValue());
        }

        public void SelectTarget(MMOCharacterIdentity target)
        {
            if (target != null && !target.Selectable)
            {
                return;
            }

            if (CurrentTarget == target)
            {
                return;
            }

            CurrentTarget = target;
            TargetChanged?.Invoke(CurrentTarget);
        }

        public void ClearTarget()
        {
            SelectTarget(null);
        }

        public bool CycleHostileTarget(int direction)
        {
            ResolveSelectionCamera();

            MMOCharacterIdentity localPlayer = MMOGameplaySessionService.LocalPlayer.Identity;
            Transform localPlayerTransform = MMOGameplaySessionService.LocalPlayer.PlayerTransform;
            if (selectionCamera == null || localPlayer == null || localPlayerTransform == null)
            {
                return false;
            }

            BuildTabTargetCandidates(localPlayer, localPlayerTransform.position);
            if (tabCandidates.Count == 0)
            {
                return false;
            }

            tabCandidates.Sort(TabTargetCandidate.Compare);
            int currentIndex = tabCandidates.FindIndex(candidate => candidate.Identity == CurrentTarget);
            int step = direction < 0 ? -1 : 1;
            int nextIndex = currentIndex < 0
                ? 0
                : (currentIndex + step + tabCandidates.Count) % tabCandidates.Count;

            SelectTarget(tabCandidates[nextIndex].Identity);
            return true;
        }

        private void BuildTabTargetCandidates(MMOCharacterIdentity localPlayer, Vector3 origin)
        {
            tabCandidates.Clear();
            float maxDistanceSquared = tabTargetMaxDistance * tabTargetMaxDistance;

            foreach (MMOCombatant combatant in MMOCombatant.ActiveCombatants)
            {
                if (combatant == null || !combatant.isActiveAndEnabled || !combatant.IsAlive)
                {
                    continue;
                }

                MMOCharacterIdentity candidate = combatant.Identity;
                if (candidate == null
                    || candidate == localPlayer
                    || !candidate.Selectable
                    || !MMOFactionRules.CanDamage(localPlayer, candidate))
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - origin;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > maxDistanceSquared)
                {
                    continue;
                }

                Vector3 viewportPosition = selectionCamera.WorldToViewportPoint(ResolveAimPoint(candidate));
                if (viewportPosition.z <= 0f
                    || viewportPosition.x < -tabTargetViewportPadding
                    || viewportPosition.x > 1f + tabTargetViewportPadding
                    || viewportPosition.y < -tabTargetViewportPadding
                    || viewportPosition.y > 1f + tabTargetViewportPadding)
                {
                    continue;
                }

                Vector2 viewportOffset = new(viewportPosition.x - 0.5f, viewportPosition.y - 0.5f);
                float normalizedDistance = Mathf.Sqrt(distanceSquared) / tabTargetMaxDistance;
                float score = viewportOffset.sqrMagnitude * tabTargetScreenCenterWeight
                    + normalizedDistance * tabTargetDistanceWeight;
                tabCandidates.Add(new TabTargetCandidate(candidate, score));
            }
        }

        private void TrySelectFromPointer(Vector2 pointerPosition)
        {
            ResolveSelectionCamera();
            if (selectionCamera == null)
            {
                return;
            }

            Ray ray = selectionCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance, selectionMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            MMOCharacterIdentity target = hit.collider.GetComponentInParent<MMOCharacterIdentity>();
            if (target != null && target.Selectable)
            {
                SelectTarget(target);
            }
        }

        private void ResolveSelectionCamera()
        {
            if (selectionCamera == null)
            {
                selectionCamera = MMORuntimeSceneReferences.MainCamera;
            }
        }

        private void EnsureSelectionIndicatorPresenter()
        {
            if (!showSelectionIndicator || GetComponent<MMOSelectionIndicatorPresenter>() != null)
            {
                return;
            }

            gameObject.AddComponent<MMOSelectionIndicatorPresenter>();
        }

        private static bool IsKeyboardInputCaptured()
        {
            GameObject selectedObject = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            return selectedObject != null
                && (selectedObject.GetComponent<InputField>() != null
                    || selectedObject.GetComponent<TMP_InputField>() != null);
        }

        private static Vector3 ResolveAimPoint(MMOCharacterIdentity identity)
        {
            Collider collider = identity != null ? identity.GetComponentInChildren<Collider>() : null;
            return collider != null ? collider.bounds.center : identity.transform.position + Vector3.up;
        }

        private readonly struct TabTargetCandidate
        {
            public TabTargetCandidate(MMOCharacterIdentity identity, float score)
            {
                Identity = identity;
                Score = score;
            }

            public MMOCharacterIdentity Identity { get; }
            private float Score { get; }

            public static int Compare(TabTargetCandidate left, TabTargetCandidate right)
            {
                int scoreComparison = left.Score.CompareTo(right.Score);
                return scoreComparison != 0
                    ? scoreComparison
                    : left.Identity.GetInstanceID().CompareTo(right.Identity.GetInstanceID());
            }
        }
    }
}
