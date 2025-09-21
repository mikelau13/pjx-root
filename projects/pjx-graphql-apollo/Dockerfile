# base image
FROM node:10-slim

WORKDIR /home/node

COPY package.json .
COPY package-lock.json .
RUN npm install --quiet

COPY . .

ENTRYPOINT ["npm", "start"]