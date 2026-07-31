#!/bin/bash
# Show the state of all pjx dev services.
set -uo pipefail   # no -e: check commands return non-zero when services are down
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BOLD='\033[1m';  NC='\033[0m'

printf "${BOLD}%-12s %-30s %-10s %-8s${NC}\n" "SERVICE" "CONTAINER" "STATE" "HTTP"
printf '%.0s-' {1..64}; echo

for entry in "${SERVICE_HEALTH[@]}"; do
    IFS='|' read -r name container url <<< "${entry}"

    state=$(docker inspect -f '{{.State.Status}}' "${container}" 2>/dev/null || echo "absent")
    case "${state}" in
        running) state_c="${GREEN}${state}${NC}" ;;
        absent)  state_c="${RED}${state}${NC}"   ;;
        *)       state_c="${YELLOW}${state}${NC}" ;;
    esac

    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "${url}" 2>/dev/null || echo "---")
    if [[ "${code}" =~ ^[23] ]]; then
        code_c="${GREEN}${code}${NC}"
    else
        code_c="${RED}${code}${NC}"
    fi

    printf "%-12s %-30s %-21b %-19b\n" "${name}" "${container}" "${state_c}" "${code_c}"
done
