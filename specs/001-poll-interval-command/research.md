# Research: Poll-interval command

No item in the Technical Context was left as NEEDS CLARIFICATION. These are the design decisions the
plan depends on, each checked against the existing code or the .NET 10 reference docs.

## R1. Wire shape of a command with more than one type

- **Decision**: Keep one flat command record. Arguments that belong to a type are nullable properties,
  and the platform omits nulls when it writes JSON. New property: `intervalSeconds`.
- **Rationale**: The client's validator has to see every argument a command carries, so it can reject
  arguments that belong to another type (FR-004). With a flat record that is a null check. It also
  keeps the claim queue to a single element type.
- **Alternatives considered**: Polymorphic JSON (`[JsonPolymorphic]`, with `type` as the discriminator).
  Rejected for two reasons. An unknown discriminator throws during deserialization, so the rejection is
  logged as a claim failure **without the command id** (FR-009 needs the id). And System.Text.Json
  expects the discriminator to come first, so the outcome would depend on the order of properties on
  the wire.

## R2. How the client reads the interval

- **Decision**: The client's DTO keeps `IntervalSeconds` as a raw JSON value (`JsonElement?`). The
  validator accepts only a JSON number for which `TryGetInt32` succeeds.
- **Rationale**: With a typed `int?`, a fraction, a string or an overflowing number throws a
  `JsonException` for the whole response. The command would still be refused, but the log would say
  "could not claim" with no id and no reason. Reading the raw value moves that decision into the
  validator, which logs the id and a fixed reason. `TryGetInt32` succeeds only when the whole token is
  an integer, so `1.5`, `1e1` and `60.0` are all rejected; nothing is rounded (FR-003).
- **Alternatives considered**: `int?` with a custom converter. Rejected: it throws from inside the
  serializer, so it has the same logging gap. `double` followed by a whole-number check. Rejected: it
  accepts `60.0` and `6e1`, which widens what counts as valid for no benefit.
- **Scope note**: `type` and `outputName` stay typed as strings. A number in those fields still fails
  the whole response, as it does today. Changing that isn't part of this feature.

## R3. Where the allowlist of command types lives

- **Decision**: Add a new `CommandParser`, which checks the id and then switches on the exact type
  string. It hands `CollectDiagnostics` to the existing `CommandValidator` (unchanged apart from
  rejecting a stray `intervalSeconds`) and `SetPollInterval` to a new `PollIntervalValidator`. Any other
  type is rejected. Both results derive from a new abstract `ValidCommand(Guid Id)`.
- **Rationale**: The closed set of types stays in one place. The existing validator keeps its
  signature, so its tests don't change (SC-004).
- **Alternatives considered**:
  - Widen `CommandValidator.TryValidate` to return any command type. Rejected: it changes the
    signature that every existing validator test calls.
  - Branch on the type in the worker. Rejected: it splits the allowlist across two classes.

## R4. Changing the interval at runtime

- **Decision**: Keep the worker's `PeriodicTimer` and set its `Period` when a poll-interval command is
  accepted.
- **Rationale**: In .NET 10, `PeriodicTimer.Period` is settable (System.Runtime.xml: "Gets or sets the
  period between ticks"). Values must be at least 1 ms and less than `UInt32.MaxValue` ms, which is
  about 49.7 days. The command is accepted while a tick is being handled, between waits, so the next
  wait is the first one to use the new interval. The spec's "wait in progress" case can't arise with
  this loop, and the spec now says "the next wait" (FR-005, amended).
- **Alternatives considered**: Replace the timer with a `Task.Delay(interval, timeProvider, token)`
  loop that re-reads the interval each time. Rejected: it rewrites a working loop to change one value.
  The worker test checks the real timing either way.

## R5. Upper limit on the bounds

- **Decision**: When remote commands are enabled, start-up validation also requires
  `MaxPollInterval <= 24 hours` (FR-008, amended).
- **Rationale**: The timer throws for periods of about 49.7 days or more. Without a cap, a
  misconfigured maximum would let an accepted command crash the poll loop, where it should simply be
  rejected. 24 hours is far below that limit, matches the spec's "checks in several times a day"
  reasoning, and is easy to test.
- **Alternatives considered**: Cap at the timer's exact limit. Rejected: it's an odd number to put in
  configuration, and nothing needs intervals that long.

## R6. Controlling time in the worker test

- **Decision**: Extend the test project's `ManualTimeProvider` with `CreateTimer` overridden to return
  a manual timer that fires when the test advances the clock. No new package.
- **Rationale**: The base `TimeProvider.CreateTimer` uses a real system timer, so today's helper can't
  drive `PeriodicTimer`. The existing helper's comment already prefers a small subclass over
  `Microsoft.Extensions.TimeProvider.Testing`, and adding a package needs approval (Principle V).
- **Alternatives considered**: `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`. It's
  the standard choice but a new dependency. It goes to the owner as an option, not a default.

## R7. Testing what gets logged

- **Decision**: Add a small in-test `ILogger` that records the formatted message and its structured
  state.
- **Rationale**: SC-003 says a rejection log never contains text from the command. The test sends
  values with a marker in them and asserts the marker never appears.
- **Alternatives considered**: `FakeLogger` from `Microsoft.Extensions.Diagnostics.Testing`. Rejected
  for the same reason as R6: it's a new dependency for about twenty lines of helper.

## R8. A seeded command for the end-to-end check

- **Decision**: Seed one `SetPollInterval` command of 60 seconds for `LAB-DEVICE-003`. No endpoint for
  queueing commands.
- **Rationale**: The quickstart needs something to claim. `LAB-DEVICE-001` already has a seeded command,
  and a test expects its queue to be empty after one claim. A test also expects `LAB-DEVICE-002` to have
  no commands. Device 003 has none today. An unauthenticated enqueue endpoint would let anyone change
  any device's interval, which is a bigger exposure than this feature needs.
- **Alternatives considered**: An enqueue endpoint. Rejected for the reason above. It belongs with
  authentication (STRIDE model).
