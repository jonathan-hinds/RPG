using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using RPGClone.Quests;
using UnityEngine;

namespace RPGClone.Services
{
    public readonly struct MMOInteractionContext
    {
        public MMOInteractionContext(
            GameObject actorObject,
            Camera camera,
            MMOCharacterIdentity identity,
            MMOQuestLog questLog,
            MMOInventoryContainer inventory,
            MMOCurrencyWallet wallet,
            MMOInteractionCastController interactionCaster,
            MMOAbilitySystem abilitySystem,
            MMOInputReader inputReader)
        {
            ActorObject = actorObject;
            Camera = camera;
            Identity = identity;
            QuestLog = questLog;
            Inventory = inventory;
            Wallet = wallet;
            InteractionCaster = interactionCaster;
            AbilitySystem = abilitySystem;
            InputReader = inputReader;
        }

        public GameObject ActorObject { get; }
        public Transform ActorTransform => ActorObject != null ? ActorObject.transform : null;
        public Camera Camera { get; }
        public MMOCharacterIdentity Identity { get; }
        public MMOQuestLog QuestLog { get; }
        public MMOInventoryContainer Inventory { get; }
        public MMOCurrencyWallet Wallet { get; }
        public MMOInteractionCastController InteractionCaster { get; }
        public MMOAbilitySystem AbilitySystem { get; }
        public MMOInputReader InputReader { get; }
        public bool IsValid => ActorObject != null;

        public static bool TryCreateForLocalPlayer(out MMOInteractionContext context)
        {
            MMOLocalPlayerContext localPlayer = MMOGameplaySessionService.LocalPlayer;
            GameObject actorObject = localPlayer.PlayerObject;
            if (actorObject == null)
            {
                context = default;
                return false;
            }

            actorObject.TryGetComponent(out MMOCharacterIdentity identity);
            actorObject.TryGetComponent(out MMOQuestLog questLog);
            actorObject.TryGetComponent(out MMOInventoryContainer inventory);
            actorObject.TryGetComponent(out MMOCurrencyWallet wallet);
            actorObject.TryGetComponent(out MMOInteractionCastController interactionCaster);
            actorObject.TryGetComponent(out MMOAbilitySystem abilitySystem);
            actorObject.TryGetComponent(out MMOInputReader inputReader);
            context = new MMOInteractionContext(
                actorObject,
                localPlayer.MainCamera,
                identity,
                questLog,
                inventory,
                wallet,
                interactionCaster,
                abilitySystem,
                inputReader);
            return true;
        }
    }
}
