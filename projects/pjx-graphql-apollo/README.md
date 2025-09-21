# pjx-graphql-apollo

This project is one of the components of the `pjx` application, please check [pjx-root](https://github.com/mikelau13/pjx-root) for more details.

## Project Dependencies

- [pjx-api-node](https://github.com/mikelau13/pjx-api-node) - the API that this Apollo Server is consuming

> For the real world problems, GraphQL is a powerful tool for a more complex infrastructure, such that a company with many differenct API endpoints, or an API have complex schema for many different scenarios, therefore many different variations to return data even on the same endpoint.  There are a lot of discussions on the internet about when to use or not to use GraphQL, so, depending on your point of view, it might be overkilled to use GraphQL to connect to one and only one simple API gateway in my demo project.  I am using GraphQL here only for demo purpose.


## Project Tools

- [TypeScript](https://www.typescriptlang.org/index.html)
- [graphQL](https://graphql.org/learn/)
- [apollo-server](https://www.apollographql.com/docs/apollo-server/)

## Environment Dependencies

Ensure you have [Docker](https://docs.docker.com/) installed and running on you local machine.

- [Install Docker for Mac](https://docs.docker.com/docker-for-mac/install/)
- [Install Docker for Windows](https://docs.docker.com/docker-for-windows/)
- [Install Docker for Ubuntu](https://phoenixnap.com/kb/how-to-install-docker-on-ubuntu-18-04)

## Local Development (docker)

Run the following in the command line from the root of the pjx-graphql-apollo project.  

```bash
docker-compose up
```

This will run a local graphql server in a docker container, can be reached at http://localhost:4000.

![pjx graphql playground](/images/apollo_query.png)

Sample queries that you may try:
```
{
  cities {
    id
    name
    city
  }
 }
```
```
{
  city(id:"1"){
    id
    name
    city
  }
}
```

To stop the local server (container) run the following:

```bash
docker-compose down
```
