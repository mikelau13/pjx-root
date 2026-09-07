# Adopting Archon for pjx development

Research and a plan for using [`coleam00/Archon`](https://github.com/coleam00/Archon)
to automate pjx's development workflow — **after** the architecture upgrade, not
during it.

Written 2026-09-05 against Archon `dev` @ **0.10.1**. This is a plan, not a
changelog: nothing here has been implemented.

## The documents

| | |
|---|---|
| [**01 — What Archon is**](01-what-is-archon.md) | The product, the architecture, the workflow language, the isolation model, and what it is not. With diagrams. Start here |
| [**02 — Adaptation plan**](02-adaptation-plan.md) | Decisions to lock, the port-collision problem, the GitOps seam, a pjx workflow catalogue, and phases A–G |
| [**03 — Jira**](03-jira-integration.md) | Four integration levels ranked by cost. Archon has no Jira adapter and you probably should not write one |
| [**04 — Adjustments**](04-adjustments-and-optimizations.md) | What to change in Archon (little) and in pjx (more), plus the free-to-do-now list |

## The short version

**What it is.** Archon turns a software-delivery process — investigate, plan,
implement, review, validate, PR, respond to CI — into a versioned YAML DAG. Each
node runs either a coding agent or a plain shell command; each run gets its own
git worktree; progress is gated on exit codes rather than on the model's opinion
that it is finished. Not a knowledge base, not an MCP server, not CI, not a
deployer.

> ⚠️ The repository previously hosted an entirely different product with the
> same name — a Python RAG/knowledge-base MCP server, now archived on
> `archive/v1-task-management-rag`. Most tutorials you will find describe **that**
> one. See [01](01-what-is-archon.md#first-the-name-collision--you-may-have-read-about-a-different-product).

**Why it fits pjx.** `docs/architecture-upgrade/phase-*.md` are already written
to Archon's contract: explicit steps, a `## Verify`, a rollback, commit per step.
You already treat process as a versioned artefact. Archon is an interpreter for
that discipline.

**Why not yet.** Archon amplifies your gates, and pjx has two —
`validate.sh build` and `validate.sh test`. Every CORS bug in Phase 7b was
invisible to `curl`; it would be invisible to an agent too. Adopting before the
gates are real converts a slow manual process into a fast wrong one.

**Recommended entry point:** after
[Phase 11](../architecture-upgrade/phase-11-deploy.md).

**Recommended today:** the seven items on
[the free list](04-adjustments-and-optimizations.md#do-these-now--they-are-free) —
`AGENTS.md`, `engineering.md`, `.env.example`, the browser test, and the extra
`validate.sh` gates. All of them are already implied by the architecture upgrade,
and all of them pay back on your current Claude Code sessions whether or not
Archon is ever installed.

## The three things that will actually bite

1. **Fixed ports.** Archon expects `n` parallel runs to boot `n` copies of the
   app. pjx binds 80/443, and your own README says only one of Compose, k3d and
   CloudDevEnvironment can run at a time. →
   [the hard problem](02-adaptation-plan.md#the-hard-problem-parallel-runs-versus-a-fixed-port-stack)
2. **Untracked config.** The real React configuration lives in a gitignored
   `.env` with no example. A fresh worktree cannot see it and an agent cannot
   infer it. → [`worktree.copyFiles`](04-adjustments-and-optimizations.md#14-configure-worktreecopyfiles-for-pjxs-untracked-essentials)
   and a `.env.example`
3. **Version churn.** 3,000+ commits on `dev`, a breaking change in a patch
   release nine days before this was written, and two of Archon's own reference
   docs already disagree. Pin it. →
   [version drift](01-what-is-archon.md#version-drift--read-this-before-trusting-any-of-this)

## Sources

Everything here was read from the repository at `dev` on 2026-09-05, not from
memory:

- [`README.md`](https://github.com/coleam00/Archon) and [`CHANGELOG.md`](https://raw.githubusercontent.com/coleam00/Archon/dev/CHANGELOG.md)
- `.claude/skills/archon-cli/authoring-workflows/node-reference.md` — the current node reference
- `.claude/docs/workflow-yaml-reference.md` — older, partly superseded
- `.claude/docs/isolation-and-worktree-guide.md` — the 7-step resolver
- `.claude/docs/adapter-implementation-guide.md` — `IPlatformAdapter` and the adapter checklist
- `.archon/workflows/sdlc/README.md` — the guard rule and the credentials rule
- `.archon/config.example.yaml` — the three config layers
- `deploy/docker-compose.yml`, `packages/` tree, `migrations/`
- Rewrite history: [issue #957](https://github.com/coleam00/Archon/issues/957), [issue #952](https://github.com/coleam00/Archon/issues/952)
