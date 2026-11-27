# Testing

## Framework

| Package | Version | Purpose |
| --- | --- | --- |
| `gdUnit4.api` | 5.1.0-rc7 | Test API, assertions, scene runner |
| `gdUnit4.test.adapter` | 3.2.0 | VSTest integration (`dotnet test`, VS Code, Rider) |
| `gdUnit4.analyzers` | 1.1.0 | Compile-time validation of test attributes |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | VSTest host |

GdUnit4 5.x runs logic-only tests without the Godot runtime. Tests that touch nodes or Godot APIs
are marked `[RequireGodotRuntime]` and are executed inside a headless Godot process.

## Running the tests

```powershell
# Point the adapter at a Godot 4.7.2 .NET binary
$env:GODOT_BIN = 'C:\Godot\4.7.2mono\Godot_v4.7.2-stable_mono_win64.exe'

# Import once so the project cache and UID map exist
godot --headless --path . --import

# Run the suite
dotnet test "project kitchen.sln" --settings .runsettings
```

`.runsettings` already contains a default `GODOT_BIN` for this machine. The CI file
`.github/ci.runsettings` intentionally omits it and reads the environment variable instead.

## Test suites

All tests live in `Tests/` and total **194 tests**.

| Suite | Tests | Coverage |
| --- | --- | --- |
| `TimeBufferTest` | 12 | Sliding-window buffers, expiration, edge values |
| `PerformanceDataTest` | 17 | Accuracy, damage/kill rates, HP-drop tracking, spawn values |
| `FlailingDetectionTest` | 11 | HP-drop accumulation, threshold edges, healing vs. damage |
| `DirectorPointsCalculationTest` | 19 | Points multiplier across both zones, benefit/negative spending, defaults |
| `DirectorActionTest` | 12 | Cost, weight, cooldown and execution contract |
| `DirectorActionExecutionTest` | 7 | Success/failure cost accounting, cooldown only on success |
| `SpawnHealthPackActionTest` | 2 | Action scene wiring and the no-placement failure path |
| `DifficultyStateMachineTest` | 4 | State enum completeness |
| `ActionSelectionTest` | 8 | Type/cost/cooldown filtering, weighted selection distribution |
| `ActionPolicyTest` | 11 | Policy selection, weight buckets, seeded replay, context snapshot |
| `HealthPackPlacementTest` | 9 | Distance ring edges, player-cell exclusion, candidate filtering, seeded selection |
| `AutoSpawnSchedulerTest` | 5 | Active-pack cap, spawn interval, first-spawn delay |
| `HealthPackPickupTest` | 3 | Runtime pickup heals once, clamps at max HP, never damages the player |
| `DungeonTopologyTest` | 13 | Reachability, bridges, corridor cross-sections, closability |
| `DungeonWorldModelTest` | 12 | Room creation rejection paths, passage close/reopen, rollback, determinism |
| `DungeonRoomPlacementTest` | 5 | Runtime room candidates: adjacency, free space, player distance, bounds, determinism |
| `AddRoomActionTest` | 4 | Reward/threat room action wiring, defaults and the session limit |
| `DungeonPassageSelectionTest` | 4 | Player-room exit exclusion, enemy stranding, alternate routes |
| `ClosePassageActionTest` | 3 | Close-passage wiring, defaults and the session limit |
| `BotSkillProfileTest` | 4 | Profile mapping, ordering, sandbag degradation and the 150 s window |
| `CalibrationOptionsTest` | 4 | Calibration command-line parsing and defaults |
| `SimulationBotIsolationTest` | 1 | The bot sources never reference Director internals |
| `LlmValidatorTest` | 7 | Response schema, catalog membership, candidate eligibility, cost and cooldown |
| `LlmPolicyTest` | 11 | Cadence, deadline metric, retries, failure flag, arm B vs arm C, token/cost accounting |
| `LlmTelemetryTest` | 5 | JSONL decisions, legal rate, 5 % flag, sample cap, session summary |
| `DungeonMutatorWiringTest` | 1 | Mutator node present in the dungeon scene |

The weighted-selection tests are statistical: they assert broad distribution properties rather
than exact draws, so they remain deterministic enough for CI.

The LLM suites use `FakeLlmClient` and a fake clock. CI never calls a provider and needs no API
key; see [llm-policies.md](llm-policies.md).

## CI execution

The `Build & Test` job in `.github/workflows/ci.yml`:

1. installs .NET 8 and Godot 4.7.2 (with .NET support),
2. imports the project,
3. builds `project kitchen.sln`,
4. runs `dotnet test --settings .github/ci.runsettings`,
5. uploads the TRX file as the `test-results` artifact.

The job name is part of the required branch-protection checks.

## Adding tests

- Put new suites in `Tests/<Area>/` and add `[TestSuite]`.
- Use `[TestCase]` for test methods and `[RequireGodotRuntime]` only when Godot types are needed.
- Free any `Node` instances you create in `[AfterTest]` (`node.Free()`), otherwise GdUnit4 reports
  orphan nodes.
- Prefer testing behaviour through public APIs; private helpers are covered indirectly (for
  example `TimeBuffer` is tested through `PerformanceData`).

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| `Could not load file or assembly 'System.Collections.Immutable, Version=9.0.0.0'` | GdUnit4 5.1.0-rc7 needs the 9.x package on `net8.0`; the project pins `System.Collections.Immutable` 9.0.0. |
| `No test is available` | `GODOT_BIN` is missing or the project was never imported. Run the import step first. |
| Orphan-node warnings | Tests create nodes and free them during teardown; warnings are informational and do not fail the run. |
| `Html Logger Error: hexadecimal value 0x1B` | The local HTML logger cannot encode an ANSI escape emitted by a test. The test run still passes; CI uses the TRX logger only. |
| Tests are skipped | `[RequireGodotRuntime]` tests are skipped when the configured Godot binary has no C# support. |
| `gdunit4_testadapter_v5` appears in the worktree | Generated by the adapter; it is ignored by `.gitignore`. |
