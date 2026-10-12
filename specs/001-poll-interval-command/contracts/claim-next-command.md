# Contract: claim the next command

`POST /api/devices/{id}/commands/next`. The endpoint, verb and status codes are unchanged: **200** with
a command, **204** when the queue is empty, **404** for an unknown device. This feature only adds a
command type to the 200 body.

## 200 body: `CollectDiagnostics` (unchanged)

```json
{ "id": "6f1c2d8e-0b7a-4c55-9e1f-3a2b4c5d6e7f", "type": "CollectDiagnostics",
  "outputName": "case-1042", "sections": ["Environment", "Service"] }
```

## 200 body: `SetPollInterval` (new)

```json
{ "id": "0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60", "type": "SetPollInterval", "intervalSeconds": 60 }
```

- `intervalSeconds`: a JSON integer, in whole seconds.
- The platform doesn't send arguments that belong to another type. The client rejects a command that
  carries them.

## What the client accepts

| Input | Result |
|---|---|
| `"intervalSeconds": 60`, with bounds 10–3600 | accepted |
| `10` or `3600` (on a bound) | accepted |
| `9`, `3601`, `0`, `-5` | rejected: `Poll interval out of range.` |
| `1.5`, `60.0`, `6e1`, `"60"`, `null`, field missing, `true`, `[]`, `{}`, `99999999999` | rejected: `Malformed poll interval.` |
| `SetPollInterval` that also has `outputName` or `sections` | rejected: `Unexpected arguments for command type.` |
| `CollectDiagnostics` that also has `intervalSeconds` | rejected: `Unexpected arguments for command type.` |
| `"type": "setpollinterval"` or `" SetPollInterval"` | rejected: `Unknown command type.` |
| A body over 64 KB, or one that isn't JSON | refused before parsing (existing); logged as a claim failure |

The pinned wire format: type and enum values travel as names, and null arguments are omitted.
`CommandEndpointTests` checks both.
