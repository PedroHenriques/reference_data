#!/bin/sh
set -e;

docker compose -f setup/local/docker-compose.yml -p myapp down;
docker system prune -f --volumes;