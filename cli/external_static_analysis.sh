#!/bin/sh
set -e;

USE_DOCKER=0;
RUNNING_IN_PIPELINE=0;

while [ "$#" -gt 0 ]; do
  case "$1" in
    --docker) USE_DOCKER=1; shift 1;;
    --cicd) RUNNING_IN_PIPELINE=1; USE_DOCKER=1; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
  esac
done

rm -rf .sonarqube;

TEST_COVERAGE_PATH="./test/**/${TEST_COVERAGE_FILE_NAME}";
CMD="dotnet tool restore && dotnet sonarscanner begin /k:"${EXTERNAL_STATIC_ANALYSIS_PROJ_KEY}" /o:"${EXTERNAL_STATIC_ANALYSIS_ORG}" /d:sonar.token="${EXTERNAL_STATIC_ANALYSIS_TOKEN}" /d:sonar.host.url="${EXTERNAL_STATIC_ANALYSIS_HOST}" /d:sonar.cs.opencover.reportsPaths="${TEST_COVERAGE_PATH}" /d:sonar.projectBaseDir=/app && dotnet build && chmod +x ./cli/test.sh && ./cli/test.sh --coverage && dotnet sonarscanner end /d:sonar.token="${EXTERNAL_STATIC_ANALYSIS_TOKEN}"";
echo "${CMD}";

if [ $USE_DOCKER -eq 1 ]; then
  INTERACTIVE_FLAGS="-it";
  if [ $RUNNING_IN_PIPELINE -eq 1 ]; then
    INTERACTIVE_FLAGS="-i";
  fi

  docker run --rm ${INTERACTIVE_FLAGS} -v "./:/app/" -w "/app/" mcr.microsoft.com/dotnet/sdk:8.0-noble /bin/sh -c "${CMD}";
else
  eval "${CMD}";
fi
