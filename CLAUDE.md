# MournSpire — Unity 6 (URP) RPG

## ⏳ Backlog (deferred — pick up next session)
- **Mace stun** — brief enemy stun/slow on mace hits (the class is in but currently just heavy/slow; no stun yet). Would need an enemy stun state in `EnemyController`.
- **Per-weapon combo flavor** — combo finishers are currently generic (×1.5, wider). Give each weapon a distinct finisher and/or weapon-gated combo skill nodes (needs `SkillNode.weaponClass` gating + `PlayerStats` knowing the equipped class — it already stores `weaponClass`).
- **Weapon tiers** — dagger/spear/mace/greatsword are one tier each. Add copper/iron (and later silver/mythril) versions like the swords, with scaled ATK + recipes.
- **Respec at a Shrine** — respec currently works anywhere from the skill panel; gate it behind a placed "Shrine" station for the intended design.
- **Zone-restore ordering** — on re-entry, resources spawn before saved structures replay, so a resource can rarely share a restored build's tile. Restore structures before `SpawnRandomResources` to fully prevent overlap.

## ███ NEXT-PHASE PLAN (design locked — start here next session) ███

**Vision for the two new zones:**
- **Deep dungeon = FROZEN CRYPT** — ice caverns + undead. New foes: **Frost Wraith**, **Bone Knight**. silver/mythril = "cold metals". Guarded by a boss. (This is the existing deep area past the gate — reskin with ice materials, replace placeholder orcs/skeletons.)
- **Island = CURSED JUNGLE RUINS** — overgrown temple. New foes: **Cultist**, **Plant-monster (Vinemaw)**. gold = ancient treasure. The ruin landmark becomes a temple with a boss. (Replace placeholder orcs/skeletons.)
- **Ores feed a real GEAR SYSTEM** — tiers **copper < iron < silver < mythril** across **weapons, armor, shields**, plus upgraded **tools**. gold = treasure/late-game.
- **Each zone = a BOSS guarding loot** (rare gear / keys).

**Equipment system (v1) — decisions:**
- **Dedicated equip slots + panel**: player has Weapon / Armor / Shield slots; equip from backpack or an equipment panel; stats apply live.
- **Stats**: Weapon → ATK; Armor → DEF + **bonus max HP**; Shield → improves block (Q); some gear affects **stamina / max stamina**. (PlayerStats already has atk/def/maxHp/maxStamina.)
- **Recipe gating**: copper/iron gear at the **Forge**; **silver/mythril need a NEW station — an "Anvil"** (craftable later, gates top-tier gear). Smelt silver_ore/mythril_ore → ingots first (mirror copper/iron ingot recipes).
- **MULTIPLE WEAPON TYPES (v1):** distinct weapon classes — e.g. **sword** (balanced), **dagger** (fast/low dmg/low stamina), **mace** (slow/high dmg), **spear** (reach/thrust), **greatsword** (slow/wide/high dmg). Each class × each tier (copper→mythril). Class defines a stat profile: damage, attack-cooldown/speed, stamina cost, reach/arc.
- **SWING ANIMATIONS per weapon type (v1):** extend `VoxelKnightAnimator` (currently a single slash via `TriggerAttack`, params `slashAngle`/`slashDuration`) into per-class swings — e.g. side slash (sword), quick jab (dagger), overhead smash (mace/greatsword), forward thrust (spear). The equipped weapon's class selects its animation + drives the attack arc/reach used by `PlayerController` melee hit detection.

**Build order (dependency-first):**
1. **Equipment system (FOUNDATION — do first).** Equip slots + panel UI; equipping modifies PlayerStats (atk/def/+maxHP/+stamina); Forge recipes (copper/iron) + Anvil recipes (silver/mythril) for weapon/armor/shield + better tools. Hooks: `InventorySlotUI.OnClick` (route equippable items to equip), `HUDController.ItemTooltip` (fill weapon/armor/shield stat lines — stub exists), new `EquipmentController`. Add "anvil" to `StructureCatalogue` (placeable) + `CraftingManager` station id. Existing `copper_sword`/`iron_sword` recipes are a starting point.
2. **New enemy types.** Extend `EnemyType` enum (+FrostWraith, BoneKnight, Cultist, Vinemaw) + `VoxelEnemy` models + `EnemyController`/`Boss` AI (reuse melee/ranged/shield behaviors). Build prefabs via `MournSpireSetup`/scene builder `BuildEnemyPrefabs`.
3. **Zone bosses + loot.** Frost Warden (crypt) + Temple Guardian (island). Add gear to `LootSystem.BossPool` (low weight) / per-boss tables. Bosses use BossController pattern; persist-on-death like the Lich if they gate progression.
4. **Theme polish.** Ice/crypt materials for deep area; jungle/temple materials + maybe traps for island.

**Current placeholders to replace:** deep dungeon + island both spawn orcs/skeletons as stand-ins; silver/mythril/gold ore are inert materials with no recipes yet.

---


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
# = Wall        . = Floor      S = Player Start   P = Teleporter (dungeon entrance)
G = Goblin      O = Orc        K = Skeleton       B = Boss
T = Tree        R = Rock       I = Iron Ore       M = Merchant
```
⚠️ S and P are DIFFERENT. S = where the player spawns. P = teleporter portal to the dungeon.
Edit the `rawMap` field on the `MapData` ScriptableObject assets in `Assets/ScriptableObjects/`.

## Enemy Spawning & Respawn
- Enemies are placed at **fixed map positions** (`G`/`O`/`K`/`B` chars → `mapData.Spawns`); `GameManager.SpawnEnemies()` instantiates them on scene init. (Resources, by contrast, spawn randomly.)
- **Overworld:** enemies respawn **each new in-game day** — `GameManager.CheckDailyEnemyRespawn()` polls `DayNightCycle.Day`; cleared map-spawn markers (`EnemyController.SpawnTile`) refill (skips ones still alive or with the player on them). Flashes "Dawn breaks — creatures have returned."
- **Dungeon:** respawns **per visit** via scene reload (skipped by the daily check since `currentZone != Overworld`).
- Also regenerates fully on scene (re)load and on player death + `R`.
- **Lich persistence:** `GameManager._lichDefeated` (persists — GM is DontDestroyOnLoad). `SpawnOneEnemy` skips the Boss once defeated, so he never respawns on dungeon re-entry.

## Deep dungeon (sealed tunnel, opens on Lich death)
- The dungeon map (`BuildDungeonRaw`) is an **entry hall** + a large **deep area**, separated by a wall row (`DungeonSealRow = 27`) with a 2-wide **rune gate** (`'X'` → `TileType.Gate`, tracked in `MapData.GateTiles`).
- `WorldBuilder.CreateGateWall` builds the glowing gate walls (tracked in `_gateWalls`). `WorldBuilder.OpenGate()` destroys them and sets the gate tiles walkable.
- On Lich death (`OnEnemyDied`, `isBoss`): set `_lichDefeated`, call `OpenGate()`, flash "A tunnel grinds open...". On dungeon re-entry, `InitScene` re-opens the gate if `_lichDefeated`.
- **2 new ores** — `silver_ore`, `mythril_ore` (`TileType.SilverOre/MythrilOre`) — spawn **deep-only** via `WorldBuilder.deepZoneMinZ` (= `DungeonSealRow+1`); ore defs carry a `minZ`. Dungeon copper/iron lowered. Tougher placeholder foes (orcs/skeletons) seeded in the deep area — **new enemy types TBD**.

## Enemy / Boss Drops (loot)
- `LootSystem` (Assets/Scripts/Items/LootSystem.cs): **commons = simple % rolls** (`RollCommon(EnemyType)`); **boss = guaranteed haul + weighted-rarity rolls** (`RollBoss()` — add rare weapons/armor/shields to `BossPool` with small weights later).
- `GameManager.OnEnemyDied` rolls loot and calls `SpawnPickup(id, amount, deathPos)`.
- `Pickup` (Assets/Scripts/Items/Pickup.cs): a coloured floating cube that **spins/bobs, magnets to the player within range, and auto-collects on contact** (adds to inventory + small debris burst). Self-contained — `Init(id, amount, color, inventory, player)`.
- Drop colours via `GameManager.DropColor(id)`. Boss death flashes "The Lich's hoard scatters!".
- Current drops are mats + consumables; **weapons/armor/shields plug into `BossPool` (and commons) when those items exist.**

## Island progression (#6: NPC → boat → island)
- **Three scenes now:** Overworld(0), Dungeon(1), Island(2). `Zone` enum has `Island`; `ZoneFromName` maps scene→zone. `BuildIsland` menu generates `IslandMap.asset` (`BuildIslandRaw`) + a sandy scene.
- **Post-Lich NPC ("sea captain"):** `GameManager` spawns `IslandHerald` (WorldBuilder.SpawnIslandHerald) at the merchant hub when `_lichDefeated` and zone=Overworld. Talk (E) → grants `boat_plans` once + lore.
- **Boat:** Workbench recipe `boat` = 20 wood + 8 iron_ore + 1 `boat_plans` (so it's gated behind the NPC). 
- **Dock travel:** `TileType.Dock` ('D', `MapData.DockPos`), rendered by `WorldBuilder.CreateDock` (pier). Overworld dock at the east water edge; island dock on its west beach. `OnInteract` on the dock: from Overworld needs a `boat` in inventory → `TransitionToScene("Island")`; from Island → back to Overworld. (Future: rideable boat instead of dock-warp.)
- **Island content:** sandy biome, water border, central ruin landmark, palms (trees→wood), rocks, **`gold_ore`** (island-only, `TileType.GoldOre`), crops, placeholder tougher foes (orcs/skeletons — **bespoke island enemies TBD**).
- **Debug grant** includes a `boat` so the island is testable without the full chain.

## IMPORTANT: enemy prefabs assigned on EVERY scene's GameManager
- The GM is DontDestroyOnLoad — the **first** scene's GM (Overworld, forced by GameBootstrap) builds *all* zones. So `AssignEnemyPrefabs` now assigns **all** prefabs/defs (incl. Boss) on every scene's GM, regardless of `hasBoss`. The map's spawn markers (`B` only in the dungeon) gate where they actually spawn. (Bug fixed: previously the overworld GM had `bossPrefab=null`, so the Lich never spawned when entering the dungeon via the portal.)

## Farming (wild plants → seeds → crops)
- **Wild plants** spawn randomly in the overworld (`TileType.Plant`, harvested with the **Sword**) → drop **`seeds`**.
- **Plant:** press **G** while standing on open Floor → consumes 1 seed, spawns a `Crop` (Assets/Scripts/Items/Crop.cs) at your tile. Crops are walkable.
- **Grow:** `Crop` advances 3 visual stages (seedling → growing → mature) over `growthDays` in-game days (default 2), driven by `DayNightCycle.Day`.
- **Harvest:** press **E** on/next to a mature crop → yields `crop` (food) + a seed back, despawns. Immature shows "Crop is growing...".
- Tune speed via `Crop.growthDays`. Plant/crop counts: `WorldBuilder.plantCount` (overworld only).

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
| E | Interact / Gather / Use station / Enter dungeon |
| F | Use health potion (6s cooldown) |
| T | Use stamina tonic (6s cooldown) |
| B | Open/close backpack (items + equipment slots + stats) |
| K | Open/close skill tree (spend points / respec for gold) |
| Shift | Dodge Roll (after unlocking it in the Utility tree) |
| C | Hand-craft menu (no station needed) |
| G | Plant a seed on the tile you're standing on (farming) |
| P | Enter build/place mode (cycles inventory) |
| LClick | Place block (in build mode) |
| Esc | Cancel build / close panel / pause |
| R | Restart after death |

## Skill Tree System (v1)
- **Three trees** (`SkillTree` enum: Attack / Defense / Utility) defined as pure data in `Assets/Scripts/Skills/SkillData.cs` — node id, tree, branch/row, maxRank, `reqPoints` (points already spent in that tree to unlock), optional `reqNode` prerequisite, a `SkillEffect`, and per-rank magnitude. Add nodes here only.
- **Points:** `PlayerStats.GainXp` now grants **+1 skill point per level (+1 extra every 5th level)** and NO auto stat-ups. Points/ranks live on the **DontDestroyOnLoad `PlayerStats`** (`skillPoints`, `skillRanks` dict) so they persist across scenes.
- **Bonuses:** `PlayerStats.RecomputeSkills()` re-aggregates all ranked nodes into `skillBonus*` fields + multipliers (`attackStaminaMult`, `moveCooldownMult`, `critChance`, `goldMult`, `xpMult`, `canDodge`, …). Effective getters are now `base + equipment + skill` (`Atk`, `Def`, `MaxHpEff`, `MaxStaminaEff`, `BlockDivisorEff`). `GameManager.RecomputeEquipment()` + `RecomputeSkills()` are both re-run on every `InitScene`.
- **Combat hooks:** `GameManager.ComputePlayerDamage()` applies Adrenaline (low-HP ATK%), Executioner (vs low-HP enemy), and Crit (double dmg). `OnEnemyDied` scales XP/gold by the multipliers. `PlayerController` reads the stamina/move multipliers and i-frames.
- **Dodge Roll** (Utility unlock): **Left/Right Shift** dashes up to 2 tiles in the last-moved direction with ~0.4s invulnerability (`PlayerController.HandleDodge`, gated by `stats.canDodge`).
- **UI:** Press **K** to open `SkillPanel`. Node buttons are built at runtime by `HUDController.BuildSkillButtons()` from `SkillData.All` into three column containers (`skillAttackCol/DefenseCol/UtilityCol`) — colour-coded maxed/buyable/locked. **Respec** button refunds all points for `40 × pointsSpent` gold (`PlayerStats.TryRespec`). *(Currently respec works anywhere via the panel; gating it behind a physical Shrine station is a TODO.)*

## Animals & Frostbite
- **Wildlife** (`AnimalController`, spawned by `WorldBuilder.SpawnAnimals` per zone, wired in `GameManager.WireAnimals`): **Deer/Rabbit** (overworld) flee; **Wolf** (island) chases + bites; **Yak** (island) is neutral but retaliates when struck. Tile-based greedy movement, share the `_occupied` grid. Melee hits them via `FindAnimalAt` in `OnAttackSwing`; death drops **hide** + **meat** (`OnAnimalDied`).
- **Frostbite** (`GameManager.TickFrostbite`, Island only): buildup 0–100 rises with time, scaled by `(1 - coldRes)`; thaws in warm zones / when fully cold-proof. At max → periodic true damage (`PlayerStats.TakeEnvironmentDamage`, ignores DEF) + movement slow (`frostMoveMult`). HUD shows a frostbite meter (`HUDController.SetFrostbite`).
- **Warm armor** (`GearInfo.coldRes`): the **Tannery** station (built at the Workbench from hides) converts base armor + hides → `warm_copper_armor` / `warm_iron_armor` (coldRes = 1, fully negates frostbite). `RecomputeEquipment` sums equipped `coldRes` into `playerStats.coldRes`.

## Weapons, Swings & Combos
- **Weapon classes** (in `GearData`, `weaponClass`): sword (copper/iron), **dagger**, **spear**, **mace**, **greatsword** (one tier each, crafted at the Forge). All occupy the Weapon slot / Sword hotbar.
- **Distinct hit patterns** (facing-relative, `GameManager.AttackTiles`): dagger/mace = single front tile; sword = front + both sides (cleave); spear = front + 2 tiles ahead (reach); greatsword = 3-tile fan. Per-weapon **attack speed** via `WeaponCooldown` (dagger fast → greatsword slow).
- **Per-weapon swings** (`VoxelKnightAnimator.SwingByClass`): dagger jab, mace heavy slam (wind-up), greatsword horizontal sweep (Y), spear forward thrust (lunge), sword/tools overhead chop. `VoxelKnight.CurrentWeaponClass` drives both mesh (`BuildWeapon`) and animation.
- **Held-weapon setup** flows through `GameManager.ApplyHeldWeapon(tool)` → sets mesh+tier colour (`EquipTool(tool, metal, weaponClass)`), `player.attackCooldown`, and `playerStats.weaponClass`. Called on equip, tool-switch, and scene init.
- **Timed 3-hit combo** (`GameManager.OnAttackSwing`): swings within `comboWindow` (+`comboWindowBonus`) chain; the 3rd is an empowered **finisher** (×1.5 +`comboFinisherBonus`, wider if `comboWideFinisher`). Attack-tree skills: **Combo Training** (finisher dmg), **Flow** (window), **Rampage** (wide finisher).

## Gathering, Tools & Alchemy
- **Multi-hit nodes:** `ResourceNode` now has hit-points (`DefaultHits` per type — wood 3, stone 4, ores 4-7, plants 1). `Interact(inv, out outcome, toolPower, yieldBonus, doubleChance)` chips it; harvest completes at 0. `GameManager.ToolPower(ToolType)` = best owned tool tier (basic 1 / copper 2 / iron 3) → better tools = fewer swings.
- **Gather skills** (Utility tree): Forager (`GatherYieldFlat`) and Prospector (`GatherDoubleChance`) feed `PlayerStats.gatherYieldBonus` / `gatherDoubleChance` into the harvest.
- **Glass & potions:** mining **stone** has a 40% chance to also drop **sand** (`ResourceNode.secondaryType`). Chain: sand → **glass** (Forge) → **bottle** + 2 **crop** → **potion**/**tonic** at the new **Alchemy Table** station (`craftingStationID = "alchemy"`, hand-crafted from 4 glass + 6 wood). This gives crops a use.
- **Tier visuals:** `VoxelKnight.EquipTool(tool, metalColor)` colours the held blade/head by tier; `GameManager.HeldMetal()` picks basic/copper/iron from the equipped weapon (sword) or best owned tool. `RefreshHeldVisual()` updates it on equip.

## Crafting & Building System
- **`'S'` = PlayerStart, `'P'` = Teleporter** — these are DIFFERENT map characters. Never confuse them.
- **Player spawns at `'S'`** — must be placed near the center of the map or the camera will look off the edge.
- **Hand-craft** (press **C**, `requiredStation = null`) only makes the **Workbench** (8 wood) to bootstrap. All other stations (Forge 6 stone+4 wood, Stonecutter 8 stone, Alchemy Table 4 glass+6 wood) are crafted **at the Workbench**.
- **`StructureCatalogue` calls `CreatePrimitive` in its constructor** — must be initialized in `Awake()`, never as a field initializer on a MonoBehaviour (causes Unity serialization crash).
- **`BuildingPlacer.inventory` and `BuildingPlacer.mainCamera`** must be wired in `MournSpireSceneBuilder.BuildSharedObjects()`.
- After crafting, `InventoryController` holds items by string ID (e.g. `"workbench"`). Press **P** to enter build/place mode.

## Input System — use the NEW one everywhere
- **Active Input Handling = "Input System" (new).** Gameplay scripts (`PlayerController`, `GameManager`, `Hotbar`, `HUDController`) read input via `Keyboard.current.<key>.wasPressedThisFrame` and `Mouse.current.position.ReadValue()` / `Mouse.current.leftButton.wasPressedThisFrame`.
- **NEVER use legacy `UnityEngine.Input` (`Input.GetKeyDown`, `Input.mousePosition`, `Input.GetMouseButtonDown`)** — it throws `InvalidOperationException` every frame because handling is set to the new system. This bit `BuildingPlacer` (P/build mode) — it was the lone legacy holdout and spammed errors on every `Update()`.
- Always `using UnityEngine.InputSystem;` and null-check `Keyboard.current` / `Mouse.current` (they can be null if no device).

## Building system (snap model)
- **Snap grid = the 2-unit world tile grid** (`BuildingPlacer.TileSize = 2`). Floors, walls, stations and player movement all share it. `TileOf(point)=round(point/2)`, tile centre = `(tx*2, tz*2)`.
- **Floors** fill a tile (walkable, buildable-on). **Stations** fill + block a tile. **Walls** sit on a tile EDGE (border line between two tiles) and block crossing that edge.
- **Rotate (R)** cycles which of the 4 edges of the pointed tile a wall snaps to (0=+Z,1=+X,2=−Z,3=−X). `GhostRotation = 90°·steps`.
- **Edge blocking**: `PlayerController.IsEdgeBlocked(from,to)` is consulted on each move; `GameManager` keeps a `HashSet<long>` of canonical edge keys (`EdgeKey` orders the two tiles + axis). Walls add/remove via `BuildingPlacer.SetEdgeBlocked`. Tile blocking (stations) still uses `_occupied` + `SetTileBlocked`.
- **Snap OFF** = free placement at the exact cursor (no occupancy, no blocking — decorative).
- Occupancy lives in `BuildingPlacer` sets (`_floorTiles`, `_solidTiles`, `_wallEdges`); `PlacedStructureMarker` records kind+tile/edge so Move/Remove undo cleanly. The old 1-unit `BuildingGrid` is no longer the placement authority.

## Dynamic UI Rows (Shop / Crafting) — hard-won lessons
- **`ShopRow.prefab` is shared** by `ShopController` and `CraftingUIController` (both have a `rowPrefab` field). It's built by `BuildShopRowPrefab()` in the scene builder.
- **An `Image`-only button inside a `HorizontalLayoutGroup` collapses to ZERO width** — `Image` reports no preferred width, so `childForceExpandWidth=false` shrinks it to nothing. Symptom: button label text shows (TMP overflows) but no background rectangle and nothing is clickable. **Fix: add a `LayoutElement` with `minWidth`/`preferredWidth` to the button** (TMP labels are fine — they report preferred width).
- **NEVER `AssetDatabase.DeleteAsset()` a prefab that scenes reference, then recreate it.** Delete changes the prefab's GUID; the next scene build (e.g. Dungeon) orphans the previously-built scene's (e.g. Overworld's) reference → `MissingReferenceException: The variable rowPrefab of CraftingUIController doesn't exist anymore`. **Always overwrite in place with `SaveAsPrefabAsset(go, path)`** — it keeps the GUID stable.
- **Don't `AssetDatabase.LoadAssetAtPath()` a prefab you created earlier in the SAME build run** — it may not be imported into the AssetDatabase yet and returns `null`. **Reuse the reference returned by `SaveAsPrefabAsset`/`BuildShopRowPrefab()`** and assign it to every consumer (`sc.rowPrefab` AND `craftingUI.rowPrefab`).
- Disabled Unity Buttons go ~50% transparent via Color Tint transition. Crafting buttons use `Selectable.Transition.None` + manual green(craftable)/grey(not) colors so they stay visible.

## Verify compilation BEFORE rebuilding scenes
- **A compile error blocks ALL recompilation.** Unity keeps running the last-good assemblies, so `Build Overworld/Dungeon Scene` silently uses STALE editor code — new panels/fields never appear, with no obvious error.
- `Assets/Refresh` + checking `read_console` errors is NOT reliable for this (it returned `[]` while a real error existed).
- **After editing C#, run `mcp__mcp-unity__recompile_scripts` and confirm 0 errors before building scenes.** It returns the actual compiler errors (e.g. CS0111 duplicate method).
- Symptom that means "stale assembly": a freshly added serialized field doesn't show on the component, or a scene-builder-created GameObject is missing after a rebuild. Recompile and check for errors first.
- Gotcha when replacing a method block via edit: don't reintroduce a helper (e.g. `FormatID`) that already exists below the replaced region → CS0111 duplicate-member error.

## Blank/blue screen on Play (world never builds) — ROOT CAUSE
- **Symptom:** Press Play, see only sky-blue with the player floating; camera stuck at the scene-builder edit pose `(10,14,-5)`; `WorldBuilder` has zero children at runtime; no console errors.
- **Cause:** `GameManager` is `DontDestroyOnLoad` + singleton. When the editor starts on the **Dungeon** scene, `GameBootstrap` forces Overworld (scene 0) to load. The Dungeon GameManager wins the singleton and its `Start()` builds Dungeon; then Overworld loads, but `OnSceneLoaded` used to early-return unless `_transitioning` (only true for teleporter transitions), so the forced Overworld load never ran `InitScene()` → `WorldBuilder.Build()` never called → blank scene.
- **Fix:** `OnSceneLoaded` gates on `_started` (has `Start()` run once) instead of `_transitioning`. The first scene is built by `Start()`; **every** later load — including `GameBootstrap`'s forced scene-0 load — re-finds refs and rebuilds. `Start()` sets `_started = true` at its end.
- **Rule:** Diagnosing "blank scene" → check the live `GameManager.currentZone` and whether `WorldBuilder` has children. `currentZone` not matching the active scene means `InitScene` didn't run for this scene.

## Camera Rules
- **Always use `GameBootstrap` (`RuntimeInitializeOnLoadMethod.BeforeSceneLoad`)** to force scene 0 load — Unity plays whichever scene is open in the editor, not necessarily scene 0.
- **`'S'` (PlayerStart) must be near map center** — camera sits at `player.position + (0, 16, -3)` looking at `player + (0, 0, 1)`. If the player spawns at a map edge, the camera looks off into empty sky.
- **Camera height 16, behind 3** (`new Vector3(0, 16f, -3f)`) gives a ~76° downward angle — fills the screen with world rather than sky.
- **Never run `Build Overworld Scene` / `Build Dungeon Scene` while Play mode is active** — it causes "cannot use during play mode" exceptions. Stop Play first.
- **`DayNightCycle.startTime`** — default is `0.5f` (noon). Dawn (0.25) tints everything brownish-orange. Keep noon as default for legible overworld.
- **Camera `clearFlags = CameraClearFlags.SolidColor`, `backgroundColor = sky blue`** — set on the Camera in the scene builder so empty sky is blue, not Unity's default gray.

## Scene Rebuild Checklist
When something looks broken visually, run in this order (with Play mode **stopped**):
1. `MournSpire → Build Overworld Scene`
2. `MournSpire → Build Dungeon Scene`
3. Press Play — Overworld loads first due to `GameBootstrap`

## Key Design Decisions
- **No Rigidbody movement** — tile grid + lerp keeps combat legible and matches the browser prototype exactly
- **EnemyStats as ScriptableObject** (`EnemyStatsDef`) — swap enemy values without touching code
- **BFS max depth 12** for standard enemies, 20 for boss — prevents pathfinding freezes on large maps
- **Projectile as self-moving MonoBehaviour** — no physics layer needed; hit check is distance-based
- **Shop UI built dynamically** by `ShopController.Rebuild()` each time it opens — avoids prefab bloat for shop rows
- **WorldBuilder creates primitives at runtime** — no tile prefabs needed; materials assigned via Inspector refs on WorldBuilder
- **Editor setup scripts** — `MournSpireSetup.cs` (ScriptableObjects + prefabs) and `MournSpireSceneBuilder.cs` (full scene hierarchy) can rebuild everything from scratch via the MournSpire menu
- **UnityEvent fields initialized inline** (`= new UnityEvent()`) to avoid null refs when components are created via AddComponent in Editor scripts
- **GitHub remote**: `https://github.com/darkmage1000/MournSpire-2026-05-20_18-22-30.git` (inner project at `MournSpire/MournSpire/`)
