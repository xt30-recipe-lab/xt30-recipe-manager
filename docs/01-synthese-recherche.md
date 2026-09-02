# Synthèse de recherche — Recettes Fujifilm via USB/PTP et X-T30 (gen 1)

Date : 2026-08-31. Cible : **Fujifilm X-T30 première génération** (X-Trans CMOS 4 / X-Processor 4).

## Verdict global

| Question | Réponse |
|---|---|
| Un projet a-t-il testé les recettes PTP sur X-T30 mk1 ? | **NON, aucun.** |
| Le mécanisme C1–C7 (0xD18C/0xD18D) existe-t-il sur d'autres Fuji ? | OUI — confirmé sur **X-S10** (Filmcase), **X-H2 / X-T5** (fujifilm-ptp-recipes) |
| Existe-t-il un contre-exemple dans la même génération ? | OUI — le **X-Pro3 (X-Trans IV) échoue** avec ce protocole (fujifilm-ptp-recipes, diagnostic ouvert) |
| Le X-T30 mk1 est-il utilisable en PTP USB par ailleurs ? | OUI — **rawji** a testé le X-T30 (conversion RAW via 0xD185, PID USB 0x02E3) |
| Peut-on flasher/modifier le firmware ? | NON pertinent — FujiHack ne supporte que XF1/X-A2, hors périmètre et hors sujet (trop risqué) |

Conclusion : la compatibilité du X-T30 mk1 avec le mécanisme C1–C7 est **plausible mais non prouvée** (X-Trans IV, 7 slots, PTP fonctionnel), et l'échec du X-Pro3 interdit toute supposition. D'où l'outil `xt30-probe` en lecture seule.

## Projet par projet

### Filmcase (gosku/Filmcase) — GPLv3, Python/Django + WebUSB
- Écrit des recettes dans les slots C1–C7 via PTP : `0xD18C` = sélecteur de slot (uint16, 1..7), `0xD18D` = nom du slot (PTP string), puis bloc `0xD190..0xD1A2`.
- Mode appareil requis : **USB RAW CONV./BACKUP RESTORE**.
- Seul boîtier testé : **X-S10** (4 slots). X-T30 listé « attendu compatible, 7 slots » — théorique.
- Particularités : écrit les payloads en **int32 (4 octets)** ; ordre d'écriture imposé (nom d'abord, Kelvin avant WB shift R/B sinon reset des shifts) ; délais 50 ms avant / 200 ms après chaque écriture ; vérification par relecture.
- Même le « scan » de slots écrit le curseur 0xD18C (seule écriture du mode lecture). Notre sonde, elle, n'écrit **rien** : elle ne lira que le slot actuellement pointé.

### fujifilm-ptp-recipes (ILFforever) — documentation du protocole
- Docs détaillées : conteneurs PTP, opcodes, propriétés `0xD18C..0xD1A5`, règles de noms (≤25 car. ASCII), séquences lecture/écriture, checklist de test.
- Payloads en **uint16/int16 (2 octets)** — divergence avec Filmcase à trancher sur X-T30.
- Testé : X-H2 (fw 5.20) OK, X-T5 OK, **X-Pro3 (X-Trans IV) : échec complet**.
- Avertissement garantie : Fujifilm considère l'accès USB tiers couvert par la clause de la EULA du SDK (§5.2).
- Divergences documentées entre boîtiers : DR Auto = 0 (X-H2/X-T5) vs 65535 (X-S10) ; Grain Off relu comme 6 ou 7 ; propriétés annexes absentes sur certains boîtiers (X-E5 sans 0xD36B/0xD16E).

### rawji (pinpox) — X-T30 testé ✔
- Alternative à X RAW Studio : envoie un RAF au boîtier qui le développe avec son processeur. Mode **USB RAW CONV./BACKUP RESTORE**.
- **X-T30 gen 1 testé et supporté** : VID 0x04CB, **PID 0x02E3**, IOPCode processeur `FF159502`.
- Manipule la propriété **0xD185 (RawConvProfile)** — c'est une *recette de développement*, PAS les Custom Settings C1–C7.
- Découvertes X-T30 précieuses : format de profil natif 605 octets (limité), mais **accepte le format standard 628 octets** ; ShadowTone limité à −2..+4 ; Kelvin par presets seulement sur ≤ X-Processor 4.

### libfuji / fudge / libpict (petabyt) — référence protocole
- `fujiptp.h` documente les modes USB Fuji et `0xD16E` (USBMode : 5=Tether, 6=RawConv, 8=Webcam), les opcodes vendor `0x900C/0x900D/0x901D` (envoi d'objets), le cluster conversion RAW `0xD183..0xD187`.
- **0xD18C/0xD18D n'apparaissent que dans un bloc expérimental commenté** de `fuji_usb.c` (avec 0xD21C) — non documentés chez petabyt ni dans libgphoto2.
- `libwpd` (petabyt) confirme la faisabilité du PTP brut via l'API Windows WPD sans changer de driver.

### libgphoto2 (camlibs/ptp2/ptp.h)
- Liste exhaustive des `PTP_DPC_FUJI_*` (copiée dans `docs/reference/ptp.h`) : clusters 0xD0xx (image), 0xD1xx (réglages), 0xD2xx (capture/état), 0xD3xx (custom/affichage).
- Piste alternative pour les banques persos : `PTP_DPC_FUJI_CustomSetting = 0xD34C` (aucun projet ne l'exploite ; à sonder).

### FujiHack
- Rétro-ingénierie firmware (XF1/X-A2 uniquement). Hors périmètre : nous n'approcherons jamais le firmware. Son wiki renvoie vers fudge pour le PTP.

## Points de vigilance pour le X-T30 mk1

1. **Rien n'est prouvé** : le probe doit vérifier si `0xD18C`/`0xD18D` figurent dans `DevicePropertiesSupported` du GetDeviceInfo, et ce que disent leurs descripteurs (datatype, writable, plage).
2. **Largeur de payload** 2 vs 4 octets : à trancher via le datatype renvoyé par `GetDevicePropDesc`.
3. **Fonctions absentes du X-T30** : Classic Negative (17), Eterna Bleach Bypass (18), Color Chrome FX Blue (0xD197), Clarity (0xD1A2), Grain Size, Smooth Skin (0xD198 ?) — le probe montrera si ces propriétés existent et quelles valeurs l'enum autorise.
4. **WB Shift R/B (0xD19A/0xD19B)** : le X-T30 a des limitations connues de mémorisation par Custom Setting — comportement à tester réellement (phase écriture, plus tard, jamais automatiquement).
5. **Sécurité** : lectures PTP = risque très faible (c'est ce que fait n'importe quel explorateur MTP) ; le sélecteur 0xD18C est une écriture réputée bénigne mais notre sonde ne la fait PAS.
