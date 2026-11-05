# Corriger « Ajouter un salon » en HTTP

Le correctif est inclus dans `custom-discord-2026.11.05-preview.5`.
En HTTP via une IP ou un nom réseau, le navigateur ne fournit pas `crypto.randomUUID`.
L'ajout des salons et des règles, ainsi que la duplication, utilisent désormais
une méthode compatible HTTP qui conserve des identifiants UUID v4 aléatoires.

Les versions 5 et suivantes incluent déjà ce correctif. Pour installer les nouveautés
plus récentes, utiliser la [mise à jour complète](CUSTOM_DISCORD_FR.md#migration-des-premières-préversions).

## Appliquer le correctif à une installation avec les fichiers standards

Depuis le dossier contenant `.env`, `docker-compose.yml` et `apps/`, copier ce bloc.
Il télécharge les deux fichiers corrigés dans un dossier temporaire avant de
remplacer leurs versions locales. Il reconstruit et redémarre uniquement l'interface.
Les identifiants, les réglages Discord et les serveurs Minecraft sont conservés.

```bash
(
  set -euo pipefail
  [[ -f .env && -f docker-compose.yml && -f apps/web/src/lib/discord.ts ]] || {
    echo 'Place-toi dans le dossier de ton installation MineOS.' >&2
    exit 1
  }
  patch_dir=$(mktemp -d)
  source_url=https://raw.githubusercontent.com/gauhthierh/mineos-custom-discord/custom-discord-2026.11.05-preview.5
  curl -fsSL "$source_url/apps/web/src/lib/discord.ts" -o "$patch_dir/discord.ts"
  curl -fsSL "$source_url/apps/web/src/routes/(app)/admin/discord/+page.svelte" -o "$patch_dir/discord.svelte"
  cp -- "$patch_dir/discord.ts" apps/web/src/lib/discord.ts
  cp -- "$patch_dir/discord.svelte" 'apps/web/src/routes/(app)/admin/discord/+page.svelte'
  sudo docker compose build web
  sudo docker compose up -d --no-deps web
)
```

Recharger ensuite la page avec **Ctrl + F5**, puis cliquer sur **Ajouter un salon**.

Si ton installation utilise encore `.env.custom`, appliquer plutôt la
[mise à jour complète](CUSTOM_DISCORD_FR.md#migration-des-premières-préversions).

## Vérifications

Le blocage a été reproduit dans Chromium avant correction. Après correction,
les parcours sur localhost et sur une origine HTTP non sécurisée passent : ajout,
création et duplication des règles, aperçu, enregistrement et rechargement.
Aucun message Discord réel n'a été envoyé pendant ces tests.

Référence du comportement navigateur :
https://developer.mozilla.org/en-US/docs/Web/API/Crypto/randomUUID
