# How a request reaches a pjx container

A walkthrough of everything between typing `https://pjx.test` and a response
coming back from the React container — name resolution, TLS, Traefik routing, and
the container network.

Written while debugging Phase 2, because the same request behaves **differently
depending on where you start it**: your host browser, a shell in the devcontainer,
or one app container calling another. Understanding why is the difference between
fixing a problem in a minute and losing an afternoon to it.

---

## 1. The three vantage points

pjx runs **docker-outside-of-docker**: the devcontainer holds only the Docker
CLI, and every container — apps, Traefik, the devcontainer itself — is a sibling
on your host's Docker daemon.

```mermaid
flowchart LR
    subgraph host["Your machine"]
        browser["Browser"]
        subgraph daemon["Host Docker daemon"]
            dev["devcontainer<br/>pjx-root-workspace-1"]
            traefik["Traefik<br/>publishes :80 :443 :9091"]
            apps["5 app containers<br/>pjx-web-react, pjx-api-dotnet, ..."]
        end
    end
    browser -->|"localhost:443"| traefik
    dev -.->|"pjx-network"| apps
    dev -.->|"pjx-network"| traefik
    traefik -->|"pjx-network"| apps
```

Three starting points, three different answers to "where does this hostname
point?":

| Starting from | `localhost` means | Reaches Traefik via |
|---|---|---|
| Host browser / host shell | the host's loopback | published ports on `127.0.0.1` / `::1` |
| Devcontainer shell | **the devcontainer's own loopback** | `host-gateway` or the `pjx-network` address |
| An app container | **that container's own loopback** | `pjx-network` |

Almost every confusing failure in Phase 1 and Phase 2 came from assuming these
were the same.

---

## 2. Name resolution — and the `.localhost` trap

Before any traffic moves, the name must become an IP. This is where `.localhost`
behaves unlike every other name.

```mermaid
flowchart TD
    A["getaddrinfo('pjx.localhost')"] --> B{"Does the name end<br/>in .localhost ?"}
    B -->|Yes| C["glibc 2.35+ applies RFC 6761:<br/>return ::1 and 127.0.0.1<br/><b>/etc/hosts is never consulted</b>"]
    B -->|No| D["nsswitch.conf → 'hosts: files dns'"]
    D --> E["Read /etc/hosts"]
    E -->|match| F["Return that address<br/>e.g. 172.17.0.1"]
    E -->|no match| G["Ask the DNS resolver<br/>(Docker's embedded DNS on pjx-network)"]
    G --> H["Return container IP, or NXDOMAIN"]

    style C fill:#7f1d1d,color:#fff
```

Observed directly in the devcontainer, with **both** names present in
`/etc/hosts` pointing at `172.17.0.1`:

```
pjx.localhost  ->  ::1              ← /etc/hosts entry ignored
pjx.test       ->  172.17.0.1       ← same file, same mechanism, honoured
```

That is not a misconfiguration. glibc implements RFC 6761, which reserves
`.localhost` and mandates it resolve to loopback. `extra_hosts`, `/etc/hosts`,
and Docker's DNS are all bypassed for those names.

### Why this is fine from the browser and fatal from a container

```mermaid
flowchart TD
    subgraph ok["From the HOST browser — correct"]
        A1["pjx.localhost"] --> B1["::1 = host loopback"]
        B1 --> C1["Traefik publishes on [::]:443<br/>✅ connects"]
    end
    subgraph bad["From a CONTAINER — broken"]
        A2["sso.pjx.localhost"] --> B2["::1 = <b>that container's</b> loopback"]
        B2 --> C2["Nothing listening there<br/>❌ connection refused"]
    end

    style C1 fill:#14532d,color:#fff
    style C2 fill:#7f1d1d,color:#fff
```

On the host, "loopback" *is* where Traefik listens, so the special-casing
accidentally does the right thing. Inside a container, loopback is that
container's own network namespace — empty.

This is why `pjx-api-dotnet` could not reach `https://sso.pjx.localhost` to fetch
the OIDC discovery document — a failure that presents as broken authentication,
not broken DNS.

### The resolution: pjx uses `.test`

`.test` is reserved by the same RFC for testing and carries **no special resolver
behaviour**, so `/etc/hosts` and `extra_hosts` work normally and the name resolves
identically from the host, the devcontainer, and every app container.

The trade is one line in the host's `/etc/hosts`:

```
127.0.0.1 pjx.test ql.pjx.test api.pjx.test node.pjx.test sso.pjx.test grafana.pjx.test
```

and `extra_hosts: ["<name>:host-gateway"]` on any container that must reach
Traefik by hostname — currently `workspace` and `pjx-api-dotnet`.

Everything from here on uses `.test`. The `.localhost` material above is kept
because the trap is easy to fall back into: it looks like it should work, and it
does work from the browser.

---

## 3. The full path, host browser → container

Assuming the name resolves, here is everything that happens:

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant K as Host kernel<br/>:443
    participant T as Traefik
    participant C as pjx-web-react<br/>:3000

    B->>B: Resolve pjx.test → 127.0.0.1<br/>(host /etc/hosts)
    B->>K: TCP connect 127.0.0.1:443
    K->>T: Docker port publish<br/>0.0.0.0:443 → container :443

    Note over B,T: TLS handshake — before any HTTP exists
    B->>T: ClientHello, SNI = "pjx.test"
    T->>T: Search tls.yml certificates<br/>for a SAN matching the SNI
    T-->>B: Serve *.pjx.test cert + chain
    B->>B: Verify against mkcert CA<br/>(imported into the browser)

    Note over B,T: Now HTTP, inside the TLS tunnel
    B->>T: GET / , Host: pjx.test
    T->>T: entrypoint "https" → match router rule<br/>Host(`pjx.test`) → router pjx-web
    T->>T: Resolve service pjx-web → port 3000
    T->>C: GET / over pjx-network
    C-->>T: 200
    T-->>B: 200
```

Two details worth internalising:

- **SNI and the `Host` header are different things.** SNI is sent in the TLS
  ClientHello and selects the *certificate*; the `Host` header is sent inside the
  encrypted tunnel and selects the *router*. They normally carry the same value,
  which hides the distinction until something breaks — a certificate error is an
  SNI/`tls.yml` problem, a 404 is a `Host`/router-rule problem.
- **Traefik reaches the container over `pjx-network`, by service name and
  container port** — `pjx-web-react:3000`, not `localhost:3000`. The published
  host port `3000` is a separate, parallel path used only by your browser.

---

## 4. Inside Traefik

Traefik is assembled from four kinds of object, wired by two providers:

```mermaid
flowchart LR
    subgraph prov["Providers"]
        D["docker provider<br/>reads container labels<br/><i>routing</i>"]
        F["file provider<br/>reads config/*.yml<br/><i>TLS + middlewares</i>"]
    end
    subgraph obj["What they build"]
        EP["Entrypoints<br/>http :80 · https :443 · traefik :8080"]
        R["Routers<br/>rule + entrypoint + tls"]
        M["Middlewares<br/>to-https"]
        S["Services<br/>backend + port"]
    end
    D --> R
    D --> S
    F --> M
    F --> TLS["TLS store<br/>certificates"]
    EP --> R --> M --> S --> CT["Container"]
    TLS -.->|"cert selection by SNI"| EP
```

The **constraint label** filters what the docker provider will look at:

```
--providers.docker.constraints=Label(`traefik.constraint-label`, `pjx-public`)
```

Because Traefik talks to the *host* daemon, it can see every container on the
machine — the devcontainer, and other projects' containers too. Only those
carrying `traefik.constraint-label=pjx-public` are considered.

> This cuts both ways: Traefik ignores **its own** labels unless it carries that
> label, which is why the `to-https` middleware belongs in the file provider and
> is referenced as `to-https@file`. A middleware defined on the Traefik container
> is silently dropped, and the redirect routers end up `"status": "disabled"`.

### Two routers per service

Each service has an HTTPS router and an HTTP router that exists only to redirect:

```mermaid
flowchart TD
    H80["Request on :80<br/>http entrypoint"] --> RH["router pjx-web-http<br/>rule: Host(`pjx.test`)"]
    RH --> MW["middleware to-https@file<br/>redirectScheme, 302"]
    MW --> RESP["302 Location:<br/>https://pjx.test/"]
    RESP -.->|"browser follows"| H443
    H443["Request on :443<br/>https entrypoint"] --> RS["router pjx-web<br/>rule + tls=true"]
    RS --> SVC["service pjx-web → :3000"]
    SVC --> CT["pjx-web-react container"]

    style MW fill:#1e3a8a,color:#fff
    style RS fill:#14532d,color:#fff
```

Pinning `entrypoints` on each router matters. A router with no `entrypoints`
attaches to **all** of them, so without it the HTTPS router would also claim `:80`
and collide with the redirect router on the same hostname.

---

## 5. Reading a failure

The symptom usually tells you which layer to look at:

| Symptom | Layer | Check |
|---|---|---|
| `Could not resolve host` / resolves to `::1` unexpectedly | Name resolution | `getent hosts <name>`, `cat /etc/hosts`, is it a `.localhost` name? |
| `Connection refused` / curl `000` | Transport | Is anything listening? `ss -tlnp \| grep ':443 '` — on the **right machine** |
| Certificate error | TLS / SNI | `openssl s_client -connect host:443 -servername host -CAfile <ca>` |
| **404 from Traefik** | Router rule | `curl -s localhost:9091/api/http/routers/<name>@docker` |
| 502 / 503 | Service → backend | `curl -s localhost:9091/api/http/services` and check `serverStatus` |
| App-level error (400, 500) | The app itself | `docker logs <container>` |

Traefik's API is the fastest tool for the middle layers, because it reports
`status` and `error` per object:

```bash
curl -s http://localhost:9091/api/overview | python3 -m json.tool

curl -s http://localhost:9091/api/http/routers > /tmp/r.json
python3 - <<'PY'
import json
for r in json.load(open('/tmp/r.json')):
    if r.get("status") != "enabled" or r.get("error"):
        print(f"  {r['name']:<30} status={r.get('status')}")
        for e in r.get("error", []): print(f"      -> {e}")
PY
```

A **404** in particular is Traefik saying "no router matched" — which is a
labels/rule problem, not an application problem. Compare against an app-generated
400 (Apollo's "GET query missing"), which proves routing *worked*.

---

## 6. Quick reference

| From | To reach a service, use |
|---|---|
| Host browser / host shell | `https://pjx.test` (published ports) |
| Devcontainer shell, via Traefik | the hostname — needs a resolvable TLD, `extra_hosts` → `host-gateway` |
| Devcontainer shell, direct to a service | `http://pjx-web-react:3000` (service name + **container** port) |
| App container → app container | `http://pjx-api-node:8081` (service name + container port) |
| Traefik → app container | service name + container port, over `pjx-network` |

Container ports are not the published ones: the .NET API listens on `:80` inside
and is published as `:6001`; the SSO server listens on `:80` and is published as
`:5001`.

---

## See also

- [Phase 2 — Traefik, TLS and hostnames](../architecture-upgrade/phase-2-traefik.md)
- [Phase 1 — Step 6a, host paths for bind mounts](../architecture-upgrade/phase-1-script-layer.md#step-6a--emit-host-paths-for-bind-mounts)
- [Phase 0 — Step 3b, the docker-in-docker vs outside-of-docker decision](../architecture-upgrade/phase-0-foundation.md#step-3b--nine-defects-found-during-execution)
