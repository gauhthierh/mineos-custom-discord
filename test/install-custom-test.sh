#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
installer='scripts/install-custom.sh'
bash -n "$installer"
bash "$installer" --help >/dev/null
bash "$installer" --repository example/mineos-custom --dir /opt/mineos-custom --dry-run >/dev/null
bash "$installer" --repository example/mineos-custom --version custom-discord-test --dir /opt/mineos-custom --dry-run >/dev/null
bash "$installer" --repository example/mineos-custom --preview --dir /opt/mineos-custom --dry-run >/dev/null
reject() {
  if bash "$installer" "$@" >/dev/null 2>&1; then
    printf 'Argument invalide accepté : %s\n' "$*" >&2
    exit 1
  fi
}
reject --repository example/repo --version ../private --dry-run
reject --repository https://evil.test --dry-run
reject --repository example/repo --dir / --dry-run
reject --repository example/repo --dir /opt/../ --dry-run
reject --repository example/repo --dir relative --dry-run
reject --repository example/repo --preview --version custom-test --dry-run
reject --repository example/repo --unknown --dry-run
reject --repository
printf '%s\n' 'Installateur : aide, simulation et refus des arguments invalides validés.'
