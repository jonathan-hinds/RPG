using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Quests;
using RPGClone.Services;
using RPGClone.UI;
using RPGClone.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Trainers
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOStandardNpcIdentity))]
    public sealed class MMOClassTrainerNpc : MonoBehaviour
    {
        [SerializeField] private string trainerId = "trainer";
        [SerializeField] private string displayNameOverride;
        [SerializeField] private string titleOverride;
        [SerializeField] private MMOPlayableClass trainerClass = MMOPlayableClass.Warrior;
        [SerializeField] private List<MMOTrainerOfferEntry> offers = new();
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private bool snapToGroundOnStart = true;

        private readonly List<MMOTrainerOfferEntry> resolvedOffers = new();
        private MMOStandardNpcIdentity standardIdentity;
        private bool offersDirty = true;

        public string TrainerId => string.IsNullOrWhiteSpace(trainerId) ? name : trainerId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride) ? name : displayNameOverride;
        public string Title => string.IsNullOrWhiteSpace(titleOverride) ? $"{trainerClass} Trainer" : titleOverride;
        public MMOPlayableClass TrainerClass => trainerClass;
        public IReadOnlyList<MMOTrainerOfferEntry> Offers
        {
            get
            {
                RebuildOffersIfNeeded();
                return resolvedOffers;
            }
        }
        public float InteractionDistance => interactionDistance;

        private void Awake()
        {
            EnsureIdentity();
        }

        private void Start()
        {
            if (snapToGroundOnStart)
            {
                MMOGroundingUtility.SnapTransformToGround(transform, GetComponent<Collider>());
            }
        }

        private void Update()
        {
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
            if (IsPointerOverThisTrainer(pointerPosition))
            {
                Interact(pointerPosition);
            }
        }

        public void Configure(string newTrainerId, string newDisplayName, MMOPlayableClass newTrainerClass, IEnumerable<MMOTrainerOfferEntry> newOffers)
        {
            Configure(newTrainerId, newDisplayName, $"{newTrainerClass} Trainer", newTrainerClass, newOffers);
        }

        public void Configure(string newTrainerId, string newDisplayName, string newTitle, MMOPlayableClass newTrainerClass, IEnumerable<MMOTrainerOfferEntry> newOffers)
        {
            trainerId = newTrainerId;
            displayNameOverride = newDisplayName;
            titleOverride = string.IsNullOrWhiteSpace(newTitle) ? $"{newTrainerClass} Trainer" : newTitle;
            trainerClass = newTrainerClass;
            offers = newOffers != null ? new List<MMOTrainerOfferEntry>(newOffers) : new List<MMOTrainerOfferEntry>();
            offersDirty = true;
            EnsureIdentity();
        }

        public bool TryTrain(MMOTrainerOfferEntry offer, GameObject player, out string result)
        {
            if (!CanTrain(offer, player, out result))
            {
                return false;
            }

            MMOAbilitySystem abilitySystem = player.GetComponent<MMOAbilitySystem>();
            MMOCurrencyWallet wallet = player.GetComponent<MMOCurrencyWallet>();
            if (!wallet.TrySpendCopper(offer.PriceCopper))
            {
                result = "You do not have enough money.";
                return false;
            }

            abilitySystem.LearnAbility(offer.Ability);
            FindAnyObjectByType<MMOActionBarPresenter>()?.FillEmptySlotsFromKnownAbilities();
            result = $"You have learned {offer.Ability.DisplayName}.";
            return true;
        }

        public bool CanTrain(MMOTrainerOfferEntry offer, GameObject player, out string result)
        {
            result = string.Empty;
            if (offer == null || !offer.IsValid || player == null)
            {
                result = "That lesson is unavailable.";
                return false;
            }

            MMOCharacterIdentity identity = player.GetComponent<MMOCharacterIdentity>();
            MMOAbilitySystem abilitySystem = player.GetComponent<MMOAbilitySystem>();
            MMOCurrencyWallet wallet = player.GetComponent<MMOCurrencyWallet>();
            if (identity == null || abilitySystem == null || wallet == null)
            {
                result = "You are not ready for training.";
                return false;
            }

            if (!TryResolvePlayerClass(player, abilitySystem, out MMOPlayableClass playerClass))
            {
                result = "Your class could not be determined.";
                return false;
            }

            if (playerClass != offer.RequiredClass)
            {
                result = $"Only {offer.RequiredClass}s can learn that.";
                return false;
            }

            if (identity.Level < offer.RequiredLevel)
            {
                result = $"Requires level {offer.RequiredLevel}.";
                return false;
            }

            if (abilitySystem.KnowsAbility(offer.Ability))
            {
                result = "You already know that ability.";
                return false;
            }

            if (wallet.Copper < offer.PriceCopper)
            {
                result = "You do not have enough money.";
                return false;
            }

            result = "Available.";
            return true;
        }

        private static bool TryResolvePlayerClass(GameObject player, MMOAbilitySystem abilitySystem, out MMOPlayableClass playerClass)
        {
            MMOCharacterCustomization customization = player.GetComponent<MMOCharacterCustomization>();
            if (customization != null)
            {
                playerClass = customization.CharacterClass;
                return true;
            }

            foreach (MMOAbilityDefinition ability in abilitySystem.KnownAbilities)
            {
                string abilityId = ability != null ? ability.AbilityId : string.Empty;
                if (abilityId == "mage_fireball" || abilityId == "mage_mage_armor" || abilityId == "mage_fire_blast" || abilityId == "mage_flamestrike")
                {
                    playerClass = MMOPlayableClass.Mage;
                    return true;
                }

                if (abilityId == "shaman_healing_beam" || abilityId == "shaman_water_shield" || abilityId == "shaman_lightning_bolt" || abilityId == "shaman_frost_shock")
                {
                    playerClass = MMOPlayableClass.Shaman;
                    return true;
                }

                if (abilityId == "warrior_bash" || abilityId == "warrior_berzerkitis" || abilityId == "warrior_charge" || abilityId == "warrior_thunderclap")
                {
                    playerClass = MMOPlayableClass.Warrior;
                    return true;
                }
            }

            playerClass = default;
            return false;
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

            standardIdentity.Configure(standardIdentity.Profile, DisplayName, Title, MMONpcIdentityRole.Trainer, false);
        }

        private void RebuildOffersIfNeeded()
        {
            if (!offersDirty)
            {
                return;
            }

            resolvedOffers.Clear();
            foreach (MMOTrainerOfferEntry offer in offers)
            {
                if (offer != null && offer.IsValid)
                {
                    resolvedOffers.Add(offer);
                }
            }

            MMOTrainerOfferCatalog.AppendOffersForClass(trainerClass, resolvedOffers);
            offersDirty = false;
        }

        private void Interact(Vector2 screenPosition)
        {
            GameObject player = MMORuntimeSceneReferences.PlayerObject;
            if (player == null)
            {
                return;
            }

            MMOClassTrainerPresenter.Open(this, player, screenPosition);
        }

        private bool IsPointerOverThisTrainer(Vector2 pointerPosition)
        {
            Camera camera = MMORuntimeSceneReferences.MainCamera;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f, interactionMask, QueryTriggerInteraction.Collide)
                || hit.collider == null
                || hit.collider.GetComponentInParent<MMOClassTrainerNpc>() != this)
            {
                return false;
            }

            Transform playerTransform = MMORuntimeSceneReferences.PlayerTransform;
            Vector3 interactorPosition = playerTransform != null ? playerTransform.position : camera.transform.position;
            float sqrInteractionDistance = interactionDistance * interactionDistance;
            return (interactorPosition - transform.position).sqrMagnitude <= sqrInteractionDistance;
        }
    }
}
