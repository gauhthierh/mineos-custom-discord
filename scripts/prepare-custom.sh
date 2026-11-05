#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
if [[ -e .env.custom ]]; then
  printf '%s\n' '.env.custom existe déjà : aucun fichier modifié.'
  exit 0
fi
command -v openssl >/dev/null || { printf '%s\n' 'Installe openssl avant de continuer.' >&2; exit 1; }
umask 077
# noclobber prevents concurrent invocations from replacing generated credentials.
set -o noclobber
{
  printf 'Auth__SeedUsername=admin\n'
  printf 'Auth__SeedPassword=%s\n' "$(openssl rand -hex 24)"
  printf 'Auth__JwtSecret=%s\n' "$(openssl rand -hex 32)"
  printf 'ApiKey__SeedKey=%s\n' "$(openssl rand -hex 32)"
  printf 'WEB_BIND_ADDRESS=127.0.0.1\nWEB_PORT=3000\nWEB_ORIGIN=http://localhost:3000\n'
  printf 'MC_PORT_RANGE=25565-25570\nBEDROCK_PORT_RANGE=19132-19137\nMINECRAFT_UID=1000\nMINECRAFT_GID=1000\n'
} > .env.custom
printf '%s\n' '.env.custom créé. Le mot de passe administrateur est dans ce fichier.'
printf '%s\n' 'Consulte docs/CUSTOM_DISCORD_FR.md avant le premier démarrage.'
