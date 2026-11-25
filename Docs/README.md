# AI Director Demo — Documentation

This directory contains the technical documentation for **AI Director Demo**, a 2D top-down
shooter built with **Godot 4.7.2 (.NET)** and **C#**. The game is the implementation part of an
engineering thesis on using AI for **Dynamic Difficulty Adjustment (DDA)** and is now the
foundation for further work in the master's thesis.

The system keeps the player in the **Flow** zone described by Csíkszentmihályi: it measures
player performance, compares it with a designer-authored difficulty curve and applies
gameplay interventions in real time.

## Documents

| Document | What it covers |
| --- | --- |
| [architecture.md](architecture.md) | Runtime structure, autoloads, signals, entities and lifecycle |
| [ai-director.md](ai-director.md) | Performance ratio, states, points economy and Director actions |
| [dungeon-generation.md](dungeon-generation.md) | Procedural dungeon pipeline and rendering |
| [testing.md](testing.md) | GdUnit4 suites, commands and CI test execution |
| [development.md](development.md) | Local setup, secrets, local-only files and workflows |
| [ci-cd.md](ci-cd.md) | GitHub Actions, exports, artifacts and releases |
| [agent-workflow.md](agent-workflow.md) | Issue → branch → PR workflow and repository rules |

## Project at a glance

| Item | Value |
| --- | --- |
| Engine | Godot 4.7.2 (.NET, Forward+) |
| Runtime | .NET 8, C# 12 |
| Test framework | GdUnit4 5.1.0-rc7 (API), 3.2.0 (adapter), 1.1.0 (analyzers) |
| Unit tests | 83 tests in `Tests/AIDirector` |
| CI | Build + tests on pull requests; Windows/Linux/macOS exports on `main`; releases on `v*` tags |
| Main scene | `res://Src/Level/Level.tscn` |

## Quick start

```powershell
# 1. Import the Godot project (Godot 4.7.2 .NET must be installed)
godot --headless --path . --import

# 2. Build and run the game
dotnet build "project kitchen.sln"
godot --path .

# 3. Run the unit tests
$env:GODOT_BIN = 'C:\Godot\4.7.2mono\Godot_v4.7.2-stable_mono_win64.exe'
dotnet test "project kitchen.sln" --settings .runsettings
```

See [development.md](development.md) for the complete setup, including local-only tooling.

## Reading order

If you are new to the project:

1. [architecture.md](architecture.md) — how the runtime fits together.
2. [ai-director.md](ai-director.md) — the DDA algorithm and its parameters.
3. [dungeon-generation.md](dungeon-generation.md) — how levels are produced.
4. [testing.md](testing.md) — what is covered by tests.
5. [development.md](development.md) and [ci-cd.md](ci-cd.md) — how to build, test and ship.
6. [agent-workflow.md](agent-workflow.md) — contribution and automation rules.
