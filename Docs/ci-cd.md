# CI/CD

All automation lives in `.github/workflows/ci.yml`.

## Triggers

| Event | What runs |
| --- | --- |
| `pull_request` → `main` | Build & Test, then the three export jobs |
| `push` → `main` | Build & Test, three export jobs, workflow artifacts and a build prerelease |
| `push` tag `v*` | Build & Test, three export jobs and a stable GitHub Release |
| `workflow_dispatch` | Manual run of all jobs (release skipped) |

Concurrency cancels superseded runs on the same branch, but not on tags.

## Jobs

### `Build & Test` (`ubuntu-latest`)

1. Checkout.
2. Install .NET 8 and Godot 4.7.2 with .NET support.
3. Resolve the Godot binary and export `GODOT_BIN`.
4. Restore NuGet packages (cached).
5. Import the Godot project headlessly.
6. Build the solution.
7. Run `dotnet test` with `.github/ci.runsettings`.
8. Upload `test-results` (TRX).

### `Export (windows|linux|macos)`

Each matrix job runs on its own runner:

| Platform | Runner | Preset | Output | Artifact |
| --- | --- | --- | --- | --- |
| Windows | `windows-latest` | `Windows Desktop` | `Builds/windows/AIDirector.exe` | `ai-director-windows-x86_64` |
| Linux | `ubuntu-latest` | `Linux` | `Builds/linux/AIDirector.x86_64` | `ai-director-linux-x86_64` |
| macOS | `macos-latest` | `macOS` | `Builds/macos/AI Director Demo.app` | `ai-director-macos-universal` |

Key details:

- Godot 4.7.2 export templates are installed by the setup action.
- On Windows the workflow resolves the **console** executable (`*_console.exe`). The GUI
  executable detaches immediately and would return before the export finishes.
- `UseSharedCompilation=false` and `MSBUILDDISABLENODEREUSE=1` prevent the Roslyn build server
  from keeping the Windows console process alive after packing
  ([godotengine/godot#110101](https://github.com/godotengine/godot/issues/110101)).
- macOS exports a universal application and is ad-hoc signed (`codesign --sign -`) before zipping.
- The macOS artifact is created with `ditto`, Linux with `zip` and Windows with
  `Compress-Archive`, preserving executable permissions where required.
- Artifacts are kept for 14 days.

### `Release`

Runs only for `v*` tags, downloads all three artifacts and runs `gh release create` with generated
notes. The job has `contents: write`; every other job is read-only.

## Branch protection

`main` requires:

- a pull request,
- the checks `Build & Test`, `Export (windows)`, `Export (linux)` and `Export (macos)`,
- branches to be up to date before merging,
- conversation resolution,
- no force pushes and no branch deletion.

No approval is required and Copilot review is not requested. Administrators may bypass protection
for emergency fixes (`enforce_admins: false`).

## Releases

Every push to `main` publishes a **prerelease build**:

- tag `build-<run>-<attempt>`, created at the pushed commit,
- title `Build <run>.<attempt> (<short sha>)`,
- the three platform zips,
- automatically generated notes since the previous **stable** release.

Version tags publish a stable release:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The tag triggers the full pipeline and creates a stable GitHub Release with the same three zips
and generated notes since the previous stable release. Build prereleases are excluded when
choosing the changelog start point, so a stable release covers all changes since the last version.

The release job is the only job with `contents: write`. It uses the short-lived `GITHUB_TOKEN`.
Do not delete build tags or releases manually unless you are cleaning up a failed run.

## Artifacts

Every run still uploads workflow artifacts for debugging. Releases are the primary distribution
channel. Download a release asset from the Releases page or with the CLI:

```powershell
gh release list --limit 5
gh release download <tag> --pattern "ai-director-*.zip"
```

The exported bundles include the .NET runtime configuration and the game assemblies. Because the
tests live in the same project as the game, test-related assemblies are currently published too;
splitting the test project is a possible future optimisation.

## Local reproduction

The same steps work locally:

```powershell
dotnet restore "project kitchen.sln"
godot --headless --path . --import
dotnet build "project kitchen.sln" -c Debug --no-restore
$env:GODOT_BIN = 'C:\Godot\4.7.2mono\Godot_v4.7.2-stable_mono_win64.exe'
dotnet test "project kitchen.sln" -c Debug --no-build --settings .github/ci.runsettings
```

For exports, see [development.md](development.md). A clean checkout without `Directory.Build.props`
and without `addons/godot_mcp` is the closest match to CI.

## Security notes

- No repository secrets are required for build, test or export.
- The release job uses the short-lived `GITHUB_TOKEN`.
- Workflow permissions are read-only by default and elevated only for releases.
- Do not add secrets to workflow files; pass them through GitHub Actions secrets if a future step
  needs them.
