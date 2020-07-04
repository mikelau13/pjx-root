# pjx-root

<p align="center">A one stop shop to launch pjx dockerized apps for staging/production environment.</p>

## Overview

Provide a one stop shop to launch all necessary services for the entire application, mostly for staging/production deployment purpose.

For development environment, visit projects and launch each of them individually, since some features are suppopsed to be development only, for instance, hotreloading.

TODO: need separated start command for dev and prod environment


## Installation

You will need to ensure you have [Docker](https://docs.docker.com/) installed on your machine.

- [Install Docker for Mac](https://docs.docker.com/docker-for-mac/install/)
- [Install Docker for Windows](https://docs.docker.com/docker-for-windows/)

Clone [pjx-root](https://github.com/mikelau13/pjx-root) repo. The is to make this the parent directory for the pjx projects.


## How does it work?

Once you've cloned this repo you will see these 2 folders:

#### projects

This folder contains other cloned github repos, where each repo represents a dockerized project. (ie. api, Apollo server, web client, etc). The contents of this folder are ignored by git, and should not be committed to version control - you download the repos and launch them, but not supposed to make or commit any changes inside `projects` folder.

### recipes

This folder contains `docker-compose` files for all projects. For example `pjx-graphql-apollo.yml` is a docker-compose file preconfigured with all the services required to run a containerized version of Apollo Server middleware.

## Running a recipe

To run a specifc recipe, clone all the required repos inside the `projects` folder. Each recipe should provide details on which repos are required.

For example if we wanted to run the `pjx` app:

```bash
$ cd pjx-root/projects
$ git clone git@github.com:mikelau13/pjx-graphql-apollo.git
$ git clone git@github.com:mikelau13/pjx-api-node.git
$ cd ..
$ docker-compose -f recipes/pjx.yml up
```

Then verify your containers are up and running:

```bash
$ docker ps
```
