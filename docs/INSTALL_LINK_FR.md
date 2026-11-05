# Installer notre version avec un lien

## Solution retenue

Héberger notre copie du projet dans un dépôt GitHub public et publier nos propres
releases avec un installateur. Aucun domaine personnel n'est nécessaire.
Le lien télécharge notre script ; celui-ci récupère un paquet contenant nos
sources modifiées, vérifie son empreinte SHA-256 et construit nos images Docker.
Il n'utilise pas les images officielles, qui ne contiennent pas nos modifications.

Le dépôt est `GauhthierH/mineos-custom-discord`. La préversion avec les fichiers standards
est `custom-discord-2026.11.05-preview.3`. Le script est disponible à une adresse
fixe sur sa branche `vibing`. Utiliser **--preview** pour cette première version.

Après publication d'une première release stable custom, la commande sera :

```bash
curl -fsSL https://raw.githubusercontent.com/GauhthierH/mineos-custom-discord/vibing/install.sh | bash
```

La même adresse permettra de choisir la dernière préversion :

```bash
curl -fsSL https://raw.githubusercontent.com/GauhthierH/mineos-custom-discord/vibing/install.sh | bash -s -- --preview
```

Pour une version précise, ou tant qu'il n'existe que des préversions, utiliser
l'adresse exacte publiée dans les notes de cette release :

```bash
curl -fsSL https://github.com/GauhthierH/mineos-custom-discord/releases/download/custom-discord-EXEMPLE/install.sh | bash -s -- --version custom-discord-EXEMPLE
```

Le raccourci `/releases/latest/` de GitHub nécessite une release stable ; une
préversion seule doit être téléchargée par son adresse de release exacte.
L'adresse fixe du script sur `raw.githubusercontent.com` permet d'utiliser
`--preview` même avant la première release stable.

## Ce que fait l'installateur

- Vérifier Debian 13 et l'architecture amd64 ou arm64.
- Refuser d'écraser un dossier existant.
- Installer les outils nécessaires et, si besoin, Docker depuis son dépôt officiel.
- Télécharger notre release et contrôler l'empreinte du paquet.
- Extraire les sources en refusant les liens et les chemins sortant de l'archive.
- Créer les identifiants aléatoires dans `.env`, sans les afficher.
- Construire et lancer notre version puis attendre que les services soient prêts.

Le dossier par défaut est `~/mineos`. L'option `--dir /chemin` le modifie.
Il faut un compte avec sudo, ou exécuter en root. Un Docker déjà fonctionnel est
conservé. Les paquets Docker incompatibles ne sont pas supprimés automatiquement.
Cet installateur réalise une installation neuve ; il n'effectue pas de migration
ni de mise à jour d'un dossier déjà installé.

La première installation construit les images sur le Debian et prend donc plus
de temps qu'un téléchargement d'images préfabriquées. On pourra ensuite publier
nos images dans notre propre registre, en conservant les mêmes commandes d'installation.

Par défaut, le panel est accessible sur `http://ADRESSE_DU_DEBIAN:6070`, comme décrit
dans [le guide custom](CUSTOM_DISCORD_FR.md). Les fichiers sont `.env` et
`docker-compose.yml` ; les commandes usuelles `docker compose` suffisent. L'installateur ne modifie pas le pare-feu
ni la configuration du domaine du propriétaire.

## Publication à effectuer lorsque le dépôt est choisi

1. Créer notre dépôt ou fork et y pousser les fichiers validés.
2. Activer GitHub Actions dans ce dépôt si nécessaire.
3. Lancer manuellement **Publish custom Discord version**, depuis la branche
   contenant nos changements, avec un nouveau tag `custom-*` et le choix préversion/stable.
4. Le workflow vérifie les tests .NET, les tests et la construction de l'interface,
   l'installateur, puis construit et démarre les conteneurs Linux.
5. Si tout passe, il publie trois fichiers :
   `install.sh`, `mineos-custom-source.tar.gz` et son fichier `.sha256`.
   L'installateur publié contient automatiquement le nom du bon dépôt.
6. Les notes de release fournissent la commande exacte à copier.

La publication se lance manuellement depuis GitHub Actions ou en poussant
explicitement un nouveau tag `custom-*`. Un tag contenant `-preview` publie une
préversion. Les simples modifications de la branche ne publient aucune release.
Aucun tag `v*` n'est créé : ces tags sont réservés au circuit de publication du
projet d'origine. Une release existante n'est pas écrasée ; une nouvelle version
reçoit un nouveau tag custom.

La publication utilise le jeton intégré de GitHub Actions, avec écriture limitée
au travail de publication. Le propriétaire n'a pas besoin de mettre un jeton
personnel dans l'installateur ni d'en communiquer dans le chat.

## Vérifications locales effectuées

- Aide, simulation stable, sélection d'une version et simulation préversion.
- Refus des dépôts, chemins, tags et options invalides.
- Syntaxe Bash du script et de la préparation des identifiants.
- Génération des identifiants sans écrasement d'un fichier existant.
- Validation du fichier Docker Compose.

La première préversion a été validée sur Linux. La nouvelle publication refait
ces contrôles avec les fichiers standards et teste aussi la connexion du compte
administrateur et l’accès aux fonctions Discord.
L'installation complète sur le Debian 13 du propriétaire reste à réaliser.

Sources :
https://docs.github.com/en/rest/releases/releases
et https://docs.docker.com/engine/install/debian/
