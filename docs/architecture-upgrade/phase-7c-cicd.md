# Phase 7c — CI/CD to GHCR

**Goal:** tag-driven image builds to GHCR and Helm charts published to GHCR OCI,
matching the release pattern documented in `CDE:CLAUDE.md`.

**Risk:** Medium — greenfield CI, so nothing to break, but it publishes artifacts.

**Reversible:** yes.

**Depends on:** [Phase 7](phase-7-cicd.md) (charts parameterised) and
[Phase 7b](phase-7b-local-k8s.md) (charts proven to actually deploy).

**Read first:** [CI/CD, registries, and why a chart gets published](../reference/ci-cd-and-registries.md)
and, for Step 0, [docker build, and where images actually live](../reference/docker-build-and-images.md)
— the concepts behind this phase: registries and OCI, the source-versus-artifact
distinction, what each `on:` trigger fires, image tag semantics, and why
Dependabot is configured here even though it is not part of any workflow.

```bash
git checkout -b feature/arch-phase-7c-cicd
```

---

## Why this comes after the local deploy

Split out of the original Phase 7 deliberately. CI automates *publishing*
artifacts — so publishing must be worth doing, which means the chart has to work
first. [Phase 7b](phase-7b-local-k8s.md) proves it does, on a real cluster, for
free. Writing the pipeline first would just automate shipping something untested.

pjx has no CI at all today — no `.github/`, no `.gitlab-ci.yml`. There is one
stray `Jenkinsfile` in `projects/pjx-api-node`, unreferenced by anything; delete
it once Actions works.

---

## Step 0 — Fix the production Dockerfiles first

**CI builds `Dockerfile`, not `Dockerfile.dev`.** Everything proven in Phase 7b
used the dev images; the production ones have not been built since before the
Phase 4 .NET 8 migration, and two of them cannot build at all.

| Service | Production base | State |
|---|---|---|
| `pjx-api-dotnet` | `dotnet/core/aspnet:8.0`, `dotnet/core/sdk:8.0` | 🔴 **do not exist** |
| `pjx-graphql-apollo` | `node:10-slim` | 🔴 EOL April 2021 |
| `pjx-web-react` | `node:14.5.0-slim` → `nginx:1.19.0` | 🔴 **build fails** — npm 6 cannot read the v3 lock file |
| `pjx-sso-identityserver` | `dotnet/core/aspnet:3.1-buster-slim` | 🟠 EOL runtime, valid path — the documented [Phase 8](phase-8-duende.md) deferral |
| `pjx-api-node` | `node:18-slim` | ✅ |

**`mcr.microsoft.com/dotnet/core/*` stopped at 3.1.** .NET 5 renamed the
repository, dropping `core/`. Phase 4 updated `Dockerfile.dev` and left
`Dockerfile` pointing at a tag that has never existed:

```bash
docker manifest inspect mcr.microsoft.com/dotnet/core/aspnet:8.0   # fails
docker manifest inspect mcr.microsoft.com/dotnet/aspnet:8.0        # pulls
```

In `projects/pjx-api-dotnet/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
```

In `projects/pjx-graphql-apollo/Dockerfile`, move off Node 10 to match what
`Dockerfile.dev` already runs successfully:

```dockerfile
FROM node:18-slim
```

In `projects/pjx-web-react/Dockerfile`, replace **Stage 1** only:

```dockerfile
#Stage 1
FROM node:18-slim AS builder
WORKDIR /app
COPY package*.json .npmrc ./
RUN npm ci
COPY . .
RUN NODE_OPTIONS=--openssl-legacy-provider npm run build
```

This one is not optional, and the reason is not obvious. `node:14.5.0-slim`
ships **npm 6.14.5**, which only understands `lockfileVersion: 1`. Handed this
project's v3 `package-lock.json` it does not warn — it ignores the lock and
resolves every range fresh, pulling an `@types/babel__traverse` that uses
TypeScript 4.1 key-remapping syntax against this project's pinned `typescript
^3.7.5`. The build dies with `TS1005 ']' expected` inside `node_modules`.
`node:18-slim` (npm 10) reads the lock and installs the pinned 7.0.13; `npm ci`
makes any future drift fail loudly instead of silently. The `NODE_OPTIONS` flag
is the OpenSSL 3 workaround `react-scripts` 3.4.3 needs on Node 17+ — the
`start` script already carried it, `build` did not.

Full walkthrough with diagrams:
[docker-build-and-images.md](../reference/docker-build-and-images.md#case-study-when-the-two-dockerfiles-drift).

This gets the image building; it does **not** retire the deferral.
`react-scripts` 3.4.3 and `typescript` 3.7.5 stay pinned and now compile on
Node 18 via a compatibility flag — still owed to
[Phase 10 Step 3](phase-10-deployable.md#step-3--react-runtime-configuration).

Leave `pjx-sso-identityserver` alone. Its 3.1 base belongs to Phase 8 and is the
reason for the Dependabot suppression below.

### Build all five locally before writing any YAML

CI failures are slow to diagnose. Prove the images build first:

```bash
for s in pjx-web-react pjx-graphql-apollo pjx-api-node pjx-api-dotnet pjx-sso-identityserver; do
  echo "==> $s"
  docker build -t "pjx-prod-$s:test" "projects/$s" || echo "FAILED: $s"
done
```

`||` fires only on a non-zero exit, so the loop reports all five verdicts
instead of stopping at the first failure. Expected result once the three fixes
above are in — note how far the production images fall below the dev ones:

| Image | Prod | Dev |
|---|---:|---:|
| `pjx-prod-pjx-web-react:test` | 206 MB | 915 MB |
| `pjx-prod-pjx-graphql-apollo:test` | 779 MB | 795 MB |
| `pjx-prod-pjx-api-node:test` | 1.38 GB | 717 MB |
| `pjx-prod-pjx-api-dotnet:test` | 358 MB | 2.22 GB |
| `pjx-prod-pjx-sso-identityserver:test` | 357 MB | 1.62 GB |

`pjx-api-node` is *larger* in production than in development. Both Node
services are single-stage, use `npm install` rather than `npm ci`, and `COPY . .`
with no `.dockerignore` — and `pjx-api-node` additionally `apt-get install`s
`python3 make build-essential` for native modules and never discards it. A
builder stage would drop that toolchain from the shipped image, the way
`pjx-api-dotnet` drops the SDK. Not blocking Phase 7c; it is the next easy win.

> ### The chart is wired for dev images
>
> The Helm chart's React Service targets **3000** — the CRA dev server. The
> production image is nginx on **80**. Deploy CI-built images with today's chart
> and React returns 502, the same failure Phase 7b hit from the opposite
> direction.
>
> Make the port a value rather than hardcoding either one:
>
> ```yaml
> # values.yaml
> web:
>   service:
>     port: 80          # production nginx
> ```
> ```yaml
> # environments/local.yaml — dev images
> web:
>   service:
>     port: 3000
> ```
>
> `stdin`/`tty` can go too once React is nginx — those exist only because
> `react-scripts start` exits when stdin closes.
>
> This is **not** a reason to do Phase 10 first. Phase 10's React step is about
> `REACT_APP_*` runtime configuration, a different problem in the same service.
> Nothing here needs PostgreSQL, Key Vault, or the EF Core upgrade.

---

## Step 1 — GitHub Actions

CloudDevEnvironment's release pattern (`CDE:CLAUDE.md`):

| Tag | Environment |
|---|---|
| `vX.Y.Z` | Production |
| `vX.Y.Z-rc-N` | Staging |
| `vX.Y.Z-ut-N` | UAT |
| branch push | Dev (`dev+branchname-hash`) |
| PR | Integration testing only |

That is four environments with GHCR→ACR promotion. **For pjx, two is enough** —
`dev` on branch pushes and `prod` on version tags. Adding UAT and staging for a
demo project is ceremony without a consumer.

Create `.github/workflows/build.yml`. For a block-by-block reading of this file
— jobs versus steps, `uses` versus `run`, what the matrix does, and where the
registry token comes from — see
[What `.github/workflows/build.yml` actually is](../reference/github-actions-workflow.md).


```yaml
name: build

on:
  push:
    branches: [master]
    tags: ['v*']
  pull_request:
    branches: [master]

env:
  REGISTRY: ghcr.io

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '18' }
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: ./local/scripts/validate.sh build
      - run: ./local/scripts/validate.sh test

  build:
    needs: test
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    strategy:
      matrix:
        service:
          - pjx-web-react
          - pjx-graphql-apollo
          - pjx-api-node
          - pjx-api-dotnet
          - pjx-sso-identityserver
    steps:
      - uses: actions/checkout@v4

      - uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ github.repository_owner }}/${{ matrix.service }}
          tags: |
            type=semver,pattern=v{{version}}
            type=ref,event=branch,prefix=dev-
            type=sha,format=short

      - uses: docker/build-push-action@v6
        with:
          context: ./projects/${{ matrix.service }}
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

The `test` job reuses `validate.sh` from Phase 1 rather than duplicating build
commands in YAML — one definition of "does this build", used locally and in CI.

> **`validate.sh` needs Docker on the runner.** It sources `lib/common.sh`, which
> runs `docker compose config --services` at load time and `exit 1`s if that
> returns nothing:
>
> ```bash
> mapfile -t APP_SERVICES < <(
>     docker compose -f "${COMPOSE_FILE}" config --services | grep -v '^workspace$' | sort
> )
> ```
>
> GitHub-hosted runners have Docker, so this works — but it couples a job that
> only compiles source to the compose file parsing, and a failure there reports as
> "no services found" rather than anything about the build. If that proves
> annoying, split the service list out of `common.sh` rather than duplicating the
> build commands in YAML.

`docker/metadata-action` handles the tag→environment mapping declaratively, which
is simpler than the shell-based version derivation CloudDevEnvironment uses.

### The SSO service in CI

`pjx-sso-identityserver` is on `netcoreapp3.1` (Decision D2) while
`setup-dotnet` above installs 8.0.x. That is fine for the **build** job — it
builds inside its own `Dockerfile`, which carries the 3.1 SDK and runtime, so the
matrix entry needs no special case.

The **test** job is the one to watch: `validate.sh` either builds SSO under the
8.0 SDK (warning NETSDK1138) or skips it as `DOCKER_ONLY`, depending on what
Phase 4 step 5 established. Do not add a second `setup-dotnet` step with
`3.1.x` — that version is no longer reliably available on GitHub-hosted runners,
and the container build already covers it.

**Expect a permanent scanning finding.** Once images are in GHCR, Dependabot and
GHCR's own scanner will flag `pjx-sso-identityserver` for its
`mcr.microsoft.com/dotnet/aspnet:3.1` base — unpatched runtime on Debian 10. That
is the known, accepted cost of the deferral, and it is
[Phase 8](phase-8-duende.md)'s trigger. Suppress the alert deliberately with a
dated note rather than leaving it to look unnoticed:

```yaml
# .github/dependabot.yml — documented, time-boxed suppression
version: 2
updates:
  - package-ecosystem: docker
    directory: /projects/pjx-sso-identityserver
    schedule: { interval: monthly }
    # netcoreapp3.1 base is a known deferral tracked in
    # docs/architecture-upgrade/phase-8-duende.md. Revisit before any
    # public deployment.
    open-pull-requests-limit: 0
```

> **`pjx-api-dotnet` has two Dockerfiles with different build contexts.**
> `projects/pjx-api-dotnet/Dockerfile.dev` builds from the project root (what
> `docker-compose.devcontainer.yml` uses), while
> `projects/pjx-api-dotnet/src/Pjx_Api/Dockerfile` builds from that subdirectory.
> The matrix above assumes `context: ./projects/<service>`, so it picks the
> project-root one — verify that is the image you want to ship, and set
> `dockerfile:` explicitly rather than relying on the default:
>
> ```yaml
>         with:
>           context: ./projects/${{ matrix.service }}
>           file: ./projects/${{ matrix.service }}/Dockerfile
> ```
>
> (The now-deleted root `docker-compose.yml` used the `src/Pjx_Api/` context,
> which is why the two ever diverged.)

---

## Step 2 — Chart packaging and metadata

Fix `Chart.yaml`:

```yaml
apiVersion: v2
name: pjx
description: The pjx demo application — React SPA, GraphQL gateway, Node and .NET APIs, and an OIDC identity server
type: application
version: 0.2.0
appVersion: "0.2.0"
home: https://github.com/mikelau13/pjx-root
sources:
  - https://github.com/mikelau13/pjx-root
```

Keeping `version` and `appVersion` aligned makes the `_helpers.tpl` fallback in
step 1 predictable.

Add `.github/workflows/chart.yml` to publish to GHCR's OCI registry on tags:

```yaml
name: chart

on:
  push:
    tags: ['v*']

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions: { contents: read, packages: write }
    steps:
      - uses: actions/checkout@v4
      - uses: azure/setup-helm@v4
      - run: helm lint helm-pjx/
      - run: |
          VERSION="${GITHUB_REF_NAME#v}"
          helm package helm-pjx/ --version "$VERSION" --app-version "$VERSION"
          echo "${{ secrets.GITHUB_TOKEN }}" | \
            helm registry login ghcr.io -u ${{ github.actor }} --password-stdin
          helm push "pjx-${VERSION}.tgz" oci://ghcr.io/${{ github.repository_owner }}/charts
```

---


## Follow-up work, deliberately not in this plan

Recorded here so it is not lost:

- **[Phase 8](phase-8-duende.md) — SSO to Duende IdentityServer.** Not "not in
  this plan", but explicitly outside the mandatory path. Gate: before any
  production deployment
- **`Pjx.Calendar_Test` mocks a method the code no longer calls.** All 12 tests in
  `OverlappingCheckTests` fail with `NullReferenceException` at
  `OverlappingCheck.cs:15` (`events.Count`).

  ```csharp
  // ConflictCheck.cs:28 — what the code calls
  _repository.GetAllBetweenByUser(ce.UserId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

  // OverlappingCheckTests.cs:22 — what the test stubs
  .Setup(x => x.GetAll())
  ```

  The stub never matches, so Moq returns `null` for the unstubbed method and
  `events.Count` throws. **Pre-existing, not a .NET 8 regression** — the same
  mismatch fails identically on `netcoreapp3.1`; these tests had evidently not
  been run in years. Fix is a one-line change per test:

  ```csharp
  .Setup(x => x.GetAllBetweenByUser(It.IsAny<string>(),
                                    It.IsAny<DateTimeOffset>(),
                                    It.IsAny<DateTimeOffset>()))
  ```

  Ruled out during Phase 4: the test SDK (bumped to 17.11 / MSTest 3.6, which
  *was* required for net8.0 discovery) and the mocking libraries (Autofac 8.4 /
  Extras.Moq 7.0). Neither changed the outcome.

- **`Pjx_Api_Test` contains no tests at all** — no `[TestClass]` or
  `[TestMethod]`. "No test is available" is accurate, not a discovery failure.
  Either write tests for the API or delete the project.

- **`authService.getUser()` calls `signinRedirectCallback()` on any page.**
  Found during Phase 2 verification: `/country/all` throws
  `Unhandled Rejection (Error): No state in response`.

  ```js
  // projects/pjx-web-react/src/services/authService.tsx:46-52
  const user = await this.UserManager.getUser();
  if (!user) {
      return await this.UserManager.signinRedirectCallback();   // ← wrong here
  }
  ```

  `signinRedirectCallback()` parses the *current URL* for an OIDC response, so it
  is only valid on the callback route. On any other page there is no `state`
  parameter and it throws. The correct behaviour for "no cached user" is
  `signinRedirect()` (start a login) or a redirect to the login page.

  **Pre-existing, not a Phase 2 regression** — the same call fails identically on
  `localhost:3000`. Phase 2 verified the underlying auth path independently: a
  `client_credentials` token carrying `iss: https://sso.pjx.test` is accepted by
  the .NET API with a 200, so issuer matching and JWKS retrieval are sound. Only
  the SPA's guard logic is wrong.

- **Frontend dependency debt in `pjx-web-react`** — three related pieces, sensibly
  done together as one project:
  - **Apollo Client 2 → `@apollo/client` v3.** `apollo-boost`, `apollo-client`,
    `apollo-cache-inmemory`, `apollo-link-http` and `@apollo/react-hooks` all
    collapse into one package. Today `@apollo/react-hooks@3.1.5` declares peer
    `graphql@^14.3.1` against the project's `graphql@^15.3.0`, which is why
    `projects/pjx-web-react/.npmrc` sets `legacy-peer-deps=true` (added in
    Phase 0). That file is a workaround, not a fix.
  - `oidc-client` 1.10.1 → `oidc-client-ts` (deprecated dependency)
  - `react-scripts` 3.4.3 → Vite or a current CRA (blocks Node 20, noted in
    [Phase 6](phase-6-devcontainer-image.md))
- `README.md:104` claims `projects/` is gitignored; it is tracked (Decision D3)
- `projects/pjx-api-node/Jenkinsfile` — delete once Actions is working
- `pjx-dummy` templates — determine whether this is still needed or leftover
  scaffolding
- `projects/pjx-test-automation` — not integrated into `validate.sh` or CI

---

## Verify

> Run these in the devcontainer unless a command is marked HOST. See
> [Where to run commands](README.md#where-to-run-commands).

```bash
# 1. Workflows are valid YAML
python3 -c "import yaml,sys; [yaml.safe_load(open(f)) for f in sys.argv[1:]]" \
  .github/workflows/build.yml .github/workflows/chart.yml && echo "workflows parse"

# 2. The chart still lints and renders after the packaging changes
helm lint helm-pjx/
helm template pjx-release helm-pjx/ -f helm-pjx/environments/local.yaml > /dev/null && echo "renders"
```

Then push the branch and confirm the **`test` job passes on the PR** before
merging. PR builds do not push images, so it is a safe first run.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-7c-cicd
```

Delete any images accidentally published to GHCR from the package settings page.
