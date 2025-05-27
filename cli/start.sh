#!/bin/sh
set -e;

docker network create myapp_shared || true;

sh ./cli/build.sh;
docker compose -f setup/local/docker-compose.yml -p myapp up --no-build $@;