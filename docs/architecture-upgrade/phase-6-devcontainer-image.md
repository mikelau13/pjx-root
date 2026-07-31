# Phase 6 — Custom devcontainer image with the Kubernetes/Helm toolchain

**Goal:** replace the prebuilt `universal:2-linux` image with a purpose-built
Dockerfile carrying `kubectl`, `kubectx`/`kubens`, `k9s`, and `helm` — the
toolchain that makes `helm-pjx/` and `kubernetes/` actually usable from inside
the container.

**Risk:** Low. **Reversible:** yes.

**Depends on:** Phase 4 (the image pins the .NET SDK version).

> ## The base swap already happened, in Phase 0
>
> `universal:2-linux` had to go earlier than planned. It bakes in
> docker-in-docker, whose nested `dockerd` claims `/var/run/docker.sock` and
> defeats the host-socket mount entirely — so no feature configuration could make
> Docker work inside the container. It also shipped an expired Yarn apt key that
> broke every feature install, used a `codespace` user instead of `vscode`, and
> weighed 15.9 GB.
>
> `.devcontainer/Dockerfile` therefore already exists:
>
> ```dockerfile
> FROM mcr.microsoft.com/devcontainers/base:jammy
> ```
>
> **Already done, so skip:** Step 2's move from `image:` to `build:` (compose
> already builds from that Dockerfile), `remoteUser: vscode`, and the `dotnet:2`
> feature at `8.0`. Step 3's Node question was also settled — Node 18 via the
> feature, deliberately not 20.
>
> **What remains:** replacing `base:jammy` + features with the pinned,
> purpose-built Dockerfile below — the Kubernetes/Helm toolchain, `azure-cli`, and
> explicit tool versions. Reconsider only whether hand-rolling the Node and .NET
> installs is worth it when the features already work; the argument for doing so is
> version pinning, not capability.

```bash
git checkout -b feature/arch-phase-6-devcontainer-image
```

---

## The gap this closes

pjx-root already has `helm-pjx/` (10 templates) and `kubernetes/` (10
manifests). What it does not have is any way to use them — no `kubectl`, no
`helm` in the container. `README.md:110` documents `helm install pjx-release
helm-pjx/` as if it works.

CloudDevEnvironment is the mirror image: **no charts at all**, but the full
toolchain baked into `CDE:.devcontainer/Dockerfile:30-35`. So this phase copies
tooling, not manifests.

Chart cleanup itself is [Phase 7](phase-7-cicd.md).

---

## Why leave `universal:2-linux`

| | `universal:2-linux` | Custom Dockerfile |
|---|---|---|
| Size | ~10GB+ (every language) | ~2-3GB |
| Startup | Slow | Faster |
| Tool versions | Whatever ships | Pinned explicitly |
| k8s tooling | None | Baked in |
| Reproducibility | Drifts with upstream | Fixed until you change it |

The pinning is the real argument. Feature-based installs move under you; a
Dockerfile with explicit versions does not.

---

## Step 1 — The Dockerfile

Create `.devcontainer/Dockerfile`, adapted from
`CDE:.devcontainer/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/vscode/devcontainers/dotnet:1-8.0-jammy

SHELL ["/bin/bash", "--login", "-c"]

ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

# ================== SYSTEM PACKAGES =================
RUN apt-get update && apt-get -y install --no-install-recommends \
      git vim ssh sudo curl wget unzip jq \
    && rm -rf /var/lib/apt/lists/*

# ================== KUBERNETES TOOLCHAIN =================
# Versions pinned deliberately — see the table in this phase doc.
ARG KUBECTL_VERSION=v1.29.3
ARG HELM_VERSION=v3.14.2
ARG KUBECTX_VERSION=v0.9.5
ARG K9S_VERSION=v0.31.9

RUN curl -LO "https://dl.k8s.io/release/${KUBECTL_VERSION}/bin/linux/amd64/kubectl" && \
    install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl && \
    rm kubectl && \
    curl -L "https://github.com/ahmetb/kubectx/releases/download/${KUBECTX_VERSION}/kubectx_${KUBECTX_VERSION}_linux_x86_64.tar.gz" \
      | tar -xz -C /usr/local/bin && \
    curl -L "https://github.com/ahmetb/kubectx/releases/download/${KUBECTX_VERSION}/kubens_${KUBECTX_VERSION}_linux_x86_64.tar.gz" \
      | tar -xz -C /usr/local/bin && \
    curl -L "https://github.com/derailed/k9s/releases/download/${K9S_VERSION}/k9s_Linux_amd64.tar.gz" \
      | tar -xz -C /usr/local/bin && \
    curl -L "https://get.helm.sh/helm-${HELM_VERSION}-linux-amd64.tar.gz" | tar -xz -C /tmp && \
    install -o root -g root -m 0755 /tmp/linux-amd64/helm /usr/local/bin/helm && \
    rm -rf /tmp/linux-amd64

# ================== USER SETUP =================
RUN echo "${USERNAME} ALL=(root) NOPASSWD:ALL" > /etc/sudoers.d/${USERNAME} && \
    chmod 0440 /etc/sudoers.d/${USERNAME}

USER ${USERNAME}
WORKDIR /home/${USERNAME}

# ================== NODE =================
# See the Create React App constraint in this phase doc before changing this.
ARG NODE_VERSION=18
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash && \
    source ~/.nvm/nvm.sh && \
    nvm install ${NODE_VERSION} && \
    nvm alias default ${NODE_VERSION} && \
    npm install -g nodemon ts-node typescript

# ================== DOTNET TOOLS =================
ENV PATH=$PATH:/home/${USERNAME}/.dotnet/tools
RUN dotnet tool install --global dotnet-ef --version 8.0.2

WORKDIR /workspaces/pjx-root
```

### One SDK, deliberately

`pjx-sso-identityserver` stays on `netcoreapp3.1` (Decision D2), but this image
installs **only** the 8.0 SDK. The .NET 8 SDK builds `netcoreapp3.1` targets by
acquiring the targeting pack as a reference assembly — it emits warning
NETSDK1138 ("target framework out of support"), which is accurate and worth
leaving visible.

Running SSO locally uses the 3.1 runtime inside its own container image, so the
devcontainer never needs it. Phase 4 step 1 verifies this; if that check failed
there, SSO is already excluded from `validate.sh` and built via Docker, and this
image still needs no second SDK.

Resist adding the 3.1 SDK here. It would be the only reason this image carries
two runtimes, and it disappears entirely at Phase 8.

### Azure CLI is required — not optional

Decision D5 targets AKS, so `azure-cli` and `kubelogin` belong in this image.
Earlier drafts of this phase dropped them as "AwareMD-specific", which was wrong.
Add:

```dockerfile
# Azure CLI + kubelogin. Required from Phase 9 onward.
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash && \
    az aks install-cli
```

`az aks install-cli` also installs `kubectl` and `kubelogin`; the pinned
`kubectl` above still wins on PATH order, which is what you want for a
reproducible version.

CloudDevEnvironment additionally installs Bruno, CSharpier, Chrome +
ChromeDriver, `openapi-generator`, `yq`, and dev-tunnels. Those *are*
AwareMD-specific — leave them out. Add `google-chrome-stable` + `chromedriver`
(`CDE:.devcontainer/Dockerfile:43-54`) only if `projects/pjx-test-automation`
needs a browser.

---

## Step 2 — Rewire `devcontainer.json`

Replace the `dockerComposeFile` / `service` keys with `build`, and drop the
features now baked into the image:

```jsonc
{
  "name": "PJX Root - Multi-service Development",
  "build": { "dockerfile": "Dockerfile" },
  "workspaceFolder": "/workspaces/pjx-root",
  "features": {
    "ghcr.io/devcontainers/features/git:1": {},
    "ghcr.io/devcontainers/features/github-cli:1": {},
    "ghcr.io/devcontainers/features/docker-in-docker:2": {}
  },
  // ... runArgs, forwardPorts, portsAttributes, customizations from Phase 2
}
```

Removed: the `node` and `dotnet` features (now in the Dockerfile).
Kept: `docker-in-docker` (needed to run the app stack), `git`, `github-cli`.

> Moving from `dockerComposeFile` to `build` means the devcontainer no longer
> participates in the app compose stack. That is intentional and matches
> CloudDevEnvironment, which uses `"dockerFile"` and starts stacks separately via
> scripts. The `workspace` service in `docker-compose.devcontainer.yml` becomes
> dead — delete it, along with the `DOCKER_HOST` override, since
> docker-in-docker handles the socket now.

Add the k8s VS Code extension if it is not already listed:

```jsonc
"ms-kubernetes-tools.vscode-kubernetes-tools"
```

Also add `hostRequirements` as documentation of what the stack needs
(CloudDevEnvironment asks for 8 CPU / 32GB; pjx is far lighter):

```jsonc
"hostRequirements": { "cpus": 4, "memory": "8gb" }
```

---

## Step 3 — The Node version constraint

**Do not casually bump `NODE_VERSION` to 20.**

`projects/pjx-web-react` uses `react-scripts` 3.4.3, whose `start` script is
already `react-scripts --openssl-legacy-provider start` — a workaround for
webpack 4's use of an MD4 hash that OpenSSL 3 removed. That flag is doing real
work today.

Node 20 tightens things further, and react-scripts 3.x is four majors behind. If
you bump Node:

1. Bump it, rebuild, and run `validate.sh build pjx-web-react` **before** doing
   anything else.
2. If it fails, either stay on 18 or upgrade `react-scripts` — which is its own
   project, out of scope here.

The other two Node services (`restify`, Apollo Server) have no such constraint.
If you want Node 20 for them specifically, use per-service `Dockerfile.dev` base
images rather than changing the devcontainer default.

**Recommendation: keep 18.** The devcontainer Node version only affects tooling
run from the terminal; each service builds against its own `Dockerfile.dev`.

---

## Step 4 — Local Kubernetes (optional)

To actually apply `helm-pjx/`, you need a cluster. Lightest option inside a
docker-in-docker devcontainer is `k3d`:

```dockerfile
RUN curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
```

```bash
k3d cluster create pjx --port "8080:80@loadbalancer"
kubectl cluster-info
helm install pjx-release helm-pjx/ --dry-run --debug   # validate templates first
```

Chart problems will surface immediately — hardcoded image tags, `NodePort`
services, `*.pjx.com` ingress hosts. That is Phase 7's work; a `--dry-run` here
just confirms the templates render.

**This step is optional.** Skip if you only want the toolchain for interacting
with a remote cluster.

---

## Verify

```bash
# Rebuild the container first: Ctrl+Shift+P → Dev Containers: Rebuild Container

# 1. Toolchain present and pinned
kubectl version --client
helm version
k9s version
kubectx --help > /dev/null && echo "kubectx ok"
az version --query '"azure-cli"' -o tsv      # required from Phase 9

# 2. Runtimes still correct
dotnet --list-sdks     # → 8.0.x ONLY (no 3.1 — see "One SDK, deliberately")
node --version         # → v18.x
dotnet ef --version

# 2b. The 3.1 project still builds under the 8.0 SDK (warning NETSDK1138 is fine)
(cd projects/pjx-sso-identityserver && dotnet build 2>&1 | tail -5)
# If this errors rather than warns, build it via Docker instead:
#   docker compose -f docker-compose.devcontainer.yml build pjx-sso-identityserver

# 3. Image is meaningfully smaller than universal:2-linux
docker images --format '{{.Repository}}:{{.Tag}}\t{{.Size}}' | grep -i pjx

# 4. Nothing regressed — the app stack still comes up
dev-up.sh -b -d && status.sh && obs-up.sh

# 5. React still builds (the Node-version canary)
validate.sh build pjx-web-react

# 6. Helm templates render
helm template pjx-release helm-pjx/ > /dev/null && echo "templates render"
```

Check 5 is the one that catches the mistake this phase is most likely to
introduce.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-6-devcontainer-image
```

Rebuild the container to return to `universal:2-linux`.
