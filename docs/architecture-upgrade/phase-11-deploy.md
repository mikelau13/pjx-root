# Phase 11 — Deploy to AKS with continuous delivery

**Goal:** the demo running on AKS, deployed by GitHub Actions using federated
credentials — no Azure secrets stored in GitHub.

**Risk:** Medium. **Reversible:** yes — `helm rollback` or `helm uninstall`.

**Depends on:** Phase 9 (infrastructure) and Phase 10 (deployable application).

```bash
git checkout -b feature/arch-phase-11-deploy
```

---

## Step 1 — Federated credentials instead of a service principal secret

The usual approach stores an Azure client secret in GitHub. Don't — use OIDC
federation, where GitHub Actions exchanges its own workload identity token for an
Azure token. Nothing long-lived is stored anywhere.

```bash
source local/scripts/azure/00-vars.sh
GH_ORG="mikelau13"
GH_REPO="pjx-root"

az ad app create --display-name "gh-pjx-deploy"
APP_ID="$(az ad app list --display-name gh-pjx-deploy --query '[0].appId' -o tsv)"
az ad sp create --id "${APP_ID}"
SP_ID="$(az ad sp show --id "${APP_ID}" --query id -o tsv)"

# Scope to the resource group only — not the subscription.
RG_ID="$(az group show -n "${RG}" --query id -o tsv)"
az role assignment create --assignee-object-id "${SP_ID}" \
  --assignee-principal-type ServicePrincipal \
  --role "Azure Kubernetes Service Cluster User" --scope "${RG_ID}"
az role assignment create --assignee-object-id "${SP_ID}" \
  --assignee-principal-type ServicePrincipal \
  --role "AcrPush" --scope "${RG_ID}"

# One credential per trust subject. Branch pushes and tags are separate subjects.
az ad app federated-credential create --id "${APP_ID}" --parameters "{
  \"name\": \"gh-master\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${GH_ORG}/${GH_REPO}:ref:refs/heads/master\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"

echo "AZURE_CLIENT_ID=${APP_ID}"
```

Add three **variables** (not secrets — none of these are sensitive):
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

> A federated credential's `subject` is an exact match. Deploying from tags as
> well as `master` needs a second credential with
> `subject: repo:…:ref:refs/tags/*`. A mismatched subject fails at login with a
> generic AADSTS message that does not name the subject — check it first when
> login fails.

---

## Step 2 — Promote GHCR → ACR

Phase 7 builds to GHCR. AKS pulls from ACR via managed identity. Promotion
copies the image rather than rebuilding it, so the artifact deployed is byte-for-byte
the artifact tested.

`.github/workflows/deploy.yml`:

```yaml
name: deploy

on:
  push:
    branches: [master]
    tags: ['v*']
  workflow_dispatch:

permissions:
  contents: read
  packages: read
  id-token: write          # required for OIDC federation

env:
  RG: rg-pjx
  AKS: pjx-aks
  RELEASE: pjx-release

jobs:
  promote:
    runs-on: ubuntu-latest
    outputs:
      tag: ${{ steps.tag.outputs.tag }}
    steps:
      - uses: actions/checkout@v4

      - id: tag
        run: |
          if [[ "${GITHUB_REF}" == refs/tags/* ]]; then
            echo "tag=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"
          else
            echo "tag=dev-${GITHUB_SHA::7}" >> "$GITHUB_OUTPUT"
          fi

      - uses: azure/login@v2
        with:
          client-id:       ${{ vars.AZURE_CLIENT_ID }}
          tenant-id:       ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      # az acr import pulls straight registry-to-registry — the image bytes
      # never transit the runner, so this is fast and cannot rebuild-drift.
      - name: Import images into ACR
        run: |
          ACR="$(az acr list -g "${{ env.RG }}" --query '[0].name' -o tsv)"
          for svc in pjx-web-react pjx-graphql-apollo pjx-api-node \
                     pjx-api-dotnet pjx-sso-identityserver; do
            az acr import --name "${ACR}" --force \
              --source "ghcr.io/${{ github.repository_owner }}/${svc}:${{ steps.tag.outputs.tag }}" \
              --image "${svc}:${{ steps.tag.outputs.tag }}" \
              --username "${{ github.actor }}" \
              --password "${{ secrets.GITHUB_TOKEN }}"
          done
```

> `az acr import` needs GHCR credentials because pjx's GHCR packages inherit the
> repository's visibility. If you make the packages public, drop the
> `--username`/`--password` flags.

---

## Step 3 — Deploy with Helm

Appended to the same workflow:

```yaml
  deploy:
    needs: promote
    runs-on: ubuntu-latest
    environment: demo          # enables a manual approval gate if you want one
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id:       ${{ vars.AZURE_CLIENT_ID }}
          tenant-id:       ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      - uses: azure/setup-helm@v4
      - run: az aks get-credentials -g "${{ env.RG }}" -n "${{ env.AKS }}" --overwrite-existing

      - name: helm upgrade
        run: |
          ACR_LOGIN="$(az acr list -g "${{ env.RG }}" --query '[0].loginServer' -o tsv)"
          helm upgrade --install "${{ env.RELEASE }}" helm-pjx/ \
            --namespace pjx --create-namespace \
            -f helm-pjx/environments/demo.yaml \
            --set global.imageRegistry="${ACR_LOGIN}" \
            --set global.imageTag="${{ needs.promote.outputs.tag }}" \
            --atomic --timeout 10m

      - name: Smoke test
        run: |
          HOST="$(helm get values "${{ env.RELEASE }}" -n pjx -o json | jq -r '.ingress.host')"
          for path in / /api/health/ready /auth/.well-known/openid-configuration; do
            code="$(curl -sk -o /dev/null -w '%{http_code}' "https://${HOST}${path}")"
            echo "${path} → ${code}"
            [[ "${code}" =~ ^2 ]] || exit 1
          done
```

`--atomic` is the important flag: a failed release rolls back automatically
rather than leaving the cluster half-updated. `--timeout 10m` is generous because
.NET cold start on a single `B2ms` is slow.

> **The smoke test will fail from GitHub's runners.** The ingress is
> IP-restricted to your address (Phase 9 step 6), and Actions runners have
> unpredictable egress IPs. Options, in order of preference:
>
> 1. Replace the external smoke test with an in-cluster one:
>    `kubectl run --rm -i --restart=Never curl --image=curlimages/curl -- …`
>    against the ClusterIP services. Tests the app, not the ingress, and works
>    regardless of the allowlist.
> 2. Add GitHub's IP ranges to the allowlist — large, changes often, and
>    substantially weakens the restriction. Not recommended.
> 3. Drop the external check and verify manually after deploy.
>
> Option 1 is what I would build. Do not solve this by widening the allowlist —
> that quietly removes the reasoning that keeps [Phase 8](phase-8-duende.md)
> optional.

---

## Step 4 — The demo values file

`helm-pjx/environments/demo.yaml`:

```yaml
global:
  imagePullPolicy: IfNotPresent

ingress:
  enabled: true
  className: traefik
  host: demo.pjx.example.com      # your DEMO_HOST from Phase 9
  tls:
    enabled: true
    issuer: letsencrypt

keyVault:
  enabled: true
  name: kv-pjx-xxxxxx             # from Phase 9
  clientId: "<CSI driver client id from Phase 9 step 5>"
  tenantId: "<your tenant id>"

# One replica each. SQLite is gone, but a single B2ms node has no room
# for more, and this is a demo.
web:       { replicas: 1 }
apollo:    { replicas: 1 }
nodeApi:   { replicas: 1 }
dotnetApi: { replicas: 1 }
sso:       { replicas: 1 }
```

No secrets appear here — connection strings, the signing certificate password,
and the OTLP headers all arrive from Key Vault via the `SecretProviderClass`
from Phase 10. This is the specific place where the plan diverges from
`AwareServices/helm/values.aks-deploy.yaml`, which carries a live connection
string in plaintext.

Add the ingress TLS block to `pjx-ingress.yaml`:

```yaml
  annotations:
    cert-manager.io/cluster-issuer: {{ .Values.ingress.tls.issuer }}
spec:
  tls:
    - hosts: [{{ .Values.ingress.host }}]
      secretName: pjx-tls
```

---

## Step 5 — First deploy, by hand

Run it manually once before trusting the pipeline. When something is wrong you
want a shell, not a workflow log.

```bash
source local/scripts/azure/00-vars.sh
az aks get-credentials -g "${RG}" -n "${AKS}" --overwrite-existing

# Render and read it before applying anything.
helm template pjx-release helm-pjx/ -f helm-pjx/environments/demo.yaml \
  --set global.imageRegistry="${ACR}.azurecr.io" \
  --set global.imageTag=dev-local > /tmp/rendered.yaml
less /tmp/rendered.yaml

helm upgrade --install pjx-release helm-pjx/ \
  --namespace pjx --create-namespace \
  -f helm-pjx/environments/demo.yaml \
  --set global.imageRegistry="${ACR}.azurecr.io" \
  --set global.imageTag=dev-local \
  --atomic --timeout 10m

kubectl -n pjx get pods -w
```

When a pod will not start, in this order:

```bash
kubectl -n pjx describe pod <name>        # scheduling, image pull, probe failures
kubectl -n pjx logs <name> --previous     # crash before the current attempt
kubectl -n pjx get events --sort-by=.lastTimestamp | tail -30
kubectl -n pjx get secretproviderclass,secrets    # Key Vault projection
```

Most likely first failures, in rough order of probability:

1. **CSI secret projection** — wrong Key Vault name, client ID, or a missing
   role assignment. `describe pod` shows the mount failing.
2. **OIDC redirect mismatch** — the issuer is now
   `https://demo.…/auth`, and `Config.cs` still has Phase 2's
   `https://pjx.test/...`. Login breaks; nothing else does.
3. **Postgres connectivity** — firewall rule or `SslMode=Require` missing.
4. **Probe timing** — .NET cold start exceeding `initialDelaySeconds` on a
   contended node, causing a restart loop that looks like a crash.

---

## Verify

```bash
# 1. Everything running
kubectl -n pjx get pods,svc,ingress
kubectl -n pjx get certificate    # → Ready=True

# 2. Images came from ACR, not GHCR
kubectl -n pjx get pods -o jsonpath='{range .items[*]}{.spec.containers[0].image}{"\n"}{end}'

# 3. Secrets projected from Key Vault, and nothing hardcoded
kubectl -n pjx get secret pjx-runtime-secrets -o jsonpath='{.data}' | jq 'keys'

# 4. TLS is a real Let's Encrypt certificate
echo | openssl s_client -connect "${DEMO_HOST}:443" -servername "${DEMO_HOST}" 2>/dev/null \
  | openssl x509 -noout -issuer -subject -dates

# 5. Reachable from your IP, and NOT from elsewhere
curl -s -o /dev/null -w '%{http_code}\n' "https://${DEMO_HOST}/"
#    From a phone on cellular: must time out

# 6. Telemetry arriving in Grafana Cloud
#    Explore → Tempo → service.name = pjx-api-dotnet

# 7. Rollback works — exercise it before you need it
helm history pjx-release -n pjx
helm rollback pjx-release -n pjx
```

**Then the full manual browser pass** against `https://${DEMO_HOST}` — register,
activate, log in, `/country/all`, `/cities`, profile, calendar CRUD, sign out.

Two differences from every prior phase:

- **Activation codes are in pod logs now**, not `docker logs`:
  `kubectl -n pjx logs -l app=pjx-sso --tail=50`
- **Calendar CRUD is the critical test.** It exercises PostgreSQL through EF Core
  with real `DateTime` values — the thing Phase 10 was most likely to break.

Check 5 is non-negotiable. Verify the restriction from a genuinely different
network before showing anyone the URL.

---

## Rollback

```bash
helm rollback pjx-release -n pjx        # previous release
helm uninstall pjx-release -n pjx       # remove the app, keep the cluster
az aks stop -g rg-pjx -n pjx-aks        # stop paying for compute
az group delete -n rg-pjx --yes         # remove everything
```

---

## After this phase

The demo is live and IP-restricted. To make it public:

1. Do [Phase 8](phase-8-duende.md) — Duende + `net8.0`, which removes the
   unpatched `aspnet:3.1` base image.
2. Then, and only then, widen `service.spec.loadBalancerSourceRanges` to
   `0.0.0.0/0` and re-run the Phase 9 ingress step.

That ordering is the whole reason the IP restriction was chosen: it lets the
deployment pipeline be proven before the auth migration, rather than making the
auth migration a prerequisite for deploying anything at all.
