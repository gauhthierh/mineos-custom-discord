#!/usr/bin/env bash
set -euo pipefail

# The release workflow rewrites this default when published from another fork.
repository='GauhthierH/mineos-custom-discord'
install_dir="${HOME}/mineos"
version=''
preview=false
dry_run=false

fail() { printf 'Erreur : %s\n' "$*" >&2; exit 1; }
usage() {
  cat <<'EOF'
MineOS custom — installation neuve sur Debian 13
Options :
  --preview              Dernière préversion custom publiée
  --version custom-TAG   Version custom précise
  --dir /chemin          Dossier d'installation (défaut : ~/mineos)
  --repository NOM/DEPOT Dépôt GitHub public (renseigné dans le script publié)
  --dry-run              Afficher le choix sans rien installer ni télécharger
  --help                 Afficher cette aide

Le script installe les prérequis et Docker si nécessaire, télécharge notre
version, vérifie son empreinte, génère les identifiants et construit les images.
Il refuse d'écraser un dossier existant. Le panel écoute sur le port 6070. Les fichiers utilisés sont .env et docker-compose.yml.
EOF
}

while (( $# )); do
  case "$1" in
    --preview) preview=true; shift ;;
    --dry-run) dry_run=true; shift ;;
    --help|-h) usage; exit 0 ;;
    --version|--dir|--repository)
      (( $# >= 2 )) && [[ -n "$2" && "$2" != --* ]] || fail "Valeur manquante pour $1."
      case "$1" in
        --version) version="$2" ;;
        --dir) install_dir="$2" ;;
        --repository) repository="$2" ;;
      esac
      shift 2 ;;
    *) fail "Option inconnue : $1" ;;
  esac
done

[[ "$repository" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$ ]] \
  || fail 'Le dépôt custom doit être renseigné avant publication (--repository NOM/DEPOT).'
[[ -z "$version" || "$version" =~ ^custom-[A-Za-z0-9][A-Za-z0-9._-]{0,100}$ ]] \
  || fail 'La version doit être un tag custom-*.'
[[ "$preview" != true || -z "$version" ]] || fail '--preview et --version ne se combinent pas.'
[[ "$install_dir" == /* && "$install_dir" != / && "$install_dir" != *$'\n'* && "$install_dir" != *$'\r'* ]] \
  || fail "Le dossier doit être un chemin absolu distinct de /."
# Refuse traversal before passing the path to privileged filesystem commands.
[[ "/${install_dir#/}/" != *'/../'* && "/${install_dir#/}/" != *'/./'* ]] || fail 'Chemin non normalisé.'
if [[ "$dry_run" == true ]]; then
  printf 'Dépôt : %s\nDossier : %s\nVersion : %s\n' "$repository" "$install_dir" "${version:-latest}"
  printf 'Préversion : %s\nAucun changement effectué.\n' "$preview"
  exit 0
fi

[[ -f /etc/os-release ]] || fail 'Ce script nécessite Debian 13.'
# shellcheck disable=SC1091
. /etc/os-release
[[ "${ID:-}" == debian && "${VERSION_ID:-}" == 13 ]] || fail 'Ce script cible une installation neuve sur Debian 13.'
[[ ! -e "$install_dir" && ! -L "$install_dir" ]] || fail "Le dossier $install_dir existe déjà. Aucun fichier remplacé."
architecture=$(dpkg --print-architecture)
[[ "$architecture" == amd64 || "$architecture" == arm64 ]] || fail 'Architecture prise en charge : amd64 ou arm64.'

root_command=()
if (( EUID != 0 )); then
  command -v sudo >/dev/null || fail 'Exécute ce script avec un compte disposant de sudo.'
  sudo -v
  root_command=(sudo)
fi
as_root() { "${root_command[@]}" "$@"; }
download() { curl --proto '=https' --tlsv1.2 -fsSL --retry 3 --connect-timeout 20 --max-time 300 "$1" -o "$2"; }

printf '%s\n' 'Préparation des outils nécessaires…'
as_root apt-get update
as_root apt-get install -y ca-certificates curl tar gzip openssl python3
if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
  for package in docker.io docker-compose docker-doc docker-buildx podman-docker containerd runc; do
    if [[ "$(dpkg-query -W -f='${db:Status-Status}' "$package" 2>/dev/null || true)" == installed ]]; then
      fail "Le paquet $package est déjà installé. Vérifie l'installation Docker existante avant de continuer."
    fi
  done
  printf '%s\n' 'Installation de Docker depuis son dépôt officiel…'
  as_root install -m 0755 -d /etc/apt/keyrings
  as_root curl --proto '=https' --tlsv1.2 -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  as_root chmod a+r /etc/apt/keyrings/docker.asc
  printf 'Types: deb\nURIs: https://download.docker.com/linux/debian\nSuites: trixie\nComponents: stable\nArchitectures: %s\nSigned-By: /etc/apt/keyrings/docker.asc\n' "$architecture" \
    | as_root tee /etc/apt/sources.list.d/mineos-custom-docker.sources >/dev/null
  as_root apt-get update
  as_root apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
as_root systemctl enable --now docker
as_root docker info >/dev/null

work_dir=$(mktemp -d /tmp/mineos-custom.XXXXXXXX)
cleanup() {
  # Only the exact temporary directory created above may be removed.
  if [[ "$work_dir" == /tmp/mineos-custom.* && -d "$work_dir" && ! -L "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
}
trap cleanup EXIT
api="https://api.github.com/repos/$repository/releases"
if [[ "$preview" == true ]]; then
  download "$api?per_page=100" "$work_dir/releases.json"
  version=$(python3 - "$work_dir/releases.json" <<'PY'
import json, sys
releases = json.load(open(sys.argv[1], encoding="utf-8"))
candidates = [r for r in releases if r.get("prerelease") and not r.get("draft")
              and r.get("tag_name", "").startswith("custom-")]
if not candidates:
    sys.exit("Aucune préversion custom publiée.")
print(max(candidates, key=lambda r: r.get("published_at") or "")["tag_name"])
PY
  )
elif [[ -z "$version" ]]; then
  download "$api/latest" "$work_dir/release.json"
  version=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["tag_name"])' "$work_dir/release.json")
fi
[[ "$version" =~ ^custom-[A-Za-z0-9][A-Za-z0-9._-]{0,100}$ ]] || fail 'La release sélectionnée n’est pas une version custom reconnue.'
base="https://github.com/$repository/releases/download/$version"
archive='mineos-custom-source.tar.gz'
printf 'Téléchargement de %s…\n' "$version"
download "$base/$archive" "$work_dir/$archive"
download "$base/$archive.sha256" "$work_dir/$archive.sha256"
expected=$(awk 'NR == 1 { print $1 }' "$work_dir/$archive.sha256")
[[ "$expected" =~ ^[0-9a-fA-F]{64}$ ]] || fail 'Empreinte publiée invalide.'
actual=$(sha256sum "$work_dir/$archive" | cut -d ' ' -f 1)
[[ "${expected,,}" == "$actual" ]] || fail 'Le fichier téléchargé ne correspond pas à son empreinte.'

# Reject links and traversal before extracting anything from the verified bundle.
python3 - "$work_dir/$archive" "$work_dir/source" <<'PY'
import pathlib, sys, tarfile
with tarfile.open(sys.argv[1], "r:gz") as archive:
    members = archive.getmembers()
    if sum(m.size for m in members) > 500 * 1024 * 1024:
        sys.exit("Archive trop volumineuse.")
    for member in members:
        path = pathlib.PurePosixPath(member.name)
        if path.is_absolute() or ".." in path.parts or not (member.isfile() or member.isdir()):
            sys.exit("Chemin ou type de fichier interdit dans l'archive.")
    archive.extractall(sys.argv[2], members=members, filter="data")
PY
[[ -f "$work_dir/source/docker-compose.yml" && -f "$work_dir/source/scripts/prepare.sh" ]] \
  || fail 'Le paquet ne contient pas notre version custom.'
as_root mkdir -p -- "$(dirname -- "$install_dir")"
as_root mkdir -- "$install_dir"
as_root chown "$(id -u):$(id -g)" -- "$install_dir"
cp -R -- "$work_dir/source/." "$install_dir/"
cd -- "$install_dir"
bash scripts/prepare.sh
printf '%s\n' 'Construction de notre version. Cette première étape peut prendre plusieurs minutes…'
as_root docker compose config --quiet
as_root docker compose up -d --build --wait --wait-timeout 180
as_root docker compose ps
printf '\nVersion installée : %s\nDossier : %s\n' "$version" "$install_dir"
printf '%s\n' 'Compte initial : admin. Mot de passe dans .env (non affiché ici).'
printf '%s\n' 'Ouvrir http://ADRESSE_DU_DEBIAN:6070 puis la page Discord.'
printf '%s\n' 'Commandes habituelles : docker compose ps ; docker compose logs ; docker compose up -d.'
