# .loop/lib.ps1  —  FRAMEWORK. Do not edit per project.
# Shared helpers for run-loop.ps1: state, git, agent invocation, usage-limit survival.

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# .env loading  — CLAUDE_CODE_OAUTH_TOKEN (and any other project secrets) live in
# a gitignored .env file in the project root, KEY=value per line. Loaded once into
# the current process's environment so every child process (claude, verify.ps1)
# inherits it automatically. Never logs values. Existing env vars are NOT
# overwritten (a real shell export always wins over .env).
# ---------------------------------------------------------------------------
function Import-DotEnv {
  param([string]$Path = ".env")
  if (-not (Test-Path $Path)) { return 0 }
  $count = 0
  foreach ($line in Get-Content $Path) {
    $t = $line.Trim()
    if ($t -eq '' -or $t.StartsWith('#')) { continue }
    if ($t -notmatch '^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') { continue }
    $key = $Matches[1]
    $val = $Matches[2].Trim()
    if (($val.StartsWith('"') -and $val.EndsWith('"')) -or ($val.StartsWith("'") -and $val.EndsWith("'"))) {
      $val = $val.Substring(1, $val.Length - 2)
    }
    $existing = [Environment]::GetEnvironmentVariable($key)
    if ([string]::IsNullOrEmpty($existing)) {
      Set-Item -Path "Env:$key" -Value $val
      $count++
    }
  }
  return $count
}

# ---------------------------------------------------------------------------
# State  (.loop/state.json)  — the loop's durable phase/counters across processes
# ---------------------------------------------------------------------------
function Get-LoopState {
  $p = ".loop/state.json"
  $raw = if (Test-Path $p) { Get-Content $p -Raw | ConvertFrom-Json } else { $null }
  # Normalize to a consistent shape so downstream property assignments always succeed,
  # even when reading a state.json written before a field (e.g. startCommits) existed.
  $startCommits = @{}
  if ($raw -and $raw.PSObject.Properties['startCommits'] -and $raw.startCommits) {
    foreach ($prop in $raw.startCommits.PSObject.Properties) { $startCommits[$prop.Name] = $prop.Value }
  }
  return [pscustomobject]@{
    phase        = if ($raw -and $raw.phase) { $raw.phase } else { "plan" }
    iteration    = if ($raw) { [int]$raw.iteration } else { 0 }
    stall        = if ($raw) { [int]$raw.stall } else { 0 }
    startCommits = $startCommits          # repo path -> commit the build loop started from
  }
}
function Set-LoopState($state) {
  $state | ConvertTo-Json -Depth 5 | Set-Content ".loop/state.json" -Encoding UTF8
}

# ---------------------------------------------------------------------------
# Managed repos  — the set of git repos the supervisor commits/gates each build
# iteration. Always includes the control repo (this clone, = current dir). In
# Mode 2 it also includes every external repo listed in .loop/projects.
#   Mode 1 (self-contained): .loop/projects empty/absent  => just the control repo.
#   Mode 2 (external repos):  one repo path per line       => control + externals.
# ---------------------------------------------------------------------------
function Read-Projects {
  # Parse .loop/projects: one repo path per line; '#' comments and blank lines ignored.
  # Relative paths resolve against the control repo root (the current directory). The
  # directory need not exist yet — it is created/inited at baseline.
  $p = ".loop/projects"
  if (-not (Test-Path $p)) { return @() }
  $paths = @()
  foreach ($line in Get-Content $p) {
    $t = $line.Trim()
    if ($t -eq '' -or $t.StartsWith('#')) { continue }
    $full = if ([System.IO.Path]::IsPathRooted($t)) { $t } else { Join-Path (Get-Location).Path $t }
    $paths += [System.IO.Path]::GetFullPath($full)
  }
  return $paths
}
function Get-ManagedRepos {
  $control = [System.IO.Path]::GetFullPath((Get-Location).Path)
  $seen  = @{}
  $repos = @()
  foreach ($r in @($control) + (Read-Projects)) {
    $key = $r.ToLowerInvariant()                 # Windows paths are case-insensitive
    if (-not $seen.ContainsKey($key)) { $seen[$key] = $true; $repos += $r }
  }
  return $repos
}

# ---------------------------------------------------------------------------
# Git helpers
# ---------------------------------------------------------------------------
function Ensure-GitBaseline {
  # Guarantee $Repo exists, is a git repo, and has at least one commit so
  # HEAD/diff/revert work. Only ever called on repos in the managed set — never
  # on the user's unrelated repos. Creates + inits an external project dir if the
  # goal points at one that doesn't exist yet.
  param([string]$Repo = ".")
  if (-not (Test-Path $Repo)) {
    New-Item -ItemType Directory -Force -Path $Repo | Out-Null
    Write-Host "  created project dir: $Repo" -ForegroundColor DarkGray
  }
  git -C $Repo rev-parse --is-inside-work-tree *> $null
  if ($LASTEXITCODE -ne 0) {
    git -C $Repo init *> $null
    Write-Host "  git init: $Repo" -ForegroundColor DarkGray
  }
  git -C $Repo rev-parse HEAD *> $null
  if ($LASTEXITCODE -ne 0) {
    git -C $Repo add -A *> $null
    git -C $Repo commit -q -m "chore: loop-agent baseline" --allow-empty *> $null
  }
}
function Get-Head { param([string]$Repo = ".") (git -C $Repo rev-parse HEAD).Trim() }

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
