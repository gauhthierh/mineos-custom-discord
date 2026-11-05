# Reprise MineOS et intégration Discord

État initial examiné le 5 novembre 2026.

## Avancement

Le périmètre a été précisé : contrôle des messages et des salons de destination,
avec installation neuve sur Debian 13 après développement.
Une première implémentation est maintenant présente : page d'administration Discord,
destinations multiples, règles par événement et serveur, modèles, aperçu, tests
manuels et historique. Les premières suites de tests et le parcours navigateur
ont passé ; la validation finale est suivie dans `docs/CUSTOM_DISCORD_FR.md`.
Les paragraphes suivants conservent le constat initial pour référence.

## Base de travail

- Dépôt : https://github.com/freeman412/mineos-sveltekit
- Copie locale de la branche de développement par défaut `vibing`.
- Référence examinée : `12bbed1`.
- Backend C# / .NET 8 ; interface SvelteKit 2 / Svelte 5.
- Installation Docker Compose et fichier de construction depuis les sources présents.
- Aucun changement fonctionnel effectué à ce stade ; aucune installation sur Debian effectuée.
- Examen statique uniquement : application et tests non exécutés.

## Discord : constat dans le code

Une intégration par webhook existe dans
`apps/MineOS.Infrastructure/Services/DiscordWebhookService.cs`.
Elle envoie des messages enrichis avec le serveur concerné et un niveau de gravité.
Les services de gestion des serveurs, de tâches et de surveillance lui transmettent
des événements : création/suppression, démarrage/arrêt, sauvegardes et incidents.

Le service utilise une file mémoire limitée à 100 événements, un espacement des
envois et une nouvelle tentative après une réponse HTTP 429. Il dispose de tests
unitaires. La configuration actuelle expose une URL de webhook globale dans les
paramètres et dans les variables du conteneur.

Aucune implémentation de bot Discord, de commandes slash, de liaison des comptes,
de synchronisation des rôles ou de passerelle de discussion n'a été trouvée lors
de cette première recherche. La page À propos annonce encore les notifications
webhook comme futures alors qu'elles sont déjà présentes dans le backend.

## Lots proposés, à prioriser avec le propriétaire

1. Compléter les notifications : réglages par serveur et type d'événement,
   test depuis l'interface, messages configurables, diagnostic des échecs et
   traitement de l'URL du webhook comme un secret.
2. Ajouter un bot : statut et joueurs en lecture seule, puis commandes de
   démarrage/arrêt/redémarrage avec droits par rôle et par serveur, confirmations
   des actions sensibles et journal des actions.
3. Ajouter un message de statut mis à jour : disponibilité, joueurs, version,
   et métriques disponibles dans MineOS.
4. Étudier la liaison Minecraft–Discord : preuve de possession du compte,
   whitelist et rôles. Définir les règles avant de les implémenter.
5. Étudier la discussion Minecraft ↔ Discord. Vérifier d'abord les plugins
   déjà installés, notamment une éventuelle passerelle existante ; décider si
   cette fonction appartient au panel ou à un plugin Minecraft.

Ces lots sont des propositions, pas des fonctionnalités déjà développées.

## Validation et installation Debian 13

Docker documente la prise en charge de Debian 13 :
https://docs.docker.com/engine/install/debian/

Avant le déploiement, relever l'installation existante : MineOS éventuel,
conteneurs, dossiers Minecraft, sauvegardes, ports, ressources et accès au panel.
Tester la version modifiée dans un environnement séparé et vérifier la restauration
d'une sauvegarde avant une migration de serveurs existants.

Construire et identifier explicitement les images de notre version. Le Compose
standard référence les images du projet d'origine : il faut préparer un déploiement
qui conserve nos modifications et une procédure de mise à jour correspondante.

Respecter `AGENTS.md` : architecture en couches, autorisations par serveur et
validation du backend par `dotnet test apps/MineOS.Tests/MineOS.Tests.csproj` ;
validation de l'interface par `npm run check` dans `apps/web`. Ajouter des tests
qui exercent chaque nouveau comportement côté backend.

## Informations nécessaires pour commencer le premier lot

- Fonctions Discord souhaitées et ordre de priorité.
- État du Debian 13 : installation neuve ou MineOS déjà en service, serveurs
  Minecraft à conserver et usage éventuel de Docker.
- Pour une passerelle de discussion ou de rôles : intégrations Minecraft–Discord
  déjà utilisées et comportement souhaité.
