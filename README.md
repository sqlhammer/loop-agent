# loop-agent

Give it a testable goal; it runs Claude in a loop until the goal is met, resuming itself
through subscription usage limits. This guide is only how to **use** it. To start a project:

## 1. Prerequisites (once)

- `claude` (Claude Code CLI) on PATH and signed in (`claude` interactively should show your plan).
- PowerShell 7+ (`pwsh`) and `git`. On macOS: `brew install --cask powershell`.
  (The harness is PowerShell but runs on Windows and macOS; the `claude setup-token`
  and `.env` steps below are identical on both.)
- **Headless auth token.** The loop runs `claude` non-interactively, which needs its own
  token even if interactive `claude` already works. Once:
  ```powershell
  claude setup-token                 # run in your own terminal, follow its prompts
  copy .env.example .env
  # edit .env: CLAUDE_CODE_OAUTH_TOKEN=<the token setup-token gave you>
  ```
  `.env` is gitignored — never commit it.
- **Run inside a sandbox / container / VM / disposable copy.** The loop runs unattended
  with permissions skipped, so don't point it at anything you can't afford it to change.

## 2. Set up a fresh project (edit two files)

1. **`GOAL.md`** — replace the template with your goal. Make every acceptance criterion
   something a test can pass/fail on. State your stack. (Keep un-testable "must feel nice"
   items in the manual checklist at the bottom.)
2. **`verify.ps1`** — set the three commands for your stack in `Invoke-Build`,
   `Invoke-Lint`, `Invoke-Test`. (Examples for Node/Python/Go/.NET are in the file.)
   Make `Invoke-Test` run your whole suite, including `tests/acceptance/`.

That's everything you edit. Leave `run-loop.ps1` and `.loop/` alone.

## 3. Run it

```powershell
# a) Generate the plan + acceptance tests, then it stops for your review:
pwsh -File run-loop.ps1

# b) Review IMPLEMENTATION_PLAN.md and tests/acceptance/. Fix the tests if they got
#    the goal wrong. Then start the unattended build loop:
pwsh -File run-loop.ps1 -Approve
```

On macOS/Linux you can use the `./run-loop.sh` launcher from a normal Terminal instead
(same flags, forwarded to `pwsh`): `./run-loop.sh` then `./run-loop.sh -Approve`.

Leave it running. It works one task at a time and, if it hits a usage limit, sleeps until
the window reopens and resumes on its own. When it prints **GOAL ACHIEVED**, do your own
acceptance testing (the manual checklist in `GOAL.md`).

## 4. The other commands

| Command | What it does |
|---------|--------------|
| `pwsh -File run-loop.ps1 -Status`  | Show current phase and counters. |
| `pwsh -File run-loop.ps1 -Replan`  | Throw away the plan/tests and regenerate from `GOAL.md`. |
| `pwsh -File run-loop.ps1 -Approve` | Resume the build loop (also used after a stall). |

If it stops with **STALLED**, read `PROGRESS.md` to see where it got stuck, then either
`-Approve` to resume or `-Replan` to start over.

## Useful flags

`-MaxIterations 300` · `-StallLimit 8` · `-Model sonnet` (build) · `-ReviewModel opus`
(plan + review; set to `sonnet` to save quota) · `-ResetBufferSeconds 120`.
