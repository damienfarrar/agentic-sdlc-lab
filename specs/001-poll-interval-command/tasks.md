---

description: "Task list for the poll-interval command"
---

# Tasks: Poll-interval command

**Input**: Design documents from `/specs/001-poll-interval-command/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/claim-next-command.md, quickstart.md

**Tests**: Required. Constitution Principle III says a test is written first and seen red, and SC-005
asks for mutation-checked rejection rules. A test that refers to a type that doesn't exist yet is red
because it doesn't compile. Where that applies, add a skeleton that returns `false` or throws
`NotImplementedException` first, so the test fails on an assertion instead.

**Organization**: Tasks are grouped by user story. US1 and US2 are both P1: US1 is the accept path and
US2 is the hostile-input catalogue. They ship together (spec, User Story 2, "Why this priority").

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1, US2 or US3 from spec.md
- Paths are relative to the repo root. **No `cd`**: use absolute paths or `git -C` (a hook blocks `cd`).

## Conventions that apply to every task (CLAUDE.md)

- File-scoped namespaces, one public type per file, explicit `Program` class (no top-level statements).
- Logging uses `[LoggerMessage]` source-generated methods. Time comes from the injected `TimeProvider`.
- Test names follow `Method_Scenario_Expected`. Async test calls pass `TestContext.Current.CancellationToken`.
- No new packages. The test helpers are written by hand (research R6, R7).
- Rejection reasons are fixed strings, quoted exactly as in data-model.md, and never include values from the command.

---

## Phase 1: Setup

**Purpose**: Record the baseline that SC-004 is measured against

- [X] T001 Run `dotnet build` and `dotnet test` from `C:\Development\agentic-sdlc-lab` on branch `001-poll-interval-command`. Record the warning count (expected 0) and the total and passed test counts as the SC-004 baseline, in the eventual commit message

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Types and test helpers that every story uses. No behaviour change.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Create `public abstract record ValidCommand(Guid Id);` in `src/Lab.ClientService/Commands/ValidCommand.cs`, namespace `Lab.ClientService.Commands`, with the XML doc "A command that passed validation. Only a subtype of this reaches code that acts on a command."
- [X] T003 Make `ValidDiagnosticsCommand` derive from it, as `public sealed record ValidDiagnosticsCommand(Guid Id, string FileName, IReadOnlyList<DiagnosticsSection> Sections) : ValidCommand(Id);`, in `src/Lab.ClientService/Diagnostics/ValidDiagnosticsCommand.cs`. Depends on T002
- [X] T004 [P] In `src/Lab.ClientService/Diagnostics/DeviceCommandDto.cs`, add a last positional parameter `JsonElement? IntervalSeconds = null`, so existing 4-argument call sites still compile. Extend the XML doc: the field is a raw JSON value on purpose, so a malformed interval reaches the validator and is logged with its id rather than failing the whole response (research R2)
- [X] T005 [P] In `src/Lab.ClientService/Diagnostics/DiagnosticsOptions.cs`, add `public TimeSpan MinPollInterval { get; set; } = TimeSpan.FromSeconds(10);` and `public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromHours(1);`. Leave `IsValid` unchanged; that's US3
- [X] T006 Create `public sealed record PollIntervalBounds(TimeSpan Min, TimeSpan Max)` with `public static PollIntervalBounds From(DiagnosticsOptions options)` in `src/Lab.ClientService/Commands/PollIntervalBounds.cs`. Depends on T005
- [X] T007 [P] Add `internal sealed class ManualTimer : ITimer` in `tests/Lab.Tests/ClientService/ManualTimer.cs` and override `CreateTimer` in `tests/Lab.Tests/ClientService/ManualTimeProvider.cs` to return it. Add `public void Advance(TimeSpan by)`, which moves `Now` forward and fires each due timer's callback, repeating for periodic timers. Support `Change(dueTime, period)`, `Dispose` and `DisposeAsync`. Keep the existing `Now` setter, because current tests use it
- [X] T008 [P] Create `internal sealed class RecordingLogger<T> : ILogger<T>` in `tests/Lab.Tests/ClientService/RecordingLogger.cs`. It records each entry's `LogLevel`, formatted message and structured state (key/value pairs), and the list it keeps is thread-safe
- [X] T009 Run `dotnet build` (0 warnings) and `dotnet test`: every baseline test still passes and no existing test file was edited

**Checkpoint**: Foundation ready

---

## Phase 3: User Story 1: Operator changes the poll interval (Priority: P1) 🎯 MVP

**Goal**: A valid `SetPollInterval` command within bounds changes how often the device checks, from the next wait on, until restart.

**Independent Test**: Queue `SetPollInterval` 60 for a device that checks every 30 s with bounds 10–3600. After the claim, later claims are 60 s apart. A new worker with the same configuration checks every 30 s again.

### Tests for User Story 1 (write first, see them fail)

- [X] T010 [P] [US1] Create `tests/Lab.Tests/ClientService/PollIntervalValidatorTests.cs` with a helper that builds a `DeviceCommandDto` of type `"SetPollInterval"`, where `IntervalSeconds` is `JsonDocument.Parse(rawJson).RootElement.Clone()`. Tests: `TryValidate_InRange_ReturnsInterval` (raw `60`, bounds 10 s–3600 s → `Interval == 00:01:00`, same `Id`); `TryValidate_OnBound_Accepted` (theory with `10` and `3600`); `TryValidate_JustOutsideBound_Rejected` (theory with `9` and `3601` → rejection `"Poll interval out of range."`)
- [X] T011 [P] [US1] Create `tests/Lab.Tests/ClientService/CommandParserTests.cs`: `TryParse_ValidDiagnostics_ReturnsValidDiagnosticsCommand` (same input as the existing validator's well-formed test) and `TryParse_ValidPollInterval_ReturnsValidPollIntervalCommand`
- [X] T012 [P] [US1] In `tests/Lab.Tests/PlatformApi/CommandEndpointTests.cs`, add new tests only, without editing the existing ones. `ClaimNext_PollIntervalDevice_ReturnsSetPollIntervalOnce`: POST `/api/devices/LAB-DEVICE-003/commands/next` returns 200, the body contains `"type":"SetPollInterval"` and `"intervalSeconds":60` and has no `outputName` or `sections` property, and a second claim returns 204. `ClaimNext_DiagnosticsCommand_OmitsIntervalSeconds`: the LAB-DEVICE-001 body has no `intervalSeconds`
- [X] T013 [US1] Create `tests/Lab.Tests/ClientService/DiagnosticsWorkerTests.cs`.
  - **Setup:** a real `DiagnosticsWorker` with `ManualTimeProvider`, a `PlatformCommandClient` over a `StubHttpMessageHandler` that hands out responses from a queue and releases a `SemaphoreSlim` on each request, a real `DiagnosticsCollector` writing to a per-test temp folder, and `RecordingLogger`. Wait on the semaphore with a 5 s timeout; never use `Task.Delay` or sleeps.
  - `PollOnce_AcceptedInterval_AppliesFromNextWait`: `PollInterval` is 30 s. The first claim returns `SetPollInterval` 60, then 204s follow. Advancing 30 s causes no claim; advancing to 60 s causes one; advancing to 120 s causes another.
  - `ExecuteAsync_AfterRestart_UsesConfiguredInterval`: a second worker with the same options, and no command, claims at 30 s.
  - `PollOnce_AcceptedInterval_LogsOldAndNew`: one Information entry with `CommandId`, `OldInterval` `00:00:30` and `NewInterval` `00:01:00`.
- [X] T014 [US1] Run the US1 tests and confirm each one fails, on an assertion where a skeleton exists. Note which failed only on compile

### Implementation for User Story 1

- [X] T015 [P] [US1] Add `SetPollInterval` to `src/Lab.PlatformApi/Commands/DeviceCommandType.cs`
- [X] T016 [US1] In `src/Lab.PlatformApi/Commands/DeviceCommand.cs`, change `OutputName` to `string?` and `Sections` to `IReadOnlyList<DiagnosticsSection>?`, and add `int? IntervalSeconds`. Put `[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` on all three. Update the XML doc: the type-specific arguments are null when they don't apply to the command's type
- [X] T017 [US1] In `src/Lab.PlatformApi/Commands/SyntheticCommandStore.cs`, switch the existing seed to named arguments and add a seed for `"LAB-DEVICE-003"`: `Id` `0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60`, `Type` `SetPollInterval`, `IntervalSeconds` `60`, other arguments null. Comment that the value is fictional. Depends on T016
- [X] T018 [P] [US1] Create `public sealed record ValidPollIntervalCommand(Guid Id, TimeSpan Interval) : ValidCommand(Id);` in `src/Lab.ClientService/Commands/ValidPollIntervalCommand.cs`
- [X] T019 [US1] Create `public static class PollIntervalValidator` in `src/Lab.ClientService/Commands/PollIntervalValidator.cs`, with `TryValidate(DeviceCommandDto command, PollIntervalBounds bounds, [NotNullWhen(true)] out ValidPollIntervalCommand? valid, [NotNullWhen(false)] out string? rejection)`. Implement data-model rules 4 and 5:
  - **Rule 4:** "`IntervalSeconds` is a JSON number that `TryGetInt32` accepts", otherwise `"Malformed poll interval."`. A JSON `null` or a missing field counts as absent and is malformed.
  - **Rule 5:** "`MinPollInterval <= seconds <= MaxPollInterval`", otherwise `"Poll interval out of range."`.
  - Compare as `TimeSpan.FromSeconds(seconds)` against `bounds`, inclusive.
  - XML doc: the value is never rounded or clamped (research R2, spec FR-003).
- [X] T020 [US1] Create `public static class CommandParser` in `src/Lab.ClientService/Commands/CommandParser.cs`, with `TryParse(DeviceCommandDto command, PollIntervalBounds bounds, [NotNullWhen(true)] out ValidCommand? valid, [NotNullWhen(false)] out string? rejection)`.
  - **Rule 1:** `Id == Guid.Empty`, then `"Missing command id."`.
  - **Rule 2:** an `Ordinal` switch on `Type`. `"CollectDiagnostics"` goes to the existing `CommandValidator.TryValidate`, and `"SetPollInterval"` goes to `PollIntervalValidator.TryValidate`. Anything else gives `"Unknown command type."`.
  - XML doc: this is the single allowlist of command types (research R3).
  - Depends on T018 and T019.
- [X] T021 [US1] In `src/Lab.ClientService/Diagnostics/DiagnosticsWorker.cs`:
  - Pass the `PeriodicTimer` into `PollOnceAsync`, and replace the `CommandValidator.TryValidate` call with `CommandParser.TryParse(command, PollIntervalBounds.From(options.Value), ...)`.
  - Switch on the result: `ValidDiagnosticsCommand` takes the existing collector path. `ValidPollIntervalCommand` records the old `timer.Period`, sets `timer.Period = valid.Interval`, and logs through a new `[LoggerMessage(Level = LogLevel.Information, Message = "Command {CommandId}: poll interval changed from {OldInterval} to {NewInterval}")]`.
  - Update the class XML doc: the worker runs every allowlisted command type.
  - Depends on T020.
- [X] T022 [US1] Run `dotnet build` (0 warnings) and `dotnet test`. The US1 tests and all baseline tests pass

**Checkpoint**: US1 works on its own. Its out-of-range pair (9 and 3601) is guarded, but the rest of the hostile catalogue isn't yet. Don't release without US2.

---

## Phase 4: User Story 2: A hostile or broken platform can't misuse the command (Priority: P1)

**Goal**: Every case in the contract's reject table is refused with its fixed reason. The interval doesn't change, the log carries no command text, and later commands still run.

**Independent Test**: Run each reject-table row through the parser and the worker. Every row is rejected with the exact reason, the next claim still comes at the old interval, and no log entry contains the test marker.

### Tests for User Story 2 (write first)

- [X] T023 [P] [US2] In `tests/Lab.Tests/ClientService/PollIntervalValidatorTests.cs`, add:
  - `TryValidate_NonPositive_Rejected`: theory with `0` and `-5`, giving `"Poll interval out of range."`.
  - `TryValidate_Malformed_Rejected`: theory with the raw JSON `1.5`, `60.0`, `6e1`, `"60"`, `null`, `true`, `[]`, `{}`, `99999999999`, plus a separate test for a missing field (`IntervalSeconds` is the C# `null`). All give `"Malformed poll interval."`.
  - `TryValidate_ForeignArguments_Rejected`: theory with `OutputName = "case-1042"` and with `Sections = ["Service"]`, each alongside a valid `60`, giving `"Unexpected arguments for command type."`.
- [X] T024 [P] [US2] In `tests/Lab.Tests/ClientService/CommandValidatorTests.cs`, add tests only. `TryValidate_DiagnosticsWithIntervalSeconds_Rejected` (raw `60`, giving `"Unexpected arguments for command type."`) and `TryValidate_DiagnosticsWithNullIntervalSeconds_Accepted` (raw JSON `null` counts as absent). Leave every existing case unchanged (SC-004)
- [X] T025 [P] [US2] In `tests/Lab.Tests/ClientService/CommandParserTests.cs`, add `TryParse_UnknownType_Rejected`: theory with `"setpollinterval"`, `" SetPollInterval"`, `"SetPollInterval "`, `"RunScript"`, `""` and `null`, each giving `"Unknown command type."`. Also add `TryParse_EmptyId_Rejected`, giving `"Missing command id."`
- [X] T026 [US2] In `tests/Lab.Tests/ClientService/DiagnosticsWorkerTests.cs`, add:
  - `PollOnce_HostileCommand_LogsNoCommandText`: claim a `SetPollInterval` whose `outputName` is `"MARKER-7f3a\r\nforged entry"`. It's rejected, and no `RecordingLogger` entry's message or state value contains `MARKER-7f3a`.
  - `PollOnce_RejectedInterval_KeepsCurrentInterval`: claim `SetPollInterval` 5. The next claim still comes at 30 s.
  - `PollOnce_AfterRejection_StillRunsLaterCommands`: a rejected command followed by a valid `SetPollInterval` 60, which is applied.
- [X] T027 [US2] Run the US2 tests. The foreign-argument tests (T023 third theory, T024 first test) and the hostile-text worker test must fail. Record which other new cases already pass because US1 implemented their rule; those get mutation-checked in T040 instead of being seen red

### Implementation for User Story 2

- [X] T028 [US2] In `src/Lab.ClientService/Commands/PollIntervalValidator.cs`, add data-model rule 3 before rule 4: "`OutputName` and `Sections` are both null", otherwise `"Unexpected arguments for command type."`
- [X] T029 [US2] In `src/Lab.ClientService/Diagnostics/CommandValidator.cs`, add data-model rule 6 after the type check and before the output-name check: if `command.IntervalSeconds` is set and its `ValueKind` isn't `JsonValueKind.Null`, reject with `"Unexpected arguments for command type."`. Keep the public signature unchanged
- [X] T030 [US2] Run `dotnet build` (0 warnings) and `dotnet test`. All tests pass

**Checkpoint**: US1 and US2 together are the shippable feature

---

## Phase 5: User Story 3: The local administrator keeps the final say (Priority: P2)

**Goal**: The bounds come from local configuration. Invalid bounds stop the service when remote commands are enabled, and equal bounds pin the interval.

**Independent Test**: Build the service provider with different `Diagnostics` settings. Defaults apply when nothing is set, every invalid combination fails validation, and pinned bounds accept only that one value.

### Tests for User Story 3 (write first)

- [X] T031 [P] [US3] In `tests/Lab.Tests/ClientService/DiagnosticsOptionsTests.cs`, add tests only, without editing existing ones:
  - `Enabled_NoBounds_UsesDefaults`: `MinPollInterval` is `00:00:10` and `MaxPollInterval` is `01:00:00`.
  - `Enabled_InvalidBounds_FailsValidation`: theory over `MinPollInterval`, `MaxPollInterval` and `PollInterval`, each row giving `OptionsValidationException`:
    - `00:00:00`, `01:00:00`, `00:00:30`
    - `-00:00:01`, `01:00:00`, `00:00:30`
    - `00:01:00`, `00:00:30`, `00:00:45`
    - `00:00:10`, `1.00:00:01`, `00:00:30`
    - `00:01:00`, `01:00:00`, `00:00:30`
    - `00:00:10`, `00:00:20`, `00:00:30`
  - `Enabled_MaxAtDayLimit_Valid`: `00:00:10`, `1.00:00:00`, `00:00:30`.
  - `Disabled_InvalidBounds_StillStarts`.
- [X] T032 [P] [US3] In `tests/Lab.Tests/ClientService/PollIntervalValidatorTests.cs`, add `TryValidate_PinnedBounds_AcceptsOnlyThatValue`: bounds 30 s–30 s accept `30` and reject `29` and `31` as `"Poll interval out of range."`. Also add `From_Options_CopiesMinAndMax` for `PollIntervalBounds.From`
- [X] T033 [US3] Run the US3 tests. The invalid-bounds rows must fail

### Implementation for User Story 3

- [X] T034 [US3] In `src/Lab.ClientService/Diagnostics/DiagnosticsOptions.cs`, add `internal static readonly TimeSpan MaxPollIntervalLimit = TimeSpan.FromHours(24);`, with a comment that `PeriodicTimer` throws at about 49.7 days and that 24 h keeps a misconfigured maximum from crashing the poll loop (research R5). Extend `IsValid`, when enabled, with "`0 < MinPollInterval <= MaxPollInterval <= 24 h`" and "`MinPollInterval <= PollInterval <= MaxPollInterval`"
- [X] T035 [US3] In `src/Lab.ClientService/Diagnostics/DiagnosticsServiceCollectionExtensions.cs`, extend the validation message: "MinPollInterval must be positive and no more than MaxPollInterval, MaxPollInterval at most 24 hours, and PollInterval within them"
- [X] T036 [P] [US3] In `src/Lab.ClientService/appsettings.json`, add `"MinPollInterval": "00:00:10"` and `"MaxPollInterval": "01:00:00"` under `Diagnostics`, so administrators can see the settings exist
- [X] T037 [US3] Run `dotnet build` (0 warnings) and `dotnet test`. All tests pass

**Checkpoint**: All three stories are done

---

## Phase 6: Polish and cross-cutting

- [X] T038 Run the automated checks in `specs/001-poll-interval-command/quickstart.md`. Compare the test count against the T001 baseline: every baseline test still passes and no existing test case changed (SC-004)
- [X] T039 Check the diff against CLAUDE.md conventions: one public type per file, file-scoped namespaces, `[LoggerMessage]`, `TimeProvider`, test names, `TestContext.Current.CancellationToken`, no package versions in `.csproj` files, and no suppressions. Fix anything found
- [X] T040 Mutation check (SC-005, Principle III). Break each rule in turn, run its tests, confirm red, then restore:
  - (a) lower bound `<=` changed to `<`
  - (b) upper bound `<=` changed to `<`
  - (c) `TryGetInt32` replaced with `TryGetDouble` and a cast
  - (d) rule 3 removed
  - (e) rule 6 removed
  - (f) `Ordinal` changed to `OrdinalIgnoreCase` in `CommandParser`
  - (g) `timer.Period` assignment removed
  - (h) the 24 h limit check removed
  - Record each mutant and the test that caught it, for the commit message and the learning log
- [X] T041 Run the `security-scan` skill (Snyk Code and Snyk Open Source). Report what it covered alongside the result. Note that a clean Snyk Code result doesn't cover the platform-to-client boundary: Snyk Code doesn't treat `HttpClient` responses as a source, so the tests in US2 carry it
- [X] T042 Set `**Status**` in `specs/001-poll-interval-command/spec.md` from `Draft` to `Implemented`. Leave the optional manual run in quickstart.md to the owner, since it needs a trusted dev certificate

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (T001)** comes first.
- **Foundational (T002–T009)** needs Setup and blocks every story. T003 depends on T002, and T006 on T005.
- **US1 (T010–T022)** needs Foundational.
- **US2 (T023–T030)** needs US1, because it adds rules to US1's validator and parser.
- **US3 (T034–T037)** needs only Foundational: `DiagnosticsOptions` validation is separate. Its tests T032 refer to `PollIntervalValidator`, so they need T019.
- **Polish (T038–T042)** needs all three stories.

### Within each story

Tests first and seen red, then the types, then validators, then the parser, then worker integration,
then a green run.

### Parallel opportunities

- Foundational: T002, T004, T005, T007 and T008 touch different files and can run together.
- US1 tests: T010, T011 and T012 can run together. T013 waits for T007 and T008.
- US1 implementation: T015 (API) and T018 (client) can run together. The API chain T015 → T016 → T017 is independent of the client chain T018 → T019 → T020 → T021.
- US2 tests: T023, T024 and T025 can run together.
- US3 can run alongside US2 once US1's T019 exists.

## Parallel Example: User Story 1

```text
Task: "T010 PollIntervalValidatorTests in tests/Lab.Tests/ClientService/PollIntervalValidatorTests.cs"
Task: "T011 CommandParserTests in tests/Lab.Tests/ClientService/CommandParserTests.cs"
Task: "T012 new CommandEndpointTests cases in tests/Lab.Tests/PlatformApi/CommandEndpointTests.cs"

Task: "T015–T017 platform side in src/Lab.PlatformApi/Commands/"
Task: "T018–T021 client side in src/Lab.ClientService/Commands/ and Diagnostics/DiagnosticsWorker.cs"
```

## Implementation Strategy

### MVP

The MVP is US1 **and** US2: Setup → Foundational → US1 → US2, then validate. US1 on its own accepts a
remote timer value with only its bounds tested, which constitution Principle II doesn't allow in a
release.

### Incremental delivery

1. Setup and Foundational: no behaviour change, all tests green.
2. US1: the accept path works and the bounds are tested.
3. US2: the hostile catalogue is closed. This is the shippable point.
4. US3: the administrator's bounds are validated at start-up.
5. Polish: mutation check, security scan, convention check.

## Notes

- Commit after each phase checkpoint, on branch `001-poll-interval-command`, and only when the owner
  asks. Use `git commit -F <file>`.
- Don't edit existing test cases. SC-004 depends on them staying unchanged.
- Out of scope: renaming `DiagnosticsWorker` and `DiagnosticsOptions` (plan, Risks and follow-ups).
