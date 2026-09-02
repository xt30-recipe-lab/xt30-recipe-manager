# Phase 2 — inventaire des objets PTP du X-T30

Date de l'investigation : 31 août 2026. Cette phase est **strictement en lecture seule**. Elle ne modifie ni les C1–C7 locaux, ni un réglage du boîtier.

## Checkpoint avant investigation

La version fonctionnelle validée est conservée dans [`project.zip`](../checkpoints/before-phase2-20260831-232006/project.zip). Le manifeste externe est [`checkpoint.json`](../checkpoints/before-phase2-20260831-232006/checkpoint.json) et sa copie détaillée, incluse dans l'archive, est [`checkpoint-payload.json`](../checkpoints/before-phase2-20260831-232006/checkpoint-payload.json).

| Élément | SHA-256 |
|---|---|
| Archive `project.zip` | `A13D05FB1FE40DD53A14432593B12603BF7DEE70A1256DF4B181A78E4D4E0F5A` |
| `Probe.cs` avant phase 2 | `61452B742ACB1FAEB79175E5B4484DD84BF9FE864BD25E19C3AE85517B5221C4` |
| Moteur PTP/WPD, fichier complet avant phase 2 | `61452B742ACB1FAEB79175E5B4484DD84BF9FE864BD25E19C3AE85517B5221C4` |
| Région `MtpDevice`, lignes 253–509, UTF-8/LF | `14D98DA017A3BF198BEB68E42E03C64D3CF58316E498A6C96EF30C580EC4ADE3` |
| Whitelist avant phase 2, lignes 229–252, UTF-8/LF | `14376105C6D682019D17300115C565282C799C760777BD46D4DDE7575B089248` |
| `xt30-recipe-manager.exe` fonctionnel | `0B109878797621DC2A6422FF6D6590A0686CE36089ECCD9E8AB13E6A54740F35` |
| `xt30-probe.exe` checkpointé | `B3E0D9194A4F29F037FF62BEC1FE6EC2B0C461ADB402283F827D19AFFB34E1CC` |

L'archive contient 2 177 fichiers et 2 178 entrées avec le manifeste embarqué. Les hashes de `Probe.cs` et de l'exécutable ont été relus directement depuis le ZIP. L'instance fonctionnelle PID 16076 n'a pas été fermée ; les fichiers qu'elle gardait ouverts ont été copiés avec partage de lecture/écriture.

## Barrière de sécurité de la phase 2

L'outil séparé [`xt30-object-inventory.exe`](../xt30-probe/xt30-object-inventory.exe), construit depuis [`ObjectInventory.cs`](../xt30-probe/ObjectInventory.cs), possède deux contrôles successifs : sa liste locale et `MtpReadOnlyGuard` dans [`Probe.cs`](../xt30-probe/Probe.cs). Les seules opérations accessibles depuis son `Main` sont :

| Opcode | Opération | Paramètres | Phase de données |
|---|---|---|---|
| `0x1004` | `GetStorageIDs` | aucun | appareil → PC |
| `0x1005` | `GetStorageInfo` | `[StorageID]` | appareil → PC |
| `0x1007` | `GetObjectHandles` | `[StorageID, 0, 0]` | appareil → PC |
| `0x1008` | `GetObjectInfo` | `[ObjectHandle]` | appareil → PC |

`GetObject (0x1009)` n'est pas autorisé : l'inventaire ne peut donc télécharger ni photo, ni blob de sauvegarde. `DeleteObject`, `SendObjectInfo`, `SendObject`, `SetDevicePropValue` et tous les opcodes vendor sont également refusés. Le SHA-256 de l'outil après validation hors ligne est `26953FBC6A9193867B28144D2DB8C650A23AE82345E88D6135B9FFFD0451BA2A`.

La séquence prévue est exactement :

1. énumérer les périphériques WPD et ouvrir le Fujifilm ;
2. appeler `GetStorageIDs()` ;
3. pour chaque `StorageID`, appeler `GetStorageInfo(StorageID)` ;
4. appeler `GetObjectHandles(StorageID, ObjectFormat=0, Association=0)` ;
5. appeler `GetObjectInfo(handle)` une fois pour chaque handle unique ;
6. appeler séparément `GetObjectInfo(0)` pour les seules métadonnées du handle Fuji spécial.

Si le handle `0` apparaît déjà dans `GetObjectHandles`, son premier `GetObjectInfo` est réutilisé : l'outil ne l'interroge pas deux fois.

Le parseur conserve tous les champs du dataset PTP `ObjectInfo` : `StorageID`, `ObjectFormat`, `ProtectionStatus`, taille compressée, format/taille/dimensions de vignette, dimensions et profondeur de l'image, `ParentObject`, `AssociationType`, `AssociationDesc`, numéro de séquence, nom, dates, mots-clés, octets restants et dataset brut hexadécimal. `StorageInfo` conserve également type de stockage, système de fichiers, capacité d'accès, capacités totale/libre, nombre d'images libres, description et label.

### Validation hors ligne

`xt30-object-inventory.exe --self-test` passe sans ouvrir WPD. Ce test vérifie :

- l'acceptation de `0x1004`, `0x1005`, `0x1007` et `0x1008` par les deux barrières ;
- l'égalité exacte des whitelists avec les listes attendues, afin de détecter tout opcode supplémentaire ;
- le rejet de `0x1009`, `0x100B`, `0x100C`, `0x100D`, `0x1016`, `0x900C`, `0x900D` et `0x901D` ;
- le décodage little-endian de tableaux `StorageIDs` ;
- tous les champs d'un dataset `StorageInfo` synthétique ;
- tous les champs d'un dataset `ObjectInfo` synthétique ;
- le rejet d'un dataset `ObjectInfo` tronqué ;
- la sérialisation puis la relecture du rapport JSON.

Résultat : `SELF-TEST OK: garde-fous, StorageIDs, StorageInfo, ObjectInfo, troncature et JSON.` La compilation s'effectue sans avertissement via [`build-object-inventory.cmd`](../xt30-probe/build-object-inventory.cmd).

La preuve structurée de ce passage est conservée dans [`offline-validation.json`](../xt30-probe/phase2-inventory/offline-validation.json). Elle enregistre zéro appareil connecté, zéro commande caméra envoyée, les quatre seuls opcodes appelables et les hashes des sources et exécutables.

Le script indépendant [`validate_grawji_offline.py`](../xt30-probe/phase2-inventory/validate_grawji_offline.py) passe également sans charger PyUSB. Il vérifie par import/AST que le layout normalisé `FUJIFILM X-T30` possède 7 slots dans un blob de 33 404 octets (`sim0=31658`, `stride=256`), que `read_backup()` emploie `GetObjectInfo(0)` puis `GetObject(0)` sans `send_data_command`, et que libfuji contient la même séquence.

Le checkpoint a en outre été relu intégralement entrée par entrée : **2 177 / 2 177 fichiers correspondent au SHA-256 et à la taille du manifeste, zéro différence**.

## PTP OBJECT INVENTORY

Premier passage réel : [`ptp-object-inventory-20260831-232840.json`](../xt30-probe/phase2-inventory/ptp-object-inventory-20260831-232840.json).

Windows n'exposait alors **aucun périphérique WPD ni PnP Fujifilm présent**. L'outil s'est arrêté pendant l'énumération, avant l'ouverture d'un appareil et donc avant toute transaction PTP.

| Résultat demandé | Résultat actuel |
|---|---|
| Storages trouvés | Non déterminé — appareil absent de WPD au passage de 23:28:40 |
| Nombre de handles | Non déterminé |
| Objets inhabituels | Non déterminé |
| Handles spéciaux | `0` identifié dans le code grawji, pas encore interrogé sur ce passage |

Le scan fonctionnel antérieur prouve bien que ce même PC a ouvert le X-T30 : [`xt30_report_2026-08-31_230709.json`](../xt30-probe/rapports/xt30_report_2026-08-31_230709.json) contient `GetDeviceInfo = 0x2001`, modèle `X-T30`, firmware `1.00` et les opcodes annoncés `0x900C`, `0x900D`, `0x901D`. Il ne contient cependant aucun inventaire d'objets ; ces résultats ne doivent pas être inventés à partir de ce scan.

## FUJI BACKUP

**Mécanisme trouvé dans le code : OUI. Validation réelle sur ce X-T30 pendant cette phase : NON, boîtier absent lors du passage.**

[`camera_backup.py`](../research/grawji-audit-20260831/src/grawji/camera/camera_backup.py) définit le handle backup comme `0`, appelle d'abord le standard `GetObjectInfo(0)`, puis le standard `GetObject(0)`. La fonction est explicitement nommée `read_backup`; le flux est appareil → PC. La restauration est un chemin distinct, standard `SendObjectInfo (0x100C)` puis `SendObject (0x100D)`, que nous n'appelons pas.

Cette convention n'est pas propre à grawji : [`libfuji/fuji_usb.c`](../research/libfuji/lib/fuji_usb.c) implémente `fujiusb_download_backup()` avec la même paire `ptp_get_object_info(r, 0)` puis `ptp_get_object(r, 0)`, et limite cette fonction au transport `FUJI_FEATURE_RAW_CONV`. Le handle `0` est donc traité comme un objet virtuel Fuji hors de l'inventaire ordinaire ; aucune source consultée ne garantit qu'il sera renvoyé par `GetObjectHandles`, ce qui justifie sa lecture de métadonnées séparée.

Le même code reconnaît un blob par la signature ASCII `FUJIFILM`, lit le modèle à l'offset `0x14`, et attend le format objet `0x5000`. [`backup_recipe.py`](../research/grawji-audit-20260831/src/grawji/camera/backup_recipe.py) associe explicitement `XT3` et `XT30` au layout X-Processor 4 précoce de 33 404 octets. Cela prouve l'intention et le protocole de grawji ; cela ne prouve pas encore que notre X-T30 firmware 1.00 renverra ces métadonnées via WPD.

Une lecture complète sans écriture est techniquement possible avec `GetObject(0)`, mais cette commande n'a pas été exécutée et n'est pas présente dans la whitelist de cet inventaire. La première vérification retenue reste `GetObjectInfo(0)` seulement.

## VENDOR OPCODES — analyse statique, aucune exécution

Les valeurs vendor sont spécifiques au constructeur : un même nombre peut avoir un autre sens chez Canon. Il faut donc interpréter ici les noms Fuji. Le `ptp.h` actuel de libgphoto2 ne définit pas `PTP_OC_FUJI_SendObjectInfo`, `SendObject2` ou `SendObject`; il ne fournit donc aucune preuve que ces opérations Fuji seraient des lectures. Les définitions exploitables viennent de libfuji/fudge, puis sont confirmées pour `0x900C/0x900D` par rawji et latent/filmkit.

| Opcode | Nom Fuji connu | Paramètres / données | Réponse attendue | Portée attestée | Classement |
|---|---|---|---|---|---|
| `0x900C` | `PTP_OC_FUJI_SendObjectInfo` | paramètres `[storage_id, handle, 0]`; **Data OUT** contenant un dataset `ObjectInfo`. Pour la conversion RAW : stockage `0`, handle `0`, format `0xF802`, taille du RAF, nom `FUP_FILE.dat`. | Les implémentations testent `0x2001 OK`; paramètres de réponse ignorés/non établis. | libfuji dit disponible sur presque tous les Fuji ; le X-T30 le déclare réellement dans son `DeviceInfo`; rawji/libfuji l'utilisent en mode RAW conversion. | **STATE_CHANGE/WRITE** |
| `0x900D` | `PTP_OC_FUJI_SendObject2` | aucun paramètre ; **Data OUT** contenant tous les octets du RAF après `0x900C`. | Les implémentations testent `0x2001 OK`; aucun paramètre de réponse attendu dans le code observé. | Même paire et même portée que `0x900C`; déclaré par notre X-T30. | **STATE_CHANGE/WRITE** |
| `0x901D` | `PTP_OC_FUJI_SendObject` | libfuji le décrit comme « write to file » et dit que `0x900D` paraît identique ou très proche. Paramètres exacts, format du payload et préconditions non établis dans les sources consultées. | Réponse PTP de succès/erreur attendue par nature, mais aucun appel Fuji observé ne permet de prouver les paramètres ou la réponse de succès exacte. | Déclaré par notre X-T30 ; présent comme constante libfuji, sans implémentation appelante trouvée. | **STATE_CHANGE/WRITE** |

Sources principales : [`fujiptp.h`](../docs/reference/libfuji_fujiptp.h), [`fuji_usb.c`](../docs/reference/libfuji_fuji_usb.c), [`rawji/fuji_usb.py`](../research/rawji-audit-20260831/src/rawji/fuji_usb.py), [`latent session.ts`](../research/latent/packages/ptp-fuji/src/ptp/session.ts), et le [`ptp.h` de libgphoto2](https://github.com/gphoto/libgphoto2/blob/master/camlibs/ptp2/ptp.h).

Aucun de ces trois opcodes n'est `SAFE_READ`. Aucun n'a été envoyé au X-T30.

## CUSTOM SETTINGS C1–C7

| Question | Réponse actuelle |
|---|---|
| Méthode potentielle trouvée | **OUI** — blob Fuji au handle spécial `0`, puis layout `XT30 = _GEN4_EARLY` dans grawji |
| Données réellement lues de C1–C7 dans cette phase | **NON** |
| Données locales de l'interface modifiées | **NON** |
| Valeur passée de `LOCAL` à `CAMERA` | **AUCUNE** |

Même si `GetObjectInfo(0)` réussit, il ne donnera que les métadonnées de l'objet. Il ne suffira pas à marquer C1–C7 comme données caméra. Il faudrait lire le contenu par `GetObject(0)`, vérifier signature, modèle et taille, puis parser les slots sans écriture. Cette nouvelle commande ne sera pas ajoutée ou exécutée sans présenter d'abord le paquet exact et sa justification de sécurité.

## État de l'investigation

La partie statique et l'outil d'inventaire sont terminés. Le seul blocage restant est physique : le X-T30 n'était plus présent dans l'énumération Windows au moment du passage. Dès qu'il réapparaît, l'outil existant peut produire l'inventaire demandé sans recompilation et sans répéter les propriétés `0xD18C–0xD1A5`, `0xD34C` ou `0xD185`.
