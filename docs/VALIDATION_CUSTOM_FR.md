# Validation locale de la version custom Discord

Date : 5 novembre 2026. Base d'origine : branche `vibing`, commit `12bbed1`.

## Résultats

- Suite .NET complète : **334 tests réussis, aucun échec, aucun test ignoré**.
- Tests unitaires de l'interface : **136 tests réussis**.
- Vérification Svelte/TypeScript : **aucune erreur** ; 93 avertissements dans
  32 fichiers du projet, hors de la nouvelle page Discord.
- Construction de l'interface de production : réussie avec Node.js 24.
- Parcours navigateur : ajout des salons, règles statut et sauvegardes,
  aperçu, enregistrement, rechargement, conservation des adresses sans les
  réexposer, et vérification de l'affichage mobile.
- Configuration Docker Compose custom : valide.
- Script de préparation : syntaxe, génération des secrets et conservation d'un
  fichier existant vérifiées dans un dossier jetable.
- Installateur par lien : aide, simulations stable/préversion/version précise,
  et refus des arguments invalides vérifiés.
- Workflow de publication : YAML et syntaxe Bash de ses blocs vérifiés localement.
- Validation GitHub sur Linux : suite .NET, interface, construction et démarrage
  des conteneurs réussis. Exécution :
  https://github.com/gauhthierh/mineos-custom-discord/actions/runs/42014516542
- Préversion publiée :
  https://github.com/gauhthierh/mineos-custom-discord/releases/tag/custom-discord-2026.11.05-preview.2
- Installateur et paquet source publics téléchargés et vérifiés après publication ;
  empreinte SHA-256 correcte et aucune donnée locale dans l'archive.
- Construction .NET de production : réussie, sans clé de licence de traitement d'images.
- Audit NuGet de l'API, dépendances transitives comprises : aucun package vulnérable
  signalé par les sources consultées lors de cette validation.

## Dépendances corrigées

Le composant ImageSharp 3.1.12 du projet d'origine a été retiré et remplacé par
Magick.NET-Q8-AnyCPU 15.0.0. Le traitement des icônes conserve une sortie PNG de
64 × 64 pixels. Il borne la lecture à 5 Mo, impose un décodeur d'image matricielle
connu et limite les dimensions et la mémoire de décodage. Onze tests supplémentaires
vérifient les conversions et le rejet des contenus invalides ou trop volumineux.

Les anciennes dépendances indirectes signalées ont aussi été remplacées par
System.Security.Cryptography.Xml 8.0.5, System.Security.Cryptography.Pkcs 8.0.2
et SQLitePCLRaw.bundle_e_sqlite3 3.0.5. Les tests et l'audit ont été relancés après
ces changements. L'audit des dépendances ne remplace pas une analyse de sécurité
complète de l'application.
Les deux dépendances historiques des outils de test, System.Net.Http et
System.Text.RegularExpressions, ont été mises à jour vers 4.3.4 et 4.3.1 ; le
contrôle des avis de sécurité transitifs utilisé avant publication passe aussi.

## Portée des tests Discord

Les tests serveur vérifient notamment la séparation des destinations statut et
sauvegardes, les filtres par serveur et événement, la désactivation des règles,
les réponses 429 de Discord, la limitation de la file, les modèles, les limites
de longueur, les URL autorisées, la conservation et la non-divulgation des secrets,
et les restrictions d'accès des administrateurs.

Les tests HTTP Discord utilisent des réponses simulées. Le test navigateur utilise
des webhooks fictifs et garde les envois automatiques désactivés. Aucun message
Discord réel n'a été envoyé. Le test avec les vrais salons du propriétaire reste
à faire après configuration.

## Vérifications restant à effectuer avant déploiement

- Exécuter l'installation complète sur Debian 13 et vérifier la gestion d'un
  serveur Minecraft réel et de ses sauvegardes dans cet environnement.

Le dépôt cible choisi par le propriétaire est `GauhthierH/mineos-custom-discord`.
La préversion a été publiée après réussite des contrôles du workflow Linux.
L'installation sur le Debian du propriétaire n'a pas encore été effectuée.
