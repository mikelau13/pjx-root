# Guide: Integrating pjx-api-dotnet into pjx-root

This document provides instructions for merging the `pjx-api-dotnet` repository into `pjx-root` as a consolidated monorepo approach.

## ✅ COMPLETED INTEGRATION

The `pjx-api-dotnet` repository has been successfully integrated into `pjx-root` using the **Git Subtree** approach.

### What was done:

1. ✅ **Added Git Subtree**: `pjx-api-dotnet` repository integrated as a subtree
2. ✅ **Updated Docker Compose**: Modified `docker-compose.yml` to reference local `./pjx-api-dotnet/` instead of `./projects/pjx-api-dotnet/`
3. ✅ **Updated README**: Removed `pjx-api-dotnet` from clone instructions and marked it as integrated
4. ✅ **Preserved History**: Full git history of pjx-api-dotnet is maintained

### Current Structure:

```
pjx-root/
├── pjx-api-dotnet/          # ← INTEGRATED (was external)
│   ├── src/
│   │   └── Pjx_Api/
│   │       └── Dockerfile
│   └── README.md
├── projects/                # ← Still used for other services
├── docker-compose.yml       # ← Updated to use local pjx-api-dotnet
└── README.md               # ← Updated with new instructions
```

## Current Architecture

Currently, `pjx-root` serves as an orchestration repository that:
- Contains Docker Compose configuration for all microservices
- **NOW INCLUDES** `pjx-api-dotnet` as an integrated component
- Uses a `/projects` folder for remaining external repositories (4 remaining)
- Orchestrates: pjx-web-react, pjx-graphql-apollo, pjx-api-node, pjx-sso-identityserver (external) + pjx-api-dotnet (integrated)

## New Setup Instructions

### For New Users:

```bash
# 1. Clone pjx-root (pjx-api-dotnet is now included!)
git clone https://github.com/mikelau13/pjx-root.git
cd pjx-root

# 2. Clone remaining external services
cd ./projects
git clone https://github.com/mikelau13/pjx-graphql-apollo.git
git clone https://github.com/mikelau13/pjx-api-node.git
git clone https://github.com/mikelau13/pjx-sso-identityserver.git
git clone https://github.com/mikelau13/pjx-web-react.git
cd ..

# 3. Run the entire solution
docker-compose up
```

### For Existing Users:

```bash
# 1. Pull latest changes (includes pjx-api-dotnet integration)
git pull origin main

# 2. Remove old pjx-api-dotnet from projects folder if exists
rm -rf ./projects/pjx-api-dotnet

# 3. Run as normal
docker-compose up
```

## Managing the Integrated pjx-api-dotnet

### Making Changes:
- Edit files directly in `./pjx-api-dotnet/`
- Commit changes to pjx-root repository
- Changes will be part of pjx-root's git history

### Pulling Updates from Original Repository:
If you need to pull updates from the original pjx-api-dotnet repository:

```bash
git subtree pull --prefix=pjx-api-dotnet pjx-api-dotnet master --squash
```

### Pushing Changes Back to Original Repository:
If you want to contribute changes back to the original pjx-api-dotnet repository:

```bash
# Create a feature branch in original repo first
git subtree push --prefix=pjx-api-dotnet pjx-api-dotnet feature-branch-name
```

## Future Integration Options

You could apply the same integration approach to other services:

### Next Candidates for Integration:
1. **pjx-api-node** - Similar backend API service
2. **pjx-sso-identityserver** - Identity server
3. **pjx-graphql-apollo** - GraphQL gateway
4. **pjx-web-react** - Frontend application

### Integration Commands for Other Services:
```bash
# For pjx-api-node:
git remote add pjx-api-node https://github.com/mikelau13/pjx-api-node.git
git subtree add --prefix=pjx-api-node pjx-api-node master --squash

# For pjx-sso-identityserver:
git remote add pjx-sso-identityserver https://github.com/mikelau13/pjx-sso-identityserver.git
git subtree add --prefix=pjx-sso-identityserver pjx-sso-identityserver master --squash

# etc...
```

## Benefits Achieved

1. **Simplified Setup**: Users no longer need to clone pjx-api-dotnet separately
2. **Unified Development**: Can develop pjx-api-dotnet alongside the orchestration
3. **History Preservation**: Full git history maintained
4. **Future Flexibility**: Can still sync with original repository
5. **Reduced Complexity**: One less external dependency to manage

## Notes for Kubernetes/Helm

The Kubernetes and Helm configurations currently use pre-built Docker images (`mikelauawaremd/pjx-api-dotnet:v0.0.1`). These don't need immediate changes, but if you want to build from source in Kubernetes, you would need to:

1. Set up a build pipeline that builds from the integrated source
2. Update the image references in:
   - `kubernetes/pjx-api-dotnet.yaml`
   - `helm-pjx/templates/pjx-api-dotnet.yaml`

## Testing

The integration has been configured but should be tested by:

1. **Docker Compose Test**: Run `docker-compose up` and verify all services start
2. **API Accessibility**: Check that pjx-api-dotnet endpoints are accessible
3. **Service Communication**: Verify other services can communicate with the .NET API
4. **Development Workflow**: Make a test change to pjx-api-dotnet code and verify it rebuilds