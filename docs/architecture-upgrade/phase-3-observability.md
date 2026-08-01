# Phase 3 — Grafana LGTM observability stack

**Goal:** Grafana running at `https://grafana.pjx.localhost`, ready to receive
OTLP telemetry.

**Risk:** Low — a self-contained container with no changes to app services.
**Reversible:** yes.

**Depends on:** Phase 2 (needs Traefik for the hostname route).

```bash
git checkout -b feature/arch-phase-3-observability
```

---

## Set expectations first

**Grafana will show no application data at the end of this phase, and that is
the expected outcome.**

In CloudDevEnvironment, wiring OTLP is *only* an environment variable —
`CDE:AwareServices/backend/docker-compose.yml:7` defines
`OTEL_EXPORTER_OTLP_ENDPOINT` as a YAML anchor and each service references it.
That works because those services already carry the OpenTelemetry SDK.

None of the five pjx services have any instrumentation. This phase stands up the
*collector and UI*; [Phase 5](phase-5-otel.md) adds the SDKs that produce data.
Splitting them keeps each phase independently verifiable — a broken Grafana
container and a broken SDK integration have completely different symptoms, and
you do not want to debug both at once.

Reference: `CDE:Grafana/docker-compose.yml`.

---

## Step 1 — The stack

Create `observability/docker-compose.yml`:

```yaml
name: pjx-otel

services:
  grafana-otel:
    image: grafana/otel-lgtm:0.18.1
    hostname: grafana-otel
    container_name: pjx-grafana-otel
    environment:
      - GF_SECURITY_ALLOW_EMBEDDING=true
    ports:
      # OTLP receivers, published so host-side tooling can also emit.
      # Grafana's own UI (3000) is NOT published — it is reached via Traefik.
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
    volumes:
      # HOST_PROJECT_PATH for the same reason as Phase 1 Step 6a and Phase 2
      # Step 1: the host daemon resolves bind-mount sources, so a relative path
      # would resolve to a non-existent /workspaces/... path and Docker would
      # mount an empty directory — Grafana would start with no dashboards.
      # Fallback is `..` because this compose file lives in observability/.
      - ${HOST_PROJECT_PATH:-..}/observability/provisioning/dashboards/provider.yml:/otel-lgtm/grafana/conf/provisioning/dashboards/custom-dashboards.yaml
      - ${HOST_PROJECT_PATH:-..}/observability/provisioning/dashboards:/otel-lgtm/custom-dashboards
      - grafana-data:/var/lib/grafana
    networks:
      - pjx-network
    labels:
      - "traefik.enable=true"
      - "traefik.constraint-label=pjx-public"
      - "traefik.http.routers.pjx-grafana.rule=Host(`grafana.pjx.localhost`)"
      - "traefik.http.routers.pjx-grafana.entrypoints=https"
      - "traefik.http.routers.pjx-grafana.tls=true"
      - "traefik.http.services.pjx-grafana.loadbalancer.server.port=3000"
      - "traefik.http.routers.pjx-grafana-http.rule=Host(`grafana.pjx.localhost`)"
      - "traefik.http.routers.pjx-grafana-http.entrypoints=http"
      - "traefik.http.routers.pjx-grafana-http.middlewares=to-https"

volumes:
  grafana-data:

networks:
  pjx-network:
    external: true
    name: pjx-network
```

Three deliberate differences from the reference:

- **Directory is `observability/`, not `Grafana/`.** Lowercase matches the rest
  of pjx-root's layout.
- **A single `pjx-network`** instead of joining three external stack networks.
  `CDE:Grafana/docker-compose.yml:31-39` attaches to `services_default` and
  `c3api_default` because its stacks each have their own network. pjx has one.
- **Added `grafana-data` volume.** The reference does not persist Grafana state,
  so dashboards and preferences vanish on recreate. Worth fixing here.

Grafana's port 3000 is intentionally not published — it would collide with the
React dev server, and everything goes through Traefik now anyway.

---

## Step 2 — Dashboard provisioning

```bash
mkdir -p observability/provisioning/dashboards
```

Create `observability/provisioning/dashboards/provider.yml`:

```yaml
apiVersion: 1

providers:
  - name: pjx-dashboards
    orgId: 1
    folder: PJX
    type: file
    disableDeletion: false
    updateIntervalSeconds: 30
    allowUiUpdates: true
    options:
      path: /otel-lgtm/custom-dashboards
```

The `otel-lgtm` image ships its own datasources (`prometheus`, `loki`, `tempo`)
pre-provisioned, so there is nothing to configure there.

> **Do not copy `CDE:Grafana/provisioning/dashboards/error-dashboard.json`.** Its
> panel queries filter on `job="Well/FhirGateway"` and similar
> AwareServices-specific labels, so it renders empty against pjx and looks
> broken. Build a pjx dashboard in Phase 5 once real telemetry exists, then
> export it into this folder.

---

## Step 3 — Add the hostname and the start script

Add to `runArgs` in `.devcontainer/devcontainer.json` — if you already included
`grafana.pjx.localhost` in Phase 2 step 4, it is done:

```jsonc
"--add-host=grafana.pjx.localhost:127.0.0.1",
```

Create `local/scripts/obs-up.sh`:

```bash
#!/bin/bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

cd "${REPO_ROOT}"
docker network create pjx-network 2>/dev/null || true
docker compose -f observability/docker-compose.yml up -d
echo "Grafana starting → https://grafana.pjx.localhost (first boot takes ~30s)"
```

```bash
chmod +x local/scripts/obs-up.sh
```

Optionally add a Grafana row to `local/scripts/status.sh`:

```bash
    "Grafana|pjx-grafana-otel|https://grafana.pjx.localhost"
```

---

## Step 4 — Reserve the OTLP endpoint variable

Add the plumbing now so Phase 5 is purely a code change. In
`docker-compose.devcontainer.yml`, near the top:

```yaml
# Set to http://grafana-otel:4318 to export telemetry. Empty disables the
# exporter, so the stack runs normally with the observability stack stopped.
x-otel-endpoint: &otel-endpoint "OTEL_EXPORTER_OTLP_ENDPOINT=${OTEL_EXPORTER_OTLP_ENDPOINT:-}"
```

Then reference it in each of the five services' `environment:` list:

```yaml
    environment:
      - *otel-endpoint
```

This is lifted directly from `CDE:AwareServices/backend/docker-compose.yml:7`.
The empty-by-default behaviour is the valuable part: the OpenTelemetry SDKs treat
an unset endpoint as "do not export", so once Phase 5 lands you can still run the
app without the observability stack and it will not spam connection errors.

Set the value in `.env`:

```dotenv
# Observability — set to http://grafana-otel:4318 once services are instrumented
# (Phase 5). Empty means no telemetry export.
OTEL_EXPORTER_OTLP_ENDPOINT=
```

---

## Verify

```bash
obs-up.sh
sleep 30

# 1. Container is up
docker ps --filter name=pjx-grafana-otel --format '{{.Names}}\t{{.Status}}'

# 2. Reachable through Traefik with a valid certificate
curl -s -o /dev/null -w '%{http_code}\n' https://grafana.pjx.localhost/login   # → 200

# 3. Traefik registered the route
curl -s http://localhost:9090/api/http/routers | grep -o 'pjx-grafana[^"]*'

# 4. All three datasources provisioned
curl -s -u admin:admin https://grafana.pjx.localhost/api/datasources \
  | grep -o '"type":"[^"]*"' | sort -u
# → prometheus, loki, tempo

# 5. OTLP receivers accepting connections
curl -s -o /dev/null -w 'OTLP HTTP: %{http_code}\n' -X POST \
  -H 'Content-Type: application/json' -d '{}' http://localhost:4318/v1/traces
# → 200 or 400 (both prove the receiver is listening; connection refused does not)

# 6. The PJX dashboard folder exists
curl -s -u admin:admin https://grafana.pjx.localhost/api/folders | grep -o '"title":"[^"]*"'
```

In a browser: open `https://grafana.pjx.localhost`, log in with `admin`/`admin`,
and confirm Explore lists the three datasources. **Querying them returns
nothing. That is correct for this phase.**

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-3-observability
docker compose -f observability/docker-compose.yml down --volumes
```
