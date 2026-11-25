# Development

## Prerequisites

- **Godot 4.7.2 (.NET / mono)** — the standard build has no C# support.
- **.NET SDK 8.0.100+** — `dotnet --info` must list an 8.0 SDK.
- **Git** and, optionally, the GitHub CLI (`gh`).

## First run

```powershell
# Restore .NET packages
dotnet restore "project kitchen.sln"

# Import assets and build the C# assembly
godot --headless --path . --import
dotnet build "project kitchen.sln"

# Open the game
godot --path .
```

Controls: **WASD** to move, **left mouse** to shoot, **scroll wheel** to zoom the camera.

## Common commands

| Task | Command |
| --- | --- |
| Build | `dotnet build "project kitchen.sln"` |
| Run | `godot --path .` |
| Import assets | `godot --headless --path . --import` |
| Test | `dotnet test "project kitchen.sln" --settings .runsettings` |
| Export Windows | `godot --headless --path . --export-release "Windows Desktop" "Builds/windows/AIDirector.exe"` |
| Export Linux | `godot --headless --path . --export-release "Linux" "Builds/linux/AIDirector.x86_64"` |
| Export macOS | `godot --headless --path . --export-release "macOS" "Builds/macos/AI Director Demo.app"` |

The export commands need the Godot 4.7.2 **.NET export templates** installed. Exports are written
to `Builds/`, which is ignored by git.

## Local-only files

This repository is public. Several files are deliberately kept out of git because they contain
machine-specific paths, tokens or third-party editor tooling:

| Path | Why it is local |
| --- | --- |
| `.env` | Godot-MCP connection settings and bearer token |
| `.vscode/settings.json` | Absolute path to the local Godot binary |
| `.vscode/mcp.json` | Local MCP server URL and token |
| `addons/godot_mcp/` | Third-party editor plugin used for AI tooling |
| `Directory.Build.props` | Adds NuGet packages required only by the MCP addon |
| `.ai-game-dev/` | Local agent scratch space |
| `.github/copilot-instructions.md` | Private agent instructions |
| `Builds/`, `.godot/`, `TestResults/`, `gdunit4_testadapter_*/` | Generated output |

Safe templates are committed:

- `.env.example` — placeholder variables.
- `.vscode/mcp.example.json` — placeholder MCP configuration.

**Never `git add -f` a local-only file.** If a new tool needs local configuration, add an example
file and extend `.gitignore`.

## Optional: Godot-MCP setup

The project can be driven by the Godot-MCP editor plugin for AI-assisted development. This is
optional and completely local:

1. Install the plugin into `addons/godot_mcp/`.
2. Copy `.env.example` to `.env` and fill in the connection values.
3. Copy `.vscode/mcp.example.json` to `.vscode/mcp.json` and fill in the URL/token.
4. Enable the plugin in Godot (this rewrites the `[editor_plugins]` entry in `project.godot`).
5. Keep `Directory.Build.props` in place so the plugin's NuGet dependencies resolve.

A clean checkout without the plugin still builds, tests and exports normally.

## Secrets policy

- Tokens, keys and passwords never belong in tracked files or commit messages.
- `.env*` is ignored except `.env.example`.
- MCP configuration is ignored except the example file.
- Before opening a PR, run `git status` and confirm only intended files are staged.
- If a secret is ever committed, stop and rotate it before rewriting history.

## Conventions

- C# classes live in the global namespace; public members use `PascalCase`, private fields use
  `_camelCase`.
- Tuning values are exposed with `[Export]` and edited in the inspector or `.tscn` files, not
  hard-coded in `_Process`.
- Cross-system communication goes through `SignalManager`; avoid reaching into another scene's
  nodes by absolute path.
- Managers are autoload singletons with an `Instance` property.
- New behaviour needs a focused GdUnit4 test when it can be tested without a scene.

### Adding a Director action

1. Create a script in `Src/AI Director/Actions/` extending `DirectorAction`.
2. Implement `OnExecute()` and set `ActionType`, `Cost`, `Weight`, `Cooldown` (defaults are
   exported).
3. Add a node under `Director/Actions` in `Director.tscn` and attach the script.
4. Add or extend a test in `Tests/AIDirector`.
5. Document the action in [ai-director.md](ai-director.md).

### Adding an enemy type

1. Extend `Enemy` (or create a new `Area2D` with the shared components).
2. Create a scene with `HealthComponent` and `HitboxComponent`.
3. Add the scene to `EnemyManager.EnemyScenes`.
4. Keep `DifficultyValue` meaningful — it is used by the Director's spawn accounting.

## Export troubleshooting

| Symptom | Fix |
| --- | --- |
| `No export template found` | Install the Godot 4.7.2 .NET export templates. |
| macOS export complains about **ETC2 ASTC** | The project enables `rendering/textures/vram_compression/import_etc2_astc`; reimport after changing it. |
| macOS refuses to open the app | The CI build is ad-hoc signed; locally exported apps are unsigned. Run `xattr -dr com.apple.quarantine "AI Director Demo.app"` on the target Mac. |
| Windows console export hangs after packing | Known Godot issue #110101 — the Roslyn build server keeps the console process alive. Set `UseSharedCompilation=false` for the export command (CI does this). |
| `_stateNodes` property warning during headless import | Benign Godot scene-conversion warning; it does not block import or export. |
