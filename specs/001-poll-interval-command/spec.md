# Feature Specification: Poll-interval command

**Feature Branch**: `001-poll-interval-command`

**Created**: 2026-10-12

**Status**: Implemented

**Input**: User description: "Device command channel: the platform API exposes pending commands for a device (GET /api/devices/{id}/commands), and the privileged Windows client service polls it and executes only typed, allowlisted commands. Everything received from the platform is untrusted: unknown command types, malformed or out-of-range arguments, oversized payloads and path-like arguments that escape an allowed root are rejected, and each rejection is logged without echoing the raw input. Keep the first slice small: one or two harmless synthetic commands (e.g. report status, set poll interval within bounds). All data is synthetic."

**Scope note**: Most of the described channel already exists: devices claim commands one at a time, one
typed command (collect diagnostics) is allowlisted, oversized responses are refused, file names are
validated and the resolved path is re-checked, and rejections are logged without the command's text.
This feature adds the **second command type, "set poll interval"**, and keeps everything else as is.
"Report status" is left out because the existing heartbeat and diagnostics bundle already cover it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Operator changes how often a device checks for commands (Priority: P1)

A platform operator queues a "set poll interval" command for one device, for example to make it check
more often while they work on an incident, or less often to cut traffic. The device claims the
command, checks that the requested interval is within the bounds its local administrator allows, and
from then on checks for commands at the new interval.

**Why this priority**: It is the feature. Without it there is nothing new for the trust-boundary rules
to protect.

**Independent Test**: Queue a valid command for a device, let the device claim it, and observe that
the gap between its later checks matches the requested interval.

**Acceptance Scenarios**:

1. **Given** the device allows 10 seconds to 1 hour and checks every 30 seconds, **When** it claims a
   command asking for 60 seconds, **Then** later checks happen every 60 seconds and the change is
   logged with the old and new interval.
2. **Given** the same device, **When** it claims a command asking for exactly 10 seconds or exactly
   1 hour, **Then** the change is accepted (the bounds are inclusive).
3. **Given** the device accepted a new interval, **When** the service restarts, **Then** it checks at
   the locally configured interval again.

---

### User Story 2 - A hostile or broken platform can't misuse the command (Priority: P1)

The platform, or anything that can impersonate it, sends a "set poll interval" command that is out of
range, malformed or mixed with arguments from another command type. The device rejects it, keeps its
current interval, logs a fixed reason with the command's identifier, and carries on.

**Why this priority**: The client is privileged and treats the platform as untrusted (constitution
Principle II). A remote value that sets a timer could flood the platform (interval too short) or make
the device go quiet (interval too long), so the rejections ship with the feature, not after it.

**Independent Test**: Feed the device each case from the edge-case list below and confirm that every
one is rejected, the interval doesn't change, and the log entry holds no text from the command.

**Acceptance Scenarios**:

1. **Given** bounds of 10 seconds to 1 hour, **When** a command asks for 9 seconds, 0, a negative
   value or more than 1 hour, **Then** it is rejected as out of range and the interval doesn't change.
2. **Given** any bounds, **When** the interval is missing, not a whole number of seconds, or too large
   to represent, **Then** the command is rejected as malformed.
3. **Given** any bounds, **When** a "set poll interval" command also carries diagnostics arguments (an
   output name or sections), **Then** it is rejected, and no diagnostics bundle is written.
4. **Given** a rejected command, **When** the device next checks, **Then** it still claims and runs
   later valid commands.

---

### User Story 3 - The local administrator keeps the final say (Priority: P2)

The administrator of the device sets the lowest and highest interval the platform may choose. Setting
both to the same value pins the interval so the platform can't change it.

**Why this priority**: It decides who controls a privileged service: the person who installed it, not
whoever reaches the platform. It ranks P2 because safe defaults already apply when the administrator
sets nothing.

**Independent Test**: Start the service with different local bounds and confirm which requests it
accepts; start it with invalid bounds and confirm it refuses to start.

**Acceptance Scenarios**:

1. **Given** the administrator set no bounds, **When** the service starts, **Then** the defaults of
   10 seconds to 1 hour apply.
2. **Given** bounds where the minimum is above the maximum, the minimum is zero or negative, or the
   configured interval is outside the bounds, **When** remote commands are enabled, **Then** the
   service refuses to start and says which setting is wrong.
3. **Given** both bounds set to 30 seconds, **When** a command asks for anything other than
   30 seconds, **Then** it is rejected.

---

### Edge Cases

- Interval one second below the minimum, or one second above the maximum: rejected.
- Interval given as a fraction, as text, as `null`, or as a number larger than any valid duration: rejected as malformed, never rounded or clamped.
- The command type is spelled with different letter case or has extra spaces: rejected as an unknown type (types match exactly, as they do today).
- The command repeats the current interval, or the same command arrives twice: accepted, and nothing changes; applying it again is harmless.
- A shorter interval is accepted after a long wait: the next wait already uses the new interval. *(Amended at planning: commands are handled between waits, so a change can't land part-way through one.)*
- Remote commands are disabled on the device: nothing is claimed, so the interval can't be changed remotely.
- The response is oversized or isn't valid command data: refused as it is today, before any command type is considered.

## Trust Boundaries *(required by constitution Principle II)*

| Boundary | What crosses it | What is checked there |
|---|---|---|
| Platform → client service (network) | The claimed command: identifier, type and arguments | Response size cap and https-only (existing); exact type match against the allowlist; for this type: interval present, whole seconds, within local bounds; no arguments belonging to other types |
| Local configuration → client service | Interval bounds and starting interval | Checked at start-up when remote commands are enabled; invalid settings stop the service starting |
| Client service → log | Rejection and change records | Only the command identifier, a fixed reason, and validated interval values; never text from the command |

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The platform MUST be able to queue a "set poll interval" command for a device, carrying a requested interval in whole seconds.
- **FR-002**: The client service MUST accept the command only when its type exactly matches an allowlisted type and the interval lies within the locally configured bounds, both inclusive.
- **FR-003**: The client service MUST reject, never round or clamp, an interval that is missing, not a whole number of seconds, out of range, or too large to represent.
- **FR-004**: The client service MUST reject a command that carries arguments belonging to a different command type.
- **FR-005**: An accepted interval MUST govern every later wait between checks, starting with the next one. *(Amended at planning: a command is accepted between waits, never during one; see research R4.)*
- **FR-006**: An accepted interval MUST NOT outlive a service restart; after a restart the locally configured interval applies.
- **FR-007**: The local administrator MUST be able to set the minimum and maximum interval; when unset, they default to 10 seconds and 1 hour.
- **FR-008**: When remote commands are enabled, the service MUST refuse to start if the minimum is not positive, the minimum exceeds the maximum, the maximum exceeds 24 hours, or the configured interval lies outside the bounds. *(24-hour cap added at planning; see research R5.)*
- **FR-009**: Every rejection MUST be logged with the command identifier and a fixed reason, and MUST NOT contain any text taken from the command.
- **FR-010**: Every accepted change MUST be logged with the command identifier and the old and new interval.
- **FR-011**: A rejected or failed command MUST NOT stop the service or stop it from handling later commands.
- **FR-012**: The existing "collect diagnostics" command MUST behave exactly as before.
- **FR-013**: The new command MUST NOT change the heartbeat interval, which stays under local control only.

### Key Entities

- **Command**: one instruction for one device, claimed once. It has an identifier, a type from a closed set, and arguments that depend on the type.
- **Poll-interval request**: the argument of the new command type, a duration in whole seconds.
- **Interval bounds**: the local administrator's minimum and maximum, plus the starting interval, set on the device and never by the platform.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the hostile cases in User Story 2 and the edge cases are rejected, and none of them changes the interval.
- **SC-002**: After an accepted change, every later gap between checks equals the new interval, measured with a controlled clock.
- **SC-003**: No log entry produced by a rejection contains any text from the command other than its identifier.
- **SC-004**: Every test of the diagnostics command that passed before this feature still passes, unchanged.
- **SC-005**: Each rejection rule has a test that fails when that rule is removed (mutation-checked, constitution Principle III).

## Assumptions

- Out-of-range values are rejected, not clamped. Clamping would quietly act on a value nobody asked for and hide a broken or hostile platform.
- The accepted interval lives in memory only (FR-006). Persisting it would let one hostile command outlive a restart, and would need a writable state file in a privileged location; it can be revisited if operators need it.
- Defaults of 10 seconds to 1 hour: short enough to cover incident work, long enough that a device still checks in several times a day. Administrators can tighten them.
- The device can't report its current interval back to the platform in this slice; operators see the effect in the device's logs.
- The existing opt-in (remote commands are off by default) and the existing gaps (no authentication on the claim endpoint) stay as they are and are tracked in the STRIDE model, not fixed here.
- All commands, devices and values are synthetic.
