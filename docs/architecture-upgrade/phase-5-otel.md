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

### Also set the protocol

Because 4318 is HTTP and the .NET exporter defaults to **gRPC on 4317**, the
protocol has to be declared explicitly or `pjx-api-dotnet` silently sends nothing
in [Step 5b](#step-5b--net-services-after-phase-4).

In `docker-compose.devcontainer.yml`, add this line to the `environment:` block of
**all five** app services, below the existing `OTEL_RESOURCE_ATTRIBUTES` line:

```yaml
      - OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

Each block then reads:

```yaml
    environment:
      - *otel-endpoint
      - OTEL_SERVICE_NAME=pjx-api-dotnet
      - OTEL_RESOURCE_ATTRIBUTES=deployment.environment=development
      - OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
      - ASPNETCORE_ENVIRONMENT=Development
```

Insert bottom-to-top so the line numbers above your cursor stay valid. The
`x-otel-endpoint` anchor at the top of the file needs no change — a plain literal
is used here because the value is a constant, where the endpoint anchor exists to
hold a `${...}` substitution written once.

Check the file still parses before starting anything:

```bash
docker compose -f docker-compose.devcontainer.yml config > /dev/null && echo "YAML OK"
```

The Node and React SDKs ignore this var — `telemetry.ts` builds
`OTLPTraceExporter` with an explicit `${endpoint}/v1/traces` URL. It is set on all
five anyway to keep one uniform OTel block per service, and so
`pjx-sso-identityserver` is already correct when Phase 8 instruments it.

### Confirm before writing any code

```bash
docker exec pjx-api-node-dev getent hosts grafana-otel || echo "NOT RESOLVABLE"
```

```bash
for c in pjx-web-react pjx-graphql-apollo pjx-api-node pjx-api-dotnet pjx-sso-identityserver; do
  printf '%-26s %-24s %s\n' "$c" \
    "$(docker exec ${c}-dev printenv OTEL_SERVICE_NAME)" \
    "$(docker exec ${c}-dev printenv OTEL_EXPORTER_OTLP_PROTOCOL)"
done
```

Every line must show its **own** service name and `http/protobuf`. Two failure
modes this catches:

- **All five names identical** — the example value copy-pasted across services.
  Tempo would collapse them into one service and a React → Apollo → Node trace
  would look like one service calling itself.
- **Blank values** — env vars are fixed at container creation, so edits to compose
  or `.env` need `docker compose -f docker-compose.devcontainer.yml up -d` to take
  effect. Plain `up -d` preserves the `node_modules` anonymous volumes; only pass
  `-V` when you intend to rebuild those.

If `getent` fails, the observability stack is not on `pjx-network` — fix that first
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

> ### ⚠️ `npm install` does not reach the container
>
> `/app/node_modules` is an **anonymous volume** in
> `docker-compose.devcontainer.yml`, so it shadows the bind-mounted host tree.
> Installing from the devcontainer shell updates
> `projects/<svc>/node_modules` on the host and `package.json` /
> `package-lock.json` — but the running container keeps looking at the volume,
> whose contents were fixed by `RUN npm install` at image build time.
>
> The symptom is a compile error naming a package that is plainly in
> `package.json`:
>
> ```
> src/telemetry.ts(1,25): error TS2307: Cannot find module '@opentelemetry/sdk-node'
> ```
>
> which reads like a TypeScript path problem and sends you to `tsconfig.json`.
> It is not — the module genuinely is not there. `status.sh` shows the service
> `running` with HTTP `000---`, because nodemon crashed and is waiting for a file
> change while the container stays up.
>
> **`--build` alone does not fix it.** Compose carries existing anonymous volumes
> over to the recreated container, so the fresh image is still shadowed by the
> stale volume. You need `--renew-anon-volumes` (`-V`), which `dev-up.sh` does
> not expose:
>
> ```bash
> cd /workspaces/pjx-root
> docker compose -f docker-compose.devcontainer.yml up -d --build -V \
>   pjx-api-node pjx-graphql-apollo
> ```
>
> ```bash
> docker exec pjx-api-node-dev ls /app/node_modules/@opentelemetry | head -5
> ```
>
> For faster iteration, install straight into the volume instead — seconds, no
> rebuild:
>
> ```bash
> docker exec pjx-api-node-dev npm install
> ```
>
> Not durable (a `-V` recreate or `compose down -v` discards it), but the
> `package.json` and `package-lock.json` changes land on the bind mount and
> survive, so a later rebuild reproduces the state.
>
> Three services have this volume: `pjx-api-node`, `pjx-graphql-apollo`, and
> `pjx-web-react` — so [Step 5c](#step-5c--react-browser-telemetry-optional) hits
> it too. `pjx-api-dotnet` and `pjx-sso-identityserver` use bind mounts only and
> are unaffected.

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

The entry point is whatever the container's `start` script runs:

| Service | Entry point | From `package.json` |
|---|---|---|
| `pjx-api-node` | `src/app.ts` | `nodemon -L --exec ts-node src/app.ts` |
| `pjx-graphql-apollo` | `src/server.ts` | `nodemon -L --exec node --inspect=4555 -r ts-node/register ... src/server.ts` |

Note `pjx-graphql-apollo` has both a `dev` and a `start` script pointing at
`src/server.ts`; the container runs `start`. Ignore `"main": "server.js"` in that
`package.json` — it is stale and refers to a file that does not exist.

TypeScript hoists all `import` statements but preserves their **order**, so
placing `./telemetry` below the `restify` / Apollo Server imports loads those
modules unpatched. The failure is quiet: the SDK still starts and connects, so
nothing errors — you just get no HTTP spans, or a flat span per request with
nothing nested inside it. If traces look suspiciously thin, check import order
before anything else.

Both services are TypeScript with `restify` (`pjx-api-node`) and Apollo Server
(`pjx-graphql-apollo`). Auto-instrumentation covers HTTP for both. Apollo also
has a dedicated GraphQL instrumentation in the auto-instrumentations bundle,
which gives per-resolver spans — useful, since resolver latency is exactly what
you want visibility into on a gateway.

### Verify 5a

```bash
# -V, not dev-up.sh -d — see the anonymous-volume callout above
docker compose -f docker-compose.devcontainer.yml up -d --build -V \
  pjx-api-node pjx-graphql-apollo
obs-up.sh

curl -s https://node.pjx.test/ > /dev/null
curl -s https://ql.pjx.test/graphql > /dev/null

# Traces arrived — query Tempo through Grafana
curl -s -u admin:admin \
  'https://grafana.pjx.test/api/datasources/proxy/uid/tempo/api/search?tags=service.name%3Dpjx-api-node' \
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

> No `-V` rebuild needed here. `pjx-api-dotnet` mounts `/app` as a plain bind
> mount with **no anonymous volume**, so the `.csproj` edits are visible in the
> container immediately and `dotnet watch` restores on its own. Watch
> `docker logs -f pjx-api-dotnet-dev` for the restore rather than recreating the
> container.

### This goes in `Startup.cs`, not `Program.cs`

Phase 4 moved the project to `net8.0` but kept the ASP.NET Core 3.1 **`Startup`
pattern** — `Program.Main` builds a host with `UseStartup<Startup>()`, and there is
no `WebApplicationBuilder`. That is fully supported on .NET 8; it just means the
minimal-hosting `builder.Services` / `builder.Configuration` form does not
compile here.

Register in `Startup.ConfigureServices`, at the end of the method after the
existing `AddCors` block:

```csharp
var otlpEndpoint = Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: Configuration["OTEL_SERVICE_NAME"] ?? "pjx-api-dotnet"))
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

Translating from any minimal-hosting example you find online:

| Minimal hosting | `Startup` pattern |
|---|---|
| `builder.Services` | `services` (the `ConfigureServices` parameter) |
| `builder.Configuration` | `Configuration` (the `Startup` property) |

Usings — these sit between the `NSwag.*` and `Pjx.*` groups in the existing
alphabetical order:

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
```

`Program.cs` needs **no changes** for traces and metrics. It only comes into play
for the Serilog sink below, which attaches to the `LoggerConfiguration` already in
`Main`.

> `Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]` resolves because
> `Host.CreateDefaultBuilder` adds unprefixed environment variables. It does not
> need the separate `ConfigurationBuilder().AddEnvironmentVariables("PJX_")` that
> `ConfigureServices` uses for the SSO authority — that exists only to strip the
> `PJX_` prefix.

The SDK reads `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL`
from the environment automatically, so `AddOtlpExporter()` needs no arguments.

**`OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` is not optional.** The exporter
defaults to gRPC on 4317 and Step 0 points the endpoint at 4318. Get this wrong
and the app runs normally while no telemetry arrives — there is no startup error,
because the exporter fails asynchronously on a background thread. Confirm before
debugging anything in Grafana:

```bash
docker exec pjx-api-dotnet-dev printenv OTEL_EXPORTER_OTLP_PROTOCOL
```

The conditional matters for the same reason as in Node: unset endpoint means the
app starts clean with the observability stack down.

For logs, add `Serilog.Sinks.OpenTelemetry` — the project already uses Serilog,
so this is a sink registration rather than a logging rewrite.

### `AddEntityFrameworkCoreInstrumentation` — two gotchas

**The package ships prerelease only.** Latest is `1.17.0-beta.1`, which matches
the `1.17.0` of the other four. Plain `dotnet add package` finds no stable version
and installs nothing, leaving `Startup.cs` calling a method whose assembly is not
referenced:

```
error CS1061: 'TracerProviderBuilder' does not contain a definition for
'AddEntityFrameworkCoreInstrumentation'
```

```bash
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore --prerelease
```

**Expect no EF spans yet.** The instrumentation hooks EF Core's
`DiagnosticSource` events and its supported floor is far above 3.1, and
[Phase 4 left EF Core at 3.1.7](phase-4-dotnet8.md#outstanding--ef-core-was-not-upgraded)
while moving the target framework to `net8.0`. Database spans will likely be
missing from traces until that is fixed. **Do not debug the OTel registration over
it** — HTTP and HttpClient spans are unaffected and prove the pipeline works.

Keeping the package and the call is still the right move: it costs nothing, and EF
spans start appearing on their own once EF Core is upgraded. The alternative is
dropping the `.AddEntityFrameworkCoreInstrumentation()` line and the package
reference.

> Once it does work, it emits a span per query. Fine on SQLite in a demo app; be
> aware it is chatty if you ever point this at a real database.

**Do not repeat this for `projects/pjx-sso-identityserver`.** It is on
`netcoreapp3.1` and these packages will not install. Phase 8 picks it up once the
framework moves.

---

## Step 5c — React browser telemetry (optional)

Lowest value of the three, and the most intrusive: it requires a CORS-enabled
OTLP endpoint and adds meaningful bundle weight for a demo app.

If you do it: `@opentelemetry/sdk-trace-web` plus
`@opentelemetry/instrumentation-fetch`, exporting to
`https://otlp.pjx.test` — which needs a new Traefik route to the collector's
4318 port with permissive CORS headers.

**Recommendation: skip unless you specifically want frontend traces.** Server-side
spans from 5a and 5b already cover the request path end to end.

> If you do it, `pjx-web-react` has the same `/app/node_modules` anonymous volume
> as the two Node services — see the
> [callout in Step 5a](#step-5a--node-services). The React failure looks different
> though: CRA's webpack reports `Module not found: Can't resolve
> '@opentelemetry/sdk-trace-web'` and the dev server keeps serving the last good
> bundle, so the browser shows a stale working app rather than an error.

---

## Step 5d — Health checks

Grouped here because it edits the same startup code as the OTel work — same
files, same rebuild. Moved forward from Phase 10, where it was blocking a local
Kubernetes deploy for no good reason.

### What already exists

| Service | Endpoint | State |
|---|---|---|
| `pjx-api-node` | `/healthcheck` | ✅ exists |
| `pjx-graphql-apollo` | `/.well-known/apollo/server-health` | ✅ built into Apollo Server 2 |
| `pjx-api-dotnet` | `/api/calendar/event/healthcheck` | ⚠️ returns a hardcoded `"okay"` |
| `pjx-web-react` | `/` via nginx | adequate |
| `pjx-sso-identityserver` | — | ❌ none |

So endpoints are mostly present; what is missing is anything *using* them.

### Register framework health checks in both .NET services

The existing `EventController.HealthCheck()` returns a constant — it cannot tell
you the database is unreachable. `AddHealthChecks` can, and it exists on
`netcoreapp3.1` as well, so the SSO server is **not** blocked by Decision D2.

```csharp
services.AddHealthChecks()
        .AddDbContextCheck<CalendarDbContext>("database");
```

```csharp
app.UseEndpoints(endpoints =>
{
    // Liveness runs NO checks — it answers only "is the process up?".
    endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    // Readiness includes dependency checks.
    endpoints.MapHealthChecks("/health/ready");
    // ...existing endpoint registrations
});
```

> **Keep liveness and readiness separate, and keep dependency checks out of
> liveness.** A liveness probe that fails because the database blinked will
> restart a perfectly healthy pod, turning a brief outage into a crash loop.
> Readiness is where "can I serve traffic?" belongs — Kubernetes removes the pod
> from the Service instead of killing it.

For `pjx-sso-identityserver`, register the same pair without the
`AddDbContextCheck` (or with its own context if you want the user store covered).

### Add `healthcheck:` to compose

Independent of Kubernetes, and immediately useful:

```yaml
  pjx-api-node:
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:8081/healthcheck"]
      interval: 15s
      timeout: 3s
      retries: 3
      start_period: 30s
```

Do the same for the other four, using each service's own endpoint and container
port. This gives `docker ps` a real `(healthy)` / `(starting)` / `(unhealthy)`
column — which is exactly what would have explained the "Up 4 minutes but
returning 000" confusion during Phase 4: the containers were still starting, and
Docker would have said so.

### Point `status.sh` at the health endpoints

In `local/scripts/lib/common.sh`, replace the ad-hoc URLs:

```bash
SERVICE_HEALTH=(
    "React Web|pjx-web-react-dev|http://pjx-web-react:3000"
    "GraphQL|pjx-graphql-apollo-dev|http://pjx-graphql-apollo:4000/.well-known/apollo/server-health"
    ".NET API|pjx-api-dotnet-dev|http://pjx-api-dotnet:80/health/ready"
    "Node API|pjx-api-node-dev|http://pjx-api-node:8081/healthcheck"
    "SSO|pjx-sso-identityserver-dev|http://pjx-sso-identityserver:80/health/ready"
    "Grafana|pjx-grafana-otel|http://grafana-otel:3000/api/health"
)
```

`/health/ready` on the .NET API replaces `/swagger`, which returned a 302 and told
you nothing about whether the app could actually serve requests.

### Why now rather than in Phase 10

[Phase 7b](phase-7b-local-k8s.md) deploys to a local cluster, and its Helm
templates declare probes. Declaring probes against endpoints that do not exist
yet means debugging restart loops on your first Kubernetes deploy. Doing the
endpoints here means they are already proven under compose before Kubernetes ever
sees them. Phase 10 keeps only resource requests and limits.

---

## Step 6 — A dashboard that matches pjx

Now that real telemetry exists, build the dashboard Phase 3 deliberately did not
copy. Grafana is at <https://grafana.pjx.test> (`admin` / `admin`), datasource
**Prometheus**, query editor switched from *Builder* to **Code**.

### Start traffic first

Every panel below reads a `rate()` over a 5-minute window. With no requests in
that window they render "No data" or `NaN`, which is indistinguishable from a
broken panel. Leave this running in a second terminal while you build:

```bash
while true; do
  curl -sk https://node.pjx.test/ -o /dev/null
  curl -sk https://ql.pjx.test/.well-known/apollo/server-health -o /dev/null
  curl -sk https://api.pjx.test/swagger -o /dev/null
  sleep 2
done
```

Metrics export on a 60s interval, so allow a minute before judging a new panel.

### The panels

Four separate panels — one question each, not extra queries on one panel.

**1. Request rate by service** — unit `requests/sec (rps)`, min `0`, legend `{{job}}`

```promql
sum by (job) (rate(http_server_request_duration_seconds_count[5m]))
```

**2. Error rate** — same unit and legend

```promql
sum by (job) (rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
```

**3. p95 latency by service** — unit **Time → seconds (s)**, min `0`

```promql
histogram_quantile(0.95, sum by (job, le) (rate(http_server_request_duration_seconds_bucket[5m])))
```

**4. Trace search** — panel type **Table**, datasource **Tempo**, query type
**Search**, filter on `service.name`.

### Three things that trip this step up

**`sum by (job)` is not optional.** The raw metric carries `http_route`,
`http_response_status_code` and `instance` labels, so without aggregation you get
one series per route per status code per service — a dozen unreadable lines
instead of one per service. `job` is where the OTLP-to-Prometheus convention puts
the `service.name` resource attribute; a `service_name` label exists too and works
identically.

**Percentiles need `_bucket`, and `le` must survive the aggregation.** One
histogram emits `_count` (how many), `_sum` (total seconds) and `_bucket` (count
per latency bucket, labelled `le`). `histogram_quantile` reconstructs the
percentile from the bucket distribution, so it only works on `_bucket` — and
dropping `le` from the `sum by` collapses the distribution it needs. This is the
most common way the p95 panel silently returns nothing.

**An empty error panel is the healthy state.** Until something returns a 5xx, the
selector matches zero series and Grafana shows "No data". To confirm the panel
itself works, temporarily widen the regex to `[23]..` — lines should appear — then
change it back. If you would rather the panel be unambiguous, append
`or vector(0)` so it draws a flat zero line instead; the fallback series has an
empty legend label, which is the cosmetic cost.

### Expect three services, not five

`pjx-web-react` is a browser app with no server-side HTTP metrics (that is
[Step 5c](#step-5c--react-browser-telemetry-optional), recommended skipped), and
`pjx-sso-identityserver` stays uninstrumented on `netcoreapp3.1` until
[Phase 8](phase-8-duende.md). Both absences are correct.

### Provision it

Save in the UI first and iterate there. When settled, **Dashboard settings → JSON
Model**, copy it, and write to
`observability/provisioning/dashboards/pjx-overview.json`.

Two edits before saving the file, or provisioning fails silently:

- `"id": null` — the copied model carries the UI's database ID, which the
  provisioner will not accept.
- `"uid": "pjx-overview"` — a stable UID, so re-provisioning updates this
  dashboard instead of creating duplicates.

`provider.yml` sets `updateIntervalSeconds: 30`, so it lands in the **PJX** folder
within half a minute with no restart. It also sets `allowUiUpdates: true`, so UI
edits keep working — but they live only in the `grafana-data` volume until
re-exported. The file on disk is the source of truth.

> Datasource UIDs in `grafana/otel-lgtm` are plain — `prometheus`, `tempo`,
> `loki` — so the exported `"datasource": {"uid": "prometheus"}` references resolve
> on any fresh start. A UID mismatch is the usual reason a provisioned dashboard
> loads with every panel empty.

---

## Verify

> Run these in the devcontainer unless a command is marked HOST. See
> [Where to run commands](README.md#where-to-run-commands) — `localhost` means
> something different in each shell.

Send some traffic first — every check below reads a time window, and an idle stack
answers all of them with nothing.

```bash
# 1. All instrumented services registered with Tempo
#    → pjx-api-node, pjx-graphql-apollo, pjx-api-dotnet
curl -s -u admin:admin \
  'https://grafana.pjx.test/api/datasources/proxy/uid/tempo/api/search/tag/service.name/values'

# 2. Prometheus is scraping metrics
#    → the same three, plus otelcol-contrib (the LGTM image's own collector)
curl -s -u admin:admin \
  'https://grafana.pjx.test/api/datasources/proxy/uid/prometheus/api/v1/label/job/values'

# 3. Loki — EXPECTED EMPTY. See "Outstanding — no log pipeline" below.
curl -s -u admin:admin \
  'https://grafana.pjx.test/api/datasources/proxy/uid/loki/loki/api/v1/label/service_name/values'

# 4. Naming is consistent — the same service names in Tempo and Prometheus,
#    with no prefixes. This is the check that keeps us out of
#    CloudDevEnvironment's service_name-vs-job trap.

# 5. The app still runs with telemetry off
OTEL_EXPORTER_OTLP_ENDPOINT= dev-up.sh -d && status.sh
```

> **Check 1 is time-windowed.** Tempo's default lookback is short, so a service
> that has had no traffic recently drops off the list and looks uninstrumented.
> Before concluding anything is broken, hit it — `curl -sk
> https://api.pjx.test/swagger` — and re-run, or pass an explicit range:
>
> ```bash
> NOW=$(date +%s)
> docker exec pjx-grafana-otel sh -c "curl -s --get \
>   'http://localhost:3200/api/search/tag/service.name/values' \
>   --data-urlencode 'start=$((NOW-21600))' --data-urlencode 'end=${NOW}'"
> ```

Then exercise a full login flow in the browser and confirm you can follow a
single trace from React through Apollo to the Node API, and through the .NET
API's token validation on `/country/all`.

Expect a **gap where the SSO redirect happens** — that service is uninstrumented
until Phase 8. The trace resumes once the token reaches the .NET API. That gap is
the expected shape, not a broken trace context.

---

## Outstanding — no log pipeline

**Recorded 2026-08-08, after Steps 0/5a/5b/6 completed.** Traces and metrics work.
The **L in LGTM does not** — Loki holds nothing, and check 3 above returns
`{"status":"success"}` with no `data` key at all.

```bash
curl -s -u admin:admin \
  'https://grafana.pjx.test/api/datasources/proxy/uid/loki/loki/api/v1/label/service_name/values'
```

This is not a Loki or datasource fault. **Nothing anywhere is exporting logs:**

| Service | What it has | What is missing |
|---|---|---|
| `pjx-api-dotnet` | `Serilog.Sinks.Console` only | `Serilog.Sinks.OpenTelemetry` |
| `pjx-api-node` | `telemetry.ts` sets `traceExporter` | a log bridge (winston / pino transport) |
| `pjx-graphql-apollo` | same | same |

Container stdout is not collected either — there is no Alloy, Promtail, or Docker
log driver pointed at Loki, so `docker logs` output never leaves the daemon.

### Why metrics work but logs do not

This asymmetry is confusing enough to be worth stating plainly. `NodeSDK`
auto-configures an OTLP **metric** exporter as soon as
`OTEL_EXPORTER_OTLP_ENDPOINT` is set — which is why Node metrics appeared in
Prometheus without anyone writing metrics code.

Logs get no such default. Auto-instrumentation patches HTTP and database clients;
it does not intercept `console.log` or Serilog. Emitting OTel log records requires
a deliberate per-service bridge, and none was added.

### Why it is deferred

Traces and metrics already carry the demo: service map, request rate, p95 latency,
error rate, and end-to-end trace search. Closing this gap means editing startup
code in three services — the application-code work being held back in favour of
devcontainer and infrastructure. Nothing in Phases 6, 7, or 7b depends on it.

### When it is picked up

Roughly a half-day, and it does not need its own phase:

- **.NET** — `dotnet add package Serilog.Sinks.OpenTelemetry`, then add
  `.WriteTo.OpenTelemetry()` to the `LoggerConfiguration` in `Program.Main`. This
  is the one place Phase 5 touches `Program.cs` rather than `Startup.cs`.
- **Node** — `@opentelemetry/winston-transport` (or the pino equivalent), wired to
  the logger in `src/logger.ts` for Apollo. Keep it conditional on the endpoint,
  matching every other integration here.
- **Verify** — check 3 returns the three service names, and a trace's span ID
  correlates to its log lines in Grafana.

The correlation is the actual payoff: clicking a slow span and landing on that
request's logs. Without it, Loki would just be a second place to read the same
output `docker logs` already shows.

---

## Rollback

Per service — revert the entry-point import and remove the packages. Or disable
everything at once without touching code:

```dotenv
OTEL_EXPORTER_OTLP_ENDPOINT=
```

That is the payoff for making every integration conditional on the endpoint.
