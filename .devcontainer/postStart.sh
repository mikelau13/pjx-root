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
