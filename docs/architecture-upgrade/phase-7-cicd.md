# Phase 7 — Helm chart cleanup

**Goal:** a Helm chart that can actually be deployed — image tags from
`values.yaml`, one routing mechanism instead of two, and per-environment values
files.

**Risk:** Medium — the chart edits change how deployment works.

**Reversible:** yes.

**Depends on:** [Phase 6](phase-6-devcontainer-image.md) (toolchain to lint and
render charts).

```bash
git checkout -b feature/arch-phase-7-charts
```

> ### Split from the original Phase 7
>
> This phase was originally "CI/CD and Helm chart cleanup". The two halves are now
> separate, and a local deploy sits between them:
>
> | Phase | Does |
> |---|---|
> | **7** (this) | Make the chart deployable |
> | [**7b**](phase-7b-local-k8s.md) | Deploy it to a local k3d cluster and pass the browser test |
> | [**7c**](phase-7c-cicd.md) | Automate building images and publishing charts |
>
> The reason: CI automates *publishing* artifacts, so the artifacts should be known
> good first. The chart in its current state cannot deploy anywhere — hardcoded
> image tags that do not exist locally, `NodePort` and Ingress fighting each other,
> and an Ingress pointing at ports that do not exist. Fixing that here, proving it
> on k3d in 7b, then automating in 7c means the pipeline ships something tested.
>
> `k3d image import` pushes locally-built Compose images straight into the
> cluster, so **no registry is needed** for 7b — CI genuinely is not a
> prerequisite.

---

## Current state

The charts have five concrete problems:

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

> Run these in the devcontainer unless a command is marked HOST. See
> [Where to run commands](README.md#where-to-run-commands) — `localhost` means
> something different in each shell.

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

# 6. Service names in the Ingress match the Service metadata.name
grep -oE 'name: pjx-[a-z-]+-service' /tmp/rendered.yaml | sort | uniq -c
#    → each name should appear at least twice: once as a Service, once as an
#      Ingress backend. A count of 1 means a typo, and the route 404s.

# 7. Nothing references the deleted secret template
grep -c 'pjx-secret' /tmp/rendered.yaml        # → 0
```

Check 6 is worth running carefully — the existing service names are inconsistent
with the `appName` values, so a mismatch here is likely and produces a 404 that
looks like an ingress problem rather than a naming one.

**This phase does not deploy anything.** `helm template` proves the chart
*renders*; [Phase 7b](phase-7b-local-k8s.md) proves it *runs*. Do not skip
straight to Azure on the strength of a clean render.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-7-cicd
```

If a release was deployed to a cluster: `helm rollback pjx-release` or
`helm uninstall pjx-release`.

---

