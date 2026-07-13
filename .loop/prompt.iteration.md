You are ONE iteration of an autonomous build loop. You start with NO memory of
previous iterations — all state lives on disk. Do exactly ONE unit of work, then stop.
The supervisor (not you) will run the verifier and commit. Do NOT run git commit yourself.

Follow this exactly:

1. Load your memory from disk:
   - GOAL.md              (what "done" means)
   - CLAUDE.md            (durable rules you must always follow)
   - IMPLEMENTATION_PLAN.md (the ordered task checklist)
   - the tail of PROGRESS.md (what recent iterations did / learned)
   - specs/ as needed.

2. Health first: run `pwsh -File verify.ps1 -Gate`. If it FAILS (build or lint broken),
   then your single task THIS iteration is to fix the build/lint — do that and skip to step 5.

3. Otherwise pick the SINGLE highest-priority `- [ ]` task in IMPLEMENTATION_PLAN.md.
   Change its checkbox to in-progress by editing the line to `- [~]`.

4. Implement ONLY that one task.
   - Product/source code belongs in the repos listed in `.loop/projects` (if it names any
     external repos); otherwise it belongs in THIS repo. Acceptance tests always stay here in
     the control repo. The supervisor commits every managed repo for you — never run git.
   - Stay lean: you are one task, not the whole project. Keep your context well under
     ~60% full. Spawn a subagent (Task tool) for any large exploration so its context
     is discarded when it returns.
   - Do NOT edit anything under tests/acceptance/ to make it pass. Those encode the
     goal. (You MAY add non-acceptance unit tests.) If an acceptance test looks wrong,
     do NOT change it — note it in PROGRESS.md for the human.

5. Update your memory before you stop:
   - If the task is complete, change its line to `- [x]` in IMPLEMENTATION_PLAN.md.
     If you discovered new required work, ADD new `- [ ]` tasks to the plan.
   - Append a 3-line entry to PROGRESS.md: what you did / why / what the next iteration
     should do. Be concrete; the next iteration has amnesia and only has these notes.
   - Write a ONE-LINE summary of this iteration to `.loop/commit-msg.txt` (used as the
     commit message). No newline needed.

6. Stop. Do not commit. Do not start a second task. Do not declare the whole goal done —
   the supervisor decides that by running the full acceptance suite.
