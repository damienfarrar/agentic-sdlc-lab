# agentic-sdlc-lab

Learning sandbox for agentic development and dev automation: Claude Code, CI security tooling, telemetry,
feedback loops. The code is a **synthetic stand-in** for a typical platform shape: an API (the platform), a
privileged Windows client service, and a web portal.

## Start of every session
The plan, learning log and ADRs live in a **private sibling repo**, checked out next to this one at
`../agentic-sdlc-lab-notes` (access is granted by `additionalDirectories` in `.claude/settings.json`).
1. Read `../agentic-sdlc-lab-notes/docs/plan.md` (where we are in the 6 weeks).
2. Read the **latest** entry at the bottom of `../agentic-sdlc-lab-notes/docs/learning-log.md`, especially its open questions.
3. Before doing anything else, summarise in 2-3 lines where we are and what's next.

If the notes folder isn't there, say so and carry on with the code only. Never copy notes content into this repo:
**this repo is public.**

## Ground rules (non-negotiable)
- **Public repo, synthetic only.** Everything here is invented: no real organisations, people, products, endpoints,
  schemas, data formats or config. If something looks real, stop and ask.
- **Never commit secrets.** Use `dotnet user-secrets` or env vars. `.gitignore` covers `.env*`,
  keys and certs, but don't rely on it. Never put secrets in `src/Lab.Portal/wwwroot/appsettings.json`:
  every browser downloads it.
- **Explain the why.** The user is here to learn. With every non-trivial change, give the reasoning,
  the trade-off and the alternative you rejected.
- **Ask before adding** any dependency (NuGet, npm, tool), external service or cloud resource.
- **Code style:** explicit `Program` class with `static Main`, never top-level statements.

## Repo map
- `src/Lab.PlatformApi`: ASP.NET Core minimal API, the platform (`/health`, `/api/devices`).
- `src/Lab.ClientService`: Worker Service that runs as a Windows service, the privileged client service.
- `src/Lab.Portal`: Blazor WASM standalone front end; calls the API over CORS.
- `tests/Lab.Tests`: xUnit v3 on Microsoft.Testing.Platform; API tests use `WebApplicationFactory`.
- Notes repo (private, sibling): `docs/plan.md`, `docs/learning-log.md`, `docs/adr/` (template is `0000-template.md`).

## Build, test, run
```powershell
dotnet build                     # whole solution; warnings are errors
dotnet test                      # MTP runner (set in global.json)
dotnet test --filter-class "*DeviceEndpointTests"      # one class (wildcards allowed)
dotnet test --filter-method "*UnknownId*"               # matching methods
dotnet run --project src/Lab.PlatformApi      # https://localhost:7101 (https is the first/default profile)
dotnet run --project src/Lab.ClientService    # console mode; Ctrl+C to stop
dotnet run --project src/Lab.Portal           # https://localhost:7201; calls the API at 7101
```
Done means `dotnet build` shows 0 warnings and `dotnet test` is all green.

Running Portal and API together requires a **trusted** ASP.NET Core dev certificate. If the browser reports
"CORS request did not succeed" with status `(null)`, the request failed at TLS, not because of CORS. Check with
`dotnet dev-certs https --check --trust`. Trusting the cert changes the user's certificate store, so the user does it, not Claude.

## Conventions
- **Guardrails live in the build, not here.** `Directory.Build.props` sets warnings-as-errors and
  `latest-recommended` analyzers. `.editorconfig` makes top-level statements a build error (IDE0211).
  Fix the code rather than suppress a rule. If a suppression is truly needed, add a comment saying why.
- **No `cd` in shell commands.** It persists between tool calls and has moved the working directory in five sessions. A PreToolUse hook
  (`.claude/hooks/block-cd.ps1`) blocks it; use absolute paths and `git -C <dir>` instead. Deny rules were probed and miss `cd` into workspace folders.
- **Package versions live only in `Directory.Packages.props`** (Central Package Management).
  `.csproj` files have `<PackageReference Include="..." />` with no version. New packages need approval first.
- Shared build settings (TFM, nullable, analyzers) live in `Directory.Build.props`. Don't repeat them in project files.
- File-scoped namespaces, one public type per file. Logging uses `[LoggerMessage]` source-generated methods.
- Test names: `Method_Scenario_Expected`. CA1707 is disabled under `tests/` only, in `.editorconfig`.
  Async test calls pass `TestContext.Current.CancellationToken` (xUnit1051).
- Inject `TimeProvider` instead of calling `DateTime.Now`/`DateTimeOffset.UtcNow`, so tests control time.
- **ADR per real decision**, in the notes repo: copy `docs/adr/0000-template.md` to the next number. Accepted ADRs are
  immutable; supersede them with a new ADR instead of editing.
- **End of every session:** append a dated entry to the notes repo's `docs/learning-log.md` (what we did, what I learned,
  open questions), and commit it **there**, not here.
- Don't commit or push unless the user asks. Code changes go to this repo; notes changes go to the notes repo.

## Windows service: the user installs it, never Claude
Installing a service needs an elevated shell, and it changes the machine outside the repo. Claude must not run
`sc.exe`, `New-Service` or `Start-Service`. For the user, from an elevated prompt:
```powershell
dotnet publish src/Lab.ClientService -c Release -o C:\LabServices\ClientService
sc.exe create LabClientService binPath= "C:\LabServices\ClientService\Lab.ClientService.exe" start= demand obj= "NT AUTHORITY\LocalService"
sc.exe start LabClientService      # remove later with: sc.exe stop LabClientService; sc.exe delete LabClientService
```
It runs as `LocalService` (least privilege) rather than `LocalSystem`. That choice belongs in the STRIDE model.
