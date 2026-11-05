#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
installer='install.sh'
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

# Exercise secret generation and one-time migration in a disposable project.
project_root="$PWD"
work_dir=$(mktemp -d)
trap 'rm -rf -- "$work_dir"' EXIT
mkdir -p "$work_dir/project/scripts" "$work_dir/bin"
cp scripts/prepare.sh scripts/migrate-layout.sh "$work_dir/project/scripts/"
cp docker-compose.yml "$work_dir/project/"
cd "$work_dir/project"
bash scripts/prepare.sh >/dev/null
[[ -f .env && ! -e .env.custom ]]
grep -q '^WEB_PORT=6070$' .env
password=$(sed -n 's/^Auth__SeedPassword=//p' .env)
[[ "$password" =~ ^[0-9a-f]{48}$ ]]
cp .env expected.env
bash scripts/prepare.sh >/dev/null
cmp .env expected.env
mv .env .env.custom
bash scripts/prepare.sh >/dev/null
cmp .env expected.env
[[ ! -e .env.custom ]]
# Migration stops the legacy project and uses the standard commands afterwards.
cat > "$work_dir/bin/docker" <<'MOCK'
#!/usr/bin/env bash
printf '%s\n' "$*" >> docker-calls.txt
MOCK
chmod +x "$work_dir/bin/docker"
export PATH="$work_dir/bin:$PATH"
cp .env .env.custom
if bash scripts/migrate-layout.sh >/dev/null 2>&1; then
  printf '%s\n' 'Conflicting credentials were accepted.' >&2; exit 1
fi
[[ ! -e docker-calls.txt ]]
rm .env
printf 'services: {}\n' > compose.custom.yml
mkdir data minecraft
printf 'database-placeholder' > data/keep
printf 'world-placeholder' > minecraft/keep
bash scripts/migrate-layout.sh >/dev/null
cmp .env expected.env
[[ ! -e .env.custom && ! -e compose.custom.yml && -f docker-compose.previous.yml ]]
grep -qx 'compose --env-file .env.custom -f compose.custom.yml stop' docker-calls.txt
grep -qx 'compose config --quiet' docker-calls.txt
grep -qx 'compose up -d --build --wait --wait-timeout 180' docker-calls.txt
[[ "$(cat data/keep)" == database-placeholder && "$(cat minecraft/keep)" == world-placeholder ]]
printf '%s\n' 'Préparation et migration : identifiants, réglages et données conservés.'
