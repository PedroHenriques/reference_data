#!/bin/sh
set -e;

UPDATE=0;
UPDATE_PROMPT=1;
USE_DOCKER=0;
RUNNING_IN_PIPELINE=0;

while [ "$#" -gt 0 ]; do
  case "$1" in
    -u|--update) UPDATE=1; shift 1;;
    -y) UPDATE_PROMPT=0; shift 1;;
    --docker) USE_DOCKER=1; shift 1;;
    --cicd) RUNNING_IN_PIPELINE=1; USE_DOCKER=1; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
  esac
done

OPTS="";
if [ $UPDATE -eq 1 ]; then
  OPTS="${OPTS} -u";
  if [ $UPDATE_PROMPT -eq 1 ]; then
    OPTS="${OPTS}:Prompt";
  fi
fi

CMD="dotnet tool restore; dotnet outdated ${OPTS} ./*.sln";
if [ $USE_DOCKER -eq 1 ]; then
  INTERACTIVE_FLAGS="-it";
  if [ $RUNNING_IN_PIPELINE -eq 1 ]; then
    INTERACTIVE_FLAGS="-i";
  fi

  docker run --rm ${INTERACTIVE_FLAGS} -v "./:/app/" -w "/app/" mcr.microsoft.com/dotnet/sdk:8.0-noble /bin/sh -c "${CMD}";
else
  eval "${CMD}";
fi