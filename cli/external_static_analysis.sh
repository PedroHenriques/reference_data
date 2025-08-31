#!/bin/sh
set -e;

: "${SONAR_QG_WAIT:=true}"
: "${SONAR_QG_TIMEOUT_SEC:=600}"

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

EXTRA_OPTS="";
PR_KEY="";

if [ -n "${GITHUB_EVENT_PATH:-}" ] && [ -f "${GITHUB_EVENT_PATH}" ]; then
  PR_KEY="$(grep -o '"number":[[:space:]]*[0-9]\+' "$GITHUB_EVENT_PATH" | head -1 | grep -o '[0-9]\+' || true)";
fi

if [ -z "${PR_KEY}" ] && [ -n "${GITHUB_REF:-}" ]; then
  PR_KEY="$(printf '%s\n' "${GITHUB_REF}" | sed -n 's#refs/pull/\([0-9]\+\)/.*#\1#p')";
fi

if [ -n "${PR_KEY}" ]; then
  EXTRA_OPTS="$EXTRA_OPTS /d:sonar.pullrequest.key=${PR_KEY} /d:sonar.pullrequest.branch=${GITHUB_HEAD_REF} /d:sonar.pullrequest.base=${GITHUB_BASE_REF}";
else
  EXTRA_OPTS="$EXTRA_OPTS /d:sonar.branch.name=${GITHUB_REF_NAME}";
fi

TEST_COVERAGE_PATH="./test/**/${TEST_COVERAGE_FILE_NAME}";
CMD="dotnet tool restore && dotnet sonarscanner begin /k:"${EXTERNAL_STATIC_ANALYSIS_PROJ_KEY}" /o:"${EXTERNAL_STATIC_ANALYSIS_ORG}" /d:sonar.token="${EXTERNAL_STATIC_ANALYSIS_TOKEN}" /d:sonar.host.url="${EXTERNAL_STATIC_ANALYSIS_HOST}" /d:sonar.cs.opencover.reportsPaths="${TEST_COVERAGE_PATH}" /d:sonar.projectBaseDir=/app /d:sonar.exclusions=**/bin/**,**/obj/**,setup/**,app/setup/** /d:sonar.coverage.exclusions=setup/**,app/setup/** ${EXTRA_OPTS} /d:sonar.qualitygate.wait=${SONAR_QG_WAIT} /d:sonar.qualitygate.timeout=${SONAR_QG_TIMEOUT_SEC} && dotnet build && chmod +x ./cli/test.sh && ./cli/test.sh --coverage && dotnet sonarscanner end /d:sonar.token="${EXTERNAL_STATIC_ANALYSIS_TOKEN}"";

if [ $USE_DOCKER -eq 1 ]; then
  INTERACTIVE_FLAGS="-it";
  if [ $RUNNING_IN_PIPELINE -eq 1 ]; then
    INTERACTIVE_FLAGS="-i";
  fi

  docker run --rm ${INTERACTIVE_FLAGS} -v "./:/app/" -w "/app/" mcr.microsoft.com/dotnet/sdk:8.0-noble /bin/sh -c "${CMD}";
else
  eval "${CMD}";
fi
