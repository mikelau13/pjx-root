#Stage 1
FROM node:18-slim as builder
WORKDIR /app
# .npmrc carries legacy-peer-deps=true, required for the Apollo Client 2 /
# graphql 15 peer conflict. Without it this RUN fails with ERESOLVE.
COPY package*.json .npmrc ./
# npm ci honours package-lock.json exactly. node:14's npm 6 cannot read this
# lockfileVersion 3 file at all — it silently resolved fresh and pulled an
# @types/babel__traverse needing TypeScript 4.1+ against this project's 3.7.5.
RUN npm ci
COPY . .
# react-scripts 3.4.3 uses a hash algorithm OpenSSL 3 (Node 17+) rejects.
# The "start" script already carries this flag; "build" does not.
RUN NODE_OPTIONS=--openssl-legacy-provider npm run build



#Stage 2
FROM nginx:1.19.0
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*
COPY --from=builder /app/build .
ENTRYPOINT ["nginx", "-g", "daemon off;"]