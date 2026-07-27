# Phase 5 — OpenTelemetry instrumentation

**Goal:** four of the five services exporting traces, metrics, and logs to the
Grafana LGTM stack, so the Grafana debugging workflows from `CDE:CLAUDE.md`
become usable against pjx.

**Risk:** Medium — touches application startup code in each instrumented service.
**Reversible:** yes, per service.

**Depends on:** Phase 3 (collector must exist) and Phase 4 for `pjx-api-dotnet`.
The Node and React work can be done before Phase 4.

> **`pjx-sso-identityserver` is out of scope for this phase.** Per Decision D2 it
> stays on `netcoreapp3.1`, and current OpenTelemetry packages target `net6.0`+.
> Its instrumentation is [Phase 8 step 5](phase-8-duende.md#step-5--now-add-telemetry).
> Practical effect: you can trace a request from React through Apollo to the Node
> API and through the .NET API's token *validation*, but token *issuance* and the
> login flow produce no spans.

```bash
git checkout -b feature/arch-phase-5-otel
```

---

## Split this phase in two

The .NET half is gated on Phase 4 — `OpenTelemetry.Extensions.Hosting` targets
net6.0+, so it cannot be installed on `netcoreapp3.1`. The Node half is not
gated on anything.

If you are following the recommended ordering from Decision D1 (Phase 4 after
Phase 3), do **5a** now for an early payoff, and **5b** after Phase 4.

| Sub-phase | Services | Gated on |
|---|---|---|
| **5a** | `pjx-api-node`, `pjx-graphql-apollo` | nothing |
| **5b** | `pjx-api-dotnet` | Phase 4 |
| **5c** | `pjx-web-react` (browser telemetry) | nothing; lowest value, do last |
| — | `pjx-sso-identityserver` | **Phase 8** — see the note above |

---

## Step 0 — Turn the exporter on

Phase 3 step 4 left `OTEL_EXPORTER_OTLP_ENDPOINT` empty by design. Set it in
`.env`:

```dotenv
OTEL_EXPORTER_OTLP_ENDPOINT=http://grafana-otel:4318
```

`grafana-otel` is the hostname set in `observability/docker-compose.yml`, and both
stacks share `pjx-network`, so it resolves container-to-container. Port 4318 is
OTLP/HTTP.

Confirm the app containers can see it before writing any code:

```bash
docker exec pjx-api-node-dev getent hosts grafana-otel || echo "NOT RESOLVABLE"
```

If that fails, the observability stack is not on `pjx-network` — fix that first
rather than debugging SDK config.

---

## Naming convention — get this right up front

CloudDevEnvironment's telemetry has an inconsistency its own docs have to warn
about (`CDE:CLAUDE.md`): **Loki keys on `service_name`** (e.g. `FhirGateway`)
while **Prometheus keys on `job` with a `Well/` prefix** (e.g.
`Well/FhirGateway`). Every query has to remember which is which.

Do not reproduce that. Use one flat, consistent `service.name` per service,
matching the container name minus the `-dev` suffix:

| Service | `service.name` |
|---|---|
| pjx-api-node | `pjx-api-node` |
| pjx-graphql-apollo | `pjx-graphql-apollo` |
| pjx-api-dotnet | `pjx-api-dotnet` |
| pjx-web-react | `pjx-web-react` |
| pjx-sso-identityserver | `pjx-sso-identityserver` (reserved — Phase 8) |

Set it via the standard env var rather than in code, so it stays declarative:

```yaml
    environment:
      - *otel-endpoint
      - OTEL_SERVICE_NAME=pjx-api-node
      - OTEL_RESOURCE_ATTRIBUTES=deployment.environment=development
```

---

## Step 5a — Node services

For both `pjx-api-node` and `pjx-graphql-apollo`:

```bash
cd projects/pjx-api-node
npm install --save \
  @opentelemetry/sdk-node \
  @opentelemetry/auto-instrumentations-node \
  @opentelemetry/exporter-trace-otlp-http \
  @opentelemetry/exporter-metrics-otlp-http
```

Create `src/telemetry.ts`:

```typescript
import { NodeSDK } from '@opentelemetry/sdk-node';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';

// No-op when the endpoint is unset, so the app runs normally with the
// observability stack stopped.
const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

export const sdk = endpoint
  ? new NodeSDK({
      traceExporter: new OTLPTraceExporter({ url: `${endpoint}/v1/traces` }),
      instrumentations: [getNodeAutoInstrumentations()],
    })
  : undefined;

if (sdk) {
  sdk.start();
  process.on('SIGTERM', () => {
    sdk.shutdown().finally(() => process.exit(0));
  });
}
```

**Import it first, before anything else.** Auto-instrumentation patches modules
at require time, so anything imported before the SDK starts is not traced. At the
very top of the entry point:

```typescript
import './telemetry';
// ...everything else after this line
```

Both services are TypeScript with `restify` (`pjx-api-node`) and Apollo Server
(`pjx-graphql-apollo`). Auto-instrumentation covers HTTP for both. Apollo also
has a dedicated GraphQL instrumentation in the auto-instrumentations bundle,
which gives per-resolver spans — useful, since resolver latency is exactly what
you want visibility into on a gateway.

### Verify 5a

```bash
dev-up.sh -d && obs-up.sh
curl -s https://node.pjx.localhost/ > /dev/null
curl -s https://ql.pjx.localhost/graphql > /dev/null

# Traces arrived — query Tempo through Grafana
curl -s -u admin:admin \
  'https://grafana.pjx.localhost/api/datasources/proxy/uid/tempo/api/search?tags=service.name%3Dpjx-api-node' \
  | head -c 400
```

Then in Grafana → Explore → Tempo, search `service.name = pjx-api-node`. Hit the
service through the browser and confirm spans appear within a few seconds.

---

## Step 5b — .NET services (after Phase 4)

```bash
cd projects/pjx-api-dotnet/src/Pjx_Api
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
```

In `Program.cs` (minimal hosting, post-Phase-4):

```csharp
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? "pjx-api-dotnet"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());
}
```

The SDK reads `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL`
from the environment automatically, so `AddOtlpExporter()` needs no arguments.
Set `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` in compose, since the default is
gRPC and we are exposing 4318.

The conditional matters for the same reason as in Node: unset endpoint means the
app starts clean with the observability stack down.

For logs, add `Serilog.Sinks.OpenTelemetry` — the project already uses Serilog,
so this is a sink registration rather than a logging rewrite.

> `AddEntityFrameworkCoreInstrumentation` emits a span per query. On SQLite in a
> demo app that is fine; be aware it is chatty if you ever point this at a real
> database.

**Do not repeat this for `projects/pjx-sso-identityserver`.** It is on
`netcoreapp3.1` and these packages will not install. Phase 8 picks it up once the
framework moves.

---

## Step 5c — React browser telemetry (optional)

Lowest value of the three, and the most intrusive: it requires a CORS-enabled
OTLP endpoint and adds meaningful bundle weight for a demo app.

If you do it: `@opentelemetry/sdk-trace-web` plus
`@opentelemetry/instrumentation-fetch`, exporting to
`https://otlp.pjx.localhost` — which needs a new Traefik route to the collector's
4318 port with permissive CORS headers.

**Recommendation: skip unless you specifically want frontend traces.** Server-side
spans from 5a and 5b already cover the request path end to end.

---

## Step 6 — A dashboard that matches pjx

Now that real telemetry exists, build the dashboard Phase 3 deliberately did not
copy. In Grafana, create panels for:

- Request rate by service —
  `rate(http_server_request_duration_seconds_count[5m])`
- Error rate —
  `rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])`
- p95 latency — `histogram_quantile(0.95, ...)` over the same metric
- Trace search by `service.name`

Then export to `observability/provisioning/dashboards/pjx-overview.json` so it is
provisioned on a fresh start.

---

## Verify

```bash
# 1. All instrumented services registered with Tempo
curl -s -u admin:admin \
  'https://grafana.pjx.localhost/api/datasources/proxy/uid/tempo/api/search/tag/service.name/values'

# 2. Prometheus is scraping metrics
curl -s -u admin:admin \
  'https://grafana.pjx.localhost/api/datasources/proxy/uid/prometheus/api/v1/label/job/values'

# 3. Logs arriving in Loki
curl -s -u admin:admin \
  'https://grafana.pjx.localhost/api/datasources/proxy/uid/loki/loki/api/v1/label/service_name/values'

# 4. Naming is consistent — the same service names in all three datasources,
#    with no prefixes. This is the check that keeps us out of
#    CloudDevEnvironment's service_name-vs-job trap.

# 5. The app still runs with telemetry off
OTEL_EXPORTER_OTLP_ENDPOINT= dev-up.sh -d && status.sh
```

Then exercise a full login flow in the browser and confirm you can follow a
single trace from React through Apollo to the Node API, and through the .NET
API's token validation on `/country/all`.

Expect a **gap where the SSO redirect happens** — that service is uninstrumented
until Phase 8. The trace resumes once the token reaches the .NET API. That gap is
the expected shape, not a broken trace context.

---

## Rollback

Per service — revert the entry-point import and remove the packages. Or disable
everything at once without touching code:

```dotenv
OTEL_EXPORTER_OTLP_ENDPOINT=
```

That is the payoff for making every integration conditional on the endpoint.
