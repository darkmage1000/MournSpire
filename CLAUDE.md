# MournSpire — Unity 6 (URP) RPG

## Architecture at a Glance

| Layer | Key Classes | Notes |
|---|---|---|
| **World** | `MapData`, `WorldBuilder` | `MapData` is a ScriptableObject holding the ASCII map; `WorldBuilder` instantiates prefabs at runtime |
| **Player** | `PlayerController`, `PlayerStats` | Tile-based movement (2 units/tile); stamina drives attack + block |
| **Enemy** | `EnemyController`, `EnemyStats`, `BossController` | BFS pathfinding; skeleton shield; orc axe throw; boss shadow bolt |
| **Items** | `Inventory`, `ResourceNode` | Persistent across scenes via DontDestroyOnLoad |
| **Core** | `GameManager`, `MerchantController` | Wires everything; handles zone transitions via `SceneManager.LoadScene` |
| **UI** | `HUDController`, `ShopController` | All HUD refs assigned in Inspector; shop built dynamically |

## Tile System
- `tileSize = 2f` world units per tile
- `MapData.TileToWorld(tx, tz)` → `Vector3(tx*2, 0, tz*2)`
- `WorldBuilder.IsWalkable(tx, tz)` → checks `MapData.Tiles[tx,tz] != Wall`
- Player / enemy tile positions stored as `Vector2Int`; lerped to world-space each frame

## Zone Transitions
- Two scenes: **Overworld** and **Dungeon**
- `GameManager.currentZone` identifies which scene is active
- Transitioning: player steps on teleporter tile (`MapData.TeleporterPos`) → presses **[E]** → `SceneManager.LoadScene(targetScene)`
- `PlayerStats` and `Inventory` persist via `DontDestroyOnLoad` on the GameManager prefab

## Map Format (ASCII)
```
# = Wall        . = Floor      S = Player Start
G = Goblin      O = Orc        K = Skeleton    B = Boss
T = Tree        R = Rock       I = Iron Ore    M = Merchant   P = Teleporter
```
Edit the `rawMap` field on the `MapData` ScriptableObject assets in `Assets/ScriptableObjects/`.

## Enemy AI
- **Goblin**: chase + melee within range 9
- **Skeleton**: chase + 45% chance to raise shield while approaching; must lower shield to attack
- **Orc**: melee at range 1; throws axe at range 3–8 (42% chance)
- **Boss (Lich King)**: triggered by proximity; phase 2 at 50% HP; power strike every 3rd melee; shadow bolt at range 3–9

## Controls
| Key | Action |
|---|---|
| WASD / Arrows | Move |
| Space | Attack (30 SP) |
| Q (hold) | Block |
| E | Interact / Gather / Enter dungeon |
| F | Use health potion |
| B | Open/close backpack |
| R | Restart after death |

## Key Design Decisions
- **No Rigidbody movement** — tile grid + lerp keeps combat legible and matches the browser prototype exactly
- **EnemyStats as ScriptableObject** (`EnemyStatsDef`) — swap enemy values without touching code
- **BFS max depth 12** for standard enemies, 20 for boss — prevents pathfinding freezes on large maps
- **Projectile as self-moving MonoBehaviour** — no physics layer needed; hit check is distance-based
- **Shop UI built dynamically** by `ShopController.Rebuild()` each time it opens — avoids prefab bloat for shop rows
