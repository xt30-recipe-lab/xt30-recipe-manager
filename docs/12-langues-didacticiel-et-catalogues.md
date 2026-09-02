# Langues, didacticiel et catalogues de recettes

Date : 02/09/2026

## 1. Interface multilingue

Cinq langues : **anglais, français, espagnol, allemand, italien**.

### Principe

La **clé de traduction est le texte anglais lui-même**. `Strings.T("Recipes")`
renvoie « Recettes » en français et, si une chaîne n'est pas encore traduite,
elle renvoie l'anglais correct plutôt qu'un identifiant technique à l'écran.

La traduction est faite **au moment du dessin**, pas à la construction des
contrôles. Conséquence : changer de langue dans les réglages rafraîchit
l'application entière sans la redémarrer, et sans rien reconstruire.

### Ce qui n'est jamais traduit

- Les **valeurs** de réglage (`Off`, `Weak`, `Strong`, `Daylight`, `DR400`,
  `Classic Chrome`…). Elles sont écrites telles quelles dans la bibliothèque et
  relues par les encodeurs du fichier de réglages : les traduire casserait le
  transfert vers l'appareil.
- Les **clés de page et de filtre** (`Camera Slots`, `Compatible`, `Video`…).
  `Control.Text` reste la clé anglaise ; seule la peinture passe par `Strings.T`.
  C'est ce qui permet aux comparaisons de l'application de continuer à
  fonctionner dans n'importe quelle langue.
- Les **catégories** de recette : la liste déroulante est dessinée par
  `Ui.TranslateItems`, qui affiche le libellé traduit tout en gardant la valeur
  anglaise dans l'élément.

### Détails d'implémentation qui ont demandé une correction

- La pastille `READ ONLY` se dimensionne sur son libellé traduit
  (`StatusBadge.PreferredWidth`) : « LECTURE SEULE » était tronqué.
- Les puces de filtre mesurent leur largeur sur le texte traduit :
  « Kompatibel » et « Compatibles » n'ont pas la largeur de « Compatible ».
- Le menu du boîtier est traduit tel que le X-T30 l'affiche :
  `SET UP` → `CONFIG.`, `USB RAW CONV./BACKUP RESTORE` → `CONV. RAW USB /
  SAUVEG. RESTAUR.`
- **Ne jamais réécrire un `.ps1` ou un `.cs` avec `Set-Content`** : PowerShell
  5.1 relit un fichier UTF-8 sans BOM en ANSI et transforme les accents et les
  « · » en mojibake. Une passe de ce type a corrompu `TutorialForm.cs` et cassé
  les clés de traduction ; le fichier a été réécrit.

## 2. Didacticiel

`Presentation/TutorialForm.cs` — six étapes dans l'ordre réel d'utilisation :

1. Ce que fait l'application, et qu'elle n'écrit jamais dans l'appareil.
2. Mettre le boîtier en `USB RAW CONV./BACKUP RESTORE`.
3. Lire l'appareil.
4. Trouver ou créer une recette (photo ou vidéo).
5. Charger des recettes dans C1-C7.
6. Ce que l'appareil ne peut pas stocker (ISO, décalage BdB, valeurs sans code
   vérifié) et pourquoi les noms de banque ressemblent à `PACIFIC R+1 B-3`.

Il s'ouvre une fois au premier lancement — case « Ne plus afficher » cochée par
défaut — et reste accessible depuis **Réglages → Revoir le didacticiel**. Il
n'exécute aucune action sur l'appareil. Le mode capture le supprime
(`MainForm.SuppressTutorial`) : une fenêtre modale bloquerait la copie d'écran.

## 3. Catalogues de recettes

### Sources retenues

| Site | API | Recettes publiées | Importées | Images |
|---|---|---|---|---|
| Fuji X Weekly | pages catalogue par génération | ~700 liens | voir rapport | passe séparée |
| film.recipes | `wp-json/wp/v2/posts` | 325 | 202 | 196 |
| Filmsim Recipes | `wp-json/wp/v2/portfolio` | 150 | 66 | 58 |

`Tools/FilmRecipesImporter/Import-FilmRecipes.ps1` (nouveau) lit la table
`<table class="recipe-settings">` de chaque article : libellé à gauche, valeur à
droite. La lecture est structurée, aucun texte d'article n'est recopié. Les
traits d'union insécables (`&#8209;`) sont normalisés, sans quoi `-4` ne serait
pas reconnu comme un nombre.

`Tools/FujiXWeeklyImporter/Fetch-Covers.ps1` (nouveau) ajoute une couverture aux
recettes déjà importées, en lisant la balise `og:image` de l'article. Une passe
séparée plutôt qu'un nouvel import : les réglages extraits ne sont pas refaits.

L'importeur Fuji X Weekly couvre désormais **X-Trans I à IV** par défaut
(`Catalog.Xt30Relevant`) : ces générations n'utilisent que des réglages présents
sur le X-T30. `--all` ajoute X-Trans V, Bayer, GFX, EXR-CMOS et Full Spectrum.

### Source écartée

**shuttergroove.com** : le site expose bien une API WordPress, mais ses recettes
sont dans du balisage Elementor sans table de réglages. En tirer des valeurs
demanderait de deviner ; on ne l'importe pas.

### Compatibilité

Les trois importeurs appliquent la même règle. Une recette est
`XT30_INCOMPATIBLE` si sa simulation n'existe pas sur ce boîtier (Classic
Negative, Nostalgic Neg., Reala Ace, Eterna Bleach Bypass) — 120 recettes de
film.recipes sont dans ce cas et ne sont pas importées. Elle est `XT30_PARTIAL`
si un réglage secondaire manque (Clarity, Color Chrome FX Blue, taille de
grain, Tone Curve) : elle reste utilisable, l'application affiche la limite.

## 4. Panneau de préparation permanent

La préparation des banques n'est plus une fenêtre à ouvrir. `BankPlanPanel`
occupe une colonne à droite de la page **Banques C1-C7**, à côté de l'état réel
lu dans le boîtier :

- une liste déroulante par banque, la recette de la bibliothèque dont le nom
  correspond à la banque étant présélectionnée ;
- sous chaque liste, le nom que portera la banque et son compteur sur 25
  caractères, recalculés à chaque changement ;
- sur la ligne de la banque, à gauche, un liseré vert et la mention `→ NOM`
  apparaissent dès qu'une recette est prévue : la modification se lit en direct
  sans rien avoir écrit dans l'appareil ;
- `Envoyer les sept à l'appareil`, `Créer seulement le fichier…`, `Réinitialiser`.

Sur une fenêtre étroite (moins de 1000 px utiles), le panneau passe sous la
carte des banques au lieu de disparaître. `BankPlanForm` existe toujours et
héberge le même panneau, pour la validation hors ligne.

### Deux défauts corrigés au passage

- `CameraBankFile.BuildBankName` rajoutait le décalage de balance des blancs à
  un nom qui le contenait déjà : `PACIFIC R+1 B-3` devenait
  `PACIFIC R+1 B-3 R+1 B-3`. Le suffixe n'est plus ajouté quand le nom se
  termine déjà par lui.
- La ligne `→ NOM` se dessinait par-dessus la ligne de provenance. Les deux
  partagent le même emplacement : la provenance s'efface quand une recette est
  prévue.

## 5. Écrans d'attente

`Presentation/OverlayForms.cs` :

- **`SplashForm`** — écran d'ouverture affiché pendant le chargement de la
  bibliothèque. `RecipeLibrary.Progress` transmet la clé anglaise de l'étape et
  ses arguments ; c'est l'interface qui traduit. Étapes : démarrage, vos
  recettes, catalogues importés, banques décodées, « N recettes prêtes ».
- **`ProgressForm`** — fenêtre d'attente pour la lecture de l'appareil et pour
  l'envoi. Le travail s'exécute sur un fil séparé (il n'appelle que des outils
  externes, il ne touche à aucun contrôle) pour que le message et l'animation
  restent vivants pendant les vingt secondes que prend une restauration.

## 6. Pagination de la grille

Avec plusieurs centaines de recettes, créer une vignette WinForms par recette
rendait la page inutilisable. `RecipeGrid` conserve la liste complète mais
n'instancie que **120 vignettes à la fois**, avec un bouton « Afficher plus de
recettes ». La légende indique « 120 sur 557 affichées ».
