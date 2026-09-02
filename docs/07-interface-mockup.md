# XT30 Recipe Manager — interface du mockup

Livraison du 31 août 2026. Framework conservé : **C# / Windows Forms / .NET Framework**, sans nouveau SDK ni dépendance USB. L'interface reprend la structure du mockup fourni : barre de titre claire, sidebar de 250 px, carte caméra, cinq recettes récentes, aide et panneau C1–C7 de 350 px.

## Lancer et compiler

Ouvrir `xt30-probe/xt30-probe.exe`. Conserver le dossier `Assets` à côté de l'exécutable. La fenêtre finale est laissée ouverte après la validation.

Depuis la racine du projet :

```powershell
.\xt30-probe\build.cmd
```

Le script compile la GUI et `xt30-probe-cli.exe` avec le compilateur .NET Framework fourni avec Windows. Fermer la GUI avant de remplacer son exécutable. Une seconde ouverture de la nouvelle GUI active désormais la fenêtre existante, sans lancer une seconde session caméra.

## Checkpoint antérieur aux modifications

- Archive intégrale : `checkpoints/before-ui-20260831-220037/project.zip`.
- Manifeste : `checkpoints/before-ui-20260831-220037/checkpoint.json`.
- 2 008 fichiers, comprenant les sources, exécutables, rapports, documents et repositories de recherche.
- SHA-256 de l'archive : `5DA693CFB895C6EEA8CC98842B8CF862BF4CDE3057FC55E4C672F841A9A9ED41`.
- Le dossier principal n'étant pas un repository Git, cette archive et son manifeste constituent le checkpoint demandé.

## Fichiers créés ou modifiés

Chemins ci-dessous relatifs à `xt30-probe/`.

| Fichier | Rôle |
| --- | --- |
| `Gui.cs` | Point d'entrée WinForms, gestion des exceptions existante, instance GUI unique, modes de validation. |
| `Models/Library.cs` | Bibliothèque locale, recettes, paramètres, compatibilité, favoris, packs et provenance des slots. |
| `Camera/CameraPresenter.cs` | Adaptateur UI : autodétection WPD, appel du scan existant, lecture des rapports et publication des états. Aucun nouvel opcode. |
| `Presentation/DesignSystem.cs` | Couleurs, typographie, cartes, boutons, badges, recherche, icônes et rendu des assets. |
| `Presentation/WindowTitleBar.cs` | Barre de titre, logo, déplacement, réduction, agrandissement et fermeture. |
| `Presentation/MainForm.cs` | Composition responsive, navigation, branchement des actions, Backups et Settings. |
| `Presentation/CameraComponents.cs` | Sidebar, navigation, CameraStatusCard, CameraOverview, CustomSlotRow, ReadOnlyNotice, QuickHelp et barre de statut. |
| `Presentation/RecipeComponents.cs` | RecipeCard, RecipeGrid, RecentRecipes et CompatibilityBadge. |
| `Presentation/RecipePages.cs` | Recherche/filtres, fiche, édition locale des recettes et page Packs. |
| `Presentation/DiagnosticPanel.cs` | Overview, USB, PTP, Properties, Logs, export ZIP et accès au visualiseur historique. |
| `Presentation/UiValidation.cs` | Vérifications hors connexion, captures des pages et capture après scan réel. |
| `Assets/reference-mockup.png` | Référence fournie ; photos temporaires des démonstrations. |
| `Assets/wordmark.png` | Nouveau logo texte fourni, utilisé dans la sidebar. |
| `Assets/app-logo.png` | Nouveau logo application fourni. |
| `Tools/BuildIcons.ps1` | Conversion déterministe du logo en conteneur ICO multirésolution. |
| `app.ico` | Icône 16, 24, 32, 48, 64, 128 et 256 px, incorporée à l'exécutable et à la fenêtre. |
| `build.cmd` | Compilation des modules UI séparés et des exécutables GUI/CLI. |

Les fichiers `README.md` et ce document décrivent la livraison. Les captures et résultats sont dans `xt30-probe/validation/`. Les rapports réels restent à leur emplacement historique et dans `xt30-probe/rapports/`.

## Fonctionnement de l'interface

Les sept destinations sont disponibles : Camera, Recipes, Camera Slots, Packs, Backups, Diagnostics et Settings.

- **Camera** utilise la détection WPD et les métadonnées du rapport. Le scan appelle toujours `Program.Run(false, sweep, outDir)`. Les boutons ouvrent les vrais rapports et leur dossier d'archives.
- **Recipes** propose recherche, huit filtres, favoris persistants, création et modification locales. Cliquer une carte ouvre sa fiche. Les quinze paramètres demandés apparaissent dans leur ordre exact. Les réglages supplémentaires restent conservés dans l'éditeur ; les incompatibilités sont signalées individuellement.
- **Camera Slots** affiche sept affectations **LOCAL / DEMONSTRATION**, jamais présentées comme une lecture des C1–C7 du boîtier. Les modèles distinguent `CAMERA`, `LOCAL` et `UNKNOWN`. Le rafraîchissement ne fait que redessiner les données locales.
- **Packs** présente quatre exemples de sept recettes. Le nombre de slots est validé. Le bouton « Load Pack to Camera » est désactivé.
- **Backups** sauvegarde uniquement la bibliothèque locale. Il ne télécharge ni ne restaure la configuration de l'appareil.
- **Diagnostics** lit les informations du rapport, les opérations/propriétés annoncées, les réponses PTP et le journal du scan. L'export produit une archive locale ; elle peut contenir le numéro de série déjà présent dans les rapports.
- **Settings** propose le balayage étendu existant. Le mode clair et le garde-fou de lecture seule ne dépendent pas d'une option permettant l'écriture.

Les favoris et recettes sont enregistrés dans `data/library.json`, avec remplacement atomique et sauvegarde précédente. Un fichier illisible est préservé avant de proposer la bibliothèque de démonstration. Les tests utilisent leur propre sous-dossier isolé dans `validation/`.

Sur petit desktop, le panneau droit devient accessible par Camera Slots, les filtres se répartissent sur plusieurs lignes et les contenus défilent verticalement. Les dimensions vérifiées sont 1536 × 1024, 1200 × 780 et 960 × 700.

## Données et assets : limites explicites

Le **modèle, firmware, VID/PID, protocole et date de scan** proviennent du moteur ou du rapport. La connexion vient de l'énumération WPD courante. Les métadonnées d'un ancien rapport sont distinguées d'une connexion courante ; un autre identifiant de périphérique n'hérite pas de ses valeurs.

Le mode USB n'est pas déduit du VID/PID. Il n'est nommé que si la lecture de `0xD16E` réussit avec une valeur reconnue. Sur le scan réel de cette livraison, D16E retourne `DevicePropNotSupported` : l'interface affiche donc **Not reported**, malgré le texte de réglage conseillé dans Quick Help.

Les photos du mockup sont utilisées comme **assets photographiques temporaires**, avec des régions source limitées aux photos. Les cartes, textes, actions, tableaux et navigation sont de vrais contrôles, pas une capture aplatie servant d'interface. Les vignettes fournies ont une résolution limitée, visible lorsqu'on agrandit une fiche ; l'éditeur permet de choisir une photo locale de meilleure résolution.

Les noms de recettes sont ceux demandés pour l'interface. Leurs valeurs sont des **exemples de démonstration**, pas des recettes originales authentifiées ni des réglages lus dans l'appareil. La fiche l'indique explicitement. Classic Negative, Eterna Bleach Bypass, Color Chrome FX Blue, Clarity et Grain Size sont signalés lorsqu'ils sont utilisés ; rien n'est supprimé silencieusement. Cette compatibilité concerne les fonctions, pas la validation exhaustive de toutes les valeurs saisies librement.

## Moteur et sécurité préservés

`Probe.cs` et `ReportViewer.cs` sont **inchangés octet pour octet**, vérifiés contre le manifeste du checkpoint :

| Source conservée | SHA-256 |
| --- | --- |
| `Probe.cs` | `61452B742ACB1FAEB79175E5B4484DD84BF9FE864BD25E19C3AE85517B5221C4` |
| `ReportViewer.cs` | `744758EC8AB0C798C5E2329EBB3DDEF175496499CCA9688FD34D12CA2861E170` |

Le transport reste Windows WPD/MTP. Aucun lancement de rawji/grawji, aucune installation de pilote, aucun Zadig. Aucun accès au firmware.

La whitelist reste **0x1001, 0x1014, 0x1015**. Les tests vérifient notamment le rejet de 0x1016, 0x900C/0x900D et 0x100C/0x100D. Les lectures d'objet 0x1008/0x1009 restent également bloquées : le test de protocole backup proposé dans le document 06 n'a pas été ajouté à cette livraison UI.

Le glisser-déposer est désactivé, les boutons d'écriture sont désactivés et les recettes ne possèdent aucun chemin d'envoi USB. **Aucune écriture caméra n'a été ajoutée ou exécutée.** Les écritures locales de bibliothèque, rapports et captures sont indépendantes de cette restriction.

Une ancienne instance de test gardait `probe-session.log` ouvert pendant la première vérification réelle. Elle a été fermée proprement, puis le scan a été répété et le journal a été vérifié. La nouvelle GUI impose une instance unique pour éviter les scans et ouvertures concurrentes du journal. Ne pas lancer en parallèle une ancienne version ou le CLI pendant un scan.

## Validation et preuves

| Vérification | Résultat |
| --- | --- |
| Compilation GUI et CLI, niveau d'avertissement 4 | Réussie, aucune erreur ni avertissement ; `validation/build-output.txt`. |
| Tests hors connexion | Réussis, `cameraAccess: false` ; `validation/ui-smoke-result.json`. |
| Persistance et filtres | Favori relu après sauvegarde, recherche/filtres, paramètres incompatibles préservés, sauvegarde locale. |
| Navigation et responsive | Sept pages, fiche et éditeur ouverts/rendus ; trois dimensions vérifiées. |
| États et métadonnées | États simulés limités au mode de validation ; D16E non supporté, rapport historique et périphérique différent vérifiés par fixtures isolées. |
| Lancement réel | Fenêtre ouverte et réactive, aucun crash observé. |
| Second lancement | Le second processus termine avec code 0 ; une seule GUI reste ouverte, `validation/single-instance-result.json`. |
| Scan réel final | **Code 0**, démarré le **31/08/2026 à 22:43:46**, terminé vers 22:43:48. |
| Boîtier lu | **FUJIFILM X-T30**, firmware **1.00**, **04CB / 02E3**, `GetDeviceInfo = 0x2001 (OK)`. |
| Rapports | JSON et TXT produits ; archives `rapports/xt30_report_2026-08-31_224348.json` et `.txt`. |
| Journal et crash | `probe-session.log` mis à jour jusqu'au message « Termine » ; aucun `crash.log` créé pendant ces validations. |
| Lecture C1–C7 | Toujours indisponible ; D18C/D18D retournent 0x200A, donc affichage LOCAL uniquement. |

Une absence WPD réelle a été observée avant le lancement réussi avec le X-T30. Les états déconnecté, connecté, communication et erreur ont également été vérifiés hors connexion. **Un cycle physique volontaire de débranchement/rebranchement pendant un même scan n'a pas été réalisé** ; il ne faut pas confondre ce point avec les simulations UI ou avec le scan réel réussi.

Capture principale : [camera-final.png](../xt30-probe/validation/camera-final.png). Son fichier compagnon [camera-final.json](../xt30-probe/validation/camera-final.json) atteste `scanAttempted: true`, `scanExitCode: 0` et `connection: Connected`. Cette capture provient du rendu de la fenêtre réellement lancée, après le scan ; aucun état connecté n'y a été simulé.

Autres captures : `offline-recipes.png`, `offline-camera-slots.png`, `offline-packs.png`, `offline-backups.png`, `offline-diagnostics.png`, `offline-settings.png`, `offline-camera-medium.png`, `offline-camera-small.png`, `offline-recipes-small.png` et `recipe-detail.png`. Les captures préfixées `offline` proviennent volontairement des tests sans matériel.

Pour rejouer les tests sans accéder au boîtier :

```powershell
.\xt30-probe\xt30-probe.exe --ui-smoke
```

Le mode `--ui-capture <chemin.png> --scan` est réservé à la validation **réelle** : il lance le scan existant, produit la capture et ses métadonnées, puis ferme la fenêtre. `--keep-open` la laisse ouverte. Ces options ne contournent pas la whitelist du moteur.
