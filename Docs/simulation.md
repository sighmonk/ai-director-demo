# Simulated players

The master's thesis experiment replaces the human player with a deterministic bot so every arm can
be replayed from a seed. The bot shares one policy; the skill profiles only change its parameters.

## Input abstraction

`Player` no longer reads the keyboard directly. It asks an `IPlayerInput` for movement, an aim point
and a one-frame shoot request:

| Type | Source |
| --- | --- |
| `DevicePlayerInput` | `Input.GetVector("Move*")`, `Input.IsActionJustPressed("PlayerShoot")` and the mouse position — the default, so normal gameplay is unchanged |
| `ScriptedPlayerInput` | Written by `SimulationBot` through `Player.InputSource` |

`Player.InputSource` is settable at any time; the bot binds it right after the player is spawned.

## Simulation bot

`SimulationBot` runs in `_PhysicsProcess`:

- **Perception** — the nearest living enemy within `PerceptionRadius` (700 px) is the target.
- **Reaction** — target, aim error and line of sight are refreshed every `ReactionTime`; the aim
  error is drawn from a seeded `RandomNumberGenerator`.
- **Navigation** — the game's `EnemyManager.GetPath` A* path is rebuilt from the current cell and
  followed cell by cell, so the 600 px/s player cannot overshoot a 16 px waypoint. A per-frame
  open-direction probe keeps `MoveAndSlide` out of wall corners.
- **Distance keeping** — inside `PreferredDistanceMin..Max` the bot holds or strafes; closer than
  the minimum it backs off, farther away it follows the path.
- **Crowd avoidance** — enemies inside `ThreatRadius` produce a repulsion vector; an enemy inside
  `DangerRadius` makes the retreat override target keeping.
- **Firing** — one shot per `FireInterval` when the target has a clear line of sight; aiming is
  offset by the current aim error.
- **Recovery** — lack of progress for 1.5 s (or 0.5 s without movement) triggers a committed escape
  step; a player centre inside a wall cell is snapped back to the nearest floor cell.

The bot reads only public gameplay state (`EnemyManager`, `GameManager`, the player node). It never
touches `DirectorData` or Director internals; `SimulationBotIsolationTest` guards the sources.

### Profiles

| Profile | Reaction | Aim error | Distance band | Fire interval | Speed | Danger radius |
| --- | --- | --- | --- | --- | --- | --- |
| `novice` | 0.80 s | 8.0° | 0–100 px | 0.55 s | 0.55 × | 10 px |
| `average` | 0.55 s | 6.0° | 0–170 px | 0.40 s | 0.65 × | 90 px |
| `expert` | 0.30 s | 4.5° | 0–215 px | 0.30 s | 0.70 × | 120 px |
| `sandbagger` | expert values; `Degrade` for the first 150 s | | | | | |

`Degrade` slows reactions, widens aim error, pulls the band to 120–190 px, slows the trigger and
reduces crowd avoidance. The sandbagger therefore plays below its skill until
`SandbagUntilSeconds` (150 s) and at expert level afterwards.

## Determinism

- Calibration runs headless with `--fixed-fps 60`, i.e. a fixed 1/60 physics step without real-time
  synchronisation.
- Seeds: dungeon (`DungeonGenerator.Seed`), enemy spawning (`EnemyManager.Seed`), pickups
  (`PickupManager.Seed`), bot aim (`SimulationBot.Bind(seed)`) and the Director policy
  (`Director.SelectionSeed`).
- One session runs in its own Godot process, so autoload state never leaks between seeds.

## Static arm S

`Director.Enabled` (default `true`) disables evaluation and action execution while keeping the data
registration. A calibration run parses `--calibration-*` from the command line, forces the Director
off and sets `GameManager.SessionDuration` to the requested length. This is the static arm: the
difficulty curve still paces enemy spawning, but the Director never intervenes.

## Calibration

```powershell
# Full matrix: four profiles, 25 seeds, 600 s per session
pwsh Tools/calibrate.ps1 -SeedCount 25 -SessionSeconds 600

# Quick smoke run
pwsh Tools/calibrate.ps1 -Profiles novice,expert -SeedCount 3 -SessionSeconds 120
```

Each session runs one Godot process:

```text
godot --headless --path . --fixed-fps 60 -- \
  --calibration-profile=novice --calibration-seed=1 \
  --calibration-seconds=600 --calibration-out=TestResults/calibration/novice-1.jsonl
```

`CalibrationSession` binds the bot to the spawned player, writes one JSONL record on death or at the
session limit, and quits. The script aggregates the records into
[calibration.md](calibration.md); raw JSONL and per-run logs stay in `TestResults/` (gitignored).

Target: novice 90–140 s and expert 380–450 s survival on the static arm, with monotonic medians.

## Limitations

- The bot is not a player model: claims from the experiment concern regulator properties, not human
  experience.
- The profiles share one policy, so the calibration only validates that the parameter sets are
  ordered, not that they represent different play styles.
- Godot exits with an access violation during finalisers on some runs after writing the record;
  `Tools/calibrate.ps1` accepts a non-zero exit code when the JSONL record exists and warns.
