# Deployable phase — Make the application deployable

> **Executes 11th of 14** — after [7c](phase-7c-cicd.md), before
> [Azure Foundation](phase-azure-foundation.md). This phase is named rather than
> numbered; see the [README](README.md#progress).

**Goal:** close the gaps between "runs in Docker Compose on localhost" and "runs
in Kubernetes" — PostgreSQL, real secrets, health probes, resource limits, and
runtime configuration for the React app.

**Risk: High.** This is application code, not infrastructure — the largest phase
after Phase 4.

**Reversible:** yes, but the database migration is one-way in practice.

**Depends on:** [Phase 7b](phase-7b-local-k8s.md) (a local cluster to prove the
changes against). **Not Azure Foundation** — most of this phase is local work.

> **Ordering — corrected.** This line previously read *"Depends on: Azure Foundation
> (Azure resources must exist)"*, which would have you provisioning ~$60–70/month
> of Azure and then leaving it idle for weeks of application work. Only two tails
> of this phase actually touch Azure:
>
> | Step | Needs Azure? |
> |---|---|
> | 1 — signing certificate | **only the tail** — `openssl` generation is local; `az keyvault secret set` and the CSI mount are not |
> | 2 — SQLite → PostgreSQL | **no** — [the step itself](#local-development-stays-on-sqlite-or-containerised-postgres) says to use `postgres:16-alpine` locally and *not* to point local dev at Azure |
> | 3 — React runtime configuration | no |
> | 5 — resource requests and limits | no |
> | 6 — observability wiring | **only the tail** — Grafana Cloud's auth header is read from Key Vault; Grafana Cloud itself is free tier, not Azure |
>
> **Run the local work first, on k3d, with production images.** Provision Azure Foundation
> when you are days from deploying, then come back for the two tails and go
> straight into [AKS Deploy](phase-aks-deploy.md). The original ordering predates
> Phase 7 being split into 7/7b/7c, which is what gave this project a local
> cluster to develop against.

```bash
git checkout -b feature/arch-deployable
```

---

## Suggested order within this phase

The step numbers are **not** the order to run them. Two subsections of Step 1 and
one of Step 6 need Azure; everything else is local work provable on k3d. Do the
free work first:

| Do | What | Needs Azure |
|---|---|---|
| ~~1~~ | ✅ **done 2026-09-07** — [EF Core 3.1.7 → 8](phase-4-dotnet8.md#how-it-actually-went-2026-09-07), Step 2's prerequisite. One real LINQ regression, found and fixed | no |
| 2 | [Step 3](#step-3--react-runtime-configuration) — React runtime config, plus making the React Service port a chart value | no |
| 3 | [Step 5](#step-5--resource-requests-and-limits) — resource requests and limits | no |
| 4 | [Step 1c](#step-1c--one-small-code-change-no-azure-needed) — the `Path.IsPathRooted` edit, on its own | no |
| 5 | [Step 2](#step-2--sqlite--postgresql) — SQLite → PostgreSQL against `postgres:16-alpine` | no |
| — | **[Azure Foundation](phase-azure-foundation.md) — provision Azure here** | — |
| 6 | [Step 1a](#step-1a--generate-and-store-after-phase-9) + [Step 1b](#step-1b--mount-it-via-the-csi-driver-after-phase-9) — store the certificate and mount it | **yes** |
| 7 | [Step 6](#step-6--observability-wiring) — the Grafana Cloud header comes from Key Vault | **yes** |
| — | [AKS Deploy](phase-aks-deploy.md) — deploy | — |

Steps 1a, 1b and 7 are quick once Key Vault exists, and 1b cannot be *tested*
before then regardless — the CSI driver only runs in AKS.

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

**The most important item in this phase.** It is the only one that is a live
vulnerability rather than an operational gap.

> **This step is split.** Reading the background and making the code change
> ([Step 1c](#step-1c--one-small-code-change-no-azure-needed)) need nothing.
> Storing the certificate ([1a](#step-1a--generate-and-store-after-phase-9)) and
> mounting it ([1b](#step-1b--mount-it-via-the-csi-driver-after-phase-9)) require
> Key Vault, so they run **after [Azure Foundation](phase-azure-foundation.md)**. Note
> that `local/scripts/azure/00-vars.sh` — which 1a sources for `${KV}` — is
> created by [Azure Foundation Step 1](phase-azure-foundation.md#step-1--naming-and-a-script-to-hold-it)
> and does not exist yet.

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

### Step 1a — Generate and store (**after Azure Foundation**)

> Requires Key Vault. Skip on a first pass through this phase.


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

### Step 1b — Mount it via the CSI driver (**after Azure Foundation**)

> The template below is guarded by `{{- if .Values.keyVault.enabled }}`, so it is
> safe to add early — with the flag false it renders to nothing and `helm
> template` still passes. It cannot be *tested* until AKS has the Secrets Store
> CSI driver.


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

> **Stale as written.** This step used to say *"delete
> `helm-pjx/templates/pjx-secret.yaml` entirely"*. That file no longer exists —
> the [Phase 7](phase-7-cicd.md) chart cleanup removed it. Nothing to delete;
> secrets come from Key Vault at pod start. Check `helm-pjx/templates/` before
> looking for it.

### Step 1c — One small code change (no Azure needed)

**Safe to do now.** This is the only part of Step 1 that runs without Key Vault,
and it changes nothing locally.

`projects/pjx-sso-identityserver/Startup.cs:88-90` loads the certificate relative
to the content root (the file uses fully-qualified type names, so match on this
exactly):

```csharp
            string certFile = section["CERTIFICATE"] ?? "pjx-sso-identityserver.rsa_2048.cert.pfx";
            string certPassword = section["PASSWORD"] ?? "password";
            var rsaCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(System.IO.Path.Combine(Environment.ContentRootPath, certFile), certPassword);
```

The CSI driver mounts to an absolute path such as `/mnt/secrets/sso-signing-pfx`,
and `Path.Combine` with a rooted second argument discards the first — so this
happens to work already. Make it explicit rather than relying on that:

```csharp
            string certFile = section["CERTIFICATE"] ?? "pjx-sso-identityserver.rsa_2048.cert.pfx";
            string certPassword = section["PASSWORD"] ?? "password";
            string certPath = System.IO.Path.IsPathRooted(certFile)
                ? certFile
                : System.IO.Path.Combine(Environment.ContentRootPath, certFile);
            var rsaCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, certPassword);
```

Local development is unaffected — `PJX_SSO__CERTIFICATE` is unset, so the
relative default still resolves against the content root exactly as before.

Verify with a browser sign-in on k3d, or:

```bash
curl -sk https://sso.pjx.test/.well-known/openid-configuration/jwks | head -5
```

A changed or empty JWKS means the certificate stopped loading.

---

## Step 2 — SQLite → PostgreSQL

Both .NET projects. `pjx-api-node` has no datastore and needs nothing.

> ### Step 0 first — upgrade EF Core to 8 in the API
>
> **This is a hard prerequisite, not a suggestion.** `Pjx_Api` and
> `Pjx.CalendarLibrary` still reference EF Core **3.1.7** while targeting
> `net8.0` — [Phase 4 left this
> undone](phase-4-dotnet8.md#outstanding--ef-core-was-not-upgraded). The
> `Npgsql...PostgreSQL 8.0.*` below depends on EF Core 8, so installing it
> reproduces exactly the failure Phase 5 hit:
>
> ```
> System.TypeLoadException: Method 'Create' in type
> 'Sqlite...QueryableMethodTranslatingExpressionVisitorFactory' ... does not
> have an implementation.
> ```
>
> Except here it is on the critical path — the provider swap *is* the phase.
>
> ```bash
> git checkout -b feature/arch-deployable   # EF upgrade lands here as step 0
> ```
>
> Upgrade `Microsoft.EntityFrameworkCore.Sqlite`, `.Design` and `.Tools` to
> `8.0.*` in **both** projects, one at a time, `dotnet build` and `dotnet test`
> after each. Then exercise `/country/all` and `/cities` in the browser before
> touching PostgreSQL — EF Core 8 translates LINQ more strictly, so queries that
> silently ran client-side on 3.1 now throw at runtime. Finding those against a
> database that still works is much easier than finding them mixed in with a
> provider swap.
>
> Doing it here rather than earlier is deliberate: this phase already regenerates
> migrations and re-tests all database access, so the LINQ regressions and the
> PostgreSQL move share one verification pass.
>
> Two things start working again as a side effect:
> [EF query spans in Grafana](phase-5-otel.md#step-5b--net-services-after-phase-4),
> and `AddDbContextCheck` on the API's readiness probe — which
> [Phase 5 had to remove](phase-4-dotnet8.md#it-is-now-blocking-not-merely-stale)
> and which [AKS Deploy](phase-aks-deploy.md) wants before production traffic.
>
> `dotnet-ef` also becomes usable: the tool is pinned to 8.x in
> [Phase 6](phase-6-devcontainer-image.md), and it cannot operate on an EF Core
> 3.1 project — so `dotnet ef migrations add InitialPostgres` below fails until
> this step is done.

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
> [Duende](phase-duende.md) forward — the framework, not the database, is the
> blocker. Establish it now rather than mid-migration.

### Regenerate migrations

Provider-specific SQL means the SQLite migrations cannot be reused:

```bash
rm -rf Migrations/
dotnet ef migrations add InitialPostgres
dotnet ef database update    # against Azure, using the allow-me firewall rule from Azure Foundation
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

> ### Node version — **partly done, differently than planned (2026-09-06)**
>
> This section used to say the build was stuck on `node:14.5.0-slim` until
> `react-scripts` was upgraded, because webpack 4 hashes with MD4 (removed in
> OpenSSL 3) and `--openssl-legacy-provider` does not exist on Node 14. The
> conclusion — that Node could not move first — turned out to be wrong.
>
> [Phase 7c Step 0](phase-7c-cicd.md#step-0--fix-the-production-dockerfiles-first)
> moved the production Dockerfile to **`node:18-slim`** while leaving
> `react-scripts` on 3.4.3, using the flag on Node 18's side:
>
> ```dockerfile
> FROM node:18-slim AS builder
> RUN npm ci
> RUN NODE_OPTIONS=--openssl-legacy-provider npm run build
> ```
>
> That works because the flag exists on Node 18 — the original reasoning had it
> backwards. It was forced by an unrelated problem: node:14 ships npm 6, which
> cannot read this project's `lockfileVersion: 3` lock file and silently resolved
> a newer `@types/babel__traverse` than TypeScript 3.7.5 can parse. See
> [docker-build-and-images.md](../reference/docker-build-and-images.md#case-study-when-the-two-dockerfiles-drift).
>
> **Current state:**
>
> | Item | State |
> |---|---|
> | `Dockerfile` builder stage | ✅ `node:18-slim` |
> | `Dockerfile` serve stage | ❌ still `nginx:1.19.0` (2020) |
> | `react-scripts` | ❌ still 3.4.3, EOL, config frozen at build time |
> | `typescript` | ❌ still `^3.7.5` |
> | `--openssl-legacy-provider` | in both `Dockerfile` and `local/scripts/validate.sh:63` — **keep both** until `react-scripts` moves |
>
> So the ordering constraint is gone. **The runtime-config change below no longer
> has to wait for the `react-scripts` upgrade** — it is independent, and doing it
> now is what lets a CI-built image be pointed at AKS. Upgrading `react-scripts`
> (5.x, or Vite) is still owed, and is what finally removes the OpenSSL flag from
> two places. `local/scripts/validate.sh:56-59` carries a comment whose premise
> ("the production Dockerfile is on Node 14 … and REJECTS it") is now false —
> correct it whenever you next touch that file.

### Where this step stands (2026-09-07)

| | Item | State |
|---|---|---|
| 3 | `src/utils/runtimeConfig.ts` | ✅ |
| 3 | `public/config.js` + `<script>` in `public/index.html` | ✅ verified in `build/` output |
| 3 | The 24 `process.env.REACT_APP_*` references across 6 files | ✅ `tsc --noEmit` clean, `validate.sh build` passes |
| 3 | Browser pass on Compose — sign in, country, city, calendar, Profile, sign out | ⬜ |
| 3 | ConfigMap under `templates/` | ⬜ wrong directory, see 🛑 below |
| **3b** | Mount it, and make the React port a value | ⬜ |
| **3c** | Warn when `config.js` fails to load | ⬜ |
| — | `nginx:1.19.0` → `nginx:1.27-alpine` | ⬜ see the end of this step |

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

Files to update — all identified in Phase 2. **24 references across 6 files**,
verified 2026-09-07:

| File | Line(s) | Reads |
|---|---|---|
| `src/utils/authConst.tsx` | **2–29 (17 refs)** | `SSO_ISSUER_URL` ×13, `SSO_CLIENT_ID`, `SSO_REDIRECT_URL`, `SILENT_REDIRECT_URL`, `LOGOFF_REDIRECT_URL` |
| `src/apollo/apolloClient.tsx` | 12 | `GRAPHQL_ENDPOINT` |
| `src/services/countryService.tsx` | 17 | `API_DOTNET_URL` |
| `src/services/calendarService.tsx` | 22 | `API_DOTNET_URL` |
| `src/services/authService.tsx` | 70, 107 | `SSO_CLIENT_ID`, `PUBLIC_URL` |
| `src/components/Menu/leftNavigator.tsx` | 94 | `PUBLIC_URL` |

Each edit is mechanical — `process.env.REACT_APP_API_DOTNET_URL` becomes
`config.apiDotnetUrl`, with an import of `runtimeConfig`. `authConst.tsx` is 17 of
the 24, so assign `const issuer = config.ssoIssuerUrl;` once at the top and build
the endpoints off it; that collapses 13 references to one.

> **`runtimeConfig.ts` deliberately covers 5 of the 8 variables.** There are eight
> distinct `REACT_APP_*` values in `.env`, and the three redirect URIs are all
> `publicUrl` plus a fixed path:
>
> ```
> REACT_APP_SSO_REDIRECT_URL     = https://pjx.test/signin-oidc
> REACT_APP_SILENT_REDIRECT_URL  = https://pjx.test/silentrenew
> REACT_APP_LOGOFF_REDIRECT_URL  = https://pjx.test/logout/callback
> ```
>
> **Compute** those in `authConst.tsx` rather than adding them to the config
> object or the ConfigMap:
>
> ```typescript
> const issuer = config.ssoIssuerUrl;
> const publicUrl = config.publicUrl;
>
> export const IDENTITY_CONFIG = {
>     authority: issuer,
>     client_id: config.ssoClientId,
>     redirect_uri: `${publicUrl}/signin-oidc`,
>     silent_redirect_uri: `${publicUrl}/silentrenew`,
>     post_logout_redirect_uri: `${publicUrl}/logout/callback`,
>     login: `${issuer}/login`,
>     // … the rest unchanged
> };
> ```
>
> That takes the ConfigMap from eight values to five and removes the case where
> four URLs agree and the fifth has a typo. It matters because those three
> redirect URIs must **exactly** match `Config.cs` on the SSO side — the same
> exact-match discipline as [Phase 2](phase-2-traefik.md) step 5, and the cause of
> the 401 debugged in [Phase 7b](phase-7b-local-k8s.md).

> **`.ts`, not `.tsx`, is correct for `runtimeConfig.ts`.** `.tsx` only enables
> JSX parsing, and this file exports a plain object. Four of the six files above
> are named `.tsx` while containing no JSX at all (`authConst`, `apolloClient`,
> `countryService`, `calendarService`) — that is an existing habit in this repo,
> not a convention to match.

Then the ConfigMap — **`helm-pjx/templates/pjx-web-config.yaml`**:

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

> 🛑 **It must be under `templates/`.** Helm renders **only** files in
> `templates/`; anything else in the chart root is inert. A `ConfigMap.yaml` in
> `helm-pjx/` is never created and its `{{ .Values… }}` placeholders never expand
> — with **no warning and no error**. This happened on 2026-09-07. Check with:
>
> ```bash
> helm template pjx-test helm-pjx/ -f helm-pjx/environments/local.yaml | grep -c PJX_CONFIG
> ```
>
> `0` means the file is in the wrong place. Also match the chart's naming — every
> other template is lowercase `pjx-*.yaml`:
>
> ```bash
> git mv helm-pjx/ConfigMap.yaml helm-pjx/templates/pjx-web-config.yaml
> ```

> **This changes the OIDC URLs again.** Phase 7 moved the ingress to path-based
> routing on one host, so the issuer becomes `https://demo.pjx.example.com/auth`,
> not a subdomain. `Config.cs`'s redirect URIs and CORS origins must match — the
> same exact-match discipline as Phase 2 step 5, with the same failure mode if
> they drift.

### Step 3b — Mount it, and make the port a value

**Still outstanding as of 2026-09-07.** The ConfigMap existing is not enough:
`helm-pjx/templates/pjx-web-react.yaml` has **no `volumes:` or `volumeMounts:` at
all**, so nothing reaches the pod. And its port is wrong for the production image:

| Line | Currently | Should be |
|---|---|---|
| 25 | `containerPort` block | a value |
| 30 | `port: 3000` — readiness probe | a value |
| 36 | `port: 3000` — liveness probe | a value |
| 51 | `port: 3000` — Service | a value |

`3000` is the CRA dev server. The production image serves from **nginx on 80**.
Deploy a CI-built image against today's chart and React returns **502** — the same
failure Phase 7b hit from the opposite direction.

These are **one change**, because a `subPath` mount over nginx's webroot only
makes sense for the production image. Doing the mount without the port leaves the
service unreachable; doing the port without the mount leaves it unconfigured.

Add to `values.yaml`:

```yaml
web:
  service:
    port: 80          # production nginx
```

and to `environments/local.yaml`, while the cluster still runs dev images:

```yaml
web:
  service:
    port: 3000        # CRA dev server
```

Then replace all four hardcoded `3000`s with `{{ .Values.web.service.port }}`, and
add the mount to the pod spec:

```yaml
        volumeMounts:
          - name: web-config
            mountPath: /usr/share/nginx/html/config.js
            subPath: config.js
      volumes:
        - name: web-config
          configMap:
            name: pjx-web-config
```

`subPath` is what makes this replace **one file** rather than masking the whole
directory — without it the mount hides every other file in nginx's webroot,
including `index.html`, and the app serves nothing.

> **Guard the mount for dev images.** The dev image is `react-scripts start`, which
> has no `/usr/share/nginx/html`. Either gate the volume on a value
> (`{{- if .Values.web.service.useNginx }}`) or accept that `local.yaml` and the
> deployed values diverge here. This is the same
> [dev-images-in-the-cluster](README.md#deferred-work) problem, surfacing again.

`stdin: true` / `tty: true` can also come off the React pod once it is nginx — they
exist only because `react-scripts start` exits when stdin closes.

### Step 3c — Warn when `config.js` fails to load

**Still outstanding as of 2026-09-07.** The `?? process.env.REACT_APP_*` fallback
in `runtimeConfig.ts` keeps local development working, but it also means the
build-time URLs stay **baked into the bundle** — verified: `sso.pjx.test` is
present in `build/static/js/main.*.chunk.js`.

So if the ConfigMap is missing, misnamed, or `config.js` 404s in the deployed
environment, the app **does not fail loudly**. It silently uses
`https://sso.pjx.test`, and you debug it through confusing CORS and OIDC errors
instead of a clear "no configuration" signal.

```typescript
// src/utils/runtimeConfig.ts
if (!(window as any).__PJX_CONFIG__) {
  console.warn('[pjx] config.js did not load — falling back to build-time values');
}
```

Cheap, and it converts a half-hour of misdirected CORS debugging into one console
line. Worth doing before the first AKS deploy, not after.

### Also: the serve stage is still stale

The builder stage is on `node:18-slim` as of Phase 7c. **`nginx:1.19.0` (2020) is
not** — and that is the layer actually shipped to AKS. Bump it while you are
editing the file:

```dockerfile
FROM nginx:1.27-alpine
```

Then `docker build -t pjx-prod-pjx-web-react:test projects/pjx-web-react` to
confirm the static output still serves. This is a serve-stage-only change, so it
cannot affect the `react-scripts` build — but re-run
`CI=true ./local/scripts/validate.sh build pjx-web-react` anyway, since that is
what CI gates on.

Note this is also where the **React Service port** must become a chart value:
`helm-pjx/templates/pjx-web-react.yaml` hardcodes `3000` (the CRA dev server) at
lines 30, 36, 51 and 52, while this production image serves on **80**. Deploy a
CI-built image against today's chart and React returns 502. Both belong in the
same change — see the callout at the end of
[Phase 7c Step 0](phase-7c-cicd.md#step-0--fix-the-production-dockerfiles-first).

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

> **Partly after Azure Foundation.** The exporter configuration and the env-var
> plumbing are local work. The Grafana Cloud auth header is read from Key
> Vault, so that half waits — see the
> [order table](#suggested-order-within-this-phase).


Phase 5 made the exporter conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`. Two
additions:

**Grafana Cloud** needs an auth header, which Azure Foundation stored in Key Vault and
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
git branch -D feature/arch-deployable
```

The Azure-side changes do not revert with git:

```bash
# Remove the seeded secrets if abandoning
az keyvault secret delete --vault-name "${KV}" --name sso-signing-pfx
az keyvault secret delete --vault-name "${KV}" --name sso-signing-password
```

The regenerated migrations and dropped SQLite databases are one-way. Local
SQLite files can be recreated by running the reverted branch's migrations.
