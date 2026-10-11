# Test table for block-cd.ps1. Each case is piped to the hook as PreToolUse JSON; block cases must exit 2,
# allow cases exit 0. Run: powershell -NoProfile -File .claude/hooks/block-cd.tests.ps1 [-Hook <path>]
# Pass -Hook to run the same table against a mutated copy: a mutant that stays green means a missing case.

param([string]$Hook = (Join-Path $PSScriptRoot 'block-cd.ps1'))

# Joins its arguments with newlines, for multi-line commands such as heredocs.
function L { $args -join "`n" }

$block = @(
    'cd /c/x', 'cd', 'git status && cd x', '(cd x && pwd)', 'echo $(cd x; pwd)', 'foo; cd x', 'a || cd x', 'a | cd x',
    'if true; then cd x; fi', 'for d in a b; do cd $d; done', 'builtin cd x', (L 'line1' 'cd x'), '  cd x',
    'pushd x', 'popd', 'chdir x', 'echo "don''t" && cd x', 'echo `cd x`',
    'Set-Location C:\x', 'set-location C:\x', 'sl C:\x', 'Push-Location x', 'Pop-Location', 'cd..', 'cd\',
    'Get-Date; Set-Location C:\x', '& { cd x }', '[IO.Directory]::SetCurrentDirectory(''C:\x'')',
    '[Environment]::CurrentDirectory = ''C:\x''',
    # Heredocs: only the body of a quoted heredoc is inert. Unquoted bodies run $(...) and backticks,
    # and the rest of the opening line and anything after the terminator are still commands.
    (L 'cat <<EOF' '`cd x`' 'EOF'),
    (L 'cat <<EOF' '$(cd x)' 'EOF'),
    (L 'cat <<''EOF'' > f && cd x' 'body' 'EOF'),
    (L 'cat <<''EOF'' > f' 'body' 'EOF' 'cd x'),
    (L 'git commit -F - <<''EOF''' 'msg' 'EOF' 'Set-Location x'),
    (L '$m = @''' 'text' '''@' 'cd x')
)
$allow = @(
    'git -C /c/x status', 'echo "a && cd x"', "echo 'cd x'", 'git commit -m "Block cd in shell commands"',
    'git commit -m "it''s; cd x"', 'dotnet test --filter-method "*cd*"', 'npm run cd', 'ls abcd', 'echo cdrom',
    'Get-ChildItem C:\cd', 'dotnet build', 'grep -n "cd " file', '$cd = 1', 'abc-cd x', 'Get-Location', '',
    'sleep 1', 'git status && sleep 1', 'cdk synth', 'popdx', 'Set-LocationX',
    # The 2026-10-08 false positive: Markdown in a quoted heredoc.
    (L 'cat >> log.md <<''EOF''' '- **`cd` guard**: blocks `cd x`; use `git -C`.' 'EOF'),
    (L 'cat <<''EOF'' > f' 'cd x' 'EOF'),
    (L 'cat <<"EOF" > f' 'Don''t `cd` here.' 'EOF'),
    (L 'cat <<\EOF > f' 'cd x' 'EOF'),
    (L 'cat <<-''EOF'' > f' "`tcd x" "`tEOF"),
    (L 'cat <<''EOF'' > f' 'EOFX' 'cd x' 'EOF'),
    (L 'cat <<''EOF'' > f' 'see EOF' 'cd x' 'EOF'),
    (L 'git commit -m @''' 'it''s' 'cd x' '''@'),
    (L 'git commit -m "$(cat <<''EOF''' 'Fix the `cd` hook; it''s quieter' 'EOF' ')"'),
    (L 'git commit -m @''' 'Fix the `cd` hook; it''s quieter' '''@')
)

function Invoke-Hook([string]$cmd) {
    $json = @{ tool_name = 'Bash'; tool_input = @{ command = $cmd } } | ConvertTo-Json -Compress
    $null = $json | powershell.exe -NoProfile -NonInteractive -File $Hook 2>$null
    return $LASTEXITCODE
}

$fail = 0
foreach ($c in $block) { $rc = Invoke-Hook $c; if ($rc -ne 2) { $fail++; "FAIL (want block, got $rc): $($c -replace "`n", '\n')" } }
foreach ($c in $allow) { $rc = Invoke-Hook $c; if ($rc -ne 0) { $fail++; "FAIL (want allow, got $rc): $($c -replace "`n", '\n')" } }

$null = '{}' | powershell.exe -NoProfile -NonInteractive -File $Hook 2>$null; $rcEmpty = $LASTEXITCODE
if ($rcEmpty -ne 0) { $fail++; "FAIL (empty payload, got $rcEmpty)" }
$null = 'not json' | powershell.exe -NoProfile -NonInteractive -File $Hook 2>$null; $rcBad = $LASTEXITCODE
if ($rcBad -ne 1) { $fail++; "FAIL (malformed payload, want 1 so the hook fails open visibly, got $rcBad)" }

"$($block.Count) block cases, $($allow.Count + 2) allow/payload cases, $fail failures"
if ($fail -gt 0) { exit 1 }
