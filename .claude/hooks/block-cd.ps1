# PreToolUse hook for the Bash and PowerShell tools: blocks commands that change the shell's working directory.
# The tools keep their working directory between calls, so one stray `cd` silently moves every later relative
# path and `dotnet` command (it caused drift in five sessions running). Permission deny rules were probed first
# and let `cd` into folders inside the workspace through, which is the case that bit us.
#
# This is an accident guard, not a security boundary (see ADR-0002): it matches command text, so a deliberate
# `eval` or variable-built command gets past it. Known false positive: a heredoc line that starts with `cd`.
#
# Exit 2 blocks the call and shows stderr to the agent. Any other failure exits 1, which Claude Code reports
# as a hook error and does not block, so a broken hook fails open but visibly.

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.Encoding]::UTF8

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$command = [string]$payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# Blank out quoted strings first, so a commit message or `echo "... cd ..."` doesn't match.
# One alternation, so whichever quote comes first wins (handles "don't" and 'say "hi"').
$scan = $command -replace '''[^'']*''|"(?:[^"\\`]|[\\`].)*"', '""'

# Directory-changing commands in Bash and PowerShell, including PowerShell's built-in `cd..` and `cd\`.
$verbs = 'cd|chdir|pushd|popd|sl|Set-Location|Push-Location|Pop-Location|cd\.\.|cd\\'

# Only at a command position: start of a line, after a separator or opening bracket, or after a keyword
# that introduces a command. So `git -C dir`, `npm run cd` and `ls abcd` don't match.
$commandStart = '(?:^|[;&|(){}`]|\b(?:then|do|else|builtin|command|exec)\s)'
$pattern = "(?im)$commandStart\s*(?:$verbs)(?=[\s;&|)``]|$)"

# .NET calls that move the process directory from PowerShell.
$dotnetPattern = '(?i)SetCurrentDirectory|\[(?:System\.)?Environment\]::CurrentDirectory\s*='

if ($scan -match $pattern -or $scan -match $dotnetPattern) {
    [Console]::Error.WriteLine(
        'Blocked by .claude/hooks/block-cd.ps1: changing directory persists across tool calls and caused drift. ' +
        'Use absolute paths, `git -C <dir>`, or a tool flag such as `dotnet ... --project <path>` instead.')
    exit 2
}

exit 0
