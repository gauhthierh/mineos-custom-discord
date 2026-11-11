#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
umask 077
[[ ! -L .env && ! -L .env.custom ]] || { printf '%s\n' 'Refus de suivre un lien pour les identifiants.' >&2; exit 1; }
if [[ -e .env ]]; then
  printf '%s\n' '.env existe déjà : identifiants conservés.'
  exit 0
fi
if [[ -f .env.custom ]]; then
  # One-time migration, without parsing or regenerating any secret.
  mv -n -- .env.custom .env
  chmod 600 .env
  printf '%s\n' 'Identifiants existants déplacés dans .env ; aucun secret régénéré.'
  exit 0
fi
command -v openssl >/dev/null || { printf '%s\n' 'Installe openssl avant de continuer.' >&2; exit 1; }
set -o noclobber
{
  printf 'ConnectionStrings__DefaultConnection=Data Source=/app/data/mineos.db\n'
  printf 'Auth__SeedUsername=admin\n'
  printf 'Auth__SeedPassword=%s\n' "$(openssl rand -hex 24)"
  printf 'Auth__JwtSecret=%s\n' "$(openssl rand -hex 32)"
  printf 'ApiKey__SeedKey=%s\n' "$(openssl rand -hex 32)"
  printf 'HOST_BASE_DIRECTORY=./minecraft\nData__Directory=./data\n'
  printf 'WEB_BIND_ADDRESS=0.0.0.0\nWEB_PORT=6070\nORIGIN=http://localhost:6070\n'
  printf 'MC_PORT_RANGE=25565-25570\nBEDROCK_PORT_RANGE=19132-19137\nHost__OwnerUid=1000\nHost__OwnerGid=1000\n'
  printf 'MINEOS_TELEMETRY_ENABLED=false\n'
  printf 'BODY_SIZE_LIMIT=10737418240\nWEB_REQUEST_TIMEOUT_MS=86400000\n'
} > .env
printf '%s\n' '.env créé. Le compte administrateur et son mot de passe sont dans ce fichier.'
