#!/bin/sh
set -e;

WATCH=0;
PROJ="";
USE_DOCKER=0;

while [ "$#" -gt 0 ]; do
  case "$1" in
    -w|--watch) WATCH=1; shift 1;;
    --docker) USE_DOCKER=1; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
    *) PROJ=$1; shift 1;;
  esac
done

CMD="dotnet test ${PROJ}";

if [ $WATCH -eq 1 ]; then
  if [ -z "$PROJ" ]; then
    echo "In watch mode a project name or path must be provided as argument." >&2; exit 1;
  fi

  CMD="dotnet watch test -q --project ${PROJ}";
fi

if [ $USE_DOCKER -eq 1 ]; then
  docker run --rm -it -v "./:/app/" -w "/app/" -u $(id -u ${USER}):$(id -g ${USER}) mcr.microsoft.com/dotnet/sdk:8.0-noble /bin/sh -c "$CMD";
else
  $CMD;
fi