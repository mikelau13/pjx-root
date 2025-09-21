#!/bin/bash

echo "Setting up PJX development environment..."

# Install global npm packages that might be useful
npm install -g nodemon ts-node typescript

# Set up git hooks or any other initialization
echo "Setting up git configuration..."
git config --global init.defaultBranch main
git config --global core.autocrlf input

# Install dependencies for all Node.js projects
echo "Installing dependencies for Node.js projects..."

if [ -d "/workspaces/pjx-root/projects/pjx-web-react" ]; then
    echo "Installing dependencies for pjx-web-react..."
    cd /workspaces/pjx-root/projects/pjx-web-react && npm install
fi

if [ -d "/workspaces/pjx-root/projects/pjx-api-node" ]; then
    echo "Installing dependencies for pjx-api-node..."
    cd /workspaces/pjx-root/projects/pjx-api-node && npm install
fi

if [ -d "/workspaces/pjx-root/projects/pjx-graphql-apollo" ]; then
    echo "Installing dependencies for pjx-graphql-apollo..."
    cd /workspaces/pjx-root/projects/pjx-graphql-apollo && npm install
fi

echo "Restoring .NET projects..."
if [ -d "/workspaces/pjx-root/projects/pjx-api-dotnet" ]; then
    cd /workspaces/pjx-root/projects/pjx-api-dotnet && dotnet restore
fi

if [ -d "/workspaces/pjx-root/projects/pjx-sso-identityserver" ]; then
    cd /workspaces/pjx-root/projects/pjx-sso-identityserver && dotnet restore
fi

cd /workspaces/pjx-root

echo "Setup complete!"
echo ""
echo "Available services:"
echo "  - React Web App: http://localhost:3000"
echo "  - GraphQL Apollo: http://localhost:4000"
echo "  - .NET API: http://localhost:6001"
echo "  - Node.js API: http://localhost:8081"
echo "  - Identity Server: https://localhost:5002"
echo ""
echo "To start all services: docker-compose -f docker-compose.devcontainer.yml up"