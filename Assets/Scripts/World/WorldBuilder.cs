using System.Collections.Generic;
using UnityEngine;

namespace MournSpire.World
{
    /// <summary>
    /// Reads a MapData asset and instantiates tile GameObjects into the scene.
    /// Attach to an empty "World" GameObject in each scene.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        [Header("Map")]
        public MapData mapData;
        public float   tileSize = 2f;

        [Header("Tile Prefabs")]
        public GameObject floorPrefab;
        public GameObject wallPrefab;

        [Header("Feature Prefabs")]
        public GameObject teleporterPrefab;
        public GameObject treePrefab;
        public GameObject rockPrefab;
        public GameObject ironOrePrefab;
        public GameObject merchantPrefab;

        [Header("Torch")]
        public GameObject torchPrefab;

        // ── Runtime refs ────────────────────────────────────────────────────
        public MapData Data        { get; private set; }
        public int     Cols        => Data.Cols;
        public int     Rows        => Data.Rows;

        private readonly List<GameObject> _spawned = new();

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            Build();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void Build()
        {
            Clear();
            Data = mapData;
            Data.Parse();

            for (int r = 0; r < Data.Rows; r++)
            for (int c = 0; c < Data.Cols;  c++)
            {
                Vector3 pos = TileToWorld(c, r);

                if (Data.Tiles[c, r] == TileType.Wall)
                {
                    Spawn(wallPrefab,  pos);
                }
                else
                {
                    Spawn(floorPrefab, pos);
                }
            }

            // Feature objects
            if (Data.TeleporterPos.HasValue)
                Spawn(teleporterPrefab, TileToWorld(Data.TeleporterPos.Value));

            foreach (var node in Data.ResourceNodes)
            {
                GameObject prefab = node.ResourceType switch
                {
                    "tree" => treePrefab,
                    "rock" => rockPrefab,
                    "iron" => ironOrePrefab,
                    _      => rockPrefab,
                };
                Spawn(prefab, TileToWorld(node.Tile));
            }

            foreach (var spot in Data.MerchantSpots)
                Spawn(merchantPrefab, TileToWorld(spot));

            PlaceTorches();
        }

        public void Clear()
        {
            foreach (var go in _spawned)
                if (go) Destroy(go);
            _spawned.Clear();
        }

        public Vector3 TileToWorld(Vector2Int tile)
            => TileToWorld(tile.x, tile.y);

        public Vector3 TileToWorld(int tx, int tz)
            => new Vector3(tx * tileSize, 0f, tz * tileSize);

        public bool IsWalkable(int tx, int tz)
            => Data?.IsWalkable(tx, tz) ?? false;

        // ── Helpers ──────────────────────────────────────────────────────────

        private GameObject Spawn(GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.identity, transform);
            _spawned.Add(go);
            return go;
        }

        private void PlaceTorches()
        {
            if (torchPrefab == null) return;

            // Spread torches across floor tiles (every ~8 tiles)
            for (int r = 3; r < Data.Rows - 2; r += 8)
            for (int c = 3; c < Data.Cols - 2; c += 8)
            {
                if (Data.IsWalkable(c, r))
                    Spawn(torchPrefab, TileToWorld(c, r));
            }
        }
    }
}
