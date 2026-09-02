# Propriétés PTP Fujifilm pertinentes (état des connaissances avant sondage)

Aucune de ces informations n'a été vérifiée sur X-T30 gen 1 — c'est le rôle de `xt30-probe`.
Sources : F = Filmcase, I = fujifilm-ptp-recipes (ILFforever), G = libgphoto2 ptp.h, L = libfuji/fudge, R = rawji.

## Bloc « Custom Settings / recettes » (mode USB RAW CONV./BACKUP RESTORE)

| ID | Nom supposé | Type | Rôle | Confirmé sur | Source |
|----|-------------|------|------|--------------|--------|
| 0xD18C | CustomSlotSelector | uint16 | Sélectionne C1..C7 (valeur 1..7) — à écrire AVANT tout accès slot | X-S10, X-H2, X-T5 | F, I |
| 0xD18D | CustomSlotName | string PTP | Nom du slot sélectionné (≤25 car. ASCII) | X-S10, X-H2, X-T5 | F, I |
| 0xD18E, 0xD18F | ? | ? | Non mappés (« à logguer ») | — | I |
| 0xD190 | DynamicRange | uint16 | 100/200/400 ; Auto = 0 (X-H2/X-T5) ou 65535 (X-S10) ⚠ divergence | X-S10, X-H2, X-T5 | F, I |
| 0xD191 | DRangePriority | uint16 | 0=Off, 1=Weak, 2=Strong, 32768=Auto ; si ≠Off, l'écriture DR est rejetée (0x201C) | idem | F, I |
| 0xD192 | FilmSimulation | uint16 | 1=Provia … 11=ClassicChrome … 16=Eterna, 17=ClassicNeg(+), 20=RealaAce ; X-T30 devrait s'arrêter à 16 + Acros/Mono/Sepia | idem | F, I, R |
| 0xD193 | MonoColor WarmCool | int16 | cadran ×10 | idem | F, I |
| 0xD194 | MonoColor MagentaGreen | int16 | cadran ×10 | idem | F, I |
| 0xD195 | GrainEffect | uint16 | 1=Off(écriture), 2=Weak/Small, 3=Strong/Small, 4=Weak/Large, 5=Strong/Large, 6/7=Off(relecture) ; X-T30 n'a pas Grain Size → enum attendu réduit | idem | F, I |
| 0xD196 | ColorChromeEffect | uint16 | 1=Off, 2=Weak, 3=Strong | idem | F, I |
| 0xD197 | ColorChromeFXBlue | uint16 | idem — **absent du X-T30** en théorie | idem | F, I |
| 0xD198 | SmoothSkinEffect | uint16 | idem — documenté par I seulement | X-H2, X-T5 | I |
| 0xD199 | WhiteBalance | uint16 | 0x2=Auto, 0x4=Daylight, 0x6=Incandescent, 0x8001..3=Fluo, 0x8006=Shade, 0x8007=Kelvin, 0x8008..A=Custom1-3, 0x8020/21=AutoWhite/Ambience | idem | F, I |
| 0xD19A | WBShiftR | int16 | −9..+9, valeur directe (PAS ×10) ; écrire Kelvin AVANT sinon reset | idem | F, I |
| 0xD19B | WBShiftB | int16 | −9..+9, idem | idem | F, I |
| 0xD19C | WBColorTemperature | uint16 | Kelvin (n'écrire que si WB=0x8007) | idem | F, I |
| 0xD19D | HighlightTone | int16 | ×10 (+1.5 → 15) ; X-T30 : −2..+4 | idem | F, I |
| 0xD19E | ShadowTone | int16 | ×10 ; X-T30 : −2..+4 | idem | F, I |
| 0xD19F | Color (saturation) | int16 | ×10 | idem | F, I |
| 0xD1A0 | Sharpness | int16 | ×10 | idem | F, I |
| 0xD1A1 | HighISONR | uint16 | non-linéaire : 0→8192, +1→4096, +2→0, +3→24576, +4→20480, −1→12288, −2→16384, −3→28672, −4→32768 | idem | F, I |
| 0xD1A2 | Clarity | int16 | ×10 — **absent du X-T30** en théorie | idem | F, I |
| 0xD1A3..0xD1A5 | ? | ? | Non mappés | — | I |

Sentinelle int16 −32768 = « valeur par défaut/inconnue » (I).

## Bloc conversion RAW (X RAW Studio) — testé sur X-T30 par rawji

| ID | Nom | Rôle |
|----|-----|------|
| 0xD183 | StartRawConversion | Set 1 = conversion pleine résolution, 0 = aperçu |
| 0xD184 | IOPCode | Identifiant processeur (X-T30 : `FF159502`) |
| 0xD185 | RawConvProfile | Profil binaire (X-T30 : natif 605 octets, accepte le standard 628 octets, params à l'offset 0x201) |
| 0xD186 / 0xD187 | TetherRawCondition/CompatibilityCode | Contrôle de compatibilité RAF (RAF d'un autre boîtier → erreur 0x2002) |

## Divers

| ID | Nom | Note |
|----|-----|------|
| 0xD16E | USBMode | 5=Tether, 6=RawConv, 8=Webcam ; absent en mode card reader/MTP (L) |
| 0xD15D | SetUSBMode | (G) |
| 0xD153 | FirmwareVersion | absent sur X-S10 (F) |
| 0xD21C | ? | vu uniquement dans un bloc expérimental de libfuji, avec 0xD18C/D |
| 0xD34C | CustomSetting | piste alternative banques persos (G, jamais exploitée) |
| 0xD36B | BatteryInfo2 | chaîne batterie (F) ; absent sur X-E5 |

## Codes film simulation (0xD192 et EXIF)

1 Provia, 2 Velvia, 3 Astia, 4 ProNeg.Hi, 5 ProNeg.Std, 6–9 Monochrome STD/Y/R/G, 10 Sepia, 11 Classic Chrome, 12–15 Acros STD/Y/R/G, 16 Eterna, 17 Classic Negative*, 18 Eterna Bleach Bypass*, 19 Nostalgic Neg.*, 20 Reala Ace*.
(*) indisponibles sur X-T30 gen 1 — l'enum du descripteur 0xD192 renvoyé par le probe fera foi.

## Séquences documentées (pour référence, phase écriture = plus tard)

- **Lecture d'un slot** (I) : OpenSession → GetDeviceInfo → Set 0xD18C=slot → attendre ~100 ms → Get 0xD18D + bloc 0xD18E..0xD1A5.
- **Lecture « live »** (I) : lire le bloc SANS toucher au sélecteur = réglages actifs courants. **C'est ce que fait notre probe (0 écriture).**
- **Écriture** (F/I) : sélecteur → nom (F: en premier / I: en dernier) → FilmSimulation d'abord → DRPriority avant DR → Kelvin avant WB shifts → propriétés couleur omises pour les simus mono.
