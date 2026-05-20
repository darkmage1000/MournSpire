using System.Collections.Generic;
using UnityEngine;

namespace MournSpire.World
{
    /// <summary>
    /// Holds the raw ASCII map strings and parses them into tile data.
    /// Both the Dungeon and Overworld maps live here.
    /// Legend:
    ///   # = Wall      . = Floor     S = Player Start
    ///   G = Goblin    O = Orc       K = Skeleton    B = Boss
    ///   T = Tree      R = Rock      I = Iron Ore    M = Merchant   P = Teleporter
    /// </summary>
    [CreateAssetMenu(fileName = "MapData", menuName = "MournSpire/Map Data")]
    public class MapData : ScriptableObject
    {
        public enum MapType { Dungeon, Overworld }

        [Header("Map Type")]
        public MapType mapType = MapType.Dungeon;

        [Header("ASCII Map (one string per row)")]
        [TextArea(10, 40)]
        public string rawMap;

        // ── Parsed results ──────────────────────────────────────────────────

        [System.NonSerialized] public int Cols;
        [System.NonSerialized] public int Rows;
        [System.NonSerialized] public TileType[,] Tiles;

        [System.NonSerialized] public Vector2Int PlayerStart;
        [System.NonSerialized] public List<SpawnEntry>   EnemySpawns  = new();
        [System.NonSerialized] public Vector2Int?        BossSpawn;
        [System.NonSerialized] public Vector2Int?        TeleporterPos;
        [System.NonSerialized] public List<ResourceEntry> ResourceNodes = new();
        [System.NonSerialized] public List<Vector2Int>   MerchantSpots = new();

        // ── Parse ───────────────────────────────────────────────────────────

        public void Parse()
        {
            EnemySpawns   = new List<SpawnEntry>();
            ResourceNodes = new List<ResourceEntry>();
            MerchantSpots = new List<Vector2Int>();
            BossSpawn     = null;
            TeleporterPos = null;

            string[] lines = rawMap.Split('\n');
            Rows = lines.Length;
            Cols = 0;
            foreach (var l in lines) Cols = Mathf.Max(Cols, l.TrimEnd().Length);

            Tiles = new TileType[Cols, Rows];

            for (int r = 0; r < Rows; r++)
            {
                string row = lines[r].TrimEnd();
                for (int c = 0; c < Cols; c++)
                {
                    char ch = c < row.Length ? row[c] : '#';
                    var tile = TileType.Floor;

                    switch (ch)
                    {
                        case '#': tile = TileType.Wall; break;
                        case 'S':
                            tile = TileType.Floor;
                            PlayerStart = new Vector2Int(c, r);
                            break;
                        case 'G':
                            tile = TileType.Floor;
                            EnemySpawns.Add(new SpawnEntry { EnemyType = "goblin",   Tile = new Vector2Int(c, r) });
                            break;
                        case 'O':
                            tile = TileType.Floor;
                            EnemySpawns.Add(new SpawnEntry { EnemyType = "orc",      Tile = new Vector2Int(c, r) });
                            break;
                        case 'K':
                            tile = TileType.Floor;
                            EnemySpawns.Add(new SpawnEntry { EnemyType = "skeleton", Tile = new Vector2Int(c, r) });
                            break;
                        case 'B':
                            tile = TileType.Floor;
                            BossSpawn = new Vector2Int(c, r);
                            break;
                        case 'T':
                            tile = TileType.Floor;
                            ResourceNodes.Add(new ResourceEntry { ResourceType = "tree", Tile = new Vector2Int(c, r) });
                            break;
                        case 'R':
                            tile = TileType.Floor;
                            ResourceNodes.Add(new ResourceEntry { ResourceType = "rock", Tile = new Vector2Int(c, r) });
                            break;
                        case 'I':
                            tile = TileType.Floor;
                            ResourceNodes.Add(new ResourceEntry { ResourceType = "iron", Tile = new Vector2Int(c, r) });
                            break;
                        case 'M':
                            tile = TileType.Floor;
                            MerchantSpots.Add(new Vector2Int(c, r));
                            break;
                        case 'P':
                            tile = TileType.Floor;
                            TeleporterPos = new Vector2Int(c, r);
                            break;
                        default:
                            tile = TileType.Floor;
                            break;
                    }

                    Tiles[c, r] = tile;
                }
            }
        }

        public bool IsWalkable(int tx, int tz)
        {
            if (tx < 0 || tz < 0 || tx >= Cols || tz >= Rows) return false;
            return Tiles[tx, tz] != TileType.Wall;
        }

        public Vector3 TileToWorld(int tx, int tz, float tileSize = 2f)
            => new Vector3(tx * tileSize, 0f, tz * tileSize);
    }

    [System.Serializable]
    public struct SpawnEntry
    {
        public string      EnemyType;
        public Vector2Int  Tile;
    }

    [System.Serializable]
    public struct ResourceEntry
    {
        public string     ResourceType;
        public Vector2Int Tile;
    }
}
