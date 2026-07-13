# Project rules for build agents

These rules are ALWAYS in effect for every iteration. The plan phase may extend this
file with project-specific conventions.

- Never edit, delete, skip, or weaken anything under `tests/acceptance/`. Those tests
  encode the goal and are the definition of done. You may add other tests.
- Do exactly one plan task per iteration. Keep changes small and reviewable.
- All durable knowledge goes in files (PROGRESS.md, IMPLEMENTATION_PLAN.md, specs/),
  never assume anything survives in context — the next iteration starts fresh.
- Prefer editing existing files over creating new ones. Match existing code style.
- The supervisor runs the verifier and git — you never commit.

