# agentic-sdlc-lab

A **synthetic** .NET 10 sandbox for learning agentic development and dev automation: Claude Code, CI security
tooling, telemetry and feedback loops. Every name, endpoint and value in it is invented.

| Project | Role |
|---|---|
| `src/Lab.PlatformApi` | ASP.NET Core minimal API: the platform (`/health`, `/api/devices`) |
| `src/Lab.ClientService` | Worker Service that runs as a Windows service: the privileged client service |
| `src/Lab.Portal` | Blazor WebAssembly front end that calls the API over CORS |
| `tests/Lab.Tests` | xUnit v3 on Microsoft.Testing.Platform |

## Build and test
Requires the .NET 10 SDK (pinned in `global.json`).
```powershell
dotnet build   # warnings are errors
dotnet test
```

`CLAUDE.md` holds the conventions the coding agent follows. `.claude/settings.json` holds its shared permission rules.
