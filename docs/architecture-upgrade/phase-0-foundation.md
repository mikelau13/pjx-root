# Phase 0 — Foundation fixes

**Goal:** a devcontainer that actually provisions itself, mounts only pjx-root,
and does not squat on port 443.

**Risk:** Low. **Reversible:** yes, `git revert`.

Nothing here adds architecture. It fixes three bugs that would silently
undermine every later phase. Do not skip it — Phase 2 assumes port 443 is free,
and Phases 1/5 assume dependencies are actually installed.

```bash
git checkout -b feature/arch-phase-0-foundation
```

---

## Step 1 — Fix the workspace mount

The mount currently exposes the parent directory, which includes
`CloudDevEnvironment`.

In `docker-compose.devcontainer.yml`, change the `workspace` service volume:

```yaml
# before
volumes:
  - ..:/workspaces/pjx-root:cached

# after
volumes:
  - .:/workspaces/pjx-root:cached
```

Then in `.devcontainer/devcontainer.json`, the doubled path is no longer
correct:

```jsonc
// before
"workspaceFolder": "/workspaces/pjx-root/pjx-root",

// after
"workspaceFolder": "/workspaces/pjx-root",
```

> Both edits must land together. Changing one without the other leaves you with
> a container whose working directory does not exist.

---

## Step 2 — Make `setup.sh` path-independent

The hardcoded paths in `.devcontainer/setup.sh` are what caused the silent
no-op. Derive the root from the script location instead, so it cannot drift
again.

Replace the whole body of `.devcontainer/setup.sh` with:

```bash
#!/bin/bash
set -euo pipefail

# Resolve the repo root from this script's own location so the paths cannot
# drift out of sync with the devcontainer mount again.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "Setting up PJX development environment (root: ${REPO_ROOT})..."

npm install -g nodemon ts-node typescript

git config --global init.defaultBranch main
git config --global core.autocrlf input

# Node projects
for proj in pjx-web-react pjx-api-node pjx-graphql-apollo; do
    dir="${REPO_ROOT}/projects/${proj}"
    if [ -f "${dir}/package.json" ]; then
        echo "==> npm install: ${proj}"
        (cd "${dir}" && npm install)
    else
        echo "!! SKIPPED ${proj} — no package.json at ${dir}" >&2
    fi
done

# .NET projects
for proj in pjx-api-dotnet pjx-sso-identityserver; do
    dir="${REPO_ROOT}/projects/${proj}"
    if [ -d "${dir}" ]; then
        echo "==> dotnet restore: ${proj}"
        (cd "${dir}" && dotnet restore)
    else
        echo "!! SKIPPED ${proj} — directory not found at ${dir}" >&2
    fi
done

echo "Setup complete."
```

Two things changed beyond the paths:

- `set -euo pipefail` — the script now fails loudly instead of printing success
  over a failed install.
- Missing projects print a `SKIPPED` warning to stderr rather than passing
  silently. That is the specific behaviour that hid the bug.

---

## Step 3 — Free up port 443

In `docker-compose.yml`, the `pjx-sso-identityserver` service publishes three
ports. Remove the bare `443`:

```yaml
# before
ports:
  - 5001:80
  - 5002:443
  - 443:443

# after
ports:
  - 5001:80
  - 5002:443
```

`5002` still reaches the container's 443, so nothing about how you use the SSO
server changes. Phase 2 needs host 443 for Traefik.

> `docker-compose.devcontainer.yml` does not have this problem — it already
> publishes only 5001 and 5002. No change needed there.

---

## Step 4 — Rebuild the container

In VS Code: `Ctrl+Shift+P` → **Dev Containers: Rebuild Container**.

Watch the `postCreateCommand` output this time. It should print an `npm install`
or `dotnet restore` line for all five projects, and no `SKIPPED` warnings.

---

## Verify

Run these inside the rebuilt container:

```bash
# 1. Workspace is pjx-root itself, and CloudDevEnvironment is NOT visible
pwd                              # → /workspaces/pjx-root
ls /workspaces/pjx-root          # → docs, helm-pjx, kubernetes, projects, ...
ls /workspaces/ | grep -c CloudDevEnvironment   # → 0

# 2. Dependencies actually installed
ls projects/pjx-web-react/node_modules       | head -3
ls projects/pjx-api-node/node_modules        | head -3
ls projects/pjx-graphql-apollo/node_modules  | head -3

# 3. .NET restore produced obj/ output
ls projects/pjx-api-dotnet/src/Pjx_Api/obj/project.assets.json

# 4. Nothing is holding host 443
# Run on the HOST, not in the container. Do NOT use `lsof -i :443`: it also
# matches remote port 443, so any outbound HTTPS connection is a false positive.
ss -tlnp | grep ':443 ' || echo "443 free"
```

All four must pass before Phase 1.

---

## Step 3b — Nine defects found during execution

Written up **after** Phase 0 completed. Steps 1–3 were derived by reading files;
every defect below was found only by running things. On a fresh clone these are
already fixed — this section records why each change exists.

The headline: **four of the nine traced to `universal:2-linux`**, and swapping
the base image resolved them together. That swap is
[Phase 6](phase-6-devcontainer-image.md)'s work, brought forward out of necessity.

| # | Defect | Fix | Origin |
|---|---|---|---|
| 1 | `setup.sh` probed hardcoded paths that never existed → silent no-op | Derive `REPO_ROOT` from `BASH_SOURCE`; `set -euo pipefail`; warn on skip | pre-existing |
| 2 | Mount `..` exposed the parent dir, incl. `CloudDevEnvironment` | Mount `.`; drop the doubled `workspaceFolder` | pre-existing |
| 3 | IdentityServer published host `443` | Remove `- 443:443` | pre-existing |
| 4 | `docker-in-docker` feature contradicted the host-socket mount | Swap to `docker-outside-of-docker` | pre-existing |
| 5 | `universal:2-linux` ships a Yarn apt source with an expired GPG key → every feature install died at exit 100 | Base swap (below) | base image |
| 6 | `remoteUser: vscode` — that user does not exist in `universal:2-linux` (it uses `codespace`) | Base swap → `vscode` exists | base image |
| 7 | CRLF line endings → `#!/bin/bash\r`, reported misleadingly as "not found" (exit 127) | `sed -i 's/\r$//'`; `.gitattributes` with `*.sh text eol=lf` | pre-existing |
| 8 | App containers wrote root-owned `node_modules`, `obj/`, `bin/` into the bind-mounted tree (378 entries) | `runServices: ["workspace"]`; `chown -R`; delete empty mountpoint dirs | pre-existing |
| 9 | Bare `dotnet restore` is ambiguous where a `.sln` and `.csproj` share a folder (MSB1011) | Resolve the target explicitly, `.sln` first | **introduced by step 2 of this doc** |

Plus one that survived three separate fix attempts:

**`universal:2-linux` bakes in docker-in-docker.** Its
`/usr/local/share/docker-init.sh` starts a nested `dockerd`, which claims
`/var/run/docker.sock` and shadows the `docker-outside-of-docker` feature
entirely — its socat proxy never ran. Symptom: `docker ps` inside the container
succeeded but listed nothing, and `docker version` reported server `28.1.1`
against the host's `29.5.3`. Swapping the feature did not help, nor did deleting
the derived image; the daemon comes from the base, not from any feature.

### The base swap

`.devcontainer/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/devcontainers/base:jammy
```

That single change fixed defects 5, 6 and the nested daemon, and took the image
from **15.9 GB to ~1 GB**. With it:

```jsonc
"remoteUser": "vscode",
"ghcr.io/devcontainers/features/dotnet:2": { "version": "8.0" }
```

Only the 8.0 SDK is installed. `pjx-sso-identityserver` still targets
`netcoreapp3.1` and restores fine under it (warning NETSDK1138), and runs from its
own 3.1 container image — validating
[Phase 6](phase-6-devcontainer-image.md)'s "one SDK" decision ahead of schedule.

### Corrections to this document

- **The rebuild command differs by where you are.** From the host it is "Dev
  Containers: **Rebuild and Reopen in** Container"; only when already attached is
  it "Rebuild Container". Use the cached variant — "Without Cache" is warranted
  only when baked-in files must go.
- **`sudo lsof -i :443` was the wrong check** — it matches *remote* port 443, and
  it must run on the host. Corrected in Verify above.

### Guard against root-owned `node_modules`

Defect 8 recurs whenever `node_modules` is missing and the app containers start:
Docker needs a mountpoint for the `- /app/node_modules` anonymous volume and
creates the directory **as root** inside the bind-mounted tree. A fresh clone or
a git-based recovery triggers it, because `node_modules` is gitignored and does
not come back.

Add this to `setup.sh`'s Node loop so it fails with one actionable line instead of
a 20-line npm `EACCES` trace:

```bash
    if [ -e "${dir}/node_modules" ] && [ ! -w "${dir}/node_modules" ]; then
        echo "!! ${proj}/node_modules is not writable by $(whoami) — likely root-owned." >&2
        echo "   Docker creates it as root when mounting the /app/node_modules volume." >&2
        echo "   Fix from a HOST terminal:" >&2
        echo "     sudo chown -R \$(id -un):\$(id -gn) <host-path-to-repo>/projects" >&2
        exit 1
    fi
```

**Prefer `chown` over `rm -rf` as the remedy.** The directories are empty, so
there is nothing to delete, and `chown` is sufficient. It also avoids a recursive
delete on a relative path — `projects/...` from the host and from the container
are different trees, and that ambiguity has already destroyed this repo once.

### Commit after every step

Phase 0's nine fixes lived only in the working tree for hours. A `sudo rm -rf`
aimed at the wrong namespace later destroyed the untracked Phase 1 work, which
was only recoverable via VS Code's local history. **Commit after each numbered
step, and push the branch early** — `git push -u origin <branch>` costs nothing
and makes loss recoverable.

## Known issue you may hit

The devcontainer requests `ghcr.io/devcontainers/features/dotnet:1` at version
`3.1`. .NET Core 3.1 is out of support and Microsoft has been retiring its
download endpoints. **If this feature now fails to install**, do not fight it —
it is a signal to pull Decision D1 forward and do Phase 4 earlier than planned.

Two ways through if it blocks you:

- **Preferred:** jump to [Phase 4](phase-4-dotnet8.md) now, then come back.
  Phases 1–3 do not depend on the .NET version.
- **Stopgap:** pin the feature to `"version": "8.0"` and accept that the .NET
  projects will not build until Phase 4. Phases 1–3 still complete, since they
  only touch infrastructure.

Note this in the phase commit message if it happens, so the sequencing change
is recorded.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-0-foundation
```

Then rebuild the container. Since nothing outside the repo changed, that is a
complete rollback.
