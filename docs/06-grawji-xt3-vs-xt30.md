# grawji : protocole X100F/X-T3, applicabilité au X-T30 et comparaison WPD

Analyse statique du 31 août 2026. **Aucun accès à l'appareil, lancement de grawji/rawji, changement de pilote, envoi de RAF ou écriture de réglages effectué. Le probe et ses exécutables sont inchangés.**

**Résultat principal : grawji implémente bien un chemin spécifique aux anciennes banques, par téléchargement puis restauration de la sauvegarde complète des réglages, objet PTP de handle `0`. Ce chemin n'utilise ni `0xD18C`, ni `0xD18D`, ni `0xD185`.** Le X-T30 figure explicitement dans la table de formats de sauvegarde, associé au X-T3. Les commentaires attribuent cependant la validation matérielle au X-T3, pas au X-T30.

**La différence USB direct / WPD est réelle, mais le code ne démontre pas qu'elle cause `DevicePropNotSupported` sur `0xD185`.** Une autre différence est établie : rawji charge un RAF avant de lire `0xD185`, contrairement au probe. Et cette propriété de conversion RAW n'est pas l'interface utilisée pour les anciennes banques.

## Périmètre et sources figées

Nouveaux clones réalisés sans installation ni exécution du code :

- [grawji](https://github.com/p5k369/grawji), commit `d6e4b7456014f070f0db9f9255a9f2732c7b8e58` du 31 août 2026.
- [rawji](https://github.com/pinpox/rawji), placé en HEAD détachée au commit **`5549fdb93028549c2bab8abe963f1a91c50b5368`**. C'est la dépendance exacte de grawji dans [pyproject.toml][pin], pas nécessairement le dernier rawji. L'ancien clone local rawji est au commit `2219c46e594ff6e3794081995da967f34ca0ae47` : il n'a pas servi de référence pour cette dépendance.
- Comparaison avec [Probe.cs](../xt30-probe/Probe.cs) et deux rapports locaux de validation, volontairement exclus du dépôt public.

Recherche `rg` insensible à la casse dans tout le checkout grawji, fichiers cachés inclus hors `.git`, avec les termes demandés : `custom bank`, `custom_bank`, `C1`, `C7`, `X-T3`, `X100F`, `XProcessor`, `D18C`, `D18D`, `D185`, `DeviceProp`, `PTP`, `set_device`, `get_device`, `rawji`. Recherche ensuite des définitions, appelants, constantes et tests dans les deux dépôts. La recherche globale finale a parcouru 116 fichiers texte et trouvé 747 lignes correspondantes. Les noms réellement utilisés sont notamment `send_command`, `send_data_command`, `read_backup` et `transfer_recipes`, pas seulement `get_device`/`set_device`.

Le [commit d'introduction des banques, 24 juillet][banks-commit] contient le backend backup ; le [commit du 28 juillet][presets-commit] ajoute notamment le chemin par propriétés. Les liens source ci-dessous pointent sur les commits audités.

| Fonction recherchée | Implémentation à examiner |
|---|---|
| Lecture des anciennes banques | [camera_backup.py, `setup` / `read_backup`, lignes 142–156][backup-read] : téléchargement de tout le blob |
| Lecture des noms C1–C7 | [camera_backup.py, `read_bank_names`, ligne 215][backup-names] puis [backup_recipe.py, `read_names`, ligne 381][bank-names] |
| Écriture des anciennes banques | [camera_backup.py, `transfer_recipes`, ligne 236][transfer] ; `restore_backup`, ligne 159 |
| Choix du format et sélection de banque | [backup_recipe.py, `LAYOUTS`, ligne 219][layouts] ; `_slot_base`, ligne 372 |
| Mapping binaire X100F/X-T3 | [backup_recipe.py, tables et `_encode`, lignes 105–310][bank-mapping] |
| Banques par propriétés, chemin récent | [camera_presets.py][presets] et [preset_recipe.py][preset-mapping] |
| Transport commun | [rawji/fuji_usb.py, `FujiCamera`, ligne 97][usb] |
| Adaptateur grawji → rawji | [core.py, `CameraSession`, notamment lignes 512–599][core-session] |

## 1. Protocole réellement implémenté pour X-T3 / X100F

Le chemin ancien est **PTP sur USB, avec détournement Fuji de l'objet `0` comme sauvegarde des réglages du boîtier**. Le téléchargement emploie les opérations standard `GetObjectInfo (0x1008)` et `GetObject (0x1009)`. La restauration emploie les opérations standard `SendObjectInfo (0x100C)` et `SendObject (0x100D)`. Le contenu et le handle spécial constituent la convention Fuji ; aucun opcode vendor n'est nécessaire dans ce chemin. Le mode demandé est `USB RAW CONV./BACKUP RESTORE`. [Source : backend backup][backup-read].

Le choix effectif de protocole est le suivant :

1. `setup()` lit `GetDeviceInfo`, puis `GetDevicePropValue(0xD16E)`.
2. Si la liste des propriétés annoncées contient `0xD18C`, grawji utilise `camera_presets`.
3. Sinon, grawji télécharge l'objet `0`, lit le modèle dans son en-tête et choisit un `BankLayout`.

**Le branchement ne repose pas sur une lecture de `0xD184`, ni sur le test `is_xprocessor5()`, ni sur le PID.** `supports_presets()` teste seulement la présence de `0xD18C`. Le nom « gen5 » décrit ce backend ; ce n'est pas une règle générale selon laquelle tous les processeurs 4 seraient incapables d'exposer ces propriétés. Si `0xD18C` est annoncé mais que son utilisation échoue, il n'y a pas de reprise automatique par le blob. [Sources : dispatch][transfer], [test de présence][presets].

Pour le blob, `model_from_blob()` vérifie le préfixe ASCII `FUJIFILM`, puis lit le modèle entre `0x14` et `0x34`, jusqu'au premier NUL. La normalisation retire `FUJIFILM`, ponctuation et espaces, et passe en majuscules. La table associe :

- `X100F`, `XPRO2`, `XT2`, `XT20`, `XE3` → `_GEN3` ; format déclaré vérifié sur X100F.
- **`XT3` et `XT30` → `_GEN4_EARLY`** ; format déclaré vérifié sur X-T3.
- Modèle inconnu → pas de format et refus de restauration. `XT30II` n'est pas dans cette table.

Le téléchargement récupère bien les octets contenant toutes les banques. En revanche, le backend ancien **ne fournit pas de décodeur complet banque → objet `Recipe`** : il extrait les noms et possède un encodeur pour modifier les champs connus. `recipe_from_profile()` dans `core.py` décode le profil du RAF, pas les sept banques. [Sources : formats][layouts], [noms][bank-names], [profil RAW][core-profile].

## 2. Propriétés, objets et mapping des paramètres

### 2.1 Trois interfaces à ne pas confondre

| Interface | Propriétés / identifiants | Utilisation réelle |
|---|---|---|
| Banques X100F/X-T3, et format attribué au X-T30 | Objet **handle `0`** ; `0xD16E` lu en préambule | Lecture/restauration du blob complet ; aucune propriété de sélection C1–C7 |
| Banques récentes par propriétés | `0xD18C`, `0xD18D`, `0xD18E` à `0xD1A5` | Sélection, nom et paramètres du slot |
| Conversion RAW | `0xD185` profil ; `0xD183` déclenchement ; upload RAF `0x900C`/`0x900D` | Développement d'une image ; ne constitue pas l'écriture d'une banque |
| Identification du processeur pour les fonctions RAW | IOPCode **dans le profil reçu** | `read_iopcode()` puis `is_xprocessor5()` ; aucune lecture de `0xD184` dans ces chemins |

`setup()` contrôle la réussite de `GetDeviceInfo`, mais **ignore le code PTP et le contenu retournés pour `0xD16E`**. Un retour `0x200A` sur cette propriété ne bloque donc pas, à lui seul, le backend backup. Une exception de transport à cet endroit reste bloquante. Il ne faut pas ajouter artificiellement un prérequis « D16E doit réussir » au test proposé. [Sources : setup][backup-read], [conversion][raw-conversion], [IOPCode][iopcode].

Aucun registre alternatif du type « autre Dxxx pour sélectionner C1 » n'a été trouvé dans le chemin X100F/X-T3 : **l'alternative est un objet de sauvegarde, pas un autre registre**.

### 2.2 Sélection de banque dans la sauvegarde

L'index `s` est basé sur zéro : C1 = 0, C7 = 6. L'ancre du slot est `sim0 + 256 × s`. Cette sélection est un calcul d'offset sur une copie en mémoire, sans changement du slot actif de l'appareil. Tous les offsets ci-dessous sont en octets, à partir du début du blob, hors conteneur USB PTP. [Source : BankLayout et `_slot_base`][layouts].

| Format du code | Taille exigée | Ancre simulation C1 | Ancre C7 | Nom | Checksum |
|---|---:|---:|---:|---|---|
| X100F / `_GEN3` | 5 660 | 3 909 | 5 445 | Aucun emplacement implémenté | Aucun checksum recalculé |
| X-T3 / `_GEN4_EARLY`, aussi attribué au X-T30 | **33 404** | **31 658** | **33 194** | Ancre + 67, champ de 16 octets | UINT16 little-endian à l'offset 176 |

| Champ | Décalage X100F | Décalage X-T3 | Encodage effectivement écrit |
|---|---:|---:|---|
| Simulation | 0 | 0 | Code sur un octet, table spécifique backup |
| Mode WB | -33 | -34 | Auto 0, Daylight 1, Shade 2, Fluorescent1/2/3 3/4/5, Incandescent 6, Underwater 7, Temperature 8 |
| Température WB | -32 | -33 | Index de la liste Kelvin triée décroissante ; valeur la plus proche ; 5 000 K → 10 |
| Réduction du bruit | -7 | -8 | Niveau + 4 |
| DR | +3 | +4 | DR100/200/400 → 1/2/3 |
| Couleur | +7 | +9 | Table ci-dessous |
| Netteté | +9 | +11 | `4 - round(valeur)` |
| Hautes lumières | +10 | +12 | `4 - round(valeur)` |
| Ombres | +11 | +13 | `4 - round(valeur)` |
| Color Chrome | Non mappé | +14 | Off/Weak/Strong → 0/1/2 |
| Grain | +12 | +15 | Strong/Weak/Off → 0/1/2 |
| Nom | Non mappé | +67 | ASCII, au plus 15 octets + NUL dans un champ de 16 ; caractères non ASCII ignorés |

Les codes de simulation backup sont : Provia 0, Astia 1, Velvia 3, Sepia 5, Monochrome 7, MonochromeR 8, MonochromeYe 9, MonochromeG 10, ProNegStd 11, ProNegHi 12, ClassicChrome 13, Acros 14, AcrosR 15, AcrosYe 16, AcrosG 17 ; **Eterna 18 seulement dans la table X-T3**. La table couleur est `-4→10, -3→9, -2→7, -1→8, 0→0, +1→6, +2→5, +3→4, +4→3`. L'inversion -1/-2 est explicite. [Source : tables et encodeur][bank-mapping].

**Ces codes ne sont pas ceux du profil D185 ou des presets récents.** Par exemple, Provia vaut 0 dans le backup mais 1 dans l'enum rawji ; Eterna vaut 18 dans le backup mais 16 dans rawji. Copier les valeurs des propriétés dans ces offsets serait incorrect. `AsShot` laisse le WB existant intact ; WB Custom1–3 n'a pas de code backup vérifié dans cette implémentation. [Sources : encodeur backup][bank-encode], [enums rawji][raw-enums].

Le checksum X-T3 est calculé ainsi, selon le code, sans avoir été vérifié ici sur l'appareil :

```text
checksum = (somme des octets [168, fin)
            - octet[176] - octet[177]
            + 0xFE6C) modulo 65536
stockage : octets 176 et 177, little-endian
```

Il porte sur le fichier complet à partir de `0xA8`, pas sur une banque isolée. [Source : `apply_checksum` et `_GEN4_EARLY`][checksum].

### 2.3 Mapping et sélection du chemin récent

`0xD18C` reçoit le numéro **1 à 7 en UINT16 little-endian**, avec attente de 100 ms après sélection. `0xD18D` reçoit une chaîne PTP UTF-16LE, limitée par grawji à 25 caractères. Les lectures numériques acceptent un payload de 4, 2 ou 1 octet ; pour 4 octets, le code conserve les 16 bits faibles. [Source : camera_presets][presets].

| Propriétés | Champs / encodages du backend récent |
|---|---|
| D18E / D18F | Taille / qualité, valeurs existantes réutilisées si lisibles |
| D190 / D191 / D192 | DR **100/200/400** ; inconnu conservé ; simulation enum rawji |
| D193 / D194 | Axes monochromes ×10 ; seulement pour les familles mono et valeurs non nulles |
| D195 | Grain et taille combinés, enum rawji |
| D196 / D197 / D198 | Color Chrome / FX Blue / Smooth Skin, Off/Weak/Strong = 1/2/3 |
| D199 / D19C / D19A / D19B | WB, Kelvin, décalage R, décalage B ; ordre WB → Kelvin si nécessaire → R → B |
| D19D / D19E / D19F / D1A0 | Hautes lumières / ombres / couleur / netteté ×10, signé encodé sur 16 bits ; couleur omise pour mono et sépia |
| D1A1 / D1A2 | NR par table rawji, **pas ×10** ; clarté ×10 |
| D1A3 / D1A4 / D1A5 | NR longue pose / espace couleur / inconnu, valeurs existantes réutilisées si lisibles |

L'ordre est celui de `encode_recipe()`, presque croissant, avec l'exception du groupe WB. Si les valeurs à préserver ne sont pas lisibles, des valeurs de repli existent : D18E=7, D18F=4, D191=0, D1A3=1, D1A4=1, D1A5=7. Pas d'opcode de commit ni de checksum dans cette voie. [Source : preset_recipe][preset-mapping].

## 3. Transport USB

### rawji/grawji

`FujiCamera` importe `usb.core` et `usb.util` de **PyUSB**. `pyproject.toml` exige `pyusb>=1.0.0`. Le code ne sélectionne pas explicitement un backend PyUSB ; l'installation Linux de grawji prévoit libusb. Ce n'est ni WPD, ni le SDK Fuji, ni PTP/IP. [Sources : transport][usb], [dépendance][raw-project].

`connect()` recherche le VID Fuji `0x04CB` parmi les PID connus, puis tout appareil Fuji en repli. Les PID incluent X100F `0x02D1`, X-T3 `0x02DD`, **X-T30 `0x02E3`**. Il tente ensuite de détacher le pilote noyau actif, applique la configuration USB, réclame l'interface **0**, puis examine l'interface `(0, 0)` pour trouver les endpoints Bulk IN/OUT et l'endpoint Interrupt éventuel. Les adresses Bulk sont découvertes, pas fixées dans le code. [Sources : `connect`][usb], [PID][pids].

Les commandes sont sérialisées dans des conteneurs USB PTP : en-tête de 12 octets `<IHHI` (longueur, type, code, transaction), paramètres UINT32 little-endian. `send_command()` écrit un conteneur COMMAND sur Bulk OUT, puis lit soit RESPONSE, soit DATA suivi de RESPONSE sur Bulk IN. `send_data_command()` émet COMMAND puis DATA. Le timeout par défaut est 5 s et les transferts sont découpés en blocs pouvant atteindre 512 Kio. [Source : conteneurs et transport][usb].

rawji ouvre lui-même la session PTP `1` avec `0x1002` et la ferme avec `0x1003`. En cas de `SessionAlreadyOpen (0x201E)`, il ferme puis réessaie. grawji ajoute, lors d'un échec de connexion, un essai de reset USB et une nouvelle tentative. **Ce sont des récupérations de transport/session, pas un déverrouillage des banques anciennes.** Ne pas les reprendre automatiquement dans un diagnostic conservateur. [Sources : sessions rawji][usb-session], [reprise grawji][core-session].

### xt30-probe

Le probe ouvre `IPortableDevice` et utilise `IPortableDevice::SendCommand` avec `WPD_COMMAND_MTP_EXT_EXECUTE_COMMAND_WITH_DATA_TO_READ`, puis `READ_DATA` et `END_DATA_TRANSFER`. Il transmet l'opcode et ses paramètres au pilote MTP Windows. Il ne réclame pas d'interface USB, ne choisit pas d'endpoints, et ne maîtrise pas l'ouverture de la session PTP. [Source : `MtpDevice` dans Probe.cs](../xt30-probe/Probe.cs).

La documentation Microsoft prévoit ce passage de commandes MTP/PTP et un code de réponse dans `WPD_PROPERTY_MTP_EXT_RESPONSE_CODE`. Le nom « MTP » n'implique donc pas, à lui seul, l'impossibilité d'émettre une commande PTP Fuji. Il ne garantit pas non plus une session identique à celle de rawji. [Source Microsoft](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/supporting-mtp-extensions).

## 4. Séquences exactes relevées dans le code

Les séquences d'écriture ci-dessous sont **une description du code audité, pas une procédure autorisée à exécuter**.

### 4.1 Lecture X-T3 / X100F

| Étape | Opération / paramètres | Sens des données |
|---|---|---|
| Connexion | Configuration USB, claim interface 0 ; `OpenSession 0x1002 [1]` | Pas de données PTP |
| Préambule | `GetDeviceInfo 0x1001 []` ; réponse OK exigée | Appareil → PC |
| Préambule | `GetDevicePropValue 0x1015 [0xD16E]` ; code PTP non contrôlé | Appareil → PC, si disponible |
| Métadonnées backup | `GetObjectInfo 0x1008 [0]` ; OK exigé | Appareil → PC |
| Contenu backup | `GetObject 0x1009 [0]` ; OK exigé | Appareil → PC |
| Fin | `CloseSession 0x1003 []`, release interface | Pas de données PTP |
| Hors connexion | Modèle à 0x14, choix du format, éventuelle extraction des noms | Traitement local |

`read_backup()` ne commence pas par énumérer les objets avec `GetObjectHandles` : le handle `0` est utilisé directement. Le résultat `GetObjectInfo` est contrôlé pour son code de réponse, mais son contenu n'est pas exploité pour valider la taille par cette fonction. [Source : lecture][backup-read].

### 4.2 Écriture X-T3 / X100F : trois connexions

1. **Connexion A : lecture** selon 4.1, puis déconnexion.
2. **Traitement local** : choix du format à partir du blob ; modification d'une copie aux offsets des slots demandés ; conservation des autres octets ; nom éventuel ; recalcul du checksum. `write_recipe()` refuse un slot hors 0–6 ou une taille différente de celle du format.
3. **Connexion B : restauration**. Nouvelle connexion/session ; même préambule `1001`, `1015(D16E)` ; `SendObjectInfo 0x100C [0,0]`, suivi de **1 076 octets** d'ObjectInfo initialement nuls : StorageID=0 à +0, format `0x5000` UINT16 à +4, ProtectionStatus=0 à +6, taille du blob UINT32 à +8. Puis `SendObject 0x100D []` et **le blob complet**. Réponse OK exigée pour chaque opération ; déconnexion.
4. **Connexion C : relecture** selon 4.1, puis comparaison entre original, cible et relecture.

Le commentaire de `transfer_recipes()` justifie les sessions séparées : le boîtier refuse un GetObject et un SendObject dans la même session avec `0x200F`. Ce n'est pas une commande spéciale de validation ; c'est une contrainte de séquence rapportée par l'auteur. Les champs réécrits par le boîtier sont traités séparément. La portée matérielle de la restauration reste **tous les réglages sauvegardés**, même si une seule banque a été modifiée en mémoire. [Source : transfert][transfer].

### 4.3 Écriture des presets récents

Après connexion et préambule, sur une seule session : lire le slot actif ; pour chaque slot demandé, écrire D18C=`s+1`, attendre 100 ms, écrire/lire le nom éventuel, lire les valeurs à préserver, puis écrire et relire chaque propriété de la liste ordonnée. Restaurer le slot actif initial dans `finally`, si sa lecture initiale était valide. [Source : backend presets][presets].

Attention : **`read_preset_names()` n'est pas strictement en lecture seule**, puisqu'il sélectionne successivement C1–C7 par `SetDevicePropValue(D18C)`. Il ne convient pas au test minimal, même si son nom commence par `read`.

### 4.4 Conversion rawji et contexte de D185

Le CLI rawji fait `connect → send_raf → get_profile`. L'adaptateur grawji reproduit cet ordre. `send_raf()` utilise :

- `0x900C [0,0,0]` et un ObjectInfo de RAF, format **`0xF802`**, nom `FUP_FILE.dat` ;
- `0x900D []` et le fichier RAF ;
- ensuite seulement `0x1015 [0xD185]` pour récupérer le profil.

Ces uploads sont des écritures vers l'appareil, même s'ils ne sont pas des écritures de recette persistante. Ils sont exclus du test proposé. `get_profile()` ne tente aucune autre propriété si D185 échoue. [Sources : rawji][raw-conversion], [appel CLI][raw-cli], [adaptateur grawji][core-session].

## 5. Différences avec xt30-probe et interprétation de 0x200A

Les deux rapports existants concordent :

| Observation locale | Résultat |
|---|---|
| Appareil | X-T30, VID 04CB, PID 02E3 ; champ PTP `DeviceVersion` = `1.00` |
| GetDeviceInfo | `0x2001`, 267 octets |
| Propriétés annoncées | `5001`, `D303`, `D406`, `D407` |
| D16E, D184, D185, D18C, D18D | **`0x200A` pour GetDevicePropDesc ET GetDevicePropValue** |
| Propriétés vendor D303, D406, D407 | Lectures réussies |
| Opérations backup en lecture | `1008`, `1009` annoncées, mais non essayées par le probe |
| Opérations backup en écriture | **`100C`, `100D` non annoncées** dans ces rapports |
| Opérations vendor | `900C`, `900D`, `901D` annoncées ; ce ne sont pas la paire standard `100C`/`100D` |

Source : rapport JSON de validation conservé localement et exclu du dépôt. Le numéro de série n'est volontairement pas recopié. Le champ `DeviceVersion` ne permet pas, sans vérification supplémentaire, d'établir toute la compatibilité firmware du format backup.

Le TXT met surtout en avant l'erreur de descripteur ; le JSON confirme que la lecture de **valeur** échoue aussi. Ce n'est donc pas seulement un faux négatif parce que rawji lirait la valeur sans demander le descripteur. [Source : `ProbeProperty` dans Probe.cs](../xt30-probe/Probe.cs).

| Point comparé | grawji/rawji | Probe actuel | Conséquence |
|---|---|---|---|
| Transport | PyUSB, interface 0, Bulk direct | WPD/MTP passthrough | Sessions et initialisation potentiellement différentes |
| D185 | Lu après upload du RAF | Lu sans upload | Les états du moteur RAW ne sont pas comparables |
| Anciennes banques | Objet backup 0 | Uniquement propriétés | **La bonne interface ancienne n'a pas encore été sondée** |
| Autorisations du code | Lecture et écriture, resets possibles | Liste blanche `1001`, `1014`, `1015` | `1008`/`1009` seraient actuellement bloqués localement |
| Détection banque récente | D18C doit être annoncé | D18C absent et lectures rejetées | Oriente vers le backup, sans prouver qu'il fonctionne |

Le probe récupère `0x200A` depuis `WPD_PROPERTY_MTP_EXT_RESPONSE_CODE` dans `END_DATA_TRANSFER`. Il ne fabrique pas ce code à partir de la liste des propriétés ; `ResponseName()` le traduit en texte. Les échecs COM/HRESULT sont traités séparément. La garde autorise bien `1015(D185)` : ce refus n'est pas produit par sa liste blanche. [Source : garde et transport dans Probe.cs](../xt30-probe/Probe.cs).

**Conclusion causale : non déterminée par analyse statique.** Les faits rendent injustifiée l'affirmation « WPD bloque toutes les propriétés Fuji », puisque plusieurs propriétés vendor répondent. Un filtrage particulier, une initialisation différente ou un état de session induit par le pilote restent possibles. Sans trace USB ou comparaison contrôlée, on ne peut pas attribuer chaque réponse au firmware seul ni exclure un effet du pilote.

L'absence de RAF est un **facteur de contexte démontré dans le code**, pas une preuve que le X-T30 retourne précisément `0x200A` pour cette seule raison. Les erreurs D16E/D184 ne sont pas expliquées par le code de chargement du RAF. Enfin, « X-T30 testé » dans rawji concerne la **conversion RAW**, pas la restauration des C1–C7. [Source : rawji, modèles testés][raw-tested].

## 6. Ce qui est raisonnablement applicable au X-T30 première génération

- **Prioriser une lecture backup du handle 0.** C'est la piste correspondant réellement au backend X-T3. Les échecs D18C/D18D et D185 n'invalident pas cette interface distincte.
- **Conserver WPD pour le premier essai.** Les commandes de lecture nécessaires sont standard et déjà annoncées. Aucun résultat actuel n'impose un changement de pilote.
- **Employer le format X-T3 comme hypothèse de décodage hors ligne**, seulement après récupération et conservation du blob original : modèle X-T30, taille, checksum, cohérence des octets et des noms. Ne rien normaliser ni réécrire pour « faire correspondre » le fichier.
- **Séparer trois preuves** : interface de lecture accessible ; offsets des banques confirmés sur le X-T30 ; restauration acceptée et fidèle. La première n'établit pas les deux suivantes.
- Même si le blob fait 33 404 octets, cette taille ne démontre pas l'identité des offsets. Le garde de taille protège contre certains mauvais formats, pas contre deux layouts différents de même longueur.
- Une lecture réussie ne permettra pas de déduire que `100C`/`100D` sont acceptés : leur absence de la liste actuelle devra rester explicitement documentée. Ne pas les remplacer par `900C`/`900D` par analogie, puisque ces derniers servent au RAF avec d'autres paramètres et formats.

## 7. Ce qui reste inconnu ou incomplet

**Validation matérielle.** Le README affirme les essais X100F/X-T3 et le code fournit des offsets, codes et checksum concrets, attribués à ces appareils. C'est une piste bien plus précise qu'une simple promesse de README. Mais aucun dump backup réel, capture PTP de restauration ou fixture matérielle X-T30 n'a été trouvé dans le checkout. Les tests utilisent des blobs synthétiques et une `FakeCamera` ; leur présence ne constitue pas une nouvelle validation sur matériel. [Sources : tests de transport][backup-tests], [tests du mapping][mapping-tests].

**Fidélité d'une recette ancienne.** L'encodeur backup ne mappe pas `wb_shift_r`/`wb_shift_b`, ni les axes de ton monochrome, l'exposition, la clarté, FX Blue, grain size, smooth skin ou l'espace couleur. Une absence de mapping ne prouve pas une absence dans le firmware. `unsupported_fields()` ne rapporte que les rejets rencontrés par `_encode()` : ces champs non visités ne sont pas tous signalés. Il ne faut pas promettre un transfert intégral de recette, notamment pour les décalages WB. Le code arrondit aussi les demi-pas avec `round()` et rapproche les Kelvin d'un preset. [Source : encodeur réellement appelé][bank-encode].

**Vérification après écriture.** La description générale « toute écriture relue et vérifiée » mérite plusieurs réserves :

- Pour le blob, `classify_readback()` compare seulement les positions dont la cible diffère de l'original et s'arrête à la plus petite des trois longueurs. Une valeur différente à la fois de l'original et de la cible est classée « maintained », même hors des offsets volatils déclarés. Les modifications collatérales d'octets non ciblés ne sont pas recensées par cette fonction.
- Le set volatil X-T3 contient exactement `{176, 248, 380, 408, 3276}` ; le checksum stocké utilise pourtant deux octets, 176 et 177. Cette liste n'est pas une spécification complète démontrée de tous les champs volatils.
- Pour les presets récents, un ACK sans relecture disponible est compté comme appliqué ; `0x8000` est accepté comme sentinelle pour certains champs. Un échec de clarté peut être rapporté comme note plutôt qu'arrêter le transfert.

Ces comportements sont des limites du validateur existant, pas la preuve d'une corruption observée. Ils interdisent de considérer son succès comme une garantie exhaustive pour un nouveau boîtier. [Sources : comparaison blob][readback], [presets][presets], [tolérances de relecture][preset-mapping].

**Détection de génération RAW.** `read_iopcode()` lit une chaîne hexadécimale UTF-16LE dans le profil, puis `is_xprocessor5()` teste `(iopcode & 0x00FFFF00) == 0x00179500`. `capabilities_for()` combine modèle, ce résultat et longueur du profil. Ce mécanisme limite les fonctions de conversion, sans choisir le layout backup. Le dépôt documente des profils natifs de 601 octets pour X100F, 605 pour X-T3 et 629 pour X-E5. [Sources : IOPCode][iopcode], [capacités][capabilities], [mesures rapportées][feature-matrix].

**Ancien « fallback X-T30 » de rawji.** `PROFILE_PARAM_INDEX_XT30`, avec commentaires sur `0x1D4`, subsiste dans `fuji_enums.py`, mais la recherche ne trouve pas d'utilisation de cette table. `parse_profile()` appelle `_parse_xt30_format()` si le profil est trop court pour 29 paramètres à `0x201` ; cette fonction retourne simplement un dictionnaire vide. Ce n'est pas un protocole USB alternatif fonctionnel. grawji utilise ses propres fonctions de lecture/modification du profil natif avec les offsets rawji et ignore certains champs au-delà de sa longueur. Ne pas convertir ces anciens commentaires en preuve de registres différents. [Sources : fallback rawji][raw-parser], [profil grawji][core-profile].

Dans le chemin actif du CLI rawji, `create_profile_from_camera()` reconstruit un profil standard avec paramètres à `0x201` et un IOPCode par défaut `FF159502`, documenté pour X-T30. La constante alloue **632 octets**, malgré les commentaires et messages qui disent encore 628. grawji préfère modifier une copie du profil natif reçu, sans allonger sa taille. Cette différence de représentation RAW n'est ni un autre transport ni un mécanisme de banques. [Sources : construction du profil][raw-profile-build], [appel effectif du CLI][raw-cli-write], [modification grawji][core-profile].

Restent à établir sur **ce X-T30** : accessibilité du handle 0 via WPD puis éventuellement USB direct, taille et version du backup, disposition réelle des banques et noms, checksum, champs WB, effets de l'initialisation Windows, acceptation des opérations de restauration et persistance après redémarrage. Aucun de ces points n'a été testé pendant cet audit.

## 8. Plus petit test proposé, sans écrire de recette

### Premier choix : lecture ciblée via WPD, sans changer le pilote

**Proposition uniquement : ni implémentée ni exécutée ici.** Ajouter à terme un mode dédié, désactivé par défaut, qui reproduit uniquement la branche `read_backup`. Garder les interdictions existantes de SetDevicePropValue et des opcodes vendor. Autoriser de façon ciblée **`GetObjectInfo(0)` et `GetObject(0)`**, avec contrôle du handle, sans permettre de lecture arbitraire de fichiers.

Sur un passage neuf, après fermeture des autres logiciels Fuji et avec le mode RAW CONV./BACKUP RESTORE sélectionné manuellement :

1. Ouvrir WPD ; `1001 []` pour l'identité et les capacités. Arrêter si ce n'est pas le X-T30 attendu ou si la commande échoue.
2. `1015 [D16E]`, journaliser le résultat. Un code PTP `200A` est consigné mais ne bloque pas la suite, comme dans grawji ; une erreur de transport impose l'arrêt.
3. **`1008 [0]`**. C'est le plus petit ajout permettant de savoir si les métadonnées de l'objet spécial sont accessibles. Conserver réponse et dataset brut ; relever format et taille, si le dataset est décodable.
4. Seulement si la réponse et les métadonnées sont cohérentes, **`1009 [0]`**, pour prouver que l'objet est bien une sauvegarde Fuji exploitable. Borne de transfert prudente proposée : 1 Mio ; une taille non nulle différente de 33 404 ne doit pas être assimilée automatiquement à une incompatibilité, mais rester une observation. Arrêter sur incohérence, dépassement ou erreur, sans reset automatique.
5. Fermer WPD. Sauvegarder localement le blob **intact**, sa longueur et SHA-256 ; inspecter hors ligne le préfixe et le modèle. Ne pas appeler `write_recipe`, `apply_checksum` pour réécrire le fichier, `restore_backup`, ni `read_preset_names`.

Soit **quatre opérations PTP en lecture** pour un essai complet neuf ; deux nouvelles opérations par rapport aux capacités du probe. L'étape 3 seule est encore plus petite mais ne prouve que l'accessibilité des métadonnées, pas la présence d'un backup valide. C'est un test de lecture de réglages, pas une garantie d'absence de tout état interne de session créé par Windows ou le boîtier.

| Résultat | Interprétation permise |
|---|---|
| `1008(0)` puis `1009(0)` OK, blob Fuji et modèle X-T30 | Interface backup en lecture accessible par WPD ; USB direct non indispensable pour cette lecture |
| Objet lisible mais taille/layout différents | Interface présente, mapping X-T3 non validé ; analyse hors ligne uniquement |
| `2009 InvalidObjectHandle` | Handle 0 indisponible dans cet état/session ; ne prouve pas l'absence définitive de banques accessibles |
| `2005 OperationNotSupported` ou erreur WPD/HRESULT | Échec de l'opération sur ce chemin ; origine pilote/session/firmware à départager |
| D16E échoue mais backup réussit | Confirme que D16E n'était pas une condition de réussite nécessaire dans cet essai |

Ne pas publier le blob brut : il contient des réglages complets et des identifiants du boîtier. Un résumé anonymisé suffit pour le diagnostic.

### Seulement si nécessaire : comparaison avec USB direct

Si le test WPD échoue, proposer ultérieurement le même mini-lecteur sur **un système Linux démarré séparément ou un autre ordinateur**, sans modifier le pilote Windows : PyUSB sur interface 0, `OpenSession`, mêmes quatre lectures, `CloseSession`. Un outil autonome restreint évitera le lancement de grawji/rawji et leurs voies d'écriture/reset. Repartir du même mode USB et d'un état frais dans chaque essai ; **aucun RAF, aucun SetDevicePropValue, aucun SendObject, aucun changement de slot**.

Un succès sous USB direct et un échec WPD montreraient une différence de chemin/session pour le **backup**, pas automatiquement la cause de D185. Pour isoler D185, il faudrait une comparaison à contexte RAW équivalent ; le chemin rawji connu requiert l'upload préalable d'un RAF. Cet upload sort du test strictement en lecture seule proposé ici. La conclusion honnête restera donc limitée tant que cette expérience distincte n'est pas autorisée et réalisée.

[pin]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/pyproject.toml#L19
[banks-commit]: https://github.com/p5k369/grawji/commit/d478a913c10f0f7338194107a98edf7dc4aa5420
[presets-commit]: https://github.com/p5k369/grawji/commit/241020ea82772fda28b6d93e7e7979887021f04b
[backup-read]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/camera_backup.py#L142-L166
[backup-names]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/camera_backup.py#L215-L233
[transfer]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/camera_backup.py#L236-L348
[readback]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/camera_backup.py#L178-L197
[layouts]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/backup_recipe.py#L166-L253
[bank-mapping]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/backup_recipe.py#L105-L310
[bank-encode]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/backup_recipe.py#L256-L325
[bank-names]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/backup_recipe.py#L372-L411
[checksum]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/backup_recipe.py#L32-L66
[presets]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/camera_presets.py#L64-L295
[preset-mapping]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/preset_recipe.py#L26-L255
[core-session]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/core.py#L478-L599
[core-profile]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/core.py#L306-L426
[capabilities]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/src/grawji/camera/capabilities.py#L172-L251
[feature-matrix]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/docs/feature-matrix.md#L78
[backup-tests]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/tests/test_camera_backup.py#L1-L153
[mapping-tests]: https://github.com/p5k369/grawji/blob/d6e4b7456014f070f0db9f9255a9f2732c7b8e58/tests/test_backup_recipe.py#L21-L203
[usb]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_usb.py#L14-L299
[usb-session]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_usb.py#L305-L342
[raw-conversion]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_usb.py#L348-L517
[raw-cli]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/__main__.py#L229-L241
[raw-project]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/pyproject.toml#L31-L33
[raw-enums]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_enums.py#L67-L139
[pids]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_enums.py#L475-L486
[iopcode]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_profile.py#L72-L94
[raw-parser]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_profile.py#L289-L332
[raw-profile-build]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/fuji_profile.py#L21-L244
[raw-cli-write]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/src/rawji/__main__.py#L352-L363
[raw-tested]: https://github.com/pinpox/rawji/blob/5549fdb93028549c2bab8abe963f1a91c50b5368/README.md#L163-L168
