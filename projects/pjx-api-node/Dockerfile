# base image
FROM node:14-slim
RUN apt-get update
RUN apt-get install python make -y

WORKDIR /home/node

COPY package.json .
COPY package-lock.json .
RUN npm install --quiet

COPY . .
ENTRYPOINT ["npm", "start"]