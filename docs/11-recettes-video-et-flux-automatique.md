# Recettes vidéo et flux automatique C1–C7

Date : 02/09/2026

## 1. Recettes vidéo

### Ce que le boîtier permet réellement

Les banques **C1–C7 du X-T30 sont des réglages photo**. Le fichier de sauvegarde
(objet PTP handle 0, format 0x5000, 5628 octets) que nous décodons ne contient
aucun réglage propre au mode film : les 256 octets par banque que nous avons
cartographiés portent la simulation, la plage dynamique, les tons, la couleur,
la netteté, le grain, le Color Chrome, la balance des blancs et le nom.

Conséquence : **une recette vidéo n'est jamais écrite dans l'appareil**. Elle est
stockée localement et sert de fiche à reporter dans les menus du boîtier.
C'est une limite du matériel, pas un choix de prudence de plus.

### Implémentation

- `Recipe.Kind` vaut `"Photo"` (défaut) ou `"Video"`, persisté sous la clé `kind`
  dans `data/library.json`.
- `Recipe.VideoParameterOrder` remplace `ParameterOrder` pour ces recettes :
  Movie Mode, F-Log, ISO, Film Simulation, Monochromatic Color, White Balance,
  WB Shift R/B, Dynamic Range, Color Chrome Effect, Highlight, Shadow, Color,
  Sharpness, Noise Reduction. `Recipe.Parameters` renvoie la bonne liste.
- Le grain n'y figure pas : il ne fait pas partie des réglages d'image du film.
- La plage dynamique vidéo n'offre pas DR-P : liste réduite à DR100/200/400.
- L'éditeur bascule d'un jeu à l'autre par la liste « This recipe is for » et
  conserve les valeurs déjà saisies pendant la bascule.
- Une recette vidéo est exclue : des packs, des slots C1–C7, du comparateur de
  banque et du sélecteur `BankPlanForm`. Trois assertions du test de fumée le
  vérifient.
- Vignette : badge `VIDEO`, et la ligne du bas affiche le Movie Mode au lieu de
  la plage dynamique.

## 2. Flux automatique C1–C7

Page **Camera Slots**, deux boutons en haut :

### « Read my camera »

`Camera/CameraBanksReader.cs` enchaîne, sans intervention :

1. `Tools/BackupRead/xt30-backup-read.exe --out phase2-inventory`
   (opcodes 0x1008 GetObjectInfo et 0x1009 GetObject, handle 0 uniquement) ;
2. si l'appareil répond « occupé », ferme la FUJIFILM Tether App — qui garde le
   périphérique USB ouvert — puis réessaie **une seule fois** ;
3. `Tools/BackupDecoder/xt30-backup-decoder.exe` sur le fichier obtenu ;
4. `RecipeLibrary.ReloadCameraBanks()` et reconstruction des lignes affichées.

Aucun octet n'est envoyé au boîtier.

### « Load recipes into C1-C7… »

`Presentation/BankPlanForm.cs` : une liste déroulante par banque, alimentée par
la bibliothèque (recettes locales d'abord, puis importées ; recettes vidéo
exclues). Chaque ligne montre en direct le nom que portera la banque, décalage
de balance des blancs compris, avec son compteur de caractères sur 25.

Si aucune banque du boîtier n'a encore été lue, le formulaire propose de la lire
lui-même avant de continuer. Ensuite :

`CameraBankFile.PrepareMany` → un fichier unique dans `generated/` →
`CameraSend.Send` → macro Tether App → redécodage → rafraîchissement de l'écran.

Les banques non sélectionnées, et tout le reste du fichier, restent intacts.

## 3. Ce qui n'a pas changé

- `Probe.cs` et la liste blanche d'opcodes : intouchés.
- `CameraWritePolicy.Available` reste `false` : l'application n'émet aucune
  commande d'écriture PTP. Seul le texte explicatif a été corrigé, il annonçait
  encore que l'écriture était « indisponible » alors que la voie officielle
  Tether App fonctionne.
- Les valeurs affichées comme venant de l'appareil viennent toujours d'une
  lecture réelle ; les champs absents restent `Not specified`.
