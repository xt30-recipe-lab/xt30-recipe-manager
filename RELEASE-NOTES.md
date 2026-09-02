# XT30 Recipe Manager v1.0.0

Première version de validation Windows.

## Fonctions

- interface complète : Camera, Recipes, Camera Slots, Packs, Backups et Diagnostics ;
- bibliothèque et favoris locaux ;
- scan PTP/MTP du Fujifilm X-T30 première génération ;
- affichage explicite de la provenance `CAMERA`, `LOCAL` ou `Not reported` ;
- génération de rapports TXT et JSON.

## Sécurité

Cette distribution utilise une whitelist PTP strictement limitée aux opérations de
lecture. Les opérations `SetDevicePropValue`, `SendObject`, `DeleteObject` et les
opcodes vendor non validés sont refusés.

Le paquet ne contient aucun rapport issu d'un appareil réel, aucune sauvegarde du
boîtier, aucune photographie de bibliothèque tierce et aucun corpus de recettes tiers.
