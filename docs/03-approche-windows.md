# Approche technique Windows — comment xt30-probe parle PTP sans toucher aux drivers

## Choix retenu : API WPD + passthrough MTP officiel de Microsoft

Quand le X-T30 est branché en mode PTP (USB RAW CONV./BACKUP RESTORE ou Tether), Windows 10
le prend en charge avec son **driver MTP intégré** (WpdMtpDr, user-mode). Microsoft fournit un
passthrough documenté permettant d'envoyer des opcodes PTP bruts à travers ce driver :
`IPortableDevice::SendCommand` + `WPD_COMMAND_MTP_EXT_EXECUTE_COMMAND_WITH_DATA_TO_READ`
(catégorie `{4D545058-1A2E-4106-A357-771E0819FC56}`), suivi de `READ_DATA` puis `END_DATA_TRANSFER`.

Avantages :
- **Aucun driver à remplacer** (pas de Zadig/WinUSB), l'Explorateur continue de fonctionner ;
- accès non exclusif, session PTP gérée par Windows (pas d'OpenSession à envoyer) ;
- même approche que `libwpd` de petabyt (utilisé par fudge sur Windows) et WpdMtpLib (Ricoh Theta).

Écarté : PyUSB/ptpy et node-usb (exigent un remplacement de driver via Zadig), libgphoto2 (pas de
port Windows fiable), MediaDevices/Bassman2 (API fichiers, pas de passthrough).

## Implémentation

- Un seul fichier `xt30-probe/Probe.cs`, compilé par le csc.exe intégré à Windows
  (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) — aucune installation, aucune
  dépendance binaire externe : tout le code est lisible et auditable.
- Interop COM écrit à la main ; les GUID, PID de commandes et ordres de vtable ont été vérifiés
  contre les en-têtes officiels du SDK Windows 10 (copies dans `docs/reference/` :
  `WpdMtpExtensions.h`, `PortableDeviceApi.h`, `PortableDeviceTypes.h`, `PortableDevice.h`).
- Pièges gérés (documentés par le blog WPD de Microsoft « dimeby8 ») :
  - les données de READ_DATA reviennent dans les résultats (`GetBufferValue`), pas dans le buffer fourni ;
  - il faut lire la totalité de TRANSFER_TOTAL_DATA_SIZE sous peine de désynchroniser le driver ;
  - taille inconnue (0xFFFFFFFF) → lire jusqu'à un chunk court ;
  - toujours END_DATA_TRANSFER (dans un `finally`) pour récupérer le code réponse PTP.

## Garde-fou lecture seule

`MtpReadOnlyGuard.Check(opcode)` est appelé avant chaque envoi. Liste blanche :
`0x1001 GetDeviceInfo`, `0x1014 GetDevicePropDesc`, `0x1015 GetDevicePropValue`.
Tout autre opcode (dont `0x1016 SetDevicePropValue` et les vendor `0x9xxx`) lève une exception.
Le probe n'implémente d'ailleurs même pas la phase de données host→device (WITH_DATA_TO_WRITE).

## Plans B si le passthrough échoue sur le X-T30

1. Si le boîtier se lie au driver Still Image (usbscan.sys/WIA) au lieu de WpdMtpDr :
   passthrough WIA `IWiaItemExtras::Escape` + `ESCAPE_PTP_VENDOR_COMMAND` (à implémenter en v2).
   Diagnostic : la sortie de `Get-PnpDevice` demandée dans les instructions de test le révèlera.
2. En dernier recours seulement : libusb + WinUSB via Zadig (réversible mais casse l'accès MTP
   normal tant que le driver est remplacé) — à éviter.

## Note sur les modes USB du X-T30

- **USB CARD READER** : périphérique de stockage de masse → PAS de PTP. Inutilisable.
- **USB RAW CONV./BACKUP RESTORE** (0xD16E = 6) : mode utilisé par Filmcase, fujifilm-ptp-recipes,
  rawji et X RAW Studio. **Mode recommandé pour le test.**
- **USB TETHER SHOOTING FIXED/AUTO** (0xD16E = 5) : mode X Acquire ; expose un autre jeu de
  propriétés. Un second passage du probe dans ce mode est un bonus intéressant.
