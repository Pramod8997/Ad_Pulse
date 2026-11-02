# Ponytail setup for OmniRush

## Source and revision

- Official repository: https://github.com/DietrichGebert/ponytail
- Source checkout used: `main` at revision `96608e078b2d5ea607c5c3b8b5a54d1c1fba6846`
- Upstream package version at that revision: `@dietrichgebert/ponytail` `5.3.3`
- Official skill SHA-256: `F18008471528F0ADDE90A560D053F85935EED5CEE26C447B7177AD24472BCF32`

## Installation method

The installed runtime is OmniRush `2.7.2` (agent core `v0.86.1`, bundled Bun `1.4.2`). The standalone `opencode` executable is not installed, and OmniRush exposes no `plugin add` or `plugin install` command. `omnirush doctor` also reported that the manager endpoint was unreachable during setup.

The supported, verifiable fallback was therefore used:

1. Copy the official upstream `skills/ponytail/SKILL.md` unchanged to `.opencode/skills/ponytail/SKILL.md`.
2. Keep the AdPulse rules in the root `AGENTS.md`, adding the full/ultra operating mode and the shared 20-role subagent contract without replacing project constraints.
3. Do not add an unverified npm plugin configuration. The OpenCode plugin remains available for a later OpenCode-compatible installation:

   ```json
   { "plugins": ["@dietrichgebert/ponytail"] }
   ```

   That entry is intentionally not present because this environment could not install or load the package through OmniRush.

## Discovery and inheritance

OmniRush's installed engine source was inspected and confirms that project skills are discovered from `.opencode/skills/<name>/SKILL.md`. It also inspects project OpenCode plugin configuration, but no working OmniRush plugin-install path was available here. The root `AGENTS.md` is the always-on project instruction source, and the agent prompts explicitly reference both it and the workspace skill for subagents that do not inherit skills automatically.

## Modes and upstream commands

The upstream Ponytail implementation supports `lite`, `full`, `ultra` and `off`; `full` is the default. `ultra` is required for architectural decisions, major refactoring, database redesign, dependency selection, security reviews and complex features in AdPulse.

The upstream OpenCode plugin defines:

- `/ponytail`
- `/ponytail-review`
- `/ponytail-audit`
- `/ponytail-debt`
- `/ponytail-gain`
- `/ponytail-help`

Its bundled skill directories are `ponytail`, `ponytail-review`, `ponytail-audit`, `ponytail-debt`, `ponytail-gain` and `ponytail-help`. Only the primary `ponytail` skill is installed in the workspace fallback; the other five remain plugin-provided and are not claimed as OmniRush-discoverable here.

These commands were inspected in the upstream `.opencode/command/` files but are **not claimed as active in OmniRush** because the plugin was not loaded. The workspace skill and `AGENTS.md` fallback are active project files; plugin lifecycle injection and command registration remain unverified.

## Verification performed

Commands and checks actually run:

- `omnirush --version` → `2.7.2`.
- `omnirush doctor` → auth, agent config, agent core, runtime, launcher and local tools OK; manager endpoint timed out; update available to `2.7.8`.
- `omnirush agents --help` → native worktree subagent mechanism is available; `omnirush agents start "Read-only verification..."` then failed as expected because this workspace has no `.git` repository, so runtime subagent inheritance could not be exercised.
- `omnirush assist status` → self-check, project-notes, diagnostics and compaction are on.
- `git status --short --branch` → failed with “not a git repository”; no Git metadata exists in this workspace, so no pre-existing Git changes could be enumerated or overwritten.
- Upstream `git clone --depth 1` → succeeded; revision recorded above.
- Source and installed skill SHA-256 → identical.
- Frontmatter structure → `SKILL.md` starts and ends with the required YAML document delimiters and contains the upstream `name`, `description`, `argument-hint` and `license` fields.

Fresh-process discovery verification used `omnirush --no-sandbox --permissions auto -p` with a read-only prompt. OmniRush reported `AGENTS.md` visible, `.opencode/skills/ponytail/SKILL.md` discoverable with declared name `ponytail`, and no visible plugin or command registration. Slash-command support was reported as unverified. No plugin activation or slash-command execution was asserted.

## Files changed

- Added `.opencode/skills/ponytail/SKILL.md` (official upstream content).
- Updated `AGENTS.md` with mode selection and the shared subagent contract.
- Updated `16-agent-prompts.md` so all 20 logical roles use the shared Ponytail sources and report unsupported host features truthfully.
- Added this document.

## Known limitations and next step

- No standalone `opencode` binary was available, so `opencode plugin add @dietrichgebert/ponytail` was not run.
- OmniRush's current CLI does not expose a documented project plugin installer, and its manager was unreachable during verification.
- `/ponytail`, `/ponytail-help`, `/ponytail-review` and `/ponytail-audit` were not reported as working; they require a verified OpenCode plugin load or a future OmniRush integration.
- This folder is not a Git repository, so there is no Git diff/stat or commit history to report. Existing files were preserved.

When a supported OpenCode-compatible runtime is available, install `@dietrichgebert/ponytail`, add its documented `plugins` entry without removing existing entries, restart the project session, and re-run the command and subagent checks above.
