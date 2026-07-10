# Loop Engineering
### How AI-first teams orchestrate autonomous coding loops — and a reference architecture you can reuse

*A teaching paper + design document*
Prepared 2026-07-09 · Target runtime: Claude Code on a Claude subscription, Windows/PowerShell

---

## 0. Provenance & honesty note

This paper is grounded in a multi-source research sweep of primary and practitioner sources (Anthropic's Claude Code docs, Geoffrey Huntley's Ralph repo, GitHub Spec Kit, Amazon Kiro, Steve Yegge's writing on agent fleets/Beads/Gas Town, Sourcegraph, Will Larson on compaction, and the open-source auto-resume tooling ecosystem). The research harness completed its **search** and **claim-extraction** phases (15 sources, 75 extracted claims) but its **adversarial-verification** phase was aborted mid-run when the account hit a Claude usage limit (`You've hit your session limit · resets 12:10am`). That is not a metaphor I added for color — it is what actually happened, and it is exactly the failure mode Section 7 exists to defeat.

Consequently: claims below are **cross-corroborated across independent sources** and consistent with official documentation, but were not machine-verified end-to-end. Where a specific number or mechanism is load-bearing and I could not independently confirm it, I mark it **[unverified]**. Treat the architecture as sound and the incidental figures as directional.

---

## 1. The one idea that matters

> **The loop is the unit of engineering, not the model.**

The single most important shift in how the best AI-first teams work is that they stopped trying to get one heroic, maximally-capable model call to produce a finished result, and instead engineered a **loop** whose *individual iterations are cheap, fresh, and disposable*, but whose *aggregate behavior converges* on a goal.

Every source in the research — Huntley's "Ralph," Yegge's "fleets," Anthropic's own best-practices docs, the codecentric and one-session-per-task writeups — independently arrives at the same framing: reliability comes from **workflow design around the loop**, not from the raw intelligence of a single inference. As one source put it, the field is "shifting away from reliance on a single frontier 'genius' model toward workflow design where the iterative loop itself does the heavy lifting."

If you internalize nothing else: **you are not prompting a model, you are building a control system.** The model is one component inside it. The other components — the goal contract, the external memory, the verifier, the supervisor — are what make it run unattended.

This reframing is what makes all five of your requirements achievable at once. Let's build to them.

---

## 2. The anatomy of an autonomous loop (six pillars)

Every durable autonomous coding system in the research decomposes into the same six pillars. Learn these as a checklist; the reference architecture in Section 4 is just a specific, opinionated wiring of them.

| # | Pillar | Question it answers | Your requirement it serves |
|---|--------|---------------------|----------------------------|
| 1 | **Goal contract** | "What does done mean, testably?" | #2 (human inputs a testable goal) |
| 2 | **Fresh-context iteration** | "How do we avoid the model rotting as context fills?" | #3 (context self-management) |
| 3 | **External memory** | "How does an amnesiac agent remember across processes?" | #3, #4 |
| 4 | **Verifier & backpressure** | "How do we know it's actually done, and stop it from lying?" | #2, #4, #6 |
| 5 | **Orchestration substrate** | "How do we run this across processes/machines?" | #3, #4 |
| 6 | **Supervisor / survival** | "How does it survive limits and resume itself?" | #5 (usage-limit auto-resume) |

### Pillar 1 — The goal contract (spec-driven development)

The failure mode of naive autonomous agents is that they **guess at unstated requirements** and drift. GitHub's Spec Kit team states this directly: vague prompts cause agent failure because the model must invent the parts you didn't specify. Every serious system therefore front-loads a **specification** that is precise enough to be *checked*.

The industry has converged on a small family of spec-driven workflows:

- **GitHub Spec Kit**: `Constitution → Specify → Plan → Tasks → Implement`. Checklists act as a "definition of done" per step; an immutable "constitution" applies to every change. Agent-agnostic (works with Claude Code, Copilot, Gemini CLI).
- **Amazon Kiro**: a fixed `Requirements → Design → Tasks` pipeline that emits three markdown docs per feature, with **acceptance criteria written in GIVEN/WHEN/THEN form** and every task traced back to a requirement number. It also keeps a persistent "steering" memory bank (`product.md`, `structure.md`, `tech.md`).
- **Ralph (Huntley)**: a human/LLM spec-writing phase produces `specs/*.md`, then the loop does gap-analysis between specs and code.

The critical design insight, and the one that makes your requirement #2 real: **the acceptance criteria must be executable, not prose.** GIVEN/WHEN/THEN is nice for humans, but the loop needs a command that exits `0` or non-`0`. The single highest-leverage thing a human does in this whole system is convert "what I want" into "a test suite that is red now and must be green at the end."

A caution from the research (Martin Fowler's SDD survey): spec-driven tooling can over-produce — Kiro generated *16 acceptance criteria for a small bug fix* — and code regeneration from an identical spec is **non-deterministic**, so specs-as-the-only-source-of-truth is not yet reliable. The practical takeaway: **spec + executable tests, with the tests as the arbiter.** The spec guides; the tests decide.

### Pillar 2 — Fresh context per iteration

This is the load-bearing idea behind the "Ralph Wiggum" technique, and the research is unusually consistent on the underlying mechanism.

Coding-agent output quality **degrades as the context window fills**. Multiple sources describe measurable "zones": clean output at **0–40%** fill, corner-cutting at **40–70%**, and sloppy/rushed/hallucinatory output **beyond 70%** [unverified as precise thresholds, but the monotonic degradation is universally reported]. The mechanism is mundane — attention cost scales quadratically with sequence length, and long histories bury the relevant tokens.

The Ralph answer is brutally simple: **throw the context away every iteration and start fresh.** The canonical form is a shell loop:

```bash
while :; do cat PROMPT.md | claude ; done
```

Each iteration is a **new process with an empty context window**. It reads its instructions and state from disk, does *one* unit of work, writes results back to disk, and dies. The next iteration is born clean. Context never rots because context never accumulates.

Huntley budgets context explicitly inside a single iteration too: treat ~176K of a 200K window as usable, do **one task per iteration**, and spawn subagents for expensive exploration so their context is "garbage-collected" when they return [unverified specific numbers]. Anthropic's own docs describe the same toolkit from the harness side: automatic **compaction**, `/clear` resets, **subagents with separate context windows**, and `CLAUDE.md` instructions that survive summarization.

An important nuance the research surfaces (codecentric, one-session-per-task): **compaction is not a substitute for fresh context.** Compaction compresses *everything indiscriminately* and its summaries "pollute working memory" and can't be relied on to preserve your rules. That's why durable knowledge belongs in **external memory files**, not in the conversation you're hoping compaction will keep. Which is Pillar 3.

### Pillar 3 — External memory (the disk is the brain)

If every iteration is an amnesiac, memory must live **outside** the context window, on disk, in a form that survives process death and is shared across parallel processes. This is how agents "manage themselves across multiple processes" (your requirement #3). The research shows a maturity ladder:

1. **Markdown plan files** — `IMPLEMENTATION_PLAN.md`, `PROGRESS.md`, `TODO.md`, `specs/*.md`. Simplest, git-friendly, human-readable. This is Ralph's default and it works.
2. **Git itself as memory** — commit after every green task. The commit history *is* the durable ledger; a broken iteration is `git revert`-ed. The Tmux-Orchestrator project enforces a **30-minute commit rule** so autonomous runs never lose more than half an hour of work.
3. **Issue-tracker-as-database (Beads)** — Yegge's critique of plain markdown is sharp and worth heeding: agents keep plans in "similarly named sibling markdown files and **gradually drift off-plan** rather than failing immediately." His answer, **Beads**, stores issues as **JSONL lines committed to git** — giving both queryable database semantics *and* version-control distribution. Parallel agents query the same logical DB, avoid collisions by checking `in_progress` status before claiming work, and **auto-file "discovered work"** as new issues instead of losing mid-task observations when context is exhausted.

The pattern underneath all three: **after each unit of work, the agent externalizes state (what it did, what it learned, what's left) before it dies, and re-reads that state on the next iteration.** Will Larson's internal agent takes this to its logical end — it converts any message over 10K tokens into a "virtual file" (keeping only the first 1K in context), and even saves the *discarded context window itself* as a retrievable file. The disk is the brain; the context window is just a scratchpad.

### Pillar 4 — The verifier and "backpressure" (why loops converge)

An unattended loop without a verifier doesn't converge — it wanders, and worse, it will happily declare victory (reward-hacking). Convergence comes from a **closed loop against a runnable check.**

Anthropic states this as official best practice: **close agent loops with a runnable check** — tests, a build exit code, a linter, a screenshot diff — and says this is precisely what lets unattended sessions finish *correctly* rather than needing a human to eyeball them. The check is the arbiter of "done."

Huntley's term for the surrounding discipline is **backpressure**: mechanisms that *refuse to let bad work proceed*.
- Failing tests or a broken build **block the commit**.
- A task cannot be marked complete until validation passes.
- Fresh context each iteration **prevents circular failure patterns** (a poisoned context can't compound across iterations).
- Quality is **ratcheted** via git: green commits stick, red work is reverted.

Two structural guardrails from the research make this robust:

- **A separate reviewer/critic with fresh context.** Anthropic recommends a Writer/Reviewer split where a *fresh-context session reviews the diff* — because a reviewer that didn't write the code isn't biased toward defending it. This is your defense against reward-hacking: the writer optimizes to pass tests; an independent critic checks that the tests weren't gamed and the spec was actually met.
- **Hard stops.** Loops need `--max-iterations` and an explicit **completion promise** (a programmatic definition of done) so they don't run forever burning quota. Claude Code's Stop hook can *block a turn from ending until a check passes*, but the harness overrides the hook after a bounded number of consecutive blocks so a wedged loop can't run infinitely [the "override after N blocks" behavior is real; the exact N is **unverified**].

The rule to remember: **"done" must be defined by something the agent cannot talk its way past.** A passing command, checked by an independent process, is that something. Self-assessment ("I believe this is complete") is not.

### Pillar 5 — Orchestration across processes and machines

Running one loop is easy. The research shows three escalating patterns for running *many*, which is how you get both parallelism and process-isolation:

- **Git worktrees (the dominant pattern).** Each agent gets its own directory + branch + working state, all sharing one `.git`. This prevents branch collisions and cross-agent context bleed and is "the dominant mechanism practitioners use to run multiple coding agents in parallel." Practical ceiling on a laptop: **~4–5 parallel agents** (RAM-bound first, then CPU at 6–10, then context/quota beyond 10). One MCP server *per worktree*, scoped to that directory, to avoid shared-state bleed.
- **tmux as the substrate (and self-scheduling).** Persistent tmux sessions host long-running agent CLIs. The Tmux-Orchestrator project runs agents "24/7," with agents **self-scheduling their own wake-ups** via a `schedule_with_note.sh` script and messaging each other via `send-claude-message.sh`. It uses a three-tier **orchestrator → project-manager → engineer** hierarchy specifically to keep each agent's context "small and role-focused." (On Windows, the equivalent substrate is a detached PowerShell process, a Windows Scheduled Task, or WSL+tmux — see Section 5.)
- **Native agent teams / fleets.** Claude Code has an experimental **agent-teams** mode (`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS`, off by default) where a lead coordinates independent Claude instances via a **shared on-disk task list** (`~/.claude/tasks/{team}/`) with three states, dependency tracking, and **file-locking to prevent race conditions** on task claims. Each teammate runs in its **own context window** and does *not* inherit the lead's history — a fresh-context-per-agent design. Recommended size: **3–5 teammates, 5–6 tasks each**; cost scales linearly per teammate. Yegge's "fleets" and "Gas Town" ("Kubernetes for agents") are the maximalist vision of this — supervisory agents managing pods of workers, escalating to a human only when stuck, decomposing work into tiny handoff-able units ("MEOW") for ephemeral fresh-context workers.

**Recommendation for a subscription user:** default to **serial, single-loop, fresh-context** execution and reach for worktree parallelism only when tasks are genuinely independent. Parallelism multiplies quota burn linearly, and on a subscription quota is your scarcest resource (Section 6). Scott Chacon's "Grit" burned **~45 billion tokens** across parallel agents — great if you're on API billing, ruinous on a Pro plan.

### Pillar 6 — The supervisor (surviving usage limits)

This is the pillar that turns "runs for a few hours" into "runs until done, across days" — your requirement #5 — and it's the least glamorous and most important for subscription users.

Claude subscriptions enforce **rolling usage windows** (a ~5-hour window, plus weekly caps). When you hit one, Claude Code prints a message like:

```
You've hit your session limit · resets 12:10am (America/New_York)
5-hour limit reached · resets 3pm
```

and stops. An unsupervised overnight run then **wastes every hour between the reset and when you wake up**. This exact gap is the subject of an open Claude Code feature request (#36320 and a cluster of related issues) asking for built-in `--auto-resume`.

Because it isn't built in yet, a small ecosystem of **auto-resume supervisors** has grown up, and they all share one design:

1. **Run the agent inside a wrapper** (historically tmux; on Windows, a supervising PowerShell process).
2. **Poll the output** for the known limit-message patterns.
3. **Parse the reset time** out of the message.
4. **Sleep until reset + a safety buffer.**
5. **Re-probe** to confirm the window actually reset, then **resume** — resend "continue" (interactive) or re-invoke the loop (headless).

Named examples from the research: **claude-auto-retry** (zero-dep npm, tmux, ~5s polling, exponential backoff, also handles API 529/5xx overloads and false-positive safeguards), **claude-auto-resume** (Windows-specific: sends a tiny probe prompt, parses `resets 3:45pm`, sleeps to reset+buffer, re-probes before resuming), and a ~350-line "Smart Resume" bash/zsh wrapper meant to be left running overnight. One operational subtlety worth budgeting for: **each auto-resume probe sends a real prompt and consumes quota from the new window.** Keep probes tiny.

The reference architecture in Section 5 gives you a concrete PowerShell supervisor implementing this loop.

---

## 3. How the leading teams actually do it (field notes)

A quick tour of who does what, so the patterns above have faces:

- **Anthropic (internal + official docs).** The Claude Code best-practices docs *are* a loop-engineering manual: close loops with runnable checks; manage context via compaction/`/clear`/subagents/`CLAUDE.md`; fan out headless `claude -p` invocations over a generated task list (e.g., migrating 2,000 files) with `--allowedTools` restricting permissions for unattended safety; use multi-session architectures (worktrees, agent teams, Writer/Reviewer split). This is the most authoritative primary source and the backbone of the recommendation below.
- **Geoffrey Huntley — "Ralph."** The canonical minimal autonomous loop: fresh context per iteration, state in files + git, PLANNING vs BUILDING modes, `--dangerously-skip-permissions` inside a **sandbox** (Docker / Fly / E2B). Proof it works: a case study built a 16-phase app end-to-end in ~4 hours for ~€70 of API spend. There is now an *ecosystem* of Ralph-style runners (ralph-claude-code, ralph-orchestrator, ralph-tui, ralphy, etc.) and reportedly an official Anthropic `ralph-wiggum` plugin implementing the loop via a Stop hook with mandatory `--max-iterations` and a completion promise [plugin existence **unverified**].
- **Steve Yegge — fleets, Beads, Gas Town.** The scaling vision: six overlapping "waves" ending in developers running **100+ agents** coordinated by supervisory agents. His concrete contributions are the memory critique (markdown drifts → use a git-backed issue DB, **Beads**) and the orchestration metaphor (**Gas Town** = "Kubernetes for agents," tiny handoff-able work units).
- **GitHub Spec Kit / Amazon Kiro.** The disciplined front-end: turn intent into a checked spec with executable acceptance criteria and human review checkpoints before code.
- **Sourcegraph / practitioner blogs (Larson, one-session-per-task, codecentric, Vaughan).** The context-management engine room: compaction internals, virtual-file external memory, "one task per fresh session," and the 0–40/40–70/70+ degradation zones.

The synthesis across all of them is remarkably coherent, which is why the reference architecture can be simple.

---

## 4. The reference architecture ("the recommended solution")

Here is the design I recommend: a **reusable, goal-in / acceptance-test-out, self-managing, self-resuming loop.** It's deliberately built from the six pillars using tools you already have on a Claude subscription. I'll describe it abstractly here and give the concrete Windows implementation in Section 5.

### 4.1 The human interface (the entire contract)

The human provides exactly two things and nothing else:

1. **`GOAL.md`** — a clear, testable goal plus **acceptance criteria expressed as, or backed by, an executable check.** Prose intent is fine *as long as* it resolves to a command that passes/fails. Example shape:
   ```
   # Goal
   A CLI todo app that stores tasks in SQLite.
   # Done when (acceptance = `npm test` green AND `npm run build` exits 0)
   - GIVEN an empty db, WHEN I run `todo add "x"`, THEN `todo list` shows "x"
   - GIVEN a task, WHEN I run `todo done 1`, THEN it is marked complete
   - ... (each bullet has a corresponding test in tests/acceptance/)
   ```
2. **Acceptance testing at the end.** When the loop reports "green," the human does real acceptance testing — the human judgment the system deliberately does *not* automate.

That's the whole human job. Everything between is autonomous.

### 4.2 The control flow

```
                    ┌──────────────────────────────────────────────┐
        HUMAN  ────► │  GOAL.md  +  acceptance tests (red today)    │
                    └───────────────────────┬──────────────────────┘
                                            │  (once)
                                   ┌────────▼─────────┐
                                   │  PLAN PHASE      │  fresh agent
                                   │  goal → specs/   │  → writes IMPLEMENTATION_PLAN.md
                                   │       → task ledger │   + task ledger (issues)
                                   └────────┬─────────┘
                                            │
   ┌────────────────────────  OUTER SUPERVISOR LOOP  ───────────────────────────┐
   │  (PowerShell wrapper: runs iterations, survives usage limits)              │
   │                                                                            │
   │   ┌──────────────────────────  ONE ITERATION  ───────────────────────┐    │
   │   │  fresh `claude -p` process, empty context                        │    │
   │   │   1. read IMPLEMENTATION_PLAN.md + ledger + PROGRESS.md          │    │
   │   │   2. pick ONE ready task (mark in_progress)                      │    │
   │   │   3. implement it (spawn subagents for exploration)             │    │
   │   │   4. run VERIFIER: tests + build + lint                          │    │
   │   │        ├─ green → git commit; mark task done; append PROGRESS    │    │
   │   │        └─ red   → revert; log failure + hypothesis to ledger     │    │
   │   │   5. process exits (context discarded)                          │    │
   │   └──────────────────────────────┬───────────────────────────────────┘    │
   │                                  │                                         │
   │              ┌───────────────────▼────────────────────┐                    │
   │              │  TERMINATION CHECK                      │                    │
   │              │  acceptance suite fully green           │                    │
   │              │       AND no open tasks?                │                    │
   │              │   yes → EXIT (notify human) ────────────┼──► HUMAN acceptance│
   │              │   no  → loop again                      │                    │
   │              │  (guardrails: max-iterations, stall     │                    │
   │              │   detector, periodic fresh-context      │                    │
   │              │   REVIEWER pass on the diff)            │                    │
   │              └───────────────────┬────────────────────┘                    │
   │                                  │                                         │
   │        ┌─────────────────────────▼─────────────────────────┐               │
   │        │  USAGE-LIMIT INTERCEPT (wraps every iteration)     │               │
   │        │  iteration output ~ "hit your session limit ·      │               │
   │        │  resets HH:MM" ?                                   │               │
   │        │    → parse reset time → sleep until reset+buffer   │               │
   │        │    → re-probe to confirm window open → resume      │               │
   │        └────────────────────────────────────────────────────┘               │
   └────────────────────────────────────────────────────────────────────────────┘
```

### 4.3 The repository layout (external memory made concrete)

```
project/
├─ GOAL.md                     # human-authored: the testable goal + acceptance criteria
├─ tests/acceptance/           # human- or agent-authored executable acceptance tests
├─ specs/                      # agent-authored detailed specs (spec-driven front-end)
├─ IMPLEMENTATION_PLAN.md      # the living plan: ordered tasks, one active at a time
├─ PROGRESS.md                 # append-only journal: what happened each iteration
├─ ledger.jsonl               # optional Beads-style issue DB (id, status, deps)
├─ CLAUDE.md                   # durable rules the agent must always honor (survives across iterations)
├─ .loop/
│  ├─ prompt.iteration.md     # the fixed per-iteration instruction (the "Ralph prompt")
│  ├─ prompt.plan.md          # the one-time planning instruction
│  ├─ verify.ps1              # the single source of truth for "done" (tests+build+lint)
│  └─ state.json             # iteration count, last reset time, stall counter
├─ run-loop.ps1               # the supervisor (Section 5)
└─ src/  ...                   # the actual product
```

Everything the loop needs to survive process death and usage limits is a file in git. Any iteration, on any machine, at any time, can `git pull` and reconstruct the entire mental state from disk. That is the whole trick behind requirements #3 and #4.

---

## 5. Concrete implementation for Claude subscriptions on Windows

This section makes the architecture runnable with Claude Code on your setup. Three ways to drive the loop, in increasing order of "just works":

### 5.1 Option A — The `/loop` skill (fastest to try, you already have it)

Your Claude Code install exposes a **`/loop`** skill and a **`schedule`** skill. `/loop` runs a prompt or slash command on a recurring interval (or self-paced). This is the lowest-effort way to get a loop going:

```
/loop  <the per-iteration instruction, pointing at IMPLEMENTATION_PLAN.md>
```

Pros: zero scripting, self-pacing, and its scheduling can straddle a usage reset. Cons: less control over the verifier gate and the resume-on-limit behavior than a dedicated supervisor. Good for prototyping the *content* of your iteration prompt before you wrap it in a supervisor.

### 5.2 Option B — The Stop-hook loop (Anthropic-native)

Claude Code's **Stop hook** can block a turn from ending until your verifier passes, and re-feed the iteration prompt — this is how the Ralph-style plugins are built. Wire `.loop/verify.ps1` as the gate: the session refuses to "finish" until acceptance is green, and the harness's built-in override-after-N-blocks prevents a true infinite loop. Pair with `--max-iterations` and a completion promise. This keeps everything inside one Claude Code process but **does not by itself survive a usage-limit stop** — for that you still want Option C wrapping it.

### 5.3 Option C — The supervisor script (recommended for unattended, limit-surviving runs)

This is the full solution for requirement #5. A PowerShell supervisor runs each iteration as a **fresh headless `claude -p` process**, gates on the verifier, checks termination, and — critically — **detects usage-limit messages, sleeps until the window resets, and resumes without you.**

```powershell
# run-loop.ps1  —  autonomous, context-fresh, usage-limit-surviving loop supervisor
# Usage:  pwsh -File run-loop.ps1  -MaxIterations 200
param(
  [int]$MaxIterations = 200,
  [int]$ResetBufferSeconds = 120,     # extra wait after a reported reset, to be safe
  [int]$StallLimit = 6                 # consecutive no-progress iterations => stop & alert
)

$ErrorActionPreference = "Stop"
$iterationPrompt = Get-Content ".loop/prompt.iteration.md" -Raw
$stall = 0

function Test-Done {
  # THE single source of truth for "done": verifier exit code 0 AND no open tasks.
  & pwsh -File ".loop/verify.ps1"; $verifyOk = ($LASTEXITCODE -eq 0)
  $openTasks = Select-String -Path "IMPLEMENTATION_PLAN.md" -Pattern '^\s*- \[ \]' -Quiet
  return ($verifyOk -and -not $openTasks)
}

function Get-ResetDelaySeconds([string]$text) {
  # Parse messages like: "hit your session limit · resets 12:10am" or "resets 3pm"
  if ($text -match 'resets\s+(\d{1,2})(?::(\d{2}))?\s*([ap]m)') {
    $h = [int]$Matches[1]; $m = if ($Matches[2]) {[int]$Matches[2]} else {0}
    if ($Matches[3] -eq 'pm' -and $h -ne 12) { $h += 12 }
    if ($Matches[3] -eq 'am' -and $h -eq 12) { $h = 0 }
    $now = Get-Date
    $reset = Get-Date -Hour $h -Minute $m -Second 0
    if ($reset -le $now) { $reset = $reset.AddDays(1) }   # reset is tomorrow
    return [int]($reset - $now).TotalSeconds + $ResetBufferSeconds
  }
  return $null   # not a limit message
}

for ($i = 1; $i -le $MaxIterations; $i++) {
  Write-Host "=== Iteration $i / $MaxIterations ===" -ForegroundColor Cyan
  $before = (git rev-parse HEAD)

  # Fresh context every iteration: a brand-new headless process, empty window.
  # --allowedTools restricts blast radius for unattended safety.
  $out = & claude -p $iterationPrompt `
                  --allowedTools "Edit,Write,Bash,Read,Grep,Glob" `
                  2>&1 | Out-String
  Write-Host $out

  # --- Usage-limit intercept: the requirement-#5 heart of the system ---
  $delay = Get-ResetDelaySeconds $out
  if ($delay -ne $null) {
    $wake = (Get-Date).AddSeconds($delay)
    Write-Host "Usage limit hit. Sleeping $([int]($delay/60)) min until ~$wake, then resuming." -ForegroundColor Yellow
    Start-Sleep -Seconds $delay
    # Re-probe: confirm the window really reopened before spending a real iteration.
    $probe = & claude -p "reply with OK" 2>&1 | Out-String
    if ((Get-ResetDelaySeconds $probe) -ne $null) { $i--; continue }  # still limited; loop waits again
    $i--; continue   # window open — redo this iteration for real
  }

  # --- Termination check ---
  if (Test-Done) {
    Write-Host "GOAL ACHIEVED: acceptance suite green and no open tasks." -ForegroundColor Green
    # Notify the human for acceptance testing (email/desktop toast/Discord/etc.)
    break
  }

  # --- Stall detector: no commit + no plan change => progress guardrail ---
  if ((git rev-parse HEAD) -eq $before) { $stall++ } else { $stall = 0 }
  if ($stall -ge $StallLimit) {
    Write-Host "STALLED: $StallLimit iterations with no progress. Stopping for human review." -ForegroundColor Red
    break
  }
}
```

The **per-iteration prompt** (`.loop/prompt.iteration.md`) is the fixed instruction that makes each fresh, amnesiac process behave correctly. It encodes the whole discipline:

```markdown
You are one iteration of an autonomous build loop. You start with NO memory of
previous iterations. All state lives on disk. Do exactly one unit of work, then stop.

1. Read GOAL.md, CLAUDE.md, IMPLEMENTATION_PLAN.md, and the tail of PROGRESS.md.
2. If IMPLEMENTATION_PLAN.md is missing or empty, STOP and ask for the plan phase.
3. Pick the single highest-priority unchecked task. Mark it in-progress.
4. Implement ONLY that task. Spawn a subagent for any large exploration so this
   context stays lean (stay under ~60% context; you are one task, not the project).
5. Run the verifier: `pwsh -File .loop/verify.ps1`.
   - GREEN: `git add -A && git commit`; check the task off in IMPLEMENTATION_PLAN.md;
     append a 3-line entry to PROGRESS.md (what/why/what-next).
   - RED: do NOT commit. Append the failure and your best hypothesis to PROGRESS.md
     and, if it's newly discovered work, add a task to IMPLEMENTATION_PLAN.md.
6. If the full acceptance suite is green and no unchecked tasks remain, write
   "LOOP-COMPLETE" as the last line of PROGRESS.md.
7. Do not mark anything done that the verifier did not prove. Never edit tests to
   make them pass unless the task explicitly is to fix a wrong test.
```

Note rule 7 — that's your **anti-reward-hacking clause**, and it's why you also periodically run an independent reviewer (below).

The **verifier** (`.loop/verify.ps1`) is small and is the *only* thing that gets to say "done":

```powershell
# .loop/verify.ps1 — exit 0 only if everything the goal demands passes.
$ErrorActionPreference = "Continue"
npm run build; if ($LASTEXITCODE -ne 0) { Write-Host "build failed"; exit 1 }
npm run lint;  if ($LASTEXITCODE -ne 0) { Write-Host "lint failed";  exit 1 }
npm test;      if ($LASTEXITCODE -ne 0) { Write-Host "tests failed"; exit 1 }
exit 0
```

(Swap the commands for whatever your project uses — that's the *only* thing that changes between projects, which is what makes the whole rig reusable.)

### 5.4 Scheduling the resumption belt-and-suspenders

The supervisor's in-process `Start-Sleep` handles the common case. For extra robustness against the *whole machine* restarting or the process dying, register a **Windows Scheduled Task** (or use the Claude Code `schedule` skill) that re-launches `run-loop.ps1` at, say, every window reset boundary. Because all state is in git and files, a fresh supervisor launch simply *resumes* — it reads the plan, sees open tasks, and continues. Idempotent resumption is a free property of the file-as-memory design.

### 5.5 Optional: parallelism via worktrees

When you have independent workstreams and quota to spare, fan out:

```powershell
git worktree add ../proj-featА feature/a
git worktree add ../proj-featB feature/b
# launch one supervisor per worktree (each its own fresh-context loop, own branch)
```

Keep it to ~3–5, one MCP server per worktree, and remember every parallel loop multiplies quota burn. On a subscription, serial is usually the right default.

---

## 6. The human's real job (and where judgment stays)

The system automates *execution*; it deliberately does **not** automate two things, because they are where human judgment is irreplaceable and where the whole thing goes wrong if you hand them over:

1. **Writing a genuinely testable goal.** This is 80% of your leverage. If you can't express "done" as a command that fails today and must pass at the end, the loop has nothing to converge toward and will either wander or reward-hack. Spend real effort here. Borrow the GIVEN/WHEN/THEN discipline from Kiro, but insist each criterion has a corresponding executable test. Have a *planning* Claude session help you draft the acceptance tests — then **you** sanity-check them, because tests the agent both writes and grades against are a reward-hacking vector.
2. **Acceptance testing the result.** When the loop reports LOOP-COMPLETE, green tests mean *the checks you specified pass* — not necessarily *the software is good*. Drive the real app. The research is blunt about this: specs over-produce, code generation is non-deterministic, and agents "ignore in-context notes and duplicate existing code." Your acceptance pass is the backstop that catches what the verifier's checks didn't encode.

A useful mental model: **you are the product owner and QA; the loop is the entire engineering team.** You write the ticket precisely and you accept or reject the delivery. You don't manage the sprint.

---

## 7. Failure modes and guardrails (making it actually converge)

The research is candid about how these loops fail. Design against each:

| Failure mode | Symptom | Guardrail |
|---|---|---|
| **Reward hacking** | Agent edits/weakens tests, hardcodes expected outputs, marks tasks done that aren't | Verifier is the sole arbiter; independent fresh-context reviewer audits the diff; "never edit tests to pass" rule; human acceptance backstop |
| **Off-plan drift (Yegge)** | Agent quietly diverges from the plan across iterations, keeps parallel markdown files | Single canonical `IMPLEMENTATION_PLAN.md` or a git-backed issue ledger (Beads); reviewer checks diff-vs-spec |
| **Context rot** | Later output degrades, hallucinations, forgotten decisions | Fresh context per iteration; one task per iteration; durable rules in `CLAUDE.md` not conversation; compact proactively (~60%) if using a persistent session |
| **Doom loop / no progress** | Same failing task retried forever, burning quota | `--max-iterations` cap; stall detector (no commit + no plan change for N iterations → stop & alert); Stop-hook override-after-N |
| **Silent halt on usage limit** | Overnight run dead, hours wasted till you wake | Supervisor detects `resets HH:MM`, sleeps to reset+buffer, re-probes, resumes (Section 5.3) |
| **Lost work on crash** | Long uncommitted run evaporates | Commit after every green task (Tmux-Orchestrator's 30-min rule as a ceiling); all state in git |
| **Runaway cost/blast radius** | Agent does something destructive unattended | `--allowedTools` allowlist; run in a sandbox/container (Docker/Fly/E2B) as Ralph does; scope MCP servers per worktree |

The meta-guardrail: **convergence is a property you engineer, not one you hope for.** It comes from (a) a checkable definition of done, (b) an independent checker, (c) backpressure that blocks bad progress, and (d) fresh context that stops failures from compounding. Remove any one and the loop stops converging.

---

## 8. An adoption ladder (don't build it all at once)

You can climb this incrementally; each rung is useful on its own:

1. **Rung 1 — Manual Ralph.** Write `GOAL.md` + a verifier. Run `/loop` with the iteration prompt. Watch it. Learn what your prompt needs. *(An afternoon.)*
2. **Rung 2 — Gated loop.** Add `verify.ps1` as the hard gate (Stop hook or supervisor). Now it can't declare false victory. *(A day.)*
3. **Rung 3 — Supervised + limit-surviving.** Add `run-loop.ps1` with the usage-limit intercept and stall detector. Now it runs unattended overnight and resumes itself. *(This is the full requirement set.)*
4. **Rung 4 — Reviewer.** Add a periodic fresh-context reviewer pass on the diff. Reward-hacking resistance jumps.
5. **Rung 5 — Ledger + parallelism.** Swap markdown plan for a git-backed issue ledger (Beads-style); fan out worktrees for independent streams. Only when you need scale and have quota.

Ship at Rung 3. Rungs 4–5 are quality and throughput multipliers, not prerequisites.

---

## 9. Summary — how the recommended solution meets your five requirements

| Your requirement | How the architecture satisfies it |
|---|---|
| **Reusable across project types** | The only per-project artifacts are `GOAL.md`, the acceptance tests, and the three commands inside `verify.ps1`. The loop engine, supervisor, and prompts are project-agnostic. Point it at any repo. |
| **Human inputs only a testable goal + does acceptance testing** | The human interface is exactly `GOAL.md` + executable acceptance criteria in; a LOOP-COMPLETE notification + human acceptance pass out. Nothing in between requires a human. |
| **Agents clear context & self-manage across processes** | Fresh headless process per iteration (empty context every time); all memory externalized to git-tracked files (`IMPLEMENTATION_PLAN.md`, `PROGRESS.md`, ledger, `CLAUDE.md`). Any process on any machine reconstructs state from disk. |
| **Loops until the goal is achieved, no human intervention** | Supervisor re-invokes iterations until the verifier is green *and* no open tasks remain; guardrails (max-iterations, stall detector) stop pathological runs and alert instead of wandering. |
| **Handles Claude usage limits, auto-resumes on reset** | Usage-limit intercept parses the `resets HH:MM` message, sleeps until the window reopens (+buffer), re-probes to confirm, and resumes — the exact pattern proven by claude-auto-retry / claude-auto-resume, implemented natively in PowerShell for your setup. |

**The one-sentence version:** *Turn your goal into a red test suite, hand it to a loop of fresh, amnesiac Claude processes that read and write their memory to git, gate every step on that test suite, and wrap the whole thing in a supervisor that sleeps through usage limits and wakes itself up — then you just write the ticket and accept the delivery.*

---

## Appendix A — Source list (15 fetched sources)

**Primary / official**
- Anthropic — *Best practices for Claude Code* — https://code.claude.com/docs/en/best-practices
- Anthropic — *Agent teams* (experimental) — https://code.claude.com/docs/en/agent-teams
- GitHub — *Spec-driven development with Spec Kit* — https://github.blog/ai-and-ml/generative-ai/spec-driven-development-with-ai-get-started-with-a-new-open-source-toolkit/
- Geoffrey Huntley — *how-to-ralph-wiggum* — https://github.com/ghuntley/how-to-ralph-wiggum
- absmartly — *Tmux-Orchestrator* — https://github.com/absmartly/Tmux-Orchestrator
- anthropics/claude-code issue #36320 — *Auto-resume after usage-limit reset* (and related #38263, #35744, #62788, #26775)

**Practitioner / secondary**
- Steve Yegge — *Introducing Beads (coding-agent memory)* — https://steve-yegge.medium.com/introducing-beads-a-coding-agent-memory-system-637d7d92514a
- Sourcegraph — *Revenge of the Junior Developer* (Yegge) — https://sourcegraph.com/blog/revenge-of-the-junior-developer
- Dev Interrupted — *Inventing the Ralph Wiggum Loop* — https://devinterrupted.substack.com/p/inventing-the-ralph-wiggum-loop-creator
- Martin Fowler — *Exploring Gen AI: SDD tools* — https://martinfowler.com/articles/exploring-gen-ai/sdd-3-tools.html
- Will Larson — *Building an internal agent: context compaction* — https://lethain.com/agents-context-compaction/
- codecentric — *The Ralph Wiggum loop: autonomous code generation with a fresh context* — https://www.codecentric.de/en/knowledge-hub/blog/the-ralph-wiggum-loop-autonomous-code-generation-with-a-fresh-context
- willness.dev — *One session per task* — https://willness.dev/blog/one-session-per-task
- Daniel Vaughan — *Context compaction deep dive: Codex CLI, Claude Code, OpenCode* — https://codex.danielvaughan.com/2026/04/14/context-compaction-deep-dive-codex-cli-claude-code-opencode/
- andyrewlee — *awesome-agent-orchestrators* — https://github.com/andyrewlee/awesome-agent-orchestrators
- Developers Digest — *Git worktrees + Claude Code parallel agents* — https://www.developersdigest.tech/blog/git-worktrees-claude-code-parallel-agents-guide
- Rate-limit tooling: *claude-auto-retry*, *claude-auto-resume* (Windows), *"Smart Resume"* wrapper

## Appendix B — Claims I could not machine-verify (treat as directional)

- Exact context-degradation thresholds (0–40 / 40–70 / 70%+).
- Huntley's specific token budget (~176K of 200K usable).
- The Stop-hook "override after N consecutive blocks" — behavior is real; the exact N is unconfirmed.
- Existence of an *official* Anthropic `ralph-wiggum` plugin (community Ralph runners definitely exist).
- Agent-teams specifics (`~/.claude/tasks/{team}/`, three states, file-locking) — consistent with docs but unverified in this run.
- Yegge's cost figures ($10–12/hr, $80–100/dev/day, Grit's ~45B tokens) and timeline predictions.

All six pillars and the reference architecture stand independent of these figures; they're cited for color and directional sizing, not as load-bearing facts.
