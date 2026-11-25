# AI Director Demo Game

[![CI](https://github.com/sighmonk/ai-director-demo/actions/workflows/ci.yml/badge.svg)](https://github.com/sighmonk/ai-director-demo/actions/workflows/ci.yml)

A **2D top-down shooter** with a custom **AI Director** system that dynamically adjusts difficulty in real time. Built in **Godot 4.7.2** with **C# (.NET 8.0)**.

## About

This game was created as part of a **Bachelor's Engineering Thesis** at Nicolaus Copernicus University in Toruń. The goal of the project is to design and implement a system that uses artificial intelligence to dynamically adjust the difficulty level of a game in real time, keeping the player in an optimal engagement state known as **Flow** — a concept from Csíkszentmihályi's psychology research describing the zone between boredom (too easy) and frustration (too hard).

The project demonstrates that automated, real-time adaptation of gameplay parameters based on player performance is a viable approach to maintaining engagement, potentially outperforming static difficulty settings.

## Documentation

Full technical documentation is in [Docs/](Docs/README.md):

- [Architecture](Docs/architecture.md)
- [AI Director](Docs/ai-director.md)
- [Dungeon generation](Docs/dungeon-generation.md)
- [Testing](Docs/testing.md)
- [Development](Docs/development.md)
- [CI/CD](Docs/ci-cd.md)
- [Agent and contribution workflow](Docs/agent-workflow.md)

## Builds

Every push to `main` publishes a **prerelease build** with Windows, Linux and macOS zips and a
generated changelog. Pushing a `v*` tag publishes a stable GitHub Release with the same artifacts.
See [Docs/ci-cd.md](Docs/ci-cd.md).

## Gameplay

The player navigates procedurally generated dungeons, fighting enemies in a top-down shooter format. Each session lasts a configurable duration (default: 120 seconds), during which the AI Director continuously monitors player performance and adapts the challenge.

- **Movement:** WASD
- **Aiming / Shooting:** Mouse
- **Enemies:** Contact-damage based; killed by player projectiles

## AI Director

The core of the project — an autonomous system inspired by Left 4 Dead's AI Director and Hunicke's Hamlet system, adapted for a single-player context.

### Performance Ratio

The Director evaluates player state using a **Performance Ratio**:

$$R = \frac{C_p}{E_p}$$

- **$C_p$ (Current Player Power):** Composite metric computed every second from a 15-second sliding window — factors in kill rate, damage taken, and current HP with non-linear health dependency.
- **$E_p$ (Expected Performance):** Read from a designer-authored difficulty curve mapped to session progress.
- The raw ratio is **exponentially smoothed** to prevent erratic state transitions.

### Three-State Difficulty Machine

| State | Condition | Behavior |
|---|---|---|
| **Maintain** | $0.8 \leq R \leq 1.2$ | No intervention — gameplay is balanced |
| **Reduce** | $R < 0.8$ or Flailing detected | Accumulates *Benefit Points*, spends them on player-helping actions |
| **Intensify** | $R > 1.2$ | Accumulates *Negative Points*, spends them on challenge-increasing actions |

Points accumulate proportionally to the distance of $R$ from the Flow zone (up to 4x multiplier) and are spent via **weighted random action selection**.

### Flailing Detection

An emergency override mechanism: if player HP drops below 30% **and** HP decreased by ≥ 30 points in the last 5 seconds, the system immediately transitions to **Reduce** state regardless of $R$.

### Director Actions

| Action | Type | Effect |
|---|---|---|
| `IncreaseSpawnRate` | Challenge | Multiplies spawn timer by 0.7 (30% faster spawns) |
| `BuffEnemies` | Challenge | +5% speed, damage, scale, and HP to all active enemies |
| `SpawnHorde` | Challenge | Instantly spawns 10 enemies near the player |
| `ReduceSpawnRate` | Benefit | Multiplies spawn timer by 1.5 (50% slower spawns) |
| `DebuffEnemies` | Benefit | −5% stats on all active enemies |

Each action has configurable cost, weight, and cooldown parameters.

## Procedural Dungeon Generation

A multi-stage pipeline generates unique dungeon layouts each run:

1. **Cell Generation** — Random-sized cells distributed in an elliptical pattern
2. **Physics-based Separation** — Godot physics resolves overlapping cells
3. **Main Room Selection** — Top 25% largest cells become rooms
4. **Graph Connection** — Delaunay triangulation → Minimum Spanning Tree + 25% loop edges for non-linear paths
5. **Corridor Creation** — L-shaped hallways between connected rooms
6. **Extra Room Discovery** — Small cells adjacent to corridors become bonus rooms
7. **Tile Rendering** — Floor and wall tiles placed on TileMapLayers

Supports seeded RNG for reproducible layouts and optional step-by-step visualization.

## Architecture

### Manager Singletons (Godot Autoload)

| Manager | Responsibility |
|---|---|
| `SignalManager` | Central event hub (Observer pattern) for all gameplay signals |
| `DataManager` | Subscribes to signals, maintains `PerformanceData` and `PlayerData` |
| `EnemyManager` | Spawns enemies on timer, handles Director modifications (buff/debuff/spawn rate), A* pathfinding |
| `GameManager` | Session time tracking, difficulty curve, player registration |

### Entity Design

- **Player** (`CharacterBody2D`) — Movement, shooting, `HealthComponent` with invincibility frames
- **Enemy** (`Area2D`) — Contact damage, projectile death, supports runtime buff/debuff with visual feedback
- **HealthComponent** — Reusable HP system with health bar, invincibility frames, damage/heal/death signals
- **HitboxComponent** — Collision-to-damage bridge delegating to `HealthComponent`

### Design Patterns

- **Singleton** — All managers via Godot Autoload
- **Observer** — Godot signals for decoupled communication
- **State Machine** — `DifficultyStateMachine` with `DirectorState` base class
- **Strategy** — `DirectorAction` polymorphism for interchangeable interventions
- **Composition** — Reusable `HealthComponent` / `HitboxComponent` for entities

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| Godot Engine | 4.7.2 (.NET) | Game engine |
| .NET | 8.0 | Runtime |
| C# | 12.0 | Language |
| GdUnit4 | 5.1.0-rc7 | Unit testing framework |

## Testing

Unit tests are written using **GdUnit4** and cover the AI Director subsystem:

| Test Suite | Area |
|---|---|
| `TimeBufferTest` | Sliding-window buffer correctness and expiration |
| `PerformanceDataTest` | Kill rate and damage rate aggregation |
| `FlailingDetectionTest` | Emergency detection edge cases |
| `DirectorPointsCalculationTest` | Points multiplier across all states |
| `DifficultyStateMachineTest` | State transition correctness |
| `ActionSelectionTest` | Weighted random action selection |
| `DirectorActionTest` | Action execution, cooldown, cost validation |

Run tests with:

```powershell
$env:GODOT_BIN = 'C:\Godot\4.7.2mono\Godot_v4.7.2-stable_mono_win64.exe'
godot --headless --path . --import
dotnet test "project kitchen.sln" --settings .runsettings
```

See [Docs/testing.md](Docs/testing.md) for the full suite map and troubleshooting.

## Project Structure

```
Src/
├── AI Director/         # AI Director system
│   ├── Director.cs      # Main Director logic & performance calculation
│   ├── DifficultyStateMachine.cs
│   ├── Actions/         # Director action implementations
│   ├── Data/            # PerformanceData, PlayerData, DirectorData
│   └── States/          # Intensify, Maintain, Reduce states
├── Components/          # Reusable HealthComponent, HitboxComponent
├── Dungeon/             # Procedural dungeon generation pipeline
├── Enemies/             # Enemy entities
├── Level/               # Level scene management
├── Managers/            # Singleton managers (Signal, Data, Enemy, Game)
├── Player/              # Player entity
├── Projectiles/         # Bullet implementations
└── UI/                  # Camera, debug overlays
Tests/
└── AIDirector/          # GdUnit4 unit tests
```