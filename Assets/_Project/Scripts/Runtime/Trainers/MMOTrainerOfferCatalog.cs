using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Trainers
{
    [CreateAssetMenu(menuName = "RPG Clone/Trainers/Trainer Offer Catalog", fileName = "TrainerOfferCatalog")]
    public sealed class MMOTrainerOfferCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOTrainerOfferEntry> offers = new();

        public IReadOnlyList<MMOTrainerOfferEntry> Offers => offers;

        public void Configure(IEnumerable<MMOTrainerOfferEntry> newOffers)
        {
            offers = newOffers != null ? new List<MMOTrainerOfferEntry>(newOffers) : new List<MMOTrainerOfferEntry>();
        }

        public static void AppendOffersForClass(MMOPlayableClass trainerClass, List<MMOTrainerOfferEntry> target)
        {
            if (target == null)
            {
                return;
            }

            MMOTrainerOfferCatalog[] catalogs = Resources.LoadAll<MMOTrainerOfferCatalog>(string.Empty);
            foreach (MMOTrainerOfferCatalog catalog in catalogs)
            {
                if (catalog == null)
                {
                    continue;
                }

                foreach (MMOTrainerOfferEntry offer in catalog.Offers)
                {
                    if (offer == null || !offer.IsValid || offer.RequiredClass != trainerClass || ContainsAbility(target, offer.Ability))
                    {
                        continue;
                    }

                    target.Add(offer);
                }
            }
        }

        private static bool ContainsAbility(IEnumerable<MMOTrainerOfferEntry> offers, MMOAbilityDefinition ability)
        {
            foreach (MMOTrainerOfferEntry offer in offers)
            {
                if (offer != null && offer.Ability == ability)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
