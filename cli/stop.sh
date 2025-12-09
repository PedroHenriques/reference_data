#!/bin/sh
set -e;

COMPOSE_PROFILES="${COMPOSE_PROFILES:-}";
KEPT_ARGS="";

while [ "$#" -gt 0 ]; do
  case "$1" in
    *) KEPT_ARGS="$KEPT_ARGS '$(printf %s "${1}" | sed "s/'/'\\\\''/g")'"; shift;; # append a single-quoted, safely-escaped copy of "$1"
  esac
done

# reset positional params to the kept args (preserves spaces/special chars)
eval "set -- ${KEPT_ARGS}";

if [ -z "$COMPOSE_PROFILES" ]; then
  COMPOSE_PROFILES="only_if_not_cicd";
else
  COMPOSE_PROFILES="${COMPOSE_PROFILES},only_if_not_cicd";
fi

export COMPOSE_PROFILES;

docker compose -f setup/local/docker-compose.yml -p myapp down;
docker compose -f setup/local/docker-compose.elk.yml -p myapp_elk down;
docker system prune -f --volumes;
