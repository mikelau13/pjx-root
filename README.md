# pjx-root

<p align="center">An one stop shop to launch the entire `pjx` dockerized application.</p>

## Overview

To run `pjx-root` you will need the following projects:

- [pjx-web-react](https://github.com/mikelau13/pjx-web-react) - this is the client side web interface, developed using React.js

- [pjx-graphql-apollo](https://github.com/mikelau13/pjx-graphql-apollo) - Api gateway using Apollo Server, the web interface `pjx-web-react` consumes Api through this middleware with GraphQL 

- [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver) - open source [IdentityServer4](https://github.com/IdentityServer/IdentityServer4) with .NET Core 3.1, it is an identity server to handle authentication of the web app with OAuth2, the `pjx-web-react` web interface will be connecting to this server using `ocid-client` client library.  Visit [IdentityServer4](https://identityserver4.readthedocs.io/en/latest/) for documentations.

- [pjx-api-node](https://github.com/mikelau13/pjx-api-node) - Api backend to fetch data and manage business logic.

TODO: need separated start command for dev and prod environment


## Installation

You will need to ensure you have [Docker](https://docs.docker.com/) installed on your machine.

- [Install Docker for Mac](https://docs.docker.com/docker-for-mac/install/)
- [Install Docker for Windows](https://docs.docker.com/docker-for-windows/)

Clone [pjx-root](https://github.com/mikelau13/pjx-root) repo. The is to make this the parent directory for the pjx projects.


## How does it work?

Once you've cloned this repo you will see these 2 folders:

#### /projects

This folder contains other cloned github repos, where each repo represents a dockerized project. (ie. api, Apollo server, web client, etc). The contents of this folder are ignored by git, and should not be committed to version control - you download the repos and launch them, but not supposed to make or commit any changes inside `projects` folder.

### /solutions

This folder contains `docker-compose` files for all projects. For example `pjx-graphql-apollo.yml` is a docker-compose file preconfigured with all the services required to run a containerized version of Apollo Server middleware.

## Running a solution

To run the `pjx` solution, clone all the required repos inside the `projects` folder, then run the `docker-compose up` on the `pjx.yml`:

```bash
$ cd pjx-root/projects
$ git clone git@github.com:mikelau13/pjx-graphql-apollo.git
$ git clone git@github.com:mikelau13/pjx-api-node.git
$ git clone git@github.com:mikelau13/pjx-sso-identityserver.git
$ git clone git@github.com:mikelau13/pjx-web-react.git
$ docker-compose -f ../solutions/pjx.yml up
```

Execute this command to stop them:

```bash
$ docker-compose -f ../solutions/pjx.yml down
```

On development environment, you might want to first prune all containers to avoid any conflicts:

```bash
docker system prune
```

Then verify your containers are up and running:

```bash
$ docker ps
```

Then visit `http://localhost:3000` to try the website.
