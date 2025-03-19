#!/bin/sh
set -e;

WATCH=0;
PROJ="";
FILTERS="";
USE_DOCKER=0;
RUNNING_IN_PIPELINE=0;
RUN_LOCAL_ENV=0;
TEST_TYPE="";
COVERAGE="";

while [ "$#" -gt 0 ]; do
  case "$1" in
    -w|--watch) WATCH=1; shift 1;;
    --docker) USE_DOCKER=1; shift 1;;
    --cicd) RUNNING_IN_PIPELINE=1; USE_DOCKER=1; shift 1;;
    --filter) FILTERS="--filter ${2}"; shift 2;;
    --unit) FILTERS="--filter Type=Unit"; TEST_TYPE="unit"; shift 1;;
    --integration) FILTERS="--filter Type=Integration"; TEST_TYPE="integration"; RUN_LOCAL_ENV=1; shift 1;;
    --e2e) FILTERS="--filter Type=E2E"; TEST_TYPE="e2e"; RUN_LOCAL_ENV=1; shift 1;;
    --coverage) COVERAGE="--collect:\"XPlat Code Coverage\""; FILTERS="--filter Type=Unit"; TEST_TYPE="unit"; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
    *) PROJ=$1; shift 1;;
  esac
done

case "${TEST_TYPE}" in
  "unit")
    if [ ! -d "./test/unit/" ]; then
      echo "No './test/unit/' directory found. Assuming no unit tests exist.";
      exit 0;
    fi
    ;;
  "integration")
    if [ ! -d "./test/integration/" ]; then
      echo "No './test/integration/' directory found. Assuming no integration tests exist.";
      exit 0;
    fi
    ;;
  "e2e")
    if [ ! -d "./test/e2e/" ]; then
      echo "No './test/e2e/' directory found. Assuming no e2e tests exist.";
      exit 0;
    fi
    ;;
esac

if [ $RUN_LOCAL_ENV -eq 1 ]; then
  sh ./cli/start.sh;
fi

CMD="dotnet test ${FILTERS} ${COVERAGE} ${PROJ}";

if [ $WATCH -eq 1 ]; then
  if [ -z "$PROJ" ]; then
    echo "In watch mode a project name or path must be provided as argument." >&2; exit 1;
  fi

  CMD="dotnet watch test -q --project ${PROJ} ${FILTERS}";
fi

if [ $USE_DOCKER -eq 1 ]; then
  INTERACTIVE_FLAGS="-it";
  if [ $RUNNING_IN_PIPELINE -eq 1 ]; then
    INTERACTIVE_FLAGS="-i";
  fi

  docker run --rm ${INTERACTIVE_FLAGS} -v "./:/app/" -w "/app/" mcr.microsoft.com/dotnet/sdk:8.0-noble /bin/sh -c "${CMD}";
else
  eval "${CMD}";
fi