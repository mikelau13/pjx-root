# Phase 1 — Developer script layer

**Goal:** `local/scripts/` on `$PATH`, so the workflow is `dev-up.sh` /
`status.sh` / `stop.sh` instead of remembering compose invocations. Plus the
devcontainer lifecycle hooks that CloudDevEnvironment relies on.

**Risk:** Low — purely additive. **Reversible:** yes.

**Depends on:** Phase 0.

```bash
git checkout -b feature/arch-phase-1-script-layer
```

---

## What we're copying, and what we're not

CloudDevEnvironment has 12 scripts in `CDE:local/scripts/`. Several exist only
because it manages three independent submodule stacks:

| Script | Port to pjx? | Why |
|---|---|---|
| `dev-up.sh` / `dev-down.sh` | Simplified | pjx is one stack — no fan-out, no process-group juggling, no `--profile aw:hrm` prefix routing |
| `stop.sh`, `clean.sh`, `status.sh` | Yes | Directly useful |
| `validate.sh` | Simplified | Keep `test`/`build`/`lint`; drop the hierarchical repo/layer/project target resolution |
| `proxy.sh` | **No** | Solves a docker-in-docker problem pjx does not have — see below |
| `pull-secrets.sh` | **No** | Azure Key Vault. pjx has no secrets worth vaulting |
| `setup.sh` | **No** | Already covered by `.devcontainer/setup.sh` from Phase 0 |
| `start-portal.sh`, `create-snapshot.sh`, `full-reset.sh`, `dev-stop.sh` | **No** | Platform portal (D4) and CloudDevEnvironment-specific workflows |

### Why `proxy.sh` is not needed

`CDE:local/scripts/proxy.sh` bridges privileged ports because CloudDevEnvironment
runs **docker-in-docker**: its containers live inside the devcontainer, so their
ports must be forwarded out — and VS Code cannot bind ports below 1024, so it
assigns random high ports and `proxy.sh` uses `simpleproxy` to bridge 80/443 to
them.

pjx uses **docker-outside-of-docker** (Phase 0), so containers run on the *host*
daemon and publish directly:

```
CloudDevEnvironment:  browser → :80 → simpleproxy → random port
                              → VS Code forwarder → devcontainer → nested dockerd → Traefik
pjx:                  browser → :80 → Traefik container on the host daemon
```

VS Code's forwarder is not in pjx's path, so there is nothing to bridge.

Docker's daemon performs the privileged bind, so no `sudo` is needed — but
confirm nothing else holds the ports before Phase 2:
`ss -tlnp | grep -E ':(80|443) ' || echo "both free"`. If pjx is ever moved to
WSL, `proxy.sh` becomes relevant again.

We are also **not** porting the `docker()` bash function from
`CDE:.devcontainer/devcontainer.bashrc`. It exists to rewrite
`HOST_PROJECT_PATH` per submodule and to intercept `docker compose` at the repo
root for the three-stack fan-out. With one stack (D3: stay vendored), plain
`docker compose` already does the right thing, and shadowing the `docker` binary
with a shell function is a surprise worth avoiding.

---

## Step 1 — Create the directories

```bash
mkdir -p local/scripts/lib
```

---

## Step 1b — `local/scripts/lib/common.sh`

**Write this first.** Every other script sources it, so it is the single place a
new service gets registered.

> ### Never let a script target the `workspace` service
>
> `docker-compose.devcontainer.yml` defines **six** services — the five apps *and*
> `workspace`, the devcontainer itself. A bare `docker compose up` targets all
> six, compares `workspace` against the plain `build:` definition, finds it
> differs from VS Code's generated override, and **recreates the container you
> are running inside**, dropping your session. `clean.sh` is worse:
> `down --volumes --remove-orphans` would delete it outright.
>
> `APP_SERVICES` excludes it. `runServices: ["workspace"]` in `devcontainer.json`
> solves the mirror-image problem — VS Code starting the apps. Both halves are
> needed.

```bash
#!/bin/bash
# Shared definitions for the pjx developer scripts. Source this, do not run it.
#
# Adding a service to docker-compose.devcontainer.yml is picked up automatically
# by dev-up.sh / stop.sh / clean.sh, because APP_SERVICES is derived rather than
# hardcoded. Only SERVICE_HEALTH and the project lists need a manual line, and
# only for things compose cannot infer.

LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${LIB_DIR}/../../.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.devcontainer.yml"

# Every compose service except `workspace` — see the warning above.
mapfile -t APP_SERVICES < <(
    docker compose -f "${COMPOSE_FILE}" config --services | grep -v '^workspace$' | sort
)
if [[ ${#APP_SERVICES[@]} -eq 0 ]]; then
    echo "ERROR: no services found in ${COMPOSE_FILE}" >&2
    exit 1
fi

# status.sh needs a health endpoint per service; compose cannot infer these.
# Format: "display|container|health_url"
#
# URLs use the compose SERVICE NAME and the CONTAINER port — not localhost and
# the published port. status.sh runs inside the devcontainer, a sibling container
# on pjx-network, where `localhost` is its own loopback: localhost:3000 would
# report every service down even when all are healthy. Service names resolve via
# Docker's embedded DNS on the shared network.
# (.NET is :80 internally, published as 6001; SSO is :80, published as 5001.)
SERVICE_HEALTH=(
    "React Web|pjx-web-react-dev|http://pjx-web-react:3000"
    # NOT /graphql — that expects a POST with a query body, so a bare GET returns
    # 400 "GET query missing." even when the server is perfectly healthy.
    # Apollo Server 2 exposes this health endpoint by default: 200 {"status":"pass"}.
    "GraphQL|pjx-graphql-apollo-dev|http://pjx-graphql-apollo:4000/.well-known/apollo/server-health"
    ".NET API|pjx-api-dotnet-dev|http://pjx-api-dotnet:80/swagger"
    "Node API|pjx-api-node-dev|http://pjx-api-node:8081"
    "SSO|pjx-sso-identityserver-dev|http://pjx-sso-identityserver:80"
)

# validate.sh needs to know how each project builds.
NODE_PROJECTS=(pjx-web-react pjx-api-node pjx-graphql-apollo)
DOTNET_PROJECTS=(pjx-api-dotnet pjx-sso-identityserver)
```

Note `../../..` — `common.sh` sits one level deeper than the command scripts.

It needs no execute bit; it is sourced, not run. Leaving it non-executable is a
useful signal that it is not a command.

> **Why derive `APP_SERVICES`?** Three scripts need the same list, and three
> hardcoded copies drift — you add a service, forget `clean.sh`, and it silently
> leaks containers. `docker compose config --services` makes the compose file the
> single source of truth. Cost: one subprocess (~100 ms) per invocation, and the
> scripts now need Docker reachable even for `--help`. Acceptable for scripts
> whose whole job is driving Docker.

---

## Step 2 — `local/scripts/dev-up.sh`

Ports the useful flags from `CDE:local/scripts/compose/dev-up.sh` — `--build`,
`--daemon`, `--watch` — without the multi-stack machinery.

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

show_help() {
    cat <<'HELPTEXT'
Usage: dev-up.sh [-b] [-d | -w]

Start the pjx stack.

Options:
  -h, --help     Show this help.
  -b, --build    Build images before starting.
  -d, --daemon   Start detached. Stop with stop.sh.
  -w, --watch    Enable file sync + hot reload (implies attached).
HELPTEXT
}

BUILD=false
DAEMON=false
WATCH=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)   show_help; exit 0 ;;
        -b|--build)  BUILD=true;  shift ;;
        -d|--daemon) DAEMON=true; shift ;;
        -w|--watch)  WATCH=true;  shift ;;
        *) echo "Error: unknown option '$1'. Try --help." >&2; exit 1 ;;
    esac
done

if [[ "${DAEMON}" == true && "${WATCH}" == true ]]; then
    echo "Error: --daemon cannot be combined with --watch." >&2
    exit 1
fi

FLAGS=(up)
[[ "${BUILD}"  == true ]] && FLAGS+=(--build)
[[ "${DAEMON}" == true ]] && FLAGS+=(-d)
[[ "${WATCH}"  == true ]] && FLAGS+=(--watch)

cd "${REPO_ROOT}"
exec docker compose -f "${COMPOSE_FILE}" "${FLAGS[@]}" "${APP_SERVICES[@]}"
```

> `--watch` requires `develop.watch` blocks in the compose file, which pjx does
> not have yet. The flag is wired up now; make it functional in Phase 2 when you
> are editing service definitions anyway. Until then it is a no-op.

> **Follow-up: a `dev.sh` convenience wrapper.** CloudDevEnvironment's
> user-facing entry point is `dev.sh` (`dev-up.sh` there is only a shim target for
> its `docker compose` interception, which pjx does not port). A pjx `dev.sh`
> starting the app stack *and* the observability stack in one command is worth
> adding once `--watch` is functional (Phase 2) and `obs-up.sh` exists (Phase 3) —
> at that point it has something to wrap.

---

## Step 3 — `local/scripts/stop.sh` and `clean.sh`

`stop.sh` — non-destructive:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

cd "${REPO_ROOT}"
docker compose -f "${COMPOSE_FILE}" stop "${APP_SERVICES[@]}"
echo "Stack stopped. Containers kept — use dev-up.sh to resume."
```

`clean.sh` — destructive, so it confirms first. This mirrors the biggest footgun
in CloudDevEnvironment, where `docker compose down` silently wipes seeded
databases:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

cat <<'WARN'
This removes the pjx application containers and their volumes.
Any data held inside those containers is lost — including the SQLite
databases (user accounts, calendar entries).
WARN

read -rp "Type 'yes' to continue: " reply
[[ "${reply}" == "yes" ]] || { echo "Aborted."; exit 1; }

cd "${REPO_ROOT}"
# `rm -fsv` = force, stop first, remove anonymous volumes — scoped to the named
# services. Deliberately NOT `down --remove-orphans`: `down` ignores a service
# list, and --remove-orphans would classify the devcontainer (`workspace`) as an
# orphan and delete it out from under you.
docker compose -f "${COMPOSE_FILE}" rm -fsv "${APP_SERVICES[@]}"
echo "Environment cleaned."
```

> Keep the confirmation prompt. pjx currently uses SQLite inside the containers
> (`Microsoft.EntityFrameworkCore.Sqlite` in both .NET projects), so `down
> --volumes` does destroy local account and calendar data.

---

## Step 4 — `local/scripts/status.sh`

Adapted from `CDE:local/scripts/status.sh`. Two changes: the service table
matches pjx, and there is no host-only guard — CloudDevEnvironment needs one
because it runs Docker-in-Docker, and pjx's `DOCKER_HOST` points at the host
socket so this works from inside the container.

```bash
#!/bin/bash
# Show the state of all pjx dev services.
set -uo pipefail   # no -e: check commands return non-zero when services are down
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BOLD='\033[1m';  NC='\033[0m'

printf "${BOLD}%-12s %-30s %-10s %-8s${NC}\n" "SERVICE" "CONTAINER" "STATE" "HTTP"
printf '%.0s-' {1..64}; echo

for entry in "${SERVICE_HEALTH[@]}"; do
    IFS='|' read -r name container url <<< "${entry}"

    state=$(docker inspect -f '{{.State.Status}}' "${container}" 2>/dev/null || echo "absent")
    case "${state}" in
        running) state_c="${GREEN}${state}${NC}" ;;
        absent)  state_c="${RED}${state}${NC}"   ;;
        *)       state_c="${YELLOW}${state}${NC}" ;;
    esac

    # No `|| echo "---"` fallback: curl already prints 000 on connection failure,
    # and the fallback would concatenate into "000---".
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "${url}" 2>/dev/null)
    if [[ "${code}" =~ ^[23] ]]; then
        code_c="${GREEN}${code}${NC}"
    else
        code_c="${RED}${code}${NC}"
    fi

    printf "%-12s %-30s %-21b %-19b\n" "${name}" "${container}" "${state_c}" "${code_c}"
done
```

> Phase 2 replaces the `localhost:PORT` URLs here with `*.pjx.test`
> hostnames, and Phase 3 adds a Grafana row. Expect to edit this file twice more.

---

## Step 5 — `local/scripts/validate.sh`

CloudDevEnvironment's version resolves hierarchical targets across three repos.
pjx needs far less:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

show_help() {
    cat <<'HELPTEXT'
Usage: validate.sh <test|build|lint> [project]

Projects: pjx-web-react pjx-api-node pjx-graphql-apollo
          pjx-api-dotnet pjx-sso-identityserver
Omit [project] to run across all of them.

Note: for a single C# test or a small subset, prefer running
  dotnet test --filter <expr>
from the project directory. This script is for whole-project sweeps.
HELPTEXT
}

[[ $# -ge 1 ]] || { show_help; exit 1; }
[[ "$1" == "-h" || "$1" == "--help" ]] && { show_help; exit 0; }

CMD="$1"; shift
# NODE_PROJECTS and DOTNET_PROJECTS come from lib/common.sh

if [[ $# -ge 1 ]]; then
    TARGETS=("$1")
else
    TARGETS=("${NODE_PROJECTS[@]}" "${DOTNET_PROJECTS[@]}")
fi

is_dotnet() {
    local p
    for p in "${DOTNET_PROJECTS[@]}"; do [[ "$1" == "${p}" ]] && return 0; done
    return 1
}

FAILED=()
for target in "${TARGETS[@]}"; do
    dir="${REPO_ROOT}/projects/${target}"
    [[ -d "${dir}" ]] || { echo "!! no such project: ${target}" >&2; exit 1; }

    echo ""
    echo "===> ${CMD} ${target}"

    if is_dotnet "${target}"; then
        case "${CMD}" in
            test)  (cd "${dir}" && dotnet test)  || FAILED+=("${target}") ;;
            build) (cd "${dir}" && dotnet build) || FAILED+=("${target}") ;;
            lint)  echo "   (no linter configured for .NET projects — skipped)" ;;
            *) echo "Error: unknown command '${CMD}'." >&2; exit 1 ;;
        esac
    else
        case "${CMD}" in
            test)  (cd "${dir}" && npm test --if-present)      || FAILED+=("${target}") ;;
            build) (cd "${dir}" && npm run build --if-present) || FAILED+=("${target}") ;;
            lint)  (cd "${dir}" && npm run lint --if-present)  || FAILED+=("${target}") ;;
            *) echo "Error: unknown command '${CMD}'." >&2; exit 1 ;;
        esac
    fi
done

echo ""
if [[ ${#FAILED[@]} -gt 0 ]]; then
    echo "FAILED: ${FAILED[*]}"
    exit 1
fi
echo "All targets passed: ${CMD}"
```

The `dotnet test --filter` note in the help text is deliberate — it is the same
guidance CloudDevEnvironment's own docs give, for the same reason: a wrapper
script's output filtering is not a substitute for real test selection.

> **Both .NET projects belong in `DOTNET_PROJECTS`, as written.** Earlier drafts
> floated a `DOCKER_ONLY_PROJECTS` escape hatch in case the .NET 8 SDK could not
> build `pjx-sso-identityserver`'s `netcoreapp3.1` target. Phase 0 settled it: the
> 8.0 SDK restores 3.1 projects with **warning NETSDK1138**, not an error. No
> special case is needed here or in [Phase 4](phase-4-dotnet8.md).

---

## Step 6 — Make them executable and put them on `$PATH`

```bash
chmod +x local/scripts/*.sh
```

That glob deliberately does not match `local/scripts/lib/common.sh`, which is
sourced rather than executed.

> The executable bit is tracked by git and appears in your diff as a mode change.
> Commit it — otherwise a fresh clone gets non-executable scripts and everyone
> hits "Permission denied".

Add to the end of `.devcontainer/setup.sh`:

```bash
# Put the developer scripts on PATH for interactive shells.
PROFILE_LINE='export PATH="$PATH:/workspaces/pjx-root/local/scripts"'
if ! grep -qF "${PROFILE_LINE}" "${HOME}/.bashrc" 2>/dev/null; then
    echo "${PROFILE_LINE}" >> "${HOME}/.bashrc"
    echo "Added local/scripts to PATH in ~/.bashrc"
fi
```

---

## Step 6a — Emit host paths for bind mounts

**Required before `dev-up.sh` can work from inside the devcontainer.** Without it
all five containers start and immediately exit — npm reports
`ENOENT /app/package.json`, `dotnet watch` reports
`Could not find a MSBuild project file`.

Under docker-outside-of-docker, **the host daemon resolves bind-mount sources**.
It has no view of the devcontainer's filesystem. So when compose runs inside the
container with cwd `/workspaces/pjx-root`, it expands `./projects/pjx-web-react`
to `/workspaces/pjx-root/projects/pjx-web-react` and hands that to the host
daemon — which does not find it, silently creates an **empty root-owned
directory** at that path on the host, and mounts that. Containers get an empty
`/app`.

This did not surface before Phase 1 because VS Code started the containers, and
VS Code resolves the compose file from the host. Moving startup into `dev-up.sh`
exposed it. It is the same problem `HOST_PROJECT_PATH` solves in
`CDE:.devcontainer/devcontainer.json` — adapted, because CloudDevEnvironment runs
docker-in-docker and needs a *container* path where pjx needs a *host* path.

**1. `.devcontainer/devcontainer.json`:**

```jsonc
// Host path of this workspace. Bind-mount sources are resolved by the HOST
// daemon, so compose must emit host paths even when run inside the container.
// containerEnv, not remoteEnv: remoteEnv only reaches processes VS Code spawns,
// so `docker exec ... echo $HOST_PROJECT_PATH` shows nothing even when your
// terminal has it — misleading when debugging. containerEnv is set at creation
// and visible to every process.
"containerEnv": {
    "HOST_PROJECT_PATH": "${localWorkspaceFolder}"
},
```

`${localWorkspaceFolder}` is substituted by VS Code — no hardcoded paths, works
for any developer.

> **This only applies on container creation.** Adding it to a running container's
> config does nothing until you Rebuild and Reopen. If `echo $HOST_PROJECT_PATH`
> is empty, compare `docker inspect <workspace> --format '{{.Created}}'` against
> the mtime of `devcontainer.json` — a container older than the config has not
> picked it up. Until you rebuild, `export HOST_PROJECT_PATH=<host path>` in your
> terminal is equivalent.

**2. `docker-compose.devcontainer.yml`** — prefix the five app bind sources:

```yaml
      - ${HOST_PROJECT_PATH:-.}/projects/pjx-web-react:/app:cached
```

…and the same for the other four. Leave the `- /app/node_modules` anonymous
volumes and the `workspace` service's own mount unchanged.

The `:-.` fallback makes the file correct from both sides: from the host the
variable is unset and `.` resolves properly; from the container it is the host
path.

Rebuild and Reopen for `remoteEnv` to apply, then confirm:

```bash
echo "$HOST_PROJECT_PATH"        # → the host path, e.g. /home/you/projects/pjx-root
docker inspect pjx-web-react-dev --format '{{range .Mounts}}{{.Source}}{{"\n"}}{{end}}'
#   → must start with the HOST path, not /workspaces/...
```

> **Clean up the empty directories Docker invented** — but read the warning below
> before running any recursive delete.
>
> ```bash
> ls -la /workspaces        # ← verify it is EMPTY and root-owned first
> ```
>
> ⚠️ **`/workspaces` exists in both namespaces.** On the host it is Docker's junk;
> **inside the devcontainer it is your repo's bind mount.** Running
> `sudo rm -rf /workspaces` in the container destroys the working tree. Delete it
> only from a host terminal, and `ls` it first. This is not hypothetical — it
> happened during Phase 1 and cost all uncommitted work.

---

## Step 6b — Remove VS Code's port forwarding

**Do this before running `dev-up.sh` for the first time**, or the stack fails with:

```
failed to bind host port 0.0.0.0:5001/tcp: address already in use
```

The culprit is not another container — it is VS Code. `devcontainer.json`
inherits a `forwardPorts` list from before Phase 0:

```jsonc
"forwardPorts": [3000, 4000, 5001, 5002, 6001, 8081],
```

VS Code binds every one on the host at `127.0.0.1`. Under DooD the app containers
publish on `0.0.0.0` on that same host — and `0.0.0.0` includes `127.0.0.1`, so
they collide. Forwarding is pointless here anyway: the ports are already on the
host.

Delete both `forwardPorts` and `portsAttributes`, and add:

```jsonc
"otherPortsAttributes": {
    "onAutoForward": "ignore"
},
```

Then **Rebuild and Reopen in Container** — that is what makes VS Code release the
existing bindings. (To unblock without a rebuild: PORTS panel → right-click each
port → Stop Forwarding Port.)

[Phase 2](phase-2-traefik.md) adds `forwardPorts: [80, 443, 9090]` back for
Traefik. Until then nothing needs forwarding.

```bash
ss -tlnp | grep -E ':(3000|4000|5001|5002|6001|8081) ' || echo "all free"
```

---

## Step 7 — Add `postStart` and `postAttach` hooks

pjx has only `postCreateCommand`. CloudDevEnvironment splits work across three
hooks because some fixes must run on *every* start, not just on creation. The
`credsStore` fix is the important one — it is lifted from
`CDE:.devcontainer/postStart.sh` and it addresses a real, recurring failure: the
Docker-in-Docker feature injects a session-specific credential helper that does
not work inside the container, and `docker build` then fails with "error getting
credentials".

Create `.devcontainer/postStart.sh`:

```bash
#!/bin/bash
set -euo pipefail

# The Docker-in-Docker feature injects a "credsStore" entry pointing at a
# session-specific credential helper that does not work inside the container,
# breaking `docker build` / `docker compose` with "error getting credentials".
# Strip it on every start so Docker falls back to config-based auth.
if [ -f "${HOME}/.docker/config.json" ] && grep -q credsStore "${HOME}/.docker/config.json"; then
    tmp=$(mktemp)
    grep -v '"credsStore"' "${HOME}/.docker/config.json" > "${tmp}" && mv "${tmp}" "${HOME}/.docker/config.json"
    echo "Removed credsStore from Docker config"
fi

echo "Post-start setup completed"
```

```bash
chmod +x .devcontainer/postStart.sh
```

Then in `.devcontainer/devcontainer.json`, alongside the existing
`postCreateCommand`:

```jsonc
"postStartCommand": "./.devcontainer/postStart.sh",
```

> CloudDevEnvironment also runs the same fix in a `postAttachCommand`
> (`CDE:.devcontainer/boot.sh`), because VS Code re-injects `credsStore` when it
> attaches — *after* `postStart` has run. Add that too if you see the credentials
> error come back after reconnecting to a running container.

---

## Adding a service later

This is the payoff for `lib/common.sh`. To add a sixth service:

| File | Change | Required? |
|---|---|---|
| `docker-compose.devcontainer.yml` | the service definition | always |
| `lib/common.sh` → `SERVICE_HEALTH` | one line | only if it serves HTTP |
| `lib/common.sh` → `NODE_PROJECTS` / `DOTNET_PROJECTS` | one line | only if it builds or tests |
| `.devcontainer/setup.sh` | dependency install loop | only if it is a new Node/.NET project |

`dev-up.sh`, `stop.sh` and `clean.sh` need **no changes** — they read the service
list from compose.

Later phases add their own touchpoints: Traefik labels and a `--add-host` entry
([Phase 2](phase-2-traefik.md)), `OTEL_SERVICE_NAME`
([Phase 5](phase-5-otel.md)), a Helm template plus a CI matrix entry
([Phase 7](phase-7-cicd.md)), probes and resource requests
([Phase 10](phase-10-deployable.md)).

---

## Verify

**Run all of this inside the devcontainer.** The `$PATH` entry lives in the
container's `~/.bashrc`, `/workspaces/pjx-root` exists only there, and the health
URLs use service names that resolve on `pjx-network`.

The one exception is browser access — the apps publish on the **host**, so
`http://localhost:3000` works in your browser but not from a shell inside the
devcontainer. Two vantage points on the same containers.

```bash
# Reload PATH (or open a fresh terminal)
source ~/.bashrc

# 0. The shared library resolves and derives the service list
bash -c 'source local/scripts/lib/common.sh; echo "REPO_ROOT=${REPO_ROOT}"; printf "%s\n" "${APP_SERVICES[@]}"'
#    → REPO_ROOT=/workspaces/pjx-root, then five services, no "workspace"

# 1. Scripts resolve from anywhere
which dev-up.sh status.sh validate.sh

# 2. Help text works
dev-up.sh --help
validate.sh --help

# 3. Bring the stack up and inspect it
dev-up.sh -d
status.sh          # every row should show running / 2xx-3xx

# 4. Non-destructive stop keeps containers — and leaves the devcontainer alone
stop.sh
docker ps -a --filter name=-dev --format '{{.Names}}\t{{.Status}}'
    # → the 5 app containers, state Exited (not removed)
    #
    # Filtering on `name=pjx-` instead would show SIX rows, because it also
    # matches pjx-root-workspace-1 — the devcontainer. That row must stay `Up`:
    # if stop.sh had targeted every compose service, it would have killed the
    # container you are running in. Seeing it still Up is the proof that
    # APP_SERVICES is doing its job.
    #
    # Exit codes: 143 = 128+15 = SIGTERM, a clean graceful stop. pjx-web-react
    # reports 1 instead because `npm start` does not relay SIGTERM cleanly to
    # react-scripts — benign, not a crash.

# 5. clean.sh refuses without explicit confirmation
echo "no" | clean.sh   # → "Aborted."
```

Then confirm from your **browser on the host** — the published-port view the
in-container health checks cannot see:

- <http://localhost:3000> — React app
- <http://localhost:4000> — GraphQL
- <http://localhost:6001/swagger> — .NET API

Step 3 is the one that matters: if `status.sh` shows every service green, the
stack is genuinely healthy before Phase 2 starts moving it behind a proxy.

> **Reading a failure.** `running` + a red HTTP code means the container is up but
> the app is not answering — check
> `docker compose -f docker-compose.devcontainer.yml logs <service>`.
> **Every** row red while containers show `running` points at the health URLs
> rather than the apps (Step 1b's localhost-vs-service-name trap). All rows
> `exited` means the containers died on startup — almost always Step 6a's
> bind-mount path problem; check `docker logs <container>` for
> `ENOENT /app/package.json`.

---

## Commit as you go

Phase 1's work existed only in the working tree while these defects were being
found. A misdirected `sudo rm -rf` then destroyed it, recoverable only via VS
Code's local file history. Commit after each step and push the branch early:

```bash
git add -A && git commit -m "Phase 1: <step>"
git push -u origin feature/arch-phase-1-script-layer
```

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-1-script-layer
```

The `~/.bashrc` PATH line lives inside the container, not the repo — it
disappears on the next rebuild, or remove it by hand.
