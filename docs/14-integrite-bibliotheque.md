# 14 — Intégrité de la bibliothèque de recettes (`xt30-probe/library`)

**Date de l'audit :** 2026-09-02, ~19h25–19h36 (heure locale)
**Portée :** lecture seule. Aucun fichier de `library/` n'a été modifié, aucun accès appareil photo, aucun import réseau.

> **Avertissement — instantané mobile.** Une passe de téléchargement d'images écrivait encore dans `library/recipes/` pendant l'audit (dernier fichier écrit : `recipes/my-fujifilm-x-t30-analog-color-film-simulation-recipe/photo-3.jpg` à 19:36:26, soit à la seconde de la mesure). Les comptages de fichiers image sont donc un instantané et peuvent avoir évolué depuis. Les comptages issus des JSON d'index sont stables (aucun index n'a été réécrit après 19:23).
> Les 5 fichiers `index/*.json` ont tous été lus et parsés **sans erreur**. Aucun JSON illisible.

---

## 1. Inventaire par catalogue

| Fichier d'index | Site | Préfixe | Portée | Recettes | XT30_COMPATIBLE | XT30_PARTIAL | XT30_INCOMPATIBLE | UNVERIFIED |
|---|---|---|---|---:|---:|---:|---:|---:|
| `fuji_x_weekly_xt30_full.json` | Fuji X Weekly | `fxw-` | ALL_CATALOGS | 336 | 128 | 100 | 108 | 0 |
| `film_recipes_xt30.json` | Film Recipes | `flr-` | — | 202 | 13 | 189 | 0 | 0 |
| `filmsimrecipes_xt30.json` | Filmsim Recipes | `fsr-` | — | 66 | 0 | 66 | 0 | 0 |
| `fuji_x_weekly_editorial.json` | Fuji X Weekly | `fxw-` | CURATED_SELECTION | 31 | 3 | 15 | 13 | 0 |
| `fuji_x_weekly_selection.json` | Fuji X Weekly | `fxw-` | CURATED_SELECTION | 7 | 3 | 4 | 0 | 0 |
| **Total (entrées)** | | | | **642** | **147** | **374** | **121** | **0** |

Remarques :

- **Aucune entrée `UNVERIFIED`** : tous les enregistrements portent une valeur `compatibility.xt30Original` explicite. Aucune valeur hors nomenclature n'a été rencontrée.
- Les fichiers `.csv` présents dans `index/` (`fuji_x_weekly_selection.csv`, `fuji_x_weekly_xt30_full.csv`) sont des exports parallèles et n'ont pas été comptés.
- **Recettes uniques : 611** (slugs distincts) pour 642 entrées. L'écart de 31 vient du recouvrement interne à Fuji X Weekly (voir §4).
- Répartition très déséquilibrée du verdict de compatibilité entre catalogues : `filmsimrecipes` classe **100 % de ses recettes en PARTIAL** et `film_recipes` **93,6 %**, alors que Fuji X Weekly full en classe 38 % COMPATIBLE et 32 % INCOMPATIBLE. Les heuristiques de classement ne sont donc pas homogènes d'un extracteur à l'autre — cette colonne n'est pas comparable entre catalogues en l'état.

---

## 2. Intégrité des références d'images

Vérification : chaque `images.cover` et chaque entrée de `images.examples` a été résolue en chemin relatif à `library/` (`recipes/<slug>/<fichier>`) et confrontée à la liste réelle des fichiers présents.

| Indicateur | Valeur |
|---|---:|
| Références d'images totales (cover + examples) | 1 376 |
| Références **cassées** (fichier absent) | **6** |
| Taux d'intégrité | 99,56 % |
| Recettes couvertes par un `cover` | 639 / 642 |
| Fichiers image réellement présents sous `library/recipes/` | ~3 009 (.jpg/.jpeg) |

### Les 6 références cassées (liste exhaustive — moins de 10 cas)

Toutes concernent **une seule recette**, `Old Gold Film Recipe` (catalogue *Film Recipes*), dont le dossier `library/recipes/old-gold-film-recipe/` est **vide** :

| # | Site | Type | Chemin référencé |
|---|---|---|---|
| 1 | Film Recipes | cover | `recipes/old-gold-film-recipe/photo-10.jpg` |
| 2 | Film Recipes | example | `recipes/old-gold-film-recipe/photo-10.jpg` |
| 3 | Film Recipes | example | `recipes/old-gold-film-recipe/photo-91.jpg` |
| 4 | Film Recipes | example | `recipes/old-gold-film-recipe/photo-93.jpg` |
| 5 | Film Recipes | example | `recipes/old-gold-film-recipe/photo-94.jpg` |
| 6 | Film Recipes | example | `recipes/old-gold-film-recipe/photo-95.jpg` |

Deux observations utiles :

- Le `cover` de cette recette pointe sur `photo-10.jpg`, qui est **aussi** listé dans `examples` — la même image est donc référencée deux fois. Ce motif (cover = première entrée d'examples) est courant dans le catalogue *Film Recipes*.
- La numérotation `photo-91` → `photo-95` suggère une extraction de galerie d'article où la numérotation source n'est pas contiguë ; le dossier étant vide, il est plausible que la passe de téléchargement ait échoué sur cet article (ou soit encore à traiter). **À revérifier une fois la passe terminée avant de conclure à une casse définitive.**

### Fichiers présents mais non référencés

Environ **1 400 fichiers image** présents sur disque ne sont référencés ni comme `cover` ni dans `examples` (répartis de façon diffuse : aucun dossier n'en concentre plus de 8). Le champ `images.all` existe dans le schéma mais est **vide pour les 642 entrées** — c'est vraisemblablement lui qui devait accueillir ces images. Ce n'est pas une corruption, mais ~493 Mo sont stockés pour ~45 % d'images effectivement exploitables par l'index.

---

## 3. Recettes sans aucune image

**3 recettes** n'ont ni `cover` ni `examples`. Toutes proviennent du catalogue *Film Recipes*, et dans les trois cas le dossier `library/recipes/<slug>/` **existe mais est vide** :

| Site | Nom | Slug |
|---|---|---|
| Film Recipes | Nightwalker, Street Film Recipe for Night Lights | `nightwalker-street-film-recipe-for-night-lights` |
| Film Recipes | 123 Chrome | `123-chrome-classic-kodachrome-look` |
| Film Recipes | Newsprint, Grainy Acros for Daily Life | `newsprint-grainy-acros-for-daily-life` |

Dossiers vides au total : **4** — les 3 ci-dessus plus `old-gold-film-recipe` (§2).

---

## 4. Doublons

### 4.1 Doublons internes à Fuji X Weekly (même site, plusieurs index)

**31 groupes** de recettes portent un nom strictement identique (normalisé : majuscules, ponctuation retirée) dans **deux fichiers d'index différents du même site** : `fuji_x_weekly_editorial.json` × `fuji_x_weekly_xt30_full.json`, et `fuji_x_weekly_selection.json` × `fuji_x_weekly_xt30_full.json`.

Dans **31 cas sur 31 les réglages sont rigoureusement identiques** sur les 19 champs `settings`. Ce ne sont pas des divergences de données mais un **recouvrement de périmètre** : les index « selection » et « editorial » sont des sous-ensembles ré-extraits du catalogue complet. C'est ce qui explique 642 entrées pour 611 slugs uniques.

Exemples : *Vivid Chrome — A Fujifilm Recipe for X and GFX Cameras*, *Kodak Vision3 250D v2*, *Kodak Portra 400 v2 (X-T5)*, *Kodak Vericolor VPS (X-E5)*, *Nostalgic Americana*, *Classic B&W Film Simulation Recipe*, *Kodak Portra 160 v2 (X-T5)*, *1970's Summer (X-T5)*.

**Un seul groupe présente une divergence** en nom strict inter-sites : *Kodak Portra 160*.

### 4.2 Doublons inter-sites (nom identique après normalisation stricte)

En comparant sur le nom normalisé exact, seuls **3 groupes** enjambent des sites différents — parce que Fuji X Weekly suffixe systématiquement ses titres (« — Fujifilm X-T5 (X-Trans V) Film Simulation Recipe ») là où les deux autres sites publient un titre nu.

**Les 3 sont divergents. Aucun n'est identique.**

| Recette | Sites | Champs divergents |
|---|---|---:|
| Kodak Portra 160 | Filmsim Recipes + Film Recipes + Fuji X Weekly | 15 / 19 |
| Reggie's Portra | Filmsim Recipes + Fuji X Weekly | 4 / 19 |
| Kodak Portra do Sol | Filmsim Recipes + Fuji X Weekly | 2 / 19 |

### 4.3 Doublons inter-sites après normalisation tolérante — les 15 cas les plus flagrants

Normalisation étendue : suppression des mentions de boîtier (`X-T5`, `X100V`, `GFX…`), de `X-Trans <gén.>`, et du boilerplate (« Film Simulation Recipe », « Fujifilm », « My… »). Appariement par recouvrement de jetons (Jaccard ≥ 0,6 ou inclusion stricte). **98 paires inter-sites** ressortent ainsi.

**Aucune de ces 98 paires n'a des réglages identiques.** Toutes divergent sur au moins 2 des 19 champs `settings`.

| # | Recette A (site) | Recette B (site) | Champs divergents |
|---:|---|---|---:|
| 1 | Kodak portra 160 *(Filmsim Recipes)* | Kodak Portra 160 *(Film Recipes)* | **14 / 19** — film sim Classic Chrome vs Eterna/Cinema |
| 2 | Kodak Portra 160 *(Film Recipes)* | My Fujifilm X-T30 Kodak Portra 160 *(Fuji X Weekly)* | **14 / 19** — Eterna/Cinema vs Classic Chrome |
| 3 | Kodak portra 160 *(Filmsim Recipes)* | My Fujifilm X-T30 Kodak Portra 160 *(Fuji X Weekly)* | 13 / 19 |
| 4 | Kodak portra 160 *(Filmsim Recipes)* | Kodak Portra 160 — X100V *(Fuji X Weekly)* | 11 / 19 |
| 5 | Kodak Portra 160 *(Film Recipes)* | Kodak Portra 160 — X100V *(Fuji X Weekly)* | 10 / 19 |
| 6 | Kodak Portra 160 *(Film Recipes)* | [Not] My Fujifilm X-T1 Kodak Portra 160 *(Fuji X Weekly)* | **17 / 19** |
| 7 | Kodak Portra 400, Classic *(Film Recipes)* | My Fujifilm X100F Kodak Portra 400 *(Fuji X Weekly)* | **17 / 19** |
| 8 | Kodak Portra Pro, Portra 400 *(Film Recipes)* | My Fujifilm X100F Kodak Portra 400 *(Fuji X Weekly)* | 16 / 19 |
| 9 | Kodak Portra 400, Classic *(Film Recipes)* | My Fujifilm X-T30 Kodak Portra 400 *(Fuji X Weekly)* | 15 / 19 |
| 10 | Kodak Portra Pro, Portra 400 *(Film Recipes)* | Kodak Pro 400 — X-Trans V *(Fuji X Weekly)* | 15 / 19 — PRO Neg. Std vs Reala Ace |
| 11 | Kodak Portra 400, Classic *(Film Recipes)* | Kodak Portra 400 v2 — X-T3/X-T30 *(Fuji X Weekly)* | 15 / 19 |
| 12 | Spanish Summer, Bright Summer *(Film Recipes)* | Bright Summer — X100V *(Fuji X Weekly)* | **14 / 19** — Velvia/Vivid vs Classic Chrome, Color −4 vs +4 |
| 13 | Portra 400 *(Filmsim Recipes)* | My Fujifilm X100F Kodak Portra 400 *(Fuji X Weekly)* | 14 / 19 |
| 14 | Kodak Gold II *(Film Recipes)* | Kodak Gold v2 — X-Trans IV *(Fuji X Weekly)* | 12 / 19 — WB shift B −5 vs +4 |
| 15 | Kodak Gold II *(Film Recipes)* | Kodak Gold 200 — X-T5 *(Fuji X Weekly)* | 8 / 19 |
| *(bonus)* | Reggie's Portra *(Filmsim Recipes)* | Reggie's Portra — X-Trans IV *(Fuji X Weekly)* | **4 / 19 — seuls écarts : `DR Auto` vs `DR-Auto`, et 3 champs `null` côté FXW** |

**Lecture de ces divergences.** Il faut distinguer deux natures d'écart, qui ne se corrigent pas de la même façon :

1. **Faux doublons — recettes réellement différentes.** *Kodak Portra 160* chez Fuji X Weekly existe en versions X-T1, X100V, X-T30 et X-T5 v2 : ce sont quatre recettes distinctes de l'auteur, pas quatre extractions divergentes. Idem pour la famille *Portra 400*. L'appariement tolérant les regroupe à tort. **Ne pas fusionner.**
2. **Vrais conflits d'extraction.** *Reggie's Portra* est le cas le plus net : même recette, même auteur d'origine, mais l'extraction Fuji X Weekly perd `dRangePriority`, `smoothSkin` et `exposureCompensation` (à `null`) que Filmsim Recipes a bien capturés, et écrit `DR-Auto` là où l'autre écrit `DR Auto`. Même situation pour *Kodak Portra do Sol* (2 champs, tous deux des plages ISO/expo formulées différemment).

**Le problème transversal est la normalisation des valeurs**, pas seulement leur présence : `DR Auto` / `DR-Auto` / `DR-Auto`, `+1/3` / `+1 to +1-1/3 (typically)` / `1 to 1/3`, `-2` / `-2 (Low)`, `Auto` / `Auto, up to ISO 6400` / `Auto up to ISO 6400`. Tant que ces valeurs restent des chaînes libres, tout appariement ou toute application automatique sur le boîtier est fragile.

---

## 5. Qualité d'extraction des réglages

Champs `settings` évalués (19) : `filmSimulation`, `dynamicRange`, `dRangePriority`, `whiteBalance`, `wbShiftR`, `wbShiftB`, `highlight`, `shadow`, `color`, `sharpness`, `highIsoNR`, `grain`, `grainSize`, `colorChrome`, `colorChromeFXBlue`, `smoothSkin`, `clarity`, `iso`, `exposureCompensation`.

| Critère | Nombre |
|---|---:|
| `settings.filmSimulation` absent ou vide | **0 / 642** |
| Plus de 8 champs `settings` à `null` | **6 / 642** (0,9 %) |

### Les 6 recettes à plus de 8 champs nuls

| Nulls | Index | Nom |
|---:|---|---|
| 12 | `fuji_x_weekly_xt30_full.json` | Fujifilm X-T1 (X-Trans II) Faded Monochrome Film Simulation Recipe |
| 11 | `fuji_x_weekly_selection.json` | There's a Built-In FRGMT B&W Recipe on the Fujifilm GFX100RF??!! |
| 10 | `fuji_x_weekly_selection.json` | Acting Like a Wes Anderson film in Sedona — Fujifilm X-T5 + Vibrant Arizona Recipe |
| 10 | `fuji_x_weekly_editorial.json` | Acting Like a Wes Anderson film in Sedona — Fujifilm X-T5 + Vibrant Arizona Recipe |
| 9 | `fuji_x_weekly_xt30_full.json` | My Fujifilm XF10 Film Simulation Recipes |
| 9 | `fuji_x_weekly_xt30_full.json` | Sepia: The Forgotten Film Simulation |

Les 6 proviennent de Fuji X Weekly, et 5 sur 6 sont des **articles éditoriaux et non des fiches de recette** : « My Fujifilm XF10 Film Simulation **Recipes** » (pluriel — un article qui en contient plusieurs), « Sepia: The Forgotten Film Simulation » (article de fond), « There's a Built-In FRGMT B&W Recipe… » (billet d'actualité), « Acting Like a Wes Anderson film in Sedona » (récit de sortie photo). Les champs nuls y sont **normaux** : il n'y a pas une recette unique à extraire. Le vrai défaut est en amont — ces URL n'auraient pas dû entrer dans le catalogue comme des recettes.

Le seuil « > 8 nulls » est donc un **détecteur d'articles mal classés** plus qu'un indicateur de qualité d'extraction. La qualité d'extraction proprement dite est bonne : `filmSimulation` est renseigné partout, et 636 recettes sur 642 ont au moins 11 champs sur 19 remplis.

Nuance : les entrées « Acting Like a Wes Anderson film in Sedona » et « There's a Built-In FRGMT B&W Recipe » portent pourtant `extraction.status = "VERIFIED"` avec une liste `missingFields` explicite. Le statut `VERIFIED` ne garantit donc pas que l'entrée soit une recette exploitable.

---

## 6. Fichiers orphelins

Comparaison des 612 sous-dossiers de `library/recipes/` aux 611 slugs référencés par l'ensemble des index.

| Constat | Valeur |
|---|---:|
| Dossiers ne correspondant à aucun slug | **1** |
| Slugs d'index sans dossier correspondant | **0** |
| Dossiers vides | 4 |

**Le seul orphelin : `library/recipes/feed/`**, contenant un unique fichier `recipe.json` (1 437 octets).

Le nom est révélateur : le crawler a suivi une URL de flux (`.../feed/`, motif WordPress standard) et l'a traitée comme un article de recette. Le dossier a été créé, un `recipe.json` écrit, mais l'entrée n'a jamais été retenue dans un index — le filtrage en aval a donc bien fonctionné, seul le nettoyage du dossier manque. Impact réel : nul. À supprimer par simple hygiène.

Point positif : **aucun slug d'index ne pointe vers un dossier inexistant**. La correspondance index → disque est complète.

---

## 7. Synthèse

| Indicateur | Valeur | Appréciation |
|---|---:|---|
| Entrées de recette (tous index) | 642 | — |
| Recettes uniques (slugs distincts) | **611** | 31 recouvrements internes FXW |
| Références d'images cassées | **6** | 0,44 % — concentrées sur 1 recette |
| Recettes sans aucune image | 3 | 0,5 % |
| Groupes de doublons inter-sites (nom strict) | **3** | tous divergents |
| Paires inter-sites (appariement tolérant) | 98 | aucune identique |
| `filmSimulation` manquant | 0 | conforme |
| Recettes à > 8 champs nuls | 6 | dont 5 articles mal classés |
| Dossiers orphelins | 1 | `feed/` |

### Les 3 problèmes prioritaires

**1. Les valeurs de `settings` ne sont pas normalisées entre extracteurs.**
C'est le problème le plus structurant, et le seul qui bloque un usage automatisé. Le même réglage s'écrit `DR Auto`, `DR-Auto` ou `DR200` ; l'exposition s'écrit `+1/3`, `1 to 1/3`, `+1 to +1-1/3 (typically)`, `0 to +1` ; les valeurs numériques apparaissent tantôt en `-2`, tantôt en `-2 (Low)`, tantôt en `1.5` (décimal, catalogue *Film Recipes*) alors que le X-T30 n'accepte que des demi-crans sur une échelle discrète. Tant que ces champs restent du texte libre, ni la comparaison inter-catalogues, ni la déduplication, ni l'application des réglages sur le boîtier ne peuvent être fiables. **Action : définir un schéma de valeurs canoniques par champ et une passe de normalisation à l'import.**

**2. Des articles éditoriaux sont catalogués comme des recettes.**
Au moins 5 entrées ne sont pas des fiches de réglages (articles de fond, récits de sortie, articles multi-recettes), et l'orphelin `feed/` montre que le crawler suit aussi des URL non-articles. Ces entrées polluent les comptages, faussent la répartition de compatibilité et produiront des fiches vides dans l'interface. Le champ `extraction.status = "VERIFIED"` ne les détecte pas. **Action : ajouter un critère de rejet (nombre minimal de champs `settings` renseignés, ou détection de titre pluriel/éditorial) et purger `feed/`.**

**3. La colonne de compatibilité X-T30 n'est pas comparable d'un catalogue à l'autre.**
`filmsimrecipes` classe 100 % de son fonds en `XT30_PARTIAL` et `film_recipes` 93,6 %, contre 38 % `COMPATIBLE` / 32 % `INCOMPATIBLE` chez Fuji X Weekly. Un tel écart traduit des heuristiques d'évaluation différentes, pas une différence réelle de contenu — les trois sites publient largement les mêmes familles de recettes. En l'état, filtrer la bibliothèque sur « compatible X-T30 » ne renverrait presque que du Fuji X Weekly et masquerait 268 recettes exploitables. **Action : réévaluer la compatibilité avec une règle unique appliquée aux `settings` normalisés (issue du point 1), plutôt que par extracteur.**

### Points secondaires

- 4 dossiers vides (`old-gold-film-recipe`, `nightwalker-street-film-recipe-for-night-lights`, `123-chrome-classic-kodachrome-look`, `newsprint-grainy-acros-for-daily-life`) — à revérifier après la fin de la passe de téléchargement, il peut s'agir d'échecs récupérables.
- Le champ `images.all` est vide pour les 642 entrées, alors que ~1 400 images téléchargées ne sont référencées nulle part : soit remplir `images.all`, soit ne pas télécharger ces fichiers (~493 Mo au total sur disque aujourd'hui).
- Dans le catalogue *Film Recipes*, `images.cover` duplique systématiquement la première entrée de `images.examples`. Sans gravité, mais l'affichage montrera deux fois la même photo si les deux champs sont rendus.

---

## Méthode

- Lecture unique de chaque `index/*.json` via `ConvertFrom-Json` (PowerShell 5.1, UTF-8). Aucune réécriture, aucun accès réseau, aucun accès appareil photo.
- Table de hachage de tous les fichiers présents sous `library/recipes/` (chemins relatifs à `library/`, comparaison insensible à la casse et aux séparateurs `\` / `/`).
- Normalisation stricte des noms : majuscules, apostrophes typographiques supprimées, tout caractère non alphanumérique réduit à une espace.
- Normalisation tolérante (§4.3) : normalisation stricte + suppression des désignations de boîtier, générations X-Trans et boilerplate, puis appariement par jetons (Jaccard ≥ 0,6 ou inclusion) entre entrées de `site` différents uniquement.
- Comparaison des réglages sur les 19 champs de `settings`, `null` et chaîne vide traités comme équivalents et distincts d'une valeur renseignée.
