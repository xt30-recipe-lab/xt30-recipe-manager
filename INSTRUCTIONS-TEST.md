# Test du X-T30 avec xt30-probe — instructions

Durée : ~5 minutes. L'outil est **strictement en lecture seule** : il n'envoie que
GetDeviceInfo / GetDevicePropDesc / GetDevicePropValue. Aucune écriture, aucun risque
au-delà de ce que fait l'Explorateur Windows quand il ouvre l'appareil.

## Préparation de l'appareil

1. Batterie chargée (ou au moins > 50 %).
2. Sur le X-T30 : `MENU → CLÉ (SET UP) → PARAMÈTRE CONNEXION (CONNECTION SETTING) → MODE CONNEXION USB`
   → sélectionner **« CONV. RAW USB/SAUVEG. RESTAUR. » (USB RAW CONV./BACKUP RESTORE)**.
   (Le mode « LECTEUR DE CARTE USB » n'expose pas le protocole PTP — la sonde ne verrait rien.)
3. Éteindre l'appareil, brancher le câble USB-C au PC, rallumer l'appareil.
4. Fermer toute application qui pourrait utiliser l'appareil (X Acquire, X RAW Studio,
   Capture One, fenêtre d'importation de photos…). L'Explorateur Windows peut rester ouvert
   mais ne pas naviguer dans l'appareil pendant le test.

## Lancement

**Double-cliquer sur `xt30-probe\xt30-probe.exe`** : une fenêtre s'ouvre.
- Le bandeau indique en direct si un Fujifilm est détecté (détection automatique toutes les 4 s).
- Cliquer sur **« ▶ Lancer le sondage (lecture seule) »**.
- Le journal défile dans la fenêtre ; à la fin, les boutons « Ouvrir xt30_report.txt / .json »
  et « 📁 Dossier rapports » donnent accès aux fichiers.

(Alternative terminal : `xt30-probe\xt30-probe-cli.exe`, mêmes options `--list`, `--sweep`, `--out`.)

Le programme écrit deux fichiers dans le dossier `xt30-probe\` :
- `xt30_report.json` (rapport complet, à me fournir)
- `xt30_report.txt` (résumé lisible, à me fournir aussi)

## Passages complémentaires (recommandés si le 1er passage réussit)

1. **Balayage de découverte** (toujours lecture seule, ~2 min de plus) :
   cocher la case **« Balayage étendu (--sweep) »** dans la fenêtre et relancer le sondage.
   (Équivalent terminal : `.\xt30-probe-cli.exe --sweep --out sweep-rawconv`)

2. **Second mode USB** : régler l'appareil sur
   `MODE CONNEXION USB → PRISE DE VUE USB FIXÉE/AUTO (USB TETHER SHOOTING)`,
   relancer le sondage (les rapports précédents seront écrasés — les renommer avant,
   ou utiliser la version CLI avec `--out tether`).
   Cela permettra de comparer les jeux de propriétés exposés par chaque mode.

3. **Diagnostic driver** (utile si la sonde ne détecte rien) — dans PowerShell :
   ```
   Get-PnpDevice | Where-Object { $_.InstanceId -match "VID_04CB" } | Format-List FriendlyName, Class, Service, InstanceId, Status
   ```
   Copier la sortie dans la réponse.

## Si rien n'est détecté

- Vérifier le mode USB (étape 2 ci-dessus) et refaire un cycle extinction/allumage appareil branché.
- Essayer l'autre port USB / un autre câble (certains câbles sont charge seule).
- Lancer `.\xt30-probe.exe --list` et m'envoyer la sortie + le diagnostic driver ci-dessus.

## Ensuite

Me transmettre `xt30_report.json` + `xt30_report.txt` (et les rapports des passages
complémentaires si faits). J'analyserai :
- si 0xD18C/0xD18D (sélecteur et nom de slot C1–C7) existent sur le X-T30 mk1 ;
- les datatypes/plages réels de tout le bloc recette 0xD190..0xD1A5 ;
- les valeurs enum de FilmSimulation (présence/absence de Classic Negative, etc.) ;
- la comparaison avec X-S10 / X-H2 / X-T5 / X-Pro3 (phase 4 du plan).

Aucune écriture ne sera envisagée avant cette analyse et ton accord explicite.
