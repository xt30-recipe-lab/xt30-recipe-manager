using System;
using System.Collections.Generic;
using System.IO;

namespace Xt30Probe.AppModel
{
    // Banques C1-C7 REELLEMENT lues dans le boitier, via le fichier de reglages
    // (handle 0, format 0x5000) decode par Tools/BackupDecoder.
    //
    // Ce module ne parle jamais a l'appareil : il lit le rapport JSON produit par
    // le decodeur. Si le fichier est absent, l'application retombe sur ses
    // donnees LOCAL de demonstration, sans jamais les presenter comme camera.
    public sealed class CameraBank
    {
        public int Number;
        public string Name = "";
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
    }

    public sealed class CameraBanksSnapshot
    {
        public string Model = "";
        public string Serial = "";
        public string Layout = "";
        public DateTime ReadAt;
        public string SourceFile = "";
        public readonly List<CameraBank> Banks = new List<CameraBank>();
        public bool IsUsable { get { return Banks.Count == 7; } }

        // Cherche le rapport du decodeur a cote de l'executable.
        public static CameraBanksSnapshot Load(string baseDirectory)
        {
            string path = Path.Combine(baseDirectory, "phase2-inventory", "xt30_camera_banks.json");
            if (!File.Exists(path)) return null;
            try
            {
                Dictionary<string, object> root = Json.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                if (root == null) return null;
                // Un rapport qui n'a pas pu decoder les banques n'est pas utilisable :
                // on prefere ne rien afficher plutot qu'afficher des valeurs douteuses.
                object banksObject;
                if (!root.TryGetValue("banks", out banksObject) || !(banksObject is List<object>)) return null;

                CameraBanksSnapshot snapshot = new CameraBanksSnapshot();
                snapshot.Model = Text(root, "model");
                snapshot.Serial = Text(root, "serial");
                snapshot.Layout = Text(root, "layout");
                snapshot.SourceFile = Text(root, "sourceFile");
                DateTime parsed;
                if (DateTime.TryParse(Text(root, "generatedAt"), out parsed)) snapshot.ReadAt = parsed;
                else snapshot.ReadAt = File.GetLastWriteTime(path);

                foreach (object item in (List<object>)banksObject)
                {
                    Dictionary<string, object> entry = item as Dictionary<string, object>;
                    if (entry == null) continue;
                    CameraBank bank = new CameraBank();
                    string slot = Text(entry, "slot");            // "C1".."C7"
                    int number;
                    if (slot.Length >= 2 && int.TryParse(slot.Substring(1), out number)) bank.Number = number;
                    else bank.Number = snapshot.Banks.Count + 1;
                    bank.Name = Text(entry, "name");
                    object settings;
                    if (entry.TryGetValue("settings", out settings) && settings is Dictionary<string, object>)
                        foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)settings)
                            if (pair.Value != null) bank.Values[pair.Key] = Convert.ToString(pair.Value);
                    snapshot.Banks.Add(bank);
                }
                return snapshot.Banks.Count == 7 ? snapshot : null;
            }
            catch (Exception) { return null; }
        }

        static string Text(Dictionary<string, object> d, string key)
        { object v; return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : ""; }

        // Construit une recette affichable a partir d'une banque lue dans l'appareil.
        // Les valeurs que le fichier de reglages ne contient pas (ISO, WB shift...)
        // restent volontairement absentes : elles s'afficheront "Not specified".
        public static Recipe ToRecipe(CameraBank bank, List<Recipe> library)
        {
            Recipe recipe = new Recipe();
            recipe.Id = "camera-c" + bank.Number;
            recipe.Name = string.IsNullOrEmpty(bank.Name) ? "C" + bank.Number + " (unnamed)" : bank.Name;
            recipe.Category = "Camera";
            recipe.Source = DataSource.CAMERA;
            recipe.SourceSite = "CAMERA";
            recipe.Demonstration = false;
            foreach (KeyValuePair<string, string> pair in bank.Values) recipe.Values[pair.Key] = pair.Value;
            Recipe match = MatchLibrary(bank.Name, library);
            recipe.Cover = match != null ? match.Cover : "pacific";
            recipe.MatchedLibraryRecipe = match;
            return recipe;
        }

        // Reconnaissance du nom : on compare en ignorant casse, espaces et
        // ponctuation, puis on accepte un prefixe commun d'au moins 5 caracteres
        // ("PORTRA400" reconnait "PORTRA 400"). Une correspondance n'est
        // qu'illustrative : elle ne remplace jamais les valeurs lues.
        public static Recipe MatchLibrary(string cameraName, List<Recipe> library)
        {
            if (string.IsNullOrEmpty(cameraName) || library == null) return null;
            string target = Normalize(cameraName);
            if (target.Length < 3) return null;
            Recipe best = null;
            int bestScore = 0;
            foreach (Recipe candidate in library)
            {
                string other = Normalize(candidate.Name);
                if (other.Length < 3) continue;
                int score = 0;
                if (other == target) score = 1000;
                else if (target.StartsWith(other) || other.StartsWith(target)) score = Math.Min(target.Length, other.Length);
                else if (target.Contains(other) || other.Contains(target)) score = Math.Min(target.Length, other.Length) - 1;
                if (score >= 5 && score > bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        static string Normalize(string value)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in (value ?? "").ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
