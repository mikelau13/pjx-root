#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

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
# NODE_PROJECTS and DOTNET_PROJECTS come from lib/common.sh

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
