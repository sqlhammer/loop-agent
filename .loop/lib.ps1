# .loop/lib.ps1  —  FRAMEWORK. Do not edit per project.
# Shared helpers for run-loop.ps1: state, git, agent invocation, usage-limit survival.

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# State  (.loop/state.json)  — the loop's durable phase/counters across processes
# ---------------------------------------------------------------------------
function Get-LoopState {
  $p = ".loop/state.json"
  if (Test-Path $p) { return (Get-Content $p -Raw | ConvertFrom-Json) }
  return [pscustomobject]@{ phase = "plan"; iteration = 0; stall = 0; startCommit = "" }
}
function Set-LoopState($state) {
  $state | ConvertTo-Json -Depth 5 | Set-Content ".loop/state.json" -Encoding UTF8
}

# ---------------------------------------------------------------------------
# Git helpers
# ---------------------------------------------------------------------------
function Ensure-GitBaseline {
  # Guarantee at least one commit exists so HEAD/diff/revert work.
  git rev-parse HEAD *> $null
  if ($LASTEXITCODE -ne 0) {
    git add -A *> $null
    git commit -q -m "chore: loop-agent baseline" --allow-empty *> $null
  }
}
function Get-Head { (git rev-parse HEAD).Trim() }

# ---------------------------------------------------------------------------
# Usage-limit parsing  — the heart of requirement #5
# Parses messages like:
#   "You've hit your session limit · resets 12:10am (America/New_York)"
#   "5-hour limit reached · resets 3pm"
# Returns seconds to wait (until reset + buffer), or $null if not a limit message.
# ---------------------------------------------------------------------------
function Get-ResetDelaySeconds([string]$text, [int]$BufferSeconds = 120) {
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  $isLimit = ($text -match '(?i)(hit your (session|usage) limit|limit reached|usage limit|rate limit|reset[s]? (at|in))')
  if (-not $isLimit) { return $null }

  if ($text -match '(?i)reset[s]?\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*([ap])\.?m') {
    $h = [int]$Matches[1]
    $m = if ($Matches[2]) { [int]$Matches[2] } else { 0 }
    $ampm = $Matches[3].ToLower()
    if ($ampm -eq 'p' -and $h -ne 12) { $h += 12 }
    if ($ampm -eq 'a' -and $h -eq 12) { $h = 0 }
    $now = Get-Date
    $reset = Get-Date -Hour $h -Minute $m -Second 0
    if ($reset -le $now) { $reset = $reset.AddDays(1) }   # reset time is in the future
    return [int]($reset - $now).TotalSeconds + $BufferSeconds
  }
  # It's a limit message but we couldn't parse a clock time (e.g. weekly cap w/ a date).
  # Signal "limited, unknown duration" with a sentinel so the caller uses a fallback wait.
  return -1
}

# ---------------------------------------------------------------------------
# Invoke-Agent  — run one fresh headless Claude process; survive usage limits
# transparently (sleep to reset, re-probe, retry) and return the final output.
# Each call is a NEW process with an EMPTY context window (fresh-context design).
# ---------------------------------------------------------------------------
function Invoke-Agent {
  param(
    [string]$PromptFile,
    [string]$PromptText,       # use instead of -PromptFile for a short inline prompt (e.g. preflight)
    [Parameter(Mandatory)][string]$Label,
    [string]$Model,
    [string[]]$ClaudeArgs,
    [int]$BufferSeconds = 120,
    [int]$FallbackWaitSeconds = 1800
  )
  if (-not $PromptText -and -not $PromptFile) { throw "Invoke-Agent: supply -PromptFile or -PromptText" }
  $prompt = if ($PromptText) { $PromptText } else { Get-Content $PromptFile -Raw }
  $ts = (Get-Date -Format "yyyyMMdd-HHmmss")
  $log = ".loop/logs/$Label-$ts.log"

  while ($true) {
    $args = @('-p', $prompt) + $ClaudeArgs
    if ($Model) { $args += @('--model', $Model) }

    Write-Host "  -> agent [$Label] model=$Model" -ForegroundColor DarkGray
    $out = (& claude @args 2>&1 | Tee-Object -FilePath $log | Out-String)
    $exitCode = $LASTEXITCODE

    $delay = Get-ResetDelaySeconds $out $BufferSeconds
    if ($null -eq $delay) {
      # Not a usage-limit message. A non-zero exit is a HARD FAILURE (auth error,
      # crash, bad invocation, etc.) — never silently treat it as a successful
      # completion. Surface it loudly so the supervisor stops instead of committing
      # empty "progress" and lying about state.
      if ($exitCode -ne 0) {
        Write-Host "  !! agent [$Label] FAILED (exit $exitCode)" -ForegroundColor Red
        Write-Host "  ---- output ----" -ForegroundColor Red
        Write-Host $out.Trim()
        Write-Host "  ---- (full log: $log) ----" -ForegroundColor Red
        throw "Agent invocation failed for '$Label' (exit $exitCode). See $log"
      }
      return $out   # normal completion
    }

    if ($delay -lt 0) { $delay = $FallbackWaitSeconds }  # limit hit, time unknown -> fallback
    $wake = (Get-Date).AddSeconds($delay)
    Write-Host "  !! usage limit. sleeping $([int]($delay/60)) min -> resume ~$($wake.ToString('t')) [$Label]" -ForegroundColor Yellow

    Start-Sleep -Seconds $delay

    # Re-probe cheaply before spending a real iteration on a still-closed window.
    $probe = (& claude -p "Reply with the single word: OK" @ClaudeArgs 2>&1 | Out-String)
    $stillLimited = Get-ResetDelaySeconds $probe $BufferSeconds
    if ($null -ne $stillLimited) {
      Write-Host "  .. still limited after wait; will wait again." -ForegroundColor Yellow
      continue   # window not actually open yet; loop re-sleeps
    }
    if ($LASTEXITCODE -ne 0) {
      Write-Host "  !! re-probe failed for a non-limit reason (exit $LASTEXITCODE):" -ForegroundColor Red
      Write-Host $probe.Trim()
      throw "Agent invocation failed for '$Label' after usage-limit wait (probe exit $LASTEXITCODE)."
    }
    Write-Host "  .. window reopened; retrying [$Label]." -ForegroundColor Green
    # loop around and re-run the real prompt
  }
}

# ---------------------------------------------------------------------------
# Convenience: are there open tasks left in the plan?
# ---------------------------------------------------------------------------
function Test-OpenTasks {
  if (-not (Test-Path "IMPLEMENTATION_PLAN.md")) { return $true }
  return [bool](Select-String -Path "IMPLEMENTATION_PLAN.md" -Pattern '^\s*[-*]\s*\[ \]' -Quiet)
}

# ---------------------------------------------------------------------------
# Convenience: run the project's verifier target. Returns $true on exit 0.
# ---------------------------------------------------------------------------
function Invoke-Verify {
  param([ValidateSet('Gate','Accept')][string]$Target)
  if (-not (Test-Path "verify.ps1")) {
    Write-Host "  verify.ps1 missing" -ForegroundColor Red; return $false
  }
  try   { & pwsh -NoProfile -File "verify.ps1" "-$Target" *> ".loop/logs/verify-$Target.log" }
  catch { return $false }
  return ($LASTEXITCODE -eq 0)
}
