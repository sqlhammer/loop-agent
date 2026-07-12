<#
  verify.ps1  —  ONE OF THE TWO FILES YOU EDIT PER PROJECT.

  It is the single source of truth for "done." The loop calls it two ways:

    verify.ps1 -Gate     Fast health check. "Is the tree healthy enough to commit?"
                         Build + lint only. Acceptance tests may still be RED here —
                         that's expected while the goal is being built up task by task.
                         Exit 0 = safe to commit this iteration's progress.

    verify.ps1 -Accept   Full definition of done. Build + lint + the ENTIRE test suite,
                         INCLUDING tests/acceptance/. Exit 0 = the goal is met.

  TO ADAPT THIS TO A NEW PROJECT: edit only the three function bodies below.
  Return $true on success, $false on failure. Delete a check you don't have
  (e.g. no linter) by making its function `return $true`.

  Examples by stack:
    Node:    Build = 'npm run build';  Lint = 'npm run lint';  Test = 'npm test'
    Python:  Build = 'python -m compileall .'; Lint = 'ruff check .'; Test = 'pytest -q'
    Go:      Build = 'go build ./...'; Lint = 'go vet ./...'; Test = 'go test ./...'
    .NET:    Build = 'dotnet build'; Lint = 'dotnet format --verify-no-changes'; Test = 'dotnet test'
#>
param([switch]$Gate, [switch]$Accept)
$ErrorActionPreference = "Continue"

$Solution = Join-Path $PSScriptRoot "EventManager.slnx"

function Invoke-Build {
  dotnet build $Solution -c Release --nologo 2>&1 | Write-Host
  return ($LASTEXITCODE -eq 0)
}

function Invoke-Lint {
  # Style/format gate. Agents must run `dotnet format EventManager.slnx` before finishing.
  dotnet format $Solution --verify-no-changes 2>&1 | Write-Host
  return ($LASTEXITCODE -eq 0)
}

function Invoke-Test {
  # MUST run the full suite, including tests/acceptance/. `dotnet test` discovers
  # every test project in the solution (xUnit acceptance tests live under tests/acceptance/).
  dotnet test $Solution -c Release --nologo 2>&1 | Write-Host
  return ($LASTEXITCODE -eq 0)
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
