# Analyse octet par octet des fichiers de sauvegarde X-T30 — recherche de l'ISO et du réglage N&B

> **Nature de ce document.** Analyse **hors ligne** de fichiers `.dat` déjà présents sur le
> disque. Aucun outil de connexion USB/PTP n'a été exécuté pour la produire : ni
> `xt30-backup-read.exe`, ni `xt30-probe-cli.exe`, ni `Restore-CameraSettings.ps1`.
> Les mesures portent uniquement sur des octets lus depuis des fichiers.
>
> **Convention de lecture.** Tout ce qui est sous le titre « Mesuré » est un fait vérifiable en
> relisant les fichiers. Tout ce qui est sous « Hypothèse à tester » est une interprétation qui
> **n'est pas démontrée** et qui appelle une expérience de contrôle.
>
> Instantané pris le 2026-09-02 à ~19h41. Des lectures supplémentaires étaient en cours d'écriture
> par une autre session pendant l'analyse ; les chiffres ci-dessous valent pour ce instantané.

---

## 1. Inventaire des fichiers (mesuré)

**43 fichiers `.dat`**, tous de **5 628 octets**, tous avec la signature `FUJIFILM` `X-BACKUP`
`0100` `X-T30` et le même numéro de série `01365935353533341904` à l'offset `0x34`.

### 1.1 Deux emplacements

| Dossier | Nombre |
|---|---|
| `xt30-probe\phase2-inventory\` | 39 (1 fichier généré + 38 lectures `xt30-settings-*`) |
| `Documents\FUJIFILM\` | 4 (2 sauvegardes Tether App + 2 fichiers générés par le PC) |

### 1.2 Trois provenances — distinction indispensable

La comparaison de deux fichiers n'a de sens que si l'on sait qui les a écrits.

| Provenance | Fichiers | Ce qu'un écart signifie |
|---|---|---|
| **Lecture appareil** (`GetObject`) | `xt30-settings-*.dat` (38) | l'appareil a changé d'état |
| **Sauvegarde Fujifilm** (Tether App) | `FUJIFILM_X-T30_2026 9 2_1624.dat`, `…_1644.dat` | idem, via le logiciel constructeur |
| **Généré par le PC** | `A-RESTAURER-C1-CLAUDE-TEST.dat`, `CLAUDE-TEST-C1.dat`, `PACK-C1-C5.dat`, `MON-SET-C1-C7.dat` | c'est **notre** modification, pas une information sur l'appareil |

### 1.3 Chronologie des fichiers non répétitifs

| Heure | Fichier | Provenance | Noms C1…C7 |
|---|---|---|---|
| 15:12:24 | `xt30-settings-20260902-151224.dat` | appareil | PORTRA400 / KODAKGOLD200 / KODAK64 R+2 B-5 / SUMMER CHROME R+5 B-6 / CLASSIC CUBAN NEGA R+4 B5 / PACIFIC R+1 B-3 / 800T R+8 B-8 |
| 15:40:43 | `xt30-settings-20260902-154043.dat` | appareil | idem |
| 16:17:02 | `A-RESTAURER-C1-CLAUDE-TEST.dat` | PC | CLAUDE TEST / … |
| 16:24:41 | `FUJIFILM_X-T30_2026 9 2_1624.dat` | Tether App | PORTRA400 / … |
| 16:44:41 | `FUJIFILM_X-T30_2026 9 2_1644.dat` | Tether App | PORTRA400 / … |
| 16:45:54 | `CLAUDE-TEST-C1.dat` | PC | CLAUDE TEST / … |
| 17:25:24 | `PACK-C1-C5.dat` | PC | KODAK PORTRA 400 R+4 B-5 / … |
| 17:43:20 | `MON-SET-C1-C7.dat` | PC | PACIFIC R+1 B-3 / 800T R+0 B+0 / SHINA R+2 B-5 / SUMMER R+5 B-6 / PORTRA800 R-1 B-3 / BLEACH R+8 B-9 / AGFA400 R+0 B+0 |
| 17:45:20 | `xt30-settings-20260902-174520.dat` | appareil | idem `MON-SET` |
| 19:34:10 → 19:40:55 | 29 lectures `xt30-settings-2026090219…dat` | appareil | idem `MON-SET` |

---

## 2. Différences entre fichiers consécutifs (mesuré)

### 2.1 Les paires qui apprennent quelque chose

**15:12 → 15:40 — 6 octets** (l'expérience de netteté déjà documentée en §8bis du doc 10) :

| Offset | Avant → Après | Classement |
|---|---|---|
| 176 | 231 → 234 | hors banque — somme de contrôle (octet bas) |
| 177 | 114 → 113 | hors banque — somme de contrôle (octet haut) |
| 1149 | 250 → 252 | hors banque — compteur |
| 1151 | 254 → 0 | hors banque — compteur |
| 3772 | 33 → 100 | hors banque — donnée objectif |
| **5429** | **5 → 4** | **C7, rel +11 = netteté** |

**16:24 → 16:44 — 1 seul octet** : 3772 (171 → 100), donnée objectif. La somme de contrôle **ne
bouge pas**, ce qui confirme que cet octet est exclu du calcul.

**17:43 → 17:45 — 0 octet.** Le fichier `MON-SET-C1-C7.dat` généré par le PC et la lecture faite
juste après la restauration sont **strictement identiques, octet pour octet**. L'appareil a stocké
notre fichier tel quel et l'a restitué à l'identique.

**15:40 → 17:45 — 162 octets**, tous dans les sept banques (champs cartographiés + noms) plus la
somme de contrôle et trois compteurs. C'est l'effet de la restauration de `MON-SET-C1-C7.dat`.

### 2.2 Les paires qui n'apprennent rien sur l'appareil

Les paires impliquant un fichier généré par le PC (`…→ A-RESTAURER…`, `…→ CLAUDE-TEST-C1`,
`… → PACK-C1-C5`, `… → MON-SET-C1-C7`) ne montrent que **nos propres écritures** : noms de banque,
champs déjà cartographiés, somme recalculée. Elles sont utiles à un seul titre : **elles ne
touchent jamais un octet hors du périmètre attendu**, ce qui reconfirme le contrôle de
`CameraBankFile.PrepareMany`. Le détail complet figure en annexe A.

### 2.3 La séquence 19:34 → 19:41 : 29 lectures, 2 octets modifiés

C'est la donnée la plus précieuse du lot. Entre 19:34:10 et 19:40:55, **29 lectures successives**
de l'appareil ont été enregistrées. Sur ces 29 fichiers, **exactement deux octets ont changé**, à
deux instants distincts, et **aucun octet de banque n'a bougé** :

| Offset | Séquence temporelle | Classement |
|---|---|---|
| **840** | 12 @17:45 → 13 @19:34:10 → **18 @19:36:24** | hors banque |
| **854** | 2 @17:45 → **1 @19:38:40** | hors banque |
| 176 | 118 → 227 @19:34:10 → 232 @19:36:24 → 231 @19:38:40 | somme de contrôle |

Autrement dit : **deux réglages ont été modifiés au boîtier pendant cette séance, et chacun se
traduit par un seul octet, situé hors des banques C1–C7.**

> **Cette analyse ne sait pas quels réglages ont été touchés à 19:36:24 et à 19:38:40.**
> Cette information n'existe dans aucun fichier ; elle est détenue par la personne qui a manipulé
> l'appareil. C'est le chaînon manquant : dès qu'elle est fournie, les offsets 840 et 854 sont
> identifiés de façon quasi certaine, parce que la mesure est celle d'une expérience à variable
> unique, répétée sur 29 lectures.

### 2.4 Le saut 17:45 → 19:34

26 octets, tous hors banque : 248, 348, 349, 352, 356, 424, 753, 758, 760, 774, 776, 831, 840,
842, 960, 1149, 1158, 1262, 3768–3773, 3830, plus 176. Cette lecture est séparée des précédentes
par près de deux heures d'usage réel du boîtier : plusieurs réglages globaux ont changé en même
temps, on ne peut donc **rien** attribuer individuellement ici.

---

## 3. Classement des offsets

### 3.1 Périmètre exact des banques (mesuré, corrige une approximation du doc 10)

Avec `Sim0 = 3882`, `Stride = 256`, 7 banques, la fenêtre de 256 octets qui contient toute la
donnée utile va de **rel −46 à rel +209** (offsets absolus 3836 → 5627, soit très exactement les
**1 792 derniers octets du fichier** : 7 × 256 = 1792 et 5628 − 1792 = 3836).

Mais la donnée réelle n'occupe pas ces 256 octets :

| Plage relative | Contenu mesuré |
|---|---|
| rel −46 … −39 | **tous nuls dans les 7 banques, dans tous les fichiers** |
| **rel −38 … +103** | zone vivante — 142 octets |
| rel +104 … +209 | **tous nuls dans les 7 banques, dans tous les fichiers** |

**L'enregistrement d'une banque fait donc environ 142 octets utiles, complétés à 256 par du
remplissage nul.**

### 3.2 Offsets hors banque rencontrés

| Offset | Statut |
|---|---|
| 176 / 177 | somme de contrôle u16 little-endian — **cartographié** |
| 248 | compteur volatil — cartographié (grawji) |
| 1149 / 1151 | compteurs — cartographiés (§8bis doc 10) |
| 3768–3773 | bloc « objectif » (chaîne `LX230A` en 3774) — **seul 3772 était cartographié ; voir §5** |
| 3830 | inconnu, varie avec le bloc objectif |
| 348, 349, 352, 356, 424, 753, 758, 760, 774, 776, 831, **840**, 842, 960, 1158, 1262, **854** | **non cartographiés** — réglages globaux du boîtier |

### 3.3 Offsets dans une banque et non cartographiés

Voir la carte complète au §4. Résultat clé : **aucun** d'entre eux n'a jamais changé, dans aucun
des 43 fichiers.

---

## 4. Carte des 256 octets d'une banque (mesuré)

Fichier de référence : lecture appareil la plus récente disponible au moment de l'analyse.
Les valeurs sont données pour C1…C7 dans cet ordre.

### 4.1 Les offsets qui varient d'une banque à l'autre

**Ce sont exactement les onze champs déjà cartographiés, plus le nom. Pas un de plus.**

| rel | Champ | C1…C7 |
|---|---|---|
| −34 | wb_mode | 8, 8, 1, 0, 8, 6, 8 |
| −33 | wb_kelvin | 7, 21, 0, 0, 5, 7, 21 |
| −8 | noise_reduction | 0, 1, 0, 0, 0, 0, 0 |
| 0 | film_simulation | 13, 11, 13, 13, 13, 11, 14 |
| +4 | dynamic_range | 3, 2, 1, 3, 3, 3, 3 |
| +9 | color | 3, 8, 5, 3, 4, 10, 4 |
| +11 | sharpness | 6, 3, 3, 4, 6, 4, 0 |
| +12 | highlight | 6, 1, 4, 6, 6, 1, 6 |
| +13 | shadow | 1, 3, 3, 6, 5, 1, 0 |
| +14 | color_chrome | 2, 0, 2, 2, 2, 0, 2 |
| +15 | grain | 0, 0, 1, 0, 0, 1, 1 |
| +78 … +102 | nom ASCII | (chaînes distinctes) |

`+8 dr_priority` vaut 3 dans les sept banques ici : il est cartographié mais ne varie pas dans ce
jeu de données.

### 4.2 Les offsets non nuls et **identiques dans les 7 banques**

Ces octets ne peuvent pas être des réglages « par banque » au vu des données actuelles — ou alors
l'utilisateur a mis la même valeur partout. **Aucun n'a jamais changé, dans aucun des 43 fichiers,
ni entre lectures appareil, ni après une restauration complète des 7 banques.**

| rel | Valeur | rel | Valeur |
|---|---|---|---|
| −38 | 1 | +25, +26, +27 | 2, 2, 2 |
| −35 | 3 | +29 | 1 |
| **−32 … −9 (24 octets)** | **9 partout** | +34 | 1 |
| −7 | 1 | +35, +36 | 3, 3 |
| −5 | 1 | +37, +38, +39 | 18, 18, 18 |
| −2 | 2 | +40, +41, +42 | 19, 19, 19 |
| −1 | 1 | +43 … +46 | 4, 4, 4, 4 |
| +1 | 14 | +47 | 3 |
| +2 | 7 | +48 | 2 |
| +3 | 9 | +49, +50, +51 | 11, 11, 11 |
| +16, +17 | 9, 9 | +52, +53, +54 | 15, 15, 15 |
| +18, +19, +20 | 7, 7, 7 | +59 … +62 | 43, 43, 43, 43 |
| +21 … +24 | 1, 1, 1, 1 | +63 | 15 |
| | | +67 | 1 |
| | | +73, +74, +75, +76, +77 | 2, 5, 7, 3, 7 |

### 4.3 Les offsets nuls dans les 7 banques

rel −46…−39, −6, −4, −3, +5, +6, +7, +10, +28, +30…+33, +55…+58, +64, +65, +66, +68…+72,
+103…+209.

### 4.4 Résultat central pour la mission

> **Mesuré : dans les 43 fichiers, il n'existe AUCUN offset relatif non cartographié qui varie
> d'une banque à l'autre.** L'indice fort recherché par la mission est absent des données
> actuelles.

Deux lectures possibles de ce fait, et il faut trancher par expérience :

1. l'ISO et le réglage N&B **ne sont pas dans l'enregistrement de banque** de ce fichier ;
2. ils y sont, mais les sept banques portent **la même valeur** (par exemple ISO AUTO partout et
   aucun réglage N&B, ce qui est plausible : une seule banque est en ACROS).

L'expérience du §7.1 sépare les deux en une manipulation.

---

## 5. Découverte de sécurité : la formule de somme de contrôle est incomplète

**Mesuré.** La formule actuellement implémentée dans `Models/CameraBankFile.cs` —

```
somme = ( Σ octets, en excluant 176, 177 et 3772 , + 0xE1E5 ) & 0xFFFF
```

— **échoue sur les 29 lectures postérieures à 19:34**, avec un écart constant de **52**. Elle
n'était correcte que parce que les deux fichiers ayant servi à la calibrer avaient des octets
« objectif » identiques : le biais `0xE1E5` absorbait silencieusement leur contribution. Dès que
l'objectif change (offsets 3768–3773 et 3830 modifiés entre 17:45 et 19:34), la formule casse.

Ensembles d'exclusion testés sur les 38 lectures appareil, biais recalibré sur la première :

| Ensemble exclu | Biais | Résultat |
|---|---|---|
| `{176, 177, 3772}` — **actuel** | 0xE1E5 | **9 / 38** — échoue sur toutes les lectures ≥ 19:34 (écart 52) |
| `{176, 177, 3768…3773}` | 0xE30D | 9 / 38 — échoue (écart 10) |
| **`{176, 177, 3769, 3770, 3771, 3772}`** | **0xE257** | **38 / 38 ✔** |
| `{176, 177, 3768…3773, 3830}` | 0xE339 | 38 / 38 ✔ |
| `{176, 177, 3760…3835}` | 0xF456 | 38 / 38 ✔ |

**Mesuré :** trois ensembles reproduisent parfaitement les 38 fichiers. Le plus petit ajout
suffisant est **3769, 3770 et 3771**.

**Hypothèse à tester :** l'appareil exclut en réalité tout un bloc « données objectif » autour de
3768–3773 (voire jusqu'à 3830), et non trois octets isolés. Les données actuelles **ne permettent
pas de trancher** entre ces trois candidats, parce que les octets concernés n'ont varié que d'une
seule façon (une seule paire avant/après). Il faudrait une lecture avec un troisième jeu de
valeurs d'objectif — par exemple après montage d'un autre objectif.

> **Conséquence pratique et immédiate.** Tant que `ChecksumExcluded` vaut `{176, 177, 3772}`,
> `IsValidSettingsFile()` **rejettera comme « corrompu » tout fichier lu depuis ce boîtier
> maintenant que l'objectif a changé**, et un fichier généré à partir d'une telle base porterait
> une somme que l'appareil pourrait refuser. Le correctif minimal et démontré est d'ajouter
> **3769, 3770, 3771** à l'ensemble exclu et de porter le biais à **0xE257**. Ce document ne
> modifie aucun code : c'est une décision à prendre explicitement.

---

## 6. Candidats pour l'ISO et pour le réglage N&B

### 6.1 Ce qui est mesuré

1. Aucun octet **de banque** non cartographié n'a jamais changé, dans aucun fichier.
2. Pendant la séance de 19:34 à 19:41, deux réglages ont été modifiés au boîtier ; ils
   correspondent à **deux octets isolés, tous deux hors banque** : **840** et **854**.
3. Il existe une copie ASCII du nom de la banque active (`PACIFIC R+1 B-3`, c'est-à-dire C1) à
   l'offset **1417**, en dehors de la zone des banques. Une comparaison de la zone 1293–1442 avec
   l'enregistrement de C1 (ancre 1339) ne donne que 42 correspondances sur 150 : **ce n'est donc
   pas une copie de l'enregistrement de banque**, seulement le nom.

### 6.2 Trois meilleurs candidats pour l'ISO, par ordre de force

#### Candidat n°1 — offset absolu **840** (hors banque)

- **Mesuré.** Séquence : 12 (17:45) → 13 (19:34:10) → **18 (19:36:24)**, puis stable sur les
  17 lectures suivantes. À 19:36:24, c'est le **seul** octet de données du fichier qui change
  (avec la seule somme de contrôle). 29 lectures encadrent ce changement.
- **Pourquoi c'est le meilleur candidat.** C'est une expérience à variable unique, de qualité
  exceptionnelle : un changement de réglage au menu, un seul octet qui bouge, confirmé par
  répétition avant et après. Aucun autre octet du fichier n'a cette propriété.
- **Hypothèse à tester.** Si le réglage modifié à 19:36:24 était l'ISO, alors 840 est l'ISO
  **global du boîtier** (pas l'ISO par banque). Le saut 13 → 18 vaudrait 5 crans, ce qui
  correspond à 5 tiers de diaphragme sur l'échelle ISO du X-T30 si l'encodage est un index de
  liste. **Rien dans les fichiers ne confirme la nature du réglage** ; il faut la réponse de la
  personne qui a manipulé l'appareil.
- **Voisinage** (mêmes fichiers) : 839 = 13 constant, 840 = variable, 841 = 14 constant,
  842 = 14 → 17 (changé une seule fois, à 19:34), 843 = 7, 844 = 7. La présence d'un groupe
  suggère une petite table, mais ce n'est pas démontré.

#### Candidat n°2 — offset absolu **842** (hors banque)

- **Mesuré.** 14 (jusqu'à 17:45) → 17 (19:34:10), puis stable. Immédiatement voisin de 840.
- **Pourquoi.** Si 840/842 forment une paire (par exemple ISO courant / borne d'ISO auto, ou ISO
  du mode photo / ISO du mode vidéo), 842 est le second membre naturel. Le doc 11
  (`11-recettes-video-et-flux-automatique.md`) évoque justement des réglages dédoublés photo/vidéo.
- **Faiblesse.** Son changement est noyé dans le saut de 26 octets du 17:45 → 19:34 : il n'est pas
  isolé, donc non attribuable.

#### Candidat n°3 — bloc **rel +37 … +46** dans la banque (18,18,18 / 19,19,19 / 4,4,4,4)

- **Mesuré.** Non nul, strictement identique dans les 7 banques et dans les 43 fichiers.
  Structure en groupes réguliers de 3 puis 3 puis 4.
- **Pourquoi.** C'est le seul endroit de l'enregistrement de banque qui ressemble à une table de
  petits index numériques susceptibles de coder une sensibilité (valeur, borne basse, borne haute
  d'ISO auto — le X-T30 mémorise trois jeux AUTO1/AUTO2/AUTO3, ce qui expliquerait les groupes de
  trois). Les valeurs 18 et 19 sont dans la plage d'un index d'échelle ISO au tiers.
- **Faiblesse, à dire franchement.** **C'est un raisonnement de forme, pas une mesure.** Aucun de
  ces octets n'a jamais varié. Cette hypothèse ne vaut que si l'expérience du §7.1 montre qu'un
  changement d'ISO **dans une banque** écrit bien dans l'enregistrement de banque.

### 6.3 Candidat pour le réglage N&B (« Monochromatic Color » / B&W ADJ)

#### Candidat unique et fort — offset absolu **854** (hors banque)

- **Mesuré.** 2 (de 15:12 à 19:38:26) → **1 (19:38:40)**, puis stable sur les 11 lectures
  suivantes. Là encore, c'est le **seul** octet de données à changer à cet instant.
- **Hypothèse à tester.** C'est le second réglage modifié pendant la séance. Si la personne a
  changé l'ISO puis le réglage N&B, alors 840 = ISO et 854 = réglage N&B. Un pas de 2 → 1 est
  cohérent avec un cran unique sur une échelle courte.
- **Voisinage** : 851, 852 = 9, 9 ; 853 = 3 ; 854 = variable ; 855, 856 = 3, 3 ; 857 = 10.

### 6.4 Piste secondaire : le bloc de 24 octets à 9

**Mesuré.** rel −32 à −9 : **24 octets valant tous 9**, dans les 7 banques et dans les 43 fichiers.
Ils sont collés à `wb_mode` (rel −34) et `wb_kelvin` (rel −33).

**Hypothèse à tester.** Il s'agirait du **décalage de balance des blancs par banque** : 12 modes de
balance des blancs × 2 axes (R et B), encodés sur 0…18 avec **9 = neutre**. Cela expliquerait
parfaitement une valeur constante à 9 : l'utilisateur n'a jamais enregistré de décalage *dans* une
banque — il les note dans le **nom** de la banque, précisément parce qu'il croit (et le doc 10
l'affirme) que le X-T30 ne les mémorise pas.

**Enjeu.** Si cette hypothèse se vérifie, l'affirmation « le X-T30 ne mémorise pas le WB Shift par
banque » (doc 10 §9bis, `CameraBankFile.BuildBankName`) serait **fausse**, et l'application
pourrait transférer le WB Shift au lieu de le coder dans le nom. C'est une amélioration
fonctionnelle importante, et elle se teste en une manipulation (§7.2).

---

## 7. Expériences de contrôle proposées (aucune n'est exécutée ici)

Toutes se font **au menu du boîtier**, suivies d'une relecture par l'outil de lecture existant.
Elles ne demandent aucune écriture depuis le PC.

### 7.1 Trancher la question de l'ISO par banque — expérience décisive

1. Sélectionner **C2**. Régler l'ISO sur une valeur nettement différente de celle de C1.
2. Enregistrer explicitement la banque (`ÉDITER/ENREG. RÉGL. PERSO`), pas seulement la changer.
3. Relire le fichier.

- Si un octet **de banque** bouge dans C2 seulement → **l'ISO est dans l'enregistrement de banque**,
  et son offset relatif est identifié en une expérience.
- Si seul **840** bouge → l'ISO est **global** ; la mention actuelle « la banque le retient, mais sa
  position n'est pas identifiée » doit alors être corrigée.

Répéter avec le réglage N&B sur une banque en ACROS ou Monochrome.

### 7.2 Vérifier le bloc de 24 octets à 9

Régler un décalage de balance des blancs **franc** (par exemple R+5 B−5) sur une banque, enregistrer,
relire. Si deux octets de la plage rel −32…−9 passent de 9 à 14 et 4 (soit 9 ± 5), l'hypothèse WB
Shift est démontrée et l'échelle est établie du même coup.

### 7.3 Compléter la somme de contrôle

Monter un autre objectif, relire, et vérifier laquelle des trois formules du §5 tient encore. C'est
le seul moyen de distinguer « 3769–3771 exclus » de « tout le bloc objectif exclu ».

### 7.4 Information à fournir, qui vaut plusieurs expériences

**Quels réglages ont été modifiés au boîtier à 19:36:24 et à 19:38:40 ?** Les fichiers prouvent
qu'un seul octet a changé à chaque fois ; il manque uniquement le nom du réglage. Cette réponse
identifie 840 et 854 immédiatement, sans nouvelle manipulation.

---

## 8. Récapitulatif

### Mesuré
- 43 fichiers, tous 5 628 octets, même boîtier, même numéro de série.
- La zone des banques occupe exactement les 1 792 derniers octets (3836 → 5627) ; la donnée utile
  d'une banque tient en ~142 octets (rel −38 … +103), le reste est du remplissage nul.
- **Aucun offset relatif non cartographié ne varie d'une banque à l'autre**, dans aucun fichier.
- **Aucun octet de banque non cartographié n'a jamais changé**, dans aucun fichier.
- Deux modifications de réglage au boîtier, isolées et encadrées par 29 lectures, correspondent
  chacune à **un seul octet hors banque** : **840** (→ 18 à 19:36:24) et **854** (→ 1 à 19:38:40).
- La formule de somme de contrôle en vigueur **échoue sur les 29 lectures les plus récentes**
  (écart 52) ; l'ajout de 3769–3771 à l'ensemble exclu, avec le biais 0xE257, la rend correcte sur
  les 38 lectures.
- Le fichier généré `MON-SET-C1-C7.dat` et la relecture faite après restauration sont **identiques
  octet pour octet**.

### Hypothèses à tester, présentées comme telles
- 840 = ISO global, 854 = réglage N&B — **subordonné** à la confirmation de ce qui a été modifié.
- rel +37…+46 = table liée à la sensibilité (groupes de 3 = AUTO1/2/3) — raisonnement de forme, non
  mesuré.
- rel −32…−9 (24 octets à 9) = décalage de balance des blancs par banque, 9 = neutre — cohérent,
  non démontré, et contredirait le doc 10 s'il se vérifiait.
- L'exclusion de la somme porte sur un bloc « objectif » plus large que trois octets — indistinguable
  avec les données actuelles.

---

## Annexe A — Diffs des paires impliquant un fichier généré par le PC

Ces écarts reflètent nos écritures, pas l'appareil. Résumé par paire :

| Paire | Octets modifiés | Répartition |
|---|---|---|
| 15:40 → `A-RESTAURER-C1-CLAUDE-TEST` | 13 | 11 dans le nom de C1 (rel +78…+88) + 176/177 |
| `A-RESTAURER…` → `FUJIFILM_…1624` | 15 | retour du nom de C1 + 176/177 + 248 + 3772 |
| `FUJIFILM_…1644` → `CLAUDE-TEST-C1` | 13 | 11 dans le nom de C1 + 176/177 |
| `CLAUDE-TEST-C1` → `PACK-C1-C5` | 135 | C1…C5 : champs cartographiés + noms, + 176/177. **Rien dans C6, C7, ni hors banque** |
| `PACK-C1-C5` → `MON-SET-C1-C7` | 174 | C1…C7 : champs cartographiés + noms, + 176/177. **Rien hors banque** |
| `MON-SET-C1-C7` → lecture 17:45 | **0** | identité parfaite |

Aucune de ces paires ne touche un octet non cartographié à l'intérieur d'une banque, ni un octet
hors banque autre que 176/177 — ce qui reconfirme le périmètre d'écriture de `PrepareMany`.

## Annexe B — Reproduire cette analyse

Les mesures proviennent de scripts PowerShell de lecture seule (`[System.IO.File]::ReadAllBytes`),
sans aucune dépendance USB. Ils sont conservés dans le répertoire temporaire de la session
(`bankdiff.ps1`, `bankmap.ps1`, `recent.ps1`, `mirror.ps1`) et se réduisent à trois opérations :
comparaison octet par octet de deux tableaux, projection d'un offset absolu en (banque, offset
relatif) via `base = 3882 + slot × 256`, et calcul de la somme avec un ensemble d'exclusion
paramétrable.
