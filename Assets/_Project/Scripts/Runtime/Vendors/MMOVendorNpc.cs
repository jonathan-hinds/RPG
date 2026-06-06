using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.UI;
using RPGClone.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Vendors
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOStandardNpcIdentity))]
    public sealed class MMOVendorNpc : MonoBehaviour
    {
        [SerializeField] private string vendorId = "vendor";
        [SerializeField] private string displayNameOverride;
        [SerializeField] private string title = "General Goods Merchant";
        [SerializeField] private List<MMOVendorStockEntry> stock = new();
        [SerializeField] private bool buysTrash = true;
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private bool snapToGroundOnStart = true;

        private MMOCharacterIdentity identity;
        private MMOStandardNpcIdentity standardIdentity;

        public string VendorId => string.IsNullOrWhiteSpace(vendorId) ? name : vendorId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride) ? name : displayNameOverride;
        public string Title => string.IsNullOrWhiteSpace(title) ? "General Goods Merchant" : title;
        public IReadOnlyList<MMOVendorStockEntry> Stock => stock;
        public bool BuysTrash => buysTrash;
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
            if (IsPointerOverThisVendor(pointerPosition))
            {
                Interact(pointerPosition);
            }
        }

        public void Configure(string newVendorId, string newDisplayName, IEnumerable<MMOVendorStockEntry> newStock, bool newBuysTrash)
        {
            Configure(newVendorId, newDisplayName, "General Goods Merchant", newStock, newBuysTrash);
        }

        public void Configure(string newVendorId, string newDisplayName, string newTitle, IEnumerable<MMOVendorStockEntry> newStock, bool newBuysTrash)
        {
            vendorId = newVendorId;
            displayNameOverride = newDisplayName;
            title = string.IsNullOrWhiteSpace(newTitle) ? "General Goods Merchant" : newTitle;
            stock = newStock != null ? new List<MMOVendorStockEntry>(newStock) : new List<MMOVendorStockEntry>();
            buysTrash = newBuysTrash;
            EnsureIdentity();
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

            standardIdentity.Configure(standardIdentity.Profile, DisplayName, Title, MMONpcIdentityRole.Vendor, false);
            identity = standardIdentity.Identity;
        }

        public bool TryBuy(MMOVendorStockEntry entry, MMOInventoryContainer inventory, MMOCurrencyWallet wallet)
        {
            if (entry == null || !entry.IsValid || inventory == null || wallet == null)
            {
                return false;
            }

            if (wallet.Copper < entry.PriceCopper || !inventory.CanAddItem(entry.Item, entry.Quantity))
            {
                return false;
            }

            if (!wallet.TrySpendCopper(entry.PriceCopper))
            {
                return false;
            }

            bool added = inventory.TryAddItem(entry.Item, entry.Quantity, out int remaining) && remaining <= 0;
            if (!added)
            {
                wallet.AddCopper(entry.PriceCopper);
            }

            return added;
        }

        public int SellTrash(MMOInventoryContainer inventory, MMOCurrencyWallet wallet)
        {
            if (!buysTrash || inventory == null || wallet == null)
            {
                return 0;
            }

            int earnedCopper = 0;
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                MMOItemStack stack = inventory.Slots[i];
                if (stack == null || stack.IsEmpty || stack.Item.ItemType != MMOItemType.Trash || stack.Item.VendorValueCopper <= 0)
                {
                    continue;
                }

                earnedCopper += stack.Item.VendorValueCopper * stack.Quantity;
                inventory.SetSlot(i, null, 0);
            }

            if (earnedCopper > 0)
            {
                wallet.AddCopper(earnedCopper);
            }

            return earnedCopper;
        }

        public bool TrySellInventorySlot(MMOInventoryContainer inventory, MMOCurrencyWallet wallet, int slotIndex, out int earnedCopper, out string itemName)
        {
            earnedCopper = 0;
            itemName = string.Empty;
            if (inventory == null || wallet == null)
            {
                return false;
            }

            MMOItemStack stack = inventory.GetSlot(slotIndex);
            if (stack == null || stack.IsEmpty || stack.Item.VendorValueCopper <= 0)
            {
                return false;
            }

            earnedCopper = stack.Item.VendorValueCopper * stack.Quantity;
            itemName = stack.Item.DisplayName;
            inventory.SetSlot(slotIndex, null, 0);
            wallet.AddCopper(earnedCopper);
            return true;
        }

        private void Interact(Vector2 screenPosition)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return;
            }

            MMOVendorPresenter.Open(
                this,
                player.GetComponent<MMOInventoryContainer>(),
                player.GetComponent<MMOCurrencyWallet>(),
                screenPosition);
        }

        private bool IsPointerOverThisVendor(Vector2 pointerPosition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f, interactionMask, QueryTriggerInteraction.Collide)
                || hit.collider == null
                || hit.collider.GetComponentInParent<MMOVendorNpc>() != this)
            {
                return false;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 interactorPosition = player != null ? player.transform.position : camera.transform.position;
            return Vector3.Distance(interactorPosition, transform.position) <= interactionDistance;
        }
    }
}
