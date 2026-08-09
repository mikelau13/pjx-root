# Phase 7b — Local Kubernetes with k3d

**Goal:** run the whole stack on a real Kubernetes cluster on your machine, and
pass the same browser test you pass under Docker Compose — before spending
anything on Azure.

**Risk:** Medium. No cloud cost, fully disposable (`k3d cluster delete pjx`), but
it is the first time the Helm charts actually run rather than merely render.

**Depends on:** [Phase 6](phase-6-devcontainer-image.md) (kubectl, helm, k9s,
k3d), [Phase 7](phase-7-cicd.md) (charts parameterised — the pre-cleanup chart
cannot deploy), and [Phase 5](phase-5-otel.md) (health endpoints, so probes have
something to hit).

```bash
git checkout -b feature/arch-phase-7b-local-k8s
```

---

## Why before CI/CD

[Phase 7c](phase-7c-cicd.md) automates building images and publishing charts. If
the chart has never run, CI just automates publishing a broken chart. Deploying
locally first means that by the time you write the pipeline you *know* the
artifacts work.

It is also free and fast to iterate on, where the alternative — learning
Kubernetes for the first time against a paid AKS cluster — is neither.

**No registry needed.** `k3d image import` pushes your locally-built Compose
images straight into the cluster, so CI is genuinely not a prerequisite.

---

## ⚠️ The Compose stack and the k3d cluster cannot run at the same time

Both want host ports **80** and **443**. This is the third instance of this exact
conflict in the project — after CloudDevEnvironment's `central-router` and its
leftover `simpleproxy` — and it fails the same misleading way: the cluster's load
balancer silently has no ports, or `k3d cluster create` errors on a port bind.

**Always stop one before starting the other:**

```bash
# switching to k3d
stop.sh                                              # the five app containers
docker compose -f local/docker-compose.yml down      # the Compose Traefik
ss -tln | grep -E ':(80|443) ' || echo "80/443 free"
```

```bash
# switching back to Compose
k3d cluster stop pjx        # or: k3d cluster delete pjx
dev-up.sh -d
docker compose -f local/docker-compose.yml up -d
```

### Why not just map k3d to 8080/8443 and run both?

Tempting, and it breaks the React app. Its production image bakes `REACT_APP_*`
values at **build** time, so the bundle contains `https://pjx.test` with port 443
implied. Served on `:8443`, the SPA loads but every API call goes to the wrong
port — and you would be debugging
[Phase 10's runtime-config problem](phase-10-deployable.md#step-3--react-runtime-configuration)
before reaching Phase 10.

With k3d on 80/443, your existing `/etc/hosts` entries and mkcert certificate
work unchanged and the app behaves exactly as it does under Compose. The cost is
that the two stacks are mutually exclusive, which is the right trade here.

Once Phase 10 lands runtime configuration, running both on different ports
becomes viable — revisit then if you want side-by-side comparison.

---

## Step 1 — Install k3d

If [Phase 6](phase-6-devcontainer-image.md) added it to the devcontainer image,
skip this. Otherwise, pinned:

```dockerfile
ARG K3D_VERSION=v5.7.4
RUN curl -fsSL https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh \
    | TAG=${K3D_VERSION} bash
```

```bash
k3d version
```

> k3d creates its cluster nodes as containers on the **host** daemon, which works
> from the devcontainer because of the mounted socket. Same
> docker-outside-of-docker arrangement as everything else.

---

## Step 2 — Create the cluster

```bash
k3d cluster create pjx \
  --port "80:80@loadbalancer" \
  --port "443:443@loadbalancer" \
  --agents 1
```

```bash
kubectl cluster-info
kubectl get nodes
```

`k3d` writes a context into your kubeconfig and switches to it. Confirm you are
pointed at the right cluster before every `kubectl` command that matters:

```bash
kubectl config current-context      # → k3d-pjx
```

> **k3s bundles Traefik as its default ingress controller.** That is a real
> convenience: your `className: traefik` and existing ingress annotations carry
> over unchanged, so you are testing the routing model you already understand
> rather than learning NGINX. It is a different Traefik instance from the Compose
> one — same software, separate deployment.

---

## Step 3 — Import the locally-built images

k3s has its own containerd and cannot see the host daemon's images. Without this
step every pod sits in `ImagePullBackOff` trying to reach a registry.

```bash
for svc in pjx-web-react pjx-graphql-apollo pjx-api-node pjx-api-dotnet pjx-sso-identityserver; do
  k3d image import "pjx-root-${svc}:latest" -c pjx
done
```

```bash
docker exec k3d-pjx-server-0 crictl images | grep pjx-root
```

Those are the image names Compose produces (`<project>-<service>`). Verify with
`docker images | grep pjx-root` if they differ.

> Imported images must be referenced with `imagePullPolicy: Never` or
> `IfNotPresent` — with `Always`, Kubernetes ignores the local copy and tries the
> registry. Set it in `environments/local.yaml` below.

---

## Step 4 — TLS from the existing mkcert certificate

No new CA, no browser re-import:

```bash
cd local/central-router/config/cert
CERT=$(ls *.pem | grep -v -- '-key' | head -1)
kubectl create namespace pjx
kubectl -n pjx create secret tls pjx-tls --cert="${CERT}" --key="${CERT%.pem}-key.pem"
kubectl -n pjx get secret pjx-tls
```

The certificate already covers `*.pjx.test` and `pjx.test`, so every hostname
works.

---

## Step 5 — A local values file

`helm-pjx/environments/local.yaml`:

```yaml
global:
  # Images are imported into k3s, not pulled. Always would bypass the local copy.
  imagePullPolicy: IfNotPresent
  imageRegistry: ""          # bare names: pjx-root-pjx-web-react:latest
  imageTag: latest

ingress:
  enabled: true
  className: traefik
  host: pjx.test
  tls:
    enabled: true
    secretName: pjx-tls      # created in step 4, not cert-manager

# SQLite is ephemeral and single-writer — one replica only until Phase 10
# replaces it with PostgreSQL.
web:       { replicas: 1 }
apollo:    { replicas: 1 }
nodeApi:   { replicas: 1 }
dotnetApi: { replicas: 1 }
sso:       { replicas: 1 }

keyVault:
  enabled: false             # Azure-only; secrets come from the image locally
```

`global.imageRegistry: ""` requires the `pjx.image` helper from
[Phase 7 Step 1](phase-7-cicd.md#step-1--parameterise-images) to omit the
registry prefix when empty — check that it does, or the name renders as
`/pjx-root-pjx-web-react:latest` with a leading slash.

---

## Step 6 — Add the probes to the templates

The endpoints exist from
[Phase 5 Step 5d](phase-5-otel.md#step-5d--health-checks). Declare them per
service, using each one's own path and container port:

```yaml
        readinessProbe:
          httpGet: { path: /health/ready, port: 80 }
          initialDelaySeconds: 10
          periodSeconds: 10
        livenessProbe:
          httpGet: { path: /health/live, port: 80 }
          initialDelaySeconds: 30
          periodSeconds: 20
          failureThreshold: 3
```

| Service | Readiness path | Container port |
|---|---|---|
| `pjx-api-dotnet` | `/health/ready` | 80 |
| `pjx-sso-identityserver` | `/health/ready` | 80 |
| `pjx-api-node` | `/healthcheck` | 8081 |
| `pjx-graphql-apollo` | `/.well-known/apollo/server-health` | 4000 |
| `pjx-web-react` | `/` | 3000 |

> **`pjx-api-dotnet`'s readiness does not check the database.** Its
> `AddDbContextCheck` had to be removed —
> [EF Core is still 3.1.7 there](phase-4-dotnet8.md#it-is-now-blocking-not-merely-stale)
> and the health-check EF package drags in EF Core 8, which breaks the Sqlite
> provider outright.
>
> Consequence for this phase: the pod reports `Ready` as soon as Kestrel answers,
> so a `CrashLoopBackOff` caused by an unwritable SQLite path (failure mode 2
> below) will *not* be caught by the probe — it surfaces as 500s from the
> ingress instead. Check `kubectl logs` rather than trusting `READY 1/1`.
>
> `pjx-sso-identityserver` keeps its database check, so its probe is the more
> meaningful of the two. [Phase 10 Step 0](phase-10-deployable.md#step-2--sqlite--postgresql)
> restores the API's.

---

## Step 7 — Deploy

Render and read it first:

```bash
helm template pjx-release helm-pjx/ -f helm-pjx/environments/local.yaml > /tmp/local.yaml
less /tmp/local.yaml
```

```bash
helm upgrade --install pjx-release helm-pjx/ \
  --namespace pjx --create-namespace \
  -f helm-pjx/environments/local.yaml \
  --atomic --timeout 5m
```

```bash
kubectl -n pjx get pods,svc,ingress -w
```

`--atomic` rolls back a failed release instead of leaving the namespace
half-updated.

### When a pod will not start

```bash
kubectl -n pjx describe pod <name>          # scheduling, image pull, probe failures
kubectl -n pjx logs <name> --previous       # the crash before the current attempt
kubectl -n pjx get events --sort-by=.lastTimestamp | tail -30
k9s -n pjx                                  # interactive, faster for browsing
```

Most likely first failures:

1. **`ImagePullBackOff`** — image not imported, or `imagePullPolicy: Always`
2. **`CrashLoopBackOff` on the .NET services** — SQLite path not writable in the
   container, or the connection string points somewhere that does not exist
3. **Probe failures during startup** — .NET cold start exceeding
   `initialDelaySeconds`; raise it rather than assuming the app is broken
4. **Ingress 404** — `className` not matching k3s's Traefik, or the host not
   matching `/etc/hosts`

---

## Verify

> Run these in the devcontainer unless a command is marked HOST. See
> [Where to run commands](README.md#where-to-run-commands) — `localhost` means
> something different in each shell.

```bash
# 1. Everything running and ready
kubectl -n pjx get pods
#    → all 5 Running, READY 1/1

# 2. Probes are actually declared, not just written
kubectl -n pjx get deploy -o json | grep -c readinessProbe    # → 5

# 3. Images came from the local import, not a registry
kubectl -n pjx get pods -o jsonpath='{range .items[*]}{.spec.containers[0].image}{"\n"}{end}'

# 4. Ingress has an address
kubectl -n pjx get ingress
```

**HOST:**

```bash
# 5. TLS terminates with the mkcert certificate
echo | openssl s_client -connect pjx.test:443 -servername pjx.test 2>&1 | grep -E 'Verify return code|issuer='

# 6. Every hostname answers
for h in pjx ql.pjx api.pjx node.pjx sso.pjx; do
  printf '  %-18s %s\n' "$h.test" "$(curl -s -o /dev/null -w '%{http_code}' -k --max-time 5 https://$h.test/)"
done
```

**Then the browser pass** at <https://pjx.test> — register, activate (activation
code from `kubectl -n pjx logs -l app=pjx-sso --tail=50`), log in,
`/country/all`, `/cities`, sign out.

That full pass is the point of this phase. Passing it locally means Phase 11's
AKS deploy is "the same thing, elsewhere" rather than a first attempt.

### Expect these, do not debug them

- **Data vanishes on pod restart.** SQLite in an ephemeral container.
  [Phase 10](phase-10-deployable.md) replaces it with PostgreSQL.
- **The signing certificate is the committed one.** Insecure, local only — also
  Phase 10.
- **No telemetry reaches Grafana** unless you point
  `OTEL_EXPORTER_OTLP_ENDPOINT` at something reachable from the cluster. The
  Compose Grafana is on `pjx-network`, which k3s pods are not.

---

## Rollback

```bash
helm -n pjx uninstall pjx-release      # remove the app, keep the cluster
k3d cluster stop pjx                   # keep it for later, frees the ports
k3d cluster delete pjx                 # remove entirely
```

Then return to Compose:

```bash
dev-up.sh -d
docker compose -f local/docker-compose.yml up -d
status.sh
```

Nothing in this phase touches the Compose stack, so the switch back is clean.
