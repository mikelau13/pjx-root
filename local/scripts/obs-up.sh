#!/bin/bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

cd "${REPO_ROOT}"

# pjx-network is owned by docker-compose.devcontainer.yml, which creates it with
# compose's labels. NEVER `docker network create` it by hand: an unlabelled
# network makes compose refuse to adopt it ("incorrect label
# com.docker.compose.network set to ''") and leaves the devcontainer detached,
# silently breaking status.sh. Start the app stack first instead.
if ! docker network inspect pjx-network >/dev/null 2>&1; then
    echo "ERROR: pjx-network does not exist. Run dev-up.sh first." >&2
    exit 1
fi

docker compose -f observability/docker-compose.yml up -d
echo "Grafana starting → https://grafana.pjx.test (first boot takes ~30s)"
