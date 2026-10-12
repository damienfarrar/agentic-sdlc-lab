# agentic-sdlc-lab Constitution

## Core Principles

### I. Synthetic and Public

Everything in this repository is invented: no real organisations, people, products, endpoints,
schemas, data formats or configuration. The repository is public. Secrets MUST NOT be committed;
they come from `dotnet user-secrets` or environment variables. Nothing secret goes in
`src/Lab.Portal/wwwroot`, because every browser downloads it. A spec or plan that needs real-looking
data MUST stop and ask instead.

### II. Hostile Input at Every Trust Boundary

The privileged client service treats everything it receives from the platform as untrusted. It
acts only on typed, allowlisted commands, never on strings from the wire. File paths are re-checked
after resolution. Every spec that crosses a trust boundary MUST name the boundary and what is
validated there.

Rationale: scanners don't treat platform responses as untrusted input, so this boundary is defended
by design and tests, not by tooling.

### III. Verified, Not Assumed (NON-NEGOTIABLE)

- A test that shows the flaw or the behaviour is written first and seen red.
- Security-relevant tests are mutation-checked: break the code and confirm the test fails.
- Done means `dotnet build` with 0 warnings and `dotnet test` all green.
- A scanner's 0 is reported with evidence of what it covered (the `security-scan` skill).

### IV. Guardrails Live in the Build

Warnings are errors and analyzers are on. Code is fixed rather than a rule suppressed; any
suppression carries a comment saying why. A rule that matters is enforced by the build or a hook,
not only written down.

### V. Least Privilege and Explicit Consent

Adding a dependency, tool, external service or cloud resource requires the owner's approval first.
The Windows service runs as a least-privilege account, and only the owner installs it. Plans MUST
list any new dependency or privilege as an explicit decision.

### VI. Explain the Why

Every plan states its reasoning, the trade-off and the alternative it rejected. A real decision gets
an ADR, kept in the private notes repository, not here.

## Sources of Truth

`CLAUDE.md` is the source of truth for conventions and commands: code style, Central Package
Management, logging, test naming, build, test and run. This constitution does not repeat them; a
plan checks against both. Where they conflict, the conflict is raised with the owner, not resolved
silently.

## Development Workflow and Quality Gates

- Features go spec → plan → constitution check → tasks → implement.
- A plan that fails the constitution check is revised or the deviation is justified in the plan
  before tasks are generated.
- Before a change is called done: build and tests pass (Principle III), and code that handles
  untrusted input or adds a package has been through the `security-scan` skill.

## Governance

This constitution governs specs and plans in this repository. Only the repository owner amends it,
through a reviewed commit. Versions follow semantic versioning: MAJOR for removing or redefining a
principle, MINOR for adding one or materially expanding guidance, PATCH for wording. Every plan
includes a constitution check; reviews verify it.

**Version**: 1.0.0 | **Ratified**: 2026-10-12 | **Last Amended**: 2026-10-12
