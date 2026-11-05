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
et ses sauvegardes comme le fichier `.env.custom`. Les journaux HTTP du client
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

L'installateur en une commande et sa procédure de publication sont décrits dans
[INSTALL_LINK_FR.md](INSTALL_LINK_FR.md). La procédure manuelle ci-dessous reste disponible.

Cette procédure est préparée pour le déploiement ultérieur. Elle n'a pas encore
été exécutée sur le Debian du propriétaire. La construction et le démarrage
des conteneurs ont réussi sur le serveur Linux de validation GitHub.

1. Installer Docker Engine et le plugin Compose selon la documentation officielle :
   https://docs.docker.com/engine/install/debian/
2. Transférer **notre copie modifiée du dépôt** dans un dossier dédié, par exemple
   `/opt/mineos-custom`. Cloner uniquement le dépôt d'origine ne récupère pas nos changements.
   Ne pas transférer `.tools`, `node_modules`, `bin`, `obj` ni les données de test.
3. Dans ce dossier, préparer les identifiants :

```bash
bash scripts/prepare-custom.sh
```

Le script crée `.env.custom` avec des secrets aléatoires et des permissions réservées
au propriétaire. Il ne remplace jamais un fichier existant. Le compte initial est
`admin` ; son mot de passe se trouve dans `.env.custom`. Modifier le nom du compte
avant le premier lancement si souhaité.

4. Vérifier la configuration et construire notre version :

```bash
docker compose --env-file .env.custom -f compose.custom.yml config --quiet
docker compose --env-file .env.custom -f compose.custom.yml up -d --build --wait --wait-timeout 180
docker compose --env-file .env.custom -f compose.custom.yml ps
```

Utiliser `sudo` si le compte n'a pas accès à Docker. Les serveurs Minecraft et la
base sont conservés dans `minecraft/` et `data/` à côté du fichier Compose.
Java et les outils nécessaires sont intégrés à l'image API.

Par défaut, le panel écoute seulement sur la boucle locale du Debian. Pour y
accéder depuis son ordinateur pendant l'installation :

```bash
ssh -L 3000:127.0.0.1:3000 utilisateur@adresse-du-debian
```

Puis ouvrir `http://localhost:3000` sur son ordinateur. On configurera l'adresse
réseau ou un domaine avec HTTPS selon l'accès souhaité. Les ports Minecraft par
défaut sont `25565–25570` et `19132–19137` ; les plages sont réglables dans `.env.custom`.

Les images sont nommées `mineos-custom-api:discord-v1` et `mineos-custom-web:discord-v1`
et sont construites depuis nos sources. Le fichier Compose custom n'utilise pas
les images publiées du projet d'origine et ne monte pas le socket Docker dans l'API.
L'API est accessible uniquement entre les conteneurs. L'interface utilise Node.js 24.
La télémétrie est désactivée dans ce déploiement.

### Mise à jour de notre version

Sauvegarder `data/`, `minecraft/` et `.env.custom` avant toute migration.
Pour une sauvegarde cohérente de la base et des mondes, arrêter proprement les
serveurs puis les conteneurs avant de copier ces dossiers.

Après transfert des nouveaux fichiers sources validés, relancer la commande
`up -d --build` ci-dessus. Ne pas utiliser `mineos update` pour ce déploiement :
cette commande est conçue pour les versions publiées du projet d'origine.

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
