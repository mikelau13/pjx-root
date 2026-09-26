# Phase 4 — .NET Core 3.1 → .NET 8 (API side)

**Goal:** move the seven `pjx-api-dotnet` projects to `net8.0` and drop their
IdentityServer4 dependency. The SSO server deliberately stays on
`netcoreapp3.1`.

**Risk:** Medium. No auth-stack replacement in this phase — that is
[Duende](phase-duende.md), optional and pre-production.

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

### The 3.1 SDK is not needed — confirmed in Phase 0

Phase 0 verified this rather than assuming it: the devcontainer installs only
`dotnet:2` at version `8.0` (8.0.423), and `dotnet restore` on
`pjx-sso-identityserver.sln` succeeded with warning **NETSDK1138**. So
`validate.sh` keeps **both** .NET projects in `DOTNET_PROJECTS`, and the
`DOCKER_ONLY_PROJECTS` fallback below is unnecessary — it has been removed from
[Phase 1](phase-1-script-layer.md).

### Original reasoning (retained)

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

## Step 1b — Stop the .NET containers first

**Do this before any `dotnet build`.** Both .NET containers run `dotnet watch` as
**root** and write into the same `obj/` and `bin/` trees you are about to build
locally. Changing the TFM creates fresh `obj/Debug/net8.0/` directories, and
whichever process gets there first owns them — usually the container, since
`dotnet watch` reacts to your edit within milliseconds.

The failure looks like a permissions bug in your own project:

```
error MSB3191: Unable to create directory "obj/Debug/net8.0/staticwebassets/".
Access to the path ... is denied.
```

```bash
docker stop pjx-api-dotnet-dev pjx-sso-identityserver-dev
```

```bash
# HOST — clean up anything the containers already claimed
sudo chown -R "$(id -un):$(id -gn)" <repo>/projects
```

Leave them stopped for the whole phase; `dotnet watch` cannot run cleanly against
half-migrated projects anyway. Start them again at Step 4 when the images are
rebuilt on the 8.0 base.

While they are stopped:

- `status.sh` shows two red rows — expected, not a regression.
- **Login does not work.** The SSO server is down, so `https://pjx.test` loads but
  cannot authenticate. React, Apollo and the Node API are unaffected.
- **Do not run `dev-up.sh -d`** — it starts all five and restarts the two you
  stopped, putting you back in the ownership race. Name the others instead:
  ```bash
  docker compose -f docker-compose.devcontainer.yml up -d \
    pjx-web-react pjx-graphql-apollo pjx-api-node
  ```

At Step 4 the base images change to 8.0, so bring them back with a rebuild rather
than a plain start: `dev-up.sh -b -d`.

> **If you would rather keep them running**, make them write as your uid instead.
> On both .NET services:
>
> ```yaml
>     user: "1000:1000"
>     environment:
>       # dotnet needs a writable HOME as non-root; /root is not writable.
>       - DOTNET_CLI_HOME=/tmp
>       - HOME=/tmp
> ```
>
> uid 1000 is your host user and the devcontainer's `vscode`, so output is owned
> consistently. Worth doing permanently — it is the same root-owned-artifacts
> problem as Phase 0 defect #8, just triggered by a different writer.

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
> source. Phase 2 moved the authority to `https://sso.pjx.test`, so this can
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
until Duende.

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
real data, and re-running it later is how you decide when Duende stops being
optional.

### No `validate.sh` change needed

Earlier drafts had a `DOCKER_ONLY_PROJECTS` list here, to exclude SSO from local
SDK builds in case the 8.0 SDK could not handle its `netcoreapp3.1` target.
**Phase 0 proved it can** — `dotnet restore` on `pjx-sso-identityserver.sln`
succeeds under SDK 8.0.423 with warning NETSDK1138.

So `validate.sh` keeps both .NET projects in `DOTNET_PROJECTS`, exactly as
[Phase 1](phase-1-script-layer.md) writes it. Nothing to change.

> One wrinkle when you get here: `dotnet restore` on the SSO project resolves
> `IdentityServer4.AspNetIdentity` 4.1.2, published for `netcoreapp3.1` — the same
> TFM the project already targets, so that is fine. Only when
> [Duende](phase-duende.md) moves SSO to `net8.0` does the package have to go.

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

> Run these in the devcontainer unless a command is marked HOST. See
> [Where to run commands](README.md#where-to-run-commands) — `localhost` means
> something different in each shell.

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
curl -s https://sso.pjx.test/.well-known/openid-configuration \
  | grep -o '"issuer":"[^"]*"'
# → "issuer":"https://sso.pjx.test"

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

## Outstanding — EF Core was not upgraded — ✅ **RESOLVED 2026-09-07**

> **Done.** All EF Core references are now `8.0.0`, verified against the running
> container's `Pjx_Api.deps.json` (no `3.1.7` anywhere in the resolved graph).
> Create, update, country list and city list all pass a browser test, and
> `AddDbContextCheck` is restored. See
> [How it actually went](#how-it-actually-went-2026-09-07) at the end of this
> section. Everything below is kept as the original record.


**Discovered during Phase 5 Step 5b, 2026-08-08.** [Step 2](#step-2--upgrade-bottom-up)
says to update every `Microsoft.EntityFrameworkCore.*` `PackageReference` to
`8.0.*`. The `TargetFramework` moved to `net8.0` on all 8 projects, but five EF
references are still on **3.1.7**:

| Project | Package | Version |
|---|---|---|
| `Pjx_Api` | `Microsoft.EntityFrameworkCore.Design` | 3.1.7 |
| `Pjx_Api` | `Microsoft.EntityFrameworkCore.Sqlite` | 3.1.7 |
| `Pjx_Api` | `Microsoft.EntityFrameworkCore.Tools` | 3.1.7 |
| `Pjx.CalendarLibrary` | `Microsoft.EntityFrameworkCore.Design` | 3.1.7 |
| `Pjx.CalendarLibrary` | `Microsoft.EntityFrameworkCore.Sqlite` | 3.1.7 |

```bash
grep -rn 'Microsoft.EntityFrameworkCore' projects/pjx-api-dotnet --include=*.csproj
```

It builds and runs — EF Core 3.1 targets `netstandard2.0`, so `net8.0` loads it
fine. Nothing is broken today.

### Why it is deferred rather than fixed

The risk is precisely the one in [Breaking changes](#breaking-changes-to-expect):
EF Core 8 translates LINQ more strictly, so queries that currently run
client-side start throwing at runtime rather than failing to compile. That is a
code-correctness change, and the working decision for this upgrade is
devcontainer-and-infrastructure first, application bugs later.

### Two consequences to know about

1. **No EF spans in Phase 5.** `OpenTelemetry.Instrumentation.EntityFrameworkCore`
   hooks EF Core's `DiagnosticSource` events and its supported floor is far above
   3.1, so database spans will likely be absent from traces. That is this version
   gap, not a misconfiguration — see
   [Phase 5 Step 5b](phase-5-otel.md#step-5b--net-services-after-phase-4).
2. **EF Core 3.1 is out of support** (December 2022), same as the pre-upgrade
   `netcoreapp3.1` runtime was. It is a genuine pre-production item, not
   housekeeping — carry it into the [Duende](phase-duende.md) revisit alongside
   the IdentityServer4 replacement.

Do this on its own branch, not folded into another phase — the whole point is that
a query regression should be attributable:

```bash
git checkout -b fix/ef-core-8
```

Then upgrade one project at a time, `dotnet build` and `dotnet test` after each,
and exercise `/country/all` and `/cities` in the browser. Those two endpoints are
the LINQ-heavy paths.

### It is now blocking, not merely stale

**2026-08-09.** Adding
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` **8.0.28** to
`Pjx_Api` for Phase 5 Step 5d took the whole API's database layer down. NuGet
resolved the graph to:

| Package | Version |
|---|---|
| `Microsoft.EntityFrameworkCore` | 8.0.28 ← pulled in by the health-check package |
| `Microsoft.EntityFrameworkCore.Relational` | 8.0.28 ← same |
| `Microsoft.EntityFrameworkCore.Sqlite` | 3.1.7 ← left behind |

The Sqlite provider is compiled against EF Core 3.1's abstract members and does
not implement EF Core 8's, so the first thing to build a query threw:

```
System.TypeLoadException: Method 'Create' in type
'Microsoft.EntityFrameworkCore.Sqlite.Query.Internal.SqliteQueryableMethodTranslatingExpressionVisitorFactory'
from assembly 'Microsoft.EntityFrameworkCore.Sqlite, Version=3.1.7.0' does not
have an implementation.
```

`AddDbContextCheck` calls `CanConnectAsync()`, which is merely the *first* thing to
touch EF — every database-backed endpoint was broken too, while `/health/live`
still answered `Healthy` because it runs no checks.

**Resolved by removing the package and the `AddDbContextCheck` call from
`Pjx_Api`**, so EF Core returns to a consistent 3.1.7. The .NET API's readiness
probe therefore does not verify the database. SSO keeps its check — it is
`netcoreapp3.1` with the matching 3.1.32 package, so its versions align.

The lesson generalises: **any package that depends on modern EF Core will drag
EF Core 8 into the graph and break the 3.1 Sqlite provider.** Version the
dependency against the EF Core version in use, not the project's
`TargetFramework`.

### Where this lands in later phases

| Phase | Impact | Now |
|---|---|---|
| [5](phase-5-otel.md) | No EF spans. The .NET API's readiness check omits the database. | ✅ `AddDbContextCheck` restored; EF spans should now appear |
| [6](phase-6-devcontainer-image.md) | The image pins `dotnet-ef` 8.x, which cannot operate on an EF Core 3.1 project — `dotnet ef migrations` fails. | ✅ resolved |
| [7b](phase-7b-local-k8s.md) | The .NET API's readiness probe passes without proving the database is reachable. | ⚠️ partly — see the SQLite caveat below |
| [Deployable](phase-deployable.md) | **Hard blocker.** `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.\* requires EF Core 8 — the same mismatch, but on the critical path. | ✅ unblocked |
| [Duende](phase-duende.md) | Already bumps SSO's EF Core to 8.0.x, so SSO is covered there. | unchanged — SSO stays on 3.1 |
| [AKS Deploy](phase-aks-deploy.md) | A pod can report Ready while its database is unreachable. Acceptable for a demo, not for production. | ⚠️ see below |

> **The readiness probe is still weak, for a different reason.**
> `AddDbContextCheck` calls `CanConnectAsync()`, and `Microsoft.Data.Sqlite` opens
> in `ReadWriteCreate` mode by default — so it can create an empty database file
> and report healthy. The check becomes meaningful once
> [Deployable Step 2](phase-deployable.md#step-2--sqlite--postgresql) moves to
> PostgreSQL, where a connection failure is a real failure. Until then
> `/health/ready` returning `Healthy` proves the process and the check pipeline
> work, not that the data is there.

**Recommendation was: do the upgrade as Step 0 of
[Deployable](phase-deployable.md).** That is what happened, though the PostgreSQL
switch was *not* folded in with it — Step 2 is deferred, so EF Core 8 was verified
against SQLite on its own. That turned out to be the better split: the one LINQ
regression was unambiguously attributable, which was the whole point of doing this
separately.

---

### How it actually went (2026-09-07)

Done on `feature/arch-deployable` rather than a dedicated `fix/ef-core-8`
branch, as the first item of Deployable's local work. Three failures, in order.

**1. `CS1061: 'IConfigurationSection' does not contain a definition for 'GetValue'`**

Four call sites in `Pjx.CalendarLibrary/Extensions/CalendarEventServiceExtensions.cs`.
`GetValue<T>()` is an extension method in `ConfigurationBinder`, which ships in
**`Microsoft.Extensions.Configuration.Binder`**. EF Core 3.1.7 pulled that package
in transitively; EF Core 8 trimmed its dependency graph and no longer does. The
code did not change — its supply chain did.

Fixed by adding the package explicitly to `Pjx.CalendarLibrary`. A solution-wide
grep for `GetValue<`, `.Bind(` and `.Get<` confirmed those four were the only
configuration-binder uses, so there was no second wave.

**2. `MSB3021: Access to the path … is denied`**

Root-owned `bin/`/`obj/` inside the bind mount — the .NET dev containers run as
root and write build output through the mount, and the devcontainer is `vscode`.
Not an EF Core issue at all. Fixed from the host with `chown -R`, not `rm`, per
the [working conventions](README.md#working-conventions). It recurs on every
`make up`; the durable fix is a `user:` mapping on the two .NET services.

**3. The actual LINQ regression — `ArgumentOutOfRangeException`**

The one this section predicted, and it surfaced exactly where predicted: the
conflict-check path.

```
System.InvalidOperationException: An exception was thrown while attempting to
  evaluate a LINQ query parameter expression.
 ---> System.ArgumentOutOfRangeException: The added or subtracted value results
      in an un-representable DateTime. (Parameter 'value')
    at System.DateTimeOffset.AddDays(Double days)
    at ...ParameterExtractingExpressionVisitor.GetValue(...)
    at Pjx_Api.Data.CalendarEventRepository.GetAllBetweenByUser(...)
    at Pjx.CalendarLibrary.ConflictChecks.ConflictCheck.DoCheck(...)
```

`ConflictCheck.cs:28` passes the full range to mean *"every event for this user"*:

```csharp
_repository.GetAllBetweenByUser(ce.UserId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
```

and `CalendarEventRepository.GetAllBetweenByUser` did `start.AddDays(-1)` inside
the `Where` predicate. `DateTimeOffset.MinValue.AddDays(-1)` is invalid — there is
no day before year 1.

**That arithmetic has been wrong since 2020.** EF Core 3.1 never executed it.
EF Core 8's `ParameterExtractingExpressionVisitor` eagerly evaluates any
sub-expression that does not reference the entity, in order to turn it into a SQL
parameter — `start.AddDays(-1)` is a captured local, so it gets computed, and
throws. The upgrade did not introduce the bug; it started running it.

Fixed in the repository rather than the caller, so both call sites are covered,
and with the arithmetic **hoisted out of the expression tree** so EF passes plain
parameters:

```csharp
DateTimeOffset startPrev = start > DateTimeOffset.MinValue.AddDays(1)
    ? start.AddDays(-1) : DateTimeOffset.MinValue;
DateTimeOffset endPrev = end > DateTimeOffset.MinValue.AddDays(1)
    ? end.AddDays(-1) : DateTimeOffset.MinValue;
```

`DateTimeOffset.Compare` turned out to translate fine under EF Core 8 — the
predicate needed no operator rewrite.

### Two findings worth carrying forward

**`dotnet watch` cannot see host edits through a bind mount.** After the fix was
written and built, the same stack trace reappeared byte-for-byte. The source was
current in both namespaces and `Pjx_Api.dll` was 18 seconds newer than the `.cs`
file — but the container had started 26 minutes earlier and the logs contained
**no watch rebuild or restart line at all**. inotify events do not cross a Docker
bind mount, and `:cached` makes it worse. The Node services already carry
`CHOKIDAR_USEPOLLING=true` for exactly this reason; the .NET services had no
equivalent. Add `DOTNET_USE_POLLING_FILE_WATCHER=1` to both, or restart the
container after every .NET edit. Until then `dotnet watch` here is decorative.

**Twelve green tests over a query that threw.** Every `GetAllBetweenByUser`
reference in `Pjx.Calendar_Test/ConflictChecks/OverlappingCheckTests.cs` is a Moq
`.Setup(...)`. The repository is mocked, so the LINQ expression is never built and
never evaluated. The tests passed throughout. Combined with `Pjx_Api_Test`
containing only a `.csproj`, the .NET API has **no test that executes a real
query** — which is why a browser pass, not a green suite, was the gate here.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-4-dotnet8
```

If step 6 dropped the calendar database, re-run the migrations on the reverted
branch. SSO accounts are unaffected.
