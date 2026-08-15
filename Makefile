# pjx-root — one entry point for the three Docker Compose stacks.
#
#   pjx-root    docker-compose.devcontainer.yml   devcontainer + 5 app services
#   pjx-otel    observability/docker-compose.yml  Grafana LGTM
#   pjx-router  local/docker-compose.yml          Traefik
#
# Run `make down` from a HOST terminal. It stops the devcontainer, so running it
# from inside the devcontainer would kill the shell executing it — the guard
# below detects that and skips the devcontainer with a note.
#
# `stop` is used throughout, never `down`. Stopped containers use no CPU and no
# memory, and everything survives: SQLite databases, node_modules volumes, and
# the Grafana dashboards in pjx-otel_grafana-data. Use local/scripts/clean.sh
# only when you actually want to discard state.

SHELL := /bin/bash

DEV_COMPOSE  := docker-compose.devcontainer.yml
OBS_COMPOSE  := observability/docker-compose.yml
ROUTER_COMPOSE := local/docker-compose.yml

# Every service in the devcontainer stack except `workspace` itself.
APP_SERVICES = $(shell docker compose -f $(DEV_COMPOSE) config --services 2>/dev/null | grep -v '^workspace$$' | sort | tr '\n' ' ')

# /.dockerenv exists inside a container, so this is "am I in the devcontainer?"
IN_CONTAINER := $(shell [ -f /.dockerenv ] && echo yes || echo no)

.DEFAULT_GOAL := help
.PHONY: help up down restart status logs urls

help:  ## Show this help
	@echo ""
	@echo "  pjx-root — make <target>"
	@echo ""
	@grep -E '^[a-z-]+:.*?## .*$$' $(MAKEFILE_LIST) \
	  | awk 'BEGIN{FS=":.*?## "}{printf "    \033[1m%-10s\033[0m %s\n", $$1, $$2}'
	@echo ""
	@echo "  Run 'make down' from a HOST terminal to release the devcontainer too."
	@echo ""

up:  ## Start Traefik, the app services and Grafana
	@echo "==> Traefik"
	@docker compose -f $(ROUTER_COMPOSE) up -d
	@echo "==> app services"
	@docker compose -f $(DEV_COMPOSE) up -d $(APP_SERVICES)
	@echo "==> Grafana"
	@docker compose -f $(OBS_COMPOSE) up -d
	@echo ""
	@echo "Up. The devcontainer starts when you open the folder in VS Code."
	@$(MAKE) --no-print-directory status

down:  ## Stop everything, including the devcontainer
	@echo "==> app services"
	@docker compose -f $(DEV_COMPOSE) stop $(APP_SERVICES) 2>/dev/null || true
	@echo "==> Grafana"
	@docker compose -f $(OBS_COMPOSE) stop 2>/dev/null || true
	@echo "==> Traefik"
	@docker compose -f $(ROUTER_COMPOSE) stop 2>/dev/null || true
ifeq ($(IN_CONTAINER),yes)
	@echo ""
	@echo "!! Skipped the devcontainer — you are running inside it."
	@echo "   Close the VS Code window (shutdownAction: stopCompose) or run"
	@echo "   'make down' again from a host terminal to release its ~1.7 GB."
else
	@echo "==> devcontainer"
	@docker compose -f $(DEV_COMPOSE) stop 2>/dev/null || true
endif
	@echo ""
	@$(MAKE) --no-print-directory status

restart: down up  ## Stop everything, then start it again

status:  ## Show every pjx container, its state and memory use
	@printf '  %-30s %-10s %s\n' CONTAINER STATE MEMORY
	@printf '  %-30s %-10s %s\n' '------------------------------' '----------' '----------'
	@for p in pjx-root pjx-otel pjx-router; do \
	    docker ps -a --filter label=com.docker.compose.project=$$p \
	                 --format '{{.Names}}\t{{.State}}'; \
	  done | sort | while IFS=$$'\t' read -r n s; do \
	    if [ "$$s" = "running" ]; then \
	        m=$$(docker stats --no-stream --format '{{.MemUsage}}' "$$n" 2>/dev/null | cut -d/ -f1 | xargs); \
	    else m='-'; fi; \
	    printf '  %-30s %-10s %s\n' "$$n" "$$s" "$$m"; \
	done
	@echo ""
	@n=$$(docker ps -q --filter name=pjx | wc -l); echo "  $$n running"

logs:  ## Tail the app service logs (Ctrl+C to stop)
	@docker compose -f $(DEV_COMPOSE) logs -f --tail=50 $(APP_SERVICES)

urls:  ## Print the service URLs
	@echo ""
	@echo "  https://pjx.test           React web app"
	@echo "  https://api.pjx.test       .NET API (/swagger)"
	@echo "  https://ql.pjx.test        GraphQL (/graphql)"
	@echo "  https://node.pjx.test      Node API"
	@echo "  https://sso.pjx.test       IdentityServer"
	@echo "  https://grafana.pjx.test   Grafana (admin/admin)"
	@echo "  http://localhost:9091      Traefik dashboard"
	@echo ""
