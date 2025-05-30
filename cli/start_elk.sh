#!/bin/sh
set -e;

docker network create myapp_shared || true;

docker compose -f setup/local/docker-compose.elk.yml -p myapp_elk up --no-build $@;