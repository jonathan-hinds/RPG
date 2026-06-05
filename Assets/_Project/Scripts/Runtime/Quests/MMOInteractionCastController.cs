using System;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Quests
{
    [RequireComponent(typeof(MMOCombatant))]
    public sealed class MMOInteractionCastController : MonoBehaviour
    {
        [SerializeField] private bool interruptOnMovement = true;
        [SerializeField] private bool interruptOnCombat = true;
        [SerializeField, Min(0.0001f)] private float movementInterruptDistance = 0.02f;

        private MMOCombatant combatant;
        private ActiveInteractionCast activeCast;

        public event Action<MMOInteractionCastController, string, float> CastStarted;
        public event Action<MMOInteractionCastController, string, float> CastProgressed;
        public event Action<MMOInteractionCastController, string, string> CastInterrupted;
        public event Action<MMOInteractionCastController, string> CastCompleted;

        public bool IsCasting => activeCast != null;
        public string CurrentCastLabel => activeCast != null ? activeCast.Label : string.Empty;
        public float CurrentCastNormalized => activeCast == null ? 0f : Mathf.Clamp01((Time.time - activeCast.StartTime) / activeCast.Duration);

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (combatant != null)
            {
                combatant.CombatActivity -= OnCombatActivity;
                combatant.CombatActivity += OnCombatActivity;
            }
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.CombatActivity -= OnCombatActivity;
            }
        }

        private void Update()
        {
            if (activeCast == null)
            {
                return;
            }

            if (interruptOnMovement && (transform.position - activeCast.StartPosition).sqrMagnitude > movementInterruptDistance * movementInterruptDistance)
            {
                Interrupt("Interaction interrupted.");
                return;
            }

            CastProgressed?.Invoke(this, activeCast.Label, CurrentCastNormalized);
            if (Time.time - activeCast.StartTime < activeCast.Duration)
            {
                return;
            }

            ActiveInteractionCast completedCast = activeCast;
            activeCast = null;
            completedCast.OnCompleted?.Invoke();
            CastCompleted?.Invoke(this, completedCast.Label);
        }

        public bool TryBeginCast(string label, float durationSeconds, Action onCompleted, out string failureReason)
        {
            failureReason = string.Empty;
            if (activeCast != null)
            {
                failureReason = "Another action is in progress.";
                return false;
            }

            if (durationSeconds <= 0f)
            {
                onCompleted?.Invoke();
                return true;
            }

            activeCast = new ActiveInteractionCast(label, transform.position, Time.time, durationSeconds, onCompleted);
            CastStarted?.Invoke(this, activeCast.Label, activeCast.Duration);
            return true;
        }

        public void Interrupt(string reason)
        {
            if (activeCast == null)
            {
                return;
            }

            string label = activeCast.Label;
            activeCast = null;
            CastInterrupted?.Invoke(this, label, reason);
        }

        private void OnCombatActivity(MMOCombatant activeCombatant)
        {
            if (interruptOnCombat)
            {
                Interrupt("Interrupted by combat.");
            }
        }

        private void EnsureReferences()
        {
            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }
        }

        private sealed class ActiveInteractionCast
        {
            public readonly string Label;
            public readonly Vector3 StartPosition;
            public readonly float StartTime;
            public readonly float Duration;
            public readonly Action OnCompleted;

            public ActiveInteractionCast(string label, Vector3 startPosition, float startTime, float duration, Action onCompleted)
            {
                Label = string.IsNullOrWhiteSpace(label) ? "Interacting" : label;
                StartPosition = startPosition;
                StartTime = startTime;
                Duration = Mathf.Max(0.01f, duration);
                OnCompleted = onCompleted;
            }
        }
    }
}
