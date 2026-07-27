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
| `proxy.sh` | Phase 2 | Only needed once Traefik owns 80/443 |
| `pull-secrets.sh` | **No** | Azure Key Vault. pjx has no secrets worth vaulting |
| `setup.sh` | **No** | Already covered by `.devcontainer/setup.sh` from Phase 0 |
| `start-portal.sh`, `create-snapshot.sh`, `full-reset.sh`, `dev-stop.sh` | **No** | Platform portal (D4) and CloudDevEnvironment-specific workflows |

We are also **not** porting the `docker()` bash function from
`CDE:.devcontainer/devcontainer.bashrc`. It exists to rewrite
`HOST_PROJECT_PATH` per submodule and to intercept `docker compose` at the repo
root for the three-stack fan-out. With one stack (D3: stay vendored), plain
`docker compose` already does the right thing, and shadowing the `docker` binary
with a shell function is a surprise worth avoiding.

---

## Step 1 — Create the directory

```bash
mkdir -p local/scripts
```

---

## Step 2 — `local/scripts/dev-up.sh`

Ports the useful flags from `CDE:local/scripts/compose/dev-up.sh` — `--build`,
`--daemon`, `--watch` — without the multi-stack machinery.

```bash
#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.devcontainer.yml"

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
exec docker compose -f "${COMPOSE_FILE}" "${FLAGS[@]}"
```

> `--watch` requires `develop.watch` blocks in the compose file, which pjx does
> not have yet. The flag is wired up now; make it functional in Phase 2 when you
> are editing service definitions anyway. Until then it is a no-op.

---

## Step 3 — `local/scripts/stop.sh` and `clean.sh`

`stop.sh` — non-destructive:

```bash
#!/bin/bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

cd "${REPO_ROOT}"
docker compose -f docker-compose.devcontainer.yml stop
echo "Stack stopped. Containers kept — use dev-up.sh to resume."
```

`clean.sh` — destructive, so it confirms first. This mirrors the biggest footgun
in CloudDevEnvironment, where `docker compose down` silently wipes seeded
databases:

```bash
#!/bin/bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

cat <<'WARN'
This removes all pjx containers, networks, and volumes.
Any data held inside those containers is lost.
WARN

read -rp "Type 'yes' to continue: " reply
[[ "${reply}" == "yes" ]] || { echo "Aborted."; exit 1; }

cd "${REPO_ROOT}"
docker compose -f docker-compose.devcontainer.yml down --volumes --remove-orphans
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

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BOLD='\033[1m';  NC='\033[0m'

# Format: "display_name|container_name|health_url"
SERVICES=(
    "React Web|pjx-web-react-dev|http://localhost:3000"
    "GraphQL|pjx-graphql-apollo-dev|http://localhost:4000/graphql"
    ".NET API|pjx-api-dotnet-dev|http://localhost:6001/swagger"
    "Node API|pjx-api-node-dev|http://localhost:8081"
    "SSO|pjx-sso-identityserver-dev|http://localhost:5001"
)

printf "${BOLD}%-12s %-30s %-10s %-8s${NC}\n" "SERVICE" "CONTAINER" "STATE" "HTTP"
printf '%.0s-' {1..64}; echo

for entry in "${SERVICES[@]}"; do
    IFS='|' read -r name container url <<< "${entry}"

    state=$(docker inspect -f '{{.State.Status}}' "${container}" 2>/dev/null || echo "absent")
    case "${state}" in
        running) state_c="${GREEN}${state}${NC}" ;;
        absent)  state_c="${RED}${state}${NC}"   ;;
        *)       state_c="${YELLOW}${state}${NC}" ;;
    esac

    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "${url}" 2>/dev/null || echo "---")
    if [[ "${code}" =~ ^[23] ]]; then
        code_c="${GREEN}${code}${NC}"
    else
        code_c="${RED}${code}${NC}"
    fi

    printf "%-12s %-30s %-21b %-19b\n" "${name}" "${container}" "${state_c}" "${code_c}"
done
```

> Phase 2 replaces the `localhost:PORT` URLs here with `*.pjx.localhost`
> hostnames, and Phase 3 adds a Grafana row. Expect to edit this file twice more.

---

## Step 5 — `local/scripts/validate.sh`

CloudDevEnvironment's version resolves hierarchical targets across three repos.
pjx needs far less:

```bash
#!/bin/bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

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
NODE_PROJECTS=(pjx-web-react pjx-api-node pjx-graphql-apollo)
DOTNET_PROJECTS=(pjx-api-dotnet pjx-sso-identityserver)

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

> **Phase 4 revisits this file.** Per Decision D2, `pjx-sso-identityserver` stays
> on `netcoreapp3.1` while everything else moves to `net8.0`. If the .NET 8 SDK
> turns out not to build the 3.1 target, Phase 4 step 5 moves SSO into a
> `DOCKER_ONLY_PROJECTS` list so it is built via its container image instead of
> the local SDK. Written as-is here — the split does not exist yet.

---

## Step 6 — Make them executable and put them on `$PATH`

```bash
chmod +x local/scripts/*.sh
```

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

## Verify

```bash
# Reload PATH (or open a fresh terminal)
source ~/.bashrc

# 1. Scripts resolve from anywhere
which dev-up.sh status.sh validate.sh

# 2. Help text works
dev-up.sh --help
validate.sh --help

# 3. Bring the stack up and inspect it
dev-up.sh -d
status.sh          # every row should show running / 2xx-3xx

# 4. Non-destructive stop keeps containers
stop.sh
docker ps -a --filter name=pjx- --format '{{.Names}}\t{{.Status}}'
                   # → 5 containers, state Exited (not removed)

# 5. clean.sh refuses without explicit confirmation
echo "no" | clean.sh   # → "Aborted."
```

Step 3 is the one that matters: if `status.sh` shows every service green, the
stack is genuinely healthy before Phase 2 starts moving it behind a proxy.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-1-script-layer
```

The `~/.bashrc` PATH line lives inside the container, not the repo — it
disappears on the next rebuild, or remove it by hand.
