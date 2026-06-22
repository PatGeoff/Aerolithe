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
- Toujours écrire dans ce fichier `INSTRUCTIONS.md` les diagnostics importants, décisions techniques, changements fonctionnels et résultats de validation.
- Toujours compiler en mode `Release` pour valider les changements: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.
- Mettre à jour `UiRevision` dans `Aerolithe.cs` quand un changement fonctionnel est fait.
- Garder les changements visuels/layout dans `*.Designer.cs` autant que possible; éviter les modifications de design appliquées au runtime, sauf nécessité technique explicite.

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

## REV-0049-actuator-autocenter-faster

- Accélération prudente de l'auto-centrage pendant les montées/descentes de l'actuateur.
- Le pulse de correction dans `AutoCentrageStepPendantActuateurAsync(...)` passe de `450 ms` à `250 ms`.
- La pause entre deux corrections continues pendant mouvement d'actuateur passe de `100 ms` à `50 ms`.
- `WaitForActuator(...)` relit maintenant l'angle actuateur aux `250 ms` au lieu de `500 ms`.
- Le polling UDP automatique après une commande actuateur demande aussi l'angle aux `250 ms` au lieu de `400 ms`.
- La tolérance de centrage pendant mouvement reste large (`20 px`) et l'auto-centrage final précis reste exécuté à la position atteinte.

## REV-0050-captured-mask-registration

- Correction du masque appliqué aux images sans focus stack.
- `device_ImageReady(...)` reconstruit maintenant en priorité un masque depuis le JPEG capturé avant d'appeler `ApplyMask(...)`.
- Objectif: éviter qu'un masque sauvegardé ou live plus ancien soit appliqué à une photo prise après un léger déplacement d'auto-centrage.
- Le masque sauvegardé puis le masque live restent utilisés seulement en fallback si le masque reconstruit depuis la capture est indisponible.

## REV-0051-mask-shrink-project-sync

- Renforcement de la synchronisation des valeurs de contraction du masque à l'ouverture d'un projet existant.
- Les trackbars `trackBar_maskShrink1` et `trackBar_maskShrink2` sont maintenant branchés sur `ValueChanged` en plus de `Scroll`, pour couvrir les changements programmatiques, clavier ou molette.
- `GetCurrentMaskShrink()` lit d'abord la valeur courante du trackbar de l'algorithme actif, avec fallback vers les valeurs du projet.
- Objectif: éviter que la séquence utilise une contraction de masque désynchronisée jusqu'à ce que l'utilisateur bouge manuellement le slider.

## REV-0052-actuator-speed-setting

- Ajout de `ActuatorSpeed` dans `AppSettings`, valeur par défaut `500`.
- `textBox_VitesseActuateur` charge cette valeur au démarrage et la sauvegarde sur `Enter` ou perte de focus.
- La vitesse est bornée entre `150` et `1023`.
- Avant chaque commande de mouvement actuateur (`5`, `25`, `45`, `custom`, `up`, `down`), Aérolithe envoie automatiquement `actuator speed, {ActuatorSpeed}` à l'ESP32.
- Les commandes `actuator angle`, `actuator stop`, `actuator speed` et `actuator calibration` ne sont pas préfixées par la vitesse.
- Le firmware `/Users/tech/Documents/Arduino/Aerolithe/Aerolithe_Actuateur` accepte `actuator speed, N`, borne la PWM entre `150` et `1023`, et applique une rampe PWM pour les déplacements vers angle cible.
- Les commandes manuelles `actuator up/down` utilisent la vitesse plafonnée directement, sans découpage par paliers côté application.

## REV-0053-actuator-startup-speed

- Le firmware actuateur démarre maintenant avec `actuatorMaxPwm = 1000`, pour que le retour initial vers `0.0` degré se fasse à vitesse maximale par défaut.
- Le mouvement automatique de démarrage ESP32 cible `0.0` degré au lieu de `5.0`.
- Suppression de la descélération PWM près de la cible: les déplacements vers angle cible utilisent maintenant la vitesse configurée constante, avec seulement la rampe d'accélération existante.

## REV-0054-network-ping-tolerance

- L'onglet Réseau tolère maintenant les pertes ponctuelles: un appareil passe à `NON Connecté` seulement après 3 pings échoués consécutifs.
- Le polling d'angle actuateur revient à un rythme plus calme après commande (`400 ms`) et pendant `WaitForActuator(...)` (`500 ms`).
- Après `actuator speed, X`, Aérolithe attend `75 ms` avant d'envoyer la commande actuateur réelle, pour éviter deux paquets UDP trop collés.

## REV-0055-actuator-autocenter-adaptive-lift

- `AutoCentrageStepPendantActuateurAsync(...)` utilise maintenant une correction verticale plus forte pendant les mouvements d'actuateur.
- Le gain proportionnel pendant actuateur passe de `kP = 0.3` à `kP = 0.35`.
- Le plafond horizontal reste à `maxStepX = 40`.
- Le plafond vertical passe à `maxStepY = 80`, donc la commande `udpSendLiftVerticalMotorData(stepY * 100)` peut atteindre `8000` au lieu de `4000`.
- La tolérance reste `20 px` et la durée d'impulsion reste `250 ms`.
- Rollback si ça oscille ou dépasse trop: remettre `kP = 0.3` et `maxStepY = 40`, ou revenir à un seul plafond commun `maxStep = 40` pour X/Y.

## REV-0056-actuator-autocenter-vertical-gain

- Après analyse de `/Users/tech/Downloads/IMG_7423.MOV`, le décrochage entre `11 s` et `15 s` vient d'un retard vertical: vers `13 s`, l'offset Y atteint environ `107 px` alors que le masque est encore présent, puis le masque est perdu vers `15.5 s`.
- `AutoCentrageStepPendantActuateurAsync(...)` sépare maintenant les gains X/Y: `kPX = 0.35`, `kPY = 0.65`.
- Le plafond vertical passe de `maxStepY = 80` à `maxStepY = 90`; la commande verticale peut donc atteindre `9000`.
- Pour un offset Y d'environ `107 px`, la commande verticale passe d'environ `3700` à environ `6900`.
- Rollback si oscillation ou surcorrection: remettre `kPY = 0.35` et `maxStepY = 80`, ou revenir à la REV-0055.

## REV-0057-osc-no-autocenter-wait

- Les commandes OSC manuelles n'attendent plus la fin d'une tâche d'auto-centrage avant d'être exécutées.
- Avant REV-0057, `CheckOSCMessage(...)` appelait `await StopAutoCenterBeforeManualCommandAsync()` pour toute commande OSC qui n'était pas auto-centrage/calibration, ce qui pouvait introduire un délai de 1-2 secondes.
- Maintenant, une commande OSC manuelle demande seulement `cancelAutoCentrage = true` et continue immédiatement vers l'envoi de la commande.
- L'onglet Réseau redevient strict: un appareil affiche `NON Connecté` selon le résultat du ping courant, sans tolérance de 3 échecs consécutifs.
- Rollback si nécessaire: réintroduire l'attente explicite de la tâche d'auto-centrage, mais seulement pour les commandes qui doivent vraiment être sérialisées avec l'auto-centrage.

## REV-0058-network-udp-status

- L'onglet Réseau n'utilise plus le ping ICMP pour les ESP32, car les commandes UDP peuvent fonctionner même si les microcontrôleurs ne répondent pas au ping.
- `PingAllDevicesAsync()` envoie maintenant la commande UDP applicative `status` à chaque microcontrôleur, sur son port réel.
- `ListenForMessages()` complète le test Réseau quand une réponse `ok esp32` ou `status ok` revient depuis l'adresse IP attendue.
- Le statut `Connecté` reflète donc la communication utilisée par Aérolithe pour piloter les appareils, pas seulement la réponse ICMP.
- Rollback si nécessaire: revenir à `PingHostAsync(...)`, mais cela peut réafficher `NON Connecté` même quand les commandes UDP fonctionnent.

## REV-0059-network-udp-status-retry

- Confirmation firmware: les ESP32 Stepper Camera, Lift Vertical, Lift Horizontal et Actuateur répondent `ok esp32` à `status`; la Table Tournante répond `waveshare --> status ok`.
- Le test Réseau envoie maintenant `status`, attend `300 ms`, réessaie une deuxième fois si aucune réponse acceptée n'est reçue, puis déclare `Connecté` ou `NON Connecté`.
- Le délai maximal de verdict par appareil est donc environ `600 ms`, tout en restant basé sur la communication UDP réelle.
- Rollback si nécessaire: augmenter `timeoutMs` ou `attempts` dans `ProbeDeviceStatusAsync(...)`, sans revenir au ping ICMP.

## REV-0060-actuator-autocenter-vertical-catchup

- Objectif: éviter que l'objet sorte du cadre pendant les mouvements d'actuateur quand le lift vertical ne rattrape pas assez vite.
- `AutoCentrageStepPendantActuateurAsync(...)` garde une correction verticale douce près du centre, puis ajoute un gain de rattrapage quand `abs(offsetY) > 55 px`.
- Le plafond utile vertical est ramené à `firmwareEffectiveMaxStepY = 60`, car le firmware du lift vertical limite `stepmotor movespeed` à `6000`; envoyer `9000` ne donnait donc pas plus de vitesse réelle.
- Pendant l'auto-centrage continu d'actuateur, le lift vertical n'est plus arrêté après chaque impulsion de correction: il continue entre deux mesures jusqu'à la prochaine correction ou jusqu'au retour dans la tolérance.
- Le lift horizontal et le rail linéaire sont toujours arrêtés après la courte impulsion de correction pour éviter une dérive latérale.
- Rollback si ça dépasse ou oscille verticalement: dans `AutoCentrageStepPendantActuateurAsync(...)`, remettre le calcul direct `Math.Abs(offsetY) * 0.65`, remettre `maxStepY = 90`, et rétablir l'arrêt vertical après le `Task.Delay(250, cancellationToken)`.

## REV-0061-vertical-lift-speed-8000

- Firmware lift vertical: dans `/Users/tech/Documents/Arduino/Aerolithe/Aerolithe_Lift_Vertical/stepper.cpp`, `maxSpeed` passe de `6000` à `8000`.
- Application: `CalculateActuatorVerticalStep(...)` ajuste `firmwareEffectiveMaxStepY` de `60` à `80`, pour que `udpSendLiftVerticalMotorData(stepY * 100)` puisse réellement demander jusqu'à `8000`.
- Objectif: donner plus de vitesse verticale au lift pendant l'auto-centrage d'actuateur, sans enlever complètement la limite de sécurité firmware.
- Rollback si pertes de pas, vibration ou dépassement vertical: remettre `maxSpeed = 6000` dans le firmware lift vertical et `firmwareEffectiveMaxStepY = 60` dans `Alignment.cs`.

## REV-0062-osc-actuator-autocenter-timeout

- Les commandes OSC actuateur depuis l'iPad (`actuator_osc_5_btn`, `25`, `45`, `up`, `down`) lancent maintenant le même suivi `StartManualActuatorAutoCenterTracking(...)` que les boutons locaux de l'application.
- Les commandes OSC actuateur de mouvement ne demandent plus l'annulation de l'auto-centrage avant l'envoi de la commande; seul `actuator_osc_stop_btn` conserve le comportement d'arrêt.
- `WaitForActuator(...)` n'utilise plus un timeout fixe de `10000 ms`: le délai est calculé selon l'écart d'angle courant/cible avec `CalculateActuatorWaitTimeoutMs(...)`, entre `10000 ms` et `40000 ms`.
- Objectif: éviter qu'une longue descente, par exemple `45° -> 5°`, arrête l'auto-centrage vers le milieu du déplacement avant que l'actuateur atteigne sa cible.
- Rollback si l'attente devient trop longue: remettre `int timeoutMs = 10000;` dans `WaitForActuator(...)` et retirer les appels `StartManualActuatorAutoCenterTracking(...)` des cas OSC actuateur.

## REV-0063-actuator-final-autofocus-delay

- Après que `WaitForActuator(...)` considère la cible atteinte, Aérolithe attend maintenant `2000 ms` avant de lancer l'autofocus final.
- Ce délai s'applique seulement à l'autofocus final après position atteinte, pas à l'autofocus de récupération déclenché quand le blob est perdu pendant le mouvement.
- Objectif: laisser l'actuateur finir de se stabiliser mécaniquement quand il entre dans la tolérance de position avant de refaire le focus.
- Rollback si le délai ralentit trop les séquences: retirer `await Task.Delay(2000, cancellationToken);` juste avant `TryAutofocusPendantActuateurAsync(...)` dans le bloc `Autofocus final à la position d'actuateur atteinte`.

## REV-0064-actuator-final-delay-only-after-move

- `WaitForActuator(...)` lit maintenant l'angle actuateur au début de l'attente avec `RequestActuatorAngleAsync(...)`.
- Si l'actuateur est déjà dans la tolérance de la cible au début, le délai de `2000 ms` avant l'autofocus final est ignoré.
- Si l'actuateur n'était pas déjà dans la tolérance, le délai de `2000 ms` reste appliqué avant l'autofocus final.
- `CalculateActuatorWaitTimeoutMs(...)` utilise maintenant l'angle initial lu au début de `WaitForActuator(...)` pour calculer le timeout selon la distance réelle à parcourir.
- Objectif: ne pas ralentir le début d'une séquence quand l'actuateur est déjà à la bonne position, par exemple déjà à `5°`.
- Rollback si la lecture initiale cause un délai indésirable: revenir à `CalculateActuatorWaitTimeoutMs(target)` basé sur `actuatorAngle` et appliquer le `Task.Delay(2000, ...)` sans condition.

## REV-0065-form-title-info-menu

- Le titre de la fenêtre principale n'affiche plus la révision; il affiche seulement le projet/titre courant.
- Ajout dans le Designer de l'item `infoRevisionToolStripMenuItem` dans le menu `Fichier`, juste au-dessus de `Quitter Aérolithe`.
- L'item affiche la révision et la date du REV: `REV-0065-form-title-info-menu - 2026-05-22`.
- Un clic sur l'item `Info` ouvre une boîte d'information avec `UiRevision` et `UiRevisionDate`.
- Rollback si nécessaire: remettre `Text = $"{_windowTitleBase} | {UiRevision}";` dans `SetMainWindowTitle(...)` et retirer `infoRevisionToolStripMenuItem` du Designer.

## REV-0066-about-submenu

- Dans le menu `Fichier`, l'item `Info` devient `À propos`.
- La révision et la date ne sont plus affichées directement dans l'item parent et n'ouvrent plus de boîte modale.
- `À propos` contient maintenant un sous-menu désactivé dont le texte est `REV-0066-about-submenu - 2026-05-22`.
- Les items Designer sont `aProposToolStripMenuItem` et `revisionToolStripMenuItem`.
- Rollback si nécessaire: remettre l'item direct `Info - REV-...` avec le handler de clic `infoRevisionToolStripMenuItem_Click(...)`.

## REV-0067-actuator-autocenter-fresh-angle

- `WaitForActuator(...)` utilise maintenant une lecture fraîche via `RequestActuatorAngleAsync(...)` dans chaque itération avant de décider que la cible est atteinte.
- Objectif: éviter qu'une ancienne valeur de `actuatorAngle` fasse croire que l'actuateur est déjà à `45°` ou `5°`, ce qui arrêtait immédiatement l'auto-centrage pendant le mouvement.
- Quand `btn_AutoCentrageActuator` est activé pendant un mouvement, l'application lance maintenant `StartManualActuatorAutoCenterTracking()` immédiatement.
- Quand `btn_AutoCentrageActuator` est désactivé, l'application annule le suivi manuel et envoie `0` aux moteurs lift vertical, lift horizontal et rail linéaire.
- Le sous-menu `À propos` affiche maintenant `REV-0067-actuator-autocenter-fresh-angle - 2026-05-22`.
- Rollback si nécessaire: remettre `await SendActuatorAngleRequestAsync();` puis la vérification sur `actuatorAngle` dans `WaitForActuator(...)`, et retirer le démarrage/arrêt de suivi dans `btn_AutoCentrageActuator_Click(...)`.

## REV-0068-total-sequence-prompt-dark

- La boîte de pause `Routine totale en pause` utilise maintenant un fond `Color.FromArgb(40, 40, 40)`.
- La boîte est centrée à l'écran avec `FormStartPosition.CenterScreen`.
- La bordure système est remplacée par une bordure blanche de 1 px via `FormBorderStyle.None`, `BackColor = Color.White` et `Padding = new Padding(1)`.
- Le texte est blanc et les boutons `Continuer` / `Annuler` sont harmonisés avec le style sombre de l'application.
- Le sous-menu `À propos` affiche maintenant `REV-0068-total-sequence-prompt-dark - 2026-05-22`.
- Rollback si nécessaire: revenir à `FormBorderStyle.FixedDialog`, `StartPosition.CenterParent`, retirer le panneau sombre et rétablir les styles par défaut des labels/boutons.

## REV-0069-email-photo-series-stats

- Le rapport courriel inclut maintenant une section `Photos par serie`, indépendante des statistiques de focus stack.
- Chaque série y indique les photos de rotation complétées: photos réussies, échecs, ou `serie ignoree` si le nombre de photos de la série est `0`.
- `SequencePhotoSeriesStats` a été ajouté au rapport pour conserver `Serie`, `Angle`, `Planned`, `Succeeded`, `Failed` et `Ignored`.
- `PrisePhotoSequenceAsync(...)` marque une photo de série comme réussie après la capture/focus stack lancé avec succès pour une position de table tournante.
- En cas d'erreur pendant une série, le premier échec de la série est ajouté au rapport.
- Le sous-menu `À propos` affiche maintenant `REV-0069-email-photo-series-stats - 2026-05-22`.
- Rollback si nécessaire: retirer `SequencePhotoSeriesStats`, les appels `RegisterSequencePhotoSeries(...)`, `MarkSequencePhotoSucceeded(...)`, `MarkSequencePhotoFailed(...)`, et la section `Photos par serie` dans `SequenceNotificationReport.BuildBody()`.

## REV-0070-focus-stack-queue-timer-idle

- Les échecs de `focus-stack.exe` ne déclenchent plus de `MessageBox`; ils sont loggés dans la console.
- Les erreurs de séquence photo/mesure ne déclenchent plus de boîte d'erreur bloquante via `ShowSequenceErrorMessage(...)` ou `ShowMeasurementSequenceErrorMessage(...)`.
- Les changements de `_stopRequested` passent maintenant par `RequestSequenceStop(...)` / `ClearSequenceStop(...)` pour logger la raison visible dans la console.
- Correction du Designer pour `btn_ResetTimer`: le bouton est déclaré comme champ Designer normal et n'utilise plus `this.btn_ResetTimer = new Button();`.
- `btn_PauseTimer` pause/reprend le chronomètre; `btn_ResetTimer` arrête et remet le chrono à `00h 00m 00s`.
- La pause de séquence met aussi le chronomètre en pause, puis le reprend à la reprise.
- Les images focus stackées incluent maintenant le côté dans le nom de fichier via `GetFocusStackImageFullPath()`: `Base_A_00.jpg` ou `Base_B_00.jpg`.
- Le contrôle de queue focus stack affiche le côté A/B à la place de la série.
- Le contrôle de queue focus stack a deux actions: refaire la prise de photo + focus stack pour cette rotation, ou relancer seulement le focus stack avec les images existantes.
- Les boutons du contrôle de queue focus stack utilisent les icônes Phosphor `` pour reprise photo + focus stack et `` pour focus stack seulement.
- Le contrôle de queue focus stack n'utilise plus de `RichTextBox` par ligne; il utilise des `Label` pour réduire la consommation de handles Windows.
- Le nettoyage de la queue focus stack dispose maintenant les contrôles retirés, et la liste visible est limitée aux 200 derniers rapports terminés/en erreur pour éviter l'épuisement de handles.
- Une erreur ou exception sur une tâche focus stack marque seulement cette tâche en `Erreur`; `ProcessFocusStackQueue()` continue ensuite avec la prochaine tâche en attente.
- Les reprises depuis les boutons du contrôle focus stack réutilisent maintenant la tâche et le contrôle existants au lieu de créer une nouvelle ligne; `focus-stack.exe` est relancé directement sur cette tâche après récupération des images sources.
- La taille des carrés de détection de netteté (`trackBar_blobCount`) est maintenant persistée dans `AppSettings.FocusDetectionBlockScale` et restaurée au démarrage.
- Un timer d'inactivité arrête le LiveView Nikon après 10 minutes sans activité de capture/séquence/focus.
- `AerolitheTabControl` désactive entièrement son rendu custom dans le Designer Visual Studio et ne l'active que dans le process runtime `Aerolithe`, pour éviter les erreurs Designer de type `same key has already been added`.
- Correction de plusieurs incohérences `RowCount`/`ColumnCount` vs `RowStyles`/`ColumnStyles` dans `Aerolithe.Designer.cs`, et `AutoSize = false` explicite sur les `TextBox` Designer pour éviter `TextBoxBase.AdjustHeight(...)` pendant la création des handles dans Visual Studio.
- Le sous-menu `À propos` affiche maintenant `REV-0070-focus-stack-queue-timer-idle - 2026-06-01`.
- À compléter dans une passe suivante: reprise automatique complète d'une séquence échouée avec bypass de la rotation si la reprise échoue au même endroit.

## REV-0071-turntable-skip-current-target

- Avant d'envoyer une commande `turntable,target,speed` pendant une séquence photo ou une séquence d'images de mesure, Aérolithe vérifie maintenant si la table est déjà à la cible.
- La vérification utilise la position en mémoire puis une lecture fraîche via `RequestTurntablePositionAsync(...)`.
- Si la table est déjà dans la tolérance de la cible, la commande de rotation est ignorée et la séquence continue directement.
- La comparaison de position tient compte du retour à zéro sur `4096`, par exemple une position proche de `4096` est considérée proche de la cible `0`.
- Objectif: éviter qu'une reprise depuis la dernière séquence réussie fasse tourner la table inutilement quand elle est déjà à la bonne position.
- Le sous-menu `À propos` affiche maintenant `REV-0071-turntable-skip-current-target - 2026-06-01`.
- Rollback si nécessaire: remettre les appels directs à `UdpSendTurnTableMessageAsync($"turntable,{...},{turntableSpeed}")` suivis de `WaitForTurntablePositionAsync(...)` dans `PriseImagesMesurePourActuateurAsync(...)` et `PrisePhotoSequenceAsync(...)`.

## REV-0072-dynamic-focusstack-phosphor-icons

- Les contrôles `FocusStackReportControl` créés dynamiquement appliquent maintenant la fonte Phosphor embarquée après `InitializeComponent()`.
- Objectif: afficher correctement les icônes Phosphor des boutons de reprise même si la police Phosphor n'est pas installée globalement dans Windows.
- Les boutons de reprise du contrôle focus stack ont maintenant `UseVisualStyleBackColor = false` dans le Designer pour conserver le rendu sombre et les icônes blanches.
- Le sous-menu `À propos` affiche maintenant `REV-0072-dynamic-focusstack-phosphor-icons - 2026-06-01`.
- Rollback si nécessaire: retirer l'appel `Aerolithe.Instance.ApplyBundledPhosphorFontToControl(this);` dans `FocusStackReportControl` et remettre `UseVisualStyleBackColor = true` sur les deux boutons.

## REV-0073-force-tagged-phosphor-icons

- Les boutons d'action du contrôle focus stack portent maintenant `Tag = "PhosphorIcon"` dans le Designer.
- Le chargement de la fonte Phosphor embarquée force maintenant la famille Phosphor sur les contrôles marqués `PhosphorIcon`, même si WinForms avait déjà remplacé `new Font("Phosphor", ...)` par une fonte fallback.
- Les glyphes demandés restent `` (`caret-circle-double-right`, code `57626`) pour reprise photo + focus stack et `` (`caret-circle-right`, code `57634`) pour focus stack seulement.
- Le sous-menu `À propos` affiche maintenant `REV-0073-force-tagged-phosphor-icons - 2026-06-01`.
- Rollback si nécessaire: retirer les tags `PhosphorIcon` et revenir au test basé seulement sur `control.Font.FontFamily.Name == "Phosphor"`.

## REV-0074-focus-stack-retry-visual-and-skip

- Une tâche focus stack relancée depuis les boutons du contrôle est maintenant marquée `IsRetry`.
- Le nom de fichier d'une tâche relancée s'affiche en bleu pâle dans `FocusStackReportControl`, même si le statut demeure `Erreur`, pour distinguer une reprise d'un échec initial.
- `AutomaticFocusThenCapture(...)` retourne maintenant `bool`: `false` quand la capture focus stack ne peut pas démarrer faute de netteté suffisante, sans mettre `_stopRequested = true`.
- Pendant une séquence photo avec focus stack, Aérolithe tente maintenant la routine autofocus puis capture focus stack jusqu'à 2 fois pour une rotation.
- Si les 2 essais échouent, la rotation est marquée échouée dans le rapport, l'image est ignorée, puis la séquence passe à la rotation suivante.
- Le sous-menu `À propos` affiche maintenant `REV-0074-focus-stack-retry-visual-and-skip - 2026-06-01`.
- Rollback si nécessaire: remettre `AutomaticFocusThenCapture(...)` en `Task`, remettre l'appel à `RequestSequenceStop(...)` sur netteté insuffisante, et retirer `IsRetry` / la couleur bleue du contrôle focus stack.

## REV-0075-focus-stack-retry-blur-threshold

- Lorsqu'une capture focus stack échoue au premier essai par netteté insuffisante, le deuxième essai baisse temporairement `blurThreshold` de `10`.
- La baisse du seuil est interne à la tentative de récupération: le slider `trackBar_blurThreshold` n'est pas modifié visuellement et le réglage utilisateur n'est pas sauvegardé.
- Le calcul LiveView utilise maintenant `GetEffectiveBlurThreshold()`, ce qui permet à l'override temporaire d'être réellement appliqué au comptage `blurredBlocks`.
- À la fin de la rotation ou de la reprise, l'override est retiré et `blurThreshold` revient à la valeur configurée dans le slider.
- La reprise `photos + focus stack` utilise aussi deux essais, avec `blurThreshold - 10` au deuxième essai.
- Le sous-menu `À propos` affiche maintenant `REV-0075-focus-stack-retry-blur-threshold - 2026-06-01`.
- Rollback si nécessaire: retirer `_temporaryBlurThresholdOverride`, remettre `blurThreshold = (double)trackBar_blurThreshold.Value` dans `CameraSetup.cs`, et retirer `ApplyTemporaryBlurThresholdForRetry()` / `ClearTemporaryBlurThresholdOverride()` des reprises.

## REV-0076-focus-stack-retry-blur-threshold-20

- Le deuxième essai de récupération focus stack baisse maintenant temporairement `blurThreshold` de `20` au lieu de `10`.
- Le sous-menu `À propos` affiche maintenant `REV-0076-focus-stack-retry-blur-threshold-20 - 2026-06-01`.
- Rollback si nécessaire: remettre `FocusStackRetryBlurThresholdReduction = 10.0`.

## REV-0077-focus-stack-task-mask-path

- `RunExistingFocusStackTaskAsync(...)` passe maintenant explicitement `task.MaskPath` et `task.ApplyMask` à `RunFocusStack(...)`.
- Le masque appliqué après `focus-stack.exe` est donc celui enregistré dans la tâche de queue, pas un masque recalculé depuis l'état courant du projet.
- Avant une reprise masquée, l'ancien PNG masqué correspondant est supprimé pour éviter d'afficher un ancien résultat.
- Si le masque attendu est introuvable, la console logge le chemin et conserve la sortie non masquée.
- Le sous-menu `À propos` affiche maintenant `REV-0077-focus-stack-task-mask-path - 2026-06-01`.
- Rollback si nécessaire: remettre `RunFocusStack(string[] imagePaths, string outputImage)` et retirer l'application explicite de `task.MaskPath`.

## REV-0078-focus-stack-mask-source-images

- Annule l'application du masque après `focus-stack.exe`: le focus stack doit utiliser les images sources déjà masquées, comme pendant une séquence normale.
- `RunFocusStack(...)` reprend sa signature `RunFocusStack(string[] imagePaths, string outputImage)` et ne dépend plus de `MaskPath`.
- Avant une reprise, un ancien PNG masqué portant le même nom de sortie est supprimé pour éviter d'afficher un résultat obsolète généré par une version précédente.
- Objectif: garder le comportement identique entre séquence normale et reprise photo + focus stack; le masque est généré/appliqué pendant la capture des images sources, pas après le stacking.
- Le sous-menu `À propos` affiche maintenant `REV-0078-focus-stack-mask-source-images - 2026-06-01`.
- Rollback si nécessaire: réintroduire l'application post-stack du masque, mais ce n'est pas le comportement souhaité actuellement.

## REV-0079-focus-stack-queue-retry-visuals

- Les boutons de reprise du contrôle `FocusStackReportControl` sont un peu plus hauts dans le Designer pour laisser respirer les icônes Phosphor.
- Le contrôle force maintenant le rendu des glyphes Phosphor en blanc sur les deux boutons de reprise, au lieu de dépendre uniquement du rendu standard WinForms.
- Une tâche relancée affiche maintenant `Reprise - ...` dans le nom de fichier et un fond bleu foncé sur ce label, afin que l'état reprise soit visible même si le bleu pâle du texte est peu perceptible.
- Le sous-menu `À propos` affiche maintenant `REV-0079-focus-stack-queue-retry-visuals - 2026-06-01`.
- Rollback si nécessaire: retirer `ConfigureFocusStackActionButton(...)`, remettre la hauteur `26` du contrôle et retirer le préfixe/fond de reprise dans `FocusStackReportControl.UpdateDisplay()`.

## REV-0080-liveview-idle-timeout-setting

- Ajout de `AppSettings.LiveViewIdleTimeoutMinutes`, valeur par défaut `10`.
- Le champ `textBox_VeilleNikon` charge cette valeur au démarrage et la sauvegarde sur Enter ou perte de focus.
- La valeur est limitée entre `1` et `240` minutes.
- L'arrêt automatique du LiveView Nikon utilise maintenant cette valeur au lieu d'un délai fixe de 10 minutes.
- Le sous-menu `À propos` affiche maintenant `REV-0080-liveview-idle-timeout-setting - 2026-06-01`.
- Rollback si nécessaire: retirer `LiveViewIdleTimeoutMinutes`, remettre `LiveViewIdleTimeout = TimeSpan.FromMinutes(10)` et retirer les handlers de `textBox_VeilleNikon`.

## REV-0081-liveview-idle-sync-ui

- Quand l'arrêt idle coupe le LiveView Nikon, l'état visuel est maintenant synchronisé: le bouton `btn_LiveViewEnable` passe à l'icône OFF et l'image principale revient à `camera_offline`.
- L'arrêt idle ne modifie pas `projet.LiveViewEnabled`; il coupe seulement le LiveView réel pour la session courante.
- Le bouton manuel LiveView rallume correctement le LiveView si le device est OFF, même si la préférence projet est encore ON.
- Le sous-menu `À propos` affiche maintenant `REV-0081-liveview-idle-sync-ui - 2026-06-01`.
- Rollback si nécessaire: remettre l'assignation directe de `btn_LiveViewEnable.Text` dans `ApplyProjectStateToUi()` et dans `btn_LiveViewEnable_Click(...)`, puis retirer `SetLiveViewRuntimeState(...)`.

## REV-0082-metashape-ssh-diagnostic-2026-06-18

- Contexte: le helper LaunchAgent Metashape a déjà été essayé, mais la communication Windows/Parallels vers le Mac ne fonctionnait pas de façon fiable. Ne pas repartir sur cette piste comme solution principale.
- État actuel: le terminal SSH Metashape communique avec `tech@macstuderolithe` et confirme que `/Applications/MetashapePro.app` existe.
- Symptôme observé: la commande `/usr/bin/open -n "/Applications/MetashapePro.app"` retourne `exit=0`, mais Metashape ne s'ouvre pas.
- Tentative `launchctl asuser`: échoue avec `Could not switch to audit session ... Operation not permitted`, puis `launchctl/open exit=1`.
- Tentative `open` direct: retourne `direct open exit=0`, mais `pgrep -fl "Metashape|MetaShape"` ne trouve aucun processus Metashape après attente.
- Test comparatif: `/usr/bin/open -n /System/Applications/Utilities/Terminal.app` lancé depuis la console SSH d'Aerolithe ouvre bien Terminal sur le Mac.
- Conclusion mise à jour: l'ouverture GUI via SSH fonctionne au moins pour Terminal; le problème semble donc spécifique à Metashape, à son bundle, ou aux arguments passés par `open`.
- Test Metashape sans argument: `/usr/bin/open -n "/Applications/MetashapePro.app"` lancé depuis la console SSH d'Aerolithe ouvre bien Metashape, et `pgrep` trouve le processus.
- Conclusion mise à jour: `open` via SSH fonctionne pour Metashape quand il n'y a pas d'arguments. Le problème vient probablement de la forme `/usr/bin/open -n "$METASHAPE_APP" --args -r "$SCRIPT_PATH"` ou du script passé à Metashape.
- Prochaine piste à privilégier: tester `open -n "/Applications/MetashapePro.app" --args -r "<script>"`, puis lancer directement l'exécutable `/Applications/MetashapePro.app/Contents/MacOS/MetashapePro -r <script>` via SSH avec log stdout/stderr.

## REV-0083-metashape-direct-executable-ssh

- Test confirmé: `/usr/bin/open -n "/Applications/MetashapePro.app" --args -r "<script>"` ne lance pas correctement Metashape depuis la console SSH d'Aerolithe.
- Test confirmé: `/Applications/MetashapePro.app/Contents/MacOS/MetashapePro -r "<script>"` lance bien Metashape et exécute le script.
- Erreur observée dans ce test direct: après création/sauvegarde initiale du `.psx`, le deuxième `doc.save(PROJECT_PATH)` échoue avec `OSError: Document.save(): editing is disabled in read-only mode`.
- `LaunchMacMetashapeViaSsh(...)` utilise maintenant l'exécutable direct Metashape (`METASHAPE_EXE`) au lieu de `launchctl asuser open` ou `open --args`.
- Le lancement direct est fait via `nohup ... -r <script>`, en arrière-plan, avec log dans `<projet>_metashape_ssh.log` et pid dans `<projet>_metashape.pid`.
- Le script Python Metashape généré supprime maintenant l'ancien `<projet>.psx` et l'ancien dossier `<projet>.files` avant de recréer un projet, afin d'éviter de réouvrir un projet précédent en mode read-only.
- Vérification: `dotnet build Aerolithe.csproj -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.
- Rollback si nécessaire: remettre `LaunchMacMetashapeViaSsh(...)` sur `/usr/bin/open -n "$METASHAPE_APP" --args -r "$SCRIPT_PATH"` et retirer `import shutil` + la suppression du `.psx/.files` dans le script généré.

## REV-0084-metashape-terminal-command-ssh

- Après test utilisateur, le bouton `Lancer Metashape` affichait encore l'ancien log `Méthode 1: launchctl asuser open` / `Méthode 2: open direct`, donc l'exécutable lancé n'incluait pas la correction `REV-0083`.
- Ajustement supplémentaire: ne plus lancer Metashape par `nohup ... &`, car le test validé avait été fait depuis un vrai terminal SSH en avant-plan.
- `LaunchMacMetashapeViaSsh(...)` génère maintenant le fichier `<projet>_run_metashape.command`, lui applique `chmod +x` via SSH, puis lance `/usr/bin/open -a Terminal "<commande.command>"`.
- Objectif: ouvrir une vraie fenêtre Terminal sur le Mac, puis exécuter dedans `/Applications/MetashapePro.app/Contents/MacOS/MetashapePro -r <script>`, qui est la méthode validée manuellement.
- Le message de lancement affiche maintenant aussi le chemin `Commande Mac`, en plus du script et du log SSH.
- `UiRevision` passe à `REV-0084-metashape-terminal-command-ssh`.
- Vérification: `dotnet build Aerolithe.csproj -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.
- Rollback si nécessaire: remettre `LaunchMacMetashapeViaSsh(...)` sur le lancement direct `nohup "$METASHAPE_EXE" -r "$SCRIPT_PATH" ... &`.

## REV-0085-metashape-command-unix-encoding

- Erreur observée dans Terminal macOS lors de l'exécution du fichier `<projet>_run_metashape.command`: `line 1: ﻿#!/bin/bash: No such file or directory` puis `set: invalid option`.
- Cause: le fichier `.command` était écrit avec `Encoding.UTF8`, ce qui peut produire un BOM, et `StringBuilder.AppendLine()` produit des fins de ligne Windows depuis Aerolithe.
- Correction: `WriteMacMetashapeCommand(...)` écrit maintenant le `.command` en UTF-8 sans BOM (`new UTF8Encoding(false)`) et force des fins de ligne Unix `\n`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.
- Rollback si nécessaire: revenir à `AppendLine(...)` et `File.WriteAllText(..., Encoding.UTF8)`, mais cela réintroduit le risque de script illisible par macOS.

## REV-0086-metashape-terminal-grep-delay

- Le délai avant `pgrep -fl "Metashape|MetaShape"` après ouverture du Terminal Mac passe à 3 secondes.
- Le message SSH précise maintenant que Terminal est ouvert et que Metashape démarre depuis la fenêtre Terminal Mac.
- Si `pgrep` ne trouve rien après 3 secondes, le message dit de vérifier la fenêtre Terminal Mac au lieu de laisser croire que l'ouverture SSH a forcément échoué.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.

## REV-0087-metashape-open-then-run-script

- Nouveau test demandé: ouvrir Metashape d'abord, attendre, puis lancer le script Python ensuite.
- Le fichier `<projet>_run_metashape.command` généré fait maintenant:
  1. `/usr/bin/open -n "/Applications/MetashapePro.app"`
  2. attente jusqu'à 15 secondes que `pgrep -fl 'Metashape|MetaShape'` trouve le process
  3. lancement direct de `/Applications/MetashapePro.app/Contents/MacOS/MetashapePro -r <script>`
- Objectif: utiliser le fait confirmé que Metashape s'ouvre sans argument via SSH, puis exécuter le script après initialisation de l'app.
- `UiRevision` passe à `REV-0087-metashape-open-then-run-script`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.
- Point à vérifier en test réel: si Metashape accepte de recevoir `-r <script>` après qu'une instance GUI est déjà ouverte, ou si cette commande ouvre une deuxième instance/échoue.

## REV-0088-metashape-ssh-open-test-command

- Ajout d'un diagnostic demandé avant de continuer la correction Metashape: afficher dans la console SSH la commande exacte pour ouvrir Metashape sans script.
- `Terminal SSH Metashape` affiche maintenant aussi un test complet copiable:
  `/usr/bin/open -n "$METASHAPE_APP"; echo open_exit=$?; sleep 3; pgrep -fl "Metashape|MetaShape" || echo Aucun process Metashape`
- Le message du bouton `Lancer Metashape` affiche aussi une commande fixe de test sans script:
  `/usr/bin/open -n "/Applications/MetashapePro.app"; echo open_exit=$?; sleep 3; pgrep -fl "Metashape|MetaShape" || echo Aucun process Metashape`
- `UiRevision` passe à `REV-0088-metashape-ssh-open-test-command`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.

## REV-0089-metashape-open-sleep-8

- Test utilisateur: la commande avec `sleep 3` ne fonctionne pas de façon fiable, mais la commande suivante fonctionne:
  `/usr/bin/open -n "/Applications/MetashapePro.app"; echo "open metashape exit=$?"; sleep 8; pgrep -fl "Metashape|MetaShape" || echo "Aucun Metashape trouvé"`
- `Terminal SSH Metashape` affiche maintenant cette commande validée par défaut.
- Le message du bouton `Lancer Metashape` affiche aussi cette commande validée.
- Le fichier `<projet>_run_metashape.command` utilise maintenant cette même logique avant de lancer le script Python: `open -n`, echo exit code, attente 8 secondes, `pgrep`, puis `MetashapePro -r <script>`.
- `UiRevision` passe à `REV-0089-metashape-open-sleep-8`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.

## REV-0090-metashape-terminal-single-line

- Correction demandée: le champ `Commande Mac` du `Terminal SSH Metashape` ne doit contenir qu'une seule ligne simple, pas toute la séquence de diagnostic (`SSH_OK`, détection executable/app, echoes multiples).
- `BuildDefaultMetashapeTerminalCommand()` retourne maintenant uniquement:
  `/usr/bin/open -n "/Applications/MetashapePro.app"; echo "open metashape exit=$?"; sleep 8; pgrep -fl "Metashape|MetaShape" || echo "Aucun Metashape trouvé"`
- `UiRevision` passe à `REV-0090-metashape-terminal-single-line`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.

## REV-0091-metashape-simple-calibration-import

- Test demandé après validation de l'ouverture Metashape: ouvrir Metashape avec la ligne validée, puis lui envoyer un script Python minimal.
- Le script Metashape généré est temporairement réduit à un test simple:
  - créer/nettoyer le projet;
  - créer un chunk `Aerolithe`;
  - charger uniquement les images de calibration/mesure depuis `images/mesures/serie_A`;
  - sauvegarder le projet;
  - arrêter le script avec `return`.
- Les étapes `detectMarkers`, import focus stacks, alignement et modèles restent dans le générateur mais ne sont plus atteintes pendant ce test simple.
- `UiRevision` passe à `REV-0091-metashape-simple-calibration-import`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.
- Rollback/prochaine étape: retirer le `return` après l'import calibration pour réactiver progressivement la suite du pipeline.

## REV-0092-metashape-ssh-actions

- Changement demandé: éviter de démarrer plusieurs instances Metashape. Les commandes vérifient maintenant `pgrep -fl "Metashape|MetaShape"` avant d'ouvrir Metashape; si une instance existe, elle est réutilisée/loggée au lieu d'ouvrir une nouvelle instance.
- `Metashape > Settings` demeure l'endroit où configurer le chemin de l'app, l'hôte SSH, l'utilisateur SSH et la clé SSH.
- Le dialogue `Lancer Metashape` ne montre plus les champs app/SSH; il garde seulement le choix de projet et les cases d'étapes.
- `Terminal SSH Metashape` contient maintenant trois commandes séparées avec leurs boutons:
  - `Démarrer Metashape`
  - `Importer mesures`
  - `Marqueurs`
- Le bouton `Importer mesures` génère/lance un script Python séparé `<projet>_metashape_import_measures.py` qui crée un chunk et importe les images `images/mesures/serie_A`.
- Le bouton `Marqueurs` génère/lance un script Python séparé `<projet>_metashape_detect_markers.py` qui ouvre/utilise le projet courant et exécute `chunk.detectMarkers(...)`.
- `UiRevision` passe à `REV-0092-metashape-ssh-actions`.
- Vérification: `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true` passe, avec les avertissements existants du projet.
- Point à vérifier en test réel: si lancer `MetashapePro -r <script>` depuis SSH agit sur l'instance ouverte ou démarre une instance CLI séparée; si ce n'est pas fiable, il faudra chercher une méthode IPC propre à Metashape.

## REV-0093-metashape-open-project-after-script

- Diagnostic utilisateur: la commande `Importer mesures` affiche que Metashape a bien exécuté le script (`AddPhotos`, `SaveProject`, projet sauvegardé), mais les images ne sont pas visibles dans la fenêtre Metashape déjà ouverte.
- Interprétation: `MetashapePro -r <script>` semble exécuter le script dans le processus lancé par la commande SSH, pas dans l'état mémoire de la fenêtre GUI déjà ouverte. Le projet `.psx` est sauvegardé, mais la fenêtre existante peut rester sur son document courant.
- Référence Agisoft: la documentation Python indique que les scripts peuvent être lancés depuis la console, `Tools > Run Script`, ou la ligne de commande avec `-r`; elle ne documente pas de mécanisme SSH permettant d'injecter un script dans une instance GUI déjà ouverte.
- Correction: après `MetashapePro -r <script>`, la commande SSH exécute maintenant `open -a "$METASHAPE_APP" <projet.psx>` pour demander explicitement à la GUI Metashape d'ouvrir/recharger le projet sauvegardé.
- `UiRevision` passe à `REV-0093-metashape-open-project-after-script`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0094-metashape-command-batches

- Demande utilisateur: générer un batch au lieu de dépendre seulement d'une longue commande SSH directe.
- Sur macOS, les batches générés sont des fichiers `.command` dans le dossier Metashape du projet:
  - `<projet>_metashape_start.command`
  - `<projet>_metashape_import_measures.command`
  - `<projet>_metashape_detect_markers.command`
- `Terminal SSH Metashape` affiche maintenant des commandes courtes qui ouvrent ces fichiers `.command` dans Terminal sur le Mac avec `/usr/bin/open -a Terminal <batch>`.
- Les batches vérifient d'abord si Metashape est déjà ouvert, évitent de démarrer une nouvelle instance si possible, exécutent le script Python concerné, puis ouvrent le projet `.psx` dans Metashape pour rendre le résultat visible dans la GUI.
- `UiRevision` passe à `REV-0094-metashape-command-batches`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0095-metashape-no-batches

- Demande utilisateur: retirer l'approche batch `.command`; le test a montré que le projet devient visible dans la GUI, mais le batch n'est pas nécessaire.
- Les champs du `Terminal SSH Metashape` reviennent à des commandes SSH directes.
- Diagnostic retenu: le projet s'ouvrait en read-only parce que la GUI Metashape gardait déjà le `.psx` ouvert pendant que le script CLI essayait de le modifier.
- Correction retenue sans batch: les commandes `Importer mesures` et `Marqueurs` ferment d'abord Metashape via AppleScript, attendent que le processus disparaisse, exécutent `MetashapePro -r <script>`, puis rouvrent le projet `.psx` dans Metashape.
- La commande `Démarrer Metashape` ne ferme rien; elle vérifie seulement si Metashape est déjà ouvert avant d'appeler `/usr/bin/open -n`.
- `UiRevision` passe à `REV-0095-metashape-no-batches`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0096-metashape-python-console-injection

- Nouvelle stratégie demandée: garder `Démarrer Metashape` en SSH, puis demander à l'instance GUI déjà ouverte d'exécuter du Python.
- `Terminal SSH Metashape` contient maintenant un `ListBox` vide `Scripts Python`, pour préparer une future sélection de scripts.
- Les boutons `Envoyer import Python` et `Envoyer marqueurs Python` n'utilisent plus `MetashapePro -r`; ils lisent le script Python généré, construisent `exec("<code>")`, le copient dans le presse-papiers du Mac avec `pbcopy`, activent le processus Metashape via `System Events`, collent le code dans la console Python et appuient sur Entrée.
- Hypothèse de test: la console Python de Metashape doit être active/focalisable, et macOS doit autoriser `System Events` à contrôler l'interface. Si macOS bloque l'automatisation, il faudra autoriser Terminal/sshd/osascript dans Accessibility.
- `UiRevision` passe à `REV-0096-metashape-python-console-injection`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0097-metashape-custom-python-send

- Correction demandée: le `ListBox`/champ précédent ne permettait pas de tester librement chaque script.
- `Terminal SSH Metashape` contient maintenant un vrai `TextBox` multiligne éditable `Code Python` avec un bouton `Envoyer`.
- Le bouton `Envoyer` lit le code Python saisi au moment du clic, construit `exec("<code>")`, l'envoie à l'instance Metashape ouverte via `pbcopy` + `System Events`, puis appuie sur Entrée.
- Les boutons `Envoyer import Python` et `Envoyer marqueurs Python` restent disponibles pour envoyer les scripts générés.
- `UiRevision` passe à `REV-0097-metashape-custom-python-send`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0098-metashape-osascript-permission-diagnostic

- Diagnostic utilisateur: l'envoi Python copie bien le code dans le presse-papiers du Mac, mais `System Events` échoue avec `osascript n'est pas autorisé à envoyer des saisies de touches (1002)`.
- Conclusion: la méthode de collage/touche vers la GUI Metashape dépend de l'autorisation macOS Accessibility pour `/usr/bin/osascript` ou le processus qui lance `osascript`.
- Correction: la commande d'envoi Python affiche maintenant explicitement que le code est copié dans le presse-papiers et indique d'autoriser `/usr/bin/osascript` dans `Réglages système > Confidentialité et sécurité > Accessibilité` si l'erreur 1002 apparaît.
- Limite: sans cette autorisation, Aerolithe ne peut pas simuler `Cmd+V`/Entrée dans Metashape via SSH.
- `UiRevision` passe à `REV-0098-metashape-osascript-permission-diagnostic`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0099-metashape-ssh-utf8-output

- Diagnostic utilisateur: les messages SSH affichent des caractères mal décodés comme `envoyÃ©` au lieu de `envoyé`.
- Cause probable: la sortie `ssh` UTF-8 du Mac était lue par .NET avec l'encodage local Windows par défaut.
- Correction: `RunMacSshCommandForHost(...)` force maintenant `StandardOutputEncoding = Encoding.UTF8` et `StandardErrorEncoding = Encoding.UTF8`.
- Correction complémentaire: les commandes SSH distantes préfixent maintenant `export LANG=fr_CA.UTF-8 LC_ALL=fr_CA.UTF-8;` pour garder une locale UTF-8 côté Mac.
- `UiRevision` passe à `REV-0099-metashape-ssh-utf8-output`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0100-metashape-python-normalize-ascii

- Diagnostic utilisateur: le code collé dans la console Metashape contenait `\r\n`, des caractères accentués mal rendus (`S√©lectionner`) et une indentation racine invalide, causant `IndentationError: unexpected indent`.
- Correction sûre: le code Python envoyé par `Envoyer` est maintenant normalisé avant `exec(...)`: `\r\n` et `\r` deviennent `\n`, puis les blancs au début/fin du bloc sont retirés.
- Limite volontaire: Aerolithe ne modifie pas l'indentation interne, car corriger automatiquement des espaces pourrait casser les blocs Python `if`, `for`, `def`, etc. Le code saisi doit rester une indentation Python valide.
- Correction complémentaire: les scripts Metashape générés pour import/markers utilisent maintenant des messages ASCII dans la console Metashape pour éviter le mojibake dans l'interface Agisoft.
- `UiRevision` passe à `REV-0100-metashape-python-normalize-ascii`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0101-metashape-focus-console-before-paste

- Demande utilisateur: avant de coller le script Python, dire à Metashape d'aller à la Console et d'être prêt à recevoir le collage.
- Correction: la séquence `osascript` active d'abord le processus Metashape, tente de cliquer le menu `View > Console`, puis tente la variante `View > Panes > Console`.
- Correction complémentaire: après l'ouverture de la console, `osascript` clique près du bas de la fenêtre Metashape pour donner le focus à la zone de console avant `Cmd+V` et Entrée.
- Limite: Metashape utilise une interface Qt; les noms de menus et les coordonnées peuvent varier selon la langue, l'état des panneaux et la taille de fenêtre. Si le focus ne tombe pas dans la console, il faudra ajuster la position du clic ou trouver le raccourci clavier exact de la console.
- `UiRevision` passe à `REV-0101-metashape-focus-console-before-paste`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0102-metashape-python-dedent

- Diagnostic utilisateur: le code envoyé à la console Metashape contenait encore une indentation parasite de deux espaces devant les lignes racine après `import Metashape`, causant `IndentationError: unexpected indent`.
- Correction: la normalisation du code Python envoyé à Metashape retire maintenant une indentation commune de 1 à 4 espaces sur les lignes suivant une première ligne non indentée, quand toutes ces lignes non vides ont cette indentation parasite.
- Limite: si le code contient déjà plusieurs lignes racine correctement alignées à colonne 0, Aerolithe ne modifie pas l'indentation.
- `UiRevision` passe à `REV-0102-metashape-python-dedent`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0103-metashape-python-editor-layout

- Demande utilisateur: enlever les champs/boutons `Envoyer import Python` et `Envoyer marqueurs Python`.
- `Terminal SSH Metashape` ne garde maintenant que:
  - `Démarrer Metashape`;
  - un grand champ multiligne éditable `Code Python` avec bouton `Envoyer`;
  - la zone `Sortie`;
  - `Fermer`.
- Le champ `Code Python` utilise une ligne de layout en pourcentage et se redimensionne avec la fenêtre.
- Les helpers morts des anciens scripts d'import/markers dédiés ont été retirés de `MetashapeAutomation.cs`.
- `UiRevision` passe à `REV-0103-metashape-python-editor-layout`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0104-metashape-python-no-console-toggle

- Demande utilisateur: renommer la fenêtre `Terminal SSH Metashape` en `Terminal SSH/Python Metashape`.
- Diagnostic utilisateur: lors de `Envoyer`, la console Python de Metashape se fermait; le clic menu `View > Console` agissait probablement comme un toggle quand la console était déjà ouverte.
- Correction: la séquence `osascript` ne clique plus les menus `Console`/`Panes > Console` avant le collage.
- Nouvelle séquence: activer le processus Metashape, cliquer près du bas de la fenêtre pour focaliser la ligne d'entrée de console, puis envoyer `Cmd+V` et Entrée.
- Limite: la console Metashape doit déjà être visible; Aerolithe ne tente plus de l'ouvrir automatiquement pour éviter de la fermer par toggle.
- `UiRevision` passe à `REV-0104-metashape-python-no-console-toggle`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0105-metashape-pipeline-generator

- Demande utilisateur: pouvoir enchaîner 5-6 commandes Metashape longues dans l'ordre, dont alignement et modèles HR/LR, sans envoyer chaque script séparément.
- Ajout dans `Terminal SSH/Python Metashape` d'une section `Pipeline` avec cases cochables:
  - `Importer mesures`
  - `Détecter marqueurs`
  - `Importer focus stacks`
  - `Aligner photos`
  - `Construire modèle HR`
  - `Construire modèle LR`
- Le bouton `Générer pipeline` remplit le grand champ `Code Python` avec un script séquentiel; il n'envoie pas automatiquement le script, pour permettre une inspection/modification avant `Envoyer`.
- Le script généré sauvegarde explicitement le projet `.psx` après chaque étape avec `doc.save(PROJECT_PATH)`.
- Les modèles HR/LR sont construits/exportés séparément vers les chemins GLB du projet Metashape.
- `UiRevision` passe à `REV-0105-metashape-pipeline-generator`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0106-metashape-terminal-layout

- Demande utilisateur: corriger le visuel de la fenêtre `Terminal SSH/Python Metashape`, où le pipeline, le champ `Code Python`, la sortie et les boutons étaient écrasés ou décalés.
- Correction: le layout du dialogue utilise maintenant 10 rangées cohérentes; `Pipeline` a une hauteur fixe suffisante, `Code Python` et `Sortie` se partagent l'espace disponible, et le bouton `Fermer` reste en bas.
- Demande utilisateur: dans le menu `Metashape`, remplacer `Terminal SSH` par `Terminal SSH/Python`.
- `UiRevision` passe à `REV-0106-metashape-terminal-layout`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0107-metashape-pipeline-clear

- Clarification demandée: `Générer pipeline` ne lance rien dans Metashape; il remplace le champ `Code Python` par un script complet reconstruit à partir des cases cochées. Il faut ensuite cliquer `Envoyer`.
- Correction UX: après génération, la sortie indique explicitement que le champ `Code Python` a été remplacé au complet et qu'il faut cliquer `Envoyer`.
- Correction UX: après génération, le champ `Code Python` revient au début du script pour que l'utilisateur voie immédiatement le contenu généré.
- Ajout demandé: bouton `Clear` dans la rangée `Code Python`, à côté de `Envoyer`, pour vider le code courant.
- Robustesse: l'envoi Python attend maintenant jusqu'à 120 secondes au lieu de 30 secondes, afin de mieux tolérer les scripts de pipeline plus longs.
- `UiRevision` passe à `REV-0107-metashape-pipeline-clear`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0108-metashape-step-pipeline-load

- Diagnostic utilisateur: après avoir exécuté `Importer mesures` et `Détecter marqueurs`, décocher ces étapes, cocher seulement `Importer focus stacks`, générer puis envoyer ne produisait rien de visible dans Metashape.
- Clarification: l'exécution étape par étape doit être supportée; chaque génération crée un script complet pour les étapes cochées seulement, puis `Envoyer` l'exécute.
- Correction: les scripts générés qui ne contiennent pas `Importer mesures` appellent maintenant `ensure_project_loaded(doc)`. Si le document courant contient déjà des chunks, il est utilisé tel quel; sinon le script ouvre le `.psx` sauvegardé avant d'exécuter l'étape demandée.
- Diagnostic ajouté: chaque script généré affiche maintenant `SCRIPT RECU PAR METASHAPE`, la liste des étapes sélectionnées et le chemin du projet avant d'exécuter les étapes. Si ces lignes n'apparaissent pas dans la console Metashape, le problème est l'envoi/focus plutôt que l'étape pipeline.
- `UiRevision` passe à `REV-0108-metashape-step-pipeline-load`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0109-metashape-hr-lr-distance-export

- Demande utilisateur: ajouter des étapes pipeline après l'alignement:
  - `Désactiver mesures`: désactive les caméras/images provenant de `mesures/serie_A`.
  - `Texture modèle HR`: génère UV + texture après `Construire modèle HR`.
  - `Texture modèle LR`: génère UV + texture après `Construire modèle LR`.
  - `Script distance`: exécute `/Volumes/tech/Desktop/Script Metashape Distance.py`.
  - `Zoom modèle 3D`: tente de rafraîchir/zoomer la vue 3D.
  - `Exporter modèles`: exporte HR et LR vers leurs chemins GLB.
- Les textures utilisent `page_count=2`, `texture_size=4096`, `fill_holes=True` et `ghosting_filter=True`.
- Le build HR/LR tente de conserver deux assets modèles distincts avec `replace_asset=False` et labels `Aerolithe HR` / `Aerolithe LR`, afin que l'export final puisse exporter chaque modèle séparément.
- Le script distance est exécuté dans un namespace contenant `Metashape`, `doc` et `chunk`.
- Limite à valider dans Metashape: selon la version de l'API, la sélection d'un modèle par assignation `chunk.model = model` peut ne pas être supportée; le script logue alors le problème.
- `UiRevision` passe à `REV-0109-metashape-hr-lr-distance-export`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0110-metashape-auto-open-send

- Demande utilisateur: si Metashape n'est pas ouvert, l'envoi Python doit envoyer la commande SSH d'ouverture avant d'essayer de coller le script.
- Correction: `Envoyer` vérifie maintenant `pgrep -fl "Metashape|MetaShape"`; si aucune instance n'est trouvée, il lance `/usr/bin/open -n "/Applications/MetashapePro.app"`, affiche le code de sortie, puis attend jusqu'à 20 secondes que le processus apparaisse.
- Clarification utilisateur: le script `/Volumes/tech/Desktop/Script Metashape Distance.py` fait déjà la sélection des deux targets proches et la création de la scale bar 25 mm. La tentative d'ajouter une étape `Scale bar 25 mm` séparée a été retirée pour éviter de dupliquer cette logique.
- État temporaire REV-0110: le pipeline gardait l'étape `Script distance`, qui exécutait le script externe avant le zoom et l'export. Cette décision est remplacée par REV-0111.
- `UiRevision` passe à `REV-0110-metashape-auto-open-send`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0111-metashape-inline-scale-bar

- Correction utilisateur: ne pas dépendre d'un script externe pour le transform de scale bar; générer le code Python comme les autres étapes.
- Le fichier `/Volumes/tech/Desktop/Script Metashape Distance.py` a été lu et sa logique de transform est intégrée au pipeline: calculer la distance actuelle, calculer `facteur = 0.025 / distance_actuelle`, puis appliquer `Metashape.Matrix.Diag([facteur, facteur, facteur, 1]) * chunk.transform.matrix`.
- L'étape `Script distance` est remplacée par `Scale bar 25 mm`.
- La nouvelle étape `Scale bar 25 mm` choisit les deux marqueurs avec position 3D les plus proches, les sélectionne, crée une scale bar entre eux, met `scale_bar.reference.distance = 0.025`, applique le transform et sauvegarde le projet.
- Limite: la sélection de la paire la plus proche suppose que les marqueurs voisins de grille sont plus proches que les diagonales après alignement; c'est le critère demandé pour éviter les diagonales/hypoténuses.
- `UiRevision` passe à `REV-0111-metashape-inline-scale-bar`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0112-metashape-launch-shares-pipeline

- Demande utilisateur: le bouton `Lancer` doit utiliser exactement les mêmes scripts que `Terminal SSH/Python Metashape`.
- Correction: `Lancer` génère maintenant le même pipeline avec `BuildMetashapePipelinePythonScript(...)` et les mêmes étapes par défaut que le Terminal.
- Correction: sur Mac, `Lancer` envoie le Python à l'instance GUI via la même commande SSH/Python que le bouton `Envoyer`; il ne passe plus par le vieux `.command` + `MetashapePro -r`.
- `Lancer` n'affiche plus la fenêtre de cases d'étapes; il affiche seulement le choix `Normale` ou `Lisse / métallique / peu de détails`, qui active/désactive `Guided Image Matching`.
- Le Terminal a une case `Guided Image Matching`, décochée par défaut, et toutes les étapes Pipeline sont cochées par défaut.
- L'alignement utilise maintenant `keypoint_limit=60000`; `guided_matching` vaut `True` seulement pour l'option lisse/métallique/peu de détails ou la case Terminal.
- Sécurité projet: le pipeline vérifie le `.psx` ouvert; si un autre projet est ouvert, il tente de le sauvegarder puis ouvre le bon projet, sans recréer à neuf si le bon projet est déjà ouvert.
- `UiRevision` passe à `REV-0112-metashape-launch-shares-pipeline`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0113-metashape-console-focus

- Diagnostic utilisateur: `Lancer` démarrait Metashape quand il était fermé, mais le projet restait `Untitled` et rien ne s'exécutait; même constat depuis `Terminal SSH/Python`.
- Cause probable: le Python est bien copié dans le presse-papiers Mac, mais le collage/Entrée ne se rend pas à la console Python Metashape, surtout quand Metashape vient juste d'être ouvert.
- Correction: l'envoi Python attend maintenant jusqu'à 30 secondes le process Metashape, puis attend une fenêtre Metashape avant de coller.
- Correction: l'envoi Python tente d'ouvrir/focaliser `View > Console` ou `View > Panes > Console`, en évitant de cliquer si le menu indique déjà la console cochée.
- Diagnostic ajouté: la sortie SSH affiche maintenant `Metashape déjà ouvert` ou `Metashape non ouvert; démarrage via SSH`, ainsi que la taille du contenu copié dans le presse-papiers avec `pbpaste | wc -c`.
- `UiRevision` passe à `REV-0113-metashape-console-focus`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0114-metashape-visible-window-send

- Diagnostic utilisateur: `Terminal SSH/Python` pouvait envoyer un script généré, mais `Lancer` ouvrait Metashape sans rien coller/exécuter; après un `Cancel` dans Metashape, supprimer/recréer un chunk pouvait aussi laisser les prochains envois sans effet visible.
- Cause probable: la commande AppleScript ciblait le premier process dont le nom contenait `Metashape`; après plusieurs essais, annulations ou instances invisibles, ce process pouvait ne pas être la fenêtre GUI visible.
- Correction: l'AppleScript cherche maintenant explicitement un process Metashape qui possède au moins une fenêtre visible avant de faire `Cmd+V` et Entrée.
- Correction: `Lancer` n'envoie plus tout le pipeline complet dans le presse-papiers; il écrit toujours le fichier `<projet>_metashape.py`, puis colle seulement une commande courte `exec(open(...).read())` dans la console Metashape.
- Le bouton `Envoyer` du Terminal conserve l'envoi du contenu du champ `Code Python`, pour permettre les tests étape par étape.
- `UiRevision` passe à `REV-0114-metashape-visible-window-send`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0115-metashape-save-before-import

- Diagnostic utilisateur: après `Metashape > Lancer`, Metashape restait sur un projet `Untitled`, sans changement visible côté Metashape.
- Correction: le script Python généré attache maintenant explicitement le document Metashape au fichier `<projet>.psx` avant l'étape `Importer mesures`, via `prepare_project_for_import(doc)`.
- `save_step(...)` sauvegarde maintenant avec `doc.save()` quand le document courant est déjà le bon `.psx`, et utilise `doc.save(PROJECT_PATH)` seulement pour attacher/changer le chemin.
- Objectif: faire passer Metashape de `Untitled` au vrai projet dès le début du pipeline, avant l'ajout des chunks/photos.
- `UiRevision` passe à `REV-0115-metashape-save-before-import`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0116-metashape-console-click-diagnostic

- Diagnostic utilisateur: le message `Lancer Metashape` affichait encore la consigne Accessibility même si `/usr/bin/osascript`, `Parallels Desktop` et `sshd-keygen-wrapper` étaient déjà autorisés dans macOS.
- Clarification: cette consigne était affichée systématiquement avant l'appel `osascript`; elle ne signifiait pas que l'erreur 1002 était réellement arrivée.
- Correction: la commande SSH capture maintenant la sortie et le code retour de `osascript`; le message Accessibility n'est affiché que si `osascript` échoue.
- Correction focus: le clic AppleScript avant `Cmd+V` vise maintenant deux points plus bas et plus à gauche dans la fenêtre Metashape, près de la ligne d'entrée de la console Python, au lieu du centre-bas qui pouvait tomber dans la sortie de console.
- `UiRevision` passe à `REV-0116-metashape-console-click-diagnostic`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0117-metashape-run-script-menu

- Décision utilisateur: ne plus dépendre d'un collage dans la console Python pour le bouton `Metashape > Lancer`; Metashape possède `Tools > Run Script...`, qui peut lancer un fichier `.py`.
- Correction: quand `Lancer` génère `<projet>_metashape.py`, l'envoi Mac utilise maintenant AppleScript pour ouvrir `Tools > Run Script...`, puis `Cmd+Shift+G`, colle le chemin complet du script et valide le dialogue.
- Le chemin du script est collé via le presse-papiers Mac plutôt que tapé au clavier, pour éviter les problèmes de layout clavier/caractères.
- Le champ manuel `Terminal SSH/Python > Code Python > Envoyer` garde l'ancienne méthode console, car il sert encore à tester du code libre non sauvegardé en fichier.
- `UiRevision` passe à `REV-0117-metashape-run-script-menu`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0118-metashape-file-log-no-print

- Diagnostic utilisateur: `Tools > Run Script...` lance maintenant bien le fichier, mais Metashape affiche `Run script failed` avec `RichJupyterWidget object has no attribute '_append_custom'`.
- Cause probable: bug interne de la console/Jupyter de Metashape déclenché par les `print(...)` du script généré.
- Correction: les scripts Python Metashape générés n'utilisent plus `print(...)` pour les logs; `log(...)` écrit maintenant dans `<projet>_metashape.log` à côté du `.psx`.
- Objectif: éviter la console Jupyter interne de Metashape pendant l'exécution du script et obtenir un log exploitable sur disque.
- `UiRevision` passe à `REV-0118-metashape-file-log-no-print`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0119-metashape-run-script-open-dialog

- Diagnostic utilisateur: après `Run Script`, la console Metashape affichait une ligne `In [1]:` contenant plusieurs chemins (`/Users/tech/Desktop/Script Metashape Distance.py`, puis le script généré), suivie de l'erreur `RichJupyterWidget`.
- Interprétation: l'AppleScript ouvrait ou ciblait mal le dialogue fichier; les frappes destinées au sélecteur de fichier pouvaient encore tomber dans la console Python.
- Correction: après `Cmd+Shift+G`, l'AppleScript fait maintenant `Cmd+A` avant de coller le chemin du script généré, afin de remplacer toute ancienne valeur ou sélection.
- Correction: après validation du chemin, l'AppleScript clique explicitement le bouton `Open` ou `Ouvrir` du dialogue, avec fallback sur Entrée, au lieu d'envoyer Entrée deux fois.
- `UiRevision` passe à `REV-0119-metashape-run-script-open-dialog`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0120-metashape-background-progress

- Décision technique: abandonner le pilotage fragile de la GUI Metashape par collage console ou dialogue `Run Script` pour le bouton `Metashape > Lancer`.
- `Lancer` génère toujours le script Python, puis démarre maintenant un traitement Metashape en arrière-plan via SSH avec l'exécutable direct `MetashapePro -r <script.py>`.
- Avant le traitement, la commande SSH tente de fermer la GUI Metashape si elle est ouverte, pour éviter que le `.psx` soit verrouillé/read-only pendant l'écriture par le process batch.
- Ajout d'une fenêtre `Progression Metashape` dans Aérolithe: elle reste ouverte pendant que Metashape travaille, affiche les logs du script et du runner SSH, et Aérolithe reste utilisable.
- Le script Python écrit ses étapes dans `<projet>_metashape.log`; le stdout/stderr Metashape est écrit dans `<projet>_metashape_runner.log`.
- La sortie SSH est maintenant lue en streaming pour alimenter la fenêtre de progression au fil de l'eau.
- À la fin, si Metashape retourne `exit=0`, la commande rouvre le `.psx` dans la GUI Metashape.
- `UiRevision` passe à `REV-0120-metashape-background-progress`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0121-metashape-close-confirmation

- Demande utilisateur: ne jamais fermer une fenêtre/projet Metashape en cours sans avertissement explicite, pour éviter de perdre du travail non sauvegardé.
- Avant de lancer le batch Mac, Aérolithe vérifie maintenant par SSH si un process Metashape est actif.
- Si Metashape est ouvert, Aérolithe affiche une confirmation bloquante expliquant que la GUI Metashape doit être fermée pour éviter un projet read-only, et demande de sauvegarder le travail avant de continuer.
- Le bouton par défaut de la boîte est `Non`; si l'utilisateur annule, aucun traitement Metashape n'est lancé.
- Si la vérification SSH échoue, Aérolithe affiche aussi un avertissement et demande une confirmation avant de continuer.
- `UiRevision` passe à `REV-0121-metashape-close-confirmation`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0122-metashape-progress-lr-viewer

- Demande utilisateur: la fenêtre `Progression Metashape` doit montrer l'étape en cours et une barre de progression, sans afficher toutes les lignes de log parce que cela ralentit le reste de l'application.
- La fenêtre de progression garde maintenant seulement les 4 dernières lignes visibles; les logs complets restent écrits dans `<projet>_metashape_runner.log` et `<projet>_metashape.log`.
- La progression UI lit les lignes `[Aerolithe Pipeline] START ...`, `DONE ...` et `PIPELINE TERMINE` pour mettre à jour l'étape courante et la barre de progression.
- Après un traitement Metashape réussi, Aérolithe tente d'ouvrir automatiquement une fenêtre `Viewer GLB LR` sur le fichier `<projet>_LR.glb`.
- Le viewer intégré charge directement le GLB LR exporté et permet une inspection rapide par rotation souris et zoom molette.
- Diagnostic logs utilisateur: `Gibeon_2001_024_C01_metashape.log` montrait un arrêt à `START Texture HR model`; `Gibeon_2001_024_C01_metashape_runner.log` montrait `AttributeError: module 'Metashape' has no attribute 'DiffuseMap'`.
- Correction compatibilité Metashape 2.2.3: le script généré cherche maintenant `Metashape.Model.DiffuseMap`, avec fallback vers `Metashape.DiffuseMap`, puis appelle `buildTexture` sans `texture_type` si aucun enum n'est disponible.
- `UiRevision` passe à `REV-0122-metashape-progress-lr-viewer`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0123-metashape-progress-cancel

- Demande utilisateur: réduire la fenêtre `Progression Metashape` maintenant que seulement 4 lignes sont affichées.
- La fenêtre passe à une taille plus mince (`900x250`) et conserve les mêmes informations utiles: étape courante, barre de progression, 4 lignes récentes et chemins des logs.
- Correction du compteur d'étape: l'affichage utilise maintenant l'ordre canonique des labels du pipeline (`Align photos` = `4/12`) au lieu de `completedSteps.Count + 1`, qui pouvait afficher un rang incohérent si des lignes arrivaient dans un ordre inattendu.
- Ajout d'un bouton `Annuler` dans la fenêtre de progression. Il envoie par SSH un arrêt du process Metashape batch associé au script courant, puis marque le traitement comme annulé dans la console Aerolithe.
- Diagnostic logs utilisateur: les derniers logs fournis ne contenaient pas de traceback ni d'erreur; le batch était rendu à `START Align photos` et le runner affichait encore le matching/alignment Metashape.
- Clarification: le pipeline courant ne construit pas le LR deux fois; il construit HR, texture HR, construit LR, texture LR, puis exporte les deux modèles.
- `UiRevision` passe à `REV-0123-metashape-progress-cancel`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0124-metashape-scalebar-diagnostic

- Diagnostic utilisateur: Metashape échouait à l'étape `Scale bar 25mm` avec `RuntimeError: Pas assez de marqueurs avec position 3D. Aligner les photos avant de creer la scale bar.`, puis `Metashape exit=1`.
- Interprétation: des marqueurs ont été détectés dans les images de mesures, mais moins de deux marqueurs possèdent une position 3D exploitable après l'alignement.
- Décision rejetée par l'utilisateur: ne pas rendre la scale bar optionnelle, car les marqueurs et l'échelle sont essentiels au résultat.
- Cette révision est conservée comme diagnostic seulement; le comportement final est corrigé par `REV-0125-metashape-scalebar-required`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0125-metashape-scalebar-required

- Correction fonctionnelle: `Scale bar 25mm` est maintenant exécutée juste après `Align photos`, avant `Disable measures` et avant la construction HR/LR.
- Motif: l'échelle doit être validée avant de passer du temps à bâtir les modèles, et avant de désactiver les images de mesures.
- La scale bar redevient bloquante: si moins de deux marqueurs possèdent une position 3D, le pipeline s'arrête.
- Le message d'erreur généré est maintenant plus utile: il logue le nombre total de marqueurs, le nombre de marqueurs avec position 3D et le nombre de caméras alignées.
- Le compteur de progression suit le nouvel ordre: `Scale bar 25mm` est maintenant l'étape `5/12`.
- `UiRevision` passe à `REV-0125-metashape-scalebar-required`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0126-metashape-scalebar-triplets

- Demande utilisateur: pour la scale bar, choisir trois marqueurs consécutifs, mesurer les distances `1-2` et `2-3`, puis utiliser la plus petite des deux.
- Le script trie maintenant les marqueurs 3D par label numérique quand possible, sinon par label texte.
- La sélection de scale bar parcourt les triplets consécutifs de marqueurs 3D, compare seulement les paires adjacentes `1-2` et `2-3`, puis choisit la plus petite distance valide.
- Le log Metashape écrit l'ordre des marqueurs 3D et chaque candidat mesuré pour vérifier la sélection.
- La scale bar reste obligatoire: il faut au moins trois marqueurs avec position 3D.
- `UiRevision` passe à `REV-0126-metashape-scalebar-triplets`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0127-metashape-scalebar-quiet-log

- Demande utilisateur: ne pas logger tous les marqueurs/candidats de scale bar.
- Le script garde la logique de sélection par triplets consécutifs, mais ne logue plus l'ordre complet des marqueurs ni chaque distance candidate.
- Le log conserve seulement le diagnostic minimal et la paire effectivement utilisée pour créer la scale bar.
- La création de la scale bar reste faite avec les deux marqueurs choisis par la plus petite distance valide.
- `UiRevision` passe à `REV-0127-metashape-scalebar-quiet-log`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0128-glb-viewer-menu-open

- Demande utilisateur: ajouter un menu `Visualisateur` après `Metashape`, avec une commande pour ouvrir le visualisateur GLB.
- Ajout dans le Designer du menu `Visualisateur > Ouvrir visualisateur GLB`.
- Le visualisateur GLB peut maintenant s'ouvrir sans fichier chargé.
- Ajout d'un bouton `Ouvrir` dans la fenêtre du visualisateur pour sélectionner un fichier `.glb`.
- L'ouverture automatique du visualisateur sur le modèle LR exporté par Metashape est conservée.
- `UiRevision` passe à `REV-0128-glb-viewer-menu-open`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0129-glb-viewer-textures-framing-icons

- Demande utilisateur: afficher les textures du GLB, faire remplir davantage le visualisateur par le modèle, et mettre l'icône Aérolithe sur les fenêtres `Progression Metashape` et `Visualisateur GLB`.
- Le visualisateur GLB lit maintenant les coordonnées `TEXCOORD_0` et les textures base color embarquées dans les matériaux GLB, puis les charge dans OpenGL.
- Le cadrage initial du modèle est resserré pour que le modèle occupe plus d'espace dans le visualisateur.
- Les fenêtres `Progression Metashape` et `Visualisateur GLB` utilisent l'icône de l'application.
- Le diagnostic de scale bar distingue maintenant le cas où les caméras ne sont pas alignées avant la création de la scale bar.
- `UiRevision` passe à `REV-0129-glb-viewer-textures-framing-icons`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0130-metashape-progress-log-tail

- Demande utilisateur: éviter que la fenêtre `Progression Metashape` garde tout le texte du log et ralentisse Aérolithe.
- L'affichage de progression conserve maintenant seulement les 8 dernières lignes reçues.
- Le suivi SSH démarre avec `tail -n 8 -f` au lieu de relire les logs depuis le début.
- Le lecteur de flux SSH garde seulement un tampon court des dernières lignes pour les messages d'erreur, au lieu d'accumuler toute la sortie en mémoire.
- Les fichiers de log complets sur disque restent inchangés.
- `UiRevision` passe à `REV-0130-metashape-progress-log-tail`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0131-metashape-main-console-quiet-marker-estimate

- Demande utilisateur: ne pas écrire les logs détaillés Metashape dans la Main Console; les garder dans `Progression Metashape`.
- En cas d'erreur du traitement suivi, la Main Console reçoit maintenant un message court et renvoie vers la fenêtre de progression et les fichiers logs.
- Le détail SSH/Metashape complet reste affiché dans `Progression Metashape`.
- La scale bar tente maintenant d'estimer les positions 3D des marqueurs à partir de leurs projections sur les caméras alignées et du sparse cloud avant d'échouer.
- Le diagnostic de scale bar indique aussi combien de marqueurs ont des projections sur au moins deux caméras alignées.
- Le visualisateur GLB rend les primitives texturées en double face avec un matériau blanc pour éviter de masquer ou assombrir des portions du modèle photogrammétrique.
- `UiRevision` passe à `REV-0131-metashape-main-console-quiet-marker-estimate`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0132-glb-viewer-gltf-uv-orientation

- Demande utilisateur: le visualisateur GLB applique mal la texture par rapport au viewer web.
- Le viewer ne retourne plus verticalement l'image de texture avant l'upload OpenGL, afin de respecter la convention glTF utilisée par les UV `TEXCOORD_0`.
- Les textures GLB utilisent maintenant `ClampToEdge` au lieu de `Repeat` pour éviter que des UV de bordure affichent une autre portion de la texture.
- `UiRevision` passe à `REV-0132-glb-viewer-gltf-uv-orientation`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0133-metashape-progress-physical-line-buffer

- Demande utilisateur: la fenêtre `Progression Metashape` ne doit pas garder les anciennes lignes en mémoire; seulement les 8 dernières lignes réelles.
- Chaque message reçu est maintenant séparé en lignes physiques avant d'être ajouté au tampon d'affichage.
- Le `TextBox` de progression est réécrit uniquement avec les 8 dernières lignes, puis son historique d'annulation est vidé avec `ClearUndo()`.
- `UiRevision` passe à `REV-0133-metashape-progress-physical-line-buffer`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0134-metashape-scalebar-first-two-markers

- Demande utilisateur: arrêter de bloquer sur les positions 3D des marqueurs et simplement sélectionner deux marqueurs consécutifs pour voir.
- La création de scale bar tente encore d'utiliser les marqueurs 3D quand ils existent.
- Si aucune distance 3D valide n'est disponible, le script prend les deux premiers marqueurs triés, les sélectionne, crée une scale bar de référence 25 mm, sauvegarde, puis continue sans appliquer de facteur d'échelle.
- `UiRevision` passe à `REV-0134-metashape-scalebar-first-two-markers`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0135-metashape-scalebar-preferred-marker-pairs

- Demande utilisateur: les exemples montrent des paires plausibles de marqueurs de mire, plutôt que les deux premiers marqueurs triés.
- En absence de distance 3D, le fallback de scale bar cherche maintenant d'abord des paires préférées comme `19-20`, `11-12`, `5-6`, `21-22`, `14-15`, `23-24` et `1-7`.
- Si aucune paire préférée n'est détectée, le script essaie une paire numérique consécutive, puis seulement ensuite les deux premiers marqueurs triés.
- `UiRevision` passe à `REV-0135-metashape-scalebar-preferred-marker-pairs`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0136-metashape-main-console-status-errors

- Demande utilisateur: garder les messages de statut du traitement Metashape suivi dans la Main Console, et y ajouter aussi les erreurs reçues.
- Le lancement, la fin normale et l'annulation du batch Metashape restent écrits dans la Main Console.
- Les lignes d'erreur reçues du flux Metashape sont aussi copiées dans la Main Console, sans copier le log normal complet.
- Avant de vider les logs Metashape d'un nouveau run, le batch conserve les fichiers précédents en `.previous`.
- `UiRevision` passe à `REV-0136-metashape-main-console-status-errors`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0137-metashape-readonly-guard

- Diagnostic utilisateur: Metashape échoue avec `Document.save(): editing is disabled in read-only mode`, puis la même erreur est répétée sur plusieurs hôtes SSH.
- Le batch Mac attend maintenant jusqu'à 20 secondes après la demande de fermeture de la GUI Metashape; si un processus Metashape reste ouvert, le traitement est annulé avant d'écrire le `.psx`.
- Les scripts Python générés transforment maintenant l'erreur read-only de `doc.save(...)` en message explicite indiquant de fermer toute fenêtre Metashape utilisant le projet cible.
- Le retry SSH ne relance plus le pipeline sur les autres hôtes quand SSH a répondu mais que la commande distante Metashape a retourné un code d'erreur.
- `UiRevision` passe à `REV-0137-metashape-readonly-guard`.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.

## REV-0138-actuator-feedforward-autocenter

- Demande utilisateur: rendre l'auto-centrage pendant mouvement d'actuateur plus smooth, en évitant d'attendre que la météorite dépasse un seuil avant de démarrer le lift vertical.
- `WaitForActuator(...)` initialise maintenant un état de feed-forward vertical avec l'angle de départ et l'angle cible quand l'auto-centrage actuateur est actif.
- `AutoCentrageStepPendantActuateurAsync(...)` ajoute une vitesse verticale anticipée pendant les mouvements ciblés d'actuateur, puis additionne la correction LiveView existante basée sur `offsetY`.
- Le feed-forward vertical utilise une vitesse de croisière prudente de `2200`, ralentit près de la cible, et s'arrête quand l'actuateur est à moins de `2.5°` de la cible.
- `ActuatorAutoCenterFeedForwardVerticalSign = 1` signifie qu'une montée d'angle actuateur envoie une vitesse verticale positive. Si le premier test physique montre que le lift part dans le mauvais sens, mettre cette constante à `-1`.
- Le lift horizontal reste corrigé seulement quand `offsetX` dépasse la tolérance pendant le suivi actuateur, pour réduire le jitter latéral.
- Les arrêts de sécurité ne sont pas adoucis: les fins de course firmware et `stepmotor stop` continuent d'arrêter brutalement les moteurs côté ESP32.
- `UiRevision` passe à `REV-0138-actuator-feedforward-autocenter`.
- Rollback si le suivi vertical dérive, oscille ou part dans le mauvais sens: retirer `BeginActuatorAutoCenterFeedForward(...)`, `ClearActuatorAutoCenterFeedForward(...)`, `CalculateActuatorVerticalFeedForwardSpeed(...)`, remettre le calcul vertical direct `udpSendLiftVerticalMotorData(stepY * 100)` dans `AutoCentrageStepPendantActuateurAsync(...)`, et remettre `UiRevision` à la révision précédente voulue.
- Vérification: compiler avec `dotnet build Aerolithe.csproj -c Release -p:EnableWindowsTargeting=true`.
