# Vérification documentaire — notice officielle Fujifilm X-T30 (1re génération)

Date : 2026-09-02
Source unique : manuel en ligne officiel FUJIFILM X-T30 — https://fujifilm-dsc.com/en/manual/x-t30/
(version anglaise ; **pas** le X-T30 II, qui a son propre manuel.)

Pages consultées et accessibles (HTTP 200) :

| Page | URL |
|---|---|
| IMAGE QUALITY SETTING | https://fujifilm-dsc.com/en/manual/x-t30/menu_shooting/image_quality_setting/index.html |
| MOVIE SETTING | https://fujifilm-dsc.com/en/manual/x-t30/menu_shooting/movie_setting/index.html |
| SHOOTING SETTING | https://fujifilm-dsc.com/en/manual/x-t30/menu_shooting/shooting_setting/index.html |
| USER SETTING | https://fujifilm-dsc.com/en/manual/x-t30/menu_setup/user_setting/index.html |
| BUTTON/DIAL SETTING | https://fujifilm-dsc.com/en/manual/x-t30/menu_setup/button-dial_setting/index.html |
| Menu List | https://fujifilm-dsc.com/en/manual/x-t30/introduction/menu_list/index.html |
| Specifications | https://fujifilm-dsc.com/en/manual/x-t30/technical_notes/spec/index.html |
| Recording Movies | https://fujifilm-dsc.com/en/manual/x-t30/basic_movie/movie_rec/index.html |
| HDMI Output | https://fujifilm-dsc.com/en/manual/x-t30/connections/hdmi_output/index.html |

Aucune page nécessaire n'était inaccessible. Le manuel du X-T30 est **monopage par menu**
(ancres `#film_simulation`, `#dynamic_range`, `#d_range_priority`, `#highlight_tone`,
`#shadow_tone`, `#color`, `#sharpness`, `#noise_reduction`, `#grain_effect`,
`#color_crome_effect`, `#white_balance`, `#color_temperature`, `#b-w_adj`,
`#select_custom_setting`, `#edit-save_custom_setting`) : il n'existe pas de sous-page
dédiée par réglage.

Aucune valeur de ce document n'est déduite ni extrapolée. Quand le manuel est muet,
c'est écrit **NON TROUVÉ** et le verdict est **NON DOCUMENTÉ**.

---

## Récapitulatif

| # | Point vérifié | Verdict |
|---|---|---|
| C01 | Liste des simulations de film | CONFORME |
| C02 | Modes de balance des blancs nommés | DIVERGENT |
| C03 | Liste des températures Kelvin sélectionnables | NON DOCUMENTÉ |
| C04 | Valeurs de plage dynamique en photo | DIVERGENT |
| C05 | Contraintes ISO de la plage dynamique en photo | DIVERGENT |
| C06 | Valeurs de plage dynamique en vidéo | CONFORME |
| C07 | Contraintes ISO de la plage dynamique en vidéo | DIVERGENT |
| C08 | Options de priorité plage D | DIVERGENT |
| C09 | Plage du ton lumière (HIGHLIGHT TONE) | DIVERGENT |
| C10 | Plage du ton ombre (SHADOW TONE) | DIVERGENT |
| C11 | Plage couleur (COLOR) | CONFORME |
| C12 | Plage netteté (SHARPNESS) | CONFORME |
| C13 | Plage réduction du bruit (NOISE REDUCTION) | CONFORME |
| C14 | Effet de grain (GRAIN EFFECT) | CONFORME |
| C15 | Color Chrome Effect | CONFORME |
| C16 | Plage du réglage N&B (B & W ADJ.) | DIVERGENT |
| C17 | Plage du décalage de balance des blancs | NON DOCUMENTÉ |
| C18 | Modes vidéo (résolutions et cadences) | DIVERGENT |
| C19 | F-Log : options et enregistrement sur la carte | CONFORME |
| C20 | F-Log : restriction ISO 640–12800 | DIVERGENT |
| C21 | INTERFRAME NR : options | DIVERGENT |
| C22 | Les banques C1–C7 ne s'appliquent pas à la vidéo | CONFORME |
| C23 | Limite de caractères du nom de banque | NON DOCUMENTÉ |
| C24 | Contenu et ordre des réglages d'une banque | CONFORME |
| C25 | Absence de grain / Color Chrome dans le menu vidéo | CONFORME |
| C26 | Réglages absents du X-T30 (Clarity, CC FX Blue, Grain Size) | CONFORME |

**12 conformes · 11 divergents · 3 non documentés (26 points).**

---

## 1. Simulations de film

### C01 — Liste des simulations

**Ce que dit le manuel**
(IMAGE QUALITY SETTING > FILM SIMULATION —
https://fujifilm-dsc.com/en/manual/x-t30/menu_shooting/image_quality_setting/index.html#film_simulation)

Dix entrées de menu :
`PROVIA/STANDARD`, `Velvia/VIVID`, `ASTIA/SOFT`, `CLASSIC CHROME`, `PRO Neg. Hi`,
`PRO Neg. Std`, `ETERNA/CINEMA`, `ACROS`, `MONOCHROME`, `SEPIA`.

Note du manuel, textuelle : « \* Available with yellow (Ye), red (R), and green (G)
filters ». L'astérisque porte sur ACROS et MONOCHROME uniquement. Cela donne
**16 combinaisons sélectionnables** au total.

Le manuel ne mentionne **nulle part** Classic Negative, Nostalgic Neg., Bleach Bypass
ni Reala Ace pour ce boîtier.

**Ce que dit notre code**
`CameraBankFile.FilmSimulations` (Models/CameraBankFile.cs:61) propose exactement 16 entrées :
Provia/Standard, Astia/Soft, Velvia/Vivid, Classic Chrome, PRO Neg. Std, PRO Neg. Hi,
Eterna, ACROS, ACROS+R, ACROS+Ye, ACROS+G, Monochrome, Monochrome+R, Monochrome+Ye,
Monochrome+G, Sepia.

`Recipe.CompatibilityIssues()` (Models/Library.cs:82) rejette explicitement
Classic Negative, Bleach Bypass, Nostalgic Neg. et Reala — cohérent avec le manuel.

**VERDICT = CONFORME.** Aucune simulation manquante, aucune proposée à tort.
(Remarque de forme, sans incidence : le manuel écrit « ETERNA/CINEMA » ; nous affichons
« Eterna ». `FilmSim()` accepte les deux orthographes.)

---

## 2. Balance des blancs

### C02 — Modes nommés

**Ce que dit le manuel**
IMAGE QUALITY SETTING > WHITE BALANCE
(…/menu_shooting/image_quality_setting/index.html#white_balance) et surtout la fiche
technique (…/technical_notes/spec/index.html), qui donne la liste sans pictogrammes :

> « Auto, Custom 1, Custom 2, Custom 3, color temperature selection, direct sunlight,
> shade, daylight fluorescent, warm white fluorescent, cool white fluorescent,
> incandescent, underwater »

Soit : AUTO · 3 mesures personnalisées · sélection de température · lumière directe ·
ombre · 3 fluorescents · incandescent · sous-marin.

**Ce que dit notre code**
`CameraBankFile.WhiteBalances()` (Models/CameraBankFile.cs:77) :
Auto, Daylight, Shade, Fluorescent 1, Fluorescent 2, Fluorescent 3, Incandescent,
Underwater, puis les 31 valeurs Kelvin.
`WhiteBalance()` (ligne 188) code : Auto=0, Daylight=1, Shade=2, Fluo1..3=3..5,
Incandescent=6, Underwater=7, Kelvin=8.

**VERDICT = DIVERGENT.** Les trois balances mesurées **CUSTOM 1 / 2 / 3** du manuel
n'existent ni dans la liste proposée ni dans l'encodeur. Elles sont pourtant
sélectionnables sur le boîtier et donc représentables dans une banque : une banque lue
qui les utilise ne pourra pas être reproduite par l'application.
Les huit modes que nous proposons correspondent bien, un pour un, à des modes réels
(« Daylight » = *direct sunlight* du manuel).

### C03 — Températures de couleur sélectionnables

**Ce que dit le manuel**
La section « k : Color Temperature »
(…/menu_shooting/image_quality_setting/index.html#color_temperature) explique la
manœuvre — « Selecting k in the white balance menu displays a list of color
temperatures » — mais **n'énumère aucune valeur**. La fiche technique dit seulement
« color temperature selection ». Ni borne basse, ni borne haute, ni pas.

→ **NON TROUVÉ** dans le manuel.

**Ce que dit notre code**
`CameraBankFile.KelvinPresets` / `KelvinAsc` (lignes 34 et 72) : 31 valeurs de 2500 K à
10000 K, non linéaires (2500, 2550, 2650, 2700, 2800, 2850, 2950, 3000, 3100, 3200,
3300, 3400, 3600, 3700, 3800, 4000, 4200, 4300, 4500, 4800, 5000, 5300, 5600, 5900,
6300, 6700, 7100, 7700, 8300, 9100, 10000). `Kelvin()` (ligne 208) n'accepte que ces
valeurs exactes et renvoie -1 sinon.

**VERDICT = NON DOCUMENTÉ.** Notre liste ne peut être ni confirmée ni infirmée par le
manuel. Elle provient de la mesure directe sur le boîtier (docs/10-piste-backup-c1c7.md)
et reste la seule source ; à conserver telle quelle, mais sans prétendre qu'elle est
« conforme à la notice ».

### C17 — Décalage de balance des blancs (WB Shift)

**Ce que dit le manuel**
La section « Fine-Tuning White Balance »
(…/menu_shooting/image_quality_setting/index.html#white_balance) décrit uniquement le
geste : « Pressing MENU/OK after selecting a white balance option displays a fine-tuning
dialog; use the focus stick (focus lever) to fine-tune white balance. »
**Aucune amplitude, aucun axe, aucun pas n'est indiqué.** → **NON TROUVÉ**.

**Ce que dit notre code**
`RecipeFields.Choices` (Presentation/VideoStudio.cs:33) propose `Scale(-9, 9)` pour
« WB Shift R » et « WB Shift B ». Le décalage n'est de toute façon pas transférable :
`IsTransferable()` le refuse et `BuildBankName()` (CameraBankFile.cs:225) le reporte
dans le nom de la banque.

**VERDICT = NON DOCUMENTÉ.**

---

## 3. Plage dynamique et priorité plage D

### C04 / C05 — En photo

**Ce que dit le manuel**
IMAGE QUALITY SETTING > DYNAMIC RANGE
(…/menu_shooting/image_quality_setting/index.html#dynamic_range) :

> Options : `AUTO`, `100%`, `200%`, `400%`
> « If AUTO is selected, the camera will automatically choose either 100% or 200%
> according to the subject and shooting conditions. »
> « 200% is available at sensitivities of from ISO 320 to ISO 12800, 400% at
> sensitivities of from ISO 640 to 12800. »

**Ce que dit notre code**
`CameraBankFile.DynamicRanges` (ligne 67) = { DR100, DR200, DR400 }. `DynamicRange()`
(ligne 171) code 100→1, 200→2, 400→3 et refuse tout le reste.
**Aucune vérification ISO nulle part** : le champ « ISO » est du texte libre
(`RecipeFields.Choices` renvoie `null`, VideoStudio.cs:36) et aucune règle ne relie ISO
et plage dynamique. `CompatibilityIssues()` (Library.cs:70) ne teste pas ce couple.

**VERDICT C04 = DIVERGENT** — l'option `AUTO`, documentée et sélectionnable dans une
banque, n'est pas proposée par l'application (et n'a pas de code vérifié dans
`DynamicRange()`).

**VERDICT C05 = DIVERGENT** — les seuils ISO 320 (DR200) et ISO 640 (DR400) sont
explicitement documentés et ne sont **pas** appliqués. Une recette DR400 à ISO 160
est acceptée sans avertissement alors que le boîtier la refusera.

### C06 / C07 — En vidéo

**Ce que dit le manuel**
MOVIE SETTING > DYNAMIC RANGE
(…/menu_shooting/movie_setting/index.html), textuellement :

> Options : `100%`, `200%`, `400%`
> « Auto dynamic range adjustment (AUTO) is not supported. »
> « 200% is available at sensitivities of from ISO 320 to ISO 12800, 400% at
> sensitivities of from ISO 640 to 12800. »
> « The MOVIE SETTING > DYNAMIC RANGE option is available when OFF is selected for
> MOVIE SETTING > F-Log RECORDING. »

Le manuel **précise donc bien** les contraintes ISO en vidéo, à l'identique de la photo,
et ajoute deux règles propres à la vidéo : pas d'AUTO, et indisponibilité si F-Log est ON.

**Ce que dit notre code**
`RecipeFields.MovieDynamicRanges` (VideoStudio.cs:18) = { DR100, DR200, DR400 }, sans
AUTO, avec le commentaire « Le mode film n'offre pas la priorité plage dynamique ».
Aucune contrainte ISO, aucune règle liant F-Log et plage dynamique.

**VERDICT C06 = CONFORME** — l'absence d'AUTO en vidéo est correcte et correspond
exactement à la note du manuel.

**VERDICT C07 = DIVERGENT** — les seuils ISO 320 / 640 en vidéo ne sont pas appliqués,
et la règle « plage dynamique indisponible quand F-Log est ON » n'est pas non plus
représentée alors que l'application propose les deux champs côte à côte.

### C08 — Priorité plage D

**Ce que dit le manuel**
IMAGE QUALITY SETTING > D RANGE PRIORITY
(…/menu_shooting/image_quality_setting/index.html#d_range_priority) :

> Options : `AUTO`, `STRONG`, `WEAK`, `OFF`
> « WEAK is available at sensitivities of from ISO 320 to ISO 12800, STRONG at
> sensitivities of from ISO 640 to 12800. »
> « When an option other than OFF is selected, HIGHLIGHT TONE, SHADOW TONE, and
> DYNAMIC RANGE will be adjusted automatically; if you wish to adjust these settings
> manually, choose OFF. »

Ce réglage n'apparaît **pas** dans le menu MOVIE SETTING (voir Menu List) : il est
photo seulement.

**Ce que dit notre code**
`CameraBankFile.DrPriorities` (ligne 68) = { "Off", "Auto" }.
`DrPriority()` (ligne 182) : AUTO→0, OFF→3, tout le reste →-1, avec le commentaire
« Seuls deux codes ont été observés sur ce boîtier ».
`PatchBank()` (ligne 376) applique correctement la règle du manuel : la plage dynamique
n'est écrite que si la priorité vaut explicitement Off.

**VERDICT = DIVERGENT.** Le manuel documente **quatre** options ; nous n'en proposons
que deux. `STRONG` et `WEAK` manquent. Le comportement conservateur du code (refus
d'écrire un octet deviné) est sain, mais l'utilisateur ne peut pas exprimer deux
réglages sur quatre. La logique « priorité ≠ Off ⇒ ton lumière / ton ombre / plage
dynamique pilotés automatiquement » est en revanche correctement implémentée.

---

## 4. Plages de réglage

Toutes les plages photo proviennent de
https://fujifilm-dsc.com/en/manual/x-t30/menu_shooting/image_quality_setting/index.html
(ancres respectives), toutes les plages vidéo de
https://fujifilm-dsc.com/en/manual/x-t30/menu_shooting/movie_setting/index.html.
Les deux menus donnent les **mêmes** amplitudes.

| Réglage | Manuel (photo et vidéo) | Notre code | Verdict |
|---|---|---|---|
| HIGHLIGHT TONE | `+4 +3 +2 +1 0 −1 −2` (7 valeurs) | `Scale(-4,4)` → 9 valeurs ; `Tone()` accepte -4..+4 | **DIVERGENT** |
| SHADOW TONE | `+4 +3 +2 +1 0 −1 −2` (7 valeurs) | idem | **DIVERGENT** |
| COLOR | `+4 … −4` (9 valeurs) | `Scale(-4,4)` ; `Color()` code les 9 | CONFORME |
| SHARPNESS | `+4 … −4` (9 valeurs) | `Scale(-4,4)` ; `Tone()` -4..+4 | CONFORME |
| NOISE REDUCTION | `+4 … −4` (9 valeurs) | `Scale(-4,4)` ; `NoiseReduction()` -4..+4 | CONFORME |
| GRAIN EFFECT | `STRONG` / `WEAK` / `OFF` | `GrainEffects` = Off/Weak/Strong | CONFORME |
| COLOR CHROME EFFECT | `STRONG` / `WEAK` / `OFF` | `ChromeEffects` = Off/Weak/Strong | CONFORME |
| B & W ADJ. (Warm/Cool) | `+9 … +1 / 0 / −1 … −9` (19 valeurs) | `Scale(-4,4)` pour « Monochromatic Color » | **DIVERGENT** |
| Décalage WB | non documenté | `Scale(-9,9)` | NON DOCUMENTÉ (voir C17) |

### C09 / C10 — Ton lumière et ton ombre : détail

**Ce que dit le manuel** — les deux tableaux HIGHLIGHT TONE et SHADOW TONE listent
**sept** valeurs et s'arrêtent à −2. Il n'y a ni −3 ni −4. Le menu MOVIE SETTING
répète exactement les mêmes sept valeurs.

**Ce que dit notre code** — `RecipeFields.Choices` (VideoStudio.cs:34) traite
« Highlight », « Shadow », « Color », « Sharpness » et « Noise Reduction » d'un seul
bloc avec `CameraBankFile.Scale(-4, 4)`. Côté encodage, `Tone()` (CameraBankFile.cs:135)
sert à la fois au ton lumière, au ton ombre **et** à la netteté, avec la même borne
-4..+4 et la formule `4 - v`.

**VERDICT = DIVERGENT** pour C09 et C10. L'application propose −3 et −4 pour le ton
lumière et le ton ombre : ces valeurs n'existent pas sur le X-T30. Elles produiraient
les codes 7 et 8 là où le boîtier n'utilise que 0..6. Pour la netteté, en revanche,
−4..+4 est exact : c'est le partage de `Tone()` entre trois réglages d'amplitudes
différentes qui est en cause, pas la formule.

### C16 — Réglage N&B (« Monochromatic Color »)

**Ce que dit le manuel** — B & W ADJ. (Warm/Cool), tableau identique en photo et en
vidéo : `+9 — +1` (cast rouge croissant), `0` (gris neutre), `-1 — -9` (cast bleu).
Soit −9 à +9.

**Ce que dit notre code** — « Monochromatic Color » figure dans `ParameterOrder` et
`VideoParameterOrder` (Library.cs:52 et 63), mais `RecipeFields.Choices` lui applique
`Scale(-4, 4)` (VideoStudio.cs:35).

**VERDICT = DIVERGENT.** L'amplitude proposée est −4..+4 au lieu de −9..+9 : les deux
tiers de la plage réelle sont inaccessibles. À noter que ce champ n'est de toute façon
pas transférable (`IsTransferable()` le refuse et `PatchBank()` le signale comme « la
banque le retient, mais sa position dans le fichier n'est pas identifiée ») — le
commentaire du code est ici exact, la notice confirme bien que B & W ADJ. fait partie
des réglages mémorisés par une banque.

---

## 5. Modes vidéo et F-Log

### C18 — Résolutions et cadences

**Ce que dit le manuel**
MOVIE SETTING > MOVIE MODE (…/menu_shooting/movie_setting/index.html) :

> Taille et rapport d'image : `4K 16:9`, `4K 17:9`, `Full HD 16:9`, `Full HD 17:9`
> Cadences : `59.94P`, `50P`, `29.97P`, `25P`, `24P`, `23.98P`
> Débits : `200Mbps`, `100Mbps`, `50Mbps`
> « The choice of frame and bit rates varies with the movie mode. » (la matrice exacte
> n'est pas donnée sur cette page)

La fiche technique (…/technical_notes/spec/index.html) lève l'ambiguïté par une note :
`59.94P` et `50P` sont marqués d'un astérisque renvoyant à « Full HD only ». Donc
4K : 29.97P / 25P / 24P / 23.98P — Full HD : les six cadences.
Format d'enregistrement : « H.264 : SD card, 4:2:0, 8-bit / HDMI output, 4:2:2, 10-bit ».

MOVIE SETTING > FULL HD HIGH SPEED REC, options documentées :
`2x 59.94P→120P`, `2x 50P→100P`, `4x 29.97P→120P`, `4x 25P→100P`, `5x 24P→120P`,
`5x 23.98P→120P`, `OFF` — 6 minutes maximum, sans son.

**Ce que dit notre code**
`RecipeFields.MovieModes` (Presentation/VideoStudio.cs:13) :
4K 30P, 4K 25P, 4K 24P, FHD 60P, FHD 50P, FHD 30P, FHD 25P, FHD 24P,
FHD 120P (high speed).

**VERDICT = DIVERGENT.** Le socle est juste (aucun mode inexistant n'est proposé : pas
de 4K 60P, ce qui est correct pour le X-T30), mais trois éléments documentés manquent :
1. la distinction `24P` / `23.98P`, qui sont deux entrées de menu séparées dans le
   manuel — nous n'offrons qu'un seul « 24P » par résolution ;
2. les variantes de rapport d'image `17:9` (DCI) pour la 4K comme pour la Full HD —
   nous ne proposons que l'équivalent 16:9 ;
3. côté haute vitesse, les six combinaisons documentées (dont les modes `100P`) sont
   réduites à un unique « FHD 120P (high speed) ».
Le débit (200/100/50 Mbps), documenté et réglable, n'est pas non plus un champ de
recette.

### C19 / C20 — F-Log

**Ce que dit le manuel**
MOVIE SETTING > F-Log RECORDING (…/menu_shooting/movie_setting/index.html) :

> « Select ON to record movies using a soft gamma curve with a wide gamut suitable for
> further processing post-production. Sensitivity is restricted to values between
> ISO 640 and 12800. »
> Options : `ON` / `OFF`

Point important pour la question posée : **le manuel du X-T30 ne propose que ON/OFF**,
sans le choix « F-Log SD CARD / F-Log HDMI » qu'on trouve sur d'autres boîtiers. Le
réglage se trouve dans le menu d'enregistrement vidéo et s'applique donc aux films
enregistrés ; la note de DYNAMIC RANGE (« available when OFF is selected for F-Log
RECORDING ») le confirme indirectement, puisqu'elle porte sur le traitement interne de
l'image. La page HDMI Output (…/connections/hdmi_output/index.html) ne mentionne pas
F-Log du tout, et rien n'indique une restriction à la sortie HDMI.
Réserve honnête : le manuel n'emploie **jamais littéralement** les mots « carte
mémoire » à propos de F-Log.

**Ce que dit notre code**
`RecipeFields.LogModes` (VideoStudio.cs:14) = { "Off", "F-Log" }. Aucune contrainte ISO.

**VERDICT C19 = CONFORME.** Deux options, comme le manuel. Il n'existe pas de variante
HDMI à proposer sur ce boîtier, et rien dans le manuel ne restreint F-Log à la sortie
HDMI : l'enregistrer sur la carte est bien le comportement documenté du réglage.

**VERDICT C20 = DIVERGENT.** La restriction « ISO 640 à 12800 quand F-Log est ON » est
explicitement documentée et n'est appliquée nulle part : une recette vidéo F-Log à
ISO 160 est acceptée sans avertissement.

### C21 — INTERFRAME NR

**Ce que dit le manuel**
MOVIE SETTING > INTERFRAME NR :

> Options : `ON` / `OFF`
> « Interframe noise reduction is available only when frame rates of 29.97P or slower
> are selected at a frame size of [4K 16:9] or [4K 17:9]. »

**Ce que dit notre code**
`RecipeFields.InterframeNr` (VideoStudio.cs:16) = { "Off", "Weak", "Strong" }.

**VERDICT = DIVERGENT.** Le manuel documente un interrupteur binaire ; nous proposons
trois niveaux, dont deux (« Weak », « Strong ») n'existent pas dans le menu du boîtier.
De plus, la condition de disponibilité (4K, 29.97P ou moins) n'est pas représentée —
notre valeur par défaut est « Off », ce qui limite les dégâts, mais une recette
« FHD 60P + Interframe NR Weak » est acceptable dans l'application alors qu'elle est
impossible sur le boîtier.

### C25 — Absence de grain et de Color Chrome dans le menu vidéo

**Ce que dit le manuel**
Le menu MOVIE SETTING (page dédiée et Menu List, …/introduction/menu_list/index.html)
contient FILM SIMULATION, B & W ADJ., WHITE BALANCE, DYNAMIC RANGE, HIGHLIGHT TONE,
SHADOW TONE, COLOR, SHARPNESS, NOISE REDUCTION, INTERFRAME NR, F-Log RECORDING — et
**ni GRAIN EFFECT ni COLOR CHROME EFFECT**, qui n'apparaissent que dans IMAGE QUALITY
SETTING.

**Ce que dit notre code**
`Recipe.VideoParameterOrder` (Library.cs:62) omet effectivement le grain et le Color
Chrome, avec un commentaire (lignes 56-61) qui énumère le menu MOVIE SETTING.

**VERDICT = CONFORME.** Le commentaire du code décrit exactement le menu du manuel.
Deux écarts d'ordre seulement, sans conséquence fonctionnelle : nous plaçons « F-Log »
en deuxième position alors que le manuel le met en dernier, et nous insérons « ISO »
et « WB Shift R/B » qui ne sont pas des entrées du menu MOVIE SETTING (l'ISO est dans
SHOOTING SETTING mais s'applique bien à la vidéo — fiche technique : « Movies :
ISO 160 – 12800 »). « FULL HD HIGH SPEED REC » est absent de notre liste.

---

## 6. Les banques C1–C7 et la vidéo

### C22 — Confirmation

**Ce que dit le manuel**

1. `SELECT CUSTOM SETTING` et `EDIT/SAVE CUSTOM SETTING` figurent **uniquement** sous
   l'onglet IMAGE QUALITY SETTING — ni dans MOVIE SETTING, ni ailleurs
   (Menu List, …/introduction/menu_list/index.html ;
   …/menu_shooting/image_quality_setting/index.html#select_custom_setting).
2. La liste des réglages qu'une banque mémorise (EDIT/SAVE CUSTOM SETTING) est
   exclusivement photo : ISO, DYNAMIC RANGE, D RANGE PRIORITY, FILM SIMULATION,
   B & W ADJ., GRAIN EFFECT, COLOR CHROME EFFECT, WHITE BALANCE, HIGHLIGHT TONE,
   SHADOW TONE, COLOR, SHARPNESS, NOISE REDUCTION. Aucun réglage vidéo.
3. La preuve la plus nette est dans BUTTON/DIAL SETTING > EDIT/SAVE QUICK MENU
   (…/menu_setup/button-dial_setting/index.html), où chaque option est marquée d'un
   astérisque si elle est « Stored in custom settings bank ». Sont marqués :
   FILM SIMULATION, B & W ADJ., GRAIN EFFECT, COLOR CHROME EFFECT, DYNAMIC RANGE,
   D RANGE PRIORITY, WHITE BALANCE, HIGHLIGHT TONE, SHADOW TONE, COLOR, SHARPNESS,
   NOISE REDUCTION, SELECT CUSTOM SETTING, ISO. **Ne sont pas marqués** : `MOVIE MODE`
   et `FULL HD HIGH SPEED REC`.
4. Le menu MOVIE SETTING duplique tous les réglages d'image sous un préfixe distinct
   (F FILM SIMULATION, F WHITE BALANCE, F DYNAMIC RANGE…), ce qui est la manière dont
   le boîtier tient deux jeux séparés.

Réserve : le manuel ne contient **aucune phrase disant explicitement** « les banques
C1–C7 ne s'appliquent pas à la vidéo ». La conclusion repose sur les quatre faits
ci-dessus, tous vérifiables et cités.

**Ce que dit notre code**
Le commentaire de `Recipe.Kind` (Library.cs:17-19) : « Le X-T30 ne range AUCUN réglage
vidéo dans son fichier de sauvegarde ni dans C1-C7 : ces recettes se reportent à la main
dans les menus du boîtier, et ne sont jamais écrites. »
`BuildPacks()` (Library.cs:353) refuse d'ailleurs de mettre une recette vidéo dans une
banque.

**VERDICT = CONFORME (corroboré, non énoncé).** L'affirmation est cohérente avec tout
ce que la notice documente et n'est contredite nulle part. Formulation à privilégier
dans l'interface : « le menu du X-T30 ne permet d'enregistrer dans une banque que des
réglages photo ; les réglages film ont leur propre menu » — vérifiable, plutôt qu'une
généralité invérifiable.

### C24 — Contenu et ordre des réglages d'une banque

**Ce que dit le manuel**
EDIT/SAVE CUSTOM SETTING, dans cet ordre exact
(…/menu_shooting/image_quality_setting/index.html#edit-save_custom_setting) :
ISO, DYNAMIC RANGE, D RANGE PRIORITY, FILM SIMULATION, B & W ADJ., GRAIN EFFECT,
COLOR CHROME EFFECT, WHITE BALANCE, HIGHLIGHT TONE, SHADOW TONE, COLOR, SHARPNESS,
NOISE REDUCTION.
Sept banques : CUSTOM 1 à CUSTOM 7. Une banque peut être remise à zéro (`RESET`) et
les banques survivent à `SHOOTING MENU RESET`
(…/menu_setup/user_setting/index.html : « Reset all shooting menu settings other than
custom white balance and custom settings banks created using EDIT/SAVE CUSTOM SETTING »).

**Ce que dit notre code**
`Recipe.ParameterOrder` (Library.cs:50) : ISO, Dynamic Range, Dynamic Range Priority,
Film Simulation, Monochromatic Color, Grain Effect, Color Chrome Effect, White Balance,
**WB Shift R, WB Shift B**, Highlight, Shadow, Color, Sharpness, Noise Reduction.
`CameraBankFile.Slots = 7`.

**VERDICT = CONFORME.** L'ordre reproduit fidèlement celui de la notice, avec la seule
insertion de « WB Shift R/B » après la balance des blancs — un ajout assumé et
explicitement traité comme non transférable par `IsTransferable()`. Le nombre de banques
(7) est correct.

### C26 — Réglages absents du X-T30

**Ce que dit le manuel** — ni `CLARITY`, ni `COLOR CHROME FX BLUE`, ni `GRAIN SIZE`
n'apparaissent dans IMAGE QUALITY SETTING, dans MOVIE SETTING ou dans la Menu List du
X-T30. Le tableau GRAIN EFFECT n'offre que STRONG / WEAK / OFF, sans réglage de taille.

**Ce que dit notre code** — `Recipe.AdditionalParameters` (Library.cs:69) liste
exactement ces trois réglages, et `CompatibilityIssues()` (ligne 86) signale une recette
importée qui les utilise avec une valeur autre que Off/0/none.

**VERDICT = CONFORME.**

---

## 7. Nom d'une banque personnalisée

### C23 — Limite de caractères

**Ce que dit le manuel**
Une seule mention dans tout le manuel, à la fin de EDIT/SAVE CUSTOM SETTING
(…/menu_shooting/image_quality_setting/index.html#edit-save_custom_setting) :

> « Banks can be renamed using EDIT CUSTOM NAME. »

Il n'existe **aucune section EDIT CUSTOM NAME** dans ce manuel : l'entrée n'apparaît ni
dans la Menu List, ni dans SET UP > USER SETTING, ni dans SAVE DATA SETTING, ni dans
BUTTON/DIAL SETTING. Recherche menée sur l'ensemble des pages téléchargées : une seule
occurrence, celle citée ci-dessus. **Aucune limite de caractères, aucun jeu de
caractères autorisé, aucune capture d'écran du clavier de saisie.** → **NON TROUVÉ**.

**Ce que dit notre code**
`CameraBankFile.NameMax = 25` (ligne 25), utilisé par `BuildBankName()` (ligne 225) pour
tronquer et par `PatchBank()` (ligne 387) pour effacer puis réécrire `NameMax + 1`
octets ASCII à l'offset relatif 78.

**VERDICT = NON DOCUMENTÉ.** La valeur 25 ne peut pas être confirmée par le manuel.
Elle provient de la structure mesurée du fichier de sauvegarde (docs/10-piste-backup-c1c7.md).
Le filtrage ASCII 0x20–0x7E appliqué par le code n'est lui non plus adossé à aucune
source documentaire.

---

## Conclusion

Le noyau photo de l'application est solide : la liste des simulations de film est exacte
au caractère près, l'ordre des réglages d'une banque reproduit celui de la notice, les
plages couleur / netteté / réduction du bruit sont justes, les réglages absents du
boîtier (Clarity, Color Chrome FX Blue, Grain Size, Classic Negative…) sont correctement
refusés, et l'affirmation sur la séparation photo / vidéo des banques est confirmée par
quatre indices convergents du manuel.

Les onze divergences relevées se répartissent en trois familles :

1. **Listes de valeurs incomplètes** (C02, C04, C08, C18, C21) — des options réellement
   présentes dans les menus du boîtier ne sont pas proposées : les trois balances des
   blancs mesurées, la plage dynamique AUTO, les priorités STRONG et WEAK, les cadences
   23.98P / les rapports 17:9 / les modes 100P. Symétriquement, INTERFRAME NR se voit
   offrir deux niveaux qui n'existent pas.
2. **Amplitudes fausses** (C09, C10, C16) — le ton lumière et le ton ombre s'arrêtent à
   −2 sur ce boîtier, pas à −4 ; le réglage N&B va de −9 à +9, pas de −4 à +4. Ces trois
   erreurs viennent du même endroit : un unique `Scale(-4, 4)` appliqué en bloc à des
   réglages d'amplitudes différentes, et un unique `Tone()` partagé entre ton lumière,
   ton ombre et netteté.
3. **Contraintes ISO documentées mais non appliquées** (C05, C07, C20) — DR200 exige
   ISO ≥ 320, DR400 ISO ≥ 640 (en photo comme en vidéo), F-Log impose ISO 640–12800.
   Le manuel est parfaitement explicite sur ces trois seuils.

Les trois points non documentés (liste Kelvin, amplitude du décalage de balance des
blancs, longueur du nom de banque) restent adossés à la mesure directe sur le boîtier ;
rien dans le manuel ne les contredit, mais rien ne les confirme non plus, et ils ne
doivent pas être présentés comme « conformes à la notice ».
