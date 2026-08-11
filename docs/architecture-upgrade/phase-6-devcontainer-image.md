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

> ### ⚠️ Read this before the snippet below — most of it is already done
>
> **`.devcontainer/Dockerfile` already exists.** Phase 0 created it, because
> `universal:2-linux` had to go before Docker worked inside the container at all.
> This step is now **"extend the existing file"**, not "create it".
>
> The snippet below is the original full-file design, kept for reference. Do
> **not** paste it over what you have — it specifies
> `mcr.microsoft.com/vscode/devcontainers/dotnet:1-8.0-jammy`, and Phase 0
> deliberately moved to `mcr.microsoft.com/devcontainers/base:jammy`. Following it
> literally would undo that decision and reintroduce the defects Phase 0 fixed.
>
> What is actually left of this phase:
>
> | Concern | State |
> |---|---|
> | Base image | ✅ done in Phase 0 |
> | mkcert + libnss3-tools | ✅ done in Phase 2 |
> | Node, .NET, git, gh, docker | ✅ provided by devcontainer **features** in `devcontainer.json` |
> | `kubectl`, `helm`, `k9s`, `k3d` | ❌ **the only remaining work** |
>
> So Phase 6 reduces to appending one `RUN` block for the Kubernetes toolchain —
> see [Step 1a](#step-1a--append-the-kubernetes-toolchain).
>
> `CDE:` prefixes below mean paths inside the **CloudDevEnvironment** reference
> repo at `/home/mike/projects/CloudDevEnvironment` — see
> [Reference environment](README.md#reference-environment). Unprefixed paths are
> relative to pjx-root.

For reference, the original design — adapted from `CDE:.devcontainer/Dockerfile`:

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

---

## Step 1a — Append the Kubernetes toolchain

**This is the whole of Phase 6 in practice.** Append to the *existing*
`.devcontainer/Dockerfile`, after the `mkcert` block:

```dockerfile

# ================== KUBERNETES TOOLCHAIN =================
# Pinned deliberately: CDE resolves kubectl from dl.k8s.io/release/stable.txt,
# which silently changes version between builds. k3d's k3s node image decides the
# real cluster version anyway, so pinning kubectl costs nothing and makes the
# image reproducible.
#
# All four are plain binary downloads into /usr/local/bin. Unlike `dotnet tool
# install`, they depend on nothing provided by a devcontainer feature, so they are
# safe at image-build time — features are layered on only after this Dockerfile
# finishes.
ARG KUBECTL_VERSION=v1.36.3
ARG HELM_VERSION=v3.16.3
ARG K9S_VERSION=v0.32.7
ARG K3D_VERSION=v5.7.4

RUN curl -fsSL "https://dl.k8s.io/release/${KUBECTL_VERSION}/bin/linux/amd64/kubectl" \
        -o /usr/local/bin/kubectl \
    && curl -fsSL "https://get.helm.sh/helm-${HELM_VERSION}-linux-amd64.tar.gz" \
        | tar -xz -C /usr/local/bin --strip-components=1 linux-amd64/helm \
    && curl -fsSL "https://github.com/derailed/k9s/releases/download/${K9S_VERSION}/k9s_Linux_amd64.tar.gz" \
        | tar -xz -C /usr/local/bin k9s \
    && curl -fsSL "https://github.com/k3d-io/k3d/releases/download/${K3D_VERSION}/k3d-linux-amd64" \
        -o /usr/local/bin/k3d \
    && chmod +x /usr/local/bin/kubectl /usr/local/bin/helm /usr/local/bin/k9s /usr/local/bin/k3d
```

Three deliberate departures from `CDE:.devcontainer/Dockerfile:30-35`:

- **kubectl is pinned.** CDE curls `stable.txt`, so two builds a month apart get
  different versions.
- **`--strip-components=1` for helm**, instead of CDE's
  `cp /usr/local/bin/linux-amd64/helm /usr/local/bin/helm`, which leaves a stray
  `linux-amd64/` directory in `/usr/local/bin`.
- **k3d added.** CDE has no k3d — it targets a real cluster.
  [Phase 7b](phase-7b-local-k8s.md) needs it, and the version matches what that
  phase specifies.

Left out on purpose: `kubectx`/`kubens` (one cluster, little to switch between),
`azure-cli` (large, and not needed until [Phase 9](phase-9-azure-foundation.md)),
and CDE's Chrome/ChromeDriver block (test automation, not this phase).

### Verify

Rebuild the container — then **run `dev-up.sh -d`**. `shutdownAction: stopCompose`
stops all six containers on close and `runServices: ["workspace"]` restarts only
the workspace, so the five app services come back **stopped**. VS Code reports
success regardless, which makes this easy to miss.

```bash
for t in kubectl helm k9s k3d; do printf '%-8s ' $t; command -v $t >/dev/null && echo ok || echo MISSING; done
kubectl version --client --output=yaml | head -3
helm version --short
k3d version
```

```bash
dev-up.sh -d && sleep 60 && status.sh
```

> `kubectl version --client` is the only one that works before a cluster exists.
> Plain `kubectl version` tries to reach a server and appears to hang, which reads
> as a broken install rather than an absent cluster.

---

> ### ⚠️ `dotnet tool install` cannot run in this Dockerfile
>
> `dotnet` is provided by the `ghcr.io/devcontainers/features/dotnet` feature, and
> **features are layered on after the Dockerfile build finishes**. The command
> above fails at build time with:
>
> ```
> /bin/sh: 1: dotnet: not found
> exit code: 127
> ```
>
> This bit Phase 5 for real — the same line in `.devcontainer/Dockerfile` blocked
> every devcontainer rebuild until it was moved to `setup.sh`, which runs as
> `postCreateCommand` and therefore after the features exist. Keep it there:
>
> ```bash
> if ! dotnet tool list --global 2>/dev/null | grep -q 'dotnet-ef'; then
>     dotnet tool install --global dotnet-ef --version 8.0.11
> fi
> ```
>
> The guard is required — a bare `install` exits non-zero when the tool is already
> present, and `set -e` would fail the whole setup script on every rebuild.
>
> The same trap applies to anything else in this phase that depends on a
> feature-provided binary. `kubectl`, `helm`, `k3d` and `k9s` are safe: they are
> installed by `curl` into `/usr/local/bin` and depend on nothing from a feature.

> ### `dotnet-ef` 8.x will not work until EF Core is upgraded
>
> The tool's major version must match the project's EF Core version, and
> [`Pjx_Api` and `Pjx.CalendarLibrary` are still on EF Core 3.1.7](phase-4-dotnet8.md#outstanding--ef-core-was-not-upgraded)
> despite targeting `net8.0`. `dotnet ef migrations add` and `dotnet ef database
> update` will fail against them.
>
> Nothing in Phases 6, 7, or 7b needs migrations, so this is not urgent — but do
> not spend time debugging the tool. Either pin `dotnet-ef` to `3.1.*` to match
> what the projects actually use, or leave it at 8.x and accept that migrations
> wait for [Phase 10 Step 0](phase-10-deployable.md#step-2--sqlite--postgresql),
> which is where the EF Core upgrade belongs.

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

## Step 2 — Keep the compose-based devcontainer

> ### 🛑 An earlier draft of this step was wrong — do not restructure
>
> It said to replace `dockerComposeFile`/`service` with `build`, delete the
> `workspace` compose service, and add the `docker-in-docker` feature. **That
> would undo most of Phases 0–3.** Specifically it would:
>
> | Lose | Consequence |
> |---|---|
> | `extra_hosts` on `workspace` | `pjx.test` and friends stop resolving in the devcontainer |
> | `containerEnv: HOST_PROJECT_PATH` | every compose bind mount silently mounts an empty directory again |
> | `pjx-network` membership | `status.sh`'s service-name health URLs all go red |
> | docker-**outside**-of-docker | reintroduces the nested `dockerd` that hijacks `/var/run/docker.sock` — Phase 0 defect #8, which took three attempts to diagnose |
>
> The reasoning was "CloudDevEnvironment does it that way", but CDE uses
> docker-in-docker deliberately: it runs three isolated submodule stacks that
> each want ports 80/443. pjx needs the browser to reach its containers, which is
> exactly why Phase 0 chose docker-outside-of-docker.

**Keep `dockerComposeFile` and `service: workspace` exactly as they are.**

> ### 🛑 Do NOT remove the `node` or `dotnet` features
>
> An earlier draft said to drop them because "the Dockerfile now provides" those
> tools. **It does not.** That draft assumed the
> `vscode/devcontainers/dotnet:1-8.0-jammy` base plus a hand-rolled `nvm` install —
> neither of which exists, because Phase 0 moved to
> `mcr.microsoft.com/devcontainers/base:jammy` and
> [Step 1a](#step-1a--append-the-kubernetes-toolchain) adds only the Kubernetes
> binaries.
>
> `base:jammy` ships neither Node nor .NET. Removing these features loses `npm`,
> `ts-node`, `dotnet`, `dotnet-ef`, and every `npm install` / `dotnet restore` in
> `setup.sh` — the same `dotnet: not found` that blocked a devcontainer rebuild
> during Phase 5, except permanent.
>
> Keep all five features exactly as they are:
>
> ```jsonc
> "features": {
>     "ghcr.io/devcontainers/features/git:1": {},
>     "ghcr.io/devcontainers/features/github-cli:1": {},
>     "ghcr.io/devcontainers/features/docker-outside-of-docker:1": {},
>     "ghcr.io/devcontainers/features/node:1":   { "version": "18" },
>     "ghcr.io/devcontainers/features/dotnet:1": { "version": "8.0" }
> },
> ```
>
> Hand-rolling Node and .NET into the image would only buy version pinning, which
> the features already provide via their `version` options. It is not worth a
> Dockerfile that can break at build time.

**So `devcontainer.json` needs no changes in this phase.** Both things this step
originally asked for are already in place:

| Item | State |
|---|---|
| `ms-kubernetes-tools.vscode-kubernetes-tools` extension | ✅ already listed |
| All five features | ✅ keep as-is |

Optionally add `hostRequirements` as documentation of what the stack needs
(CloudDevEnvironment asks for 8 CPU / 32GB; pjx is far lighter):

```jsonc
"hostRequirements": { "cpus": 4, "memory": "8gb" }
```

Leave untouched: `runServices`, `remoteEnv`, `workspaceFolder`,
`remoteUser: vscode`, `forwardPorts`, `otherPortsAttributes`,
`postCreateCommand`, `postStartCommand`, and everything in
`docker-compose.devcontainer.yml`.

> ### Where each tool comes from
>
> Worth internalising, because "which layer owns this tool?" is what both wrong
> drafts of this phase got confused about — and it decides whether an install can
> run at image-build time:
>
> | Tool | Provided by | Available during Dockerfile build? |
> |---|---|---|
> | `git`, `gh`, `docker` | features | ❌ no |
> | `node`, `npm`, `ts-node` | `node` feature | ❌ no |
> | `dotnet` | `dotnet` feature | ❌ no |
> | `dotnet-ef` | `setup.sh` (`postCreateCommand`) | ❌ no — needs `dotnet` |
> | `mkcert` | Dockerfile (`curl`) | ✅ yes |
> | `kubectl`, `helm`, `k9s`, `k3d` | Dockerfile (`curl`) | ✅ yes |
>
> **Features are layered on after the Dockerfile finishes.** Anything in the
> Dockerfile that invokes a feature-provided binary fails with exit 127. That is
> the whole reason `dotnet-ef` lives in `setup.sh`.

---

## Step 3 — The Node version constraint

**Do not casually bump Node to 20.**

> The version lives in `devcontainer.json`, not the Dockerfile — there is no
> `NODE_VERSION` build arg to find:
>
> ```jsonc
> "ghcr.io/devcontainers/features/node:1": { "version": "18" },
> ```

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

### `validate.sh build pjx-web-react` fails on Node 18 — fix it in the script

```
Error: error:0308010C:digital envelope routines::unsupported
  code: 'ERR_OSSL_EVP_UNSUPPORTED'
```

Only `start` carries the flag; `build` does not:

```json
"start": "GENERATE_SOURCEMAP=false react-scripts --openssl-legacy-provider start",
"build": "react-scripts build",
```

**Do NOT add the flag to the `build` script.** That is the obvious fix and it
breaks deployment:

| Base | OpenSSL | `npm run build` | accepts `--openssl-legacy-provider` |
|---|---|---|---|
| `node:14.5.0-slim` — production `Dockerfile` | 1.1.1g | ✅ works as-is | ❌ **`node: bad option`** |
| `node:18-alpine` — `Dockerfile.dev`, devcontainer | 3.0.16 | ❌ needs the flag | ✅ |

The production image builds on Node 14, whose OpenSSL still permits MD4, so
`build` never needed the flag. Adding it there makes Node 14 refuse to start.

Set it in `local/scripts/validate.sh` instead, so only the devcontainer sees it:

```bash
# react-scripts 3.4.3 uses webpack 4, whose MD4 hash OpenSSL 3 removed. Node 18
# here needs --openssl-legacy-provider; the production Dockerfile is on Node 14
# (OpenSSL 1.1.1g), which works without it and REJECTS it as "bad option" — so
# this must not move into package.json's build script.
build) (cd "${dir}" && NODE_OPTIONS=--openssl-legacy-provider npm run build --if-present) || FAILED+=("${target}") ;;
```

Harmless for the other two Node projects — neither uses webpack.

> ### This is pre-existing, not a Phase 6 regression
>
> `build` only ever ran on Node 14 before, where it worked. Nothing is blocked
> today: the production image still builds, and
> [Phase 7b](phase-7b-local-k8s.md) imports the **dev** image (`Dockerfile.dev` →
> `npm start`, which has the flag). Only the canary check itself was broken.
>
> The real problem it reveals is that the production `Dockerfile` pins
> **`node:14.5.0-slim`, EOL since April 2023** — and it cannot move to Node 18
> while `react-scripts` is on 3.4.3. See the
> [deferred-work table](README.md#deferred-work); it pairs with
> [Phase 10's React runtime configuration](phase-10-deployable.md#step-3--react-runtime-configuration),
> which is blocked by the same dependency.

The other two Node services (`restify`, Apollo Server) have no such constraint.
If you want Node 20 for them specifically, use per-service `Dockerfile.dev` base
images rather than changing the devcontainer default.

**Recommendation: keep 18.** The devcontainer Node version only affects tooling
run from the terminal; each service builds against its own `Dockerfile.dev`.

---

## Step 4 — Do not create a cluster here

> ### 🛑 This step is superseded by [Phase 7b](phase-7b-local-k8s.md)
>
> An earlier draft created a k3d cluster here with:
>
> ```bash
> k3d cluster create pjx --port "8080:80@loadbalancer"
> ```
>
> Three things wrong with that now:
>
> | Problem | Why |
> |---|---|
> | "inside a docker-in-docker devcontainer" | pjx uses docker-**outside**-of-docker. k3d creates its nodes on the **host** daemon through the mounted socket. |
> | Unpinned `install.sh` | [Step 1a](#step-1a--append-the-kubernetes-toolchain) already installs k3d pinned to `v5.7.4`. Running the script again would silently replace it with whatever is current. |
> | `--port "8080:80@loadbalancer"` | [Phase 7b](phase-7b-local-k8s.md#why-not-just-map-k3d-to-80808443-and-run-both) maps **80/443** deliberately. React bakes `REACT_APP_*` in at build time, so on `:8443` the SPA loads but every API call targets the wrong port. |
>
> Cluster creation, TLS from the existing mkcert certificate, image import, probes,
> and the port-conflict handling all live in Phase 7b, which also depends on
> [Phase 7](phase-7-cicd.md) having made the chart deployable. The pre-cleanup
> chart cannot deploy anywhere, so `helm install` here would only produce failures
> Phase 7 is already scheduled to fix.

Phase 6 ends with the toolchain installed and nothing deployed. A useful sanity
check that needs no cluster:

```bash
helm lint helm-pjx/
helm template pjx-release helm-pjx/ > /dev/null && echo "templates render"
```

Expect complaints — hardcoded image tags, `NodePort` services, `*.pjx.com`
ingress hosts. That is [Phase 7](phase-7-cicd.md)'s work. Seeing them now just
confirms `helm` itself works.

---

## Verify

> Run these in the devcontainer unless a command is marked HOST. See
> [Where to run commands](README.md#where-to-run-commands) — `localhost` means
> something different in each shell.

```bash
# Rebuild the container first: Ctrl+Shift+P → Dev Containers: Rebuild Container

# 1. Toolchain present and pinned
kubectl version --client     # → v1.36.3 (see the az overwrite warning below)
helm version                 # → v3.16.3
k9s version                  # → v0.32.7
k3d version                  # → v5.7.4, and the k3s node image it defaults to
az version --query '"azure-cli"' -o tsv      # only if you added it early

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

> ### `kubectx` is not installed — that is deliberate
>
> [Step 1a](#step-1a--append-the-kubernetes-toolchain) skips `kubectx`/`kubens`
> (and CDE's Chrome/ChromeDriver). `kubectx: command not found` is the expected
> result, not a failed install. There is one local cluster to switch between.
> `kubectl config use-context` covers it.

> ### ⚠️ `az aks install-cli` silently un-pins kubectl
>
> If you add `azure-cli` — needed from [Phase 9](phase-9-azure-foundation.md), and
> reasonable to add early — note that `az aks install-cli` writes
> `/usr/local/bin/kubectl` at **latest** by default, overwriting
> `KUBECTL_VERSION`:
>
> ```
> --install-location : Default: /usr/local/bin/kubectl
> ```
>
> The failure is invisible: while `latest` happens to equal your pin, everything
> looks correct. Months later a rebuild installs the pin and then replaces it, and
> `ARG KUBECTL_VERSION` has become decoration.
>
> **Put the Azure block BEFORE the Kubernetes toolchain block** so the pinned
> binaries land last and win:
>
> ```dockerfile
> RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash && az aks install-cli
>
> # ================== KUBERNETES TOOLCHAIN =================
> ARG KUBECTL_VERSION=v1.36.3
> ...
> ```
>
> Verify which one won:
>
> ```bash
> kubectl version --client   # must match KUBECTL_VERSION
> ```
>
> `kubelogin` staying at latest is fine — it is an auth helper, and its version
> does not affect cluster API compatibility. `sudo` is also unnecessary in that
> `RUN`; the build already runs as root.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-6-devcontainer-image
```

Rebuild the container to return to `universal:2-linux`.
