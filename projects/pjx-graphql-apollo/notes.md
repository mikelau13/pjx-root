## Config nodemon

```bash
npm install --save-dev nodemon
```

## Config typescript

```bash
npm install -g typescript
```

> **NOTE:** Might need to set permissions first time installing global library

```bash
sudo chown -R $(whoami) /usr/local
```

npm install ts-node --save-dev


## Config docker compose (already installed docker)

sudo curl -L "https://github.com/docker/compose/releases/download/1.25.5/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose

sudo chmod +x /usr/local/bin/docker-compose

docker-compose --version


## Config Apollo Server
npm install apollo-server graphql


Try a simple query on the playground - http://localhost:4000/:

```
{
  books{
    title
  }
}
```
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
  city(id:"1"){
    id
    name
    city
  }
```

## Apollo data source

Apollo server built-in support for caching, deduplication, and error handling.

https://www.apollographql.com/docs/apollo-server/data/data-sources/

```bash
npm install apollo-datasource-rest
```


## Winston Logger

https://github.com/winstonjs/winston

```bash
npm install winston
```


## Jest

(will install later)

```bash
npm i -D ts-jest @types/jest
```
