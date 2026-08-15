# pjx-root

<p align="center">An one stop shop to launch the entire <a href='https://github.com/users/mikelau13/projects/1'>pjx</a> dockerized application.</p>

## Overview

To run `pjx-root` you will need the following projects:

- [pjx-web-react](https://github.com/mikelau13/pjx-web-react) - this is the client side web interface, developed using React.js

- [pjx-graphql-apollo](https://github.com/mikelau13/pjx-graphql-apollo) - Api gateway using Apollo Server, the web interface `pjx-web-react` consumes Api through this GraphQL middleware

- [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver) - open source [IdentityServer4](https://github.com/IdentityServer/IdentityServer4) with .NET Core 3.1, it is an identity server to handle authentication of the web app with OAuth2, the `pjx-web-react` web interface will be connecting to this server using `ocid-client` client library.  Visit [IdentityServer4](https://identityserver4.readthedocs.io/en/latest/) for documentations.

- [pjx-api-node](https://github.com/mikelau13/pjx-api-node) - Api backend developed with TypeScript to fetch data and manage business logic.

- [pjx-api-dotnet](https://github.com/mikelau13/pjx-api-dotnet) - Api backend developed with DotNet Core 3.1 to fetch data and manage business logic.

Architecture overview looks like this: 
![pjx Architecture Overview](/images/pjx-overview.png)

Kubernetes Cluster looks like this: 
![pjx Kubernetes Deployment](/images/pjx-Deployment.png)


#### Plant UML (Auto converted by Cluade.Ai)


<div hidden>
```
@startuml pjx-overview

package "web Pjx" {
  component "pjx-web-react:\nGeneral data" as WebReactGeneral
  component "pjx-web-react:\nClientPage" as WebReactClient
  component "pjx-web-react:\nLogin,Register,Activation Pages" as WebReactLogin
}

package "Apollo_Server" as ApolloServer {
  component "GraphQL API" as GraphQLAPI
}

package "API server" as APIServerNode {
  component "pjx-api-node:\nRestify API" as RestifyAPI
  database "Database1" as DB1
}

package "API server" as APIServerDotnet {
  component "pjx-api-dotnet:\ncontroller" as DotnetController
  database "Database2" as DB2
}

package "Identity Server" as IdentityServer {
  component "pjx-sso-identityserver:\nOAuth2.0 endpoint" as OAuth
  component "pjx-sso-identityserver:\nMVC" as MVC
  database "Database3" as DB3
}

note top of ApolloServer : pjx-graphql-apollo
note top of APIServerNode : pjx-api-node
note top of APIServerDotnet : pjx-api-dotnet
note top of IdentityServer : pjx-sso-identityserver
note top of WebReactGeneral : React js Web
note left of WebReactClient : restricted pages

WebReactGeneral -right-> GraphQLAPI : "GraphQL query"
GraphQLAPI -right-> RestifyAPI : "request data"

WebReactClient -right-> DotnetController : "OpenID Connect"
DotnetController -down-> OAuth : "authorize"

WebReactLogin -right-> MVC : "redirect"

@enduml
```
</div>

![](/images/pjx-overview.svg)


## Installation

### Prerequisites

You will need to ensure you have [Docker](https://docs.docker.com/) installed on your machine.

- [Install Docker for Mac](https://docs.docker.com/docker-for-mac/install/)
- [Install Docker for Windows](https://docs.docker.com/docker-for-windows/)
- [Install Docker for Ubuntu](https://phoenixnap.com/kb/how-to-install-docker-on-ubuntu-18-04)

For **WSL Development Container support**, you'll also need:
- [Visual Studio Code](https://code.visualstudio.com/)
- [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
- [WSL 2](https://docs.microsoft.com/en-us/windows/wsl/install) (Windows users)

### Repository Setup

Clone [pjx-root](https://github.com/mikelau13/pjx-root) repo. This is to make this the parent directory for the pjx projects.


## How does it work?

The five services live under `projects/` and are **vendored into this
repository** — they are tracked in git, not submodules and not gitignored. You
do not need to clone them separately; `git clone` of pjx-root brings everything.

| Directory | Service |
|---|---|
| `projects/pjx-web-react` | React SPA |
| `projects/pjx-graphql-apollo` | Apollo GraphQL gateway |
| `projects/pjx-api-node` | Node/Restify API |
| `projects/pjx-api-dotnet` | .NET API |
| `projects/pjx-sso-identityserver` | OIDC identity server |

Everything runs behind a **Traefik** reverse proxy on `*.pjx.test` with
locally-trusted TLS. See
[docs/reference/request-flow.md](docs/reference/request-flow.md) for how a
request actually reaches a container, and
[docs/architecture-upgrade/](docs/architecture-upgrade/README.md) for the
migration plan this setup came from.

---

## TL;DR — `make`

If you only read one section, read this one.

```bash
cd ~/projects/pjx-root
make            # list every target
make up         # Traefik + 5 app services + Grafana
make down       # stop everything, ~4 GB freed
make status     # what is running, and how much memory it holds
make urls       # the service URLs
```

`make down` **from a host terminal** — it stops the devcontainer, so from inside
it would kill the shell running it. Run there anyway and it stops everything else
and tells you what it skipped.

**`make up` never rebuilds.** After changing a `Dockerfile`, `package.json` or
`.csproj`, use `dev-up.sh -b -d` instead — `make up` would start the stale image
without complaining. Everything else routine is `make up`.

> `make up` is not a substitute for `dev-up.sh` so much as a superset of the
> common case: `dev-up.sh` starts the five app services only, while Traefik and
> Grafana live in separate Compose projects. Starting just the app services gives
> five healthy containers, no routing, and no dashboards — a state that looks
> fine in `status.sh` right up until the browser refuses the connection.

Nothing here is destructive. Every target uses `docker compose stop`, never
`down`, so SQLite databases, `node_modules` volumes and Grafana dashboards all
survive. Use `local/scripts/clean.sh` only when you actually want to discard
state.

There are three separate Compose stacks, which is why a single wrapper is worth
having:

| Project | File | Contains |
|---|---|---|
| `pjx-root` | `docker-compose.devcontainer.yml` | devcontainer + 5 app services |
| `pjx-otel` | `observability/docker-compose.yml` | Grafana LGTM |
| `pjx-router` | `local/docker-compose.yml` | Traefik |

The devcontainer is started by VS Code when you open the folder, so `make up`
leaves it alone — it only starts the things VS Code will not.

> **Coming back after a break:** `make up` on the host, then open the folder in
> VS Code. First run on a new machine needs
> [Prerequisites](#1-prerequisites-on-the-host) and
> [TLS certificates](#3-generate-tls-certificates-first-time-only) below.

---

## Running the stack

Development happens inside a **VS Code Dev Container**. It runs
docker-outside-of-docker, so the app containers are siblings on your host's
Docker daemon rather than nested inside the devcontainer.

### 1. Prerequisites, on the host

Add the dev hostnames to `/etc/hosts` — `.test` has no automatic resolution:

```bash
sudo sh -c 'echo "127.0.0.1 pjx.test ql.pjx.test api.pjx.test node.pjx.test sso.pjx.test grafana.pjx.test" >> /etc/hosts'
```

Ports **80**, **443** and **9091** must be free — Traefik claims them. If you
also use another Traefik-based environment, stop it first, and check for
leftover host processes holding the ports:

```bash
sudo ss -tlnp | grep -E ':(80|443|9091) '
```

### 2. Open in the Dev Container

Open the repo in VS Code and choose **Reopen in Container** (or `Ctrl+Shift+P` →
*Dev Containers: Reopen in Container*). First build takes a few minutes;
`postCreateCommand` installs all Node and .NET dependencies.

### 3. Generate TLS certificates (first time only)

```bash
cd local/central-router/config/cert
export CAROOT=/workspaces/pjx-root/local/central-router/config/ca
mkcert -install
mkcert "*.pjx.test" "pjx.test" "localhost" "127.0.0.1"
```

Then trust the CA in your **host** browser — Chrome:
`chrome://settings/certificates` → *Authorities* → **Import** →
`local/central-router/config/ca/rootCA.pem`.

TLS material is gitignored: mkcert CAs are per-machine, so everyone generates
their own.

### 4. Start everything

`make up` from the host covers this. The per-stack commands, if you want one
piece at a time from **inside the devcontainer**:

```bash
dev-up.sh -d                                       # the five app services
docker compose -f local/docker-compose.yml up -d   # Traefik
obs-up.sh                                          # Grafana
status.sh                                          # health table
```

| Script | Does |
|---|---|
| `dev-up.sh [-b] [-d\|-w]` | start the stack — build / detached / watch |
| `stop.sh` | stop the app containers, keep them |
| `obs-up.sh` | start the Grafana LGTM stack |
| `clean.sh` | remove containers and volumes (prompts first — destroys the SQLite databases) |
| `status.sh` | per-service container state and HTTP health |
| `validate.sh <test\|build\|lint> [project]` | build or test one project or all |

`status.sh` and `make status` answer different questions. `status.sh` runs inside
the devcontainer and checks each service over `pjx-network`, so it reports **HTTP
health**. `make status` works from either side and reports **container state and
memory** — which is what you want when the question is "is this project still
eating my RAM?"

### Stopping

```bash
make down          # host terminal — everything, including the devcontainer
```

The devcontainer is the single largest consumer (~1.7 GB) because the VS Code
server runs inside it. If you stop only the app services, that stays resident.
Closing the VS Code window releases it too, via `shutdownAction: stopCompose`.

Stopped containers use no CPU and no memory, so `stop` is all that is needed to
hand resources back to another project. `docker compose down` would additionally
delete the containers, costing recreate time for no benefit.

### Service URLs

| Service | URL |
|---|---|
| React SPA | <https://pjx.test> |
| GraphQL | <https://ql.pjx.test> |
| .NET API | <https://api.pjx.test/swagger> |
| Node API | <https://node.pjx.test> |
| Identity Server | <https://sso.pjx.test> |
| Traefik dashboard | <http://localhost:9091/dashboard/> |

Plain `http://` redirects to `https://`.

---

## Helm Charts

```bash
helm install pjx-release helm-pjx/
```

---

### Using the web app

Visit <https://pjx.test> to try the website. A few sanity checks:

- register a new account - verify if the web app `client side` is consuming the `Identity Server API`, with `SSL` and `CoRS` settings, properly or not
<br/><img src="/images/user_registration.png" alt="pjx user registration" style="max-width: 60%;" />

- activate your account - since this project is for demo purpose, you will not receive the activation email, instead, after registration, check the command logs to find the activation code to active your account 
<br/><img src="/images/account_registered.png" alt="pjx user registered" style="max-width: 60%;" />
<br/><img src="/images/account_validated.png" alt="pjx user validated" style="max-width: 60%;" />
- login
<br/><img src="/images/user_login.png" alt="pjx user login" style="max-width: 50%;" />

- on the site menu, visit the `/country/all` page - this will verify the connectivity with the .NET Core API, which will authenticate the connection with the Identity Server on the  `backend` side
- on the left/hamburger menu, visit the `/cities` page - it will verify the Apollo Server and the Restify API
- on the left/hamburger menu, visit the `Profile` page - it will verify the Identity Server MVC
- Calendar - CURD
<br/><img src="/images/calendar.png" alt="pjx calendar" style="max-width: 70%;" />
- Sign Out
<br/><img src="/images/user_signout.png" alt="pjx user signout" style="max-width: 50%;" />

- visit the GraphQL playground of the Apollo Server - https://ql.pjx.test
![pjx graphql playground](/images/apollo_query.png)
- try out the Swagger of the .NET Core API - https://api.pjx.test/swagger/
![pjx api swagger](/images/api_swagger.png)
- try out the Swagger of the Identity Server - https://sso.pjx.test/swagger
![pjx sso swagger](/images/identityserver_swagger.png)
- try out the responsive HTML design by changing the browser size
![pjx html responsive](/images/mobile_desktop.png)


