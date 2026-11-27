# AI Director (Dynamic Difficulty Adjustment)

The Director keeps the player near an optimal engagement state by comparing current player power
(`Cp`) with expected performance (`Ep`) and applying gameplay interventions.

## Performance ratio

The core metric is:

```text
R = Cp / Ep
```

### Current player power (`Cp`)

`Cp` is recomputed every `CheckInterval` (default 1 s) from the 15-second kill and damage windows:

```text
normKills  = clamp(KillRate / ExpectedKillsPer15s, 0, 1.5)
normDamage = clamp(DamageTakenRate / MaxDamagePer15s, 0, 1)

dangerLevel    = 0                                    if HP% >= HealthSafetyThreshold
               = 1 - (HP% / HealthSafetyThreshold)    otherwise
healthPenalty  = dangerLevel * LowHealthPenalty
damageScale    = 1 + dangerLevel * (LowHealthMultiplier - 1)

Cp = clamp(CpBaseline + KillBonus * normKills
           - DamagePenalty * normDamage * damageScale
           - healthPenalty, 0, 1)
```

Low HP therefore hurts twice: it applies a direct penalty and amplifies the impact of recent
damage.

### Expected performance (`Ep`)

`Ep` is derived from the designer-authored difficulty curve:

```text
Ep = CpBaseline + 0.15 * CurrentDifficulty
```

`CurrentDifficulty` is sampled from `Assets/Resources/DifficultyCurve.tres` at the current session
progress (0–1). The shipped curve starts at 0.4 and reaches 1.0 at 85% of the session.

### Smoothing

The raw ratio is exponentially smoothed with `RatioSmoothingFactor` (default `0.1`) on every check:

```text
smoothed = lerp(smoothed, raw, RatioSmoothingFactor)
```

This prevents rapid state flapping when a single sample is noisy.

## States

| State | Condition | Behaviour |
| --- | --- | --- |
| `Maintain` | `0.8 <= R <= 1.2` | No points, no intervention |
| `Reduce` | `R < 0.8` or flailing | Accumulates **benefit points**, spends them on player-helping actions |
| `Intensify` | `R > 1.2` | Accumulates **negative points**, spends them on challenge-increasing actions |

Transitions emit `SignalManager.DifficultyStateChanged`; the debug UI displays the new state.

### Flailing override

Flailing is an emergency override that forces `Reduce` regardless of `R`:

```text
HP% < FlailingHealthThreshold (0.3)
AND HP lost in the last 5 seconds >= FlailingHPDropThreshold (30)
```

HP drops are tracked every frame in a 5-second `TimeBuffer`; healing does not count as a drop.

## Points economy

While in `Reduce` / `Intensify`, points accumulate every frame:

```text
pointsPerSecond = BasePointsPerSecond * multiplier
```

The multiplier depends on the distance from the Flow zone:

- `0` inside the Flow zone,
- `1` exactly at the threshold (0.8 or 1.2),
- up to `MaxPointsMultiplier` (default `4`) at the extremes (ratio 0 or 2.0+).

Eligible actions are filtered by action type, cost and cooldown, then handed to the active
`IActionPolicy` (`Director.Policy`). The default `RulePolicy` performs a weighted random draw. A
successful action spends its cost; actions can fire repeatedly while points and cooldowns allow it.

## Action policies

Action selection is a strategy so the master's thesis experiment can swap policy arms while the
state machine, filtering and points economy stay identical:

| Type | Responsibility |
| --- | --- |
| `ActionSelectionContext` | Immutable snapshot: eligible candidates, action type, available points, current time, state name |
| `IActionPolicy` | `SelectAction(context)` returns the action to execute, or `null` to do nothing |
| `RulePolicy` | Baseline: weighted random draw over positive weights; zero/negative weights are never selected |

`Director` filters candidates by type, cost and `CanExecute` before calling the policy, so a policy
never has to re-implement eligibility rules. `Director.Policy` is assigned in `_Ready` when it has
not been set already; `Director.SelectionSeed` (`0` = global RNG) makes the `RulePolicy` draw
reproducible for fixed-seed experiment runs. State and action-type filtering stay in the Director,
which keeps arm A behaviour unchanged.

## Director actions

| Action | Type | Default effect | Cost | Weight | Cooldown |
| --- | --- | --- | --- | --- | --- |
| `IncreaseSpawnRate` | Negative | Spawn interval × `0.7` (faster) | 10 | 1 | 5 s |
| `BuffEnemies` | Negative | +5% speed, damage, scale and max HP | 10 | 1 | 5 s |
| `SpawnHorde` | Negative | Spawns a horde immediately (scene default: 20 enemies) | 10 | 1 | 5 s |
| `ReduceSpawnRate` | Benefit | Spawn interval × `1.5` (slower) | 10 | 1 | 5 s |
| `DebuffEnemies` | Benefit | −5% speed, damage, scale and max HP | 10 | 1 | 5 s |
| `SpawnHealthPack` | Benefit | Spawns a health pack on a reachable floor tile near the player, preferring tiles outside enemy aggro range | 10 | 1 | 5 s |
| `AddRewardRoom` | Benefit | Opens a small room next to an existing corridor or room and places one health pack in its centre | 10 | 1.5 | 20 s |
| `AddThreatRoom` | Negative | Opens the same kind of room and spawns `EnemiesPerRoom` enemies on its floor tiles | 10 | 1.5 | 20 s |

`Cost`, `Weight` and `Cooldown` are exported on every action node, so they can be tuned per action
in `Director.tscn`. Spawn-rate multipliers decay back to `1.0` at `0.1` per second and are clamped
to `[0.3, 3.0]`.

### Action success and failure

`DirectorAction.OnExecute` reports whether the action had an effect. `TryExecute` applies the
cooldown and emits `DirectorActionExecuted` only on success, and `Director.TryExecuteAction` spends
the action cost only on success. A rejected action therefore stays ready and free; `SpawnHealthPack`,
for example, returns `false` when `PickupManager` cannot find a reachable floor tile, and logs the
rejection instead of charging benefit points.

`SpawnHealthPack` exports `SpawnDistanceMin`, `SpawnDistanceMax` and `EnemyAggroAvoidRadius`; the
placement itself lives in `PickupManager` (see [architecture.md](architecture.md)).

### Runtime room actions

`AddRewardRoom` and `AddThreatRoom` share `AddRoomAction`, which opens a new room through the
`DungeonMutator` (see [dungeon-generation.md](dungeon-generation.md)). Both are limited by
`MaxRoomsAddedPerSession` and report a rejection without spending points when no room can be
carved.

- **Candidate search** — `DungeonRoomPlacement.FindCandidates` anchors a square room of
  `RoomSideMin`–`RoomSideMax` tiles so the midpoint of one side touches an existing floor tile.
  The area must stay one tile inside the dungeon bounds, contain no floor or entity cell, and its
  centre must be at least `MinDistanceFromPlayer` tiles from the player. The search is
  deterministic: floors, sides and sizes are enumerated in a fixed order.
- **Room contents** — `AddRewardRoom` validates that `PickupManager.HealthPackScene` is assigned,
  then spawns one pack at the room centre. `AddThreatRoom` spawns `EnemiesPerRoom` enemies on
  sorted floor cells inside the room and records their difficulty value through
  `DataManager.RegisterDirectorSpawn`.
- **Validation** — `AddRoomAction` asks the mutator for a refreshed model, so player and living
  enemy cells are never covered, and tries at most `MaxCandidateAttempts` candidates. A failed
  mutation leaves the dungeon untouched.
- **Telemetry** — every created room emits `SignalManager.DungeonRoomCreated(kind, tilePosition,
  tileSize, entityCount)` for the experiment log.

`MaxRoomsAddedPerSession`, `RoomSideMin`, `RoomSideMax`, `MinDistanceFromPlayer` and
`MaxCandidateAttempts` are exported on the actions; `EnemiesPerRoom` is exported on
`AddThreatRoom`.

## Tuning parameters

All parameters below are exported on `Director` and can be changed in the inspector.

| Parameter | Default | Meaning |
| --- | --- | --- |
| `CheckInterval` | 1.0 s | Evaluation period |
| `CpBaseline` | 0.5 | Neutral Cp |
| `KillBonus` | 0.3 | Maximum kill contribution |
| `DamagePenalty` | 0.3 | Maximum damage penalty |
| `HealthSafetyThreshold` | 0.5 | HP% below which penalties start |
| `LowHealthMultiplier` | 2.0 | Damage-penalty amplification at 0 HP |
| `LowHealthPenalty` | 0.3 | Direct Cp penalty at 0 HP |
| `ExpectedKillsPer15s` | 5.0 | Normalisation constant for kills |
| `MaxDamagePer15s` | 20.0 | Normalisation constant for damage |
| `ReduceThreshold` | 0.8 | Lower Flow boundary |
| `IntensifyThreshold` | 1.2 | Upper Flow boundary |
| `FlailingHPDropThreshold` | 30.0 | HP loss in 5 s that counts as flailing |
| `FlailingHealthThreshold` | 0.3 | HP% below which flailing can trigger |
| `BasePointsPerSecond` | 1.0 | Points at the threshold edge |
| `MaxPointsMultiplier` | 4.0 | Points multiplier at the extremes |
| `RatioSmoothingFactor` | 0.1 | Exponential smoothing factor |
| `SelectionSeed` | 0 | Seed for the rule policy's random source; `0` uses the global RNG |

## Observation and debugging

The Director emits `PerformanceMetricsUpdated` with `Ep`, `Cp`, smoothed ratio, both point pools,
kill rate, damage rate and a flailing flag. `DebugUi` renders those values together with the
current state, session progress, difficulty, spawn interval and the recent action log.

`DirectorAction.Execute()` emits `DirectorActionExecuted` with the action name and cost; this is
also shown in the debug log.

## Limitations and possible extensions

- `Cp` is a hand-tuned heuristic, not a learned player-skill model.
- All enemies share the same buff/debuff treatment; there is no per-enemy targeting.
- The expected-performance curve is authored, not generated from telemetry.
- The Director acts on aggregate rates; it does not model individual encounters.
- The session is a fixed 120-second run; meta-progression is out of scope.

Future work for the master's thesis can compare this rule-based Director against learned or
player-modelled policies, and can log telemetry for offline evaluation.
