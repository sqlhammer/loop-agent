<#
  run-loop.ps1  —  FRAMEWORK. Do not edit per project. Run from the project root.

  Autonomous, single-loop, context-fresh, usage-limit-surviving build supervisor.

  Per-project you edit only:  GOAL.md  and  verify.ps1
  Lifecycle (state machine in .loop/state.json):
     plan  -> await_approval -> build <-> review -> done   (or -> stalled)

  Usage:
     pwsh -File run-loop.ps1              # first run: generates plan + acceptance tests, then stops for your review
     pwsh -File run-loop.ps1 -Approve     # after you review: starts the unattended build loop
     pwsh -File run-loop.ps1 -Replan      # discard plan/tests and regenerate from GOAL.md
     pwsh -File run-loop.ps1 -Status      # print current phase/counters and exit
#>
param(
  [switch]$Approve,
  [switch]$Replan,
  [switch]$Status,
  [int]$MaxIterations     = 300,
  [int]$StallLimit        = 8,
  [int]$ResetBufferSeconds = 120,
  [string]$Model          = "sonnet",   # build iterations (conserve quota)
  [string]$ReviewModel    = "opus",     # plan + reviewer (higher quality at candidate-done)
  # Unattended permissions. Loops cannot answer permission prompts, so autonomy needs this.
  # STRONGLY prefer running inside a sandbox/container/VM. Dial back if you want tighter control.
  [string[]]$ClaudeArgs   = @('--dangerously-skip-permissions')
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
. ".loop/lib.ps1"

# --- guards ---------------------------------------------------------------
if (-not (Test-Path "GOAL.md"))   { Write-Host "GOAL.md not found. Fill it in first." -ForegroundColor Red; exit 1 }
if (-not (Test-Path "verify.ps1")){ Write-Host "verify.ps1 not found." -ForegroundColor Red; exit 1 }
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) { Write-Host "Not a git repo. Run 'git init' first." -ForegroundColor Red; exit 1 }
Ensure-GitBaseline

$state = Get-LoopState
if ($Status)  { $state | Format-List; exit 0 }
if ($Replan)  {
  Remove-Item -ErrorAction SilentlyContinue IMPLEMENTATION_PLAN.md, .loop/REVIEW-PASS, .loop/commit-msg.txt
  $state.phase = "plan"; $state.iteration = 0; $state.stall = 0; Set-LoopState $state
  Write-Host "Replan: cleared plan artifacts." -ForegroundColor Cyan
}

$common = @{ ClaudeArgs = $ClaudeArgs; BufferSeconds = $ResetBufferSeconds }

# =====================================================================
# PHASE: plan  — generate specs, plan, and (red) acceptance tests, then STOP
# =====================================================================
if ($state.phase -eq "plan") {
  Write-Host "=== PLAN PHASE ===" -ForegroundColor Cyan
  Invoke-Agent -PromptFile ".loop/prompt.plan.md" -Label "plan" -Model $ReviewModel @common | Out-Null
  git add -A *> $null; git commit -q -m "plan: specs, implementation plan, acceptance tests" --allow-empty *> $null
  $state.phase = "await_approval"; Set-LoopState $state
  Write-Host ""
  Write-Host "PLAN COMPLETE — one-time human review needed." -ForegroundColor Green
  Write-Host "  1. Review  IMPLEMENTATION_PLAN.md" -ForegroundColor Green
  Write-Host "  2. Review  tests/acceptance/  (these encode 'done' — edit if wrong)" -ForegroundColor Green
  Write-Host "  3. Approve: pwsh -File run-loop.ps1 -Approve" -ForegroundColor Green
  exit 0
}

# =====================================================================
# PHASE: await_approval  — gate the build behind the one-time approval
# =====================================================================
if ($state.phase -eq "await_approval") {
  if (-not $Approve) {
    Write-Host "Awaiting approval. Review the plan + tests/acceptance/, then:" -ForegroundColor Yellow
    Write-Host "  pwsh -File run-loop.ps1 -Approve" -ForegroundColor Yellow
    exit 0
  }
  git add -A *> $null; git commit -q -m "approve: human-approved plan + acceptance tests" --allow-empty *> $null
  $state.startCommit = (Get-Head)
  $state.phase = "build"; Set-LoopState $state
  Write-Host "Approved. Starting unattended build loop." -ForegroundColor Green
}

if ($state.phase -eq "done")    { Write-Host "Already DONE. Use -Replan to start over." -ForegroundColor Green; exit 0 }
if ($state.phase -eq "stalled") { Write-Host "STALLED previously. Inspect PROGRESS.md, then -Approve to resume or -Replan." -ForegroundColor Red
                                  $state.phase = "build"; Set-LoopState $state }

# =====================================================================
# PHASE: build  (with inline review at candidate-done)
# =====================================================================
Write-Host "=== BUILD LOOP (max $MaxIterations iterations) ===" -ForegroundColor Cyan
while ($state.iteration -lt $MaxIterations) {
  $state.iteration++
  Write-Host "--- iteration $($state.iteration) / $MaxIterations  (stall $($state.stall)/$StallLimit) ---" -ForegroundColor Cyan

  # 1) One fresh-context agent does ONE task. It edits files + updates memory,
  #    but does NOT commit — the supervisor owns commit/revert deterministically.
  Remove-Item -ErrorAction SilentlyContinue .loop/commit-msg.txt
  Invoke-Agent -PromptFile ".loop/prompt.iteration.md" -Label "iter$($state.iteration)" -Model $Model @common | Out-Null

  # 2) Deterministic commit gate. Commit only real changes (no empty commits) so that
  #    "no file changes" is a reliable stall signal. Record build/lint health in the
  #    message so the next fresh iteration knows it must fix the build first.
  $dirty = [bool](git status --porcelain)
  if ($dirty) {
    $msg = if (Test-Path ".loop/commit-msg.txt") { (Get-Content ".loop/commit-msg.txt" -Raw).Trim() } else { "iteration $($state.iteration)" }
    if ($msg.Length -gt 120) { $msg = $msg.Substring(0,120) }
    git add -A *> $null
    if (Invoke-Verify -Target Gate) {
      git commit -q -m "iter $($state.iteration): $msg" *> $null
      Write-Host "  gate: PASS (committed)" -ForegroundColor Green
    } else {
      git commit -q -m "iter $($state.iteration) [gate-fail]: $msg" *> $null
      Write-Host "  gate: FAIL — build/lint broken; next iteration must fix it first" -ForegroundColor Yellow
    }
    $state.stall = 0
  } else {
    Write-Host "  no file changes this iteration (no progress)" -ForegroundColor Yellow
    $state.stall++
  }

  # 3) Stall accounting: consecutive no-progress iterations => stop for human review.
  Set-LoopState $state
  if ($state.stall -ge $StallLimit) {
    $state.phase = "stalled"; Set-LoopState $state
    Write-Host "STALLED: $StallLimit iterations with no new commit. Stopping for human review." -ForegroundColor Red
    Write-Host "Inspect PROGRESS.md. Resume with -Approve, or -Replan." -ForegroundColor Red
    exit 2
  }

  # 4) Candidate-done? acceptance suite fully green AND no open tasks.
  if (-not (Test-OpenTasks) -and (Invoke-Verify -Target Accept)) {
    Write-Host "  candidate DONE — running independent reviewer pass..." -ForegroundColor Cyan
    Remove-Item -ErrorAction SilentlyContinue .loop/REVIEW-PASS
    Invoke-Agent -PromptFile ".loop/prompt.review.md" -Label "review$($state.iteration)" -Model $ReviewModel @common | Out-Null
    if ([bool](git status --porcelain)) { git add -A *> $null; git commit -q -m "review after iter $($state.iteration)" *> $null }

    if ((Test-Path ".loop/REVIEW-PASS") -and -not (Test-OpenTasks) -and (Invoke-Verify -Target Accept)) {
      $state.phase = "done"; Set-LoopState $state
      Write-Host ""
      Write-Host "==================================================" -ForegroundColor Green
      Write-Host " GOAL ACHIEVED — acceptance green + reviewer approved." -ForegroundColor Green
      Write-Host " Do your human acceptance testing now." -ForegroundColor Green
      Write-Host "==================================================" -ForegroundColor Green
      [console]::Beep(880,300)
      exit 0
    }
    Write-Host "  reviewer found gaps (added tasks) — continuing build loop." -ForegroundColor Yellow
  }
}

Write-Host "Reached MaxIterations ($MaxIterations) without full acceptance. Inspect PROGRESS.md." -ForegroundColor Red
exit 3
