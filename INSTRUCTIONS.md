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

- Continuer à documenter les changements significatifs dans ce fichier, section `Journal Technique`.
- Mettre à jour `UiRevision` dans `Aerolithe.cs` quand un changement fonctionnel est fait.

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

- `REV-0022-saved-mask-apply`
  - Retrait de `checkBox_ApplyMaskStackedImage`, qui existait dans le Designer mais n'était branchée à aucune logique.
  - `PostFocusStackMask()` applique maintenant le masque sauvegardé sur disque via `projet.GetMaskFullImagePath()` au lieu d'utiliser directement `maskMatLive`.
  - L'application du masque utilise maintenant un redimensionnement `Nearest` pour préserver un masque net 0/255.
  - Pendant `device_ImageReady`, si `projet.ApplyMask` est actif et que le focus stack est désactivé, l'image utilise le masque sauvegardé existant sans l'écraser automatiquement.
  - Si aucun masque sauvegardé n'existe, un masque est généré depuis l'image capturée en fallback, puis sauvegardé.
  - Les JPEG continuent d'être écrasés lors d'une reprise de séquence, car la sauvegarde utilise `FileMode.Create`.
  - Correction additionnelle: l'application du masque crée maintenant une image noire et copie l'image source uniquement où le masque est blanc.
  - Correction additionnelle: les PNG de masque sauvegardent maintenant le masque en RGB noir/blanc en plus de l'alpha, pour rester valides même si l'alpha est ignoré à la relecture.
  - Ajout d'un log `ApplyMask: pixels masque=...` pour confirmer qu'un masque non vide est réellement appliqué.

- `REV-0023-binary-mask-png`
  - Correction du format de sauvegarde des fichiers `*_mask.png`.
  - Les masques sont maintenant sauvegardés comme PNG grayscale noir/blanc sans canal alpha.
  - Motif: les PNG avec alpha pouvaient être affichés ou relus comme blancs partout, ce qui rendait l'application du masque inefficace.
  - Ajout d'un log `SaveMask: pixels masque=...` pour confirmer que le fichier sauvegardé contient bien un masque non plein écran.

- `REV-0024-save-displayed-mask`
  - La sauvegarde du masque prend maintenant explicitement l'image affichée dans `picBox_liveMaskLum` au moment de la sauvegarde.
  - Le fichier `*_mask.png` existant est écrasé par ce masque affiché, sans recalcul depuis la photo capturée.
  - Objectif: revenir au principe initial: le masque sauvé correspond exactement au blob visible dans le PictureBox.

- `REV-0025-no-mask-write-on-capture`
  - Pendant `device_ImageReady`, une capture photo n'écrit plus de fichier `*_mask.png`.
  - Si le masque sauvegardé est disponible, il est lu et appliqué.
  - Si le masque sauvegardé n'est pas disponible ou pas accessible, la capture applique le `maskMatLive` courant en mémoire sans écrire sur disque.
  - Motif: éviter les erreurs réseau/partage du type `Access to the path ... *_mask.png is denied` pendant la prise de photo.

- `REV-0026-mask-inset`
  - Réintroduction d'un léger rétrécissement du masque avant sauvegarde et avant application sur les photos.
  - Le masque est binarisé puis érodé avec un kernel rectangle 3x3, une itération.
  - Objectif: retirer le fin contour blanc autour de l'objet pour éviter que Metashape l'interprète comme faisant partie de la roche.

- `REV-0027-nonblocking-focus-mask`
  - `nikonDoFocus()` ne propage plus une exception Nikon d'autofocus vers les séquences.
  - En cas d'échec autofocus Nikon, l'erreur est loggée dans la console et le code tente de relancer le live view si nécessaire.
  - Pendant une capture avec masque, si aucun masque sauvegardé ou live n'est disponible, l'image est sauvegardée sans masque au lieu d'arrêter la séquence.
  - Si aucun masque n'est affiché dans `picBox_liveMaskLum`, la sauvegarde du masque est ignorée avec un log console au lieu de lever une exception.

- `REV-0028-live-mask-shrink`
  - Ajout de deux valeurs projet `MaskShrink_1` et `MaskShrink_2` pour contrôler la contraction du masque par algorithme.
  - Les valeurs sont chargées depuis le projet au démarrage/ouverture et mises à jour via les sliders/labels du Designer.
  - Le masque affiché dans `picBox_liveMaskLum` est le masque déjà contracté.
  - `maskMatLive`, le focus map, l'application sur photo et la sauvegarde disque utilisent ce même masque affiché, sans recontraction additionnelle.
  - La sauvegarde du masque reste un PNG grayscale noir/blanc sans canal alpha.
  - La méthode de sauvegarde a été renommée `SaveMaskAsPngNoTransparency` pour refléter ce comportement.

- `REV-0029-thumbnail-layout`
  - Refonte visuelle des miniatures ajoutées dans `flowLayoutPanel1`.
  - Le titre utilise maintenant `Path.GetFileNameWithoutExtension(imagePath)` pour afficher le vrai nom du fichier.
  - Le label du titre utilise `AutoEllipsis` et un tooltip avec le nom complet.
  - Le bouton de suppression utilise l'icône Phosphor `` au lieu du `X` rouge.
  - Le header, la taille du titre, la largeur du bouton et la taille de l'icône sont recalculés quand les boutons `+/-` changent la taille des miniatures.
  - La hauteur du header dépend de la taille de police du titre et de l'icône pour éviter que Phosphor déborde verticalement.

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

## REV-0030-camera-settings-load

- Les infos visibles dans `Settings/Caméra` sont maintenant chargées par `LoadCameraSettings()`.
- Chaque lecture de setting caméra passe par `TryLoadCameraSetting(...)` pour qu'une erreur Nikon sur une capacité ne bloque pas l'affichage des settings suivants.
- Les ComboBox sont remplis avec `_isInitializingCameraSettings = true`; les handlers `SelectedIndexChanged` quittent immédiatement pendant cette phase.
- Important: remplir une ComboBox déclenche quand même `SelectedIndexChanged` dans WinForms. Il ne faut donc pas envoyer de `SetEnum` / `SetUnsigned` pendant le chargement initial.
- `GetLiveViewSize()` ne force plus la taille live view à l'index `2` pendant une simple lecture. Le changement de taille doit passer par l'action utilisateur sur `comboBox_TailleLiveView`.
- Les méthodes réactivées/branchées au chargement incluent:
  - type d'image
  - dimensions image
  - shutter speed
  - dimensions live view
  - mode d'exposition
  - focus mode
  - AF mode
  - AF-C priority
  - focus area mode
  - live view AF mode

### À Surveiller

- Si une capacité Nikon gèle complètement dans `device.GetEnum(...)` ou `device.GetUnsigned(...)`, le `try/catch` ne peut pas reprendre tant que l'appel natif ne retourne pas.
- Dans ce cas, désactiver temporairement la capacité fautive dans `LoadCameraSettings()` et tester une lecture manuelle isolée avec la caméra connectée.
- Ne pas remettre de `SetEnum` dans les méthodes `Get...`; ces méthodes doivent rester des lectures pures.

## REV-0031-sequence-email-notifications

- Ajout d'un service de notification courriel pour les fins de séquences photo.
- Les destinataires de l'onglet `Settings/Messagerie` sont maintenant persistés dans `AppSettings.MessagingUsers`, incluant l'état coché/non coché de chaque checkbox.
- Les notifications sont envoyées après:
  - la routine totale;
  - une série individuelle 5°;
  - une série individuelle 25°;
  - une série individuelle 45°.
- La séquence de calibration/mesure n'envoie pas de notification courriel.
- Quand le focus stack est actif pour la séquence, l'envoi attend la fin de la file de focus stack via `WaitForFocusStackQueueIdleAsync(...)` avant de composer le rapport.
- Le rapport envoyé contient le nom du projet, le nom de la séquence, l'état final, la durée, l'état du focus stack, les compteurs de focus stacks réussis/échoués/en attente et la liste des focus stacks échoués.
- La configuration SMTP est ajoutée dans `AppSettings`:
  - `SmtpHost`
  - `SmtpPort`
  - `SmtpEnableSsl`
  - `SmtpUser`
  - `MailFrom`
  - `SmtpPasswordProtected`
- Le mot de passe SMTP, s'il est utilisé, doit être stocké via `SetSmtpPassword(...)`, qui le protège avec DPAPI pour l'utilisateur Windows courant.
- Les erreurs d'envoi courriel sont loggées dans la console et ne doivent pas faire échouer la séquence photo.

## REV-0032-messaging-layout-width

- Ajustement du layout des destinataires dans `Settings/Messagerie`.
- Les contrôles utilisateur du `flowlayoutPanel_Messagerie` utilisent maintenant la largeur disponible du panneau au lieu d'une largeur fixe.
- Le `FlowLayoutPanel` est forcé en `TopDown` avec `WrapContents = false` pour obtenir une liste verticale propre.
- La hauteur de chaque ligne destinataire reste fixe à `32 px`.
- La largeur des lignes est recalculée lors du redimensionnement du panneau.

## REV-0033-messaging-row-layout

- Correction du layout interne des lignes destinataires dans `Settings/Messagerie`.
- La hauteur de ligne passe à `40 px` pour éviter que le texte soit coupé verticalement.
- Le libellé courriel est aligné à gauche, utilise `AutoEllipsis` et une police `Segoe UI 10 pt`.
- Les colonnes checkbox/suppression sont réduites et stabilisées à `34 px`.
- La checkbox est centrée dans sa cellule et le bouton de suppression reste centré sans agrandir la ligne.

## REV-0034-messaging-delete-button-align

- Correction de l'alignement du bouton de suppression dans `Settings/Messagerie`.
- La hauteur réelle des lignes destinataires est harmonisée à `50 px` entre la création et le redimensionnement.
- Le bouton de suppression utilise maintenant un texte `×` en `Segoe UI 11 pt`, avec `TextAlign = MiddleCenter`, padding nul et taille fixe `26 x 26`.
- Objectif: centrer visuellement le `×` dans la cellule de suppression.

## REV-0035-zero-photo-series-skip

- Les textbox du nombre de photos dans `Caméra/Automation` acceptent maintenant la valeur `0`.
- Une valeur `0` affiche `Série ignorée` au lieu de tenter de calculer un angle/diviseur.
- `PrisePhotoSequenceAsync(...)` quitte proprement la série courante si son nombre d'images est `0`, ce qui évite la division par zéro.
- Le calcul des paddings et la liste `listBox_paddingView` gèrent les séries à `0` en les affichant comme ignorées.
- Objectif: permettre de bypasser une série et permettre ensuite de changer la valeur sans bloquer la saisie.

## REV-0036-smtp-settings-form

- Ajout d'un formulaire `SmtpSettingsForm` ouvert depuis `Messagerie/Réglages du serveur d'envoi`.
- Le formulaire permet d'éditer manuellement:
  - serveur SMTP;
  - port;
  - SSL/TLS;
  - utilisateur SMTP;
  - adresse expéditeur;
  - mot de passe.
- Le mot de passe SMTP est maintenant aussi disponible en clair via `AppSettings.SmtpPassword`, selon la préférence utilisateur.
- `AppSettings.GetSmtpPassword()` privilégie `SmtpPassword` si rempli, puis retombe sur `SmtpPasswordProtected`.
- Ajout d'un bouton `Tester` qui envoie un courriel SMTP de test à une adresse saisie.

## REV-0037-manual-autocenter-reset

- Correction du lancement manuel de l'auto-centrage après annulation d'une séquence.
- Le bouton d'auto-centrage remet maintenant `_stopRequested = false` et `cancelAutoCentrage = false` avant de lancer `RoutineAutoCentrage()`.
- Cause: `StopSequences()` laisse `_stopRequested = true` après une annulation; `RoutineAutoCentrage()` respecte ce flag et quittait donc immédiatement.
- La routine totale semblait réparer le problème parce qu'elle appelle `ResetSequenceCancellationButton()`, qui remet `_stopRequested = false`.

## REV-0038-smtp-form-polish

- Ajustement visuel du formulaire `SmtpSettingsForm`.
- Le libellé de sécurité utilise maintenant `Utiliser SSL/TLS` dans une ligne plus large pour éviter que le texte soit coupé.
- Les boutons `Tester`, `Annuler` et `Sauvegarder` utilisent un style sombre cohérent avec l'application et des dimensions fixes.
- Le champ mot de passe affiche le mot de passe enregistré avec des étoiles au chargement.
- Lorsqu'on entre dans le champ mot de passe pour le modifier, le texte redevient visible pendant l'édition.
- Après sauvegarde, le champ mot de passe redevient masqué.

## REV-0039-smtp-form-font-size

- Réduction locale des polices du formulaire `SmtpSettingsForm` à `Segoe UI 8.25 pt`.
- Le formulaire ne dépend pas d'un setting global de police maison; il utilisait explicitement `Segoe UI 10 pt`.
- Les lignes du formulaire, la largeur de la colonne des libellés et les boutons ont été resserrés pour mieux correspondre au reste de l'application.
- Le scaling DPI Windows/WinForms reste appliqué par `ApplicationConfiguration.Initialize()`.

## REV-0040-smtp-form-row-heights

- Correction des hauteurs de rangées du formulaire `SmtpSettingsForm`.
- Les six rangées de champs utilisent maintenant une hauteur fixe unique définie dans le `TableLayoutPanel`, au lieu d'ajouter des `RowStyle` dans `AddControlRow(...)`.
- La rangée des boutons a maintenant sa propre hauteur fixe pour éviter que le texte des boutons soit coupé.
- La checkbox `Utiliser SSL/TLS` utilise la même marge verticale que les champs texte pour garder un alignement uniforme.

## REV-0041-smtp-buttons-height

- Augmentation de la hauteur réelle des boutons du formulaire `SmtpSettingsForm` à `38 px`.
- La rangée des boutons passe à `64 px` pour laisser assez d'espace au rendu texte avec le style `Flat`.
- Suppression des hauteurs locales appliquées avant `StyleDialogButton(...)`, car elles étaient écrasées par le style commun.

## REV-0042-smtp-buttons-width

- Augmentation de la largeur des boutons du formulaire `SmtpSettingsForm`.
- `Tester` et `Annuler` passent à `110 px`; `Sauvegarder` passe à `140 px`.
- Objectif: éviter que le rendu texte soit coupé horizontalement avec le style `Flat` et le scaling DPI.

## REV-0043-zero-series-and-focus-pause

- Si le nombre de photos d'une série est `0`, la routine totale ignore maintenant cette série avant d'envoyer l'actuateur.
- Une série à `0` n'exécute plus l'attente d'actuateur, le retour de table tournante, l'autofocus ou l'auto-centrage pour ce degré.
- Les boutons de séries individuelles 5°, 25° et 45° appliquent la même règle et marquent la série comme `Ignoré`.
- `AutomaticFocusRoutine(...)` accepte maintenant un `CancellationToken` optionnel et respecte la pause pendant ses boucles de recherche de masque et de focus.
- `AutomaticFocusThenCapture(...)` accepte maintenant un `CancellationToken` optionnel et respecte la pause entre les images du focus stack et avant les mouvements de focus.

## REV-0044-lift-xy-pad

- Ajout du contrôle custom `LiftXYPadControl`.
- Le pad X/Y est ajouté dans `tableLayoutPanel39`, colonne 1, à droite du trackbar vertical de l'onglet `Élévateur`.
- Le mouvement horizontal du pad utilise la même plage que `trkBar_LiftHorizontal` et envoie `udpSendLiftHorizontalData(value * -5)`.
- Le mouvement vertical du pad utilise la même plage que `trkBar_LiftVertical` et envoie `udpSendLiftVerticalMotorData(value * 100)`.
- Au relâchement de la souris, le pad revient au centre, remet les deux trackbars à `0`, envoie les deux vitesses à `0` et redemande la position verticale via `stepmotor readData`.
- `displayVerticalLiftData()` écrit maintenant les positions verticales dans la console, car les anciens labels de position verticale ne sont plus présents dans le Designer courant.

## REV-0045-lift-switch-diagnostics

- Ajout d'un diagnostic de switchs dans l'onglet `Élévateur`.
- Si `tableLayoutPanel37` est vide au démarrage, huit boutons sont créés automatiquement: quatre lectures ESP32 et quatre états mémorisés Aérolithe.
- Les boutons nommés `btn_Esp*`, `btn_Aero*` ou `btn_Aerolithe*` dans `tableLayoutPanel37` sont branchés automatiquement au même diagnostic.
- Au clic, Aérolithe envoie `stepmotor switchState` au lift horizontal et au lift vertical, attend brièvement les réponses UDP, puis écrit deux lignes dans la console:
  - `(Esp32) Switch Verticale Max/Min, Switch Horizontale Gauche/Droite`;
  - `(Aerolithe) Switch Verticale Max/Min, Switch Horizontale Gauche/Droite`.
- `StepperSwitchState` est maintenant interprété selon le câblage `INPUT_PULLUP`: `0` signifie switch pesée (`True`), `1` signifie relâchée (`False`).
- Les messages UDP `FarLimitSwitch...` et `NearLimitSwitch...` sont séparés selon l'adresse source: le lift horizontal met à jour les états horizontaux, tandis que le rail caméra garde ses états existants.

## REV-0046-lift-switch-buttons

- Retrait de la création automatique des huit boutons de diagnostic de switchs.
- Le diagnostic utilise maintenant seulement les boutons existants nommés `btn_HorizontalLiftSwitches` et `btn_VerticalLiftSwitches`.
- `btn_HorizontalLiftSwitches` envoie `stepmotor switchState` au lift horizontal, puis affiche seulement:
  - `(Esp32) Switch Horizontale Gauche/Droite`;
  - `(Aerolithe) Switch Horizontale Gauche/Droite`.
- `btn_VerticalLiftSwitches` envoie `stepmotor switchState` au lift vertical, puis affiche seulement:
  - `(Esp32) Switch Verticale Max/Min`;
  - `(Aerolithe) Switch Verticale Max/Min`.

## REV-0047-vertical-switch-button-designer

- Correction du bouton Designer `btn_VerticalLiftSwitches`.
- Le Designer n'utilise plus `this.btn_VerticalLiftSwitches`; il utilise `btn_VerticalLiftSwitches`, comme les autres contrôles générés.
- Ajout du champ `public Button btn_VerticalLiftSwitches;` dans `Aerolithe.Designer.cs`, comme pour `btn_HorizontalLiftSwitches`.

## REV-0048-autocenter-fresh-offsets

- Stabilisation de l'auto-centrage entre les séries photo et après annulation.
- `RoutineAutoCentrage(...)` accepte maintenant un `CancellationToken` et le respecte pendant l'attente de masque, les délais de mouvement et la sortie de cadre.
- Les auto-centrages lancés par les séquences passent maintenant le token de la séquence, afin qu'une annulation arrête aussi la routine d'auto-centrage en cours.
- `StopSequences()` annule aussi l'auto-centrage manuel/actuateur en cours et met `cancelAutoCentrage = true`.
- Les boutons manuels d'actuateur remettent `_stopRequested = false` et `cancelAutoCentrage = false` avant de démarrer, pour éviter qu'une annulation précédente bloque le mode manuel.
- Le flag global `cancelAutoCentrage` n'est plus remis à `true` à la fin normale de l'auto-centrage continu pendant mouvement d'actuateur, car cela pouvait annuler une routine suivante.
- Ajout d'une version interne `_autoCenterOffsetsVersion` pour les offsets de centrage.
- Au début de chaque `RoutineAutoCentrage(...)`, les offsets sont réinitialisés et la routine attend une nouvelle frame LiveView fraîche avant de bouger les moteurs.
- Objectif: éviter que la deuxième série utilise les offsets/masques de la série précédente ou démarre trop tôt après le mouvement d'actuateur + autofocus.
- Le timeout d'attente d'une nouvelle frame valide est maintenant `3000 ms`.
- Quand le masque LiveView devient vide/noir, les offsets sont remis à zéro et leur version est incrémentée pour distinguer une frame reçue sans objet d'une frame pas encore traitée.
