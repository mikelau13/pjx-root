# Phase 7c — CI/CD to GHCR

**Goal:** tag-driven image builds to GHCR and Helm charts published to GHCR OCI,
matching the release pattern documented in `CDE:CLAUDE.md`.

**Risk:** Medium — greenfield CI, so nothing to break, but it publishes artifacts.

**Reversible:** yes.

**Depends on:** [Phase 7](phase-7-cicd.md) (charts parameterised) and
[Phase 7b](phase-7b-local-k8s.md) (charts proven to actually deploy).

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

## Step 3 — GitHub Actions

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

Create `.github/workflows/build.yml`:

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

## Step 4 — Chart packaging and metadata

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
