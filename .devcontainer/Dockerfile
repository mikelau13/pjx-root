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

# Trust the repo's mkcert CA. Runtime `mkcert -install` is lost on every
# rebuild; this copies the committed CA into the system trust store at build
# time. The CA cert is public — only its private key is gitignored.
COPY --chown=root:root local/central-router/config/ca/rootCA.pem \
    /usr/local/share/ca-certificates/pjx-mkcert-ca.crt
RUN update-ca-certificates