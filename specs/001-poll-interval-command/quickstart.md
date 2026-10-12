# Quickstart: validate the poll-interval command

## Automated checks (the gate)

```powershell
dotnet build                                          # 0 warnings
dotnet test                                           # all green
dotnet test --filter-class "*PollIntervalValidatorTests"   # every row of contracts/claim-next-command.md
dotnet test --filter-class "*CommandParserTests"           # type allowlist, foreign arguments, both types routed
dotnet test --filter-class "*DiagnosticsWorkerTests"       # interval change on a controlled clock; no command text in logs
dotnet test --filter-class "*DiagnosticsOptionsTests"      # bounds checked at start-up
dotnet test --filter-class "*CommandEndpointTests"         # wire format; seeded command for LAB-DEVICE-003
```

What each check proves is in [data-model.md](data-model.md), under "Validation rules", and in
[contracts/claim-next-command.md](contracts/claim-next-command.md).

**Mutation check (SC-005):** remove or weaken each validation rule in turn and confirm a test goes red,
then put it back. Rules to break:
- the lower bound and the upper bound
- the whole-number check
- the foreign-argument check, for each type
- the exact type match

## Manual end-to-end check (optional)

You'll need a **trusted** dev certificate (`dotnet dev-certs https --check --trust`). You trust it
yourself; Claude doesn't.

1. Start the platform: `dotnet run --project src/Lab.PlatformApi`.
2. Start the client as the seeded device, with remote commands on:

   ```powershell
   dotnet run --project src/Lab.ClientService -- --Heartbeat:DeviceId=LAB-DEVICE-003 --Diagnostics:Enabled=true --Diagnostics:PlatformBaseAddress=https://localhost:7101/ --Diagnostics:OutputDirectory=$env:TEMP\lab-diagnostics
   ```

3. **What you should see:** the first poll claims the seeded command, and the client logs a change
   from `00:00:30` to `00:01:00`. Neither process logs an empty poll, so the timing itself is proven by
   `DiagnosticsWorkerTests` on a controlled clock, not by this run. Restart both processes (the
   platform's queue is in memory too) and the change is logged again from `00:00:30`, showing the
   restart went back to the configured interval.
4. **Negative check:** restart the client with `--Diagnostics:MaxPollInterval=00:00:45`. The same
   command is now rejected, and the log shows `Poll interval out of range.` with the command id and no
   other command text.
