#!/bin/bash
# Shared definitions for the pjx developer scripts. Source this, do not run it.
#
# Adding a service to docker-compose.devcontainer.yml is picked up automatically
# by dev-up.sh / stop.sh / clean.sh, because APP_SERVICES is derived rather than
# hardcoded. Only SERVICE_HEALTH and the project lists need a manual line, and
# only for things compose cannot infer.

LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${LIB_DIR}/../../.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.devcontainer.yml"

# Every compose service except `workspace` — see the warning above.
mapfile -t APP_SERVICES < <(
    docker compose -f "${COMPOSE_FILE}" config --services | grep -v '^workspace$' | sort
)
if [[ ${#APP_SERVICES[@]} -eq 0 ]]; then
    echo "ERROR: no services found in ${COMPOSE_FILE}" >&2
    exit 1
fi

# status.sh needs a health endpoint per service; compose cannot infer these.
# Format: "display|container|health_url"
#
# URLs use the compose SERVICE NAME and the CONTAINER port, not localhost and
# the published port. status.sh runs inside the devcontainer, which is a sibling
# container on pjx-network — `localhost` there is its own loopback, so
# localhost:3000 would report every service down even when all are healthy.
# Service names resolve via Docker's embedded DNS on the shared network.
# (Note .NET is :80 internally, published as 6001; SSO is :80, published as 5001.)
SERVICE_HEALTH=(
    "React Web|pjx-web-react-dev|http://pjx-web-react:3000"
    "GraphQL|pjx-graphql-apollo-dev|http://pjx-graphql-apollo:4000/.well-known/apollo/server-health"
    ".NET API|pjx-api-dotnet-dev|http://pjx-api-dotnet:80/swagger"
    "Node API|pjx-api-node-dev|http://pjx-api-node:8081"
    "SSO|pjx-sso-identityserver-dev|http://pjx-sso-identityserver:80"
    # Service name + container port, consistent with the other rows — status.sh
    # runs inside the devcontainer on pjx-network. Using the Traefik hostname
    # would work too but adds a dependency on the router and CA trust.
    "Grafana|pjx-grafana-otel|http://grafana-otel:3000/api/health"
)

# validate.sh needs to know how each project builds.
NODE_PROJECTS=(pjx-web-react pjx-api-node pjx-graphql-apollo)
DOTNET_PROJECTS=(pjx-api-dotnet pjx-sso-identityserver)
