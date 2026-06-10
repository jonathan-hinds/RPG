using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Services;
using RPGClone.UI;
using RPGClone.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Quests
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOStandardNpcIdentity))]
    public sealed class MMOQuestNpc : MonoBehaviour
    {
        private const float ReferenceRetrySeconds = 0.25f;

        [SerializeField] private string npcId = "npc";
        [SerializeField] private string displayNameOverride;
        [SerializeField] private List<MMOQuestDefinition> offeredQuests = new();
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private bool snapToGroundOnStart = true;

        private TextMesh questMarker;
        private MMOQuestLog observedQuestLog;
        private MMOCharacterIdentity identity;
        private MMOStandardNpcIdentity standardIdentity;
        private string currentMarkerText = string.Empty;
        private float nextReferenceResolveTime;

        public string NpcId => string.IsNullOrWhiteSpace(npcId) ? name : npcId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride) ? name : displayNameOverride;
        public IReadOnlyList<MMOQuestDefinition> OfferedQuests => offeredQuests;
        public float InteractionDistance => interactionDistance;

        private void Awake()
        {
            EnsureIdentity();
            EnsureMarker();
            RefreshMarker();
        }

        private void Start()
        {
            if (snapToGroundOnStart)
            {
                MMOGroundingUtility.SnapTransformToGround(transform, GetComponent<Collider>());
            }
        }

        private void OnEnable()
        {
            SubscribeToPlayerQuestLog();
        }

        private void OnDisable()
        {
            if (observedQuestLog != null)
            {
                observedQuestLog.Changed -= OnQuestLogChanged;
            }

            observedQuestLog = null;
        }

        private void Update()
        {
            if (observedQuestLog == null && Time.unscaledTime >= nextReferenceResolveTime)
            {
                nextReferenceResolveTime = Time.unscaledTime + ReferenceRetrySeconds;
                SubscribeToPlayerQuestLog();
            }

            FaceMarkerToCamera();

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            if (IsPointerOverThisNpc(pointerPosition))
            {
                Interact(pointerPosition);
            }
        }

        public void Configure(string newNpcId, string newDisplayName, IEnumerable<MMOQuestDefinition> newOfferedQuests)
        {
            npcId = newNpcId;
            displayNameOverride = newDisplayName;
            offeredQuests = newOfferedQuests != null ? new List<MMOQuestDefinition>(newOfferedQuests) : new List<MMOQuestDefinition>();
            EnsureIdentity();
            RefreshMarker();
        }

        private void EnsureIdentity()
        {
            if (standardIdentity == null)
            {
                standardIdentity = GetComponent<MMOStandardNpcIdentity>();
                if (standardIdentity == null)
                {
                    standardIdentity = gameObject.AddComponent<MMOStandardNpcIdentity>();
                }
            }

            standardIdentity.Configure(standardIdentity.Profile, DisplayName, MMONpcIdentityRole.QuestGiver, false);
            identity = standardIdentity.Identity;
        }

        private void Interact(Vector2 screenPosition)
        {
            MMOQuestLog questLog = ResolvePlayerQuestLog();
            if (questLog == null)
            {
                return;
            }

            questLog.RecordSpeakToNpc(NpcId);
            MMOQuestDialogPresenter.Open(this, questLog, screenPosition);
        }

        private bool IsPointerOverThisNpc(Vector2 pointerPosition)
        {
            Camera camera = MMORuntimeSceneReferences.MainCamera;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f, interactionMask, QueryTriggerInteraction.Collide)
                || hit.collider == null
                || hit.collider.GetComponentInParent<MMOQuestNpc>() != this)
            {
                return false;
            }

            Transform playerTransform = MMORuntimeSceneReferences.PlayerTransform;
            Vector3 interactorPosition = playerTransform != null ? playerTransform.position : camera.transform.position;
            float sqrInteractionDistance = interactionDistance * interactionDistance;
            return (interactorPosition - transform.position).sqrMagnitude <= sqrInteractionDistance;
        }

        private void SubscribeToPlayerQuestLog()
        {
            MMOQuestLog questLog = ResolvePlayerQuestLog();
            if (questLog == observedQuestLog)
            {
                return;
            }

            if (observedQuestLog != null)
            {
                observedQuestLog.Changed -= OnQuestLogChanged;
            }

            observedQuestLog = questLog;
            if (observedQuestLog != null)
            {
                observedQuestLog.Changed += OnQuestLogChanged;
            }
        }

        private void OnQuestLogChanged(MMOQuestLog questLog)
        {
            RefreshMarker();
        }

        private MMOQuestLog ResolvePlayerQuestLog()
        {
            if (observedQuestLog != null)
            {
                return observedQuestLog;
            }

            return MMORuntimeSceneReferences.TryGetPlayerComponent(out MMOQuestLog questLog) ? questLog : null;
        }

        private void EnsureMarker()
        {
            if (questMarker != null)
            {
                return;
            }

            Transform existing = transform.Find("Quest Marker");
            GameObject markerObject = existing != null ? existing.gameObject : new GameObject("Quest Marker");
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            questMarker = markerObject.GetComponent<TextMesh>();
            if (questMarker == null)
            {
                questMarker = markerObject.AddComponent<TextMesh>();
            }

            questMarker.anchor = TextAnchor.MiddleCenter;
            questMarker.alignment = TextAlignment.Center;
            questMarker.fontSize = 64;
            questMarker.characterSize = 0.1f;
        }

        private void RefreshMarker()
        {
            EnsureMarker();
            MMOQuestLog questLog = observedQuestLog != null ? observedQuestLog : ResolvePlayerQuestLog();
            bool ready = questLog != null && questLog.HasReadyTurnInForNpc(NpcId);
            bool available = questLog != null && HasAvailableQuest(questLog);
            string markerText = ready ? "?" : available ? "!" : string.Empty;
            bool markerActive = !string.IsNullOrEmpty(markerText);
            if (markerText != currentMarkerText
                || questMarker.text != markerText
                || questMarker.gameObject.activeSelf != markerActive)
            {
                currentMarkerText = markerText;
                questMarker.text = markerText;
                questMarker.gameObject.SetActive(markerActive);
            }

            questMarker.color = ready ? new Color(1f, 0.86f, 0.18f, 1f) : new Color(1f, 0.78f, 0.12f, 1f);
            FaceMarkerToCamera();
        }

        private void FaceMarkerToCamera()
        {
            if (questMarker == null || !questMarker.gameObject.activeSelf)
            {
                return;
            }

            Camera camera = MMORuntimeSceneReferences.MainCamera;
            if (camera != null)
            {
                questMarker.transform.rotation = Quaternion.LookRotation(questMarker.transform.position - camera.transform.position);
            }
        }

        private bool HasAvailableQuest(MMOQuestLog questLog)
        {
            foreach (MMOQuestDefinition quest in offeredQuests)
            {
                if (quest != null && questLog.CanAccept(quest))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
