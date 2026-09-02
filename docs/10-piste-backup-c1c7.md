# Piste backup — lire les vrais C1–C7 du X-T30 (2026-09-02)

Objectif : accéder aux banques personnalisées C1–C7 réelles du boîtier, en **lecture seule**,
par le fichier de réglages que Fujifilm expose en mode « SAUVEG. RESTAUR. » — puisque les
propriétés PTP de recettes (0xD18C–0xD1A5) ne répondent pas.

## 1. Découverte majeure : tous nos scans étaient en mode LECTEUR DE CARTE

L'inventaire d'objets du 02/09/2026 (lecture seule, opcodes 0x1004/0x1005/0x1007/0x1008)
le prouve sans ambiguïté :

| Élément lu | Valeur |
|---|---|
| Stockages | 1 — `0x10000001`, « External Memory », 31,1 Go dont 21,5 Go libres |
| Handles d'objets | **185** : `DCIM` → `982_FUJI` → 90 JPEG, 88 RAF, 5 MOV |
| Exemple | `DSCF2266.JPG`, 6240 × 4160 (le capteur 26 Mpx du X-T30) |
| **Handle spécial `0`** | **`0x2009 InvalidObjectHandle` — le blob de réglages n'existe pas dans ce mode** |

C'est la signature exacte de la personnalité **lecteur de carte / MTP** : l'appareil expose la
carte SD comme un disque, pas ses réglages internes.

**Conséquence rétroactive, essentielle :** les scans précédents (qui ont conclu que
0xD16E, 0xD185, 0xD18C–0xD1A5 et 0xD34C étaient tous `DevicePropNotSupported`) ont été faits
dans ce même mode lecteur de carte. Ils ne prouvent donc **pas** que le X-T30 n'expose pas ces
propriétés : ils prouvent que le mode lecteur de carte ne les expose pas — ce qui est attendu
et documenté par libfuji (`fujiusb_setup()` : 0xD16E illisible ⇒ mode card reader ⇒ aucune
fonction Fuji disponible). Le verdict « le X-T30 ne sait pas » reste **non démontré**.

## 2. Prochaine étape — aucune nouvelle commande, un simple réglage de menu

Sur le boîtier :

```
MENU → SET UP (clé) → PARAMÈTRE CONNEXION → MODE CONNEXION USB
     → CONV. RAW USB / SAUVEG. RESTAUR.
```

puis éteindre / rallumer l'appareil branché. On relance ensuite **les deux outils existants**,
sans recompilation et sans ajouter un seul opcode :

1. `xt30-probe-cli.exe` — les propriétés 0xD16E (mode USB), 0xD183–0xD187 et 0xD18C–0xD1A5
   répondent-elles dans ce mode ?
2. `xt30-object-inventory.exe` — `GetObjectInfo(0)` renvoie-t-il enfin un objet, avec le
   format `0x5000` et une taille de l'ordre de 33 404 octets ?

Ce sont deux lectures pures. Le mode USB est changé **par l'utilisateur dans le menu**, jamais
par une commande logicielle (`SetUSBMode` 0xD15D reste interdit).

## 3. Le décodeur est déjà prêt (analyse de fichier, zéro PTP)

`xt30-probe/Tools/BackupDecoder/` → `xt30-backup-decoder.exe`. C'est un **analyseur de
fichier** : il ne contient aucune commande appareil, aucune fonction d'écriture, et ne modifie
jamais le tableau d'octets qu'on lui donne.

Il décode le layout `gen4-early` (X-T3 / X-T30, X-Processor 4) rétro-ingéniéré par grawji :

| Élément | Valeur |
|---|---|
| Taille attendue du blob | 33 404 octets |
| Signature / modèle / série | ASCII `FUJIFILM` en tête, modèle à `0x14`, série à `0x34` |
| Banques | 7, première à l'offset **31 658**, pas de **256** |
| Nom de banque | +67 relatif, ASCII, 16 octets max |
| Réglages (offsets relatifs) | film sim +0, WB mode −34, Kelvin −33, NR −8, DR +4, Color +9, Sharpness +11, Highlight +12, Shadow +13, Color Chrome +14, Grain +15 |

Encodages inverses implémentés : ton = `4 − octet`, NR = `octet − 4`, Color par table
(+4→3, +3→4, +2→5, +1→6, 0→0, −1→8, −2→7, −3→9, −4→10), Grain (0 Strong / 1 Weak / 2 Off),
Color Chrome (0/1/2), DR (1/2/3 → DR100/200/400), WB (0–8), Kelvin = index dans la liste
**décroissante** depuis 10000 K (repère matériel grawji : index 10 → 5000 K, vérifié).

Garde-fous du décodeur : refus si la signature `FUJIFILM` manque, si le modèle n'est pas
cartographié (aucun offset deviné), ou si la taille ne correspond pas au layout.
**Auto-test hors ligne : 24/24**, dont l'immutabilité du blob fourni.

> ⚠️ Ces offsets sont vérifiés sur **X-T3** et **X100F** par grawji, **pas encore sur X-T30**.
> Le layout `XT30` y est mappé par déduction (même processeur que le X-T3). La première
> lecture réelle servira précisément à valider ou infirmer cette déduction — et le garde
> « taille attendue » empêche toute interprétation hasardeuse.

## 4. Deux voies pour obtenir le blob

**Voie A — X Acquire (risque nul, aucune commande de notre part).**
Le logiciel officiel gratuit de Fujifilm sauvegarde les réglages du boîtier dans un fichier
`.dat` sur le PC. Notre décodeur lit ce fichier. Aucun opcode ajouté, aucun code à nous qui
parle à l'appareil : c'est Fujifilm qui fait le transfert, avec son propre logiciel.
C'est la façon la plus sûre de valider le layout sur ton X-T30.

**Voie B — lecture directe par l'application (nécessite une autorisation).**
`GetObject (0x1009)` sur le handle `0`. C'est une lecture pure (appareil → PC, aucune donnée
envoyée au boîtier), mais **c'est un opcode absent de la whitelist**, donc une modification de
`Probe.cs`. Conformément à la règle, rien ne sera ajouté ni exécuté sans accord explicite, et
la demande sera présentée avec le paquet exact. À noter : grawji observe que `GetObject` et
`SendObject` dans une même session sont refusés (`0x200F`) — chaque phase ouvre sa propre
connexion ; côté lecture seule, cela ne nous concerne pas.

## 5. Ce que la lecture apporterait déjà, sans jamais écrire

Si le blob est lisible et le layout validé, l'application peut afficher les **vraies** recettes
C1–C7 du boîtier (nom + réglages), marquées `CAMERA` au lieu de `LOCAL` — c'est-à-dire la
fonctionnalité « voir ce qu'il y a dans mon appareil », entièrement en lecture seule.

L'écriture, elle, resterait un chantier distinct et bien plus engageant : le mécanisme de
restauration réécrit **l'intégralité des réglages du boîtier** (pas un slot isolé), avec
recalcul obligatoire d'une somme de contrôle additive u16 à l'offset 176 (sinon rejet `0x200F`)
et des champs volatils (176, 248, 380, 408, 3276) que l'appareil régénère. Rien de tout cela
ne sera abordé sans une décision explicite et une sauvegarde préalable.

## 6. Résultats en mode CONV. RAW USB / SAUVEG. RESTAUR. (02/09/2026, 14h58)

Le mode a été changé par l'utilisateur dans le menu du boîtier. Windows garde le même
PID `0x02E3` et le même pilote `WUDFWpdMtp` : le passthrough WPD continue de fonctionner.

### L'objet de sauvegarde existe — confirmé

```
GetObjectInfo(0x00000000) -> 0x2001 (OK), 56 octets
    objectFormat         = 0x5000      <- format « fichier de reglages » attendu
    objectCompressedSize = 5628 octets
    storageID = 0, parentObject = 0, filename vide
```

En mode lecteur de carte, ce même appel renvoyait `0x2009 InvalidObjectHandle`. Le handle `0`
est donc bien un objet virtuel Fuji qui n'apparaît que dans ce mode — exactement ce que
décrivent libfuji (`fujiusb_download_backup()` limité au transport RAW CONV) et grawji.

### La taille invalide le layout supposé pour le X-T30

| Boîtier | Taille du blob | Source |
|---|---|---|
| X100F, X-Pro2, X-T2, X-T20, X-E3 (gen3) | 5 660 octets | grawji, vérifié matériel |
| X-T3 (gen4-early) | 33 404 octets | grawji, vérifié matériel |
| **X-T30 (ce boîtier, firmware 1.00)** | **5 628 octets** | **mesuré ici, inédit** |

grawji associait `XT30` au layout gen4-early de 33 404 octets **par déduction** (même
processeur que le X-T3). C'est faux : notre X-T30 produit un blob de 5 628 octets, plus proche
de la famille gen3 (5 660) à 32 octets près. **Le layout du X-T30 est donc inconnu et devra
être établi sur données réelles.** Le garde-fou « taille attendue » du décodeur refuse
justement de décoder ce blob avec un layout qui ne lui correspond pas — il n'inventera rien.

C'est un point de données que, à notre connaissance, aucun projet public ne possède.

### Le reste de l'état PTP dans ce mode

Le `GetDeviceInfo` passe de 4 à **202 propriétés annoncées** et ajoute des opérations
(`0x1009 GetObject`, `0x100E`, `0x1017`, `0x1018`, `0x101B GetPartialObject`, `0x101C`).
En revanche, seules **7 propriétés répondent** réellement à `GetDevicePropDesc` :

| Code | Nom | Type | Valeur |
|---|---|---|---|
| 0xD183 | StartRawConversion | UINT16 | 65535 |
| 0xD208 | (inconnu) | UINT16 | 772 |
| 0xD20B | DeviceName | STRING | (vide) |
| 0xD212 | CurrentState / EventsList | — | — |
| 0xD21C | (expérimental, vu dans libfuji) | UINT16 | 3 |
| 0xD406 / 0xD407 | propriétés MTP standard | STRING / UINT32 | — |

`0xD18C`, `0xD18D` et tout le bloc `0xD190–0xD1A5` restent `DevicePropNotSupported` **même dans
le bon mode** : le chemin « propriétés de recettes » est donc bien absent de ce boîtier, ce qui
est cohérent avec la liste de compatibilité de l'app Fuji X Weekly. En revanche `0xD185`
(RawConvProfile) n'est pas exposé non plus **avant l'envoi d'un RAF** — comportement documenté
par rawji, et que nous ne testerons pas puisque l'envoi d'un objet est une écriture.

La voie des recettes passe donc **exclusivement par le blob de sauvegarde**.

### Voie A (X Acquire) : écartée

La table de compatibilité officielle de X Acquire 1.29 ne liste **pas** le X-T30 première
génération (seulement le X-T30 II). De plus ce logiciel expose « RESTORE CAMERA SETTINGS »
à côté de « BACKUP », c'est-à-dire une fonction d'écriture, sur un boîtier qu'il ne prend pas
officiellement en charge. Installer un outil capable d'écrire, non supporté pour ce modèle,
serait plus risqué qu'une unique commande de lecture émise par notre propre code — lequel est
structurellement incapable d'écrire. Voie non retenue, sauf demande contraire.

## 7. Lecture réelle du fichier et cartographie du X-T30 (02/09/2026, 15h12)

`GetObject (0x1009)` a été autorisé explicitement par l'utilisateur et ajouté à la whitelist
de `Probe.cs` (une ligne, commentée). Il n'est utilisé que par
`Tools/BackupRead/xt30-backup-read.exe`, qui le restreint au handle `0` et refuse de
télécharger si le format n'est pas `0x5000`.

```
GetObjectInfo(0) -> 0x2001 OK ; format 0x5000 ; 5628 octets
GetObject(0)     -> 0x2001 OK ; 5628 octets recus ; 0 octet envoye au boitier
```

En-tête du fichier : `FUJIFILM` `X-BACKUP` `0100` `X-T30`, numéro de série à `0x34`.
La convention d'en-tête de grawji (magie en 0, modèle à `0x14`, série à `0x34`) est donc
**confirmée sur le X-T30**.

### Layout X-T30 établi (inédit)

Les sept noms de banques apparaissent en ASCII clair, espacés de **256 octets** exactement
(`0x0F78`, `0x1078`, … `0x1578`). En recalant l'ancre sur l'octet « film simulation »
(constant à 13 = Classic Chrome dans les sept banques), **tous les décalages relatifs de
champs se révèlent identiques au layout gen4-early du X-T3**.

| Paramètre | Valeur |
|---|---|
| Taille du blob | **5 628** octets |
| Banques | 7, première ancre `sim0` = **3882** (`0x0F2A`), pas de **256** |
| Nom de banque | **+78** relatif, ASCII (jusqu'à 25 caractères observés) |
| Champs relatifs | wb_mode −34, wb_kelvin −33, nr −8, dr +4, color +9, sharpness +11, highlight +12, shadow +13, color_chrome +14, grain +15 |

Autrement dit : **la structure d'enregistrement de grawji est correcte pour le X-T30 ; seules
la taille du fichier et l'adresse absolue des banques diffèrent du X-T3.** Le mapping
`XT30 → gen4-early` de grawji aurait produit des valeurs fausses (mauvaise taille, mauvaise
ancre) ; il est remplacé par un layout `xt30-gen1` propre dans notre décodeur.

### Validations croisées du décodage

- **C7 « 800T » → 3200 K.** La CineStill 800T est une pellicule tungstène : 3200 K est
  précisément la valeur attendue. Preuve forte que l'octet Kelvin et sa table décroissante
  sont correctement décodés.
- **C6 « PACIFIC » → 5900 K**, et les deux seules banques en mode « température » sont
  justement celles dont le nom suggère une température fixe.
- **C3 « KODAK64 » → balance des blancs Ensoleillé**, cohérent avec une recette diurne.
- Les sept banques sont en **Classic Chrome**, ce qui colle avec des recettes nommées
  Portra / Kodak Gold / Kodachrome / Pacific.
- Les noms encodent le décalage WB (`R+1 B-3`…) : l'utilisateur contourne ainsi la limite
  connue du X-T30, qui ne mémorise pas le WB Shift par banque.

### Levée des ambiguïtés contre le menu du boîtier (02/09/2026)

Les banques C1 et C2 ont été comparées ligne à ligne avec l'écran de l'appareil.
**Tous les champs correspondent**, et les trois incertitudes restantes sont tranchées :

| Question | Réponse du boîtier | Conclusion |
|---|---|---|
| `0x00` en plage dynamique | C1 affiche **DR-P** | 0 = la priorité de plage dynamique pilote la plage dynamique (elle neutralise aussi les tons lumière/ombre à l'écran, alors que le fichier en conserve les valeurs) |
| `+4` ou `+8` pour la plage dynamique | C2 affiche **DR100**, or `+4` vaut 1 et `+8` vaut 3 | **`+4` = plage dynamique** (1 = DR100). L'autre hypothèse aurait donné DR400 : écartée |
| Rôle de `+8` | C1 = **AUTO**, C2 = **OFF** | **`+8` = priorité de plage dynamique**, champ absent des layouts de grawji. Codes confirmés : 0 = AUTO, 3 = Off ; 1 et 2 jamais observés, donc signalés « non confirmé » |
| `+14` / `+15` interchangeables ? | C2 : grain **faible**, chrome **fort** ; or `+14`=2 et `+15`=1 | Ordre initial correct : **`+14` = Color Chrome**, **`+15` = Grain** |

Le layout X-T30 est désormais **entièrement établi et vérifié sur matériel**, sans supposition
restante hormis deux codes de priorité DR jamais rencontrés.

### Valeurs restées non interprétées

- **C4, White Balance = `0x09`** : hors table (0–8). Probablement une balance personnalisée
  (grawji note que Custom 1–3 n'ont jamais été mesurés). Affiché tel quel.
- La température n'est affichée que si le mode WB vaut 8 ; sinon `not applicable`, pour ne pas
  présenter l'octet nul comme « 10000 K ».
- Priorité de plage dynamique : seuls les codes 0 (AUTO) et 3 (Off) ont été observés.

## 8. Descripteur ≠ valeur : une leçon de méthode (02/09/2026, 15h26)

Un scan ultérieur a révélé que **le X-T30 refuse de décrire certaines propriétés tout en
acceptant d'en donner la valeur**. `GetDevicePropDesc` et `GetDevicePropValue` ne répondent
donc pas la même chose, et conclure à partir du seul descripteur est une erreur de méthode —
c'est d'ailleurs pour cela que rawji n'utilise **jamais** `GetDevicePropDesc`.

| Propriété | `GetDevicePropDesc` | `GetDevicePropValue` | Lecture |
|---|---|---|---|
| 0xD16E USBMode | 0x200A | **0x2001, valeur `06`** | **RAW CONV confirmé par l'appareil lui-même** |
| 0xD184 IOPCode | 0x200A | **0x2001, `FF159502,FA159502`** | Confirme la valeur que rawji codait en dur pour le X-T30 |
| 0xD186 / 0xD187 | 0x200A | **0x2001, `X-T30_0100`** | Code de compatibilité, firmware 1.00 |
| 0xD185 RawConvProfile | 0x200A | **0x2002 GeneralError** | Existe, mais exige un RAF envoyé au préalable (comportement rawji) |
| 0xD18C, 0xD18D, 0xD190–0xD1A1, 0xD34C | 0x200A | **0x200A** | Réellement absentes, par les deux méthodes |

Conséquences :

1. **Le verdict sur les propriétés de recettes est maintenant rigoureux** : elles sont absentes
   du X-T30, vérifié dans le bon mode USB et par les deux commandes. Le blob reste la seule voie.
2. **Le mode USB est désormais lu, pas déduit** : l'interface affiche « RAW CONV./BACKUP
   RESTORE » parce que l'appareil renvoie la valeur 6, conformément à la règle « jamais de
   valeur inventée ».
3. **La conversion RAW est vivante sur ce boîtier** (D183 décrit, D184/D186/D187 lus). Elle
   n'est pas exploitable ici sans envoyer un RAF, ce qui serait une écriture — hors périmètre.

## 8bis. Somme de contrôle résolue par expérience contrôlée (02/09/2026)

Expérience **sans aucune écriture depuis le PC** : l'utilisateur a modifié **un seul réglage
au menu du boîtier** (netteté de C7, −1 → 0), puis le fichier a été relu. Diff des deux
versions : **6 octets modifiés**.

| Offset | Avant → Après | Interprétation |
|---|---|---|
| **5429** | 5 → 4 | **Banque C7, rel +11 = netteté.** `4−5 = −1` puis `4−4 = 0` : la cartographie ET l'encodage des tons sont confirmés par une modification contrôlée |
| **176 / 177** | 0x72E7 → 0x71EA | Les deux octets bougent ensemble : **somme de contrôle u16 little-endian** (même offset que celui trouvé par grawji sur X-T3) |
| 1149, 1151 | 250→252, 254→0 | Compteurs maintenus par l'appareil, **inclus** dans la somme |
| 3772 | 33 → 100 | Donnée d'objectif (juste avant `LX230A`), **exclue** de la somme |

Résolution : le delta de la somme stockée vaut **−253**, et la somme des deltas de données
vaut exactement **−254 + 2 − 1 = −253** en incluant 1149, 1151 et 5429 et en excluant 3772.
Si 3772 était inclus, le delta serait −186 : l'exclusion est donc démontrée, pas supposée.

**Formule (reproduit exactement les deux fichiers) :**

```
somme_controle = ( Σ octets[0..fin] , en excluant 176, 177 et 3772 , + 0xE1E5 ) & 0xFFFF
stockée en u16 little-endian à l'offset 176
```

Le biais `0xE1E5` est identique pour les deux fichiers (vérifié aussi avec des points de
départ 0xA8, 0xB2 et 0x100, qui donnent chacun un biais différent mais également constant :
le choix du départ est une convention, seul le couple départ/biais compte).

Le décodeur **vérifie** désormais cette somme à chaque lecture (`COHERENTE` sur les deux
fichiers réels) et l'auto-test couvre les trois propriétés attendues : reconnaissance après
inscription, invalidation par une valeur modifiée, insensibilité à l'octet exclu.

> ⚠️ Limite honnête : deux échantillons ne permettent pas d'exclure qu'une **plage** autour de
> 3772 soit exclue plutôt que ce seul octet, ni qu'un octet avant 0xA8 se comporte
> différemment. Pour écrire, il faudra d'autres échantillons de contrôle.

**Conséquence : le dernier verrou technique de l'écriture est levé.** Il ne reste que la
décision de sécurité, car renvoyer le fichier exige `SendObjectInfo (0x100C)` et
`SendObject (0x100D)`, tous deux explicitement interdits par l'utilisateur, et réécrit
**l'intégralité des réglages du boîtier**, pas une banque isolée.

## 9. Intégration dans l'application

`Models/CameraBanks.cs` charge le rapport du décodeur ; il ne parle jamais à l'appareil. Si le
fichier est absent ou non décodable, l'application retombe sur ses données `LOCAL` de
démonstration et ne prétend jamais qu'elles viennent du boîtier.

- La page **Camera Slots** affiche les sept banques réelles, en-tête `READ FROM CAMERA · X-T30`
  avec la date de lecture, provenance `CAMERA` par ligne, plage dynamique affichée.
- **Reconnaissance des noms** : le nom lu dans le boîtier est rapproché de la bibliothèque en
  ignorant casse et ponctuation (`PORTRA400` reconnaît « PORTRA 400 »). La correspondance est
  purement illustrative — elle fournit une vignette et une mention, jamais une valeur.
- La fiche d'une banque indique `SOURCE: CAMERA · READ FROM YOUR X-T30` et précise que les
  réglages absents du fichier (ISO, WB Shift, exposition) restent « Not specified ».
- Les **packs restent LOCAL** et ne reprennent jamais les banques lues, pour ne pas mélanger
  les provenances. Le bouton d'écriture reste désactivé partout.

Validation hors connexion : **9/9**, dont « Seven banks read from the camera settings file,
provenance CAMERA (X-T30) » et le contrôle que toute écriture reste refusée.

## 9bis. Comment l'application applique réellement une recette

**Découverte décisive (02/09/2026)** : la table de compatibilité officielle de la
**FUJIFILM Tether App** liste le X-T30 première génération avec la mention
*« Only the "BACKUP RESTORE" function is available »*. Le logiciel du constructeur sait donc
écrire les réglages dans ce boîtier précis, officiellement et sans bricolage.

L'architecture retenue en découle, et elle supprime tout risque de notre côté :

```
notre application  →  fichier .dat modifié sur le disque  →  Tether App Fujifilm  →  boîtier
```

Nous n'envoyons jamais un octet à l'appareil. C'est le logiciel Fujifilm, conçu et testé pour
ce modèle, qui effectue l'écriture. Les opcodes `SendObjectInfo`/`SendObject` restent inutiles
et interdits, et `Probe.cs` demeure strictement en lecture seule.

`Models/CameraBankFile.cs` produit ce fichier. C'est de la **pure manipulation de fichier**,
sans aucune connexion USB. Ses règles :

- refuse un fichier qui n'est pas un backup `FUJIFILM` de 5 628 octets, d'un modèle autre que
  `X-T30`, ou dont la somme de contrôle est déjà incohérente ;
- n'écrit **que** les octets dont la valeur possède un code vérifié — une valeur inconnue ou
  une fonction absente du X-T30 n'écrit **rien** et est signalée ;
- n'écrit la plage dynamique que si la priorité DR est explicitement `Off`, puisque sinon
  c'est la priorité qui la pilote ;
- signale les réglages que ce fichier ne stocke pas (ISO, WB Shift, Monochromatic Color) au
  lieu de laisser croire qu'ils sont transférés ;
- recalcule la somme de contrôle en dernier et vérifie qu'elle se revalide.

### Vérification par test automatisé (20 contrôles, tous passants)

Test sur le **fichier réel** du boîtier, banque C1 remplacée par une recette de test nommée
« CLAUDE TEST » :

| Contrôle | Résultat |
|---|---|
| Taille inchangée, somme recalculée cohérente | OK |
| **Aucun octet modifié hors de la banque C1 et du champ de contrôle** | OK — 0 en trop |
| **Banques C2 à C7 strictement intactes** | OK |
| Encodage relu : ACROS=14, Highlight +2→2, Shadow −3→7, Sharpness +4→0, Color −2→7, NR +1→5, Grain Strong→0, Chrome Off→0, DR-P Off→3, DR200→2, WB temp→8, 5600K→index 8 | OK |
| Nom de banque relu | « CLAUDE TEST » |
| Une simulation absente du X-T30 (Classic Negative) | **n'écrit aucun octet** |

### Dans l'interface

Fiche d'une recette → **Compare with camera** → choix de la banque → tableau des écarts
(`already correct` / `CHANGE` / `set by hand` / `not on the X-T30 — skip`) → bouton
**« Create a camera settings file… »**. Le message final rappelle la marche à suivre dans la
Tether App et insiste sur la conservation du fichier d'origine, qui restaure l'état exact.

## 9ter. CHAÎNE COMPLÈTE VALIDÉE SUR MATÉRIEL (02/09/2026, ~16h50)

**Le X-T30 affiche « CLAUDE TEST » en C1.** L'aller-retour complet fonctionne :

```
lecture PTP (GetObject handle 0)  →  décodage layout X-T30  →  modification + somme
      →  fichier .dat  →  Tether App « Restauration »  →  boîtier mis à jour
```

### Validation croisée contre le logiciel Fujifilm

La fonction « Sauvegarde des paramètres de l'appareil » de la Tether App a été déclenchée,
produisant `FUJIFILM_X-T30_2026 9 2_1644.dat`. Comparaison avec notre lecture `GetObject` :

| Contrôle | Résultat |
|---|---|
| Taille | **5 628 octets des deux côtés** |
| Différence | **2 octets seulement** : offset 248 (compteur, listé volatil par grawji) et 176/177 (la somme qui en découle) |
| Notre formule de somme appliquée au fichier **de Fujifilm** | stockée `0x71E9` = calculée `0x71E9` — **correspond** |

Autrement dit : notre lecture est fidèle au bit près, et notre rétro-ingénierie de la somme de
contrôle est confirmée contre l'implémentation du constructeur.

### Le test appliqué

Fichier généré **à partir de la sauvegarde Fujifilm** (base la plus sûre) : nom de C1 remplacé
par « CLAUDE TEST », **13 octets modifiés, tous dans le champ du nom et la somme, zéro
ailleurs**, somme revalidée. Restauré via la Tether App. **Le boîtier affiche le nouveau nom.**

### Répartition des rôles retenue

- **Notre application** : lit, décode, compare, et produit un `.dat` valide. Elle n'envoie
  jamais un octet à l'appareil ; `Probe.cs` reste en lecture seule.
- **La Tether App Fujifilm** : effectue l'écriture, via une fonction officiellement supportée
  pour ce modèle. Trois clics : Appareil photo → Restauration des paramètres → choisir le fichier.
- **Retour arrière** : restaurer la sauvegarde Fujifilm d'origine remet l'appareil à l'identique.

## 9quater. Macro d'automatisation de la restauration

`xt30-probe/Tools/RestoreMacro/Restore-CameraSettings.ps1` automatise les trois gestes de la
Tether App. **Elle n'envoie aucune commande USB** : elle pilote le logiciel de Fujifilm, qui
reste seul à écrire dans le boîtier.

### Pourquoi le clavier plutôt que des clics au pixel

Trois pistes ont été évaluées sur l'application réelle :

| Piste | Verdict |
|---|---|
| Messages Win32 sur le menu (`GetMenu` + `WM_COMMAND`) | **Impossible** — `GetMenu` renvoie 0 : c'est un `MenuStrip` WinForms dessiné, pas un menu Win32 |
| UI Automation | **Impossible** — les 30 éléments exposés sont des `Pane` anonymes ; aucune entrée de menu accessible |
| Clavier `Alt` → `→` → `↓` → `Fin` | **Retenu** — vérifié par capture : surligne exactement « Restauration des paramètres de l'appareil », indépendamment de la position de la fenêtre et de la résolution |

`Fin` sélectionne la **dernière** entrée du menu, ce qui évite de compter les entrées grisées
(les items désactivés sont sautés par la navigation clavier).

### Garde-fous

- **Validation du fichier avant tout** : taille 5 628, signature `FUJIFILM`, modèle `X-T30`,
  et **somme de contrôle recalculée** — un fichier incohérent est refusé sans rien tenter.
- Vérification que l'appareil est présent (VID 04CB).
- **Contrôle du focus avant chaque frappe** : si une autre application prend le focus, le script
  **abandonne** au lieu d'envoyer des touches — notamment « Entrée » — à une fenêtre inconnue.
  Ce défaut s'est produit au premier essai (une fenêtre de navigateur a volé le focus) et c'est
  ce garde-fou qui l'a corrigé.
- Mode `-Preview` : navigue jusqu'à l'entrée de menu, enregistre une capture et **s'arrête sans
  rien activer**. À utiliser après chaque mise à jour de la Tether App pour vérifier que la
  dernière entrée du menu est toujours la restauration.
- Le champ « nom de fichier » est renseigné par `WM_SETTEXT`, pas par frappe de caractères.

### Pistes écartées

- **Embarquer la fenêtre de la Tether App dans la nôtre** (`SetParent`) : ne supprime aucun
  clic, casse à chaque mise à jour, et modifier la présentation d'un logiciel tiers est
  juridiquement douteux. Sans intérêt.
- **Reprendre le code de la Tether App** : logiciel propriétaire — contrefaçon. Exclu.
- **Écrire nous-mêmes (`SendObjectInfo`/`SendObject`)** : reste la solution idéale à terme, mais
  le boîtier n'annonce pas ces opcodes. Il faudrait capturer le trafic USB pendant une
  restauration réelle (méthode employée par grawji avec X RAW Studio) pour connaître les
  opcodes exacts. C'est de l'interopérabilité par observation, légitime — mais non nécessaire
  tant que la macro suffit.

## 10. État

- [x] Décodeur de blob construit et auto-testé, aucun accès appareil.
- [x] Inventaire en mode lecteur de carte : handle 0 absent, 185 objets SD.
- [x] Inventaire en mode RAW CONV : handle 0 présent, format 0x5000, 5 628 octets.
- [x] **Fichier de réglages lu (lecture seule) et layout X-T30 établi.**
- [x] **Les 7 vraies banques C1–C7 sont décodées, noms et réglages.**
- [ ] Confirmer les 3 points ci-dessus contre le menu du boîtier.
- [ ] Afficher ces banques dans l'application, marquées `CAMERA`.
- [ ] L'écriture reste un chantier distinct, non engagé.
