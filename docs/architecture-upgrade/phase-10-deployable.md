# Phase 10 — Make the application deployable

**Goal:** close the gaps between "runs in Docker Compose on localhost" and "runs
in Kubernetes" — PostgreSQL, real secrets, health probes, resource limits, and
runtime configuration for the React app.

**Risk: High.** This is application code, not infrastructure — the largest phase
after Phase 4.

**Reversible:** yes, but the database migration is one-way in practice.

**Depends on:** Phase 9 (Azure resources must exist).

```bash
git checkout -b feature/arch-phase-10-deployable
```

---

## Why this phase exists

Six things make the current application non-deployable. Ordered by severity:

| # | Problem | Evidence |
|---|---|---|
| 1 | **The token signing key is public** | `pjx-sso-identityserver.rsa_2048.cert.pfx` is tracked in git; password is `password` |
| 2 | **Committed base64 "secrets"** | `helm-pjx/templates/pjx-secret.yaml` ships `sso-password: cGFzc3dvcmQNCg==` = `password\r\n` |
| 3 | **SQLite** | `Data Source=AspIdUsers.db` and `Data Source=PjxCalendar.db` — file-backed, single-replica, lost on reschedule |
| 4 | **React config is baked at build time** | `REACT_APP_*` are compile-time substitutions; no ConfigMap can change them |
| 5 | **No health probes, and no endpoints to probe** | Nothing in `helm-pjx/templates/`; no `AddHealthChecks` anywhere |
| 6 | **No resource requests or limits** | Nothing in `helm-pjx/templates/` — AKS scheduling and autoscaling misbehave |

---

## Step 1 — Replace the signing certificate

**Do this first.** It is the only item that is a live vulnerability rather than
an operational gap.

### Understand what cannot be undone

The certificate and its private key are in **git history**, and
`projects/pjx-api-dotnet/src/Pjx_CreateCertificates/generated/…cert.key` is
tracked too. `pjx-root` is a public repository.

Deleting the files does **not** remove them from history, and rewriting history
does not help either — anyone who cloned or forked already has them, and GitHub
retains unreferenced objects. **The only real remedy is to treat that key as
permanently compromised and never use it anywhere but localhost.**

So: generate a new one, keep it out of git entirely, and leave the old one alone
or delete it as tidying — not as remediation.

### Generate and store

```bash
source local/scripts/azure/00-vars.sh

PFX_PASSWORD="$(openssl rand -base64 24)"
openssl req -x509 -newkey rsa:2048 -nodes -days 730 \
  -subj "/CN=pjx-sso-signing" \
  -keyout /tmp/sso-signing.key -out /tmp/sso-signing.crt
openssl pkcs12 -export -out /tmp/sso-signing.pfx \
  -inkey /tmp/sso-signing.key -in /tmp/sso-signing.crt \
  -passout "pass:${PFX_PASSWORD}"

az keyvault secret set --vault-name "${KV}" --name sso-signing-pfx \
  --file /tmp/sso-signing.pfx --encoding base64
az keyvault secret set --vault-name "${KV}" --name sso-signing-password \
  --value "${PFX_PASSWORD}"

shred -u /tmp/sso-signing.key /tmp/sso-signing.crt /tmp/sso-signing.pfx
```

> This is a self-signed signing certificate, which is correct — it signs tokens,
> it does not authenticate a TLS endpoint. It never needs to be CA-issued. Its
> only consumer is the API validating tokens via the discovery document's JWKS.

### Mount it via the CSI driver

`helm-pjx/templates/pjx-secretprovider.yaml`:

```yaml
{{- if .Values.keyVault.enabled }}
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: pjx-keyvault
spec:
  provider: azure
  parameters:
    clientID: {{ .Values.keyVault.clientId }}
    keyvaultName: {{ .Values.keyVault.name }}
    tenantId: {{ .Values.keyVault.tenantId }}
    objects: |
      array:
        - objectName: sso-signing-pfx
          objectType: secret
          objectEncoding: base64
        - objectName: sso-signing-password
          objectType: secret
        - objectName: identity-connection-string
          objectType: secret
        - objectName: calendar-connection-string
          objectType: secret
        - objectName: otlp-endpoint
          objectType: secret
        - objectName: otlp-headers
          objectType: secret
  secretObjects:
    - secretName: pjx-runtime-secrets
      type: Opaque
      data:
        - objectName: sso-signing-password
          key: PJX_SSO__PASSWORD
        - objectName: identity-connection-string
          key: ConnectionStrings__DefaultConnection
        - objectName: otlp-endpoint
          key: OTEL_EXPORTER_OTLP_ENDPOINT
        - objectName: otlp-headers
          key: OTEL_EXPORTER_OTLP_HEADERS
{{- end }}
```

Then **delete `helm-pjx/templates/pjx-secret.yaml` entirely.** Nothing should
replace it — secrets now come from Key Vault at pod start.

### One small code change

`Startup.cs:85-87` loads the certificate relative to the content root:

```csharp
string certFile = section["CERTIFICATE"] ?? "pjx-sso-identityserver.rsa_2048.cert.pfx";
var rsaCertificate = new X509Certificate2(
    Path.Combine(Environment.ContentRootPath, certFile), certPassword);
```

The CSI driver mounts to an absolute path such as `/mnt/secrets/sso-signing-pfx`,
and `Path.Combine` with a rooted second argument discards the first — so this
happens to work already. Make it explicit rather than relying on that:

```csharp
string certFile = section["CERTIFICATE"] ?? "pjx-sso-identityserver.rsa_2048.cert.pfx";
string certPath = Path.IsPathRooted(certFile)
    ? certFile
    : Path.Combine(Environment.ContentRootPath, certFile);
var rsaCertificate = new X509Certificate2(certPath, certPassword);
```

Local development is unaffected — the relative default still resolves.

---

## Step 2 — SQLite → PostgreSQL

Both .NET projects. `pjx-api-node` has no datastore and needs nothing.

```bash
cd projects/pjx-api-dotnet/src/Pjx_Api
dotnet remove package Microsoft.EntityFrameworkCore.Sqlite
dotnet add    package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.*
```

Change `UseSqlite(...)` to `UseNpgsql(...)` in the `DbContext` registration, and
update `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=pjx_calendar;Username=pjx;Password=password"
}
```

Repeat for `projects/pjx-sso-identityserver` — but note it is on
`netcoreapp3.1` per Decision D2, so pin the provider to a compatible major:

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 3.1.*
```

> **Verify this resolves before going further.** If the 3.1-compatible Npgsql
> provider cannot be installed alongside IS4, that is a hard signal to pull
> [Phase 8](phase-8-duende.md) forward — the framework, not the database, is the
> blocker. Establish it now rather than mid-migration.

### Regenerate migrations

Provider-specific SQL means the SQLite migrations cannot be reused:

```bash
rm -rf Migrations/
dotnet ef migrations add InitialPostgres
dotnet ef database update    # against Azure, using the allow-me firewall rule from Phase 9
```

### Expect these differences

SQLite is permissive; PostgreSQL is not. The failures show up at runtime, not
compile time:

| Area | What changes |
|---|---|
| Identifier casing | Postgres folds unquoted identifiers to lowercase; EF quotes them, so `PascalCase` table names become case-sensitive |
| `DateTime` | Npgsql 6+ requires `timestamptz` values to be UTC — a `DateTime` with `Kind=Unspecified` throws. The calendar feature stores dates, so **expect to hit this** |
| Booleans | SQLite stores 0/1; Postgres has a real `boolean` |
| Auto-increment | `AUTOINCREMENT` → `serial`/`identity` |
| Empty vs null strings | SQLite is loose about the distinction; Postgres is not |

The `DateTime` one is the most likely to bite. If the calendar starts throwing
`Cannot write DateTime with Kind=Unspecified`, normalise to UTC at the entity
boundary rather than scattering conversions through the query code.

### Local development stays on SQLite or containerised Postgres

Do not point local dev at Azure. Add a Postgres service to
`docker-compose.devcontainer.yml` so local and deployed use the same engine:

```yaml
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: pjx
      POSTGRES_PASSWORD: password
      POSTGRES_DB: pjx_calendar
    volumes:
      - pjx-pgdata:/var/lib/postgresql/data
    networks: [pjx-network]
```

This is the point where `clean.sh`'s confirmation prompt (Phase 1) starts
earning its keep — there is now a real volume to lose.

---

## Step 3 — React runtime configuration

**The problem:** `react-scripts` substitutes `REACT_APP_*` at *build* time. The
production image (`projects/pjx-web-react/Dockerfile`) builds with
`npm run build` and serves the static output from nginx. So the API and issuer
URLs are frozen into the JavaScript bundle, and no Kubernetes env var or
ConfigMap can change them.

Left unaddressed, the deployed app loads and then tries to reach whatever was
baked in at build time — `https://api.pjx.test` after Phase 2 — which does not
resolve from a user's browser pointed at your AKS demo.

**The fix:** serve configuration as a separate file that nginx delivers and the
bundle reads at startup.

Add `projects/pjx-web-react/public/config.js` as the local default:

```javascript
// Runtime configuration. Overwritten in deployed environments by a ConfigMap
// mounted at /usr/share/nginx/html/config.js — see helm-pjx/templates.
window.__PJX_CONFIG__ = {
  GRAPHQL_ENDPOINT:  "https://ql.pjx.test",
  SSO_ISSUER_URL:    "https://sso.pjx.test",
  SSO_CLIENT_ID:     "pjx-web-react",
  API_DOTNET_URL:    "https://api.pjx.test",
  PUBLIC_URL:        "https://pjx.test"
};
```

Load it before the bundle, in `public/index.html`:

```html
<script src="%PUBLIC_URL%/config.js"></script>
```

Then refactor the consumers to prefer runtime config, falling back to build-time
values so local `.env` development keeps working:

```typescript
// src/utils/runtimeConfig.ts
const rc = (window as any).__PJX_CONFIG__ ?? {};

export const config = {
  graphqlEndpoint: rc.GRAPHQL_ENDPOINT  ?? process.env.REACT_APP_GRAPHQL_ENDPOINT,
  ssoIssuerUrl:    rc.SSO_ISSUER_URL    ?? process.env.REACT_APP_SSO_ISSUER_URL,
  ssoClientId:     rc.SSO_CLIENT_ID     ?? process.env.REACT_APP_SSO_CLIENT_ID,
  apiDotnetUrl:    rc.API_DOTNET_URL    ?? process.env.REACT_APP_API_DOTNET_URL,
  publicUrl:       rc.PUBLIC_URL        ?? process.env.REACT_APP_PUBLIC_URL,
};
```

Files to update — all identified in Phase 2:

- `src/utils/authConst.tsx` — the largest, builds ~12 OIDC endpoints from `REACT_APP_SSO_ISSUER_URL`
- `src/apollo/apolloClient.tsx`
- `src/services/countryService.tsx`
- `src/services/calendarService.tsx`
- `src/services/authService.tsx`
- `src/components/Menu/leftNavigator.tsx`

Derived values like `REACT_APP_SSO_REDIRECT_URL` should be **computed** from
`publicUrl` rather than configured separately — five URLs that must agree is five
chances to typo one.

Then the ConfigMap:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: pjx-web-config
data:
  config.js: |
    window.__PJX_CONFIG__ = {
      GRAPHQL_ENDPOINT: "https://{{ .Values.ingress.host }}/graphql",
      SSO_ISSUER_URL:   "https://{{ .Values.ingress.host }}/auth",
      SSO_CLIENT_ID:    "pjx-web-react",
      API_DOTNET_URL:   "https://{{ .Values.ingress.host }}/api",
      PUBLIC_URL:       "https://{{ .Values.ingress.host }}"
    };
```

mounted as a `subPath` volume over `/usr/share/nginx/html/config.js`.

> **This changes the OIDC URLs again.** Phase 7 moved the ingress to path-based
> routing on one host, so the issuer becomes `https://demo.pjx.example.com/auth`,
> not a subdomain. `Config.cs`'s redirect URIs and CORS origins must match — the
> same exact-match discipline as Phase 2 step 5, with the same failure mode if
> they drift.

### Also: the production image is stale

`projects/pjx-web-react/Dockerfile` builds on `node:14.5.0-slim` and serves from
`nginx:1.19.0`. Both are long past end of life, and this is the image going to
AKS. Bump to `node:18-alpine` and `nginx:1.27-alpine` while you are editing it —
and re-run `validate.sh build pjx-web-react`, since `react-scripts` 3.4.3 is
sensitive to the Node version (see Phase 6).

---

## Step 4 — Health endpoints — **moved to Phase 5**

> The endpoints now live in
> [Phase 5 Step 5d](phase-5-otel.md#step-5d--health-checks), and the Kubernetes
> probe declarations in [Phase 7b](phase-7b-local-k8s.md). Both are done before
> this phase.
>
> Moved because declaring probes against endpoints that do not exist yet means
> debugging restart loops on a first Kubernetes deploy — and because the endpoint
> work belongs with the OTel edits to the same startup files. It also gives
> Docker Compose real `healthcheck:` blocks, which is useful long before
> Kubernetes.

Nothing to do here beyond confirming the AKS values file keeps the probe timings
generous enough for a cold start on a small node:

```yaml
        readinessProbe:
          httpGet: { path: /health/ready, port: 80 }
          initialDelaySeconds: 10
        livenessProbe:
          httpGet: { path: /health/live, port: 80 }
          initialDelaySeconds: 30
          failureThreshold: 3
```

> On a single `B2ms` node, .NET cold start can exceed 30 seconds under
> contention. If pods restart-loop on first deploy, raise
> `initialDelaySeconds` before assuming the app is broken — or use a
> `startupProbe`, which exists precisely for slow-starting containers. Timings
> that were fine on k3d locally may be too tight on a contended AKS node.

---

## Step 5 — Resource requests and limits

Nothing has any. Without requests the scheduler cannot place pods sensibly and
one service can starve the rest — on a single 8GB node that is a real risk, not
a theoretical one.

In `values.yaml`, per service:

```yaml
  resources:
    requests: { cpu: 50m,  memory: 128Mi }
    limits:   { cpu: 500m, memory: 512Mi }
```

Reasonable starting points; the .NET services want more memory than the Node
ones. Budget the *sum of requests* to fit one `B2ms` (2 vCPU / 8GB) with room
for Traefik, cert-manager, the CSI driver, and kube-system.

> Set memory `limits` deliberately: exceeding one is an immediate OOM kill, not
> throttling. CPU limits throttle instead, which is why the CPU limit can sit
> well above its request but memory should not.

---

## Step 6 — Observability wiring

Phase 5 made the exporter conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`. Two
additions:

**Grafana Cloud** needs an auth header, which Phase 9 stored in Key Vault and
step 1's `SecretProviderClass` projects as `OTEL_EXPORTER_OTLP_HEADERS`. The
OpenTelemetry SDKs read it natively — no code change.

**App Insights, dormant.** Add the package now so switching later is
configuration rather than a code change:

```bash
cd projects/pjx-api-dotnet/src/Pjx_Api
dotnet add package Azure.Monitor.OpenTelemetry.AspNetCore
```

```csharp
var aiConnection = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(aiConnection))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(o => o.ConnectionString = aiConnection);
}
```

Both exporters register independently on the presence of their own config:

| Config present | Result |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP → local LGTM or Grafana Cloud |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Monitor |
| Both | Both — useful for comparing during a migration |
| Neither | No-op; runs clean offline |

That is the flexibility requirement: no backend switch variable, no code change
to move, and both can run at once.

---

## Verify

> Run these in the devcontainer (it carries `az`, `kubectl` and `helm` from
> Phase 6). Browser checks and anything on a published port are HOST-side. See
> [Where to run commands](README.md#where-to-run-commands).

```bash
# 1. No secrets in the repo
git ls-files | grep -Ei '\.pfx$|\.key$|\.pem$'      # → nothing, or localhost-only material
grep -rn 'cGFzc3dvcmQ' helm-pjx/ || echo "clean"
test ! -f helm-pjx/templates/pjx-secret.yaml && echo "pjx-secret.yaml removed"

# 2. No SQLite left
grep -rn -i 'sqlite' --include=*.csproj --include=*.json projects/ || echo "clean"

# 3. Health endpoints answer locally
dev-up.sh -d
curl -s -o /dev/null -w 'dotnet  %{http_code}\n' https://api.pjx.test/health/ready
curl -s -o /dev/null -w 'node    %{http_code}\n' https://node.pjx.test/health
curl -s -o /dev/null -w 'apollo  %{http_code}\n' https://ql.pjx.test/.well-known/apollo/server-health

# 4. Runtime config is served and consumed
curl -s https://pjx.test/config.js       # → window.__PJX_CONFIG__ = {...}
#    In the browser console: window.__PJX_CONFIG__ is populated

# 5. Every deployment declares probes and resources
helm template pjx-release helm-pjx/ -f helm-pjx/environments/dev.yaml \
  | grep -c 'readinessProbe'    # → one per service
helm template pjx-release helm-pjx/ -f helm-pjx/environments/dev.yaml \
  | grep -c 'requests:'         # → one per service

# 6. Postgres works locally and the calendar round-trips
validate.sh test pjx-api-dotnet
```

**Then the manual browser pass** from [Phase 2](phase-2-traefik.md#verify) — but
this time the **calendar CRUD matters most**. It is the feature that exercises
`DateTime` handling through EF Core, which is where the Postgres migration is
most likely to have broken something a build cannot catch.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-10-deployable
```

The Azure-side changes do not revert with git:

```bash
# Remove the seeded secrets if abandoning
az keyvault secret delete --vault-name "${KV}" --name sso-signing-pfx
az keyvault secret delete --vault-name "${KV}" --name sso-signing-password
```

The regenerated migrations and dropped SQLite databases are one-way. Local
SQLite files can be recreated by running the reverted branch's migrations.
