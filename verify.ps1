<#
  verify.ps1  —  ONE OF THE TWO FILES YOU EDIT PER PROJECT.

  It is the single source of truth for "done." The loop calls it two ways:

    verify.ps1 -Gate     Fast health check. "Is the tree healthy enough to commit?"
                         Build + lint only. Acceptance tests may still be RED here —
                         that's expected while the goal is being built up task by task.
                         Exit 0 = safe to commit this iteration's progress.

    verify.ps1 -Accept   Full definition of done. Build + lint + the ENTIRE test suite,
                         INCLUDING the REST acceptance tests below. Exit 0 = goal met.

  STACK: C# / .NET 10 server, runs in a Docker container, exposed as a public REST API.

  Invoke-Test starts the containerized server (fresh, empty SQLite DB), then drives every
  acceptance criterion in GOAL.md through real HTTP calls, then tears the container down.
  Until the server + endpoints exist, every acceptance check fails — the intended TDD
  "all red" starting state.

  Config:
    $env:EVENTMANAGER_BASE_URL   Base URL of the API (default http://localhost:8080).
                                 If a docker compose file exists it is (re)built and started;
                                 otherwise the server is expected to already be running here.

  Verbose test output (OFF by default so the unattended loop doesn't burn tokens):
    -ShowTests                   Switch. Prints the per-AC PASS/FAIL lines, failure
                                 diagnostics, and docker/startup chatter.
    $env:EVENTMANAGER_VERBOSE=1  Same, via environment (handy when you can't change the
                                 loop's invocation). Either one turns details on.
    Quiet mode still prints the final one-line tally and, on failure, the list of failed ACs.
#>
param([switch]$Gate, [switch]$Accept, [switch]$ShowTests)
$ErrorActionPreference = "Continue"

$BaseUrl = if ($env:EVENTMANAGER_BASE_URL) { $env:EVENTMANAGER_BASE_URL.TrimEnd('/') } else { 'http://localhost:8080' }

# Debug/verbose toggle — default OFF. On via -ShowTests or $env:EVENTMANAGER_VERBOSE.
$script:Verbose = [bool]$ShowTests -or (@('1','true','yes','on') -contains ("$env:EVENTMANAGER_VERBOSE").ToLower())

function Write-Dbg {
  param([string]$Msg, [string]$Color = 'Gray')
  if ($script:Verbose) { Write-Host $Msg -ForegroundColor $Color }
}

# ---------------------------------------------------------------------------
# Build / Lint
# ---------------------------------------------------------------------------
function Find-DotnetProject {
  Get-ChildItem -Path . -Recurse -File -Include *.sln,*.csproj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Select-Object -First 1
}

function Invoke-Build {
  if (-not (Find-DotnetProject)) {
    Write-Host "Invoke-Build: no .sln/.csproj found yet." -ForegroundColor Yellow
    return $false
  }
  dotnet build 2>&1 | Write-Host
  return ($LASTEXITCODE -eq 0)
}

function Invoke-Lint {
  if (-not (Find-DotnetProject)) { return $true }
  dotnet format --verify-no-changes 2>&1 | Write-Host
  return ($LASTEXITCODE -eq 0)
}

# ---------------------------------------------------------------------------
# REST helpers
# ---------------------------------------------------------------------------
function Invoke-Api {
  param([string]$Method, [string]$Path, [object]$Body)
  $params = @{ Method = $Method; Uri = "$BaseUrl$Path"; SkipHttpErrorCheck = $true; TimeoutSec = 30 }
  if ($null -ne $Body) {
    $params.Body = ($Body | ConvertTo-Json -Depth 10)
    $params.ContentType = 'application/json'
  }
  try {
    $resp = Invoke-WebRequest @params
    $json = $null
    if ($resp.Content) { try { $json = $resp.Content | ConvertFrom-Json -ErrorAction Stop } catch { $json = $null } }
    return [pscustomobject]@{ Status = [int]$resp.StatusCode; Json = $json; Raw = [string]$resp.Content }
  } catch {
    return [pscustomobject]@{ Status = 0; Json = $null; Raw = "$_" }
  }
}

function AsArray { param($x) if ($null -eq $x) { @() } elseif ($x -is [array]) { $x } else { @($x) } }

function Get-One { param($json) $a = AsArray $json; if ($a.Count -ge 1) { $a[0] } else { $null } }

function Has-Props {
  param($obj, [string[]]$Props)
  if ($null -eq $obj) { return $false }
  $names = @($obj.PSObject.Properties.Name)
  foreach ($p in $Props) { if ($names -notcontains $p) { return $false } }
  return $true
}

function Get-Id {
  param($json)
  if ($null -eq $json) { return $null }
  if ($json -is [int] -or $json -is [long]) { return [int]$json }
  foreach ($k in 'id','event_id','match_id','competitor_id','bracket_id') {
    if ($json.PSObject.Properties.Name -contains $k) { return $json.$k }
  }
  return $null
}

# ---------------------------------------------------------------------------
# Test tracking
# ---------------------------------------------------------------------------
$script:Pass = 0
$script:Fail = 0
$script:Failed = @()

function Check {
  param([string]$Ac, [string]$Name, [bool]$Cond, [string]$Detail = '')
  if ($Cond) { $script:Pass++ } else { $script:Fail++; $script:Failed += $Ac }

  # Per-AC lines are verbose-only so the loop doesn't pay for them every iteration.
  if ($script:Verbose) {
    $result = if ($Cond) { 'PASS' } else { 'FAIL' }
    $color  = if ($Cond) { 'Green' } else { 'Red' }
    $label  = "[{0,-5}] {1}" -f $Ac, $Name       # [AC#] <test description> .... RESULT
    $pad    = [Math]::Max(1, 78 - $label.Length)
    Write-Host ("{0} {1} {2}" -f $label, ('.' * $pad), $result) -ForegroundColor $color
    if (-not $Cond -and $Detail) {
      Write-Host "         -> $Detail" -ForegroundColor DarkYellow
    }
  }
}

# ---------------------------------------------------------------------------
# Container lifecycle
# ---------------------------------------------------------------------------
$script:ComposeFile = $null

function Find-ComposeFile {
  foreach ($p in @('docker-compose.yml','docker-compose.yaml','compose.yml','compose.yaml',
                   'src/docker-compose.yml','deploy/docker-compose.yml')) {
    if (Test-Path $p) { return (Resolve-Path $p).Path }
  }
  return $null
}

function Wait-ForServer {
  param([int]$TimeoutSec = 90)
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  while ((Get-Date) -lt $deadline) {
    $r = Invoke-Api GET '/event/'
    if ($r.Status -ne 0) { return $true }   # any HTTP response means it's up
    Start-Sleep -Seconds 2
  }
  return $false
}

function Start-Server {
  $script:ComposeFile = Find-ComposeFile
  if ($script:ComposeFile) {
    Write-Dbg "Starting stack (fresh, empty DB) via $script:ComposeFile ..." 'Cyan'
    if ($script:Verbose) {
      docker compose -f $script:ComposeFile down -v 2>&1 | Write-Host
      docker compose -f $script:ComposeFile up -d --build 2>&1 | Write-Host
    } else {
      docker compose -f $script:ComposeFile down -v 2>&1 | Out-Null
      docker compose -f $script:ComposeFile up -d --build 2>&1 | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { Write-Dbg "docker compose up failed." 'Red'; return $false }
  } else {
    Write-Dbg "No docker compose file found; expecting a server already running at $BaseUrl" 'Yellow'
  }
  return (Wait-ForServer)
}

function Stop-Server {
  if ($script:ComposeFile) {
    if ($script:Verbose) { docker compose -f $script:ComposeFile down -v 2>&1 | Write-Host }
    else                 { docker compose -f $script:ComposeFile down -v 2>&1 | Out-Null }
  }
}

# ---------------------------------------------------------------------------
# Acceptance criteria — ONE FUNCTION PER AC in GOAL.md.
# Each is self-contained (makes its own REST call(s) and records its own Check).
# They share the single running container, so they are order-dependent: the
# empty-state GETs must run before anything is created, and the seeded-state
# reads must run after their creators. Run-AcceptanceTests fixes that order.
# ---------------------------------------------------------------------------

function Test-Ac1 {   # GIVEN empty db, GET /event/ -> 200 + empty list
  $r = Invoke-Api GET '/event/'
  Check 'AC1' 'GET /event/ on empty db -> 200 + empty list' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 0) "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac3 {   # GIVEN empty db, GET /match/ -> 200 + empty list
  $r = Invoke-Api GET '/match/'
  Check 'AC3' 'GET /match/ on empty db -> 200 + empty list' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 0) "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac5 {   # GIVEN empty db, GET /bracket/ -> 200 + empty list
  $r = Invoke-Api GET '/bracket/'
  Check 'AC5' 'GET /bracket/ on empty db -> 200 + empty list' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 0) "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac7 {   # GIVEN empty db, GET /competitor/ -> 200 + empty list
  $r = Invoke-Api GET '/competitor/'
  Check 'AC7' 'GET /competitor/ on empty db -> 200 + empty list' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 0) "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac9 {   # POST /create_event/ -> 200 + new event id (creates "Test Event 1" = event 1)
  $r = Invoke-Api POST '/create_event/' @{ name = 'Test Event 1' }
  $eventId = Get-Id $r.Json
  Check 'AC9' 'POST /create_event/ -> 200 + new event id' `
    ($r.Status -eq 200 -and $null -ne $eventId) "status=$($r.Status) id=$eventId"
}

function Test-Ac2 {   # GIVEN event id 1 exists, GET /event/1/ -> 200 + one event with all fields
  $r = Invoke-Api GET '/event/1/'
  $ev = Get-One $r.Json
  Check 'AC2' 'GET /event/1/ -> 200 + one event with all data points' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 1 -and (Has-Props $ev @('id','name'))) `
    "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac10 {  # POST /create_event/ with a duplicate name -> 409 + "already exists"
  $r = Invoke-Api POST '/create_event/' @{ name = 'Test Event 1' }
  Check 'AC10' 'POST /create_event/ duplicate name -> 409 + "already exists"' `
    ($r.Status -eq 409 -and $r.Raw -match 'already exists') "status=$($r.Status) body=$($r.Raw)"
}

function Test-Ac11 {  # POST /create_match/ -> 200 + match id and match type (creates match 1)
  $r = Invoke-Api POST '/create_match/' @{ type = 'kata'; event_id = 1 }
  $matchId = Get-Id $r.Json
  Check 'AC11' 'POST /create_match/ -> 200 + match id and match type' `
    ($r.Status -eq 200 -and $null -ne $matchId -and $r.Raw -match 'kata') "status=$($r.Status) id=$matchId body=$($r.Raw)"
}

function Test-Ac4 {   # GIVEN match id 1 exists, GET /match/1/ -> 200 + one match with all fields
  $r = Invoke-Api GET '/match/1/'
  $m = Get-One $r.Json
  Check 'AC4' 'GET /match/1/ -> 200 + one match with all data points' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 1 -and (Has-Props $m @('id','type'))) `
    "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac12 {  # POST /create_match/ with a non-whitelisted type -> 400 + "invalid match type"
  $r = Invoke-Api POST '/create_match/' @{ type = 'BJJ'; event_id = 1 }
  Check 'AC12' 'POST /create_match/ type=BJJ -> 400 + "invalid match type"' `
    ($r.Status -eq 400 -and $r.Raw -match 'invalid match type') "status=$($r.Status) body=$($r.Raw)"
}

function Test-Ac13 {  # POST /create_competitor/ -> 200 + competitor data + new id (creates competitor 1)
  $r = Invoke-Api POST '/create_competitor/' (New-CompetitorBody 'Test comp 1')
  $compId = Get-Id $r.Json
  Check 'AC13' 'POST /create_competitor/ -> 200 + competitor data + new id' `
    ($r.Status -eq 200 -and $null -ne $compId -and $r.Raw -match 'Test comp 1') "status=$($r.Status) id=$compId"
}

function Test-Ac8 {   # GIVEN competitor id 1 exists, GET /competitor/1/ -> 200 + one competitor with all fields
  $r = Invoke-Api GET '/competitor/1/'
  $c = Get-One $r.Json
  Check 'AC8' 'GET /competitor/1/ -> 200 + one competitor with all data points' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 1 -and `
     (Has-Props $c @('id','name','styles','birthdate','last_weigh_in'))) `
    "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

function Test-Ac14 {  # GIVEN event 1 + 3 matches + 8 competitors, POST /generate_bracket/ -> 200 + bracket + id
  $r = Invoke-Api POST '/generate_bracket/' @{ event_id = 1 }
  $bracketId = Get-Id $r.Json
  Check 'AC14' 'POST /generate_bracket/ -> 200 + bracket data + new id' `
    ($r.Status -eq 200 -and $null -ne $bracketId) "status=$($r.Status) id=$bracketId body=$($r.Raw)"
}

function Test-Ac6 {   # GIVEN a generated bracket, GET /bracket/1/ -> 200 + one bracket w/ competitor groupings per match
  $r = Invoke-Api GET '/bracket/1/'
  $bk = Get-One $r.Json
  $hasGroupings = ($null -ne $bk) -and ((Has-Props $bk @('matches')) -or (Has-Props $bk @('groupings')))
  Check 'AC6' 'GET /bracket/1/ -> 200 + one bracket with competitor groupings per match' `
    ($r.Status -eq 200 -and (AsArray $r.Json).Count -eq 1 -and (Has-Props $bk @('id')) -and $hasGroupings) `
    "status=$($r.Status) count=$((AsArray $r.Json).Count)"
}

# Not an AC — seeds the remaining fixtures AC14/AC6 need: 3 matches + 8 competitors total.
# AC11 already made match 1; AC13 already made competitor 1.
function Add-BracketSeedData {
  Invoke-Api POST '/create_match/' @{ type = 'combat'; event_id = 1 } | Out-Null   # match 2
  Invoke-Api POST '/create_match/' @{ type = 'kata';   event_id = 1 } | Out-Null   # match 3
  for ($i = 2; $i -le 8; $i++) {
    Invoke-Api POST '/create_competitor/' (New-CompetitorBody "Test comp $i") | Out-Null
  }
}

function New-CompetitorBody {
  param([string]$Name)
  @{
    name          = $Name
    styles        = @('karate','BJJ')
    birthdate     = '09-01-2000'
    last_weigh_in = @{ weight = 160.4; units = 'lbs' }
  }
}

# Wrapper: runs every AC test in the order the shared container state requires.
function Run-AcceptanceTests {
  # Empty-database reads first (must precede any create).
  Test-Ac1; Test-Ac3; Test-Ac5; Test-Ac7
  # Event: create, read back, reject duplicate.
  Test-Ac9; Test-Ac2; Test-Ac10
  # Match: create, read back, reject invalid type.
  Test-Ac11; Test-Ac4; Test-Ac12
  # Competitor: create, read back.
  Test-Ac13; Test-Ac8
  # Seed the rest, then bracket: generate, read back.
  Add-BracketSeedData
  Test-Ac14; Test-Ac6
}

function Invoke-Test {
  $script:Pass = 0
  $script:Fail = 0
  $script:Failed = @()
  $script:ComposeFile = $null

  Write-Dbg "=== REST acceptance tests against $BaseUrl ===" 'Cyan'
  if (-not (Start-Server)) {
    # No server yet = every AC is red. This is the intended TDD starting state.
    Stop-Server
    Write-Host "Acceptance: server never became reachable at $BaseUrl -- all criteria fail." -ForegroundColor Red
    return $false
  }

  try {
    Run-AcceptanceTests
  } finally {
    Stop-Server
  }

  # Always print the one-line tally (cheap). Details are behind -ShowTests.
  Write-Host "Acceptance: $script:Pass passed, $script:Fail failed" -ForegroundColor Cyan
  if ($script:Fail -gt 0) {
    Write-Host ("Failed: {0}" -f ($script:Failed -join ', ')) -ForegroundColor Red
    if (-not $script:Verbose) { Write-Host "(re-run with -ShowTests or set EVENTMANAGER_VERBOSE=1 for per-test details)" -ForegroundColor DarkGray }
  }
  return ($script:Fail -eq 0 -and $script:Pass -gt 0)
}

# ---- framework wiring below: do not edit -------------------------------
if ($Gate) {
  if ((Invoke-Build) -and (Invoke-Lint)) { exit 0 } else { exit 1 }
}
if ($Accept) {
  if ((Invoke-Build) -and (Invoke-Lint) -and (Invoke-Test)) { exit 0 } else { exit 1 }
}
Write-Host "Usage: verify.ps1 -Gate | -Accept"
exit 1
