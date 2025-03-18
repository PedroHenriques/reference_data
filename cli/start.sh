#!/bin/sh
set -e;

sh ./cli/build.sh;
docker compose -f setup/local/docker-compose.yml -p myapp up --no-build $1;