# MineOS custom : notifications Discord

## Ce que cette version ajoute

Une page **Discord** dans le menu d'administration, réservée aux administrateurs.
Elle propose jusqu'à 20 destinations et 60 règles. Chaque destination correspond
à un webhook créé dans un salon Discord. Aucun bot Discord n'est nécessaire.

Une règle choisit :

- les événements qui déclenchent un message ;
- un ou plusieurs salons de destination ;
- les serveurs concernés, ou tous les serveurs actuels et futurs ;
- un titre et un message personnalisés ;
- une carte Discord ou du texte simple ;
- une couleur et l'affichage du serveur et de la date pour les cartes.

Le nom de l'expéditeur se règle globalement. Les règles et les salons peuvent être
mis en pause séparément, et un interrupteur général suspend les envois automatiques.

### Exemple : statut dans un salon, sauvegardes dans un autre

1. Dans Discord, créer un webhook dans `#statut`, et un autre dans `#sauvegardes`.
2. Dans MineOS → Discord, ajouter ces deux salons et leurs adresses de webhook.
3. Cliquer sur **Statut en ligne / hors ligne**. Cela ajoute deux règles :
   démarrage et arrêt/crash, avec des messages français. Sélectionner `#statut`.
4. Cliquer sur **Sauvegardes** puis sélectionner uniquement `#sauvegardes` dans cette règle.
5. Ajuster les événements : par exemple ne garder que les échecs de sauvegarde.
6. Enregistrer, puis tester les salons et consulter l'historique.

Un événement sans règle ne produit aucun message. Plusieurs règles correspondant
au même événement produisent plusieurs messages : éviter de doubler les règles
si un seul message est souhaité. Les boutons rapides ajoutent des règles sans
supprimer les règles existantes.

### Événements disponibles

- Statut : démarrage, arrêt, y compris arrêt forcé demandé depuis MineOS.
- Incidents : crash, redémarrage automatique réussi ou échoué.
- Administration : création et suppression d'un serveur.
- Sauvegardes : début, réussite, échec, annulation ; sauvegardes manuelles et planifiées.
- Restaurations : début, réussite, échec, annulation.
- Imports, installations de mods/modpacks et autres tâches : réussite, échec, annulation.
- Autres notifications système.

Il s'agit de messages déclenchés par des événements, pas d'un message permanent
actualisé périodiquement. Les événements sont ceux observés ou exécutés par MineOS.
Le panel doit être en fonctionnement et le watchdog doit surveiller un serveur
pour détecter ses crashs. Les préférences de notification Discord sont indépendantes
des cases de notification du watchdog dans le panel.

### Personnalisation des messages

Variables : `{server}`, `{title}`, `{message}`, `{event}`, `{level}`, `{timestamp}`.
Le titre et le message par défaut reprennent le texte de l'événement, dont certains
textes du projet d'origine sont en anglais. Les modèles peuvent être remplacés par
du français, par exemple :

```text
Titre : 🟢 Serveur en ligne
Message : {server} est maintenant accessible. Bonne partie !
```

L'aperçu utilise un événement d'exemple et n'envoie rien. Il faut le régénérer après
une modification. Le test envoie réellement un message d'exemple à une destination
enregistrée ; il fonctionne même lorsque les envois automatiques sont en pause.
Le test d'une règle cible le premier salon sélectionné ; les tests individuels
des salons permettent de vérifier chaque destination.

Les mentions ne déclenchent pas de notifications de personnes, de rôles ou de
`@everyone`. Les messages trop longs sont tronqués aux limites de Discord.

### Conservation et diagnostic

Les réglages sont stockés dans la base MineOS. Les adresses de webhook ne sont
jamais renvoyées par l'API Discord ni réaffichées après enregistrement. Un champ
vide conserve l'adresse existante ; pour la remplacer, saisir une nouvelle adresse.
Supprimer le salon dans MineOS supprime sa destination de la configuration ; cela
ne supprime pas le webhook dans Discord.

La configuration contient des secrets dans la base : protéger le dossier `data`
et ses sauvegardes comme le fichier `.env`. Les journaux HTTP du client
Discord sont désactivés pour éviter de journaliser les jetons présents dans les URL.

L'historique conserve les 100 derniers résultats d'envoi **en mémoire**, y compris
les tests. Il est effacé au redémarrage. La file d'événements est aussi en mémoire,
limitée à 100 événements : les plus anciens sont écartés en cas de saturation,
et le compteur est visible dans la page. Les échecs ne bloquent pas les opérations
Minecraft. Trois tentatives au maximum sont effectuées pour les réponses 429 et
les erreurs serveur de Discord ; les erreurs définitives sont affichées dans l'historique.

Une ancienne configuration avec un webhook unique reste utilisable. Au premier
chargement, elle apparaît comme un salon et une règle couvrant les événements.
Une fois la nouvelle configuration enregistrée, elle prend le relais ; supprimer
toutes les règles ne réactive pas l'ancien webhook.

## Installation neuve sur Debian 13

```bash
curl -fsSL https://raw.githubusercontent.com/GauhthierH/mineos-custom-discord/vibing/install.sh | bash -s -- --preview
```

L'installateur crée `~/mineos`, prépare `.env`, construit nos sources et attend le
démarrage des services. Il refuse d'écraser un dossier existant. Modifier le chemin
avec `--dir /chemin/absolu` si nécessaire. L'installation complète de cette révision
sur le Debian du propriétaire reste à vérifier.

Le compte initial est `admin` ; le mot de passe est généré dans `.env` avec des
permissions réservées au propriétaire. Pour une installation manuelle depuis nos
sources, exécuter `bash scripts/prepare.sh` puis :

```bash
docker compose config --quiet
docker compose up -d --build --wait --wait-timeout 180
docker compose ps
```

Utiliser `sudo` si nécessaire. Aucune option `--env-file` ou `-f` n'est requise.
Les fichiers habituels sont `.env` et `docker-compose.yml`.
Ouvrir **http://ADRESSE_DU_DEBIAN:6070**. Le panel écoute sur les interfaces réseau
par défaut ; `WEB_BIND_ADDRESS` et `WEB_PORT` permettent de changer cet accès.
Les anciens réglages sont conservés lors d'une migration, y compris le port 3000
et l'écoute locale des premières préversions.

Les données restent dans `minecraft/` et `data/`, réglables par
`HOST_BASE_DIRECTORY` et `Data__Directory`. Les ports Minecraft restent
`25565–25570` et `19132–19137`. L'API est interne aux conteneurs ; le panneau permet
d'utiliser toutes ses fonctions. Le socket Docker n'est pas monté.
Les images locales `mineos-custom-api:discord-v1` et `mineos-custom-web:discord-v1`
sont construites depuis nos sources, sans tirer les images officielles.
Les intégrations, les chemins, les modèles et les règles Discord restent configurables.

### Migration des premières préversions

Cette procédure conserve les identifiants, la base, les mondes et les réglages.
Prévoir une interruption et arrêter proprement les serveurs Minecraft avant de
sauvegarder `data/`, `minecraft/` et le fichier de secrets.

Télécharger le paquet `mineos-custom-source.tar.gz` et son `.sha256` depuis la
[release preview.3](https://github.com/gauhthierh/mineos-custom-discord/releases/tag/custom-discord-2026.11.05-preview.3).
Vérifier `sha256sum -c mineos-custom-source.tar.gz.sha256` dans le dossier de téléchargement,
puis extraire le paquet dans le dossier de l'installation existante, sans déplacer
`data/` ou `minecraft/`. Le paquet ne contient aucun fichier `.env`.

Depuis le dossier de l'installation mis à jour :

```bash
sudo bash scripts/migrate-layout.sh
```

Le script arrête l'ancien projet Compose, conserve son fichier sous
`docker-compose.previous.yml`, renomme `.env.custom` en `.env` et reconstruit nos images.
Il ne régénère aucun secret et ne supprime aucun volume. Si les deux fichiers
`.env` et `.env.custom` existent, il s'arrête pour éviter de choisir les mauvais
identifiants. Si `.env` existe seul, il est conservé.

Après migration, utiliser simplement :

```bash
sudo docker compose ps
sudo docker compose logs --tail=100
sudo docker compose up -d
```

Pour une mise à jour suivante, sauvegarder les données et `.env`, transférer les
nouvelles sources puis exécuter `docker compose up -d --build`.

## Vérification du développement

Commandes de référence :

```bash
dotnet test apps/MineOS.Tests/MineOS.Tests.csproj
cd apps/web
npm ci
npm run check
npm run test:unit
npm run build
```

Le test navigateur `tests/discord.spec.ts` s'active uniquement avec
`MINEOS_E2E_USERNAME` et `MINEOS_E2E_PASSWORD` renseignés. Il doit viser une instance
isolée avec une base jetable : il remplace sa configuration Discord par celle du
scénario de test. Il garde les envois automatiques en pause et utilise des webhooks
fictifs ; aucun message Discord réel n'est envoyé par ce test.

Références API utilisées :
https://docs.discord.com/developers/resources/webhook
et https://docs.discord.com/developers/topics/rate-limits
