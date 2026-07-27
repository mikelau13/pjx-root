# Phase 4 — .NET Core 3.1 → .NET 8 (API side)

**Goal:** move the seven `pjx-api-dotnet` projects to `net8.0` and drop their
IdentityServer4 dependency. The SSO server deliberately stays on
`netcoreapp3.1`.

**Risk:** Medium. No auth-stack replacement in this phase — that is
[Phase 8](phase-8-duende.md), optional and pre-production.

**Reversible:** on its branch; keep it there until the manual browser test from
Phase 2 passes again.

**Depends on:** Decision **D1** (timing). **D2 is no longer a blocker** — see
[README.md](README.md).

```bash
git checkout -b feature/arch-phase-4-dotnet8
```

---

## Scope

Of the 8 `.csproj` files, **7 move and 1 stays**:

| Project | Target | Notes |
|---|---|---|
| `Pjx.CalendarEntity` | `net8.0` | Leaf — no project references |
| `Pjx.CalendarLibrary` | `net8.0` | → CalendarEntity |
| `Pjx_CreateCertificates` | `net8.0` | No project references |
| `Pjx_Api` | `net8.0` | → CalendarLibrary, CalendarEntity. **Drops IS4** |
| `Pjx.Calendar_Test` | `net8.0` | → CalendarEntity, CalendarLibrary |
| `Pjx_Api_Test` | `net8.0` | → CalendarLibrary, Pjx_Api |
| `Pjx_CreateCertificates_Test` | `net8.0` | → CalendarLibrary, Pjx_CreateCertificates |
| `IdentityServerAspNetIdentity` (SSO) | **stays `netcoreapp3.1`** | IS4 bump only — step 5 |

### Why the split is clean

`pjx-sso-identityserver` is a **standalone solution with zero project
references in or out**. Nothing in `pjx-api-dotnet` references it, and it
references nothing there. The two communicate only over HTTP, via the OIDC
discovery document. So there is no shared library dragged down to the lower
framework and no build-order coupling — the seam holds without special handling.

### The API's IS4 dependency is nearly nothing

`Pjx_Api` references `IdentityServer4.AspNetIdentity` 4.0.4, but:

- `Startup.cs:69-79` already uses **stock `AddJwtBearer`** against
  `PJX_SSO__AUTHORITY` — not IS4's authentication handler.
- The only IS4 reference in source is `using IdentityServer4.Extensions;` at
  `Controllers/Calendar/EventController.cs:6`, and nothing in that file calls
  the methods the namespace provides (`GetSubjectId()`, `IsAuthenticated()`, …).
  It looks like an unused leftover import.

So removing IS4 from the API is likely a two-line change. The compiler settles
it immediately — if `EventController.cs` compiles with the `using` deleted, you
are done. If it does not, replace the extension call with the standard
equivalent (`User.FindFirst("sub")?.Value` or
`ClaimTypes.NameIdentifier`).

---

## Breaking changes to expect

| Area | 3.1 | 8.0 | Notes |
|---|---|---|---|
| Startup | `Startup.cs` + `Program.cs` | Minimal hosting | `Startup.cs` still works via the compat shim; not required to change |
| EF Core | 3.1 | 8.0 | Stricter query translation — LINQ that silently ran client-side now throws |
| JSON | `Newtonsoft.Json` | `System.Text.Json` default | Differences around nulls, casing, `TimeSpan` |
| `NSwag.AspNetCore` 13.7 | OK | Needs ≥ 14.x | — |
| `Serilog` 2.9 / `Serilog.Sinks.Console` 3.1 | OK | Bump to current | — |
| Base images | `3.1-aspnet` / `3.1-sdk` | `8.0-aspnet` / `8.0-sdk` | `pjx-api-dotnet` only |

---

## Step 1 — Toolchain

In `.devcontainer/devcontainer.json`:

```jsonc
"ghcr.io/devcontainers/features/dotnet:1": { "version": "8.0" }
```

Add `global.json` at the repo root:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

Rebuild the container, then `dotnet --list-sdks` → `8.0.x`.

### You should not need the 3.1 SDK

The .NET 8 SDK can build `netcoreapp3.1` targets — it acquires the targeting
pack as a NuGet reference assembly. It emits warning **NETSDK1138** ("target
framework out of support"), which is accurate and worth leaving visible.

To *run* the SSO project locally you need the 3.1 runtime, and its
`Dockerfile` already provides that inside the container image. So build and run
SSO through Docker rather than the devcontainer SDK, and the devcontainer stays
on a single SDK.

Confirm this before relying on it:

```bash
cd projects/pjx-sso-identityserver && dotnet build
# Expect: success, with warning NETSDK1138
```

If it fails rather than warns, fall back to building SSO only via
`docker compose build pjx-sso-identityserver` and exclude it from local SDK
builds (step 5).

---

## Step 2 — Upgrade bottom-up

Follow the real dependency order so a failure has one cause:

1. `Pjx.CalendarEntity`
2. `Pjx.CalendarLibrary`
3. `Pjx_CreateCertificates`
4. `Pjx_Api`
5. `Pjx.Calendar_Test`, `Pjx_Api_Test`, `Pjx_CreateCertificates_Test`

For each:

```xml
<TargetFramework>net8.0</TargetFramework>
```

then update every `Microsoft.*` and `Microsoft.EntityFrameworkCore.*`
`PackageReference` to `8.0.*`.

`dotnet build` after each one. Do not batch them.

Microsoft's assistant handles most of the mechanical work if you prefer:

```bash
dotnet tool install -g upgrade-assistant
cd projects/pjx-api-dotnet/src/Pjx.CalendarEntity
upgrade-assistant upgrade Pjx.CalendarEntity.csproj
```

For projects this small, hand-editing is often faster.

---

## Step 3 — Drop IS4 from the API

In `projects/pjx-api-dotnet/src/Pjx_Api/pjx-api-dotnet.csproj`, remove:

```xml
<PackageReference Include="IdentityServer4.AspNetIdentity" Version="4.0.4" />
```

In `Controllers/Calendar/EventController.cs`, remove line 6:

```csharp
using IdentityServer4.Extensions;
```

Then `dotnet build`. Bump `Microsoft.AspNetCore.Authentication.JwtBearer` from
3.1.18 to 8.0.x while you are here.

Leave `Startup.cs:69-79` alone otherwise — the JWT bearer configuration already
points at the discovery document and keeps working against IS4 unchanged. Token
validation is a protocol boundary, not a library one; a `net8.0` API validating
tokens from a `netcoreapp3.1` issuer is a normal arrangement.

> `options.RequireHttpsMetadata = false` at `Startup.cs:72` is marked TODO in the
> source. Phase 2 moved the authority to `https://sso.pjx.localhost`, so this can
> now be `true` — provided the container trusts the mkcert CA (Phase 2 step 5c).
> If token validation starts failing after flipping it, that is the CA trust
> issue, not this phase.

---

## Step 4 — Container base images

In `projects/pjx-api-dotnet/Dockerfile` **and** `Dockerfile.dev`:

```dockerfile
# before
FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS base
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build

# after
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
```

**Do not change `projects/pjx-sso-identityserver/Dockerfile*`.** It stays on 3.1
until Phase 8.

---

## Step 5 — SSO: bump IdentityServer4, stay on 3.1

Two changes only.

In `IdentityServerAspNetIdentity.csproj`, move IdentityServer4 to its final
release:

```xml
<PackageReference Include="IdentityServer4.AspNetIdentity" Version="4.1.2" />
```

4.1.2 is the last version ever published and carries fixes over 4.0.0. It is
free to take and there is no later one.

Then record the actual risk rather than assuming it:

```bash
cd projects/pjx-sso-identityserver
dotnet restore
dotnet list package --vulnerable --include-transitive
```

Paste that output into the commit message. It documents the deferral against
real data, and re-running it later is how you decide when Phase 8 stops being
optional.

### Exclude SSO from local SDK builds

If step 1's `dotnet build` check failed, exclude SSO from `validate.sh` so a
missing 3.1 SDK does not fail the whole sweep. In
`local/scripts/validate.sh`, change the project lists:

```bash
NODE_PROJECTS=(pjx-web-react pjx-api-node pjx-graphql-apollo)
DOTNET_PROJECTS=(pjx-api-dotnet)
# pjx-sso-identityserver is netcoreapp3.1 and builds only inside its container
# image (see Phase 8). Build it with:
#   docker compose -f docker-compose.devcontainer.yml build pjx-sso-identityserver
DOCKER_ONLY_PROJECTS=(pjx-sso-identityserver)
```

And handle the case explicitly rather than silently skipping — the silent-skip
pattern is what hid the Phase 0 bug:

```bash
is_docker_only() {
    local p
    for p in "${DOCKER_ONLY_PROJECTS[@]}"; do [[ "$1" == "${p}" ]] && return 0; done
    return 1
}

# inside the target loop, before the dotnet/node branch:
if is_docker_only "${target}"; then
    echo "   (${target} is netcoreapp3.1 — build via Docker, skipping local SDK)"
    continue
fi
```

---

## Step 6 — Database

`Pjx_Api` uses `Microsoft.EntityFrameworkCore.Sqlite`. EF Core 3.1 → 8.0 may
need a migration refresh:

```bash
cd projects/pjx-api-dotnet/src/Pjx_Api
dotnet ef migrations list
dotnet ef database update
```

If the existing migrations fail to apply, regenerate them — for a demo app with
seeded data that is simpler than writing a migration path:

```bash
rm -f *.db          # confirm the filename in appsettings.json first
dotnet ef migrations add Net8Upgrade
dotnet ef database update
```

> This destroys local calendar data. The SSO database is untouched — it is a
> separate project on an unchanged framework, so **user accounts survive this
> phase.** That is a side benefit of the deferral worth knowing.

---

## Verify

```bash
# 1. Seven projects on net8.0, one deliberately on netcoreapp3.1
grep -rh '<TargetFramework>' --include=*.csproj projects/ | sort | uniq -c
# → 7 net8.0, 1 netcoreapp3.1

# 2. IS4 remains ONLY in the SSO project, at 4.1.2
grep -rn 'IdentityServer4' --include=*.csproj --include=*.cs projects/
# → one hit: IdentityServerAspNetIdentity.csproj, Version="4.1.2"

# 3. Build and test the API side
validate.sh build pjx-api-dotnet
validate.sh test  pjx-api-dotnet

# 4. SSO still builds (via Docker if the SDK route failed)
docker compose -f docker-compose.devcontainer.yml build pjx-sso-identityserver

# 5. Full stack up
dev-up.sh -b -d && status.sh

# 6. Discovery document unchanged — the issuer must not have moved
curl -s https://sso.pjx.localhost/.well-known/openid-configuration \
  | grep -o '"issuer":"[^"]*"'
# → "issuer":"https://sso.pjx.localhost"

# 7. A net8.0 API validating tokens from a netcoreapp3.1 issuer
#    — this is the cross-framework boundary, and check 6 alone does not prove it
```

**Then the manual browser pass from
[Phase 2](phase-2-traefik.md#verify)**: register, activate, log in,
`/country/all`, `/cities`, profile, sign out.

`/country/all` is the check that matters most here — it is the only one that
exercises a `net8.0` API validating a token minted by the untouched 3.1 SSO
server. A green build proves nothing about that boundary.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-4-dotnet8
```

If step 6 dropped the calendar database, re-run the migrations on the reverted
branch. SSO accounts are unaffected.
