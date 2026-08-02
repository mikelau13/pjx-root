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

# Trust the repo's mkcert CA (Phase 2). Re-applied every start because a
# devcontainer rebuild discards the system trust store.
CA=/workspaces/pjx-root/local/central-router/config/ca/rootCA.pem
if [ -f "$CA" ] && ! grep -q "$(openssl x509 -in "$CA" -noout -subject)"
/etc/ssl/certs/ca-certificates.crt 2>/dev/null; then
    sudo cp "$CA" /usr/local/share/ca-certificates/pjx-mkcert-ca.crt
    sudo update-ca-certificates >/dev/null 2>&1
    echo "Installed pjx mkcert CA into the trust store"
fi

# Trust the repo's mkcert CA (Phase 2). Runtime `mkcert -install` is lost on
# every rebuild, and the CA cannot go in the image: the build context is
# .devcontainer/ and the CA is gitignored (per-machine).
CA=/workspaces/pjx-root/local/central-router/config/ca/rootCA.pem
if [ -f "$CA" ] && [ ! -f /usr/local/share/ca-certificates/pjx-mkcert-ca.crt ]; then
    sudo cp "$CA" /usr/local/share/ca-certificates/pjx-mkcert-ca.crt
    sudo update-ca-certificates >/dev/null 2>&1
    echo "Installed pjx mkcert CA into the trust store"
fi

echo "Post-start setup completed"
