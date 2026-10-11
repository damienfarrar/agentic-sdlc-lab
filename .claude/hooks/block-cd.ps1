# PreToolUse hook for the Bash and PowerShell tools: blocks commands that change the shell's working directory.
# The tools keep their working directory between calls, so one stray `cd` silently moves every later relative
# path and `dotnet` command (it caused drift in five sessions running). Permission deny rules were probed first
# and let `cd` into folders inside the workspace through, which is the case that bit us.
#
# This is an accident guard, not a security boundary (see ADR-0002): it matches command text, so a deliberate
# `eval` or variable-built command gets past it. Known false positive: an unquoted heredoc (<<EOF) whose body
# has a line starting with `cd` or a backticked `cd`; quote the delimiter (<<'EOF') for text that isn't run.
# Test table: block-cd.tests.ps1, next to this file.
#
# Exit 2 blocks the call and shows stderr to the agent. Any other failure exits 1, which Claude Code reports
# as a hook error and does not block, so a broken hook fails open but visibly.

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.Encoding]::UTF8

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$command = [string]$payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# Blank out text that is never run, so Markdown in a log entry or a commit message doesn't match. First the bodies
# of quoted heredocs (<<'EOF', <<"EOF", <<\EOF, optionally <<-) and PowerShell literal here-strings (@'...'@). Their
# quoted delimiters would confuse the quote stripping below, so they go first. The rest of the heredoc's opening line
# is kept: it is still a command. Unquoted heredocs (<<EOF) are left alone, because their bodies expand $(...) and
# backticks. An unterminated heredoc doesn't match, so it is scanned as is (stricter than Bash).
$scan = $command -replace '(?ms)<<-?[ \t]*(?:''(?<d>[^''\n]+)''|"(?<d>[^"\n]+)"|\\(?<d>\w+))(?<rest>[^\n]*)\n.*?^\t*\k<d>[ \t]*$', '<<_${rest}'
$scan = $scan -replace '(?ms)@''[ \t]*\n.*?^''@', '""'

# Then quoted strings, so `echo "... cd ..."` doesn't match.
# One alternation, so whichever quote comes first wins (handles "don't" and 'say "hi"').
$scan = $scan -replace '''[^'']*''|"(?:[^"\\`]|[\\`].)*"', '""'

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
