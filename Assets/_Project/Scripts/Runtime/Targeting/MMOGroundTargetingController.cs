using RPGClone.Abilities;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Targeting
{
    public sealed class MMOGroundTargetingController : MonoBehaviour
    {
        private const int CircleSegments = 96;
        private const float IndicatorLift = 0.04f;

        [SerializeField] private Camera targetingCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;
        [SerializeField] private Color validColor = new(0.95f, 0.72f, 0.28f, 0.42f);
        [SerializeField] private Color invalidColor = new(1f, 0.08f, 0.03f, 0.48f);

        private MMOAbilitySystem abilitySystem;
        private MMOAbilityDefinition ability;
        private GameObject indicatorRoot;
        private MeshRenderer diskRenderer;
        private LineRenderer ringRenderer;
        private Material diskMaterial;
        private Material ringMaterial;
        private Vector3 currentPosition;
        private bool hasCurrentPosition;
        private bool currentPositionInRange;

        public static bool IsAnyTargeting { get; private set; }
        public bool IsTargeting => abilitySystem != null && ability != null;

        private void Awake()
        {
            ResolveCamera();
            CreateIndicatorIfNeeded();
            SetIndicatorVisible(false);
        }

        private void OnDisable()
        {
            CancelTargeting();
        }

        private void OnDestroy()
        {
            if (indicatorRoot != null)
            {
                Destroy(indicatorRoot);
            }

            if (diskMaterial != null)
            {
                Destroy(diskMaterial);
            }

            if (ringMaterial != null)
            {
                Destroy(ringMaterial);
            }
        }

        private void Update()
        {
            if (!IsTargeting)
            {
                return;
            }

            UpdateIndicator();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                CancelTargeting();
                return;
            }

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!hasCurrentPosition || !currentPositionInRange)
            {
                abilitySystem.TryUseAbilityAtPosition(ability, currentPosition, out _);
                return;
            }

            if (abilitySystem.TryUseAbilityAtPosition(ability, currentPosition, out _))
            {
                CancelTargeting();
            }
        }

        public void BeginTargeting(MMOAbilitySystem newAbilitySystem, MMOAbilityDefinition newAbility)
        {
            if (newAbilitySystem == null || newAbility == null || !newAbility.RequiresGroundTarget)
            {
                return;
            }

            abilitySystem = newAbilitySystem;
            ability = newAbility;
            IsAnyTargeting = true;
            CreateIndicatorIfNeeded();
            SetIndicatorVisible(true);
            UpdateIndicator();
        }

        public void CancelTargeting()
        {
            bool wasTargeting = IsTargeting;
            abilitySystem = null;
            ability = null;
            hasCurrentPosition = false;
            SetIndicatorVisible(false);
            if (wasTargeting)
            {
                IsAnyTargeting = false;
            }
        }

        private void UpdateIndicator()
        {
            ResolveCamera();
            hasCurrentPosition = TryResolvePointerPosition(out currentPosition);
            currentPositionInRange = hasCurrentPosition && abilitySystem != null && abilitySystem.IsPositionInRange(currentPosition, ability.Range);
            SetIndicatorColor(currentPositionInRange ? validColor : invalidColor);

            if (!hasCurrentPosition)
            {
                SetIndicatorVisible(false);
                return;
            }

            SetIndicatorVisible(true);
            indicatorRoot.transform.position = currentPosition + Vector3.up * IndicatorLift;
            float radius = Mathf.Max(0.1f, ability.AreaRadius);
            indicatorRoot.transform.localScale = new Vector3(radius, 1f, radius);
        }

        private bool TryResolvePointerPosition(out Vector3 position)
        {
            position = default;
            Mouse mouse = Mouse.current;
            if (targetingCamera == null || mouse == null)
            {
                return false;
            }

            Ray ray = targetingCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                position = ProjectToGround(hit.point);
                return true;
            }

            Plane fallbackPlane = new(Vector3.up, abilitySystem != null ? abilitySystem.transform.position : Vector3.zero);
            if (!fallbackPlane.Raycast(ray, out float distance))
            {
                return false;
            }

            position = ProjectToGround(ray.GetPoint(distance));
            return true;
        }

        private Vector3 ProjectToGround(Vector3 candidate)
        {
            return NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas)
                ? navHit.position
                : candidate;
        }

        private void ResolveCamera()
        {
            if (targetingCamera == null)
            {
                targetingCamera = MMORuntimeSceneReferences.MainCamera;
            }
        }

        private void CreateIndicatorIfNeeded()
        {
            if (indicatorRoot != null)
            {
                return;
            }

            indicatorRoot = new GameObject("Ground Targeting Indicator")
            {
                hideFlags = HideFlags.DontSave
            };

            MeshFilter diskFilter = indicatorRoot.AddComponent<MeshFilter>();
            diskFilter.sharedMesh = CreateDiskMesh();
            diskRenderer = indicatorRoot.AddComponent<MeshRenderer>();
            diskMaterial = CreateTransparentMaterial(validColor);
            diskRenderer.sharedMaterial = diskMaterial;

            GameObject ringObject = new("Ring");
            ringObject.transform.SetParent(indicatorRoot.transform, false);
            ringRenderer = ringObject.AddComponent<LineRenderer>();
            ringMaterial = CreateTransparentMaterial(validColor);
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.useWorldSpace = false;
            ringRenderer.loop = true;
            ringRenderer.widthMultiplier = 0.035f;
            ringRenderer.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), 0.02f, Mathf.Sin(angle)));
            }
        }

        private static Mesh CreateDiskMesh()
        {
            Vector3[] vertices = new Vector3[CircleSegments + 1];
            int[] triangles = new int[CircleSegments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            for (int i = 0; i < CircleSegments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == CircleSegments - 1 ? 1 : i + 2;
            }

            Mesh mesh = new()
            {
                name = "GroundTargetingDisk"
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            return new Material(shader)
            {
                color = color
            };
        }

        private void SetIndicatorColor(Color color)
        {
            if (diskMaterial != null)
            {
                diskMaterial.color = color;
            }

            if (ringMaterial != null)
            {
                ringMaterial.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.25f));
            }
        }

        private void SetIndicatorVisible(bool visible)
        {
            if (indicatorRoot != null)
            {
                indicatorRoot.SetActive(visible);
            }
        }
    }
}
