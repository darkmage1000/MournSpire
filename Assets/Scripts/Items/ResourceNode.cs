using UnityEngine;

namespace MournSpire.Items
{
    /// <summary>
    /// Attach to a Tree / Rock / IronOre prefab.
    /// Player interacts via [E] — GameManager calls Interact().
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public string resourceType = "wood";  // "wood" | "stone" | "iron"
        public int    yieldMin     = 1;
        public int    yieldMax     = 3;
        public string interactLabel => $"[E] {LabelFor(resourceType)}";

        public bool Depleted { get; private set; }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Gather resources; returns amount gathered (0 if depleted).</summary>
        public int Interact(Inventory inventory)
        {
            if (Depleted) return 0;

            int amount = Random.Range(yieldMin, yieldMax + 1);
            inventory.AddResource(resourceType, amount);
            Depleted = true;
            SetDepletedVisual();
            return amount;
        }

        // ── Visual ─────────────────────────────────────────────────────────────

        void SetDepletedVisual()
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mat = r.material;
                mat.color = Color.gray;
            }
        }

        static string LabelFor(string t) => t switch
        {
            "wood"  => "Chop tree",
            "stone" => "Mine rock",
            "iron"  => "Mine ore",
            _       => "Gather"
        };
    }
}
