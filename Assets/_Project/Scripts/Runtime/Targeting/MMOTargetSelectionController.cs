using System;
using RPGClone.Characters;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace RPGClone.Targeting
{
    public sealed class MMOTargetSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera selectionCamera;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField, Min(1f)] private float maxSelectionDistance = 250f;
        [SerializeField] private bool ignorePointerOverUi = true;
        [SerializeField] private bool showSelectionRing = true;

        public event Action<MMOCharacterIdentity> TargetChanged;

        public MMOCharacterIdentity CurrentTarget { get; private set; }
        public MMOTargetContext CurrentTargetContext => new(CurrentTarget);

        public void SetSelectionCamera(Camera newSelectionCamera)
        {
            selectionCamera = newSelectionCamera;
        }

        private void Awake()
        {
            if (selectionCamera == null)
            {
                selectionCamera = MMORuntimeSceneReferences.MainCamera;
            }

            EnsureSelectionRingPresenter();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClearTarget();
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

        private void TrySelectFromPointer(Vector2 pointerPosition)
        {
            if (selectionCamera == null)
            {
                selectionCamera = MMORuntimeSceneReferences.MainCamera;
            }

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
                return;
            }

        }

        private void EnsureSelectionRingPresenter()
        {
            if (!showSelectionRing || GetComponent<MMOSelectionRingPresenter>() != null)
            {
                return;
            }

            gameObject.AddComponent<MMOSelectionRingPresenter>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class MMOSelectionRingPresenter : MonoBehaviour
    {
        private const int CircleSegments = 96;
        private const float DefaultRadius = 0.8f;

        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0.1f)] private float minimumRadius = 0.55f;
        [SerializeField, Min(0.1f)] private float radiusMultiplier = 1.15f;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.045f;
        [SerializeField, Min(0.001f)] private float groundOffset = 0.045f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 5f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 24f;
        [SerializeField] private bool hideForLocalPlayer = true;
        [SerializeField] private Color ringColor = new(0.95f, 0.78f, 0.24f, 0.88f);

        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private MMOCharacterIdentity target;
        private GameObject ringRoot;
        private LineRenderer ringRenderer;
        private Material ringMaterial;

        private void Awake()
        {
            ResolveController();
            CreateRingIfNeeded();
            SetRingVisible(false);
        }

        private void OnEnable()
        {
            ResolveController();
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
                targetSelectionController.TargetChanged += OnTargetChanged;
                OnTargetChanged(targetSelectionController.CurrentTarget);
            }
        }

        private void OnDisable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
            }

            SetRingVisible(false);
        }

        private void OnDestroy()
        {
            if (ringRoot != null)
            {
                Destroy(ringRoot);
            }

            if (ringMaterial != null)
            {
                Destroy(ringMaterial);
            }
        }

        private void LateUpdate()
        {
            if (!ShouldShowRing())
            {
                SetRingVisible(false);
                return;
            }

            UpdateRingTransform();
        }

        private void OnTargetChanged(MMOCharacterIdentity newTarget)
        {
            target = newTarget;
            SetRingVisible(ShouldShowRing());
        }

        private bool ShouldShowRing()
        {
            return target != null
                && target.Selectable
                && (!hideForLocalPlayer || MMOGameplaySessionService.LocalPlayer.Identity != target);
        }

        private void ResolveController()
        {
            if (targetSelectionController == null)
            {
                targetSelectionController = GetComponent<MMOTargetSelectionController>();
            }
        }

        private void CreateRingIfNeeded()
        {
            if (ringRoot != null)
            {
                return;
            }

            ringRoot = new GameObject("Selected Target Ring")
            {
                hideFlags = HideFlags.DontSave
            };

            ringRenderer = ringRoot.AddComponent<LineRenderer>();
            ringMaterial = CreateRingMaterial(ringColor);
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.useWorldSpace = false;
            ringRenderer.loop = true;
            ringRenderer.positionCount = CircleSegments;
            ringRenderer.widthMultiplier = lineWidth;
            ringRenderer.alignment = LineAlignment.TransformZ;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
        }

        private void UpdateRingTransform()
        {
            CreateRingIfNeeded();
            Bounds targetBounds = ResolveTargetBounds(target);
            float radius = ResolveRingRadius(targetBounds);
            Vector3 probeCenter = targetBounds.size == Vector3.zero ? target.transform.position : targetBounds.center;

            if (TryResolveGroundPose(probeCenter, target, out Vector3 groundPosition, out Vector3 groundNormal))
            {
                ringRoot.transform.SetPositionAndRotation(
                    groundPosition + groundNormal * groundOffset,
                    Quaternion.FromToRotation(Vector3.up, groundNormal));
            }
            else
            {
                ringRoot.transform.SetPositionAndRotation(
                    new Vector3(probeCenter.x, targetBounds.min.y + groundOffset, probeCenter.z),
                    Quaternion.identity);
            }

            ringRoot.transform.localScale = new Vector3(radius, radius, radius);
            SetRingVisible(true);
        }

        private bool TryResolveGroundPose(
            Vector3 probeCenter,
            MMOCharacterIdentity selectedTarget,
            out Vector3 groundPosition,
            out Vector3 groundNormal)
        {
            Vector3 rayOrigin = probeCenter + Vector3.up * groundProbeHeight;
            float rayDistance = groundProbeHeight + groundProbeDistance;
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                groundHits,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearestGroundHit = default;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = groundHits[i].collider;
                if (hitCollider == null
                    || selectedTarget != null && hitCollider.transform.IsChildOf(selectedTarget.transform))
                {
                    continue;
                }

                if (groundHits[i].distance < nearestDistance)
                {
                    nearestDistance = groundHits[i].distance;
                    nearestGroundHit = groundHits[i];
                }
            }

            if (nearestGroundHit.collider != null)
            {
                groundPosition = nearestGroundHit.point;
                groundNormal = nearestGroundHit.normal.sqrMagnitude > 0.001f ? nearestGroundHit.normal.normalized : Vector3.up;
                return true;
            }

            if (NavMesh.SamplePosition(probeCenter, out NavMeshHit navHit, groundProbeDistance, NavMesh.AllAreas))
            {
                groundPosition = navHit.position;
                groundNormal = Vector3.up;
                return true;
            }

            groundPosition = default;
            groundNormal = Vector3.up;
            return false;
        }

        private static Bounds ResolveTargetBounds(MMOCharacterIdentity selectedTarget)
        {
            if (selectedTarget == null)
            {
                return default;
            }

            Collider[] colliders = selectedTarget.GetComponentsInChildren<Collider>();
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (hasBounds)
            {
                return bounds;
            }

            return new Bounds(selectedTarget.transform.position, Vector3.zero);
        }

        private float ResolveRingRadius(Bounds targetBounds)
        {
            if (targetBounds.size == Vector3.zero)
            {
                return Mathf.Max(minimumRadius, DefaultRadius);
            }

            float extents = Mathf.Max(targetBounds.extents.x, targetBounds.extents.z);
            return Mathf.Max(minimumRadius, extents * radiusMultiplier);
        }

        private static Material CreateRingMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            return new Material(shader)
            {
                color = color
            };
        }

        private void SetRingVisible(bool visible)
        {
            if (ringRoot != null && ringRoot.activeSelf != visible)
            {
                ringRoot.SetActive(visible);
            }
        }
    }
}
