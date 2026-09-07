# Adjustments to optimise the workflow

> Two halves. The first is what you would change about **Archon**; it is short,
> because the answer is mostly "configure it, do not change it." The second is
> what you would change about **pjx**; it is longer, and it is the half that
> actually determines whether this works.
>
> The asymmetry is the finding. Archon is a process engine, and a process engine
> is only as good as the checks it can run. Most of the work is making pjx
> checkable.

---

## Part 1 — Adjustments to Archon

### 1.1 Pin the version, and treat upgrades as a change

3,000+ commits on `dev`; 0.10.1 shipped a breaking change to model tiers in a
*patch* release, on 2026-08-30. Two of its own reference documents already
disagree about how many node types exist.

- Install a specific version and record it next to `global.json`, in the same
  spirit — a pinned toolchain is a pinned toolchain.
- Never track `latest` for something that opens PRs against your repo.
- Before upgrading, read the `CHANGELOG.md` Breaking section, then re-run
  `pjx-gates` on a known-good branch as the upgrade's own regression test.

### 1.2 Do not fork — extend through the four supported seams

| Seam | What you put there |
|---|---|
| `.archon/workflows/**/*.yaml` | Your workflows. Same `name:` as a bundled one shadows it — that is the supported override mechanism |
| `.archon/workflows/<pack>/commands/*.md` | Prompts. Never inline prose in YAML for anything substantial |
| `.archon/workflows/<pack>/scripts/*.py` | Deterministic logic. Python via `uv` is the house preference |
| `.archon/config.yaml` + `~/.archon/config.yaml` + `--config` | Everything about models, tiers, branches, copied files |

Everything pjx needs fits in these. See
[decision A2](02-adaptation-plan.md#a2--fork-or-override).

### 1.3 Extend the container image, do not replace it

If you run Archon as a container (Option 2), the stock image has Bun, `git`,
`gh` and the agent CLIs — it does **not** have `dotnet`, `helm`, `kubectl`,
`k3d` or `az`. Archon ships a `deploy/Dockerfile.user.example` and a
`docker-extend` skill for exactly this.

Prefer [decision A4 option (c)](02-adaptation-plan.md#a4--which-toolchain-do-bash-nodes-see)
anyway — have `bash:` nodes `docker run` the *pjx devcontainer image* against the
worktree. Then the toolchain is versioned by pjx, one image serves the
devcontainer and CI and Archon, and Archon's image stays stock and upgradeable.

### 1.4 Configure `worktree.copyFiles` for pjx's untracked essentials

A fresh worktree gets tracked files plus `.archon/`. pjx has three things that
are needed and not tracked, and finding out the hard way costs a run each:

```yaml
# .archon/config.yaml
worktree:
  copyFiles:
    - ".env"                       # gitignored; the REAL React config lives here
    - "local/certs -> local/certs" # mkcert material — regenerating per worktree
                                   # would need a fresh trust-store import each time
    - "global.json"                # tracked, but listed so a future move is caught
```

Finding #8 in the architecture-upgrade README is that the app reads
`REACT_APP_GRAPHQL_ENDPOINT` / `_API_DOTNET_URL` / `_SSO_ISSUER_URL` from a
gitignored `.env` with no `.env.example`. **An agent in a fresh worktree cannot
see that file and cannot infer it.** Copying it is the workaround; writing a
`.env.example` is the fix, and it is on the [do-now list](#do-these-now--they-are-free).

### 1.5 Bound concurrency by Docker, not by Archon's default

Archon defaults to 25 worktrees and its own concurrency setting. Neither knows
about your Docker daemon. Three concurrent pjx runs means three `dotnet restore`
and up to three Compose stacks contending for one machine, and the failure mode
is not a clean error — it is the one your own README documents: probes killing
pods mid-build because a cold compile took ten minutes under load.

Set concurrency to **2** initially, in `~/.archon/config.yaml`, and raise it only
after [Phase D](02-adaptation-plan.md#phase-d--parallelism-and-the-stack-lock)
shows you have headroom.

### 1.6 Give Archon its own least-privilege credentials

It runs an LLM's choice of shell command against your repositories. Therefore:

- A dedicated **GitHub App** or fine-grained PAT scoped to `mikelau13/pjx-root`
  only, with `contents: write`, `pull_requests: write`, `issues: write` — and
  **not** `workflows: write` unless you specifically want agents editing CI.
- `GITHUB_ALLOWED_USERS=mikelau13`. An **empty allow-list means open access**,
  which is a genuinely dangerous default on a public webhook.
- `GITHUB_WEBHOOK_SECRET` set, so the HMAC verification is doing something.
- **No Azure credentials in the ambient environment.** See
  [pjx adjustment 10](#10-do-not-let-a-workflow-hold-azure-credentials).

### 1.7 Adopt Archon's own two authoring rules — they are better than most

From the bundled `sdlc` pack's README, and worth quoting into your
`engineering.md` verbatim:

> **If this pack's fixture suite cannot exercise the guard, it is not a guard.
> It is a comment — write it as one.**

> **Evidence never carries credentials.** The engine retains what every exec
> node prints, so a node's output is the record whether it set out to keep one
> or not.

The second one has immediate consequences for pjx: a `bash:` node that runs
`docker compose config`, echoes a git remote that may contain a token, or dumps
`.env` has just written secrets into a run transcript that persists in Archon's
database and streams to whoever is watching.

---

## Part 2 — Adjustments to pjx

Ordered by how much they matter. The first four are the ones that decide
whether any of this works.

### 1. Make the gates real — this is the whole game

Archon gates on exit codes. pjx currently has two: `validate.sh build` and
`validate.sh test`. Every other check in the project is "Mike opens a browser."

An agent cannot open a browser. So every failure class your README documents as
having been caught by a human is, today, invisible to an agent:

| Failure from Phase 7b | Caught by | Would an agent catch it? |
|---|---|---|
| Two CORS policies hardcoding `http://localhost:3000` | browser only — "curl and Bruno ignore CORS entirely" | **No** |
| Apollo calling the Compose hostname in-cluster | browser → `ENOTFOUND` | **No** |
| Dropped `/api/1` prefix turning `ENOTFOUND` into a 404 | human noticing a 404 "looked like progress" | **No** |
| `react-scripts start` exiting 0 when stdin closes | pod restarts | Only if probes are asserted |

Target gate set:

```
validate.sh build     # exists
validate.sh test      # exists
validate.sh lint      # new — eslint + dotnet format + tsc --noEmit
validate.sh chart     # new — helm lint + helm template | kubeconform -strict
validate.sh e2e       # new — Playwright against the running stack
validate.sh smoke     # new — every service's /health returns 200 with a real DB check
```

Each must **exit non-zero honestly**. The known trap here is already in your
README: `pjx-api-dotnet`'s readiness probe does not check the database because
`AddDbContextCheck` had to be removed for EF Core 3.1, so a pod reports `Ready`
without proving Postgres is reachable. A `smoke` gate built on that probe would
be a gate that always passes. Close the EF Core item (already a Phase 10 hard
blocker) or make `smoke` query the database directly.

### 2. Build the browser test now — it is the highest-leverage item in the project

It is already on your deferred list, already assigned to Phase 7c, and already
identified as the only thing that catches the CORS class. Archon does not create
this requirement; it just makes the cost of not having it much higher, because
an agent will reintroduce that class of bug faster than you can review it.

Suggested shape, biased toward being a *gate* rather than a test suite:

- Playwright, headless, against the Compose stack over the real `*.pjx.test`
  hostnames and TLS — not against `localhost`, or you reintroduce the exact
  blind spot.
- One journey, the one your README says constitutes a pass: **register →
  activate → login → `/country/all` → `/cities` → sign out.**
- Assert on network responses, not only on rendered text, so a CORS
  preflight failure fails the test rather than producing an empty list.
- Runs in `validate.sh e2e`, in `build.yml` on every PR, and — via the stack
  lock — in `pjx-gates`.

Archon ships a `playwright-cli` skill with references for session management,
storage state, request mocking and tracing. Worth reading before you write yours.

### 3. Solve the port problem, at least at Tier 1

Covered in detail in
[the plan](02-adaptation-plan.md#the-hard-problem-parallel-runs-versus-a-fixed-port-stack).
Minimum viable: `flock` around the e2e gate. Better: `PJX_INSTANCE`
parameterisation of the Compose project name, host ports and Traefik host rules,
which incidentally lets you run Compose and k3d simultaneously — something your
README currently says is impossible.

Add a preflight to every stack-starting node, because the silent-failure mode is
documented and expensive:

```bash
docker port pjx-traefik | grep -q . || {
  echo "Traefik published NO ports — something holds 80/443" >&2; exit 1; }
```

The `Makefile` already does exactly this for `make up`. Reuse it rather than
writing a second copy — that is Archon's own "hand-synced pair" anti-pattern.

### 4. Write `AGENTS.md` and `engineering.md`

Archon reads a repository's `engineering.md` (root or `.archon/`) before agent
nodes write code, and the convention is that `AGENTS.md` carries project-wide
judgment. pjx has unusually good conventions and they are all currently trapped
in prose inside `docs/architecture-upgrade/README.md`, where no agent will find
them.

Extract, at minimum:

| Convention | Currently buried in | Why an agent must know |
|---|---|---|
| One branch per phase, off `master`; never commit to `master` | Working conventions | Otherwise it commits to master |
| Commit after every numbered step | Working conventions | Recovery granularity — you lost Phase 1's work once |
| **Use `stop`, never `down`** — `down` destroys seeded databases | `Makefile` header | An agent that runs `make down` to "clean up" wipes state |
| **Never recursive-delete a path that exists in both namespaces** | the ⚠️ block | `sudo rm -rf /workspaces` destroyed the working tree once |
| Devcontainer vs host: bind mounts need `HOST_PROJECT_PATH`; `localhost` differs per shell | "Where to run commands" | Rule 1 and 2 there are the source of most of the defects found in Phases 0–2 |
| Fix application bugs under Compose first; the k3d cycle is ~4 minutes | the k3d edit-cycle note | Stops an agent burning an hour rebuilding images |
| The real React config is in a gitignored `.env` | finding #8 | It cannot infer this |
| Branch and PR naming carries the Jira key | new, see [Jira Level 0](03-jira-integration.md#level-0--bridge-jira-to-github-and-leave-archon-alone) | Free traceability |

This costs an hour, is useful to *every* AI tool you use including plain Claude
Code today, and is the single best-value item on this page.

### 5. Make a fresh worktree cheap

Every worktree pays `npm install` × 4 and `dotnet restore` × 8 from cold. Two
fixes, both independently worthwhile:

- Mount a **shared package cache** into worktree builds — `~/.nuget/packages`
  and an npm cache directory — rather than letting each worktree populate its
  own. The `in-toolchain.sh` wrapper from
  [A4](02-adaptation-plan.md#a4--which-toolchain-do-bash-nodes-see) is the place
  to add the `-v` flags.
- Prefer **`docker compose build` with BuildKit cache mounts** over host restore
  where possible, so CI, the devcontainer and Archon share one cache story.

### 6. Ship `.env.example`

Finding #8 says Compose sets three `REACT_APP_*` variables the app does not
read, while the three it does read live in an untracked `.env`. That is a
landmine for a human and a wall for an agent. A tracked `.env.example` with the
correct names, plus deleting the three dead Compose variables, closes it.

### 7. Make CI the gate Archon trusts

Archon's `deliver` workflow polls CI and routes on the failure class. That is
only useful if CI is fast, deterministic and green-means-green.

- `build.yml` already runs `validate.sh build` and `test` on PRs — good. Add
  `lint`, `chart` and `e2e` to the same job so the gate an agent runs locally
  and the gate CI runs are the same set.
- Keep the PR job under ~10 minutes or the CI-response loop becomes the
  bottleneck for every run.
- Branch protection on `master` requiring that job. An agent will open PRs; the
  protection is what makes that safe.

### 8. Add a dangerous-command hook

Archon ships `.claude/skills/rulecheck/hooks/block-dangerous.sh`. pjx has a
documented, actually-occurred catastrophe: `sudo rm -rf /workspaces` inside the
devcontainer destroyed the working tree, recoverable only from VS Code's local
file history.

Add a hook that refuses, at minimum: `rm -rf` on `/workspaces`, `/`, or any path
outside the current worktree; `docker compose down` and `clean.sh` (use `stop`);
`git push --force` to `master`; `k3d cluster delete`; and any `az` command that
creates or deletes a resource. This is cheap, and it is worth having for
interactive Claude Code sessions today, before Archon exists.

### 9. Make the chart deployment-testable without a cluster

`helm lint` proves syntax. It does not prove the manifests are valid Kubernetes
or that a change did what you meant.

```bash
helm template pjx helm-pjx/ -f helm-pjx/values-dev.yaml | kubeconform -strict -summary
helm template pjx helm-pjx/ -f helm-pjx/values-dev.yaml > /tmp/after.yaml
diff -u /tmp/before.yaml /tmp/after.yaml   # the reviewable artefact
```

That rendered diff is what a `pjx-chart-change` workflow puts in the PR body,
and it is what makes an agent's chart edit reviewable in seconds instead of
minutes. Phase 7 already did the hard part — one routing mechanism, named ports,
`pjx.image`, per-environment values — so this is a small addition on top.

### 10. Do not let a workflow hold Azure credentials

Phase 9 is the first that spends money, and the README budgets ~$60–70/month
running. An agent with `az` and a standing service principal can create
resources in a loop.

- **No `AZURE_CLIENT_SECRET` in Archon's environment.** Ever.
- Cluster and infrastructure changes go through **GitHub Actions with OIDC
  federation** — which Phase 11 already specifies — so the credential lives in
  Actions, not in Archon, and is short-lived and audited.
- Any node that could spend money sits behind an `approval:` node, which means
  the workflow is `interactive: true` and cannot be launched detached. That is
  the correct amount of friction.
- Archon opens PRs against the environment repo. Argo CD or Flux applies them.
  That boundary is the security control, not just an architectural preference.

### 11. Keep `CloudDevEnvironment` out of the worktrees

`docker-compose.devcontainer.yml` currently mounts `..` — the parent of
pjx-root, which also contains the read-only reference repo. Finding #2. If that
mount survives into agent-run containers, an agent has write access to a
repository your memory explicitly marks read-only. Narrow the mount to
pjx-root before any of this.

### 12. (Later, optional) Feed run outcomes to Grafana

You have an LGTM stack and Phase 5 instrumentation. Archon runs are events with
durations, outcomes and costs. A `script:` node posting run metrics via OTLP
would put agent throughput on the same dashboards as application telemetry.

Genuinely nice, genuinely not urgent. Do it when you have twenty runs a week and
want to know which workflow is wasting money.

---

## Do these now — they are free

None of these need Archon, all of them are good practice for the architecture
upgrade you are already doing, and together they are most of
[Phase B](02-adaptation-plan.md#phase-b--own-the-gates).

| # | Item | Effort | Also fixes |
|---|---|---|---|
| 1 | `AGENTS.md` + `engineering.md` extracted from the phase README | 1 hour | Every Claude Code session today |
| 2 | `.env.example` + delete the three dead `REACT_APP_*` Compose vars | 30 min | Finding #8 |
| 3 | Narrow the devcontainer mount from `..` to pjx-root | 15 min | Finding #2 |
| 4 | `validate.sh lint` and `validate.sh chart` | 2 hours | Cheap PR feedback |
| 5 | The Playwright journey test → `validate.sh e2e` | 1 day | The 7c deferred blocker |
| 6 | Dangerous-command hook | 1 hour | The `rm -rf /workspaces` class |
| 7 | `local/scripts/in-toolchain.sh` | 1 hour | Makes CI, devcontainer and future agents share one toolchain |

Items 2, 3 and 5 are already on your own deferred list. Item 1 pays back
immediately regardless of whether you ever install Archon.

---

## Risks and anti-patterns

| Risk | Mitigation |
|---|---|
| **Adopting before the gates are real** | The whole point of [the gate](02-adaptation-plan.md#the-gate-do-not-start-this-before-phase-11) and Phase B |
| **Workflow sprawl** — twelve half-working YAML files nobody trusts | Author `pjx-gates` first and `include:` it. Fewer, composed workflows beat many bespoke ones |
| **Forking Archon** | Use the four seams. If you must patch, keep it a thin overlay and open an upstream PR the same day |
| **Agents editing CI** | Do not grant `workflows: write`. A change to `build.yml` is a change to the referee |
| **Trusting a green run** | Green means the gates passed. Review the diff. Especially early |
| **Secrets in run transcripts** | Archon's own rule 1.7. Audit every `bash:` node for what it prints |
| **Cost drift** | Tiers, `effort`, `allowed_tools: []` on judgment nodes, and `pjx-gates` before anything expensive |
| **Version churn** | Pin. Read the Breaking section. Regression-test the upgrade with `pjx-gates` |

---

## Open questions for you

1. **Is the Jira requirement real, or anticipated?** It changes whether
   [Level 3](03-jira-integration.md#level-3--make-jira-a-trigger-surface) is
   worth two days. If it is anticipated, do Levels 0–2 and revisit.
2. **Do you want an environment repository separate from pjx-root?** The GitOps
   seam is cleaner with one, but it is a real Phase 11 decision independent of
   Archon, and it doubles the number of repos you maintain alone.
3. **Would you actually delegate an architecture phase?** `pjx-phase-run` is the
   most interesting workflow in the catalogue and the most dangerous. If the
   answer is no, that is fine — say so and the catalogue gets shorter.
4. **Is `PJX_INSTANCE` parameterisation worth doing for its own sake?** It would
   let you run Compose and k3d together, which your README currently lists as
   impossible. That may justify it before Archon is ever installed.

---

← [What Archon is](01-what-is-archon.md) ·
[The adaptation plan](02-adaptation-plan.md) ·
[Jira](03-jira-integration.md)
