using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Quests
{
    [RequireComponent(typeof(Collider))]
    public sealed class MMOQuestNpc : MonoBehaviour
    {
        [SerializeField] private string npcId = "npc";
        [SerializeField] private string displayNameOverride;
        [SerializeField] private List<MMOQuestDefinition> offeredQuests = new();
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private TextMesh questMarker;
        private MMOQuestLog observedQuestLog;

        public string NpcId => string.IsNullOrWhiteSpace(npcId) ? name : npcId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride) ? name : displayNameOverride;
        public IReadOnlyList<MMOQuestDefinition> OfferedQuests => offeredQuests;

        private void Awake()
        {
            EnsureMarker();
            RefreshMarker();
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
        }

        private void Update()
        {
            SubscribeToPlayerQuestLog();
            RefreshMarker();

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
            RefreshMarker();
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
            Camera camera = Camera.main;
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

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 interactorPosition = player != null ? player.transform.position : camera.transform.position;
            return Vector3.Distance(interactorPosition, transform.position) <= interactionDistance;
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
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<MMOQuestLog>() : null;
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
            questMarker.text = ready ? "?" : available ? "!" : string.Empty;
            questMarker.color = ready ? new Color(1f, 0.86f, 0.18f, 1f) : new Color(1f, 0.78f, 0.12f, 1f);
            questMarker.gameObject.SetActive(!string.IsNullOrEmpty(questMarker.text));

            Camera camera = Camera.main;
            if (camera != null && questMarker.gameObject.activeSelf)
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
