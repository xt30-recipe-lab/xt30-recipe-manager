// ============================================================================
// xt30-backup-decoder — Lecteur du fichier de réglages Fujifilm (C1–C7)
//
// CE QUE C'EST : un analyseur de FICHIER, rien d'autre.
//   - aucune commande PTP, aucun accès USB/WPD, aucun using vers le moteur ;
//   - il ne sait QUE lire : aucune fonction d'écriture n'existe dans ce fichier ;
//   - il ne touche jamais l'appareil. Le blob lui est fourni par l'utilisateur
//     (export officiel Fujifilm X Acquire) ou, plus tard et seulement après
//     autorisation explicite, par une lecture PTP séparée.
//
// STRUCTURE DÉCODÉE : layout « gen4-early » (X-T3 / X-T30, X-Processor 4) et
// « gen3 » (X100F, X-Pro2, X-T2, X-T20, X-E3), rétro-ingéniérés par le projet
// grawji (MIT) par diffs contrôlés sur du matériel réel.
//
//   ATTENTION : ces offsets sont vérifiés sur X-T3 et X100F, PAS sur X-T30.
//   Le décodeur valide signature + modèle + taille et refuse de deviner.
//
// Usage :
//   xt30-backup-decoder --self-test
//   xt30-backup-decoder <fichier.dat> [--out <dossier>] [--hex]
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Xt30BackupDecoder
{
    // ------------------------- JSON minimal (autonome) -------------------------
    public static class Json
    {
        public static string Serialize(object o) { StringBuilder sb = new StringBuilder(); Write(sb, o, 0); return sb.ToString(); }
        static void Indent(StringBuilder sb, int n) { sb.Append('\n'); for (int i = 0; i < n; i++) sb.Append("  "); }
        static void Write(StringBuilder sb, object o, int depth)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string) { WriteString(sb, (string)o); return; }
            if (o is bool) { sb.Append(((bool)o) ? "true" : "false"); return; }
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d != null)
            {
                sb.Append('{'); bool first = true;
                foreach (KeyValuePair<string, object> kv in d)
                { if (!first) sb.Append(','); first = false; Indent(sb, depth + 1); WriteString(sb, kv.Key); sb.Append(": "); Write(sb, kv.Value, depth + 1); }
                if (!first) Indent(sb, depth); sb.Append('}'); return;
            }
            System.Collections.IEnumerable en = o as System.Collections.IEnumerable;
            if (en != null)
            {
                sb.Append('['); bool first = true;
                foreach (object item in en) { if (!first) sb.Append(','); first = false; Indent(sb, depth + 1); Write(sb, item, depth + 1); }
                if (!first) Indent(sb, depth); sb.Append(']'); return;
            }
            sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
        }
        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c); else sb.Append(c); break;
                }
            }
            sb.Append('"');
        }
    }

    // ------------------------- Layout des banques -------------------------
    public sealed class BankLayout
    {
        public string Name;
        public int BlobSize;
        public int Slots;
        public int Sim0;          // offset absolu de l'octet « film simulation » de C1
        public int Stride;        // pas entre deux banques
        public int NameRel;       // offset du nom relatif à Sim0 (-1 = banques non nommables)
        public int NameMax = 16;
        public Dictionary<string, int> Rel = new Dictionary<string, int>();
        public Dictionary<int, string> FilmSims = new Dictionary<int, string>();
        public string VerifiedOn;

        public int SlotBase(int slot) { return Sim0 + slot * Stride; }
    }

    public static class Layouts
    {
        // Enum film simulation du blob de sauvegarde — DIFFÉRENT de celui du PTP
        // temps réel (0xD192) et de celui du profil d185.
        static Dictionary<int, string> Gen4Sims()
        {
            return new Dictionary<int, string> {
                {0,"Provia / Standard"},{1,"Astia / Soft"},{3,"Velvia / Vivid"},{5,"Sepia"},
                {7,"Monochrome"},{8,"Monochrome + R"},{9,"Monochrome + Ye"},{10,"Monochrome + G"},
                {11,"Pro Neg. Std"},{12,"Pro Neg. Hi"},{13,"Classic Chrome"},
                {14,"ACROS"},{15,"ACROS + R"},{16,"ACROS + Ye"},{17,"ACROS + G"},{18,"Eterna"}
            };
        }
        static Dictionary<int, string> Gen3Sims()
        {
            Dictionary<int, string> sims = Gen4Sims();
            sims.Remove(18); // Eterna absent des boîtiers gen3
            return sims;
        }

        // X-T3 / X-T30 (X-Processor 4). Offsets vérifiés sur X-T3 par grawji.
        public static BankLayout Gen4Early()
        {
            BankLayout l = new BankLayout {
                Name = "gen4-early", BlobSize = 33404, Slots = 7, Sim0 = 31658, Stride = 256,
                NameRel = 67, FilmSims = Gen4Sims(), VerifiedOn = "X-T3 (grawji, hardware diffs)"
            };
            l.Rel["wb_mode"] = -34; l.Rel["wb_kelvin"] = -33; l.Rel["nr"] = -8; l.Rel["dr"] = 4;
            l.Rel["color"] = 9; l.Rel["sharpness"] = 11; l.Rel["highlight"] = 12;
            l.Rel["shadow"] = 13; l.Rel["color_chrome"] = 14; l.Rel["grain"] = 15;
            return l;
        }

        // X-T30 première génération — MESURÉ sur le boîtier de ce projet le 02/09/2026.
        // Le blob fait 5 628 octets (et non 33 404 comme le X-T3), donc l'adresse absolue
        // des banques diffère ; en revanche TOUS les décalages relatifs de champs sont
        // identiques au gen4-early du X-T3, ce qui valide la structure d'enregistrement
        // de grawji. Ancre trouvée en repérant l'octet « film simulation » constant
        // (13 = Classic Chrome) puis vérifiée par les noms de banques en clair à +78.
        public static BankLayout Xt30()
        {
            BankLayout l = new BankLayout {
                Name = "xt30-gen1", BlobSize = 5628, Slots = 7, Sim0 = 3882, Stride = 256,
                NameRel = 78, NameMax = 32, FilmSims = Gen4Sims(),
                VerifiedOn = "X-T30 gen 1 firmware 1.00 (mesure directe, ce projet)"
            };
            l.Rel["wb_mode"] = -34; l.Rel["wb_kelvin"] = -33; l.Rel["nr"] = -8; l.Rel["dr"] = 4;
            // +8 = priorité de plage dynamique. Champ absent des layouts de grawji,
            // identifié ici : C1 = 0 avec « DR-P AUTO » au menu, C2 = 3 avec « OFF ».
            l.Rel["dr_priority"] = 8;
            l.Rel["color"] = 9; l.Rel["sharpness"] = 11; l.Rel["highlight"] = 12;
            l.Rel["shadow"] = 13; l.Rel["color_chrome"] = 14; l.Rel["grain"] = 15;
            return l;
        }

        // X100F / X-Pro2 / X-T2 / X-T20 / X-E3 (X-Processor Pro), banques sans nom.
        public static BankLayout Gen3()
        {
            BankLayout l = new BankLayout {
                Name = "gen3", BlobSize = 5660, Slots = 7, Sim0 = 3909, Stride = 256,
                NameRel = -1, FilmSims = Gen3Sims(), VerifiedOn = "X100F (grawji, hardware diffs)"
            };
            l.Rel["wb_mode"] = -33; l.Rel["wb_kelvin"] = -32; l.Rel["nr"] = -7; l.Rel["dr"] = 3;
            l.Rel["color"] = 7; l.Rel["sharpness"] = 9; l.Rel["highlight"] = 10;
            l.Rel["shadow"] = 11; l.Rel["grain"] = 12;
            return l;
        }

        // Modèle EXIF normalisé -> layout. Tout modèle absent renvoie null : on
        // n'invente jamais un layout pour un boîtier non cartographié.
        public static BankLayout For(string model)
        {
            if (model == null) return null;
            string key = "";
            foreach (char c in model.ToUpperInvariant().Replace("FUJIFILM", ""))
                if (char.IsLetterOrDigit(c)) key += c;
            switch (key)
            {
                case "XT30": return Xt30();
                case "XT3": return Gen4Early();
                case "X100F": case "XPRO2": case "XT2": case "XT20": case "XE3": return Gen3();
                default: return null;
            }
        }
    }

    // ------------------------- Décodage des valeurs -------------------------
    public static class Decode
    {
        static readonly int[] KelvinAsc = {
            2500,2550,2650,2700,2800,2850,2950,3000,3100,3200,3300,3400,3600,3700,3800,4000,
            4200,4300,4500,4800,5000,5300,5600,5900,6300,6700,7100,7700,8300,9100,10000 };

        public static string Tone(byte b)
        {
            int v = 4 - b;                       // encodage inverse : code = 4 - valeur
            if (v < -8 || v > 8) return "unknown (0x" + b.ToString("X2") + ")";
            return (v > 0 ? "+" : "") + v.ToString(CultureInfo.InvariantCulture);
        }
        public static string NoiseReduction(byte b)
        {
            int v = b - 4;
            if (v < -8 || v > 8) return "unknown (0x" + b.ToString("X2") + ")";
            return (v > 0 ? "+" : "") + v.ToString(CultureInfo.InvariantCulture);
        }
        public static string Color(byte b)
        {
            switch (b)
            {
                case 3: return "+4"; case 4: return "+3"; case 5: return "+2"; case 6: return "+1";
                case 0: return "0"; case 8: return "-1"; case 7: return "-2"; case 9: return "-3"; case 10: return "-4";
                default: return "unknown (0x" + b.ToString("X2") + ")";
            }
        }
        public static string Grain(byte b)
        {
            switch (b) { case 0: return "Strong"; case 1: return "Weak"; case 2: return "Off"; default: return "unknown (0x" + b.ToString("X2") + ")"; }
        }
        public static string Chrome(byte b)
        {
            switch (b) { case 0: return "Off"; case 1: return "Weak"; case 2: return "Strong"; default: return "unknown (0x" + b.ToString("X2") + ")"; }
        }
        public static string DynamicRange(byte b)
        {
            switch (b)
            {
                case 1: return "DR100"; case 2: return "DR200"; case 3: return "DR400";
                // 0 = DR-P : la priorité de plage dynamique pilote la plage dynamique.
                // CONFIRMÉ le 02/09/2026 en comparant la banque C1 avec le menu du boîtier
                // (l'appareil y affiche « DR-P » pour la plage dynamique ET pour les tons
                // lumière/ombre, qu'elle neutralise).
                case 0: return "DR-P (set by dynamic range priority)";
                default: return "unknown (0x" + b.ToString("X2") + ")";
            }
        }
        // Codes confirmés le 02/09/2026 contre le menu du boîtier : 0 = AUTO (banque C1),
        // 3 = OFF (banque C2). Les codes 1 et 2 n'ont pas été observés : on les affiche
        // comme non confirmés plutôt que de supposer « faible / fort ».
        public static string DRangePriority(byte b)
        {
            switch (b)
            {
                case 0: return "AUTO";
                case 3: return "Off";
                default: return "code " + b + " (non confirmé)";
            }
        }

        public static string WhiteBalance(byte b)
        {
            switch (b)
            {
                case 0: return "Auto"; case 1: return "Daylight"; case 2: return "Shade";
                case 3: return "Fluorescent 1"; case 4: return "Fluorescent 2"; case 5: return "Fluorescent 3";
                case 6: return "Incandescent"; case 7: return "Underwater"; case 8: return "Color temperature";
                default: return "unknown (0x" + b.ToString("X2") + ")";
            }
        }
        // Le blob stocke un index dans la liste DÉCROISSANTE depuis 10000 K.
        public static string Kelvin(byte b)
        {
            int index = KelvinAsc.Length - 1 - b;
            if (index < 0 || index >= KelvinAsc.Length) return "unknown index " + b;
            return KelvinAsc[index].ToString(CultureInfo.InvariantCulture) + "K";
        }
        public static string FilmSim(BankLayout layout, byte b)
        {
            string name;
            return layout.FilmSims.TryGetValue(b, out name) ? name : "unknown (0x" + b.ToString("X2") + ")";
        }
    }

    // ------------------------- Résultat -------------------------
    public sealed class BankSettings
    {
        public int Number;
        public string Name;
        public Dictionary<string, object> Values = new Dictionary<string, object>();
        public Dictionary<string, object> RawBytes = new Dictionary<string, object>();
    }

    // Somme de contrôle du fichier de réglages X-T30.
    //
    // RÉSOLUE le 02/09/2026 sur DEUX fichiers réels : le boîtier a été relu après
    // avoir changé un seul réglage au menu (netteté de C7, -1 -> 0). Les octets
    // modifiés étaient 176/177 (le champ lui-même), 1149, 1151, 3772 et 5429.
    // Le total additif reproduit exactement les deux fichiers en incluant 1149,
    // 1151 et 5429, et en excluant 3772 (une donnée d'objectif que l'appareil
    // régénère) ainsi que les deux octets du champ.
    //
    // Cette classe ne sert QU'À VÉRIFIER un fichier lu. Rien ici n'écrit.
    public static class BlobChecksum
    {
        public const int Offset = 176;          // u16 little-endian
        public const int PayloadStart = 0;      // total sur tout le fichier
        public const int Bias = 0xE1E5;
        public static readonly int[] Excluded = { 176, 177, 3772 };

        public static int Stored(byte[] blob)
        {
            if (blob == null || blob.Length < Offset + 2) return -1;
            return blob[Offset] | (blob[Offset + 1] << 8);
        }

        public static int Computed(byte[] blob)
        {
            if (blob == null || blob.Length < Offset + 2) return -1;
            int total = 0;
            for (int i = PayloadStart; i < blob.Length; i++)
            {
                bool skip = false;
                for (int e = 0; e < Excluded.Length; e++) if (Excluded[e] == i) { skip = true; break; }
                if (!skip) total += blob[i];
            }
            return (total + Bias) & 0xFFFF;
        }

        public static bool Matches(byte[] blob) { return Stored(blob) == Computed(blob); }
    }

    public sealed class BlobReport
    {
        public string SourceFile;
        public long Size;
        public bool MagicOk;
        public string Model;
        public string Serial;
        public string LayoutName;
        public string LayoutVerifiedOn;
        public bool LayoutMatches;
        public int ChecksumStored = -1, ChecksumComputed = -1;
        public bool ChecksumOk;
        public string Warning = "";
        public List<BankSettings> Banks = new List<BankSettings>();
    }

    public static class BlobReader
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("FUJIFILM");
        const int ModelOffset = 0x14, SerialOffset = 0x34;

        public static string AsciiAt(byte[] blob, int offset, int max)
        {
            if (offset < 0 || offset >= blob.Length) return "";
            int end = Math.Min(blob.Length, offset + max);
            StringBuilder sb = new StringBuilder();
            for (int i = offset; i < end; i++)
            {
                if (blob[i] == 0) break;
                if (blob[i] < 0x20 || blob[i] > 0x7E) return sb.ToString().Trim();
                sb.Append((char)blob[i]);
            }
            return sb.ToString().Trim();
        }

        public static BlobReport Read(byte[] blob, string sourceName)
        {
            BlobReport report = new BlobReport { SourceFile = sourceName, Size = blob.Length };
            report.MagicOk = blob.Length >= Magic.Length;
            for (int i = 0; report.MagicOk && i < Magic.Length; i++) if (blob[i] != Magic[i]) report.MagicOk = false;
            if (!report.MagicOk)
            {
                report.Warning = "Not a Fujifilm settings backup: the file does not start with the ASCII signature FUJIFILM.";
                return report;
            }
            report.Model = AsciiAt(blob, ModelOffset, SerialOffset - ModelOffset);
            report.Serial = AsciiAt(blob, SerialOffset, 32);

            BankLayout layout = Layouts.For(report.Model);
            if (layout == null)
            {
                report.Warning = "No verified bank layout for model \"" + report.Model + "\". Refusing to guess offsets.";
                return report;
            }
            report.LayoutName = layout.Name;
            report.LayoutVerifiedOn = layout.VerifiedOn;
            if (layout.Name == "xt30-gen1")
            {
                report.ChecksumStored = BlobChecksum.Stored(blob);
                report.ChecksumComputed = BlobChecksum.Computed(blob);
                report.ChecksumOk = report.ChecksumStored == report.ChecksumComputed;
            }
            report.LayoutMatches = blob.Length == layout.BlobSize;
            if (!report.LayoutMatches)
            {
                report.Warning = string.Format(
                    "Size mismatch: the file is {0} bytes, the verified {1} layout expects {2}. " +
                    "The offsets below would be meaningless, so no bank was decoded.",
                    blob.Length, layout.Name, layout.BlobSize);
                return report;
            }
            int last = layout.SlotBase(layout.Slots - 1) + 64;
            if (last >= blob.Length)
            {
                report.Warning = "Layout overruns the file; no bank decoded.";
                return report;
            }

            for (int slot = 0; slot < layout.Slots; slot++)
            {
                int b = layout.SlotBase(slot);
                BankSettings bank = new BankSettings { Number = slot + 1 };
                bank.Name = layout.NameRel >= 0 ? AsciiAt(blob, b + layout.NameRel, layout.NameMax) : null;
                bank.Values["Film Simulation"] = Decode.FilmSim(layout, blob[b]);
                Add(bank, blob, b, layout, "dr", "Dynamic Range", Decode.DynamicRange);
                Add(bank, blob, b, layout, "dr_priority", "Dynamic Range Priority", Decode.DRangePriority);
                Add(bank, blob, b, layout, "highlight", "Highlight", Decode.Tone);
                Add(bank, blob, b, layout, "shadow", "Shadow", Decode.Tone);
                Add(bank, blob, b, layout, "color", "Color", Decode.Color);
                Add(bank, blob, b, layout, "sharpness", "Sharpness", Decode.Tone);
                Add(bank, blob, b, layout, "nr", "Noise Reduction", Decode.NoiseReduction);
                Add(bank, blob, b, layout, "grain", "Grain Effect", Decode.Grain);
                Add(bank, blob, b, layout, "color_chrome", "Color Chrome Effect", Decode.Chrome);
                Add(bank, blob, b, layout, "wb_mode", "White Balance", Decode.WhiteBalance);
                // La température n'a de sens que si la balance des blancs est réglée
                // dessus (code 8) : sinon l'octet vaut 0 et afficher « 10000K » serait
                // une valeur inventée. On expose alors l'octet brut sans l'interpréter.
                int wbRel, kRel;
                if (layout.Rel.TryGetValue("wb_mode", out wbRel) && layout.Rel.TryGetValue("wb_kelvin", out kRel))
                {
                    byte wbRaw = blob[b + wbRel], kRaw = blob[b + kRel];
                    bank.Values["Color Temperature"] = wbRaw == 8 ? Decode.Kelvin(kRaw) : "not applicable";
                    bank.RawBytes["Color Temperature@" + (b + kRel)] = kRaw;
                }
                bank.RawBytes["film_sim@" + b] = blob[b];
                report.Banks.Add(bank);
            }
            return report;
        }

        static void Add(BankSettings bank, byte[] blob, int baseOffset, BankLayout layout,
                        string key, string label, Func<byte, string> decoder)
        {
            int rel;
            if (!layout.Rel.TryGetValue(key, out rel)) return;
            int offset = baseOffset + rel;
            if (offset < 0 || offset >= blob.Length) return;
            byte raw = blob[offset];
            bank.Values[label] = decoder(raw);
            bank.RawBytes[label + "@" + offset] = raw;
        }

        public static Dictionary<string, object> ToJson(BlobReport report)
        {
            List<object> banks = new List<object>();
            foreach (BankSettings bank in report.Banks)
                banks.Add(new Dictionary<string, object> {
                    { "slot", "C" + bank.Number }, { "name", bank.Name },
                    { "settings", bank.Values }, { "rawBytes", bank.RawBytes } });
            return new Dictionary<string, object> {
                { "tool", "xt30-backup-decoder" }, { "readOnly", true },
                { "generatedAt", DateTime.Now.ToString("o") },
                { "sourceFile", report.SourceFile }, { "sizeBytes", report.Size },
                { "signatureFujifilm", report.MagicOk }, { "model", report.Model }, { "serial", report.Serial },
                { "layout", report.LayoutName }, { "layoutVerifiedOn", report.LayoutVerifiedOn },
                { "layoutSizeMatches", report.LayoutMatches },
                { "checksumStored", report.ChecksumStored }, { "checksumComputed", report.ChecksumComputed },
                { "checksumOk", report.ChecksumOk },
                { "warning", report.Warning },
                { "banks", banks } };
        }
    }

    // ------------------------- Programme -------------------------
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("xt30-backup-decoder — lecture seule d'un fichier de reglages Fujifilm");
                Console.WriteLine("  xt30-backup-decoder --self-test");
                Console.WriteLine("  xt30-backup-decoder <fichier.dat> [--out <dossier>]");
                Console.WriteLine();
                Console.WriteLine("Ce programme n'envoie AUCUNE commande a l'appareil : il analyse un fichier.");
                return 0;
            }
            if (args[0] == "--self-test") return SelfTest();

            string path = args[0];
            string outDir = Path.GetDirectoryName(Path.GetFullPath(path));
            for (int i = 1; i < args.Length; i++) if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
            if (!File.Exists(path)) { Console.WriteLine("Fichier introuvable : " + path); return 2; }

            byte[] blob = File.ReadAllBytes(path);
            BlobReport report = BlobReader.Read(blob, Path.GetFileName(path));
            Print(report);
            Directory.CreateDirectory(outDir);
            string json = Path.Combine(outDir, "xt30_camera_banks.json");
            File.WriteAllText(json, Json.Serialize(BlobReader.ToJson(report)), new UTF8Encoding(false));
            Console.WriteLine();
            Console.WriteLine("Rapport ecrit : " + json);
            return report.Banks.Count > 0 ? 0 : 3;
        }

        static void Print(BlobReport report)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("XT30 BACKUP DECODER — LECTURE SEULE (analyse de fichier)");
            Console.WriteLine("=====================================================");
            Console.WriteLine("Fichier      : {0} ({1} octets)", report.SourceFile, report.Size);
            Console.WriteLine("Signature    : {0}", report.MagicOk ? "FUJIFILM OK" : "ABSENTE");
            Console.WriteLine("Modele       : {0}", string.IsNullOrEmpty(report.Model) ? "(non lu)" : report.Model);
            Console.WriteLine("Numero serie : {0}", string.IsNullOrEmpty(report.Serial) ? "(non lu)" : report.Serial);
            if (report.LayoutName != null)
                Console.WriteLine("Layout       : {0} (verifie sur {1}) — taille attendue : {2}",
                    report.LayoutName, report.LayoutVerifiedOn, report.LayoutMatches ? "OUI" : "NON");
            if (report.ChecksumStored >= 0)
                Console.WriteLine("Somme controle: stockee 0x{0:X4}, calculee 0x{1:X4} -> {2}",
                    report.ChecksumStored, report.ChecksumComputed, report.ChecksumOk ? "COHERENTE" : "INCOHERENTE");
            if (report.Warning != "") { Console.WriteLine(); Console.WriteLine("ATTENTION : " + report.Warning); }
            foreach (BankSettings bank in report.Banks)
            {
                Console.WriteLine();
                Console.WriteLine("C{0}  {1}", bank.Number, bank.Name == null ? "(banques non nommables sur ce boitier)" : "\"" + bank.Name + "\"");
                foreach (KeyValuePair<string, object> kv in bank.Values)
                    Console.WriteLine("    {0,-22}: {1}", kv.Key, kv.Value);
            }
        }

        // ---------------- Auto-test hors ligne, sans fichier ni appareil ----------------
        static int SelfTest()
        {
            List<string> failures = new List<string>();
            BankLayout layout = Layouts.Xt30();

            // 1) Blob synthétique X-T30 avec des valeurs connues dans chaque banque.
            byte[] blob = new byte[layout.BlobSize];
            byte[] magic = Encoding.ASCII.GetBytes("FUJIFILM");
            Array.Copy(magic, blob, magic.Length);
            byte[] model = Encoding.ASCII.GetBytes("X-T30");
            Array.Copy(model, 0, blob, 0x14, model.Length);
            byte[] serial = Encoding.ASCII.GetBytes("SYNTHETIC12345");
            Array.Copy(serial, 0, blob, 0x34, serial.Length);

            string[] names = { "PORTRA", "GOLD 200", "KODACHROME", "SUMMER", "CUBAN", "PACIFIC", "NIGHT" };
            for (int slot = 0; slot < layout.Slots; slot++)
            {
                int b = layout.SlotBase(slot);
                byte[] nameBytes = Encoding.ASCII.GetBytes(names[slot]);
                Array.Copy(nameBytes, 0, blob, b + layout.NameRel, nameBytes.Length);
                blob[b] = 13;                                  // Classic Chrome
                blob[b + layout.Rel["dr"]] = 3;                // DR400
                blob[b + layout.Rel["dr_priority"]] = 0;       // AUTO
                blob[b + layout.Rel["highlight"]] = 2;         // 4-2 = +2
                blob[b + layout.Rel["shadow"]] = 6;            // 4-6 = -2
                blob[b + layout.Rel["color"]] = 5;             // +2
                blob[b + layout.Rel["sharpness"]] = 5;         // 4-5 = -1
                blob[b + layout.Rel["nr"]] = 0;                // 0-4 = -4
                blob[b + layout.Rel["grain"]] = 1;             // Weak
                blob[b + layout.Rel["color_chrome"]] = 2;      // Strong
                blob[b + layout.Rel["wb_mode"]] = 8;           // Color temperature
                blob[b + layout.Rel["wb_kelvin"]] = 8;         // index descendant -> 5600K
            }

            BlobReport report = BlobReader.Read(blob, "synthetic");
            Check(failures, report.MagicOk, "signature reconnue");
            Check(failures, report.Model == "X-T30", "modele X-T30 lu a 0x14 (obtenu : " + report.Model + ")");
            Check(failures, report.Serial == "SYNTHETIC12345", "numero de serie lu a 0x34");
            Check(failures, report.LayoutName == "xt30-gen1", "layout xt30-gen1 selectionne (obtenu : " + report.LayoutName + ")");
            Check(failures, Layouts.For("X-T3").Name == "gen4-early" && Layouts.For("X-T3").BlobSize == 33404,
                "le X-T3 garde son propre layout de 33404 octets");
            Check(failures, report.LayoutMatches, "taille conforme au layout");
            Check(failures, report.Banks.Count == 7, "sept banques decodees");
            if (report.Banks.Count == 7)
            {
                BankSettings c1 = report.Banks[0];
                Check(failures, c1.Name == "PORTRA", "nom de C1 (obtenu : " + c1.Name + ")");
                Check(failures, report.Banks[6].Name == "NIGHT", "nom de C7");
                Check(failures, (string)c1.Values["Film Simulation"] == "Classic Chrome", "film simulation");
                Check(failures, (string)c1.Values["Dynamic Range"] == "DR400", "dynamic range");
                Check(failures, (string)c1.Values["Dynamic Range Priority"] == "AUTO", "priorite de plage dynamique");
                Check(failures, Decode.DRangePriority(3) == "Off" && Decode.DRangePriority(1).Contains("non confirm"),
                    "codes DR-P : 0=AUTO et 3=Off confirmes, 1 et 2 signales comme non confirmes");
                Check(failures, Decode.DynamicRange(0).StartsWith("DR-P") && Decode.DynamicRange(1) == "DR100",
                    "plage dynamique : 0=DR-P et 1=DR100 confirmes sur le boitier");
                Check(failures, (string)c1.Values["Highlight"] == "+2", "highlight (obtenu : " + c1.Values["Highlight"] + ")");
                Check(failures, (string)c1.Values["Shadow"] == "-2", "shadow");
                Check(failures, (string)c1.Values["Color"] == "+2", "color");
                Check(failures, (string)c1.Values["Sharpness"] == "-1", "sharpness");
                Check(failures, (string)c1.Values["Noise Reduction"] == "-4", "noise reduction");
                Check(failures, (string)c1.Values["Grain Effect"] == "Weak", "grain");
                Check(failures, (string)c1.Values["Color Chrome Effect"] == "Strong", "color chrome");
                Check(failures, (string)c1.Values["White Balance"] == "Color temperature", "white balance");
                Check(failures, (string)c1.Values["Color Temperature"] == "5600K", "kelvin index 8 (obtenu : " + c1.Values["Color Temperature"] + ")");
            }
            // Point de reference materiel de grawji : index 10 -> 5000 K sur X100F/X-T3.
            Check(failures, Decode.Kelvin(10) == "5000K", "kelvin index 10 = 5000K (repere materiel grawji, obtenu : " + Decode.Kelvin(10) + ")");
            Check(failures, Decode.Kelvin(0) == "10000K" && Decode.Kelvin(30) == "2500K", "bornes kelvin (index 0 = 10000K, index 30 = 2500K)");
            {
            }

            // 1bis) Vérificateur de somme de contrôle : cohérent, sensible aux
            // modifications, et insensible au seul octet exclu (3772).
            byte[] signed = (byte[])blob.Clone();
            int value = BlobChecksum.Computed(signed);
            signed[BlobChecksum.Offset] = (byte)(value & 0xFF);
            signed[BlobChecksum.Offset + 1] = (byte)((value >> 8) & 0xFF);
            Check(failures, BlobChecksum.Matches(signed), "somme de controle reconnue apres inscription");
            byte[] tampered = (byte[])signed.Clone();
            tampered[layout.SlotBase(0) + layout.Rel["sharpness"]] ^= 0x01;
            Check(failures, !BlobChecksum.Matches(tampered), "une valeur modifiee invalide la somme de controle");
            byte[] lensTouched = (byte[])signed.Clone();
            lensTouched[3772] ^= 0xFF;
            Check(failures, BlobChecksum.Matches(lensTouched), "l'octet 3772 exclu n'affecte pas la somme de controle");

            // 2) Un fichier qui n'est pas un backup doit être refusé.
            BlobReport bad = BlobReader.Read(Encoding.ASCII.GetBytes("NOT A BACKUP FILE AT ALL........."), "bad");
            Check(failures, !bad.MagicOk && bad.Banks.Count == 0, "fichier non-Fujifilm refuse");

            // 3) Bonne signature, modèle non cartographié -> aucun décodage deviné.
            byte[] unknown = new byte[layout.BlobSize];
            Array.Copy(magic, unknown, magic.Length);
            byte[] other = Encoding.ASCII.GetBytes("X-H2S");
            Array.Copy(other, 0, unknown, 0x14, other.Length);
            BlobReport unknownReport = BlobReader.Read(unknown, "unknown-model");
            Check(failures, unknownReport.Banks.Count == 0 && unknownReport.Warning.Contains("Refusing to guess"),
                "modele non cartographie : aucun offset devine");

            // 4) Bon modèle mais mauvaise taille -> refus (protection anti-corruption d'analyse).
            byte[] shortBlob = new byte[layout.BlobSize - 100];
            Array.Copy(magic, shortBlob, magic.Length);
            Array.Copy(model, 0, shortBlob, 0x14, model.Length);
            BlobReport shortReport = BlobReader.Read(shortBlob, "short");
            Check(failures, shortReport.Banks.Count == 0 && shortReport.Warning.Contains("Size mismatch"),
                "taille inattendue : aucun decodage");

            // 5) Le layout gen3 reste distinct et sans noms de banque.
            BankLayout gen3 = Layouts.Gen3();
            Check(failures, gen3.BlobSize == 5660 && gen3.NameRel < 0 && !gen3.Rel.ContainsKey("color_chrome"),
                "layout gen3 distinct, sans noms ni color chrome");

            // 6) Aucune capacite d'ecriture : le decodeur ne modifie jamais le tableau fourni.
            byte[] copy = (byte[])blob.Clone();
            BlobReader.Read(copy, "immutability");
            bool identical = true;
            for (int i = 0; i < copy.Length && identical; i++) if (copy[i] != blob[i]) identical = false;
            Check(failures, identical, "le blob fourni n'est jamais modifie");

            Console.WriteLine();
            if (failures.Count == 0)
            {
                Console.WriteLine("SELF-TEST OK : signature, modele, layout, 7 banques, noms, toutes les valeurs,");
                Console.WriteLine("refus des fichiers etrangers / modeles inconnus / tailles inattendues, immutabilite.");
                Console.WriteLine("Aucune commande appareil n'existe dans cet outil.");
                return 0;
            }
            Console.WriteLine("SELF-TEST EN ECHEC :");
            foreach (string f in failures) Console.WriteLine("  - " + f);
            return 1;
        }

        static void Check(List<string> failures, bool ok, string label)
        {
            Console.WriteLine("  [{0}] {1}", ok ? "OK  " : "FAIL", label);
            if (!ok) failures.Add(label);
        }
    }
}
