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
