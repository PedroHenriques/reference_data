#!/bin/sh
set -e;

docker compose -f setup/local/docker-compose.yml -p myapp build;
docker compose -f setup/local/docker-compose.yml -p myapp up --no-build $1;