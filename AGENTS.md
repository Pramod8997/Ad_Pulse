# AdPulse Agent Instructions — Ponytail-aligned

Ponytail is the governing coding approach. Read the actual skill when available: https://github.com/DietrichGebert/ponytail/blob/main/skills/ponytail/SKILL.md. This file is a compact project-specific fallback, not a claim that the plugin has been installed.

## Ponytail operating mode

- The official workspace skill is `.opencode/skills/ponytail/SKILL.md`, sourced from https://github.com/DietrichGebert/ponytail.
- Use Ponytail **full** as the default development mode: smallest complete, tested implementation.
- Use **ultra** for architectural decisions, major refactoring, database redesign, dependency selection, security reviews and complex features.
- Use **lite** only when the task explicitly calls for minimal scope; use **off** only when explicitly requested. Never change modes silently.
- If the host exposes Ponytail commands, `/ponytail full`, `/ponytail ultra`, `/ponytail-review` and `/ponytail-audit` are the supported forms. If it does not, follow this file and the workspace skill without claiming those commands are enabled.

## Core behavior

Be efficient, not careless. Understand the task and trace the real call path before editing. Look for existing helpers, installed dependencies, native .NET/SQL/browser features and existing UI components before writing new ones. Prefer the smallest correct diff. Fix root causes, not only the reported symptom.

## Ponytail decision ladder

1. Does this need to exist? Skip speculative scope (YAGNI).
2. Does the repository already solve it? Reuse the existing implementation.
3. Can the standard library do it?
4. Can a native platform feature do it?
5. Can an already-installed dependency do it?
6. Is a one-line/simple implementation correct for edge cases?
7. Otherwise build the minimum complete solution.

Do not add an abstraction with one implementation, a factory for one product, configuration for an immutable value, scaffolding “for later”, or a dependency where a small native implementation suffices.

## Never simplify away

- Authentication, authorization or tenant isolation.
- Validation at trust boundaries.
- Error handling that prevents data loss.
- Accessibility basics and keyboard operation.
- Tests for non-trivial branching, loops, parsing, money calculations and security-sensitive paths.
- Observability needed to investigate failures, with secrets redacted.

## AdPulse non-negotiables

- Derive tenant scope from validated authentication and server-side membership, never from user-controlled request values.
- Tenant-scope every read/write, including exports, imports, nested resources, background jobs, scoring and alert lifecycle actions.
- SQL Server is the source of truth; Python scoring is stateless and never grants authorization.
- Use decimal semantics for money and explicit null behavior for zero denominators.
- Make metric ingestion and scoring idempotent.
- Never expose SQL, stack traces, tokens, secrets or raw uploaded rows to users/logs.
- Never add Kafka, Redis, Kubernetes or speculative microservices without measurable justification.

## Simplification markers

Use `ponytail:` comments only for a deliberate simplification with a real known ceiling and a concrete upgrade path, for example:

```csharp
// ponytail: single-process scheduler is sufficient for the current one-instance deployment;
// replace with a distributed lease only if multiple API replicas run scoring concurrently.
```

Do not use these comments to excuse missing security or tests. Keep a debt ledger in `docs/ponytail-debt.md` for all such comments.

## Multi-agent rules

- Every agent gets explicit file ownership and acceptance criteria.
- All 20 AdPulse specialist roles use this `AGENTS.md` plus `.opencode/skills/ponytail/SKILL.md`; their prompts must name that shared instruction source when the host does not inject project skills automatically.
- Every role uses full mode by default and ultra mode for the elevated work listed above. A role may not treat minimal scope as permission to omit tests, validation, security, accessibility or error handling.
- Shared contracts, EF migrations and package manifests have one owner at a time.
- No agent reports a command or test as successful unless it ran it and records the actual outcome.
- The integrator resolves conflicts and performs cross-module tests.
- If the host cannot spawn 20 agents, run the 20 roles as sequential/limited parallel workstreams and state that honestly.

## Completion report

For each task, state: change made, files touched, tests/commands actually run and outcomes, what was not checked, and remaining risks. Avoid claiming production readiness without deployment evidence.
