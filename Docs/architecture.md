# Architecture

## Runtime overview

The game boots the `Level` scene declared in `project.godot` and relies on five autoload
singletons for cross-cutting state. The level then spawns the dungeon, the AI Director and the
debug UI.

```mermaid
flowchart TD
    subgraph Autoloads
        DM[DataManager]
        SM[SignalManager]
        EM[EnemyManager]
        GM[GameManager]
        PM[PickupManager]
    end

    subgraph Level[Level.tscn]
        DG[DungeonGenerator]
        DIR[Director]
        UI[DebugUI]
        P[Player]
        E[Enemies]
    end

    GM --> DG
    DG -->|DungeonGenerationCompleted| GM
    DG --> EM
    GM --> P
    PM -->|spawns| HP[HealthPacks]
    P -->|pickup| HP
    DIR --> DSm[DifficultyStateMachine]
    DSm --> A[DirectorAction nodes]
    A --> EM
    A --> PM
    P --> SM
    E --> SM
    HP --> SM
    SM --> DM
    DM --> DIR
```

The autoloads are registered in this order in `project.godot`: `DataManager`, `SignalManager`,
`EnemyManager`, `GameManager`, `PickupManager`. Keep that in mind when changing initialization code
that reaches for another singleton.

### Managers

| Manager | Responsibilities | Key API |
| --- | --- | --- |
| `SignalManager` | Central event hub. Declares all gameplay and debug signals. | `EnemyKilled`, `PlayerTookDamage`, `WeaponFired`, `HealthPackSpawned`, `HealthPackCollected`, `DungeonChanged`, `DungeonRoomCreated`, `DungeonPassageClosed`, `DungeonPassageReopened`, `PerformanceMetricsUpdated`, … |
| `DataManager` | Subscribes to gameplay signals, owns `DirectorData`, syncs player health, tracks spawned/living difficulty value. | `RegisterDirectorData`, `RegisterPlayer`, `RegisterEnemySpawn`, `RegisterDirectorSpawn`, `RegisterEnemyDeath` |
| `EnemyManager` | Spawns enemies on a timer, keeps the active enemy list, builds an `AStarGrid2D` from the world tilemap, answers floor/reachability queries and applies Director buffs/debuffs or spawn-rate changes. | `SetWorldTileMap`, `GetPath`, `IsFloorTile`, `IsFloorReachable`, `TryGetRandomFloorTileNear`, `SpawnEnemyAt`, `RebuildNavigation`, `ModifySpawnRate`, `BuffAllEnemies`, `DebuffAllEnemies`, `ForceSpawnEnemy`, `ActiveEnemies` |
| `GameManager` | Session clock, session progress, current difficulty from `DifficultyCurve`, player and dungeon registration, debug toggles. | `SessionTime`, `SessionProgress`, `CurrentDifficulty`, `RegisterPlayer`, `RegisterDungeon`, `Dungeon` |
| `PickupManager` | Ambient health pack spawning, the active-pack cap and a direct spawn entry point for Director actions. Placement only uses floor tiles reachable from the player. | `TrySpawnNearPlayer`, `SpawnHealthPackAt`, `ActiveCount`, `MaxActiveHealthPacks` |

### Signal flow

Gameplay code does not call the Director directly. It emits through `SignalManager`; `DataManager`
aggregates the values and the Director samples them.

```mermaid
sequenceDiagram
    participant P as Player
    participant E as Enemy
    participant S as SignalManager
    participant D as DataManager
    participant R as Director

    P->>S: WeaponFired / WeaponHit
    P->>S: PlayerTookDamage / PlayerHealed
    E->>S: EnemyKilled
    S->>D: update PerformanceData / PlayerData
    loop every CheckInterval (1 s)
        R->>D: read KillRate, DamageTakenRate, health
        R->>R: compute Cp, Ep, smoothed ratio
        R->>R: pick state (Maintain/Reduce/Intensify)
        R->>R: accumulate and spend points
    end
```

`DataManager` keeps both the 15-second windows used by the Director (`KillRate`,
`DamageTakenRate`) and shorter/longer buffers used by other metrics (6 s, 5 s, 30 s). See
[ai-director.md](ai-director.md) for the exact windows.

### Session lifecycle

1. Autoloads are added to the scene tree.
2. `Level._Ready()` connects to `DungeonGenerator.DungeonGenerationCompleted`.
3. `DungeonGenerator` generates and renders the dungeon; the physics separation step is
   asynchronous and completes on a deferred callback.
4. `OnDungeonGenerationCompleted` gives the world tilemap to `EnemyManager`, spawns the player at
   the starting room centre and attaches `PlayerCamera`.
5. `Director` evaluates performance every second and drives the `DifficultyStateMachine`.
6. `EnemyManager` keeps spawning enemies for `SessionDuration`; the spawn rate follows the
   difficulty curve and any active Director multiplier.

## Entities and components

| Type | Base | Responsibility |
| --- | --- | --- |
| `Player` | `CharacterBody2D` | WASD movement, aiming and shooting through an `IPlayerInput` (device input by default; the simulation bot can replace it). Scene default speed is `600`. |
| `Enemy` | `Area2D` | Registers with `EnemyManager`, contact damage through `HitboxComponent`, death handling, runtime buff/debuff with visual feedback. |
| `EnemyRed` | `Enemy` | Aggro (200 px) / de-aggro (800 px), line-of-sight raycast, A* path following refreshed every 0.2 s. |
| `HealthComponent` | `Node2D` | HP, health bar, optional invincibility frames, `HealthChanged` and `Died` signals. |
| `HitboxComponent` | `Area2D` | Small bridge that forwards `Damage()` calls to a `HealthComponent`. |
| `PlayerBullet` | `Area2D` | Directional projectile with lifespan, pierce counter and tile collision cleanup. |
| `HealthPack` | `Area2D` | Group `HealthPickups`: heals the player once through `HealthComponent.Heal`, then plays a short scale/fade effect and frees itself. Enemies cannot pick it up. |
| `PlayerCamera` | `Camera2D` | Free-look camera with scroll-wheel zoom. |
| `DebugUi` | `CanvasLayer` | Renders Director state, Cp/Ep/ratio, points, spawn rate and recent actions. |
| `SimulationBot` | `Node` | Seeded simulated player for the experiment: perception, reaction, aim error, distance keeping and crowd avoidance; drives the player through `ScriptedPlayerInput`. |

Composition is preferred over inheritance for reusable behaviour: enemies and the player share
the same `HealthComponent` / `HitboxComponent` scenes.

## Design patterns

- **Singleton** — managers exposed through `Instance` properties and registered as autoloads.
- **Observer** — Godot signals are used for gameplay-to-analytics communication.
- **State machine** — `DifficultyStateMachine` owns three `DirectorState` nodes.
- **Strategy** — each Director intervention is a `DirectorAction` subclass; selection of the
  action to run is delegated to an `IActionPolicy` (`RulePolicy` by default), while filtering by
  cost and cooldown stays in the Director.
- **Model/service split** — `DungeonWorldModel` holds the mutable floor layout and validates every
  mutation, `DungeonMutator` applies accepted mutations to the tilemaps; the same pattern keeps
  `HealthPackPlacement` and `AutoSpawnScheduler` testable without a scene.
- **Composition** — health and hitbox behaviour is attached to entities as child nodes.

## Source map

```text
Src/
├── AI Director/     Director, state machine, states, actions, data buffers, policies
├── Components/      HealthComponent, HitboxComponent
├── Dungeon/         Generation pipeline, tile rendering, runtime mutation
├── Enemies/         Enemy base class and EnemyRed implementation
├── Level/           Level scene orchestration
├── Managers/        Signal, Data, Enemy, Pickup and Game managers
├── Pickups/         Health pack entity, placement rules and spawn scheduler
├── Player/          Player controller and input abstraction
├── Projectiles/     PlayerBullet
├── Simulation/      Bot profiles, bot controller and the headless calibration harness
└── UI/              Camera and debug overlay
Tests/AIDirector/    GdUnit4 test suites for the Director
Tests/Gameplay/      GdUnit4 test suites for gameplay systems
Tests/Simulation/    GdUnit4 test suites for the bot profiles and calibration options
```

## Runtime caveats

- **Autoload order** matters when adding initialization code; do not assume a later autoload is
  ready inside an earlier one's `_Ready()`.
- **Local-only Godot-MCP addon** — `addons/godot_mcp` is intentionally not committed. The project
  still imports and exports without it; Godot logs a missing-editor-plugin error if the
  `[editor_plugins]` entry is present in a clean checkout.
- **Headless export warning** — Godot may log
  `Failed to get property '_stateNodes' from node 'DifficultyStateMachine'` while converting
  scenes during a headless import/export. It does not stop the export and the value is restored
  when the scene is instantiated at runtime.
- **Debug UI** is toggled by `GameManager.ShowDebugUi`; `ShowEnemyDebug` draws aggro ranges,
  de-aggro ranges, paths and line-of-sight rays.
