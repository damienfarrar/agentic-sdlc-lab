# Evidence for a trusted result

Use the Snyk binary that `.mcp.json` names (a versioned path), so the CLI and the MCP server are the same
build. Run from the repo root with absolute paths (no `cd`), and write output files to the session
scratchpad, never into the repo.

## Exit codes (both commands)
| Code | Meaning | Treat as |
|---|---|---|
| 0 | Scan completed, no issues | Clean **only** if the coverage checks below pass |
| 1 | Scan completed, issues found | Findings to triage |
| 2 | Failure | No result. An expired login shows as `401` (SNYK-0005) from `code test` but as `400 Bad Request` (SNYK-0003) from `test`: if `test` gives SNYK-0003, ask the user to run `snyk auth`. Don't use `-d`; debug logs can put request headers into the transcript |
| 3 | No supported projects detected | **Nothing was scanned.** Usually a missing `dotnet restore` |

## Open Source
```powershell
dotnet restore
& "<snyk.exe from .mcp.json>" test --all-projects --print-deps --json-file-output=<scratchpad>\sca.json
```
Check in `sca.json` (an array, one entry per project):
- Every project in the solution is there, each found through its `obj/project.assets.json`.
- `dependencyCount` is above 0 for every project that has packages, and versions are resolved (Central
  Package Management puts them in `Directory.Packages.props`, not the `.csproj`).
- `Lab.PlatformApi` showing 0 is expected: ASP.NET Core comes in as a framework reference, not a package, and
  Snyk doesn't check the shared framework. Report that as a gap, not as clean.

## Code
```powershell
& "<snyk.exe from .mcp.json>" code test --sarif-file-output=<scratchpad>\code.sarif
```
Check in `code.sarif`:
- `runs[0].properties.coverage`: one entry per file type; `files` on the `lang: ".cs"` entry is the number
  of C# files analysed. Compare it with `git ls-files --cached --others --exclude-standard '*.cs'`. Fewer
  means some files weren't scanned.
- `runs[0].results`: the findings, each with its rule id and location.

## Positive control
Use when a change brings a source or sink this repo hasn't checked Snyk against. It costs one Code test.
1. Add one throwaway file to the project under test, e.g. `ZzPositiveControl.cs`. Feed the new source into a
   sink Snyk Code is known to catch here (`Process.Start("cmd.exe", "/c " + x)`, command injection), and
   into the sink under test.
2. Run a Code scan.
3. Read the result:
   - Known sink caught: the source is visible to Snyk, so the sink-under-test result means something.
   - Known sink missed: Snyk doesn't treat this source as untrusted, so a 0 for the real code proves nothing.
     Say so, and rely on design and tests for that flow.
4. Delete the file. `git status --short` must not list it. Never build or commit it.
