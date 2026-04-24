# Instructions Pour Codex

Utilise ce fichier pour me donner du contexte et des consignes persistantes pour ce projet.

## Objectif

J'ai développé une application Winform qui permet de prendre des photos de météorites, de faire des focus stack si nécessaire, afin de les utiliser dans Metashape Pro pour en refaire des modèles 3d high res. 
Au lieu de faire la prise de photo manuellement, j'ai des ESP32 en à des moteurs via wifi afin d'automatiser le tout. 
J'ai une table tournante sur laquelle il y a la météorite, cette table est sur un lift qui peut bouger de côté et en hauteur. 
Un actuateur linéaire fait monter et descendre la caméra sur un axe, la caméra elle-même étant sur un rail motorisé qui permet de l'avancer et de la reculer. 
Le but est de prendre environ 20 photos à 5 degrés, 14 à 25 degrés et 14 à 25 degrés. Chaque photo est le résultat, si voulu, d'un focus stack. Donc par côté de roche on peut avoir plus de 2000 photos prises. 
Le liveview de la caméra est analysé, j'en extrait un masque que j'applique à chaque photos (si voulu) afin que le résultat du focus stack soit masqué, pour que Métashape n'analyse que la météorite, pas la table tournante. 
Aussi je décèle les portions nettes de la météorite seulement à l'intérieur du masque, pour éviter de faire un focus stack avec les images de la table tournante.
Aussi je dois prendre (si voulu) une image sans masque afin d'avoir les points de référence, sous la météorite, qui vont permettre de calculer le volume dans MS (Metashape)
Tout doit se dérouler séquentiellement mais sur des threads différents. 



-

## Contexte

Nikon Wrapper C# me permet de communiquer avec la D850

Un des enjeux que j'ai est que l'application nécessite d'analyer le flux vidéo de la caméra sur le thread principal mais mes Tasks se lancent sur des theads autres que le UI #1. 
Les Cancellation tokens et les TaskCompletionSources se réalisent pas pas sur le même thread qui les a lancés. Nikon device.Capture s'exécute sur le thead 1 toujours mais il e lancé depuis des threads autres. C'est à régler.  



-

## Contraintes

Ce que je dois respecter :

-

## Fichiers Concernés

Liste les fichiers ou dossiers importants :

-

## À Faire

Décris les tâches à exécuter :

1. Vérifier que les 
2.
3.

## À Éviter

Ce que je ne dois pas modifier ou faire :

-

## Vérification

Comment valider que le travail est correct :

-

## Journal Technique

### Historique Des Révisions

- `REV-0001`
  - Ajout d'un identifiant de révision visible dans le titre principal de la fenêtre.
  - Le titre garde maintenant un suffixe de version même quand le projet change.
  - Objectif: permettre de comparer visuellement l'état affiché dans l'application avec les changements de code effectués.

- `REV-0002-capture-sequence-debug`
  - Passage à une révision plus descriptive dans la barre de titre.
  - Contexte actuel: diagnostic et stabilisation du flux Nikon autour de la capture simple, de `SaveMesurementImage()` et de la séquence totale.
  - Cette révision sert de point de repère pour les tests sur la capture, les miniatures et les séquences.

### Contexte Confirmé

- Le projet est une application WinForms .NET 8 avec Nikon D850, live view, autofocus, focus stack, masquage, UDP/ESP32 et séquences automatiques.
- Le wrapper Nikon capture son `SynchronizationContext` à l'initialisation du `NikonManager`, donc les interactions caméra doivent rester cohérentes avec le flux UI/callbacks existant.

### Changements Déjà Effectués

- Ajout d'une sérialisation des opérations Nikon pour éviter les collisions entre autofocus, focus manuel et autres commandes caméra.
- `ManualFocus` a été converti en version async sérialisée.
- `NikonAutofocus` a été nettoyé pour éviter `Task.Run(...)` autour des appels Nikon.
- `takePictureAsync()` a été rendu réellement awaitable via `imageReadyTcs`.
- Plusieurs warnings simples ont été nettoyés:
  - doublons de `using`
  - quelques nullabilités triviales
  - quelques `async` inutiles

### Ce Qui Fonctionne Maintenant

- Le bouton `Prendre une photo` fonctionne de nouveau normalement.
- Le focus manuel a été sécurisé.
- L'autofocus est sérialisé.

### Régression Identifiée Puis Corrigée

- Une version de `takePictureAsync()` coupait ou encadrait trop agressivement le live view autour de `device.Capture()`.
- Symptôme: log bloqué après `Capture de l'image par la Nikon ...`, plus aucun `device_ImageReady`, UI encore réactive mais capture figée.
- Conclusion importante:
  - pour la capture simple, ne pas sur-encadrer `device.Capture()`
  - ne pas couper `device.LiveViewEnabled` autour de la capture simple
  - éviter de mettre la capture dans le même verrou ou flux Nikon que l'autofocus si cela retarde ou bloque les callbacks

### Essais Qui N'ont Pas Fonctionné

- Couper `device.LiveViewEnabled` avant `Capture()` puis le remettre ensuite:
  - a cassé la capture simple
  - plus de `device_ImageReady`
- Stopper/redémarrer le live view timer autour de `Capture()`:
  - a aussi provoqué un blocage ou timeout sur la capture simple dans les essais
- Garder un verrou Nikon trop englobant pendant toute la capture:
  - a mené à des timeouts `Timeout en attente de device_ImageReady après Capture().`
- Ajouter une attente stricte de `CaptureComplete`/`ImageReady` dans un flux trop encapsulé:
  - n'a pas aidé à rétablir le comportement sur la capture simple

### Indices À Garder En Tête

- Si `Prendre une photo` rebloque après `Capture de l'image par la Nikon ...`, suspecter d'abord toute logique ajoutée autour de `device.Capture()`, pas `device_ImageReady` en premier.
- Pour la D850 dans ce projet, la capture simple doit rester très proche du comportement d'origine.
- Les protections de concurrence sont utiles pour:
  - autofocus
  - focus manuel
  - certaines opérations Nikon concurrentes
- Elles sont risquées si elles modifient trop le chemin de capture simple.

### Problèmes Encore Ouverts

- La séquence totale plante avec:
  - `System.NullReferenceException`
  - dans `PrisePhotos.cs`, ligne autour de `await miniaturesTcs.Task`
- Cause probable:
  - `miniaturesTcs` peut être remis à `null` avant l'attente, ou ne plus correspondre au flux réel de la capture de mesure
- `SaveMesurementImage` reste lent avant `device_ImageReady`
  - exemple observé:
    - `Capture de l'image par la Nikon ...` à `12:37:33:15`
    - `device_ImageReady` à `12:37:51:71`
    - soit environ `16,5 s`
- La sauvegarde disque n'est pas le goulot principal:
  - l'écriture de l'image pour mesure a pris environ `0,52 s`
  - la lenteur est donc avant `device_ImageReady`, côté Nikon/capture/focus/état caméra

### Piste De Travail Recommandée

- Corriger d'abord le flux `SaveMesurementImage` / `miniaturesTcs` dans la séquence totale.
- Ensuite mesurer séparément:
  - autofocus seul
  - capture simple seule
  - capture pour mesure
- Ne pas rebasculer `device.LiveViewEnabled` autour de la capture simple sans nouvelle preuve.
