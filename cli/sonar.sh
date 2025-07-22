#!/bin/sh
set -e;

USE_DOCKER=0;
RUNNING_IN_PIPELINE=0;
START=0;
END=0;

while [ "$#" -gt 0 ]; do
  case "$1" in
    --docker) USE_DOCKER=1; shift 1;;
    --cicd) RUNNING_IN_PIPELINE=1; USE_DOCKER=1; shift 1;;
    --start) START=1; shift 1;;
    --end) END=1; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
  esac
done

TEST_COVERAGE_PATH="${TEST_COVERAGE_DIR_PATH}/${TEST_COVERAGE_FILE_NAME}";
# if [ $START -eq 1 ]; then
#   CMD="dotnet tool restore && dotnet sonarscanner begin /k:"${SONAR_PROJ_KEY}" /o:"${SONAR_ORG}" /d:sonar.token="${SONAR_TOKEN}" /d:sonar.host.url="${SONAR_HOST}" /d:sonar.cs.opencover.reportsPaths="${TEST_COVERAGE_PATH}";";
# elif [ $END -eq 1 ]; then
#   CMD="dotnet tool restore && dotnet build && dotnet sonarscanner end /d:sonar.token="${SONAR_TOKEN}"";
# fi

CMD="dotnet tool restore && dotnet sonarscanner begin /k:"${SONAR_PROJ_KEY}" /o:"${SONAR_ORG}" /d:sonar.token="${SONAR_TOKEN}" /d:sonar.host.url="${SONAR_HOST}" /d:sonar.cs.opencover.reportsPaths="${TEST_COVERAGE_PATH}" && dotnet build && chmod +x ./cli/coverage.sh && ./cli/coverage.sh && dotnet sonarscanner end /d:sonar.token="${SONAR_TOKEN}"";

if [ $USE_DOCKER -eq 1 ]; then
  INTERACTIVE_FLAGS="-it";
  if [ $RUNNING_IN_PIPELINE -eq 1 ]; then
    INTERACTIVE_FLAGS="-i";
  fi

  docker run --rm ${INTERACTIVE_FLAGS} -v "./:/app/" -w "/app/" -e TEST_COVERAGE_DIR_PATH mcr.microsoft.com/dotnet/sdk:8.0-noble /bin/sh -c "${CMD}";
else
  eval "${CMD}";
fi