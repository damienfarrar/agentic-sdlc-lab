# Data model: Poll-interval command

## Platform side (`Lab.PlatformApi/Commands`)

### DeviceCommandType (enum, serialized as its name)

| Value | Status |
|---|---|
| `CollectDiagnostics` | existing |
| `SetPollInterval` | **new** |

### DeviceCommand (record)

| Field | Type | Used by | Change |
|---|---|---|---|
| `Id` | `Guid` | all | unchanged |
| `Type` | `DeviceCommandType` | all | unchanged |
| `OutputName` | `string?` | `CollectDiagnostics` | was non-nullable |
| `Sections` | `IReadOnlyList<DiagnosticsSection>?` | `CollectDiagnostics` | was non-nullable |
| `IntervalSeconds` | `int?` | `SetPollInterval` | **new** |

Null fields are omitted from the JSON (`JsonIgnoreCondition.WhenWritingNull`). The platform doesn't
check the interval against bounds: those are set on the device, and the platform doesn't know them.

## Client side (`Lab.ClientService`)

### DeviceCommandDto (record, untrusted)

| Field | Type | Change |
|---|---|---|
| `Id` | `Guid` | unchanged |
| `Type` | `string?` | unchanged |
| `OutputName` | `string?` | unchanged |
| `Sections` | `IReadOnlyList<string?>?` | unchanged |
| `IntervalSeconds` | `JsonElement?` | **new**, optional positional parameter (default `null`), so existing call sites still compile |

### ValidCommand (abstract record, new)

`ValidCommand(Guid Id)`. Only a subtype of this reaches code that acts on a command.

| Subtype | Fields | Change |
|---|---|---|
| `ValidDiagnosticsCommand` | `Id`, `FileName`, `Sections` | now derives from `ValidCommand` |
| `ValidPollIntervalCommand` | `Id`, `Interval: TimeSpan` | **new** |

### DiagnosticsOptions (configuration section `Diagnostics`)

| Setting | Default | Change |
|---|---|---|
| `PollInterval` | 30 s | unchanged; it's now also the interval a restart goes back to |
| `MinPollInterval` | 10 s | **new** |
| `MaxPollInterval` | 1 h | **new** |

These are checked at start-up only when `Enabled` is true. Failing any of them stops the service.

- `0 < MinPollInterval <= MaxPollInterval <= 24 h`
- `MinPollInterval <= PollInterval <= MaxPollInterval`

## Validation rules (client)

These run in order, and the first one that fails gives the rejection reason. Each reason is a fixed
string.

| # | Applies to | Rule | Rejection reason |
|---|---|---|---|
| 1 | all | `Id` is not empty | `Missing command id.` (existing) |
| 2 | all | `Type` exactly matches `CollectDiagnostics` or `SetPollInterval` | `Unknown command type.` (existing) |
| 3 | `SetPollInterval` | `OutputName` and `Sections` are both null | `Unexpected arguments for command type.` |
| 4 | `SetPollInterval` | `IntervalSeconds` is a JSON number that `TryGetInt32` accepts | `Malformed poll interval.` |
| 5 | `SetPollInterval` | `MinPollInterval <= seconds <= MaxPollInterval` | `Poll interval out of range.` |
| 6 | `CollectDiagnostics` | `IntervalSeconds` is null | `Unexpected arguments for command type.` |
| 7 | `CollectDiagnostics` | existing output-name and section rules | existing reasons |

A JSON `null` for `IntervalSeconds` counts as absent under rules 4 and 6.

## State

The current interval is the timer's `Period`, held in memory only.

- **At start-up**: `PollInterval` from configuration.
- **On a valid `SetPollInterval` command**: the requested interval, from the next wait onwards. The
  change is logged with the old and new values.
- **On restart**: back to `PollInterval` (FR-006).

The heartbeat interval is separate and is never changed by a command (FR-013).
