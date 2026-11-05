# Validation locale de la version custom Discord

Date : 5 novembre 2026. Base d'origine : branche `vibing`, commit `12bbed1`.

## Pied de page personnalisable (preview.6)

- Nouveau réglage global `footerText`, initialisé à MineOS pour les anciennes
  configurations comme pour les nouvelles. Un champ vide rétablit MineOS.
- Texte limité à 80 caractères, enregistré dans la configuration existante.
- Valeur utilisée par les envois automatiques, les tests manuels et les aperçus.
- L'aperçu du navigateur affiche le pied de page réellement calculé par l'API.
- Tests ajoutés pour le réglage par défaut, l'enregistrement de Noryandor, les
  anciennes configurations sans ce champ, la remise à zéro et le rejet des textes trop longs.
- Les parcours navigateur vérifient MineOS par défaut, l'aperçu Noryandor,
  l'enregistrement puis la conservation après rechargement, sur localhost et en HTTP.
- Vérification locale : 343 tests .NET, 139 tests unitaires de l'interface et
  deux parcours Chromium réussis. Vérification Svelte sans erreur ; les 93
  avertissements hérités restent inchangés. Construction de production réussie.

## Correctif du bouton en HTTP (preview.5)

Le défaut a été reproduit dans Chromium avec `isSecureContext === false` :
`crypto.randomUUID` n'existe pas, le clic lève une exception et le nombre de salons
reste inchangé. Les premières vérifications sur localhost ne couvraient pas ce cas.

La création des identifiants des salons, des règles et des copies utilise désormais
`crypto.getRandomValues` lorsque `randomUUID` n'est pas disponible. Le format UUID v4
et le caractère aléatoire des identifiants sont conservés.

- Tests unitaires : 139 réussis, dont trois tests de compatibilité HTTP.
- Deux parcours Chromium réussis : localhost et origine HTTP non sécurisée.
  Ils couvrent l'ajout de deux salons, les règles de statut et de sauvegardes,
  la création et la duplication d'une règle, l'aperçu, l'enregistrement et le rechargement.
- L'origine HTTP est explicitement vérifiée non sécurisée, sans `crypto.randomUUID`.
- Aucun message Discord réel envoyé : notifications en pause et webhooks fictifs.
- Vérification Svelte/TypeScript : aucune erreur, avertissements hérités inchangés.
- Construction de l'interface de production réussie.
- Ces parcours navigateur font désormais partie des contrôles avant publication.
- Validation Linux complète et publication réussies :
  https://github.com/gauhthierh/mineos-custom-discord/actions/runs/42034168388
- Release : `custom-discord-2026.11.05-preview.5`, commit `7612134`.
- Paquet public téléchargé, correctif présent et empreinte SHA-256 vérifiée :
  `1969c524b1199f5b46bff9d460696e8ce76cd6ded654711f4ff69c3f894a85c6`.
- La procédure de correction de l'interface seule a été vérifiée dans un dossier
  jetable ; elle conserve les fichiers de données et les identifiants et ne demande
  que la reconstruction et le redémarrage du service web.
- Le correctif n'a pas été appliqué à distance au Debian du propriétaire.

## Révision avec fichiers standards (preview.3)

- Déploiement normalisé sur `.env`, `docker-compose.yml` et `install.sh`.
- Lancement sans options supplémentaires : `docker compose up -d`.
- Port par défaut d'une installation neuve : 6070, accessible sur le réseau.
- Quatre tests de résolution Compose vérifient les identifiants, les images
  modifiées, les ports, les chemins et les anciens réglages.
- Test de génération et de migration dans un dossier jetable : secrets conservés,
  conflit entre deux fichiers de secrets refusé et données non supprimées.
- Suite .NET relancée : 334 tests réussis.
- Vérification de l'interface relancée : aucune erreur, 93 avertissements existants.
- Le workflow de cette révision teste aussi le démarrage et la connexion réelle
  du compte administrateur, puis les routes Discord authentifiées, sur Linux.
- Tous ces contrôles et la publication ont réussi :
  https://github.com/gauhthierh/mineos-custom-discord/actions/runs/42024112099
- Release : `custom-discord-2026.11.05-preview.3`, commit `32b1787`.
- Paquet public téléchargé après publication et empreinte SHA-256 vérifiée :
  `6cd9451ed8294c8473b05d625d84a462ef37d89cb5b79c9824c21171781db3c9`.
- Cette correction est publiée ; elle n'a pas été appliquée à distance au Debian
  du propriétaire. La migration avec ses données reste à effectuer sur sa machine.

## Résultats de la première préversion

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
