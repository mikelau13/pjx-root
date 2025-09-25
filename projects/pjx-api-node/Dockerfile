# base image
FROM node:18-slim
RUN apt-get update && apt-get install -y python3 make build-essential && rm -rf /var/lib/apt/lists/*

WORKDIR /home/node

COPY package.json .
COPY package-lock.json .
RUN npm install --quiet

COPY . .
ENTRYPOINT ["npm", "start"]