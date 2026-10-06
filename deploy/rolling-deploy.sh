#!/usr/bin/env bash
# Rolling deploy of an already-built tag: one replica at a time, so nginx always has a healthy API behind it.
#   COMPOSE_FILE_PATH=<release>/docker-compose.yml ./deploy/rolling-deploy.sh <tag>
# Images worktime-api:<tag> / worktime-web:<tag> must exist; this script never builds.
# The compose file must be the one released WITH that tag: config travels with the version, so a rollback
# restores the old configuration too, not just the old images.
# Secrets come from the environment (JWT_SIGNING_KEY, POSTGRES_PASSWORD, ...), see docker-compose.yml.
set -euo pipefail

TAG="${1:?usage: rolling-deploy.sh <tag>}"
KEEP_IMAGES="${KEEP_IMAGES:-5}"
COMPOSE_FILE_PATH="${COMPOSE_FILE_PATH:-$(dirname "$0")/../docker-compose.yml}"
export WORKTIME_TAG="$TAG"

compose() { docker compose -f "$COMPOSE_FILE_PATH" "$@"; }

wait_healthy() {
  local svc="$1" id status
  id="$(compose ps -q "$svc")"
  for _ in $(seq 1 60); do
    status="$(docker inspect -f '{{.State.Health.Status}}' "$id" 2>/dev/null || echo missing)"
    if [ "$status" = "healthy" ]; then echo "  $svc healthy"; return 0; fi
    sleep 2
  done
  echo "  $svc did not become healthy (last status: $status)" >&2
  compose logs --tail 60 "$svc" >&2 || true
  return 1
}

echo "==> Deploying worktime $TAG"
compose up -d --no-build postgres redis
wait_healthy postgres
wait_healthy redis

# One replica at a time: while api-1 restarts (and migrates, under an advisory lock), api-2 keeps serving.
for svc in api-1 api-2; do
  echo "--> $svc"
  compose up -d --no-build --no-deps "$svc"
  wait_healthy "$svc"
done

echo "--> nginx"
compose up -d --no-build --no-deps nginx

echo "==> Smoke test through nginx"
docker run --rm --network worktime_default curlimages/curl:8.11.1 \
  -fsS --retry 15 --retry-delay 2 --retry-all-errors http://nginx/health
echo
docker run --rm --network worktime_default curlimages/curl:8.11.1 \
  -fsS -o /dev/null -w "  /es/ -> %{http_code}\n" http://nginx/es/

# Keep the last N builds for instant rollback; never remove the one just deployed.
for repo in worktime-api worktime-web; do
  docker images "$repo" --format '{{.Tag}}' \
    | grep -E '^[0-9]+-' | sort -t- -k1,1nr | tail -n +"$((KEEP_IMAGES + 1))" \
    | grep -vx "$TAG" | xargs -r -I{} docker rmi "$repo:{}" >/dev/null || true
done

echo "==> worktime $TAG is live"
