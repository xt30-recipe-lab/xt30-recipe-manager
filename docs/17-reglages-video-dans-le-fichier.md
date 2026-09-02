# Les réglages du mode film sont dans le fichier de sauvegarde

Date : 02/09/2026 — mesuré sur X-T30 première génération, série 01365935353533341904

## Ce qui est établi

Le fichier de sauvegarde du X-T30 (objet PTP handle 0, format 0x5000, 5628 octets)
**contient les réglages d'image du mode film**. Ils ne sont pas dans les banques
C1–C7 — qui commencent à l'offset 3836 — mais dans un bloc autour de l'offset 840.

Cela corrige une affirmation antérieure de ce projet. Nous avions écrit qu'une
recette vidéo ne pourrait jamais être transférée. C'est faux : elle le peut, par
exactement le même chemin que les banques photo.

## Méthode

Expérience à variable unique, entièrement en lecture seule.

1. Lecture de référence (`Tools/BackupRead`, opcodes 0x1008 et 0x1009, handle 0).
2. **Un seul** réglage modifié au menu du boîtier.
3. Relecture, puis comparaison octet à octet.

Prérequis vérifié au préalable : **deux lectures successives sans rien toucher
donnent des fichiers strictement identiques** (0 octet différent sur 5628, y
compris le compteur de l'offset 248). Toute différence observée est donc un vrai
changement de réglage, jamais du bruit.

## Carte mesurée

| Offset | Hex | Réglage du menu RÉGLAGE FILM | Encodage | Preuve |
|---|---|---|---|---|
| 757 | 0x02F5 | Température de couleur | index **décroissant** dans `KelvinPresets` | 21 = 3200 K, 8 = 5600 K (mesuré deux fois) |
| 840 | 0x0348 | Simulation de film | identique aux banques photo | 13 = Classic Chrome, 18 = Eterna |
| 854 | 0x0356 | Plage dynamique | **décalé** : 0 = DR100, 1 = DR200, 2 = DR400 | 2 → 1 en passant à DR200 |
| 857 | 0x0359 | Couleur | identique aux banques photo | 10 → 8 en passant à −1 |
| 862 | 0x035E | Ton lumière | `valeur = 4 − octet` | 6 → 4 en passant à 0 |
| 864 | 0x0360 | Ton ombre | `valeur = 4 − octet` | 6 → 4 en passant à 0 |

### Le piège de la plage dynamique

En photo, la table est `0 = DR-P, 1 = DR100, 2 = DR200, 3 = DR400`. Le menu film
n'offre pas DR-P, et la table y est décalée d'un cran. **Réutiliser l'encodeur
photo écrirait DR100 en croyant écrire DR200.** Il faut un encodeur distinct.

## Hypothèses non confirmées

- **775 (0x0307) et 777 (0x0309)** : décalage de balance des blancs R et B, avec
  9 pour neutre. Ils ont bougé vers 9 lors du passage à une température fixe,
  mais n'ont pas rebougé au changement suivant. À tester séparément.
- **842 (0x034A)** : voisin immédiat de la simulation de film, a varié une fois
  dans un diff non attribuable.
- **753, 758, 760, 774, 776, 831** : ont varié dans le premier diff long, jamais
  isolés depuis.

## Une deuxième copie des réglages

Les offsets **180–420** forment un second bloc qui reflète les mêmes valeurs :
l'offset 216 est passé de 13 à 18 en même temps que l'offset 840 (Classic Chrome
→ Eterna), et les offsets 356/357 suivent la balance des blancs. C'est
vraisemblablement l'état actif courant du boîtier, distinct des réglages
mémorisés. Ne pas y écrire sans l'avoir compris.

## Avertissement de méthode

Deux séries de mesures ont été perdues parce que plusieurs choses avaient changé
en même temps — molette de mode tournée, ou relecture spontanée du bloc objectif
(offsets 3768–3773 et 3830, qui se réécrit tout seul). Un diff de 10 à 29 octets
n'est pas exploitable. La discipline « un réglage, une pause, une relecture » est
la condition du résultat.

## Reste à cartographier

Netteté, réduction du bruit, RB inter-image, mode film (résolution/cadence),
enregistrement F-Log, réglage N&B, et le mode de balance des blancs lui-même
(distinct de la température).
