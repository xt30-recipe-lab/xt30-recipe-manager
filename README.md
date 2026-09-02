# XT30 Recipe Manager

![XT30 Recipe Manager](docs/assets/github/xt30-recipe-manager-hero.png)

Application Windows pour organiser des recettes Fujifilm et inspecter en lecture
seule un **X-T30 première génération** (X-Trans CMOS 4 / X-Processor 4) connecté
en USB.

Le projet propose une bibliothèque locale, des favoris, des packs, l'affichage des
slots C1–C7 et des diagnostics PTP/MTP. Les données affichées comme `CAMERA`
proviennent d'une lecture réelle du boîtier ; les autres restent explicitement
marquées `LOCAL` ou `Not reported`.

> Projet indépendant et non affilié à FUJIFILM Corporation. Fujifilm et les noms
> de produits associés appartiennent à leurs détenteurs respectifs.

## Règle absolue

Tant que les capacités réelles du X-T30 mk1 ne sont pas connues : **lecture seule**.
Aucun SetDevicePropValue, aucun opcode vendor, aucune écriture d'aucune sorte.
Le firmware n'est jamais approché.

## État du projet

- [x] Phase 1 — Recherche : projets Filmcase, fujifilm-ptp-recipes, rawji, FujiHack,
      libfuji/libpict, libgphoto2 analysés → `docs/01-synthese-recherche.md`
- [x] Sonde PTP/MTP Windows construite et testée en lecture seule.
- [x] Phase 3 — Scan réel réussi ; rapports JSON/TXT produits et archivés.
- [x] Phase 4 — Comparaison avec les implémentations existantes, dont
      [grawji X-T3/X-T30](docs/06-grawji-xt3-vs-xt30.md).
- [ ] Phase 5 — Écriture expérimentale (JAMAIS sans autorisation explicite ;
      premier test = renommage de C7 avec sauvegarde/restauration)
- [x] Interface du mockup, bibliothèque locale, packs de démonstration et diagnostics.
- [x] Import de métadonnées de recettes pris en charge séparément.
- [ ] Lecture/écriture réelle des banques C1–C7.

Les corpus de recettes et les photographies provenant de sites tiers ne sont pas
distribués dans ce dépôt. Leur import exige une autorisation ou une licence compatible.

## Compilation

Prérequis : Windows avec .NET Framework 4.x.

```bat
cd xt30-probe
build.cmd
```

La compilation produit `xt30-recipe-manager.exe` et `xt30-probe-cli.exe`. Les
fichiers du dossier `xt30-probe/Assets` doivent rester à côté de l'exécutable.

## Utilisation

1. Lancez `xt30-recipe-manager.exe`.
2. Pour un scan, placez le boîtier en mode `USB RAW CONV./BACKUP RESTORE`.
3. Connectez le X-T30 puis utilisez **Scan Camera**.

Le scan ne modifie aucun réglage et n'écrit rien dans l'appareil.

## Arborescence

- `xt30-probe/` — l'application : `Probe.cs` (moteur), `Gui.cs` (entrée GUI),
  `Presentation/` (composants et pages), `Models/` (bibliothèque locale), `Camera/` (adaptateur UI),
  `build.cmd`, `xt30-recipe-manager.exe` (fenêtre, double-clic), `xt30-probe-cli.exe` (console)
- `docs/01-synthese-recherche.md` — synthèse de la recherche
- `docs/02-proprietes-ptp.md` — table des propriétés PTP Fuji (état des connaissances)
- `docs/03-approche-windows.md` — choix technique (WPD passthrough, garde-fou read-only)
- `INSTRUCTIONS-TEST.md` — procédure de test à suivre avec l'appareil
- `xt30-probe/Tools/FujiXWeeklyImporter/` — importeur Fuji X Weekly (module séparé du moteur)
- `docs/` — recherche, protocole, validation et visuels promotionnels

## Faits clés issus de la recherche

- Le mécanisme C1–C7 par PTP (0xD18C sélecteur / 0xD18D nom / bloc 0xD190..0xD1A5) est
  confirmé sur X-S10, X-H2, X-T5 — mais **jamais testé sur X-T30 mk1**, et le X-Pro3
  (même génération) échoue. D'où le probe.
- Mode USB requis sur l'appareil : **USB RAW CONV./BACKUP RESTORE**.
- Le X-T30 mk1 est confirmé fonctionnel en PTP USB, PID `0x02E3`.
- Limitations attendues du X-T30 : pas de Classic Negative, Bleach Bypass, Color Chrome FX Blue,
  Clarity, Grain Size ; l'application devra les gérer explicitement (jamais d'envoi silencieux).

## Licence

Aucune licence open source n'a encore été choisie. En l'absence de fichier
`LICENSE`, tous les droits sur le code original sont réservés. Les composants ou
références tiers conservent leurs propres licences.
