# Implementation Plan: Poll-interval command

**Branch**: `001-poll-interval-command` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-poll-interval-command/spec.md`

## Summary

Add a second allowlisted command type, `SetPollInterval`, to the existing command channel. The platform
queues it with an interval in whole seconds. The client service reads the value raw, so a malformed one
is still logged with its command id. A new `CommandParser` checks the type against the allowlist and
routes the command to its validator, which checks it against bounds the device's administrator
configures. The accepted interval is applied to the worker's existing `PeriodicTimer` by setting its
`Period`. Nothing is persisted, no endpoint is added, and no package is added.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`, set in `Directory.Build.props`)

**Primary Dependencies**: None added. Uses what's already referenced: ASP.NET Core minimal APIs,
Microsoft.Extensions.Hosting and Options, System.Text.Json.

**Storage**: None. The interval is held in memory, and the platform's queue stays in memory.

**Testing**: xUnit v3 on Microsoft.Testing.Platform, with `WebApplicationFactory` for the API. Two
in-test helpers are written rather than added as packages: a controllable timer on `ManualTimeProvider`
(research R6) and a recording logger (R7).

**Target Platform**: Windows service (`Lab.ClientService`, running as LocalService) and an ASP.NET Core
API (`Lab.PlatformApi`).

**Project Type**: Existing three-project solution plus its tests. No new project.

**Performance Goals**: None beyond today's. One poll per interval, and the interval is at least 10 s by
default.

**Constraints**: Bounds must be ≤ 24 h, well under the timer's limit of about 49.7 days. Command
responses stay under the existing 64 KB cap. `dotnet build` must show 0 warnings.

**Scale/Scope**: One new command type and one argument. About 6 new source files and 4 changed, plus
tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-research | Post-design | How the design meets it |
|---|---|---|---|
| I. Synthetic and Public | Pass | Pass | Seeded command for the fictional `LAB-DEVICE-003`; no secrets or config values introduced |
| II. Hostile Input at Every Trust Boundary | Pass | Pass | The spec names the three boundaries. Typed allowlist in one place (R3). The raw value is parsed by the validator, not the serializer (R2). Arguments from another type are rejected. Logs carry the id and a fixed reason only |
| III. Verified, Not Assumed | Pass | Pass | Tests first and seen red; every row of the contract table is a test case; the mutation check is listed in quickstart; timing is verified on a controlled clock, not assumed from docs (R4) |
| IV. Guardrails Live in the Build | Pass | Pass | No suppressions planned. New logging uses `[LoggerMessage]`; time comes from `TimeProvider` |
| V. Least Privilege and Explicit Consent | Pass | Pass | **No new dependency, endpoint or privilege.** An enqueue endpoint was rejected (R8). The package alternatives in R6 and R7 go to the owner as options, not defaults. Bounds give the device administrator the final say |
| VI. Explain the Why | Pass | Pass | research.md records the decision, rationale and rejected alternative for each choice. ADR: none needed, as this follows the existing channel's design. Spec Kit adoption gets its own ADR in the notes repo |

**CLAUDE.md conventions** (the source of truth for conventions):
- one public type per file
- file-scoped namespaces
- `Method_Scenario_Expected` test names
- async tests pass `TestContext.Current.CancellationToken`
- no versions in `.csproj` files (no packages are added anyway)

No violations, so there's no Complexity Tracking table.

**Spec amendments made during planning** (recorded in the spec itself):
- FR-005 and the matching edge case: the change applies from the next wait. A command can't arrive
  mid-wait.
- FR-008: added the 24 h cap on the maximum.

## Project Structure

### Documentation (this feature)

```text
specs/001-poll-interval-command/
├── spec.md
├── plan.md                        # this file
├── research.md                    # R1–R8 decisions
├── data-model.md                  # types, settings, validation rules, state
├── quickstart.md                  # gate commands, mutation check, manual run
├── contracts/
│   └── claim-next-command.md      # wire format and the accept/reject table
├── checklists/
│   └── requirements.md
└── tasks.md                       # /speckit-tasks output (not created here)
```

### Source Code (repository root)

```text
src/Lab.PlatformApi/Commands/
├── DeviceCommandType.cs           # + SetPollInterval
├── DeviceCommand.cs               # type-specific fields nullable; + IntervalSeconds; nulls omitted
└── SyntheticCommandStore.cs       # + seeded SetPollInterval(60) for LAB-DEVICE-003

src/Lab.ClientService/
├── Commands/                      # new folder: what's common to every command type
│   ├── ValidCommand.cs            # new abstract record
│   ├── CommandParser.cs           # new: id check, exact type allowlist, routes to validators
│   ├── PollIntervalValidator.cs   # new: foreign arguments, whole seconds, bounds
│   ├── ValidPollIntervalCommand.cs  # new
│   └── PollIntervalBounds.cs      # new: min and max, built from DiagnosticsOptions
└── Diagnostics/
    ├── DeviceCommandDto.cs        # + IntervalSeconds (JsonElement?, optional)
    ├── ValidDiagnosticsCommand.cs # now derives from ValidCommand
    ├── CommandValidator.cs        # + reject a stray intervalSeconds; signature unchanged
    ├── DiagnosticsOptions.cs      # + MinPollInterval, MaxPollInterval; IsValid extended
    ├── DiagnosticsServiceCollectionExtensions.cs  # validation message updated
    └── DiagnosticsWorker.cs       # uses CommandParser; sets timer Period; logs the change

tests/Lab.Tests/
├── ClientService/
│   ├── PollIntervalValidatorTests.cs   # new: one case per contract row
│   ├── CommandParserTests.cs           # new
│   ├── DiagnosticsWorkerTests.cs       # new: timing on a manual clock; log content
│   ├── CommandValidatorTests.cs        # + stray-intervalSeconds case; existing cases untouched
│   ├── DiagnosticsOptionsTests.cs      # + bounds cases
│   ├── ManualTimeProvider.cs           # + CreateTimer returning a manual timer
│   └── RecordingLogger.cs              # new helper
└── PlatformApi/
    └── CommandEndpointTests.cs         # + SetPollInterval wire format for LAB-DEVICE-003
```

**Structure Decision**: The new shared types go in `Lab.ClientService/Commands`, because they aren't
about diagnostics. The existing `Diagnostics` types stay where they are, so their tests and namespaces
don't move. `DiagnosticsWorker` now runs more than diagnostics, so its name is wrong. Renaming it is
left as a follow-up, to keep this diff to the feature.

## Risks and follow-ups

- **Naming debt**: `DiagnosticsWorker` and `DiagnosticsOptions` now cover all remote commands. Rename
  them to `CommandWorker` and `RemoteCommandOptions` in a separate refactor. That rename also changes
  the configuration section, so it's a breaking change for existing config.
- **No authentication on the claim endpoint** (existing, in the STRIDE model). Anyone who can reach the
  platform can claim a device's commands. They can't inject them, because there's no enqueue endpoint.
- **Security scan**: the client now parses a new untrusted field, so run the `security-scan` skill
  before calling it done. Snyk Code doesn't treat `HttpClient` responses as a source (Week 1 finding),
  so the tests carry this boundary, not the scanner.
