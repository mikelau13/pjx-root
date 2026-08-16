# Phase 7 — Helm chart cleanup

**Goal:** a Helm chart that can actually be deployed — image tags from
`values.yaml`, one routing mechanism instead of two, and per-environment values
files.

**Risk:** Medium — the chart edits change how deployment works.

**Reversible:** yes.

**Depends on:** [Phase 6](phase-6-devcontainer-image.md) (toolchain to lint and
render charts).

**Read first:** [The Helm chart, before and after Phase 7](../reference/helm-chart.md)
— diagrams of what changes here, plus what Helm does with `values.yaml` and
`_helpers.tpl` if that model is new.

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

**3. `kubernetes/` and `helm-pjx/templates/` have already drifted.** 11 files
each. `NOTES.txt` is Helm-only; of the 10 shared files, **6 are byte-identical
and 4 differ** — and the 4 that differ are exactly the 4 that were templated:

```
DIFFERS:   pjx-api-dotnet.yaml  pjx-api-node.yaml  pjx-config.yaml  pjx-web-react.yaml
identical: pjx-graphql-apollo  pjx-sso-identityserver  pjx-dummy
           pjx-ingress  pjx-namespace  pjx-secret
```

An earlier draft of this doc called them "byte-for-byte the same". They are not —
the drift has already started, which argues for deleting `kubernetes/` more
strongly, not less.

**3b. Only 4 of 10 templates contain any Helm syntax at all.**

```bash
grep -rl '{{' helm-pjx/templates/
#  → pjx-api-dotnet, pjx-api-node, pjx-config, pjx-web-react
```

`pjx-graphql-apollo`, `pjx-sso-identityserver`, `pjx-dummy`, `pjx-ingress`,
`pjx-namespace` and `pjx-secret` are plain YAML sitting in a templates directory.
So [Step 1](#step-1--parameterise-images) is not "swap a line in each template" —
apollo and sso need templating built from scratch, and they have **no
`values.yaml` keys at all**. `values.yaml` currently holds only `dotnet_api`,
`node_api`, `react`, and `ssoUrl`.

**3c. `ssoUrl` is a hidden dependency on a NodePort this phase deletes.**

```yaml
# values.yaml
ssoUrl: http://pjx-sso-identityserver:30501
```
```yaml
# templates/pjx-config.yaml — a ConfigMap consumed by pjx-api-dotnet
sso-authority: {{ .Values.ssoUrl }}
```

That is the .NET API's OIDC authority, and it is wrong twice over: it points at
NodePort **30501**, which [Step 2](#step-2--pick-one-routing-mechanism) removes,
and `pjx-sso-identityserver` is not the Service name — that is `pjx-sso-service`.
Fixed in [Step 2a](#step-2a--fix-the-sso-authority).

**3d. `pjx-namespace.yaml` will collide with Phase 7b.** The chart creates
namespace `pjx`; [Phase 7b](phase-7b-local-k8s.md) installs with
`--namespace pjx --create-namespace`. Helm rejects a release containing a
namespace it was also asked to create, typically with "invalid ownership
metadata". **Delete the template** — charts conventionally do not create their own
namespace, and `--create-namespace` is the idiomatic mechanism.

**3e. `pjx-dummy.yaml` serves no purpose.** A placeholder Deployment plus
`NodePort` 31001 with a hardcoded image. **Delete it.**

```bash
git rm helm-pjx/templates/pjx-dummy.yaml helm-pjx/templates/pjx-namespace.yaml
```

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
  imageTag: ""        # overrides every per-service tag; Phase 7b sets "latest"

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
  # Base domain. Subdomains are derived in the template: api., ql., sso., node.
  # Matches Phase 2 — NOT pjx.local, which is reserved for mDNS.
  host: pjx.test
  tls:
    enabled: false
    secretName: ""      # Phase 7b supplies the mkcert secret
```

> ### This is a restructure, not a rename
>
> The existing file has only `dotnet_api`, `node_api`, `react` (plus `ssoUrl`).
> So this step does three things at once, and all three are required for the
> `pjx.image` helper below to work:
>
> | Change | Affects |
> |---|---|
> | `dotnet_api` → `dotnetApi`, `node_api` → `nodeApi`, `react` → `web` | the 3 templates that already reference them |
> | **add** `apollo:` and `sso:` — they have no keys today | `pjx-graphql-apollo.yaml`, `pjx-sso-identityserver.yaml` |
> | **add** `global:` and per-service `image:` blocks | all 5 deployment templates |
>
> Do the renames and their template references **in one commit**. A half-applied
> rename renders as an empty string rather than failing, so `helm template`
> succeeds and you get `image: ghcr.io/mikelau13/:` — a manifest that looks
> plausible and fails at pull time.
>
> `pjx-graphql-apollo.yaml` and `pjx-sso-identityserver.yaml` contain **no Helm
> syntax at all** today (finding 3b). For those two you are adding the first
> `{{ }}` in the file, not editing an existing expression.

Add `helm-pjx/templates/_helpers.tpl`:

```
{{/*
Fully-qualified image reference.

Tag precedence: per-service image.tag → global.imageTag → .Chart.AppVersion, so a
chart release and its images move together by default while a single --set can
override everything.

An EMPTY global.imageRegistry must emit a bare name, not a leading slash. Phase 7b
relies on this: k3d-imported images are referenced as `pjx-root-pjx-web-react:latest`
with no registry, and `/pjx-root-pjx-web-react:latest` is not a valid reference.
*/}}
{{- define "pjx.image" -}}
{{- $registry := .root.Values.global.imageRegistry | default "" -}}
{{- $tag := .svc.image.tag | default .root.Values.global.imageTag | default .root.Chart.AppVersion -}}
{{- if $registry -}}
{{- printf "%s/%s:%s" $registry .svc.image.repository $tag -}}
{{- else -}}
{{- printf "%s:%s" .svc.image.repository $tag -}}
{{- end -}}
{{- end -}}
```

> ### Both conditionals exist for [Phase 7b](phase-7b-local-k8s.md)
>
> Its `environments/local.yaml` sets `global.imageRegistry: ""` and
> `global.imageTag: latest`. A naive helper — unconditional `printf "%s/%s:%s"`
> reading only `.svc.image.tag` — breaks on both counts: it renders a leading
> slash, and it ignores `imageTag` entirely so every image falls back to
> `.Chart.AppVersion`.
>
> Neither failure is caught by `helm lint` or `helm template`; both produce
> syntactically valid manifests that fail at pull time as `ImagePullBackOff`,
> which reads as a missing image rather than a rendering bug.

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

**`port` is not 80 everywhere.** Keep each Service's existing number — only the
`type`, the deleted `nodePort`, and the added `name` change:

| Service | `port` / `targetPort` |
|---|---|
| `pjx-react-service` | 80 |
| `pjx-dotnet-service` | 80 |
| `pjx-apollo-service` | **4000** |
| `pjx-node-service` | **8081** |
| `pjx-sso-service` | 80 |

> ### Three things `helm template` cannot catch here
>
> All three render as perfectly valid YAML and fail only when a cluster sees
> them — which means Phase 7b, after everything else has looked correct.
>
> **`ClusterIP` is case-sensitive.** `clusterIP` is rejected by the API server
> with `Unsupported value: "clusterIP": supported values: "ClusterIP",
> "ExternalName", "LoadBalancer", "NodePort"`. Helm treats it as an ordinary
> string and says nothing.
>
> **A leftover `nodePort` invalidates a `ClusterIP` Service.** Delete the line;
> changing only `type` is not enough.
>
> **An untemplated host survives silently.** Missing one
> `{{ .Values.ingress.host }}` leaves a stale `*.pjx.com` rule that renders fine
> and never matches a request. If that one is `sso`, the login redirect breaks
> while the other four hostnames work — which reads as an auth bug.
>
> ```bash
> helm template pjx-release helm-pjx/ | grep -c 'type: ClusterIP'    # → 5
> helm template pjx-release helm-pjx/ | grep -cE 'nodePort|NodePort'  # → 0
> helm template pjx-release helm-pjx/ | grep -c 'pjx.com'             # → 0
> ```

Then fix `pjx-ingress.yaml`. It currently routes three hosts
(`api.pjx.com`, `ql.pjx.com`, `sso.pjx.com`) to ports 80/82/83 — and **82/83 do
not exist** on those services. It also has no route to the React app, which is
the actual entry point.

Rewrite as **host-based** routing across the five Phase 2 hostnames:

```yaml
{{- if .Values.ingress.enabled }}
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: pjx-ingress
  annotations:
    kubernetes.io/ingress.class: {{ .Values.ingress.className }}
spec:
  {{- if .Values.ingress.tls.enabled }}
  tls:
    - hosts:
        - {{ .Values.ingress.host }}
        - api.{{ .Values.ingress.host }}
        - ql.{{ .Values.ingress.host }}
        - sso.{{ .Values.ingress.host }}
        - node.{{ .Values.ingress.host }}
      secretName: {{ .Values.ingress.tls.secretName }}
  {{- end }}
  rules:
    - host: {{ .Values.ingress.host }}                 # pjx.test → React
      http:
        paths:
          - path: /
            pathType: Prefix
            backend: { service: { name: pjx-react-service, port: { name: http } } }
    - host: api.{{ .Values.ingress.host }}
      http:
        paths:
          - path: /
            pathType: Prefix
            backend: { service: { name: pjx-dotnet-service, port: { name: http } } }
    - host: ql.{{ .Values.ingress.host }}
      http:
        paths:
          - path: /
            pathType: Prefix
            backend: { service: { name: pjx-apollo-service, port: { name: http } } }
    - host: sso.{{ .Values.ingress.host }}
      http:
        paths:
          - path: /
            pathType: Prefix
            backend: { service: { name: pjx-sso-service, port: { name: http } } }
    - host: node.{{ .Values.ingress.host }}
      http:
        paths:
          - path: /
            pathType: Prefix
            backend: { service: { name: pjx-node-service, port: { name: http } } }
{{- end }}
```

> ### Host-based, not path-based — an earlier draft got this wrong
>
> That draft routed `/graphql`, `/api`, `/auth` and `/` under a single host and
> described it as "mirroring the Phase 2 hostname layout". It does the opposite.
>
> `react-scripts` bakes `REACT_APP_*` into the bundle at **build** time, so the
> deployed app issues requests to the literal `https://api.pjx.test/...` and
> `https://sso.pjx.test`. Under path-based routing those hostnames appear nowhere
> in the Ingress, so every API call and the whole OIDC flow fail — and
> [Phase 7b](phase-7b-local-k8s.md)'s browser pass fails completely.
>
> Host-based routing also means the existing mkcert wildcard certificate covers
> every hostname unchanged, and your `/etc/hosts` entries keep working. Same model
> as the Compose Traefik, so Phase 7b is comparing like with like.
>
> Revisit only after
> [Phase 10's runtime configuration](phase-10-deployable.md#step-3--react-runtime-configuration)
> makes the frontend's URLs changeable at deploy time.

`pjx-node-service` is included even though the browser does not call it directly —
Apollo reaches it in-cluster over Service DNS. Routing it costs nothing and makes
`node.pjx.test` available for debugging, exactly as under Compose.
> Service names must match the `metadata.name` in each service template — check
> each one, since the current names are inconsistent with the `appName` values.

---

## Step 2a — Fix the SSO authority

Removing `NodePort` breaks `ssoUrl` (finding 3c), because it points at port 30501.
In `values.yaml`:

```yaml
# In-cluster Service DNS. Was http://pjx-sso-identityserver:30501 — a NodePort
# that Step 2 removes, and a hostname that is not the Service name.
ssoUrl: http://pjx-sso-service:80
```

`pjx-config.yaml` needs no change; it already reads `{{ .Values.ssoUrl }}`.

> ### ⚠️ This will likely fail issuer validation until IdentityServer pins its issuer
>
> `pjx-api-dotnet` validates the `iss` claim (`Startup.cs` sets `Authority` and
> `MetadataAddress`; `ValidateIssuer` defaults to **true**, only
> `ValidateAudience` is disabled). IdentityServer4 derives the issuer from the
> incoming request, which Phase 2 made correct for browser traffic via
> `UseForwardedHeaders`.
>
> The mismatch: tokens minted through the browser carry
> `iss: https://sso.pjx.test`, but discovery fetched in-cluster from
> `http://pjx-sso-service:80` advertises `issuer: http://pjx-sso-service`. The API
> then rejects perfectly valid tokens.
>
> **Fix by pinning the issuer** so discovery reports the same value regardless of
> how it is reached — in `projects/pjx-sso-identityserver/Startup.cs`:
>
> ```csharp
> services.AddIdentityServer(options =>
> {
>     options.IssuerUri = Configuration["PJX_SSO__PUBLIC_ORIGIN"] ?? "https://sso.pjx.test";
> })
> ```
>
> With that set, in-cluster metadata fetch and browser-minted tokens agree, and
> the URL above works purely as a reachability address.
>
> This is the same class of problem as Phase 2's `UseForwardedHeaders` fix —
> IdentityServer inferring its identity from the request — so expect it to behave
> identically: the container starts fine and only token validation fails.
> [Phase 7b](phase-7b-local-k8s.md)'s browser pass on `/country/all` is the check
> that catches it.

---

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

## Step 3 — Per-environment values

```bash
mkdir -p helm-pjx/environments
```

`helm-pjx/environments/dev.yaml`:

```yaml
global:
  imagePullPolicy: Always
ingress:
  host: pjx.test
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

# 8. Deleted templates are really gone
grep -cE 'pjx-dummy|kind: Namespace' /tmp/rendered.yaml   # → 0

# 9. No half-applied rename — an empty image name renders as ".../:"  (see Step 1)
grep -E 'image: .*/:|image: .*:$' /tmp/rendered.yaml || echo "no empty image refs"

# 10. The SSO authority no longer points at a NodePort
grep -A1 'sso-authority' /tmp/rendered.yaml    # → http://pjx-sso-service:80
grep -c '30501' /tmp/rendered.yaml             # → 0
```

Checks 9 and 10 are the two that fail silently otherwise. A half-applied rename
renders valid YAML with an empty image name, and a stale `ssoUrl` renders a
perfectly well-formed ConfigMap pointing at a port that no longer exists — both
pass `helm lint`.

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
git branch -D feature/arch-phase-7-charts
```

If a release was deployed to a cluster: `helm rollback pjx-release` or
`helm uninstall pjx-release`.

---

