# Phase 2 — Traefik reverse proxy, TLS, and real hostnames

**Goal:** replace `localhost:3000` / `localhost:4000` / `localhost:6001` with
`pjx.localhost`, `ql.pjx.localhost`, `api.pjx.localhost` behind a single Traefik
instance on ports 80/443, with a locally-trusted TLS certificate.

**Risk: Medium — the highest of Phases 0–3.** The OIDC redirect URIs move, and
OpenID Connect is strict about exact-match URIs. Budget time for step 5.

**Reversible:** yes, but see the note about `projects/pjx-web-react/.env` under
Rollback — that file is gitignored, so `git revert` will not restore it.

**Depends on:** Phase 1.

```bash
git checkout -b feature/arch-phase-2-traefik
```

---

## Before you start: two hard constraints

**1. CloudDevEnvironment cannot be running.** Its `central-router` claims host
ports 80, 443, and 9090 — exactly what pjx's Traefik will claim. Stop it first:

```bash
docker ps --filter name=central-router --format '{{.Names}}'
# if anything is listed:
(cd /home/mike/projects/CloudDevEnvironment && docker compose -f local/docker-compose.yml down)
```

**2. There is a config discrepancy to clean up as you go.**
`docker-compose.devcontainer.yml:81-83` sets `REACT_APP_GRAPHQL_URI`,
`REACT_APP_API_URI`, and `REACT_APP_SSO_URI`. **Nothing reads those names.** The
React app actually reads, from `projects/pjx-web-react/.env` (gitignored):

| Variable | Current value |
|---|---|
| `REACT_APP_GRAPHQL_ENDPOINT` | `http://localhost:4000` |
| `REACT_APP_SSO_ISSUER_URL` | `http://localhost:5001` |
| `REACT_APP_SSO_CLIENT_ID` | `pjx-web-react` |
| `REACT_APP_SSO_REDIRECT_URL` | `http://localhost:3000/signin-oidc` |
| `REACT_APP_PUBLIC_URL` | `http://localhost:3000` |
| `REACT_APP_LOGOFF_REDIRECT_URL` | `http://localhost:3000/logout/callback` |
| `REACT_APP_SILENT_REDIRECT_URL` | `http://localhost:3000/silentrenew` |
| `REACT_APP_API_DOTNET_URL` | `http://localhost:6001` |

Consumed by `src/utils/authConst.tsx`, `src/apollo/apolloClient.tsx`,
`src/services/countryService.tsx`, `src/services/calendarService.tsx`.

Delete the three dead compose variables in step 6 rather than carrying them
forward.

---

## Hostname map

| Service | Hostname | Container port |
|---|---|---|
| pjx-web-react | `pjx.localhost` | 3000 |
| pjx-graphql-apollo | `ql.pjx.localhost` | 4000 |
| pjx-api-dotnet | `api.pjx.localhost` | 80 |
| pjx-api-node | `node.pjx.localhost` | 8081 |
| pjx-sso-identityserver | `sso.pjx.localhost` | 80 |
| Grafana (Phase 3) | `grafana.pjx.localhost` | 3000 |
| Traefik dashboard | `localhost:9090` | 8080 |

`.localhost` resolves to `127.0.0.1` automatically in Chrome and under
systemd-resolved, so no `/etc/hosts` editing is needed on the host. The
devcontainer needs explicit entries — step 4.

---

## Step 1 — Traefik, HTTP only

Do not add TLS yet. Get routing working first; TLS is step 3.

Create `local/central-router/config/.gitkeep` and
`local/docker-compose.yml`:

```yaml
name: pjx-router

services:
  traefik:
    image: traefik:3.6
    command: >
      --providers.docker=true
      '--providers.docker.constraints=Label(`traefik.constraint-label`, `pjx-public`)'
      --providers.file.directory=/traefik/config/
      --providers.file.watch=true
      --entrypoints.http.address=:80
      --entrypoints.http.forwardedHeaders.insecure=true
      --entrypoints.https.address=:443
      --entrypoints.https.forwardedHeaders.insecure=true
      --api.insecure=true
      --accesslog
      --log
    ports:
      - "80:80"
      - "443:443"
      - "9090:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - ./central-router/config:/traefik/config/
    networks:
      - pjx-network
    extra_hosts:
      - "host.docker.internal:host-gateway"

networks:
  pjx-network:
    external: true
    name: pjx-network
```

Two providers are enabled deliberately, and this is where we diverge from the
reference architecture:

- **docker provider** does the routing, via labels on each service. This is how
  `CDE:AwareServices/docker-compose.yml` routes within a stack.
- **file provider** carries only the TLS store (step 3). This is how
  `CDE:local/central-router/config/services.yml` handles certificates.

CloudDevEnvironment splits these across *two* Traefik tiers because it has three
independently-started submodule stacks. pjx is one stack, so one Traefik using
both providers is the same capability with half the moving parts. See the
"Deliberate divergence" section in [README.md](README.md).

The `constraint-label` matters: without it Traefik would try to route the
`workspace` devcontainer service too.

> `pjx-network` is declared `external` here, so it must already exist. It is
> created by `docker-compose.devcontainer.yml`. Start the app stack before the
> router, or run `docker network create pjx-network` once.

---

## Step 2 — Label the services

In `docker-compose.devcontainer.yml`, add labels to each of the five app
services. Leave the existing `ports:` blocks alone for now — running both paths
in parallel means a broken label does not cost you a working environment.

```yaml
  pjx-web-react:
    labels:
      - "traefik.enable=true"
      - "traefik.constraint-label=pjx-public"
      - "traefik.http.routers.pjx-web.rule=Host(`pjx.localhost`)"
      - "traefik.http.services.pjx-web.loadbalancer.server.port=3000"

  pjx-graphql-apollo:
    labels:
      - "traefik.enable=true"
      - "traefik.constraint-label=pjx-public"
      - "traefik.http.routers.pjx-ql.rule=Host(`ql.pjx.localhost`)"
      - "traefik.http.services.pjx-ql.loadbalancer.server.port=4000"

  pjx-api-dotnet:
    labels:
      - "traefik.enable=true"
      - "traefik.constraint-label=pjx-public"
      - "traefik.http.routers.pjx-api.rule=Host(`api.pjx.localhost`)"
      - "traefik.http.services.pjx-api.loadbalancer.server.port=80"

  pjx-api-node:
    labels:
      - "traefik.enable=true"
      - "traefik.constraint-label=pjx-public"
      - "traefik.http.routers.pjx-node.rule=Host(`node.pjx.localhost`)"
      - "traefik.http.services.pjx-node.loadbalancer.server.port=8081"

  pjx-sso-identityserver:
    labels:
      - "traefik.enable=true"
      - "traefik.constraint-label=pjx-public"
      - "traefik.http.routers.pjx-sso.rule=Host(`sso.pjx.localhost`)"
      - "traefik.http.services.pjx-sso.loadbalancer.server.port=80"
```

Also give the network a stable name so the router's `external` reference
resolves:

```yaml
networks:
  pjx-network:
    driver: bridge
    name: pjx-network      # add this line
```

### Create React App behind a proxy

`react-scripts` 3.4.3 uses webpack-dev-server 3, whose host check rejects any
`Host` header that is not `localhost`, and whose HMR websocket tries to connect
back to the origin port. Add to the `pjx-web-react` environment:

```yaml
    environment:
      - CHOKIDAR_USEPOLLING=true
      - DANGEROUSLY_DISABLE_HOST_CHECK=true
      - WDS_SOCKET_HOST=pjx.localhost
      - WDS_SOCKET_PORT=80
```

Without `DANGEROUSLY_DISABLE_HOST_CHECK` you get "Invalid Host header" instead
of the app. Without the `WDS_SOCKET_*` pair the page loads but hot reload
silently stops working. Both are dev-server-only settings and never reach a
production build.

> `WDS_SOCKET_PORT` becomes `443` in step 3.

### Verify step 2 before continuing

```bash
docker network create pjx-network 2>/dev/null || true
dev-up.sh -d
docker compose -f local/docker-compose.yml up -d

curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: pjx.localhost'      http://localhost/
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: ql.pjx.localhost'   http://localhost/
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: api.pjx.localhost'  http://localhost/swagger
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: node.pjx.localhost' http://localhost/
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: sso.pjx.localhost'  http://localhost/
```

All five should return 2xx or 3xx. If any returns 404, the router did not pick up
that label — check `http://localhost:9090/dashboard/` and fix before proceeding.
Do not stack TLS on top of broken routing.

---

## Step 3 — TLS with mkcert

CloudDevEnvironment's certificates in `CDE:local/central-router/config/cert/` are
**committed, not generated** — there is no script to copy. Generate fresh ones.

```bash
# Install mkcert (inside the devcontainer)
curl -fsSL "https://dl.filippo.io/mkcert/latest?for=linux/amd64" -o /tmp/mkcert
chmod +x /tmp/mkcert && sudo mv /tmp/mkcert /usr/local/bin/mkcert

mkdir -p local/central-router/config/cert local/central-router/config/ca

# Local CA. CAROOT is redirected so the CA lands in the repo, matching
# CloudDevEnvironment's layout (CDE:local/central-router/config/ca/rootCA.pem).
export CAROOT="$(pwd)/local/central-router/config/ca"
mkcert -install

cd local/central-router/config/cert
mkcert "*.pjx.localhost" "pjx.localhost" "localhost" "127.0.0.1"
cd -
ls local/central-router/config/cert/
```

> A wildcard `*.pjx.localhost` does **not** cover the bare `pjx.localhost`.
> Both names must be on the certificate — that is why they are listed
> separately above. Getting this wrong produces a browser warning only on the
> root hostname, which is easy to misdiagnose.

mkcert names files after the first SAN, e.g.
`_wildcard.pjx.localhost+3.pem` and `_wildcard.pjx.localhost+3-key.pem`.
Check the actual filenames and use them below.

Create `local/central-router/config/tls.yml`:

```yaml
tls:
  stores:
    default:
      defaultCertificate:
        certFile: /traefik/config/cert/_wildcard.pjx.localhost+3.pem
        keyFile: /traefik/config/cert/_wildcard.pjx.localhost+3-key.pem
  certificates:
    - certFile: /traefik/config/cert/_wildcard.pjx.localhost+3.pem
      keyFile: /traefik/config/cert/_wildcard.pjx.localhost+3-key.pem
```

Add TLS and an HTTP→HTTPS redirect to each router's labels — for example on
`pjx-web-react`:

```yaml
      - "traefik.http.routers.pjx-web.entrypoints=https"
      - "traefik.http.routers.pjx-web.tls=true"
      - "traefik.http.routers.pjx-web-http.rule=Host(`pjx.localhost`)"
      - "traefik.http.routers.pjx-web-http.entrypoints=http"
      - "traefik.http.routers.pjx-web-http.middlewares=to-https"
```

And define the redirect middleware once, on the Traefik service in
`local/docker-compose.yml`:

```yaml
    labels:
      - "traefik.enable=true"
      - "traefik.http.middlewares.to-https.redirectscheme.scheme=https"
      - "traefik.http.middlewares.to-https.redirectscheme.permanent=false"
```

Repeat the four-label pattern for the other four services. Then update
`WDS_SOCKET_PORT=443` for the React service.

> Keep `permanent=false` (a 302). A permanent 301 gets cached hard by browsers,
> which is painful if you later need to debug something over plain HTTP.

### Commit the CA, not the key

```gitignore
# Local TLS material — the CA cert is shared so teammates can trust it;
# private keys never are.
local/central-router/config/ca/rootCA-key.pem
local/central-router/config/cert/*-key.pem
```

CloudDevEnvironment commits both halves. Do not copy that — commit the public
CA certificate so others can import it, and keep every private key out of git.
Anyone cloning regenerates their own leaf certs with the commands above.

---

## Step 4 — Teach the devcontainer the hostnames

In `.devcontainer/devcontainer.json`, add `runArgs` (the pattern is from
`CDE:.devcontainer/devcontainer.json:8-21`):

```jsonc
"runArgs": [
  "--add-host=pjx.localhost:127.0.0.1",
  "--add-host=ql.pjx.localhost:127.0.0.1",
  "--add-host=api.pjx.localhost:127.0.0.1",
  "--add-host=node.pjx.localhost:127.0.0.1",
  "--add-host=sso.pjx.localhost:127.0.0.1",
  "--add-host=grafana.pjx.localhost:127.0.0.1"
],
```

Replace the whole `forwardPorts` / `portsAttributes` block. Only the router's
ports need forwarding now:

```jsonc
"forwardPorts": [80, 443, 9090],
"portsAttributes": {
  "80":  { "label": "Traefik (HTTP)",     "onAutoForward": "notify" },
  "443": { "label": "Traefik (HTTPS)",    "onAutoForward": "notify" },
  "9090":{ "label": "Traefik Dashboard",  "onAutoForward": "notify" }
},
"otherPortsAttributes": {
  // All workloads run on the host Docker daemon and are reached through
  // Traefik, so auto-forwarding app ports is unnecessary and interferes
  // with in-container connectivity.
  "onAutoForward": "ignore"
},
```

Then **Rebuild Container** — `runArgs` only take effect on creation.

### Host-side: trust the CA

On the **host**, not in the container:

- **Chrome:** `chrome://settings/certificates` → *Authorities* → **Import** →
  select `local/central-router/config/ca/rootCA.pem` → check "Trust this
  certificate for identifying websites".
- **Firefox:** Settings → Privacy & Security → Certificates → View Certificates
  → Authorities → Import.

If VS Code cannot bind privileged ports 80/443, port-forward them manually —
this is what `CDE:local/scripts/proxy.sh` exists for. Write the pjx equivalent
only if you actually hit the problem; on Linux without WSL you usually will not.

---

## Step 5 — Move the OIDC configuration

**This is the step most likely to cost you time.** OIDC compares redirect URIs
by exact string match, and IdentityServer4 validates that the token issuer
matches the authority the client asked for. Three files must agree.

**5a. `projects/pjx-sso-identityserver/Config.cs`** — the `pjx-web-react` client
at lines 90-98:

```csharp
ClientId = "pjx-web-react",
RedirectUris =           { "https://pjx.localhost/signin-oidc",
                           "https://pjx.localhost/dashboard",
                           "https://pjx.localhost/callback" },
PostLogoutRedirectUris = { "https://pjx.localhost",
                           "https://pjx.localhost/logout/callback" },
AllowedCorsOrigins =     { "https://pjx.localhost" },
```

Add `https://pjx.localhost/silentrenew` to `RedirectUris` as well — the React
app sets `REACT_APP_SILENT_REDIRECT_URL` and silent renew fails without it.

> The `mvc` (line 51) and `js` (line 71) clients still point at
> `localhost:5002` / `localhost:5003`. Leave them — they are IdentityServer4
> sample scaffolding, not used by pjx. Note it and move on.

**5b. `projects/pjx-web-react/.env`** — gitignored, so edit it directly and
**back it up first**:

```bash
cp projects/pjx-web-react/.env projects/pjx-web-react/.env.phase2-backup
```

```dotenv
NODE_ENV=development
REACT_APP_GRAPHQL_ENDPOINT=https://ql.pjx.localhost
REACT_APP_SSO_ISSUER_URL=https://sso.pjx.localhost
REACT_APP_SSO_CLIENT_ID=pjx-web-react
REACT_APP_SSO_REDIRECT_URL=https://pjx.localhost/signin-oidc
REACT_APP_PUBLIC_URL=https://pjx.localhost
REACT_APP_LOGOFF_REDIRECT_URL=https://pjx.localhost/logout/callback
REACT_APP_SILENT_REDIRECT_URL=https://pjx.localhost/silentrenew
REACT_APP_API_DOTNET_URL=https://api.pjx.localhost
```

Also commit a `projects/pjx-web-react/.env.example` with these values. The
current setup has no template, which is why the compose-vs-code drift went
unnoticed.

**5c. `PJX_SSO__AUTHORITY`** in `docker-compose.devcontainer.yml:46` — this is
the .NET API validating tokens *server-side*, so it uses the internal service
name today (`https://pjx-sso-identityserver`). It must now match the **issuer**
in the tokens the SSO server mints, which is `https://sso.pjx.localhost`:

```yaml
      - PJX_SSO__AUTHORITY=https://sso.pjx.localhost
```

Because that resolves through Traefik, the API container needs the hostname and
the CA. Add to the `pjx-api-dotnet` service:

```yaml
    extra_hosts:
      - "sso.pjx.localhost:host-gateway"
```

If the API rejects tokens with a certificate-validation error, the container
does not trust the mkcert CA. Simplest fix for a dev environment — mount the CA
and register it:

```yaml
    volumes:
      - ./local/central-router/config/ca/rootCA.pem:/usr/local/share/ca-certificates/pjx-root-ca.crt:ro
```

then rebuild with `update-ca-certificates` in `Dockerfile.dev`. Expect to
iterate here; token validation across a proxy boundary is genuinely fiddly.

---

## Step 6 — Remove the direct port publishes

Only once every hostname works end to end. Delete the `ports:` block from all
five app services in `docker-compose.devcontainer.yml`, and delete the three
dead React variables (`REACT_APP_GRAPHQL_URI`, `REACT_APP_API_URI`,
`REACT_APP_SSO_URI`) identified at the top of this document.

Traefik reaches the containers over `pjx-network`, so no published ports are
needed. Keep `9090` on the router for the dashboard.

Then update the URLs in `local/scripts/status.sh` from `localhost:PORT` to the
new hostnames.

---

## Verify

```bash
# 1. Router is healthy and sees five routers
curl -s http://localhost:9090/api/overview | head -c 200
curl -s http://localhost:9090/api/http/routers | grep -c '"name"'   # ≥ 5

# 2. HTTP redirects to HTTPS
curl -s -o /dev/null -w '%{http_code} %{redirect_url}\n' http://pjx.localhost/
# → 302 https://pjx.localhost/

# 3. TLS is valid, not self-signed-untrusted
curl -sI https://pjx.localhost/ | head -1
echo | openssl s_client -connect pjx.localhost:443 -servername pjx.localhost 2>/dev/null \
  | openssl x509 -noout -subject -ext subjectAltName

# 4. Every service answers over HTTPS
for h in pjx ql.pjx api.pjx node.pjx sso.pjx; do
  printf '%-18s %s\n' "$h" "$(curl -s -o /dev/null -w '%{http_code}' https://$h.localhost/)"
done

# 5. OIDC discovery document reports the NEW issuer
curl -s https://sso.pjx.localhost/.well-known/openid-configuration | grep -o '"issuer":"[^"]*"'
# → "issuer":"https://sso.pjx.localhost"

# 6. No app ports are published any more
docker ps --format '{{.Names}}\t{{.Ports}}' | grep pjx-
```

**Then the manual test that actually matters** — the automated checks above
cannot confirm OIDC works. In a browser:

1. Open `https://pjx.localhost` — no certificate warning.
2. Register a new account; read the activation code from
   `docker logs pjx-sso-identityserver-dev`.
3. Activate, then log in — you should land back on `https://pjx.localhost`
   authenticated. **This is the real pass/fail for Phase 2.**
4. Visit `/country/all` — exercises the .NET API with a bearer token, i.e. it
   proves step 5c worked.
5. Visit `/cities` — exercises Apollo → Node API.
6. Sign out — confirms `PostLogoutRedirectUris`.
7. Edit a React file and confirm hot reload still fires (validates
   `WDS_SOCKET_*`).

If login redirects to a blank page or an `invalid_request` error, compare the
`redirect_uri` query parameter in the browser URL against `Config.cs`
character for character. It is almost always a trailing-slash or scheme
mismatch.

---

## Rollback

```bash
git checkout master
git branch -D feature/arch-phase-2-traefik
docker compose -f local/docker-compose.yml down
```

Two things `git revert` will **not** undo:

```bash
# .env is gitignored — restore from the backup you made in step 5b
mv projects/pjx-web-react/.env.phase2-backup projects/pjx-web-react/.env

# mkcert installed a CA into the system trust store
CAROOT="$(pwd)/local/central-router/config/ca" mkcert -uninstall
```

Also remove the imported CA from your browser, and rebuild the container to drop
the `runArgs`.
