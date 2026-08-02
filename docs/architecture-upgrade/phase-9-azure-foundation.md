# Phase 9 — Azure foundation

**Goal:** provision the Azure resources pjx needs — ACR, AKS, PostgreSQL, Key
Vault, DNS — plus the in-cluster controllers (Traefik, cert-manager, Secrets
Store CSI driver). No application changes.

**Risk:** Low technically, but it is the first phase that **costs money**. Read
the cost section before running anything.

**Reversible:** yes — `az group delete` removes everything.

**Depends on:** Phase 7 (charts parameterised, images in GHCR).

```bash
git checkout -b feature/arch-phase-9-azure-foundation
```

---

## Locked decisions this phase implements

| Decision | Choice |
|---|---|
| Registry | **ACR**, following CloudDevEnvironment's pattern (GHCR → ACR) |
| Database | **Azure Database for PostgreSQL Flexible Server**, Burstable |
| Ingress | **Traefik**, for parity with local dev (Phase 2) |
| TLS | cert-manager + Let's Encrypt, **DNS-01** — see the note below |
| Access | **IP-restricted**, which keeps [Phase 8](phase-8-duende.md) optional |
| Observability | **Grafana Cloud free tier** — nothing monitoring-related deployed in-cluster |

### Why DNS-01 and not HTTP-01

Earlier drafts of this plan assumed HTTP-01, which needs no DNS-provider
integration — just an A record. **That does not work with an IP-restricted load
balancer**: Let's Encrypt validates by fetching a token over the public
internet, and it deliberately publishes no stable source-IP list to allowlist.

So this phase provisions an **Azure DNS zone** (~$0.50/month) and uses DNS-01,
which proves domain control via a TXT record and needs no inbound reachability at
all. It also enables wildcard certificates if you ever want per-subdomain
routing back.

If you later remove the IP restriction (the last step of Phase 8), HTTP-01
becomes available — but there is no reason to switch back.

---

## Cost — read this first

Approximate monthly figures, **verify current pricing**:

| Resource | SKU | ~Cost/month |
|---|---|---|
| AKS control plane | Free tier (no SLA) | $0 |
| Node pool | 1 × `Standard_B2ms` (2 vCPU, 8GB) | ~$35–40 |
| ACR | Basic | ~$5 |
| PostgreSQL Flexible Server | Burstable `B1ms` + 32GB storage | ~$15–20 |
| Public IP | Standard, static | ~$4 |
| Azure DNS zone | 1 zone | ~$0.50 |
| Domain registration | `.com` or `.dev` at a registrar | ~$1/month equivalent |
| Grafana Cloud | Free tier | $0 |
| **Total, running continuously** | | **~$60–70** |

### Cost controls — use these

```bash
# Stop the cluster between demos. Billing for the node pool stops;
# the control plane and all config persist.
az aks stop  --name pjx-aks --resource-group rg-pjx
az aks start --name pjx-aks --resource-group rg-pjx
```

Stopping between demos is the single biggest lever — it takes the running cost
down to roughly ACR + Postgres + IP + DNS, around $25/month.

`Standard_B2ms` is deliberate. A `B2s` (4GB) has to host Traefik, cert-manager,
the CSI driver, and five app pods; 4GB is too tight and you will chase OOM kills
instead of doing the deployment. If you want to try it, scale down later rather
than debugging a starved cluster on day one.

> **Azure Container Apps** would host this demo for a fraction of the cost with
> scale-to-zero. AKS is the choice here because the repo already carries Helm
> charts and k8s manifests, and because CloudDevEnvironment targets AKS — the
> goal is platform parity, not cheapest hosting. Worth re-deciding if cost
> becomes the binding constraint.

---

## Step 0 — Prerequisites

Phase 6's devcontainer image needs `azure-cli` restored — I removed it there as
"AwareMD-specific", which was wrong given this phase. Add to
`.devcontainer/Dockerfile`:

```dockerfile
# Azure CLI + kubelogin (required for AKS with Entra auth)
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash && \
    az aks install-cli
```

Rebuild the container, then authenticate. `az login` is interactive, so run it
yourself:

```bash
az login
az account set --subscription "<your-subscription-id>"
az account show --query '{name:name, id:id}' -o tsv
```

Register the providers this phase needs (one-time per subscription, and slow):

```bash
az provider register --namespace Microsoft.ContainerService --wait
az provider register --namespace Microsoft.DBforPostgreSQL --wait
az provider register --namespace Microsoft.KeyVault --wait
```

---

## Step 1 — Naming and a script to hold it

Put the provisioning in a script rather than pasting commands — you will run
parts of it more than once.

Create `local/scripts/azure/00-vars.sh`:

```bash
#!/bin/bash
# Shared variables for the Azure provisioning scripts. Source, don't execute.
export LOCATION="canadacentral"
export RG="rg-pjx"
export ACR="acrpjx$(echo -n "${RG}" | md5sum | cut -c1-6)"   # ACR names must be globally unique
export AKS="pjx-aks"
export KV="kv-pjx-$(echo -n "${RG}" | md5sum | cut -c1-6)"   # so must Key Vault names
export PG="pg-pjx"
export DNS_ZONE="pjx.example.com"        # <-- your registered domain
export DEMO_HOST="demo.${DNS_ZONE}"
export ACME_EMAIL="mike.lau@awaremd.com"

# Your public IP, for the ingress allowlist. Re-run when it changes.
export MY_IP="$(curl -s ifconfig.me)/32"
```

> `canadacentral` matches CloudDevEnvironment's App Insights resources
> (`AppInsight-awmd-*-cace-01`). Any region works — keep everything in one to
> avoid cross-region egress charges.

Both ACR and Key Vault names must be globally unique across all of Azure, which
is why they are derived rather than fixed. If the derived name collides, set it
explicitly.

---

## Step 2 — Resource group, ACR, AKS

`local/scripts/azure/01-core.sh`:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/00-vars.sh"

az group create --name "${RG}" --location "${LOCATION}"

az acr create --resource-group "${RG}" --name "${ACR}" \
  --sku Basic --admin-enabled false

# --tier free: no control-plane SLA, appropriate for a demo.
# Workload identity + OIDC issuer are needed for Key Vault access without secrets.
az aks create \
  --resource-group "${RG}" \
  --name "${AKS}" \
  --tier free \
  --node-count 1 \
  --node-vm-size Standard_B2ms \
  --enable-managed-identity \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --enable-addons azure-keyvault-secrets-provider \
  --generate-ssh-keys \
  --network-plugin azure

# Managed-identity pull from ACR — no imagePullSecret anywhere.
az aks update --resource-group "${RG}" --name "${AKS}" --attach-acr "${ACR}"

az aks get-credentials --resource-group "${RG}" --name "${AKS}" --overwrite-existing
kubectl get nodes
```

`--attach-acr` is the reason ACR beats GHCR for the cluster: the kubelet's
managed identity gets `AcrPull`, so no registry credentials exist in the cluster
at all.

`--enable-addons azure-keyvault-secrets-provider` installs the Secrets Store CSI
driver that Phase 10 uses for the signing certificate.

---

## Step 3 — PostgreSQL

`local/scripts/azure/02-postgres.sh`:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/00-vars.sh"

# Generated once, then stored in Key Vault (step 5). Never in a values file.
PG_ADMIN_PASSWORD="$(openssl rand -base64 24)"

az postgres flexible-server create \
  --resource-group "${RG}" \
  --name "${PG}" \
  --location "${LOCATION}" \
  --tier Burstable \
  --sku-name Standard_B1ms \
  --storage-size 32 \
  --version 16 \
  --admin-user pjxadmin \
  --admin-password "${PG_ADMIN_PASSWORD}" \
  --public-access None \
  --yes

# Two databases, matching the two SQLite files being replaced in Phase 10.
az postgres flexible-server db create -g "${RG}" -s "${PG}" -d pjx_calendar
az postgres flexible-server db create -g "${RG}" -s "${PG}" -d pjx_identity

echo "${PG_ADMIN_PASSWORD}"   # capture this — step 5 puts it in Key Vault
```

`--public-access None` means the server has no public endpoint. You then need
either a private endpoint into the AKS VNet, or — simpler for a demo — allow
Azure services and restrict by firewall rule:

```bash
# Simpler demo option: reachable from Azure, plus your own IP for migrations.
az postgres flexible-server firewall-rule create -g "${RG}" -s "${PG}" \
  --rule-name allow-azure --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az postgres flexible-server firewall-rule create -g "${RG}" -s "${PG}" \
  --rule-name allow-me --start-ip-address "${MY_IP%/32}" --end-ip-address "${MY_IP%/32}"
```

> The `0.0.0.0` rule is Azure's "allow all Azure services" convention, not a
> literal open-to-internet rule — but it does permit *other Azure tenants'*
> resources to attempt connections. For a demo behind a strong generated
> password that is an accepted trade-off; for production use a private endpoint.
> Decide deliberately rather than by default.

You need the `allow-me` rule to run EF migrations from the devcontainer in
Phase 10.

---

## Step 4 — DNS

Register your domain at a registrar, then delegate it to Azure DNS:

```bash
az network dns zone create -g "${RG}" -n "${DNS_ZONE}"
az network dns zone show -g "${RG}" -n "${DNS_ZONE}" --query nameServers -o tsv
```

Set those four nameservers at your registrar. Delegation propagates in minutes
to a few hours. Verify before continuing — cert-manager's DNS-01 challenge fails
confusingly if delegation is incomplete:

```bash
dig +short NS "${DNS_ZONE}"      # → the Azure nameservers, not your registrar's
```

The A record for `${DEMO_HOST}` comes after step 6, once Traefik has a public IP.

---

## Step 5 — Key Vault

`local/scripts/azure/03-keyvault.sh`:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/00-vars.sh"

az keyvault create --resource-group "${RG}" --name "${KV}" \
  --location "${LOCATION}" --enable-rbac-authorization true

# Let yourself write secrets.
ME="$(az ad signed-in-user show --query id -o tsv)"
KV_ID="$(az keyvault show -g "${RG}" -n "${KV}" --query id -o tsv)"
az role assignment create --assignee "${ME}" \
  --role "Key Vault Secrets Officer" --scope "${KV_ID}"

# Federate the AKS workload identity so pods read secrets with no credentials.
CLIENT_ID="$(az aks show -g "${RG}" -n "${AKS}" \
  --query addonProfiles.azureKeyvaultSecretsProvider.identity.clientId -o tsv)"
az role assignment create --assignee "${CLIENT_ID}" \
  --role "Key Vault Secrets User" --scope "${KV_ID}"

echo "CSI driver client id: ${CLIENT_ID}"   # Phase 10's SecretProviderClass needs this
```

Seed the secrets. **Everything here is generated, never reused from the repo:**

```bash
# Postgres password from step 2
az keyvault secret set --vault-name "${KV}" --name pg-admin-password --value "<from step 2>"

# Connection strings
az keyvault secret set --vault-name "${KV}" --name calendar-connection-string \
  --value "Host=${PG}.postgres.database.azure.com;Database=pjx_calendar;Username=pjxadmin;Password=<pw>;SslMode=Require"
az keyvault secret set --vault-name "${KV}" --name identity-connection-string \
  --value "Host=${PG}.postgres.database.azure.com;Database=pjx_identity;Username=pjxadmin;Password=<pw>;SslMode=Require"

# Grafana Cloud OTLP credentials (from your Grafana Cloud stack's OTLP page)
az keyvault secret set --vault-name "${KV}" --name otlp-endpoint --value "<https://otlp-gateway-...>"
az keyvault secret set --vault-name "${KV}" --name otlp-headers  --value "Authorization=Basic <base64 instanceID:token>"
```

> The token signing certificate is **not** set here. Phase 10 generates a fresh
> one and loads it, because the certificate currently in the repo is public and
> must never reach a deployed environment. See
> [Phase 10](phase-10-deployable.md).

---

## Step 6 — Traefik, IP-restricted

`local/scripts/azure/04-ingress.sh`:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/00-vars.sh"

helm repo add traefik https://traefik.github.io/charts
helm repo update

helm upgrade --install traefik traefik/traefik \
  --namespace traefik --create-namespace \
  --set service.spec.loadBalancerSourceRanges="{${MY_IP}}" \
  --set 'ports.web.redirectTo.port=websecure' \
  --set ingressClass.enabled=true \
  --set ingressClass.isDefaultClass=true

kubectl -n traefik get svc traefik -w    # wait for EXTERNAL-IP
```

`loadBalancerSourceRanges` is the whole IP restriction — one setting, and it is
what keeps Phase 8 optional. To add collaborators, append their CIDRs.

Then point DNS at it:

```bash
INGRESS_IP="$(kubectl -n traefik get svc traefik \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}')"
az network dns record-set a add-record -g "${RG}" -z "${DNS_ZONE}" \
  -n "${DEMO_HOST%%.*}" -a "${INGRESS_IP}"
dig +short "${DEMO_HOST}"
```

> Your home IP almost certainly changes. When the demo becomes unreachable,
> re-run this step — that is the recurring cost of the restricted approach, and
> it is much cheaper than the alternative.

---

## Step 7 — cert-manager with DNS-01

`local/scripts/azure/05-certmanager.sh`:

```bash
#!/bin/bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/00-vars.sh"

helm repo add jetstack https://charts.jetstack.io && helm repo update
helm upgrade --install cert-manager jetstack/cert-manager \
  --namespace cert-manager --create-namespace \
  --set crds.enabled=true

# Managed identity for the DNS-01 solver, scoped to the DNS zone only.
az identity create -g "${RG}" -n id-certmanager
CM_CLIENT_ID="$(az identity show -g "${RG}" -n id-certmanager --query clientId -o tsv)"
CM_PRINCIPAL="$(az identity show -g "${RG}" -n id-certmanager --query principalId -o tsv)"
ZONE_ID="$(az network dns zone show -g "${RG}" -n "${DNS_ZONE}" --query id -o tsv)"

az role assignment create --assignee "${CM_PRINCIPAL}" \
  --role "DNS Zone Contributor" --scope "${ZONE_ID}"

OIDC_ISSUER="$(az aks show -g "${RG}" -n "${AKS}" --query oidcIssuerProfile.issuerUrl -o tsv)"
az identity federated-credential create \
  --name certmanager --identity-name id-certmanager -g "${RG}" \
  --issuer "${OIDC_ISSUER}" \
  --subject "system:serviceaccount:cert-manager:cert-manager" \
  --audiences api://AzureADTokenExchange

echo "cert-manager client id: ${CM_CLIENT_ID}"
```

Then the `ClusterIssuer` —
`local/scripts/azure/manifests/cluster-issuer.yaml`:

```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt
spec:
  acme:
    # Swap to acme-v02 (production) only after a staging cert issues cleanly.
    # Production has strict rate limits and a failed loop will exhaust them.
    server: https://acme-staging-v02.api.letsencrypt.org/directory
    email: ACME_EMAIL_PLACEHOLDER
    privateKeySecretRef:
      name: letsencrypt-account-key
    solvers:
      - dns01:
          azureDNS:
            resourceGroupName: RG_PLACEHOLDER
            subscriptionID: SUB_PLACEHOLDER
            hostedZoneName: DNS_ZONE_PLACEHOLDER
            environment: AzurePublicCloud
            managedIdentity:
              clientID: CM_CLIENT_ID_PLACEHOLDER
```

Start on **staging**. Let's Encrypt production allows 5 duplicate-certificate
failures per week, and a misconfigured DNS-01 solver burns through that fast.
Staging issues an untrusted cert, which is enough to prove the plumbing works.

---

## Verify

> Run these in the devcontainer (it carries `az`, `kubectl` and `helm` from
> Phase 6). Browser checks and anything on a published port are HOST-side. See
> [Where to run commands](README.md#where-to-run-commands).

```bash
source local/scripts/azure/00-vars.sh

# 1. Core resources
az aks show -g "${RG}" -n "${AKS}" --query '{state:powerState.code, k8s:kubernetesVersion}'
az acr show  -g "${RG}" -n "${ACR}" --query '{sku:sku.name, login:loginServer}'
az postgres flexible-server show -g "${RG}" -n "${PG}" --query '{state:state, version:version}'

# 2. ACR is attached — no imagePullSecret needed
az aks check-acr -g "${RG}" -n "${AKS}" --acr "${ACR}"

# 3. Controllers running
kubectl -n traefik get pods
kubectl -n cert-manager get pods
kubectl -n kube-system get pods -l app=secrets-store-csi-driver

# 4. DNS delegated and resolving to the ingress
dig +short NS "${DNS_ZONE}"
dig +short "${DEMO_HOST}"

# 5. The allowlist actually restricts. From your machine:
curl -s -o /dev/null -w '%{http_code}\n' "http://${DEMO_HOST}/"   # → 404 (Traefik, no route yet)
#    Then from anywhere else (phone on cellular, a cloud shell):
#    the same request must time out. If it answers, the allowlist is not applied.

# 6. Key Vault readable by you, and secrets present
az keyvault secret list --vault-name "${KV}" --query '[].name' -o tsv

# 7. A staging certificate issues end to end
kubectl apply -f local/scripts/azure/manifests/test-certificate.yaml
kubectl describe certificate test-cert    # → Ready=True within ~2 minutes
```

Check 5 is the one that matters. An allowlist you believe is applied but is not
is worse than no allowlist, because it silently invalidates the reasoning that
keeps Phase 8 optional. Test it from a genuinely different network.

Only switch the `ClusterIssuer` to `acme-v02` once check 7 passes on staging.

---

## Rollback

```bash
az group delete --name rg-pjx --yes --no-wait
```

Removes everything and stops all billing. Then remove the registrar's
nameserver delegation, and delete the branch.

---

## Follow-up, not in scope here

These scripts are imperative `az` CLI, which is the right weight for a demo and
matches the repo's bash-script style. If pjx ever becomes long-lived, port them
to **Bicep** or **Terraform** so the infrastructure is reviewable and
reproducible rather than a sequence someone ran once.
