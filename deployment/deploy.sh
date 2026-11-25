#!/usr/bin/env bash
set -euo pipefail
umask 077
set -a
source ./.env
set +a
chmod 600 runtime.compose.json sql-init.sql

if ! docker network inspect "$DOCKER_NETWORK" >/dev/null 2>&1; then
  docker network create --subnet "$DOCKER_SUBNET" "$DOCKER_NETWORK" >/dev/null ||
    docker network inspect "$DOCKER_NETWORK" >/dev/null
fi
actual_subnet=$(docker network inspect --format '{{(index .IPAM.Config 0).Subnet}}' "$DOCKER_NETWORK")
if [ "$actual_subnet" != "$DOCKER_SUBNET" ]; then
  echo "The Docker network subnet does not match DOCKER_SUBNET." >&2
  exit 1
fi

compose=(docker compose --env-file .env -f docker-compose.yml -f runtime.compose.json)
"${compose[@]}" config --quiet
# Refuse a changed or missing volume after the first successful SQL startup.
# The marker lives in the deployment directory and is never uploaded/replaced by CI.
sql_volume="${COMPOSE_PROJECT_NAME}_sql_data"
if [ -f .sql-data-volume ]; then
  recorded_volume=$(cat .sql-data-volume)
  if [ "$recorded_volume" != "$sql_volume" ] || ! docker volume inspect "$recorded_volume" >/dev/null 2>&1; then
    echo "SQL data volume changed or is missing. Restore/verify the existing volume before deploying." >&2
    exit 1
  fi
fi
case "${RECREATE_DATABASE_CONTAINER:-false}" in
  false) sql_flags=(--no-recreate) ;;
  true) sql_flags=(--force-recreate) ;;
  *) echo "Invalid RECREATE_DATABASE_CONTAINER value." >&2; exit 1 ;;
esac
"${compose[@]}" pull
"${compose[@]}" up -d --no-deps --wait --wait-timeout 180 kafka
"${compose[@]}" up -d --no-deps --no-recreate --wait --wait-timeout 120 redis
"${compose[@]}" up -d --no-deps "${sql_flags[@]}" --wait --wait-timeout 300 sqlserver
docker volume inspect "$sql_volume" >/dev/null
printf '%s\n' "$sql_volume" > .sql-data-volume
# Apply additive schema initialization/migrations against the retained database.
"${compose[@]}" run --rm --no-deps -T sql-init
"${compose[@]}" up -d --no-deps --wait --wait-timeout 120 api caddy
"${compose[@]}" exec -T caddy caddy reload --config /etc/caddy/Caddyfile
curl --fail --silent --show-error --retry 12 --retry-delay 5 --retry-all-errors --max-time 5 \
  -H "Host: $PUBLIC_DOMAIN" "http://127.0.0.1:$APP_PORT/health"
echo
curl --fail --silent --show-error --retry 6 --retry-delay 5 --retry-all-errors --max-time 10 "https://$PUBLIC_DOMAIN/health"
echo
echo "API deployment is healthy."
