using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Targeting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Combat
{
    [RequireComponent(typeof(MMOAbilitySystem))]
    [RequireComponent(typeof(MMOCombatant))]
    public sealed class MMOAutoAttackController : MonoBehaviour
    {
        [SerializeField] private MMOAbilityDefinition autoAttackAbility;
        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private bool handleRightClickInput = true;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField, Min(1f)] private float maxInteractionDistance = 250f;
        [SerializeField] private bool selectRightClickedTarget = true;
        [SerializeField] private bool ignorePointerOverUi = true;
        [SerializeField] private bool faceTargetWhileAttacking = true;
        [SerializeField, Min(0f)] private float facingDegreesPerSecond = 720f;
        [SerializeField, Min(0.1f)] private float failedAttemptRetrySeconds = 0.5f;

        private MMOAbilitySystem abilitySystem;
        private MMOCombatant combatant;
        private MMOCharacterIdentity currentTarget;
        private MMOCharacterIdentity lastSwingTarget;
        private float nextSwingTime;

        public MMOAbilityDefinition AutoAttackAbility => autoAttackAbility;
        public MMOCharacterIdentity CurrentTarget => currentTarget;
        public bool IsAutoAttacking => currentTarget != null;

        private void Awake()
        {
            EnsureInitialized();
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }
        }

        private void Update()
        {
            HandleRightClickAttack();
            UpdateAutoAttack();
        }

        public void SetAutoAttackAbility(MMOAbilityDefinition ability)
        {
            autoAttackAbility = ability;
            if (abilitySystem != null)
            {
                abilitySystem.LearnAbility(ability);
            }
        }

        public void SetTargetSelectionController(MMOTargetSelectionController controller)
        {
            targetSelectionController = controller;
        }

        public void SetInteractionCamera(Camera camera)
        {
            interactionCamera = camera;
        }

        public void SetHandleRightClickInput(bool value)
        {
            handleRightClickInput = value;
        }

        public void ToggleAutoAttack(MMOCharacterIdentity target)
        {
            if (IsAutoAttacking && currentTarget == target)
            {
                StopAutoAttack();
                return;
            }

            StartAutoAttack(target);
        }

        public bool StartAutoAttack(MMOCharacterIdentity target)
        {
            EnsureInitialized();
            if (abilitySystem == null || combatant == null || target == null || autoAttackAbility == null)
            {
                abilitySystem?.TryUseAbility(autoAttackAbility, target, out _);
                return false;
            }

            if (!abilitySystem.KnowsAbility(autoAttackAbility))
            {
                abilitySystem.LearnAbility(autoAttackAbility);
            }

            MMOCombatant targetCombatant = target.GetComponent<MMOCombatant>();
            if (targetCombatant == null || !targetCombatant.IsAlive || !MMOFactionRules.CanDamage(combatant.Identity, target))
            {
                abilitySystem.TryUseAbility(autoAttackAbility, target, out _);
                return false;
            }

            currentTarget = target;
            if (lastSwingTarget != target || nextSwingTime <= Time.time)
            {
                nextSwingTime = Time.time;
            }

            float effectiveRange = combatant.Identity.Stats != null ? combatant.Identity.Stats.MeleeRange : autoAttackAbility.Range;
            if (!abilitySystem.IsInRange(target, effectiveRange))
            {
                abilitySystem.TryUseAbility(autoAttackAbility, target, out _);
                nextSwingTime = Time.time + failedAttemptRetrySeconds;
            }

            return true;
        }

        public void StopAutoAttack()
        {
            lastSwingTarget = currentTarget;
            currentTarget = null;
        }

        public float GetAutoAttackCooldownRemaining()
        {
            return Mathf.Max(0f, nextSwingTime - Time.time);
        }

        public float GetAutoAttackCooldownNormalized()
        {
            float swingDelay = combatant != null && combatant.Identity != null && combatant.Identity.Stats != null
                ? combatant.Identity.Stats.MeleeAttackSpeed
                : 2f;
            return Mathf.Clamp01(GetAutoAttackCooldownRemaining() / Mathf.Max(0.1f, swingDelay));
        }

        private void HandleRightClickAttack()
        {
            if (!handleRightClickInput)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
            {
                return;
            }

            if (ignorePointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            MMOCharacterIdentity target = RaycastCharacter(mouse.position.ReadValue());
            if (target == null || target == combatant.Identity)
            {
                return;
            }

            if (selectRightClickedTarget && targetSelectionController != null)
            {
                targetSelectionController.SelectTarget(target);
            }

            StartAutoAttack(target);
        }

        private void UpdateAutoAttack()
        {
            EnsureInitialized();
            if (currentTarget == null || autoAttackAbility == null)
            {
                return;
            }

            MMOCombatant targetCombatant = currentTarget.GetComponent<MMOCombatant>();
            if (combatant == null || !combatant.IsAlive || targetCombatant == null || !targetCombatant.IsAlive)
            {
                StopAutoAttack();
                return;
            }

            FaceCurrentTarget();

            if (Time.time < nextSwingTime)
            {
                return;
            }

            if (!abilitySystem.TryUseAbility(autoAttackAbility, currentTarget, out _))
            {
                nextSwingTime = Time.time + failedAttemptRetrySeconds;
                return;
            }

            float swingDelay = combatant.Identity.Stats != null ? combatant.Identity.Stats.MeleeAttackSpeed : 2f;
            nextSwingTime = Time.time + Mathf.Max(0.1f, swingDelay);
            lastSwingTarget = currentTarget;
        }

        private void FaceCurrentTarget()
        {
            if (!faceTargetWhileAttacking || currentTarget == null)
            {
                return;
            }

            Vector3 direction = currentTarget.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                facingDegreesPerSecond * Time.deltaTime);
        }

        private MMOCharacterIdentity RaycastCharacter(Vector2 pointerPosition)
        {
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }

            if (interactionCamera == null)
            {
                return null;
            }

            Ray ray = interactionCamera.ScreenPointToRay(pointerPosition);
            return Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance, interactionMask, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<MMOCharacterIdentity>()
                : null;
        }

        private void EnsureInitialized()
        {
            if (abilitySystem == null)
            {
                abilitySystem = GetComponent<MMOAbilitySystem>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }
        }
    }
}
