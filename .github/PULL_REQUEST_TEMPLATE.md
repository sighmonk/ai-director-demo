## Summary

<!-- What changed and why? Keep it focused on the linked issue. -->

Closes #

## Verification

- [ ] `dotnet build "project kitchen.sln"`
- [ ] `dotnet test "project kitchen.sln" --settings .runsettings`
- [ ] Godot import checked when scenes, resources or settings changed
- [ ] Export checked when export presets or project settings changed
- [ ] All CI checks are green

## Checklist

- [ ] The change is limited to the linked issue
- [ ] Tests were added or updated for new behaviour
- [ ] Documentation in `Docs/` was updated when needed
- [ ] Godot-owned files (`*.tscn`, `*.tres`, `project.godot`, `*.cfg`, `*.import`, `.godot/`) were changed only through the editor/MCP, or editor steps were given to the user
- [ ] All new text (commits, PR, docs, code comments) is in English
- [ ] No secrets, tokens or local-only files are included
- [ ] No generated output (`Builds/`, `.godot/`, `TestResults/`, `gdunit4_testadapter_*/`) is included
- [ ] Commit identity is the repository owner; there is no Copilot/agent attribution and no Copilot review request
