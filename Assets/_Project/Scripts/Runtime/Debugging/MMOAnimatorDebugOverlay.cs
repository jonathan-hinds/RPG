using System.Text;
using System.Reflection;
using RPGClone.Combat;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Debugging
{
    [DisallowMultipleComponent]
    public sealed class MMOAnimatorDebugOverlay : MonoBehaviour
    {
        private const int BaseLayer = 0;
        private const int WindowId = 384022;
        private static readonly int MoveSpeedHash = Animator.StringToHash(MMOPlayerLocomotionAnimationSet.MoveSpeedParameter);
        private static readonly int InCombatHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.InCombatParameter);
        private static readonly MethodInfo ForceLeaveCombatMethod = typeof(MMOCombatant).GetMethod(
            "ForceLeaveCombat",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] private bool visible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [SerializeField] private Vector2 windowPosition = new(16f, 16f);

        private readonly StringBuilder builder = new(1024);
        private Animator animator;
        private MMOCombatant combatant;
        private MMOPlayerMotor motor;
        private MMOPlayerCombatAnimator combatAnimator;
        private Rect windowRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnPlayer()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.GetComponent<MMOAnimatorDebugOverlay>() == null)
            {
                player.AddComponent<MMOAnimatorDebugOverlay>();
            }
#endif
        }

        private void Awake()
        {
            ResolveReferences();
            windowRect = new Rect(windowPosition.x, windowPosition.y, 460f, 360f);
        }

        private void Update()
        {
            ResolveReferences();
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == toggleKey)
            {
                visible = !visible;
                current.Use();
            }

            if (!visible)
            {
                return;
            }

            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "Animator Debug");
        }

        private void DrawWindow(int windowId)
        {
            GUI.contentColor = Color.white;
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force In Combat"))
                {
                    ForceInCombat();
                }

                if (GUILayout.Button("Force Out Of Combat"))
                {
                    ForceOutOfCombat();
                }
            }

            GUILayout.TextArea(BuildDebugText(), GUILayout.ExpandHeight(true));
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        public void ForceInCombat()
        {
            combatant?.RegisterCombatActivity();
        }

        public void ForceOutOfCombat()
        {
            if (combatant == null)
            {
                return;
            }

            ForceLeaveCombatMethod?.Invoke(combatant, null);
        }

        private string BuildDebugText()
        {
            builder.Clear();
            builder.AppendLine($"Toggle: {toggleKey}");

            if (animator == null || animator.runtimeAnimatorController == null || !animator.isInitialized)
            {
                builder.AppendLine("Animator: <missing or not initialized>");
                return builder.ToString();
            }

            bool inCombat = combatant != null && combatant.IsInCombat;
            float planarSpeed = motor != null ? motor.CurrentPlanarSpeed : -1f;
            builder.AppendLine($"Combatant.IsInCombat: {inCombat}");
            builder.AppendLine($"Motor planar speed: {planarSpeed:0.000}");
            builder.AppendLine($"Animator controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<none>")}");
            builder.AppendLine($"Animator enabled/speed: {animator.enabled} / {animator.speed:0.00}");
            builder.AppendLine($"Param InCombat: {ReadBoolParameter(InCombatHash)}");
            builder.AppendLine($"Param MoveSpeed: {ReadFloatParameter(MoveSpeedHash):0.000}");
            builder.AppendLine($"Expected idle: {(inCombat ? "CombatIdle" : "Locomotion/Idle")}");
            builder.AppendLine($"Combat driver: {(combatAnimator != null && combatAnimator.enabled ? "enabled" : "missing/disabled")}");
            if (combatAnimator != null)
            {
                builder.AppendLine($"Driver target: {combatAnimator.DebugLastRequestedState}");
                builder.AppendLine($"Driver ready/busy: {combatAnimator.DebugLastAnimatorReady} / {combatAnimator.DebugLastBaseLayerBusy}");
            }

            AppendLayerInfo(BaseLayer);

            return builder.ToString();
        }

        private void AppendLayerInfo(int layer)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
            builder.AppendLine();
            builder.AppendLine($"Layer {layer} weight: {animator.GetLayerWeight(layer):0.00}");
            builder.AppendLine($"Current: {ResolveKnownStateName(current)}");
            builder.AppendLine($"Current hash: {current.fullPathHash}");
            builder.AppendLine($"Current tag: {ResolveKnownTag(current)}");
            builder.AppendLine($"Current normalized: {current.normalizedTime:0.000}");
            builder.AppendLine($"In transition: {animator.IsInTransition(layer)}");

            if (animator.IsInTransition(layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                builder.AppendLine($"Next: {ResolveKnownStateName(next)}");
                builder.AppendLine($"Next hash: {next.fullPathHash}");
                builder.AppendLine($"Next normalized: {next.normalizedTime:0.000}");
            }

            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(layer);
            builder.AppendLine("Current clips:");
            if (clips.Length == 0)
            {
                builder.AppendLine("  <none>");
            }

            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i].clip;
                builder.AppendLine($"  {i}: {(clip != null ? clip.name : "<null>")} weight={clips[i].weight:0.000}");
            }

            if (!animator.IsInTransition(layer))
            {
                return;
            }

            AnimatorClipInfo[] nextClips = animator.GetNextAnimatorClipInfo(layer);
            builder.AppendLine("Next clips:");
            if (nextClips.Length == 0)
            {
                builder.AppendLine("  <none>");
            }

            for (int i = 0; i < nextClips.Length; i++)
            {
                AnimationClip clip = nextClips[i].clip;
                builder.AppendLine($"  {i}: {(clip != null ? clip.name : "<null>")} weight={nextClips[i].weight:0.000}");
            }
        }

        private bool ReadBoolParameter(int hash)
        {
            return HasParameter(hash, AnimatorControllerParameterType.Bool) && animator.GetBool(hash);
        }

        private float ReadFloatParameter(int hash)
        {
            return HasParameter(hash, AnimatorControllerParameterType.Float) ? animator.GetFloat(hash) : float.NaN;
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveKnownStateName(AnimatorStateInfo state)
        {
            if (state.IsName(MMOPlayerCombatAnimationSet.CombatIdleStatePath))
            {
                return MMOPlayerCombatAnimationSet.CombatIdleStatePath;
            }

            if (state.IsName(MMOPlayerCombatAnimationSet.LocomotionStatePath))
            {
                return MMOPlayerCombatAnimationSet.LocomotionStatePath;
            }

            if (state.IsName(MMOPlayerCombatAnimationSet.CastingStatePath))
            {
                return MMOPlayerCombatAnimationSet.CastingStatePath;
            }

            if (state.IsName(MMOPlayerCombatAnimationSet.CastStatePath))
            {
                return MMOPlayerCombatAnimationSet.CastStatePath;
            }

            if (state.IsName(MMOPlayerCombatAnimationSet.FullBodyDamageStatePath))
            {
                return MMOPlayerCombatAnimationSet.FullBodyDamageStatePath;
            }

            return "<unknown>";
        }

        private static string ResolveKnownTag(AnimatorStateInfo state)
        {
            if (state.IsTag("Idle")) return "Idle";
            if (state.IsTag("Jump")) return "Jump";
            if (state.IsTag("Attack")) return "Attack";
            if (state.IsTag("Cast")) return "Cast";
            if (state.IsTag("Damage")) return "Damage";
            if (state.IsTag("Death")) return "Death";
            return "<none>";
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }

            if (motor == null)
            {
                motor = GetComponent<MMOPlayerMotor>();
            }

            if (combatAnimator == null)
            {
                combatAnimator = GetComponent<MMOPlayerCombatAnimator>();
            }
        }
    }
}
