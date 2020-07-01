# pjx-root

<p align="center">A one stop shop to launch pjx dockerized apps.</p>


## Installation

Ensure Docker have been installed and running. [Download Docker](https://www.docker.com/get-started)

Clone [pjx-root](https://github.com/mikelau13/pjx-root) repo. The is to make this the parent directory for the pjx projects.


## How does it work?

Once you've cloned this repo you will these folders:

#### projects

This folder will contain other cloned github repos, where each repo represents a dockerized project. (ie. api, Apollo server, etc). The contents of this folder are ignored by git, and should not be committed to version control - you download the repos and launch them, but not supposed to make or commit any changes inside `projects` folder.

### recipes

This folder contains `docker-compose` files for all projects. For example `pjx-graphql-apollo.yml` is a docker-compose file preconfigured with all the services required to run a containerized version of Apollo Server middleware.

## Running a recipe

To run a specifc recipe, clone all the required repos inside the `projects` folder. Each recipe should provide details on which repos are required.

For example if we wanted to run the `pjx-graphql-apollo` app:

```bash
$ cd pjx-root/projects
$ git clone git@github.com:mikelau13/pjx-graphql-apollo.git
$ cd ..
$ docker-compose -f recipes/pjx.yml up
```

Then verify your containers are up and running:

```bash
$ docker ps
```
