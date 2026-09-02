using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xt30Probe.AppModel
{
    // Écriture d'un fichier de réglages X-T30 modifié — SUR LE DISQUE UNIQUEMENT.
    //
    // Cette classe ne parle jamais à l'appareil et n'ouvre aucune connexion USB.
    // Elle produit un fichier .dat que l'utilisateur restaure ensuite avec le
    // logiciel officiel Fujifilm (Tether App / X Acquire), dont la table de
    // compatibilité indique pour le X-T30 : « Only the BACKUP RESTORE function
    // is available ». C'est donc Fujifilm qui écrit dans le boîtier, pas nous.
    //
    // Layout et somme de contrôle : voir docs/10-piste-backup-c1c7.md, établis
    // par mesure directe sur ce boîtier et vérifiés par modification contrôlée.
    public static class CameraBankFile
    {
        public const int BlobSize = 5628;
        public const int Slots = 7;
        public const int Sim0 = 3882;
        public const int Stride = 256;
        public const int NameRel = 78;
        public const int NameMax = 25;
        public const int ChecksumOffset = 176;
        public const int ChecksumBias = 0xE1E5;
        static readonly int[] ChecksumExcluded = { 176, 177, 3772 };

        public const int RelWbMode = -34, RelWbKelvin = -33, RelNr = -8, RelDr = 4,
                         RelDrPriority = 8, RelColor = 9, RelSharpness = 11,
                         RelHighlight = 12, RelShadow = 13, RelChrome = 14, RelGrain = 15;

        static readonly int[] KelvinAsc = {
            2500,2550,2650,2700,2800,2850,2950,3000,3100,3200,3300,3400,3600,3700,3800,4000,
            4200,4300,4500,4800,5000,5300,5600,5900,6300,6700,7100,7700,8300,9100,10000 };

        public static int SlotBase(int slot) { return Sim0 + slot * Stride; }

        // Dernier fichier de réglages lu depuis le boîtier, s'il en existe un.
        public static string FindLatestSettingsFile(string baseDirectory)
        {
            try
            {
                string dir = Path.Combine(baseDirectory, "phase2-inventory");
                if (!Directory.Exists(dir)) return null;
                string[] files = Directory.GetFiles(dir, "xt30-settings-*.dat");
                if (files.Length == 0) return null;
                Array.Sort(files, delegate(string a, string c)
                { return File.GetLastWriteTime(c).CompareTo(File.GetLastWriteTime(a)); });
                return files[0];
            }
            catch (Exception) { return null; }
        }

        // ---------------- Valeurs proposées à l'utilisateur ----------------
        // Ces listes sont la référence de l'éditeur de recettes. Elles ne
        // contiennent QUE des valeurs que les encodeurs ci-dessous savent traduire :
        // une recette créée dans l'application est donc toujours transférable.

        public static readonly string[] FilmSimulations = {
            "Provia/Standard", "Astia/Soft", "Velvia/Vivid", "Classic Chrome",
            "PRO Neg. Std", "PRO Neg. Hi", "Eterna",
            "ACROS", "ACROS+R", "ACROS+Ye", "ACROS+G",
            "Monochrome", "Monochrome+R", "Monochrome+Ye", "Monochrome+G", "Sepia" };

        public static readonly string[] DynamicRanges = { "DR100", "DR200", "DR400" };
        public static readonly string[] DrPriorities = { "Off", "Auto" };
        public static readonly string[] GrainEffects = { "Off", "Weak", "Strong" };
        public static readonly string[] ChromeEffects = { "Off", "Weak", "Strong" };

        public static readonly int[] KelvinPresets = {
            2500,2550,2650,2700,2800,2850,2950,3000,3100,3200,3300,3400,3600,3700,3800,4000,
            4200,4300,4500,4800,5000,5300,5600,5900,6300,6700,7100,7700,8300,9100,10000 };

        // Modes nommés puis les températures exactes que le boîtier accepte.
        public static string[] WhiteBalances()
        {
            List<string> list = new List<string> {
                "Auto", "Daylight", "Shade", "Fluorescent 1", "Fluorescent 2", "Fluorescent 3",
                "Incandescent", "Underwater" };
            foreach (int k in KelvinPresets) list.Add(k + "K");
            return list.ToArray();
        }

        // Échelle signée « -4 … +4 » telle qu'affichée par l'appareil.
        public static string[] Scale(int min, int max)
        {
            List<string> list = new List<string>();
            for (int v = max; v >= min; v--) list.Add(v > 0 ? "+" + v : v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return list.ToArray();
        }

        // Le champ est-il transférable dans le fichier de réglages du boîtier ?
        public static bool IsTransferable(string key)
        {
            switch (key)
            {
                case "Film Simulation": case "Dynamic Range": case "Dynamic Range Priority":
                case "White Balance": case "Highlight": case "Shadow": case "Color":
                case "Sharpness": case "Noise Reduction": case "Grain Effect":
                case "Color Chrome Effect": return true;
                default: return false;   // ISO, WB Shift, Monochromatic Color… : non stockés
            }
        }

        // ---------------- Encodeurs : inverses exacts des décodeurs ----------------
        // Chacun renvoie -1 quand la valeur n'a pas de code vérifié : on n'écrit
        // JAMAIS un octet deviné, on signale le champ comme non transférable.

        public static int FilmSim(string value)
        {
            switch ((value ?? "").Trim().ToUpperInvariant().Replace(".", "").Replace(" ", ""))
            {
                case "PROVIA": case "PROVIA/STANDARD": case "STANDARD": return 0;
                case "ASTIA": case "ASTIA/SOFT": case "SOFT": return 1;
                case "VELVIA": case "VELVIA/VIVID": case "VIVID": return 3;
                case "SEPIA": return 5;
                case "MONOCHROME": return 7;
                case "MONOCHROME+R": return 8;
                case "MONOCHROME+YE": return 9;
                case "MONOCHROME+G": return 10;
                case "PRONEGSTD": return 11;
                case "PRONEGHI": return 12;
                case "CLASSICCHROME": return 13;
                case "ACROS": return 14;
                case "ACROS+R": return 15;
                case "ACROS+YE": return 16;
                case "ACROS+G": return 17;
                case "ETERNA": case "ETERNA/CINEMA": return 18;
                default: return -1;
            }
        }

        public static int Tone(string value)   // highlight / shadow / sharpness
        {
            int v;
            if (!TryNumber(value, out v) || v < -4 || v > 4) return -1;
            return 4 - v;
        }

        public static int NoiseReduction(string value)
        {
            int v;
            if (!TryNumber(value, out v) || v < -4 || v > 4) return -1;
            return v + 4;
        }

        public static int Color(string value)
        {
            int v;
            if (!TryNumber(value, out v)) return -1;
            switch (v)
            {
                case 4: return 3; case 3: return 4; case 2: return 5; case 1: return 6;
                case 0: return 0; case -1: return 8; case -2: return 7; case -3: return 9; case -4: return 10;
                default: return -1;
            }
        }

        public static int Grain(string value)
        {
            switch (Clean(value)) { case "OFF": return 2; case "WEAK": return 1; case "STRONG": return 0; default: return -1; }
        }

        public static int Chrome(string value)
        {
            switch (Clean(value)) { case "OFF": return 0; case "WEAK": return 1; case "STRONG": return 2; default: return -1; }
        }

        public static int DynamicRange(string value)
        {
            switch (Clean(value))
            {
                case "DR100": case "100": return 1;
                case "DR200": case "200": return 2;
                case "DR400": case "400": return 3;
                default: return -1;   // « DR-P » ne s'écrit pas : c'est la priorité qui l'impose
            }
        }

        public static int DrPriority(string value)
        {
            // Seuls deux codes ont été observés sur ce boîtier : on refuse le reste.
            switch (Clean(value)) { case "AUTO": return 0; case "OFF": case "0": return 3; default: return -1; }
        }

        public static int WhiteBalance(string value)
        {
            string s = Clean(value);
            if (s.EndsWith("K") && s.Length > 1) { int k; if (TryNumber(s.Substring(0, s.Length - 1), out k)) return 8; }
            switch (s)
            {
                case "AUTO": return 0;
                case "DAYLIGHT": case "SUNNY": case "FINE": return 1;
                case "SHADE": return 2;
                case "FLUORESCENT1": case "FLUO1": return 3;
                case "FLUORESCENT2": case "FLUO2": return 4;
                case "FLUORESCENT3": case "FLUO3": return 5;
                case "INCANDESCENT": case "TUNGSTEN": return 6;
                case "UNDERWATER": return 7;
                case "COLORTEMPERATURE": case "KELVIN": case "TEMPERATURE": return 8;
                default: return -1;
            }
        }

        // Index dans la liste DÉCROISSANTE ; seules les valeurs exactes du boîtier passent.
        public static int Kelvin(string value)
        {
            string s = Clean(value);
            if (s.EndsWith("K")) s = s.Substring(0, s.Length - 1);
            int k;
            if (!TryNumber(s, out k)) return -1;
            for (int i = 0; i < KelvinAsc.Length; i++)
                if (KelvinAsc[i] == k) return KelvinAsc.Length - 1 - i;
            return -1;
        }

        // Le X-T30 ne mémorise PAS le décalage de balance des blancs par banque : ce
        // réglage n'existe nulle part dans le fichier. La convention retenue (celle
        // que l'utilisateur applique déjà à la main) est de l'inscrire dans le NOM de
        // la banque, par exemple « PACIFIC R+1 B-3 », pour pouvoir le ressaisir.
        // Le nom est plafonné à NameMax : c'est le nom de base qui est raccourci,
        // jamais le décalage, car c'est lui qui serait autrement perdu.
        public static string BuildBankName(Recipe recipe, string baseName)
        {
            string suffix = "";
            int r, b;
            if (TryShift(recipe.Get("WB Shift R"), out r) && r != 0) suffix += " R" + Signed(r);
            if (TryShift(recipe.Get("WB Shift B"), out b) && b != 0) suffix += " B" + Signed(b);
            string name = (baseName ?? "").Trim();
            // Le nom porte déjà le décalage : c'est le cas des recettes créées ici,
            // dont le nom a été formé de cette façon. Le rajouter donnerait
            // « PACIFIC R+1 B-3 R+1 B-3 ».
            if (suffix.Length > 0 && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return name.Length > NameMax ? name.Substring(0, NameMax).TrimEnd() : name;
            int room = NameMax - suffix.Length;
            if (room < 1) return name.Length > NameMax ? name.Substring(0, NameMax) : name;
            if (name.Length > room) name = name.Substring(0, room).TrimEnd();
            return name + suffix;
        }

        static bool TryShift(string value, out int result)
        {
            result = 0;
            if (value == null || value == "Not specified") return false;
            return int.TryParse(value.Trim().Replace("+", "").Replace(" ", ""), out result);
        }

        static string Signed(int v) { return (v > 0 ? "+" : "") + v.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        static string Clean(string v)
        {
            string s = (v ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("_", "");
            if (s.StartsWith("+")) s = s.Substring(1);
            return s;
        }

        static bool TryNumber(string v, out int result)
        {
            result = 0;
            string s = (v ?? "").Trim().Replace("+", "").Replace(" ", "");
            return int.TryParse(s, out result);
        }

        // ---------------- Somme de contrôle ----------------
        public static int Checksum(byte[] blob)
        {
            int total = 0;
            for (int i = 0; i < blob.Length; i++)
            {
                bool skip = false;
                for (int e = 0; e < ChecksumExcluded.Length; e++) if (ChecksumExcluded[e] == i) { skip = true; break; }
                if (!skip) total += blob[i];
            }
            return (total + ChecksumBias) & 0xFFFF;
        }
        public static int StoredChecksum(byte[] blob) { return blob[ChecksumOffset] | (blob[ChecksumOffset + 1] << 8); }
        public static bool ChecksumValid(byte[] blob) { return StoredChecksum(blob) == Checksum(blob); }

        // ---------------- Résultat d'une préparation ----------------
        public sealed class PatchResult
        {
            public bool Success;
            public string OutputPath = "";
            public readonly List<string> Written = new List<string>();
            public readonly List<string> Skipped = new List<string>();
            public string Error = "";
        }

        public static bool IsValidSettingsFile(byte[] blob, out string reason)
        {
            reason = "";
            if (blob == null || blob.Length != BlobSize) { reason = "The file is not " + BlobSize + " bytes."; return false; }
            if (Encoding.ASCII.GetString(blob, 0, 8) != "FUJIFILM") { reason = "Missing FUJIFILM signature."; return false; }
            string model = ReadAscii(blob, 0x14, 32);
            if (model != "X-T30") { reason = "This file is from a " + model + ", not an X-T30."; return false; }
            if (!ChecksumValid(blob)) { reason = "The file's checksum is inconsistent; it may be corrupted."; return false; }
            return true;
        }

        public static string ReadAscii(byte[] blob, int offset, int max)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = offset; i < Math.Min(blob.Length, offset + max); i++)
            { if (blob[i] == 0) break; if (blob[i] < 0x20 || blob[i] > 0x7E) break; sb.Append((char)blob[i]); }
            return sb.ToString();
        }

        // Prépare un fichier de réglages où la banque `slot` (0..6) porte `recipe`.
        // Écrit uniquement les octets dont la valeur a un code vérifié ; tout le
        // reste du fichier est laissé strictement intact.
        public static PatchResult Prepare(byte[] source, int slot, Recipe recipe, string newName, string outputPath)
        {
            Dictionary<int, Recipe> one = new Dictionary<int, Recipe>();
            one[slot] = recipe;
            Dictionary<int, string> names = new Dictionary<int, string>();
            if (!string.IsNullOrEmpty(newName)) names[slot] = newName;
            return PrepareMany(source, one, names, outputPath);
        }

        // Écrit plusieurs banques dans un seul fichier : un pack complet ne demande
        // alors qu'UNE restauration au lieu de sept.
        public static PatchResult PrepareMany(byte[] source, Dictionary<int, Recipe> assignments,
                                              Dictionary<int, string> names, string outputPath)
        {
            PatchResult result = new PatchResult();
            string reason;
            if (!IsValidSettingsFile(source, out reason)) { result.Error = reason; return result; }
            if (assignments == null || assignments.Count == 0) { result.Error = "No bank to write."; return result; }
            foreach (int s in assignments.Keys)
                if (s < 0 || s >= Slots) { result.Error = "Bank index out of range."; return result; }

            byte[] blob = (byte[])source.Clone();
            foreach (KeyValuePair<int, Recipe> pair in assignments)
            {
                string bankName = null;
                if (names != null && names.ContainsKey(pair.Key)) bankName = names[pair.Key];
                PatchBank(blob, pair.Key, pair.Value, bankName, result);
            }

            // Somme de contrôle recalculée une seule fois, après toutes les banques.
            int sum = Checksum(blob);
            blob[ChecksumOffset] = (byte)(sum & 0xFF);
            blob[ChecksumOffset + 1] = (byte)((sum >> 8) & 0xFF);
            if (!ChecksumValid(blob)) { result.Error = "Internal error: the recomputed checksum does not verify."; return result; }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                File.WriteAllBytes(outputPath, blob);
            }
            catch (Exception ex) { result.Error = "Could not write the file: " + ex.Message; return result; }

            result.OutputPath = outputPath;
            result.Success = true;
            return result;
        }

        static void PatchBank(byte[] blob, int slot, Recipe recipe, string newName, PatchResult result)
        {
            int b = SlotBase(slot);
            string tag = "C" + (slot + 1) + " ";

            Put(blob, b + 0, FilmSim(recipe.Get("Film Simulation")), tag + "Film Simulation", result);
            Put(blob, b + RelHighlight, Tone(recipe.Get("Highlight")), tag + "Highlight", result);
            Put(blob, b + RelShadow, Tone(recipe.Get("Shadow")), tag + "Shadow", result);
            Put(blob, b + RelSharpness, Tone(recipe.Get("Sharpness")), tag + "Sharpness", result);
            Put(blob, b + RelColor, Color(recipe.Get("Color")), tag + "Color", result);
            Put(blob, b + RelNr, NoiseReduction(recipe.Get("Noise Reduction")), tag + "Noise Reduction", result);
            Put(blob, b + RelGrain, Grain(recipe.Get("Grain Effect")), tag + "Grain Effect", result);
            Put(blob, b + RelChrome, Chrome(recipe.Get("Color Chrome Effect")), tag + "Color Chrome Effect", result);

            // La plage dynamique n'est écrite que si la priorité est explicitement Off :
            // sinon c'est la priorité qui la pilote et l'écrire n'aurait pas de sens.
            int priority = DrPriority(recipe.Get("Dynamic Range Priority"));
            if (priority >= 0) Put(blob, b + RelDrPriority, priority, tag + "Dynamic Range Priority", result);
            else result.Skipped.Add(tag + "Dynamic Range Priority (no verified code for \"" + recipe.Get("Dynamic Range Priority") + "\")");
            if (priority == 3) Put(blob, b + RelDr, DynamicRange(recipe.Get("Dynamic Range")), tag + "Dynamic Range", result);
            else result.Skipped.Add(tag + "Dynamic Range (driven by the priority setting)");

            int wb = WhiteBalance(recipe.Get("White Balance"));
            Put(blob, b + RelWbMode, wb, tag + "White Balance", result);
            if (wb == 8) Put(blob, b + RelWbKelvin, Kelvin(recipe.Get("White Balance")), tag + "Color Temperature", result);

            // Nom de la banque : ASCII, tronqué, ancien nom effacé.
            if (!string.IsNullOrEmpty(newName))
            {
                string ascii = "";
                foreach (char c in newName) if (c >= 0x20 && c <= 0x7E) ascii += c;
                if (ascii.Length > NameMax) ascii = ascii.Substring(0, NameMax);
                for (int i = 0; i < NameMax + 1; i++) blob[b + NameRel + i] = 0;
                for (int i = 0; i < ascii.Length; i++) blob[b + NameRel + i] = (byte)ascii[i];
                result.Written.Add(tag + "name = \"" + ascii + "\"");
            }

            // Réglages que ce fichier ne stocke pas : signalés, jamais inventés.
            // Les décalages de balance des blancs sont repris dans le nom de la banque
            // (voir BuildBankName) ; on le dit plutôt que de laisser croire à une perte.
            foreach (string key in new string[] { "WB Shift R", "WB Shift B" })
                if (recipe.Get(key) != "Not specified" && recipe.Get(key).Trim() != "0")
                    result.Skipped.Add(tag + key + " (the X-T30 cannot store it per bank — carried in the bank name, set it by hand)");
            foreach (string key in new string[] { "ISO", "Monochromatic Color" })
                if (recipe.Get(key) != "Not specified") result.Skipped.Add(tag + key + " (not stored in the camera settings file — set it by hand)");
        }

        static void Put(byte[] blob, int offset, int value, string label, PatchResult result)
        {
            if (value < 0 || value > 255) { result.Skipped.Add(label + " (no verified code for this value)"); return; }
            blob[offset] = (byte)value;
            result.Written.Add(label);
        }
    }
}
