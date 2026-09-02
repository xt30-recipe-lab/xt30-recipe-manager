// ============================================================================
// xt30-backup-read — Lecture du fichier de réglages du boîtier (handle 0)
//
// PORTÉE VOLONTAIREMENT MINUSCULE. Cet outil ne fait que deux appels :
//   1. GetObjectInfo (0x1008) sur le handle 0 -> métadonnées (format, taille)
//   2. GetObject     (0x1009) sur le handle 0 -> les octets, appareil -> PC
//
// Il possède son propre garde-fou en plus de MtpReadOnlyGuard de Probe.cs :
//   - seuls les opcodes 0x1008 et 0x1009 sont acceptés ;
//   - seul le handle 0 est acceptable ;
//   - aucune fonction d'écriture n'existe dans ce fichier.
//
// Il refuse de continuer si l'objet n'est pas au format 0x5000 (fichier de
// réglages), pour ne jamais télécharger autre chose par accident.
//
// Le fichier obtenu est écrit tel quel sur le disque ; rien n'est renvoyé
// à l'appareil, à aucun moment.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Xt30Probe;

namespace Xt30BackupRead
{
    public static class Program
    {
        const uint BackupHandle = 0;
        const ushort SettingsFormat = 0x5000;
        static readonly ushort[] Allowed = { 0x1008, 0x1009 };

        static void Guard(ushort opcode, uint handle)
        {
            bool ok = false;
            for (int i = 0; i < Allowed.Length; i++) if (Allowed[i] == opcode) ok = true;
            if (!ok) throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "GARDE BACKUP-READ : opcode 0x{0:X4} refuse.", opcode));
            if (handle != BackupHandle) throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "GARDE BACKUP-READ : handle 0x{0:X8} refuse (seul le handle 0 est lisible).", handle));
        }

        public static int Main(string[] args)
        {
            string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "phase2-inventory");
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);

            Console.WriteLine("=====================================================");
            Console.WriteLine("XT30 BACKUP READ — LECTURE SEULE (handle 0 uniquement)");
            Console.WriteLine("Opcodes autorises : 0x1008 GetObjectInfo, 0x1009 GetObject");
            Console.WriteLine("Aucune donnee n'est envoyee a l'appareil.");
            Console.WriteLine("=====================================================");

            string fuji = null;
            foreach (string id in MtpDevice.ListDeviceIds())
            {
                string manufacturer = MtpDevice.GetDeviceString(id, 1);
                string friendly = MtpDevice.GetDeviceString(id, 0);
                if (id.ToLowerInvariant().Contains("vid_04cb")
                    || manufacturer.ToUpperInvariant().Contains("FUJI")
                    || friendly.ToUpperInvariant().Contains("FUJI")) { fuji = id; break; }
            }
            if (fuji == null) { Console.WriteLine("Aucun appareil Fujifilm detecte."); return 2; }

            using (MtpDevice device = new MtpDevice())
            {
                device.Open(fuji);
                Console.WriteLine("Appareil ouvert.");

                ushort rc; uint[] rp;
                Guard(0x1008, BackupHandle);
                byte[] info = device.ExecuteRead(0x1008, new uint[] { BackupHandle }, out rc, out rp);
                Console.WriteLine("GetObjectInfo(0) -> {0} ({1}), {2} octets",
                    "0x" + rc.ToString("X4"), PtpReader.ResponseName(rc), info.Length);
                if (rc != 0x2001) { Console.WriteLine("L'objet de sauvegarde n'est pas disponible dans ce mode."); return 3; }

                // ObjectInfo : StorageID(4) ObjectFormat(2) ProtectionStatus(2) CompressedSize(4)
                ushort format = (ushort)(info[4] | (info[5] << 8));
                uint size = (uint)(info[8] | (info[9] << 8) | (info[10] << 16) | (info[11] << 24));
                Console.WriteLine("  format annonce : 0x{0:X4}   taille annoncee : {1} octets", format, size);
                if (format != SettingsFormat)
                {
                    Console.WriteLine("REFUS : format 0x{0:X4} != 0x5000 attendu. Rien n'est telecharge.", format);
                    return 4;
                }

                Guard(0x1009, BackupHandle);
                byte[] blob = device.ExecuteRead(0x1009, new uint[] { BackupHandle }, out rc, out rp);
                Console.WriteLine("GetObject(0)     -> {0} ({1}), {2} octets recus",
                    "0x" + rc.ToString("X4"), PtpReader.ResponseName(rc), blob.Length);
                if (rc != 0x2001 || blob.Length == 0) { Console.WriteLine("Lecture incomplete."); return 5; }

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string binPath = Path.Combine(outDir, "xt30-settings-" + stamp + ".dat");
                File.WriteAllBytes(binPath, blob);
                Console.WriteLine();
                Console.WriteLine("Fichier de reglages enregistre : " + binPath);

                // Aperçu : signature, modèle, série — sans interpréter les banques.
                Console.WriteLine();
                Console.WriteLine("Apercu :");
                Console.WriteLine("  signature      : {0}", Ascii(blob, 0, 8));
                Console.WriteLine("  modele  @0x14  : {0}", Ascii(blob, 0x14, 32));
                Console.WriteLine("  serie   @0x34  : {0}", Ascii(blob, 0x34, 32));
                Console.WriteLine("  premiers octets: {0}", Hex(blob, 0, Math.Min(48, blob.Length)));

                Dictionary<string, object> meta = new Dictionary<string, object> {
                    { "tool", "xt30-backup-read" }, { "readOnly", true },
                    { "generatedAt", DateTime.Now.ToString("o", CultureInfo.InvariantCulture) },
                    { "opcodesUsed", new object[] { "0x1008 GetObjectInfo", "0x1009 GetObject" } },
                    { "handle", 0 }, { "objectFormat", "0x" + format.ToString("X4") },
                    { "announcedSize", size }, { "receivedSize", blob.Length },
                    { "savedTo", binPath },
                    { "signature", Ascii(blob, 0, 8) }, { "model", Ascii(blob, 0x14, 32) },
                    { "bytesSentToCamera", 0 } };
                File.WriteAllText(Path.Combine(outDir, "backup-read-" + stamp + ".json"),
                    Json.Serialize(meta), new UTF8Encoding(false));
                return 0;
            }
        }

        static string Ascii(byte[] data, int offset, int max)
        {
            if (data == null || offset >= data.Length) return "";
            StringBuilder sb = new StringBuilder();
            int end = Math.Min(data.Length, offset + max);
            for (int i = offset; i < end; i++)
            {
                if (data[i] == 0) break;
                sb.Append(data[i] >= 0x20 && data[i] <= 0x7E ? (char)data[i] : '.');
            }
            return sb.ToString();
        }

        static string Hex(byte[] data, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = offset; i < offset + count && i < data.Length; i++) sb.Append(data[i].ToString("X2") + " ");
            return sb.ToString().Trim();
        }
    }
}
