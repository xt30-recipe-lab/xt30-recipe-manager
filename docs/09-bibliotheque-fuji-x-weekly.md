# Import de métadonnées de recettes Fuji X Weekly

L'importeur situé dans `xt30-probe/Tools/FujiXWeeklyImporter/` est entièrement
séparé du moteur caméra. Il ne référence ni `Probe.cs`, ni USB, ni PTP, ni WPD.

## Politique de contenu

- aucune photographie n'est collectée, référencée ou téléchargée ;
- aucun texte d'article n'est recopié dans la bibliothèque ;
- seules les valeurs structurées nécessaires à une recette sont analysées ;
- le nom de la source, l'auteur et l'URL de l'article restent attachés à la recette ;
- aucune valeur absente n'est inventée ;
- aucun corpus tiers n'est distribué avec le dépôt ou l'exécutable.

Un ancien import local de validation a permis de tester l'interface avec 212
entrées X-Trans III/IV. Ce corpus et les images associées sont exclus du dépôt.
Le résultat historique ne constitue pas une autorisation de redistribution.

## Verrou d'autorisation

Le périmètre complet des catalogues publics ne peut pas être lancé par accident :

```text
fxw-importer --all --permission-confirmed
```

L'option `--permission-confirmed` ne doit être utilisée qu'après réception d'une
autorisation écrite ou vérification d'une licence permettant cette réutilisation.

Sans `--all`, l'importeur conserve son périmètre technique X-Trans III/IV. Le
programme reste en mode métadonnées uniquement dans tous les cas.

## Données produites

Chaque entrée contient :

- le nom, le slug, l'auteur, la date et l'URL source ;
- les réglages trouvés sur la page ;
- la compatibilité estimée avec le X-T30 première génération et sa justification ;
- les champs absents et le statut d'extraction ;
- une section `images` vide par conception.

Les recettes importées sont toujours affichées comme contenu externe en lecture
seule. Elles ne deviennent jamais des données `CAMERA` et ne sont jamais envoyées
au boîtier.
