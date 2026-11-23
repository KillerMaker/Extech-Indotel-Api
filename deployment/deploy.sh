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
"${compose[@]}" pull
"${compose[@]}" up -d --wait --wait-timeout 300
"${compose[@]}" exec -T caddy caddy reload --config /etc/caddy/Caddyfile
curl --fail --silent --show-error --retry 12 --retry-delay 5 --retry-all-errors --max-time 5 \
  -H "Host: $PUBLIC_DOMAIN" "http://127.0.0.1:$APP_PORT/health"
echo
curl --fail --silent --show-error --retry 6 --retry-delay 5 --retry-all-errors --max-time 10 "https://$PUBLIC_DOMAIN/health"
echo
echo "API deployment is healthy."
