# Phase 6's base swap, brought forward: universal:2-linux bakes in
# docker-in-docker (which hijacks /var/run/docker.sock and defeats the
# host-socket mount), uses a 'codespace' user, ships expired apt keys, and
# weighs 15.9GB. This base has none of those problems and is ~1GB.
FROM mcr.microsoft.com/devcontainers/base:jammy

# mkcert for local TLS (Phase 2). Installed in the image, not at runtime —
# anything installed by hand is lost on the next devcontainer rebuild.
# libnss3-tools provides certutil, which `mkcert -install` needs to write into
# the container's browser/NSS trust store.
ARG MKCERT_VERSION=v1.4.4
RUN apt-get update && apt-get install -y --no-install-recommends libnss3-tools \
    && rm -rf /var/lib/apt/lists/* \
    && curl -fsSL "https://github.com/FiloSottile/mkcert/releases/download/${MKCERT_VERSION}/mkcert-${MKCERT_VERSION}-linux-amd64" \
        -o /usr/local/bin/mkcert \
    && chmod +x /usr/local/bin/mkcert


# Azure CLI + kubelogin + kubectl. Required from Phase 9 onward.
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash && az aks install-cli

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

RUN curl -fsSL "https://dl.k8s.io/release/${KUBECTL_VERSION}/bin/linux/amd64/kubectl" -o /usr/local/bin/kubectl \
    && curl -fsSL "https://get.helm.sh/helm-${HELM_VERSION}-linux-amd64.tar.gz" | tar -xz -C /usr/local/bin --strip-components=1 linux-amd64/helm \
    && curl -fsSL "https://github.com/derailed/k9s/releases/download/${K9S_VERSION}/k9s_Linux_amd64.tar.gz" | tar -xz -C /usr/local/bin k9s \
    && curl -fsSL "https://github.com/k3d-io/k3d/releases/download/${K3D_VERSION}/k3d-linux-amd64" -o /usr/local/bin/k3d \
    && chmod +x /usr/local/bin/kubectl /usr/local/bin/helm /usr/local/bin/k9s /usr/local/bin/k3d

