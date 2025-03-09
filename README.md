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


Or Plant UML (Auto converted by Cluade.Ai)

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

You will need to ensure you have [Docker](https://docs.docker.com/) installed on your machine.

- [Install Docker for Mac](https://docs.docker.com/docker-for-mac/install/)
- [Install Docker for Windows](https://docs.docker.com/docker-for-windows/)
- [Install Docker for Ubuntu](https://phoenixnap.com/kb/how-to-install-docker-on-ubuntu-18-04)

Clone [pjx-root](https://github.com/mikelau13/pjx-root) repo. The is to make this the parent directory for the pjx projects.


## How does it work?

Once you've cloned this repo you will see an empty folder `/projects`.

This folder contains other cloned github repos, where each repo represents a dockerized project. (ie. api, Apollo server, web client, etc). The contents of this folder are ignored by git, and should not be committed to version control - you download the repos and launch them, but not supposed to make or commit any changes inside `projects` folder.


## Helm Charts

```ps
helm install pjx-release helm-pjx/
```



## Running a solution

To run the `pjx` solution, clone all the required repos inside the `projects` folder, then run the `docker-compose up` on the root folder:

```bash
cd ./projects
git clone https://github.com/mikelau13/pjx-graphql-apollo.git
git clone https://github.com/mikelau13/pjx-api-node.git
git clone https://github.com/mikelau13/pjx-api-dotnet.git
git clone https://github.com/mikelau13/pjx-sso-identityserver.git
git clone https://github.com/mikelau13/pjx-web-react.git
docker-compose -f ../docker-compose.yml up
```

Execute this command to stop them:

```bash
$ docker-compose -f ../docker-compose.yml down
```

On development environment, you might want to first prune all containers to avoid any conflicts:

```bash
docker system prune
```

Then verify your containers are up and running:

```bash
$ docker ps
```

### Hosts and SSL

For localhost testing, you will need to set up the `hosts` file and trust the SSL certificate for your machine/web browsers, please follow the instructions on my other project [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver).


### Using the web app

You can now visit `http://localhost:3000` to try the website, there are few sanity testing you can try:

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

- visit the GraphQL playground of the Apollo Server - http://localhost:4000
![pjx graphql playground](/images/apollo_query.png)
- try out the Swagger of the .NET Core API - http://localhost:6001/swagger/
![pjx api swagger](/images/api_swagger.png)
- try out the Swagger of the Identity Server - https://pjx-sso-identityserver/swagger
![pjx sso swagger](/images/identityserver_swagger.png)
- try out the responsive HTML design by changing the browser size
![pjx html responsive](/images/mobile_desktop.png)

