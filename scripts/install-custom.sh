#!/usr/bin/env bash
# Compatibility for the first published installation URL.
set -euo pipefail
curl --proto '=https' --tlsv1.2 -fsSL https://raw.githubusercontent.com/GauhthierH/mineos-custom-discord/vibing/install.sh | bash -s -- "$@"
