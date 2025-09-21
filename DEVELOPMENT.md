# PJX Development Guide

This guide covers development setup using WSL Development Containers for the PJX multi-service application.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Development Workflows](#development-workflows)
- [Service Details](#service-details)
- [Troubleshooting](#troubleshooting)

## Prerequisites

1. **Windows with WSL 2** (for Windows users)
   - Install WSL 2: `wsl --install`
   - Ensure Docker Desktop is configured to use WSL 2

2. **Development Tools**
   - [Visual Studio Code](https://code.visualstudio.com/)
   - [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
   - [Docker Desktop](https://www.docker.com/products/docker-desktop)

3. **Git Configuration**
   ```bash
   git config --global user.name "Your Name"
   git config --global user.email "your.email@example.com"
   ```

## Quick Start

### 1. Clone and Setup

```bash
# Clone to WSL file system for better performance
cd ~
git clone <repository-url> pjx-root
cd pjx-root
```

### 2. Open in Dev Container

```bash
# Option A: Using VS Code command
code .
# Then Ctrl+Shift+P → "Dev Containers: Reopen in Container"

# Option B: Direct command
code --folder-uri vscode-remote://dev-container+<path-to-project>
```

### 3. Start All Services

```bash
# In the dev container terminal
docker-compose -f docker-compose.devcontainer.yml up -d
```

### 4. Access Services

- **React Web App**: http://localhost:3000
- **GraphQL Playground**: http://localhost:4000
- **.NET API Swagger**: http://localhost:6001/swagger
- **Node.js API**: http://localhost:8081
- **Identity Server**: https://localhost:5002

## Development Workflows

### Multi-Service Development

When working on features that span multiple services:

1. **Use the root devcontainer** for orchestration
2. **Open individual services** in separate VS Code windows for focused development
3. **Use the shared network** for inter-service communication

```bash
# Start all services in background
docker-compose -f docker-compose.devcontainer.yml up -d

# View logs for specific service
docker-compose -f docker-compose.devcontainer.yml logs -f pjx-web-react

# Restart specific service
docker-compose -f docker-compose.devcontainer.yml restart pjx-api-node
```

### Individual Service Development

For focused development on a single service:

```bash
# Navigate to service directory
cd projects/pjx-web-react

# Open in its own devcontainer
code .
# Select "Reopen in Container"
```

### Hot Reload Development

All services are configured with hot reload:

- **React**: Automatic reload on file changes
- **Node.js services**: Nodemon for automatic restart
- **.NET services**: `dotnet watch` for automatic rebuild

### Debugging

#### Node.js Services (API and GraphQL)

1. **In VS Code**: Set breakpoints in TypeScript files
2. **Start debugging**: F5 or Debug panel
3. **Attach to process**: Pre-configured debug configurations available

#### .NET Services

1. **Set breakpoints** in C# files
2. **Use F5** to start debugging
3. **Attach to process** for running containers

#### React Application

1. **Browser DevTools**: Standard React debugging
2. **VS Code**: Use React DevTools extension
3. **Source maps**: Enabled for TypeScript debugging

## Service Details

### pjx-web-react (Port 3000)

**Technology**: React 16.13, TypeScript, Material-UI

**Key Features**:
- Hot reload enabled
- Source maps for debugging
- Pre-configured ESLint and Prettier
- React DevTools support

**Development Commands**:
```bash
npm start          # Start development server
npm test           # Run tests
npm run build      # Create production build
npm run eject      # Eject from Create React App
```

### pjx-api-node (Port 8081)

**Technology**: Node.js, TypeScript, Restify

**Key Features**:
- Nodemon for auto-restart
- TypeScript compilation
- Memory caching
- Environment-based configuration

**Development Commands**:
```bash
npm start          # Start with nodemon
npm run build      # Compile TypeScript
npm test           # Run Mocha tests
```

### pjx-graphql-apollo (Port 4000)

**Technology**: Apollo Server, TypeScript, GraphQL

**Key Features**:
- GraphQL Playground at `/graphql`
- Hot reload with nodemon
- TypeScript support
- Winston logging

**Development Commands**:
```bash
npm run dev        # Start development server
npm run build      # Compile TypeScript
npm start          # Start production server
```

### pjx-api-dotnet (Port 6001)

**Technology**: .NET Core 3.1, C#, Swagger

**Key Features**:
- `dotnet watch` for hot reload
- Swagger documentation at `/swagger`
- Entity Framework Core
- Authentication integration

**Development Commands**:
```bash
dotnet watch run   # Start with hot reload
dotnet build       # Build project
dotnet test        # Run unit tests
dotnet ef migrations add <name>  # Add EF migration
```

### pjx-sso-identityserver (Ports 5001/5002)

**Technology**: IdentityServer4, .NET Core 3.1, ASP.NET Identity

**Key Features**:
- OAuth2/OpenID Connect
- HTTPS with self-signed certificate
- ASP.NET Identity integration
- Entity Framework for persistence

**Development Commands**:
```bash
dotnet watch run   # Start with hot reload
dotnet build       # Build project
dotnet ef database update  # Update database
```

## Network Configuration

All services communicate through the `pjx-network` Docker network:

```yaml
# Service communication examples
- React → GraphQL: http://pjx-graphql-apollo:4000
- GraphQL → Node API: http://pjx-api-node:8081
- React → .NET API: http://pjx-api-dotnet:80
- All → Identity Server: https://pjx-sso-identityserver:443
```

## Environment Variables

### React Application
```bash
REACT_APP_GRAPHQL_URI=http://localhost:4000/graphql
REACT_APP_API_URI=http://localhost:6001
REACT_APP_SSO_URI=https://localhost:5002
```

### Node.js Services
```bash
NODE_ENV=development
CHOKIDAR_USEPOLLING=true  # For file watching in containers
```

### .NET Services
```bash
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:80
PJX_SSO__AUTHORITY=https://pjx-sso-identityserver
```

## Troubleshooting

### Common Issues

#### Port Conflicts
```bash
# Check if ports are in use
netstat -tulpn | grep :3000

# Stop conflicting containers
docker-compose -f docker-compose.devcontainer.yml down
```

#### SSL Certificate Issues
```bash
# Trust the self-signed certificate (Identity Server)
# Follow the SSL setup guide in pjx-sso-identityserver/README.md
```

#### Node Modules Issues
```bash
# Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install
```

#### .NET Restore Issues
```bash
# Clear NuGet cache and restore
dotnet nuget locals all --clear
dotnet restore
```

### Performance Tips

1. **WSL File System**: Keep code in WSL file system (not Windows drive mapping)
2. **Docker Resources**: Allocate sufficient RAM/CPU in Docker Desktop
3. **VS Code**: Disable unnecessary extensions in containers
4. **Hot Reload**: Use polling for file watching in containers

### Debugging Network Issues

```bash
# Test service connectivity
docker exec pjx-web-react-dev curl http://pjx-graphql-apollo:4000/graphql

# Check container logs
docker-compose -f docker-compose.devcontainer.yml logs pjx-api-node

# Inspect networks
docker network inspect pjx-network
```

### VS Code Extensions

Recommended extensions are automatically installed per service:

- **All services**: Git, GitHub CLI, Path Intellisense
- **Node.js**: TypeScript, ESLint, REST Client
- **React**: React extensions, Tailwind CSS
- **GraphQL**: GraphQL syntax highlighting, Apollo
- **.NET**: C# extension, .NET Core debugging

## Contributing

1. **Create feature branch**: `git checkout -b feature/your-feature`
2. **Develop in containers**: Use appropriate devcontainer for your work
3. **Test thoroughly**: Ensure all services work together
4. **Update documentation**: Keep this guide updated with changes
5. **Create pull request**: Include tests and documentation updates

For more information, see the main [README.md](./README.md) file.