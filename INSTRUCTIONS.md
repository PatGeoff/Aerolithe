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

- `REV-0003-ui-uniformisation-base`
  - Ajout d'une couche de standardisation visuelle par code sur l'UI principale.
  - Uniformisation de base des boutons, labels, textboxes, combobox et onglets sans modifier le workflow.
  - Distinction plus nette entre boutons texte, boutons icône et boutons de type danger/stop.
  - Objectif: rendre l'interface plus cohérente visuellement avant toute réorganisation plus fine des panneaux.

- `REV-0004-ui-button-text-fit`
  - Ajustement de la couche UI pour mieux faire entrer les libellés longs dans les boutons.
  - Largeur minimale augmentée pour les boutons texte.
  - Police légèrement réduite sur les boutons avec libellés longs.
  - Autorisation d'un rendu texte plus souple pour limiter les débordements visuels.

- `REV-0005-ui-layout-global`
  - Uniformisation de la position des boutons et des contrôles par code sur l'ensemble du formulaire.
  - Normalisation des marges, paddings et docks dans les `TableLayoutPanel`, `FlowLayoutPanel` et panneaux.
  - Objectif: rendre l'alignement des boutons plus cohérent sur tous les onglets sans refaire le Designer manuellement.

- `REV-0006-ui-layout-revert`
  - Retrait de la passe de layout global.
  - Motif: le rendu global n'était pas satisfaisant visuellement.
  - Retour à la version précédente du layout, tout en conservant l'uniformisation visuelle de base et l'ajustement des textes de boutons.

- `REV-0007-ui-font-vs`
  - Adoucissement de la typographie pour se rapprocher davantage du rendu Visual Studio.
  - Remplacement des variantes `Segoe UI Semibold` par `Segoe UI` régulière sur les contrôles standardisés.
  - Objectif: réduire l'aspect trop dur ou trop gras de l'interface.

- `REV-0008-ui-phosphor-toggles`
  - Restauration d'un style plus proche de l'UI d'origine pour les boutons icône/toggle.
  - Retour à la fonte `Phosphor` sur ces boutons.
  - Rendu circulaire réintroduit pour retrouver l'apparence des boutons avec crochet plus appréciée visuellement.

- `REV-0009-ui-toggle-revert-exact`
  - Annulation de toute stylisation appliquée par code sur les boutons icône/toggle.
  - Motif: retour exact à l'apparence définie par le Designer pour les boutons crochet/toggle.
  - Les boutons texte standardisés restent harmonisés, mais les toggles reviennent à leur rendu d'origine.

- `REV-0010-ui-sequence-button-12pt`
  - Fix explicite de la taille de police à `12 pt` pour les boutons `Prise de photos en séquence totale`.
  - Motif: éviter que la couche de standardisation ne réduise trop ce libellé important.

- `REV-0011-ui-roboto-medium`
  - La couche de style standardisée utilise maintenant `Roboto Medium` au lieu de `Segoe UI`.
  - S'applique aux boutons texte, labels standard, textboxes, combobox et onglets.
  - Les boutons toggle/icône restent inchangés et continuent de suivre leur rendu Designer.

- `REV-0012-ui-console-light`
  - Les consoles utilisent maintenant une fonte plus légère que le reste de l'interface.
  - `txtBox_Console` et `txtBox_FFMPEGConsole` passent en `Roboto` au lieu de `Roboto Medium`.

- `REV-0013-ui-tabcontrol-style`
  - Les `TabControl` utilisent maintenant un rendu custom au lieu du style WinForms par défaut.
  - Objectif: obtenir des onglets plus lisibles et plus propres visuellement, avec un état sélectionné plus clair.

- `REV-0014-ui-tabcontrol-revert`
  - Retrait du rendu custom des `TabControl`.
  - Motif: le comportement/rendu n'était pas satisfaisant.
  - Retour au rendu standard précédent avec seulement la fonte harmonisée.

- `REV-0015-ui-cancel-neutral`
  - Les boutons `Cancel`/`Canceller` ne sont plus colorés en rouge par la couche de style.
  - Retour à une teinte neutre pour mieux respecter le goût utilisateur.

- `REV-0016-ui-height-plus4`
  - Légère augmentation de la hauteur visuelle des boutons, du `MenuStrip` et des onglets.
  - Le bouton `Prise de photos en séquence totale` est forcé en `DockStyle.Fill`.
  - Objectif: donner un peu plus d'air sans modifier fortement le layout.

- `REV-0017-reset-increments-full`
  - Le reset remet maintenant à zéro `Serie`, `RotationSerieIncrement` et `FocusSerieIncrement`.
  - Motif: faire en sorte que `lbl_ImgFullPath` reflète réellement une remise à zéro du nom/path affiché.

- `REV-0018-console-lighter`
  - Les consoles utilisent maintenant une fonte plus légère visuellement.
  - Passage à `Segoe UI` pour les consoles, avec une couleur de texte adoucie (`220,220,220`) au lieu du blanc pur.

- `REV-0019-button-font-10pt`
  - Augmentation de la taille de texte des boutons texte standardisés à `10 pt`.
  - Les libellés longs restent légèrement réduits pour éviter les débordements.
  - Les boutons de séquence gardent leur exception à `12 pt`.

- `REV-0020-reset-button-text-fix`
  - Désactivation de `UseCompatibleTextRendering` sur les boutons texte standardisés.
  - Motif: le bouton `Reset Increment` affichait son texte dans le Designer mais pas au runtime.
  - Hypothèse retenue: conflit de rendu texte WinForms/GDI+ avec la couche de style appliquée par code.

- `REV-0021-series-progress-fix`
  - Correction de l'affichage de progression des séries pendant les séquences automatiques.
  - `lbl_CoteSerie`, `lbl_ElevSerie` et `lbl_RotSerie` sont maintenant mis à jour aussi quand le code tourne déjà sur le thread UI.
  - Le report de `flowPanelReports` utilise maintenant une série 1-based cohérente, ce qui supprime les valeurs `-1`.
  - L'incrément de rotation n'est plus poussé au-delà de la dernière photo d'une série.

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
