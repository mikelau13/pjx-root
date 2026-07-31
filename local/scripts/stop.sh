#!/bin/bash
# stop.sh — non-destructive
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

cd "${REPO_ROOT}"
docker compose -f "${COMPOSE_FILE}" stop "${APP_SERVICES[@]}"
echo "Stack stopped. Containers kept — use dev-up.sh to resume."
