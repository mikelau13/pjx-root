# Phase 7 — CI/CD and Helm chart cleanup

**Goal:** tag-driven image builds to GHCR, a Helm chart whose image tags come
from `values.yaml`, and per-environment values files — matching the release
pattern documented in `CDE:CLAUDE.md`.

**Risk:** Medium — greenfield CI, so nothing to break, but the chart edits change
how deployment works.

**Reversible:** yes.

**Depends on:** Phase 6 (toolchain to validate charts locally).

```bash
git checkout -b feature/arch-phase-7-cicd
```

---

## Current state

pjx-root has **no CI** — no `.github/`, no `.gitlab-ci.yml`. There is a single
`Jenkinsfile` in `projects/pjx-api-node`, unreferenced by anything.

The charts have four concrete problems:

**1. Image tags are hardcoded in templates.** Every deployment pins a literal
tag, so shipping a new version means editing a template:

| Template | Image |
|---|---|
| `pjx-web-react.yaml:19` | `mikelauawaremd/pjx-web-react:v0.0.6` |
| `pjx-graphql-apollo.yaml:19` | `mikelauawaremd/pjx-graphql-apollo:v0.0.3` |
| `pjx-api-node.yaml:19` | `mikelauawaremd/pjx-api-node:v0.0.2` |
| `pjx-api-dotnet.yaml:19` | `mikelauawaremd/pjx-api-dotnet:v0.0.1` |
| `pjx-sso-identityserver.yaml:19` | `mikelauawaremd/pjx-sso-identityserver:v0.0.1` |
| `pjx-dummy.yaml:17` | `mikelauawaremd/pjx-dummy:v0.0.2` |

`values.yaml` carries only `appName` and `replicas` — no image, repository, or
tag values at all.

**2. Everything is `NodePort`** (30100, 30400, 30601, 30881, 30501, 31001)
*and* there is an Ingress (`pjx-ingress.yaml`). Two routing mechanisms doing the
same job. The Ingress also references service ports 80/82/83, which do not match
the ports the services actually declare.

**3. `kubernetes/` and `helm-pjx/templates/` are the same 10 files** —
byte-for-byte the same file set, one templated and one not. Two copies to keep in
sync, and they will drift.

**4. Chart metadata is untouched scaffolding.** `version: 0.1.0`,
`appVersion: "1.16.0"`, `description: A Helm chart for Kubernetes`.

**5. `pjx-secret.yaml` ships committed credentials.** It carries
`sso-password: cGFzc3dvcmQNCg==` and `certificate-password: cGFzc3dvcmQNCg==` —
base64 for `password\r\n`. Base64 is encoding, not encryption, and this file is
in a public repository.

**Delete `helm-pjx/templates/pjx-secret.yaml` in this phase.** Nothing replaces
it here — [Phase 10](phase-10-deployable.md) sources these from Key Vault via the
Secrets Store CSI driver. Removing it now means no deployment path can
accidentally depend on it.

```bash
git rm helm-pjx/templates/pjx-secret.yaml
grep -rn 'pjx-secret' helm-pjx/    # → no remaining references
```

> Deleting the file does not remove it from git history, and the repo is public.
> Treat both passwords as compromised — they are `password` anyway. Phase 10
> generates fresh material rather than reusing anything from the repo.

---

## Step 1 — Parameterise images

In `values.yaml`:

```yaml
global:
  imageRegistry: ghcr.io/mikelau13
  imagePullPolicy: IfNotPresent

web:
  appName: pjx-react
  replicas: 2
  image:
    repository: pjx-web-react
    tag: ""          # defaults to .Chart.AppVersion

apollo:
  appName: pjx-apollo
  replicas: 1
  image:
    repository: pjx-graphql-apollo
    tag: ""

nodeApi:
  appName: pjx-node
  replicas: 1
  image:
    repository: pjx-api-node
    tag: ""

dotnetApi:
  appName: pjx-dotnet
  replicas: 1
  image:
    repository: pjx-api-dotnet
    tag: ""

sso:
  appName: pjx-sso
  replicas: 1
  image:
    repository: pjx-sso-identityserver
    tag: ""

ingress:
  enabled: true
  className: traefik
  host: pjx.local
```

> The existing keys are `dotnet_api`, `node_api`, `react`. Renaming to camelCase
> is optional — if you do it, update every template reference in the same commit.

Add `helm-pjx/templates/_helpers.tpl`:

```
{{/*
Fully-qualified image reference. Falls back to .Chart.AppVersion when no
explicit tag is set, so a chart release and its images move together.
*/}}
{{- define "pjx.image" -}}
{{- $registry := .root.Values.global.imageRegistry -}}
{{- $tag := .svc.image.tag | default .root.Chart.AppVersion -}}
{{- printf "%s/%s:%s" $registry .svc.image.repository $tag -}}
{{- end -}}
```

Then in each deployment template:

```yaml
        image: {{ include "pjx.image" (dict "root" $ "svc" .Values.web) }}
        imagePullPolicy: {{ .Values.global.imagePullPolicy }}
```

Note the registry moves from Docker Hub (`mikelauawaremd/`) to GHCR
(`ghcr.io/mikelau13/`), matching where the CI in step 3 pushes.

> `global.imageRegistry` is deliberately a value, not a constant. Decision D5
> chose ACR for the AKS cluster, so [Phase 11](phase-11-deploy.md) overrides it
> with `--set global.imageRegistry=<acr>.azurecr.io` while local and CI keep
> GHCR. `az acr import` copies images registry-to-registry, so the artifact
> deployed is the artifact tested — no rebuild between the two.

---

## Step 2 — Pick one routing mechanism

Keep the Ingress, drop `NodePort`. NodePort was presumably a way to reach
services before an ingress controller existed; with one in place it is just a
second, conflicting path.

In each service template:

```yaml
# before
spec:
  type: NodePort
  ports:
    - protocol: TCP
      port: 80
      targetPort: 80
      nodePort: 30100

# after
spec:
  type: ClusterIP
  ports:
    - protocol: TCP
      port: 80
      targetPort: 80
      name: http
```

Then fix `pjx-ingress.yaml`. It currently routes three hosts
(`api.pjx.com`, `ql.pjx.com`, `sso.pjx.com`) to ports 80/82/83 — and **82/83 do
not exist** on those services. It also has no route to the React app, which is
the actual entry point.

Rewrite as path-based routing on one host, mirroring the Phase 2 hostname layout:

```yaml
{{- if .Values.ingress.enabled }}
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: pjx-ingress
  annotations:
    kubernetes.io/ingress.class: {{ .Values.ingress.className }}
spec:
  rules:
    - host: {{ .Values.ingress.host }}
      http:
        paths:
          - path: /graphql
            pathType: Prefix
            backend: { service: { name: pjx-apollo-service,  port: { number: 80 } } }
          - path: /api
            pathType: Prefix
            backend: { service: { name: pjx-dotnet-service,  port: { number: 80 } } }
          - path: /auth
            pathType: Prefix
            backend: { service: { name: pjx-sso-service,     port: { number: 80 } } }
          - path: /
            pathType: Prefix
            backend: { service: { name: pjx-react-service,   port: { number: 80 } } }
{{- end }}
```

> Ordering matters: the catch-all `/` must come last, or it swallows the others.
> Service names must match the `metadata.name` in each service template — check
> each one, since the current names are inconsistent with the `appName` values.

### Resolve the `kubernetes/` duplication

`kubernetes/` and `helm-pjx/templates/` are the same file set. Options:

- **Delete `kubernetes/`** and note in `kubernetes/README.md`'s place that Helm
  is the deployment path. Cleanest.
- **Keep it as generated output**: `helm template pjx-release helm-pjx/ >
  kubernetes/rendered.yaml`, and mark the directory generated.

**Recommendation: delete it.** A hand-maintained duplicate of templated
manifests will drift, and the templated copy is the one with the parameterisation
work in it.

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

> `pjx-api-dotnet`'s `Dockerfile` expects a different build context than the
> project root — `docker-compose.yml:20` uses
> `./projects/pjx-api-dotnet/src/Pjx_Api/` while
> `docker-compose.devcontainer.yml:34` uses `./projects/pjx-api-dotnet`. Confirm
> which is correct and set `context:` accordingly, or the matrix entry fails.

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

## Step 5 — Per-environment values

```bash
mkdir -p helm-pjx/environments
```

`helm-pjx/environments/dev.yaml`:

```yaml
global:
  imagePullPolicy: Always
ingress:
  host: pjx.local
web:
  replicas: 1
```

`helm-pjx/environments/prod.yaml`:

```yaml
global:
  imagePullPolicy: IfNotPresent
ingress:
  host: pjx.example.com
web:
  replicas: 2
```

```bash
helm upgrade --install pjx-release helm-pjx/ -f helm-pjx/environments/dev.yaml
```

---

## Verify

```bash
# 1. Chart lints and renders
helm lint helm-pjx/
helm template pjx-release helm-pjx/ -f helm-pjx/environments/dev.yaml > /tmp/rendered.yaml

# 2. No hardcoded tags survive
grep -n 'image:' /tmp/rendered.yaml
grep -c 'mikelauawaremd' /tmp/rendered.yaml    # → 0

# 3. No NodePort survives
grep -c 'NodePort' /tmp/rendered.yaml          # → 0

# 4. Registry override works
helm template pjx-release helm-pjx/ --set global.imageRegistry=localhost:5000 \
  | grep 'image:' | head -3

# 5. Ingress renders with the catch-all last
helm template pjx-release helm-pjx/ -s templates/pjx-ingress.yaml

# 6. Workflows are valid YAML
python3 -c "import yaml,sys; [yaml.safe_load(open(f)) for f in sys.argv[1:]]" \
  .github/workflows/build.yml .github/workflows/chart.yml && echo "workflows parse"

# 7. If you set up k3d in Phase 6, deploy for real
helm upgrade --install pjx-release helm-pjx/ -f helm-pjx/environments/dev.yaml
kubectl get pods,svc,ingress
```

Push the branch and confirm the `build` workflow's `test` job passes on the PR
before merging. PR builds do not push images, so it is a safe first run.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-7-cicd
```

If a release was deployed to a cluster: `helm rollback pjx-release` or
`helm uninstall pjx-release`.

---

## Follow-up work, deliberately not in this plan

Recorded here so it is not lost:

- **[Phase 8](phase-8-duende.md) — SSO to Duende IdentityServer.** Not "not in
  this plan", but explicitly outside the mandatory path. Gate: before any
  production deployment
- `oidc-client` 1.10.1 → `oidc-client-ts` (deprecated dependency)
- `react-scripts` 3.4.3 → Vite or a current CRA (blocks Node 20, noted in
  [Phase 6](phase-6-devcontainer-image.md))
- `README.md:104` claims `projects/` is gitignored; it is tracked (Decision D3)
- `projects/pjx-api-node/Jenkinsfile` — delete once Actions is working
- `pjx-dummy` templates — determine whether this is still needed or leftover
  scaffolding
- `projects/pjx-test-automation` — not integrated into `validate.sh` or CI
