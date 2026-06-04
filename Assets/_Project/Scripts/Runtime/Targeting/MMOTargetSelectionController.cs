using System;
using RPGClone.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Targeting
{
    public sealed class MMOTargetSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera selectionCamera;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField, Min(1f)] private float maxSelectionDistance = 250f;
        [SerializeField] private bool clearTargetOnMiss = true;
        [SerializeField] private bool ignorePointerOverUi = true;

        public event Action<MMOCharacterIdentity> TargetChanged;

        public MMOCharacterIdentity CurrentTarget { get; private set; }

        public void SetSelectionCamera(Camera newSelectionCamera)
        {
            selectionCamera = newSelectionCamera;
        }

        private void Awake()
        {
            if (selectionCamera == null)
            {
                selectionCamera = Camera.main;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || mouse.rightButton.isPressed)
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

        private void TrySelectFromPointer(Vector2 pointerPosition)
        {
            if (selectionCamera == null)
            {
                selectionCamera = Camera.main;
            }

            if (selectionCamera == null)
            {
                return;
            }

            Ray ray = selectionCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance, selectionMask, QueryTriggerInteraction.Ignore))
            {
                if (clearTargetOnMiss)
                {
                    ClearTarget();
                }

                return;
            }

            MMOCharacterIdentity target = hit.collider.GetComponentInParent<MMOCharacterIdentity>();
            if (target != null && target.Selectable)
            {
                SelectTarget(target);
                return;
            }

            if (clearTargetOnMiss)
            {
                ClearTarget();
            }
        }
    }
}
