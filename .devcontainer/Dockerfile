# Phase 6's base swap, brought forward: universal:2-linux bakes in
# docker-in-docker (which hijacks /var/run/docker.sock and defeats the
# host-socket mount), uses a 'codespace' user, ships expired apt keys, and
# weighs 15.9GB. This base has none of those problems and is ~1GB.
FROM mcr.microsoft.com/devcontainers/base:jammy
  