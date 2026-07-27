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
sudo lsof -i :443 || echo "443 free"
```

All four must pass before Phase 1.

---

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
