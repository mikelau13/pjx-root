# pjx-api-node

This is an API layer developed with [Restify](http://restify.com),

This project is one of the components of my demo `pjx` application, please check [pjx-root](https://github.com/mikelau13/pjx-root) for more details.


## Build & Launch

To start the docker container, run this command:

```bash
docker-compose up --build
```

Or if you want to run the app directly locally:

```bash
npm start
```

To test:

```bash
curl http://localhost:8081/healthcheck
curl http://localhost:8081/api/1/cities/
curl http://localhost:8081/api/1/city/1
```
