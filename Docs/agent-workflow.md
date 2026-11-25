# Agent and Contribution Workflow

This repository is developed with AI agents, but every change follows the same reviewable path as
human work: **issue → branch → pull request → green checks → merge**.

## 1. Create an issue first

Every change starts with a GitHub issue that states the goal, the scope and the acceptance
criteria. Use the templates in `.github/ISSUE_TEMPLATE/`:

- bug report,
- feature request,
- maintenance task.

Labels: `bug`, `enhancement`, `documentation`, `chore`, `ci`, `security`.

## 2. Branch from `main`

Name branches after the issue:

```text
<type>/<issue-number>-<short-slug>
```

Examples:

```text
feat/42-player-dash
fix/57-flailing-threshold
docs/63-agent-workflow
ci/71-export-cache
```

Do not push directly to `main`; branch protection rejects it.

## 3. Make the change

- Keep the change focused on the issue.
- Add or update tests for behaviour that changes.
- Update the relevant document in `Docs/` when architecture, tuning or workflows change.
- Never commit secrets, local configuration or generated build output.
- Follow the conventions in [development.md](development.md).

## 4. Commit identity

Commits must be authored and committed by the repository owner's configured identity. Agents must
not impersonate the owner or add bot attribution.

**Never:**

- commit or push as `copilot`, `copilot-swe-agent`, or another automation account,
- add `Co-authored-by: Copilot <...>`,
- add “Generated with Copilot” or similar text to commits or PR descriptions.

Before committing, verify:

```powershell
git config user.name
git config user.email
```

The commit author and committer must match the owner's identity, and the commit must not contain a
Copilot trailer.

## 5. Open a pull request

The PR description should include:

- a short summary,
- the verification performed (commands and results),
- documentation updates,
- `Closes #<issue>`.

Use the pull request template. Do not request a Copilot review.

## 6. Wait for checks

Required checks on `main`:

- `Build & Test`
- `Export (windows)`
- `Export (linux)`
- `Export (macos)`

A PR is mergeable only when all checks are green and the branch is up to date with `main`.

## 7. Merge and clean up

- Merge with **squash** so `main` keeps one commit per change.
- Delete the branch after merging.
- Confirm the issue closed automatically through `Closes #<issue>`.
- Verify the follow-up `main` run produced the three platform artifacts.

## Secrets and local-only files

This is a public repository. The following must never be committed:

- `.env` and any `.env.*` file except `.env.example`,
- `.vscode/mcp.json` (use `.vscode/mcp.example.json`),
- `addons/godot_mcp/`,
- `Directory.Build.props`,
- `.github/copilot-instructions.md` (private agent instructions),
- `Builds/`, `.godot/`, `TestResults/`, `gdunit4_testadapter_*/`.

If a secret is committed by accident, stop, rotate it and ask before rewriting history.

## Documentation upkeep

Update documentation in the same PR as the behaviour change:

| Change | Document |
| --- | --- |
| Director metric, state, action or tuning | [ai-director.md](ai-director.md) |
| Dungeon stage or generator parameter | [dungeon-generation.md](dungeon-generation.md) |
| Scene, manager, signal or entity | [architecture.md](architecture.md) |
| Test suite or test command | [testing.md](testing.md) |
| Build, local setup or export | [development.md](development.md) |
| Workflow, check or release | [ci-cd.md](ci-cd.md) |
| Agent/contribution rules | this file |

## Private agent instructions

`.github/copilot-instructions.md` is intentionally gitignored. It contains project-specific
instructions for local AI agents. Keep it up to date when conventions change, but never force-add
it to git.
