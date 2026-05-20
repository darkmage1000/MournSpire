using UnityEngine;

namespace MournSpire.Core
{
    public enum MerchantRole { ResourceBuyer, GearSeller, ConsumableSeller }

    /// <summary>
    /// Marker + data component on merchant stall prefabs.
    /// The actual shop UI is driven by HUDController.OpenShop().
    /// </summary>
    public class MerchantController : MonoBehaviour
    {
        public MerchantRole role = MerchantRole.ResourceBuyer;

        public string RoleTitle => role switch
        {
            MerchantRole.ResourceBuyer    => "🪵 Resource Buyer",
            MerchantRole.GearSeller       => "⚔ Gear Upgrades",
            MerchantRole.ConsumableSeller => "🧪 Consumables",
            _                             => "Merchant"
        };
    }
}
