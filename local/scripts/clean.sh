# clean.sh — destructive, so it confirms first. This mirrors the biggest footgun in CloudDevEnvironment, where docker compose down silently wipes seeded databases.
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
# `rm -fsv` = force, stop first, and remove anonymous volumes — scoped to the
# named services. Deliberately NOT `down --remove-orphans`: `down` ignores a
# service list, and --remove-orphans would classify the devcontainer
# (`workspace`) as an orphan and delete it out from under you.
docker compose -f "${COMPOSE_FILE}" rm -fsv "${APP_SERVICES[@]}"
echo "Environment cleaned."
