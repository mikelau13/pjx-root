#Stage 1
FROM node:14.5.0-slim as builder
WORKDIR /app
# .npmrc carries legacy-peer-deps=true, required for the Apollo Client 2 /
# graphql 15 peer conflict. Without it this RUN fails with ERESOLVE.
COPY package*.json .npmrc ./
RUN npm install
COPY . .
RUN npm run build

#Stage 2
FROM nginx:1.19.0
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*
COPY --from=builder /app/build .
ENTRYPOINT ["nginx", "-g", "daemon off;"]