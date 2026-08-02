#!/bin/bash
set -euo pipefail

# Resolve the repo root from this script's own location so the paths cannot
# drift out of sync with the devcontainer mount again.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

for proj in pjx-web-react pjx-api-node pjx-graphql-apollo; do
    dir="${REPO_ROOT}/projects/${proj}"
    if [ -e "${dir}/node_modules" ] && [ ! -w "${dir}/node_modules" ]; then
        echo "!! ${proj}/node_modules is not writable by $(whoami) — likely root-owned." >&2
        echo "   Docker creates it as root when mounting the /app/node_modules volume." >&2
        echo "   Fix from a HOST terminal:" >&2
        echo "     sudo chown -R \$(id -un):\$(id -gn) <host-path-to-repo>/projects" >&2
        exit 1
    fi
done

echo "Setting up PJX development environment (root: ${REPO_ROOT})..."

npm install -g nodemon ts-node typescript

git config --global init.defaultBranch main
git config --global core.autocrlf input
git config --global --add safe.directory "${REPO_ROOT}"

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
    if [ ! -d "${dir}" ]; then
        echo "!! SKIPPED ${proj} — directory not found at ${dir}" >&2
        continue
    fi

    # A bare `dotnet restore` fails with MSB1011 when a folder holds both a
    # .sln and a .csproj (pjx-sso-identityserver does). Name the target.
    target="$(find "${dir}" -maxdepth 1 -name '*.sln' | head -1)"
    [ -n "${target}" ] || target="$(find "${dir}" -maxdepth 1 -name '*.csproj' | head -1)"

    if [ -z "${target}" ]; then
        echo "!! SKIPPED ${proj} — no .sln or .csproj in ${dir}" >&2
        continue
    fi

    echo "==> dotnet restore: ${proj} ($(basename "${target}"))"
    (cd "${dir}" && dotnet restore "$(basename "${target}")")
done

# Put the developer scripts on PATH for interactive shells.
PROFILE_LINE='export PATH="$PATH:/workspaces/pjx-root/local/scripts"'
if ! grep -qF "${PROFILE_LINE}" "${HOME}/.bashrc" 2>/dev/null; then
    echo "${PROFILE_LINE}" >> "${HOME}/.bashrc"
    echo "Added local/scripts to PATH in ~/.bashrc"
fi

TOOLS_LINE='export PATH="$PATH:$HOME/.dotnet/tools"'
if ! grep -qF "${TOOLS_LINE}" "${HOME}/.bashrc" 2>/dev/null; then
    echo "${TOOLS_LINE}" >> "${HOME}/.bashrc"
    echo "Added ~/.dotnet/tools to PATH in ~/.bashrc"
fi

echo "Setup complete."
