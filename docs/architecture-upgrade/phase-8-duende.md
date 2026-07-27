# Phase 8 — Migrate SSO to Duende IdentityServer

**Goal:** move `pjx-sso-identityserver` off the archived IdentityServer4 onto a
supported OIDC library, and onto `net8.0`.

**Status: OPTIONAL. Gate: before making the demo publicly reachable.**

**Risk:** Medium-high — rewrites auth configuration and startup.
**Reversible:** on its branch.

**Depends on:** Phase 4 (API side already on `net8.0` and free of IS4) and
**Phase 11** — this phase runs *last*, after the AKS deployment is working.

> **Ordering note.** This phase keeps the number 8 but executes after Phase 11.
> Decision D5 makes the first AKS demo **IP-restricted**, so the unpatched
> `netcoreapp3.1` SSO container is not internet-facing and this migration is not
> a prerequisite for deploying anything. That was the point of choosing the
> restriction: prove the deployment pipeline first, then do the auth work, then
> open it up. **Removing the IP restriction is the final step of this phase** —
> see the end of this document.

```bash
git checkout -b feature/arch-phase-8-duende
```

---

## Why this is deferred, and what triggers it

Phase 4 deliberately left the SSO server on `netcoreapp3.1` with IdentityServer4
4.1.2. That is a reasonable position for local and demo use: IS4 is a
self-hosted, in-process library with no external service dependency, its clients
are declared in `Config.cs` under version control, and the OIDC boundary to the
API is protocol-level so the frameworks can differ.

What makes it stop being reasonable:

| Trigger | Why it matters |
|---|---|
| **Making the demo public** | `mcr.microsoft.com/dotnet/aspnet:3.1` left support in December 2022, on Debian 10 (past LTS). Unpatched runtime *and* base OS, reachable from the internet. The Phase 9 IP allowlist is what defers this |
| **A new advisory** against IS4 or its transitive dependencies | It will never be fixed — 4.1.2 is the final release |
| **Wanting SSO telemetry** | Current OpenTelemetry packages target `net6.0`+, so the auth hop stays dark in Grafana ([Phase 5](phase-5-otel.md)) |
| **CI scanning noise** | Phase 7's GHCR/Dependabot scanning flags the 3.1 image on every run, permanently |

Re-run this whenever you want a current read on the first two:

```bash
cd projects/pjx-sso-identityserver
dotnet list package --vulnerable --include-transitive
```

---

## Why Duende rather than OpenIddict or Keycloak

| Option | Licence | Effort | Keeps config in git? |
|---|---|---|---|
| **Duende IdentityServer** | Commercial; free community licence under the revenue threshold | **Lowest** — direct successor, same lineage and concepts | Yes |
| OpenIddict | Apache 2.0 | Medium — different API surface | No — applications are database-backed |
| Keycloak | Apache 2.0 | Highest — external service, delete the in-repo project | No — external config |

Duende **is** IdentityServer4's continuation by the same authors. Namespaces move
from `IdentityServer4.*` to `Duende.IdentityServer.*`, and the in-memory
`Clients` / `ApiScopes` / `IdentityResources` model you already use in
`Config.cs` carries over largely intact.

That last column is the deciding factor. IS4's static `Config.cs` client list is
version-controlled, code-reviewed, and needs no seeding step. OpenIddict stores
applications in the database, so client registration moves into `SeedData.cs`
plus a migration — a real regression in reviewability and automation. Duende
preserves the property; OpenIddict does not.

**Check the current Duende licence terms before committing.** The community
licence is free below a revenue threshold, which a personal or demo project sits
inside, but the terms are Duende's to change and this plan is not legal advice.

---

## Step 1 — Framework and packages

```xml
<TargetFramework>net8.0</TargetFramework>
```

```bash
cd projects/pjx-sso-identityserver
dotnet remove package IdentityServer4.AspNetIdentity
dotnet add package Duende.IdentityServer.AspNetIdentity
dotnet add package Duende.IdentityServer.EntityFramework
```

Bump the rest to 8.0.x: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
`Microsoft.AspNetCore.Identity.UI`, `Microsoft.EntityFrameworkCore.Sqlite`,
`Microsoft.EntityFrameworkCore.Tools`,
`Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`,
`Microsoft.AspNetCore.Authentication.Google`. Also `NSwag.AspNetCore` 13.7 → 14.x
and `Serilog.AspNetCore` 3.2 → 8.x.

Base images in `Dockerfile` and `Dockerfile.dev`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
```

---

## Step 2 — Namespaces and startup

Mechanical, mostly:

```bash
grep -rln 'IdentityServer4' --include=*.cs .
```

Then `IdentityServer4.` → `Duende.IdentityServer.` across `Config.cs`,
`Startup.cs`, `SeedData.cs`, `Controllers/`, and `Views/`.

In `Startup.cs`, `AddIdentityServer()` keeps its name but its options and some
builder extensions changed. Work through the official upgrade guide rather than
guessing — Duende publishes a version-by-version path from IdentityServer4.

---

## Step 3 — Preserve the client contract exactly

**Nothing about the OIDC contract may change.** The React app and the .NET API
both depend on it, and neither is being modified in this phase.

From `Config.cs` (line numbers as of Phase 2), the `pjx-web-react` client must
keep:

- `ClientId`: `pjx-web-react`
- `RedirectUris`: `https://pjx.localhost/signin-oidc`, `/dashboard`,
  `/callback`, `/silentrenew`
- `PostLogoutRedirectUris`: `https://pjx.localhost`, `/logout/callback`
- `AllowedCorsOrigins`: `https://pjx.localhost`
- Authorization code + PKCE (`oidc-client` 1.10.1 in the React app)

**And the issuer must stay `https://sso.pjx.localhost`.** If it moves, both
`PJX_SSO__AUTHORITY` (`docker-compose.devcontainer.yml`) and
`REACT_APP_SSO_ISSUER_URL` (`projects/pjx-web-react/.env`) have to move with it —
and the `.env` is gitignored, so that change is easy to lose.

> Drop the `mvc` and `js` clients rather than porting them. They are
> IdentityServer4 sample scaffolding pointing at `localhost:5002` / `5003` and
> nothing in pjx uses them.

---

## Step 4 — Database

Duende's schema differs from IS4's, and ASP.NET Identity moves 3.1 → 8.0 as well.

```bash
dotnet ef migrations add DuendeUpgrade
dotnet ef database update
```

If the migration will not apply cleanly, drop and re-seed — `SeedData.cs` already
creates test users:

```bash
rm -f *.db      # confirm the filename in appsettings.json first
dotnet ef migrations add InitialDuende
dotnet ef database update
```

> **This destroys registered accounts.** Unlike Phase 4, which left the SSO
> database untouched, this phase does not. If you have test accounts worth
> keeping, export them first.

---

## Step 5 — Now add telemetry

The reason to do this here rather than in Phase 5: on `net8.0` the SSO service
can finally take current OpenTelemetry packages. Follow
[Phase 5 step 5b](phase-5-otel.md#step-5b--net-services-after-phase-4) with
`OTEL_SERVICE_NAME=pjx-sso-identityserver`.

This closes the last gap in the trace path — login and token issuance become
visible alongside everything else.

---

## Verify

```bash
# 1. No IdentityServer4 anywhere
grep -rn 'IdentityServer4' --include=*.csproj --include=*.cs projects/ || echo "clean"

# 2. Everything on net8.0 now
grep -rh '<TargetFramework>' --include=*.csproj projects/ | sort -u   # → net8.0 only

# 3. No 3.1 base images
grep -rn 'dotnet/aspnet:3.1\|dotnet/sdk:3.1' projects/ || echo "clean"

# 4. Build, test, run
validate.sh build pjx-sso-identityserver
validate.sh test  pjx-sso-identityserver
dev-up.sh -b -d && status.sh

# 5. Issuer unchanged — the single most important check
curl -s https://sso.pjx.localhost/.well-known/openid-configuration \
  | grep -o '"issuer":"[^"]*"'
# → "issuer":"https://sso.pjx.localhost"

# 6. Client registration survived
curl -s https://sso.pjx.localhost/.well-known/openid-configuration \
  | grep -o '"grant_types_supported":\[[^]]*\]'

# 7. SSO now reports telemetry
curl -s -u admin:admin \
  'https://grafana.pjx.localhost/api/datasources/proxy/uid/tempo/api/search/tag/service.name/values' \
  | grep pjx-sso
```

Then the full manual pass from [Phase 2](phase-2-traefik.md#verify) — register a
**new** account (the old ones are gone), activate, log in, `/country/all`,
`/cities`, profile, sign out.

Also re-run `validate.sh test pjx-api-dotnet`. The API was not modified, but its
token validation now runs against a different issuer implementation, and that is
exactly the kind of thing that passes a build and fails at runtime.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-8-duende
```

Accounts dropped in step 4 do not come back — re-run the old `SeedData` on the
reverted branch to regenerate test users.

---

## Final step — open the demo up

Only after everything above passes. This is what the whole deferral was for.

```bash
source local/scripts/azure/00-vars.sh

helm upgrade --install traefik traefik/traefik \
  --namespace traefik --reuse-values \
  --set service.spec.loadBalancerSourceRanges=null
```

Then re-verify from a network that was previously blocked:

```bash
curl -s -o /dev/null -w '%{http_code}\n' "https://${DEMO_HOST}/"
```

Before you do this, confirm all three are true:

- [ ] No `netcoreapp3.1` images anywhere (`grep -rn 'aspnet:3.1' projects/`)
- [ ] `dotnet list package --vulnerable --include-transitive` is clean for every project
- [ ] The signing certificate comes from Key Vault, and nothing in git history is in use ([Phase 10](phase-10-deployable.md) step 1)

Opening the demo while any of those is false undoes the reasoning that made the
deferral safe.

> With the allowlist gone, cert-manager could switch from DNS-01 back to
> HTTP-01 — but there is no reason to. DNS-01 works, needs no inbound
> reachability, and supports wildcards. Leave it.

## If you decide against Duende

The licence is the only real objection, and if it blocks you, **OpenIddict** is
the fallback. The migration is larger, and the specific cost is that client
registration moves from `Config.cs` into database seeding — see the table at the
top of this document. Everything else in this phase (framework bump, base
images, issuer preservation, telemetry) applies unchanged.
