#!/usr/bin/env bash
# Run after updating the source files in an existing preview installation.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
[[ -f docker-compose.yml && -f scripts/prepare.sh ]] || { printf '%s\n' 'Les nouvelles sources doivent être présentes.' >&2; exit 1; }
if [[ -e .env && -e .env.custom ]]; then
  printf '%s\n' 'Deux fichiers de secrets existent. Choisis le fichier à conserver avant de migrer.' >&2
  exit 1
fi
[[ -f .env || -f .env.custom ]] || { printf '%s\n' 'Identifiants existants introuvables ; aucune modification.' >&2; exit 1; }
[[ ! -L .env && ! -L .env.custom && ! -L compose.custom.yml ]] || exit 1
if [[ -f compose.custom.yml ]]; then
  [[ ! -e docker-compose.previous.yml && ! -L docker-compose.previous.yml ]] || { printf '%s\n' 'docker-compose.previous.yml existe déjà ; aucune modification.' >&2; exit 1; }
  legacy_env=.env
  [[ ! -f .env.custom ]] || legacy_env=.env.custom
  # Stop only this Compose project. No volumes or application data are removed.
  docker compose --env-file "$legacy_env" -f compose.custom.yml stop
  mv -n -- compose.custom.yml docker-compose.previous.yml
fi
bash scripts/prepare.sh
docker compose config --quiet
docker compose up -d --build --wait --wait-timeout 180
docker compose ps
printf '%s\n' 'Migration terminée : utiliser désormais docker compose, avec .env et docker-compose.yml.'
