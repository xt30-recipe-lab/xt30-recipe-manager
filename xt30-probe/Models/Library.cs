using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xt30Probe.AppModel
{
    public enum DataSource { CAMERA, LOCAL, UNKNOWN }

    public sealed class Recipe
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "New Recipe";
        public string Category = "Vintage";
        public string Cover = "pacific";
        // "Photo" : recette destinée aux banques C1-C7.
        // "Video" : recette destinée au mode film. Le X-T30 ne range AUCUN réglage
        // vidéo dans son fichier de sauvegarde ni dans C1-C7 : ces recettes se
        // reportent à la main dans les menus du boîtier, et ne sont jamais écrites.
        public string Kind = "Photo";
        public bool IsVideo { get { return Kind == "Video"; } }
        public bool Favorite;
        public bool Demonstration;
        public DataSource Source = DataSource.LOCAL;
        // Provenance : "LOCAL" ou "FUJI X WEEKLY" (recettes importées, lecture seule)
        public string SourceSite = "LOCAL";
        public string ArticleUrl = "";
        public string Author = "";
        public string PublishedAt = "";
        public string CompatStatus = "";   // XT30_COMPATIBLE / XT30_PARTIAL / XT30_INCOMPATIBLE / UNVERIFIED ("" = local, calculé)
        public string CompatReason = "";
        // Recette de la bibliothèque dont le nom ressemble à celui de la banque (illustratif).
        public Recipe MatchedLibraryRecipe;
        public bool IsFromCamera { get { return Source == DataSource.CAMERA; } }
        public bool IsImported { get { return SourceSite != "LOCAL" && !IsFromCamera; } }
        // Toutes les photos publiées avec la recette, couverture en premier.
        // Vide pour une recette locale : seule sa couverture existe.
        public readonly List<string> Gallery = new List<string>();
        public Dictionary<string, string> Values = new Dictionary<string, string>();
        public string Get(string key) { string value; return Values.TryGetValue(key, out value) ? value : "Not specified"; }
        public string Simulation { get { return Get("Film Simulation"); } }
        public bool IsBlackAndWhite
        {
            get
            {
                string s = Simulation.ToLowerInvariant();
                return s.Contains("acros") || s.Contains("monochrome") || s.Contains("sepia");
            }
        }
        public static readonly string[] ParameterOrder = {
            "ISO", "Dynamic Range", "Dynamic Range Priority", "Film Simulation",
            "Monochromatic Color", "Grain Effect", "Color Chrome Effect", "White Balance",
            "WB Shift R", "WB Shift B", "Highlight", "Shadow", "Color", "Sharpness", "Noise Reduction"
        };
        // Réglages d'image du mode film. Le grain n'y figure pas : il ne fait pas
        // partie des réglages d'image appliqués au film sur ce boîtier.
        public static readonly string[] VideoParameterOrder = {
            "Movie Mode", "F-Log", "ISO", "Film Simulation", "Monochromatic Color",
            "White Balance", "WB Shift R", "WB Shift B", "Dynamic Range",
            "Color Chrome Effect", "Highlight", "Shadow", "Color", "Sharpness",
            "Noise Reduction", "Interframe NR"
        };
        public string[] Parameters { get { return IsVideo ? VideoParameterOrder : ParameterOrder; } }
        public static readonly string[] AdditionalParameters = { "Color Chrome FX Blue", "Clarity", "Grain Size" };
        public List<string> CompatibilityIssues()
        {
            List<string> issues = new List<string>();
            if (IsImported)
            {
                // La compatibilité des recettes importées vient du rapport d'import,
                // jamais recalculée silencieusement.
                if (CompatStatus == "XT30_COMPATIBLE") return issues;
                issues.Add(CompatReason == "" ? CompatStatus : CompatReason);
                return issues;
            }
            string sim = Simulation.ToLowerInvariant();
            if (sim.Contains("classic neg") || sim.Contains("bleach") || sim.Contains("nostalgic") || sim.Contains("reala"))
                issues.Add(Simulation + " is unsupported on X-T30.");
            string[] known = { "Provia", "Velvia", "Astia", "Classic Chrome", "Pro Neg. Hi", "Pro Neg. Std", "Eterna", "Acros", "Acros + Ye", "Acros + R", "Acros + G", "Monochrome", "Monochrome + Ye", "Monochrome + R", "Monochrome + G", "Sepia" };
            if (issues.Count == 0 && !known.Contains(Simulation)) issues.Add("Film simulation compatibility has not been verified.");
            foreach (string key in AdditionalParameters)
            {
                string v = Get(key).Trim().ToLowerInvariant();
                if (v != "off" && v != "0" && v != "none" && v != "not specified" && v != "")
                    issues.Add(key + " is unsupported on X-T30 (" + Get(key) + ").");
            }
            return issues;
        }
    }

    public sealed class CustomSlot
    {
        public int Number;
        public Recipe Recipe;
        public string PreviewCover;
        public DataSource Source = DataSource.UNKNOWN;
    }

    public sealed class RecipePack
    {
        public string Name;
        public string Description;
        public readonly List<CustomSlot> Slots = new List<CustomSlot>();
        public void Validate() { if (Slots.Count != 7) throw new InvalidOperationException("A pack must contain exactly seven recipes."); }
    }

    // Local data only. This class has no reference to the transport or camera commands.
    public sealed class RecipeLibrary
    {
        public readonly List<Recipe> Recipes = new List<Recipe>();
        public readonly List<RecipePack> Packs = new List<RecipePack>();
        public readonly List<CustomSlot> Slots = new List<CustomSlot>();
        public readonly string DirectoryPath;
        public bool ExtendedScan;
        public event EventHandler Changed;
        public string LoadWarning = "";
        // Étapes du chargement, rapportées à l'écran d'ouverture. Volontairement un
        // simple délégué : ce fichier ne connaît rien de l'interface.
        // La clé anglaise et ses arguments sont transmis séparément : c'est
        // l'interface qui traduit et met en forme, ce fichier n'en sait rien.
        public static Action<string, object[]> Progress;
        static void Report(string step, params object[] args) { if (Progress != null) Progress(step, args); }

        public RecipeLibrary(string path)
        {
            DirectoryPath = path;
            Report("Loading your recipes…");
            string file = Path.Combine(path, "library.json");
            if (File.Exists(file))
            {
                try
                {
                    Dictionary<string, object> root = Json.Parse(File.ReadAllText(file)) as Dictionary<string, object>;
                    foreach (object item in (List<object>)root["recipes"])
                    {
                        Dictionary<string, object> d = (Dictionary<string, object>)item;
                        Recipe r = new Recipe(); r.Id = Text(d, "id"); r.Name = Text(d, "name"); r.Category = Text(d, "category");
                        r.Cover = Text(d, "cover"); r.Favorite = Text(d, "favorite") == "True"; r.Demonstration = Text(d, "demonstration") == "True";
                        r.Kind = Text(d, "kind") == "Video" ? "Video" : "Photo";
                        Dictionary<string, object> values = (Dictionary<string, object>)d["values"];
                        foreach (var v in values) r.Values[v.Key] = Convert.ToString(v.Value);
                        Recipes.Add(r);
                    }
                    ExtendedScan = Text(root, "extendedScan") == "True";
                }
                catch (Exception ex)
                {
                    Recipes.Clear(); LoadWarning = "The local library could not be read. The original file was preserved. " + ex.Message;
                    // Preserve malformed data before a user explicitly saves a repaired library.
                    File.Copy(file, file + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                }
            }
            if (Recipes.Count == 0) Seed();
            Report("Loading the imported recipe catalogues…");
            LoadFujiXWeekly();
            Report("Reading the decoded camera banks…");
            CameraBanks = CameraBanksSnapshot.Load(AppDomain.CurrentDomain.BaseDirectory);
            BuildPacks();
            Report("{0} recipes ready", Recipes.Count);
        }

        // Banques réellement lues dans le boîtier (null tant qu'aucune lecture n'a eu lieu).
        public CameraBanksSnapshot CameraBanks;
        public bool SlotsAreFromCamera { get { return CameraBanks != null && CameraBanks.IsUsable; } }
        public void ReloadCameraBanks()
        {
            CameraBanks = CameraBanksSnapshot.Load(AppDomain.CurrentDomain.BaseDirectory);
            BuildPacks();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        // Bibliothèque importée Fuji X Weekly (offline, lecture seule, distincte
        // des recettes locales : jamais réécrite dans library.json).
        public int ImportedCount;
        // Charge TOUS les index présents dans library/index/, quelle que soit leur
        // source (Fuji X Weekly, filmsimrecipes…). Chaque index déclare son propre
        // nom de site ; rien n'est deviné.
        void LoadFujiXWeekly()
        {
            string libraryRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "library");
            string indexDir = Path.Combine(libraryRoot, "index");
            if (!Directory.Exists(indexDir)) return;
            HashSet<string> favorites = LoadImportedFavorites();
            HashSet<string> seen = new HashSet<string>();
            string[] indexes = Directory.GetFiles(indexDir, "*.json");
            Array.Sort(indexes);
            foreach (string file in indexes) LoadIndexFile(file, libraryRoot, favorites, seen);
        }

        void LoadIndexFile(string indexFile, string libraryRoot, HashSet<string> favorites, HashSet<string> seen)
        {
            try
            {
                Dictionary<string, object> root = Json.Parse(File.ReadAllText(indexFile)) as Dictionary<string, object>;
                if (root == null) return;
                object list;
                if (!root.TryGetValue("recipes", out list) || !(list is List<object>)) return;
                // Le nom du site vient de l'index lui-même ; à défaut, on retombe sur
                // Fuji X Weekly pour rester compatible avec les index déjà produits.
                string site = Text(root, "site");
                if (site == "") site = "Fuji X Weekly";
                string prefix = Text(root, "idPrefix");
                if (prefix == "") prefix = "fxw-";
                foreach (object item in (List<object>)list)
                {
                    Dictionary<string, object> d = (Dictionary<string, object>)item;
                    Dictionary<string, object> source = Sub(d, "source");
                    Dictionary<string, object> compat = Sub(d, "compatibility");
                    Dictionary<string, object> settings = Sub(d, "settings");
                    Dictionary<string, object> images = Sub(d, "images");
                    Recipe r = new Recipe();
                    r.Id = prefix + Text(d, "slug");
                    if (seen.Contains(r.Id)) continue;   // même recette dans deux index
                    seen.Add(r.Id);
                    r.Name = CleanRecipeName(Text(d, "name"));
                    r.Category = site;
                    r.SourceSite = site.ToUpperInvariant();
                    r.ArticleUrl = Text(source, "articleUrl");
                    r.Author = Text(source, "author");
                    r.PublishedAt = Text(source, "publishedAt");
                    r.CompatStatus = Text(compat, "xt30Original");
                    r.CompatReason = Text(compat, "reason");
                    r.Favorite = favorites.Contains(r.Id);
                    string cover = Text(images, "cover");
                    r.Cover = cover == "" ? "pacific" : Path.Combine(libraryRoot, cover.Replace('/', '\\'));
                    // Galerie : la couverture puis les autres photos de l'article,
                    // dans l'ordre publié. Un fichier absent est simplement ignoré.
                    if (cover != "") r.Gallery.Add(r.Cover);
                    object examples;
                    if (images.TryGetValue("examples", out examples) && examples is List<object>)
                        foreach (object entry in (List<object>)examples)
                        {
                            string relative = Convert.ToString(entry);
                            if (relative == "") continue;
                            string full = Path.Combine(libraryRoot, relative.Replace('/', '\\'));
                            if (!r.Gallery.Contains(full)) r.Gallery.Add(full);
                        }
                    MapSetting(r, settings, "iso", "ISO");
                    MapSetting(r, settings, "dynamicRange", "Dynamic Range");
                    MapSetting(r, settings, "dRangePriority", "Dynamic Range Priority");
                    MapSetting(r, settings, "filmSimulation", "Film Simulation");
                    MapSetting(r, settings, "monochromaticColor", "Monochromatic Color");
                    MapSetting(r, settings, "grain", "Grain Effect");
                    MapSetting(r, settings, "colorChrome", "Color Chrome Effect");
                    MapSetting(r, settings, "whiteBalance", "White Balance");
                    MapSetting(r, settings, "wbShiftR", "WB Shift R");
                    MapSetting(r, settings, "wbShiftB", "WB Shift B");
                    MapSetting(r, settings, "highlight", "Highlight");
                    MapSetting(r, settings, "shadow", "Shadow");
                    MapSetting(r, settings, "color", "Color");
                    MapSetting(r, settings, "sharpness", "Sharpness");
                    MapSetting(r, settings, "highIsoNR", "Noise Reduction");
                    MapSetting(r, settings, "colorChromeFXBlue", "Color Chrome FX Blue");
                    MapSetting(r, settings, "clarity", "Clarity");
                    MapSetting(r, settings, "grainSize", "Grain Size");
                    Recipes.Add(r);
                    ImportedCount++;
                }
            }
            catch (Exception ex)
            {
                // Un index illisible ne doit pas empêcher les autres de se charger.
                if (LoadWarning == "")
                    LoadWarning = "An imported recipe index could not be read (" + Path.GetFileName(indexFile) + "): " + ex.Message;
            }
        }
        static string CleanRecipeName(string name)
        {
            string n = name;
            foreach (string prefix in new[] { "My Fujifilm ", "Fujifilm " })
                if (n.StartsWith(prefix)) n = n.Substring(prefix.Length);
            return n;
        }
        static Dictionary<string, object> Sub(Dictionary<string, object> d, string k)
        { object v; return d.TryGetValue(k, out v) && v is Dictionary<string, object> ? (Dictionary<string, object>)v : new Dictionary<string, object>(); }
        static void MapSetting(Recipe r, Dictionary<string, object> settings, string jsonKey, string uiKey)
        {
            object v;
            if (settings.TryGetValue(jsonKey, out v) && v != null)
            {
                string s = Convert.ToString(v);
                if (s != "") r.Values[uiKey] = s;
            }
        }
        HashSet<string> LoadImportedFavorites()
        {
            HashSet<string> set = new HashSet<string>();
            string file = Path.Combine(DirectoryPath, "library.json");
            try
            {
                if (File.Exists(file))
                {
                    Dictionary<string, object> root = Json.Parse(File.ReadAllText(file)) as Dictionary<string, object>;
                    object list;
                    if (root != null && root.TryGetValue("importedFavorites", out list) && list is List<object>)
                        foreach (object o in (List<object>)list) set.Add(Convert.ToString(o));
                }
            }
            catch (Exception) { }
            return set;
        }
        static string Text(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) ? Convert.ToString(v) : ""; }
        void Seed()
        {
            AddDemo("PACIFIC BLUES", "Classic Chrome", "DR400", "Travel", "pacific");
            AddDemo("CLASSIC CUBAN", "Classic Chrome", "DR400", "Street", "cuban");
            AddDemo("KODAK GOLD 200", "Classic Chrome", "DR200", "Vintage", "gold");
            AddDemo("CINESTILL 800T", "Classic Negative", "DR400", "Night", "cinestill");
            AddDemo("PORTRA 400", "Pro Neg. Std", "DR400", "Portrait", "portra");
            AddDemo("KODACHROME 64", "Classic Chrome", "DR200", "Vintage", "kodachrome");
            AddDemo("SUMMER CHROME", "Classic Chrome", "DR400", "Cinematic", "summer");
        }
        void AddDemo(string name, string sim, string dr, string category, string cover)
        {
            Recipe r = new Recipe(); r.Name = name; r.Id = cover; r.Cover = cover; r.Category = category; r.Demonstration = true;
            string[] vals = { "Auto", dr, "Off", sim, "0", "Weak", "Weak", "5900K", "+1", "-3", "-2", "+3", "+2", "-1", "-4" };
            for (int i = 0; i < Recipe.ParameterOrder.Length; i++) r.Values[Recipe.ParameterOrder[i]] = vals[i];
            r.Values["Color Chrome FX Blue"] = "Off"; r.Values["Clarity"] = "0"; r.Values["Grain Size"] = "Not specified";
            Recipes.Add(r);
        }
        void BuildPacks()
        {
            Slots.Clear(); Packs.Clear();
            if (SlotsAreFromCamera)
            {
                // Valeurs lues dans l'appareil : provenance CAMERA, aucune donnée inventée.
                foreach (CameraBank bank in CameraBanks.Banks)
                {
                    Recipe r = CameraBanksSnapshot.ToRecipe(bank, Recipes);
                    Slots.Add(new CustomSlot { Number = bank.Number, Recipe = r, Source = DataSource.CAMERA });
                }
            }
            else
            {
                string[] order = { "portra", "gold", "kodachrome", "summer", "cuban", "pacific", "cinestill" };
                for (int i = 0; i < 7; i++)
                {
                    Recipe r = Recipes.Find(delegate(Recipe x) { return x.Id == order[i]; }) ?? Recipes[i % Recipes.Count];
                    Slots.Add(new CustomSlot { Number = i + 1, Recipe = r, Source = DataSource.LOCAL, PreviewCover = i==0?"slot-portra":i==1?"slot-gold":null });
                }
            }
            // Les packs restent une composition LOCALE : ils ne reprennent jamais les
            // banques lues dans l'appareil, pour ne pas mélanger les provenances.
            string[] packOrder = { "portra", "gold", "kodachrome", "summer", "cuban", "pacific", "cinestill" };
            List<Recipe> localSeven = new List<Recipe>();
            for (int i = 0; i < 7; i++)
                localSeven.Add(Recipes.Find(delegate(Recipe x) { return x.Id == packOrder[i]; })
                    // Une recette vidéo ne peut pas remplir une banque : elle n'en a pas les réglages.
                    ?? Recipes.Find(delegate(Recipe x) { return !x.IsImported && !x.IsFromCamera && !x.IsVideo; })
                    ?? Recipes[i % Recipes.Count]);
            string[] names = { "SUMMER", "STREET", "NIGHT", "TRAVEL" };
            for (int p = 0; p < names.Length; p++)
            {
                RecipePack pack = new RecipePack { Name = names[p], Description = "Local demonstration pack · 7 recipes" };
                for (int i = 0; i < 7; i++) pack.Slots.Add(new CustomSlot { Number = i + 1, Recipe = localSeven[(i + p) % 7], Source = DataSource.LOCAL });
                pack.Validate(); Packs.Add(pack);
            }
        }
        public List<Recipe> Query(string search, string filter) { return Query(search, filter, "All simulations"); }
        public List<Recipe> Query(string search, string filter, string simulation)
        {
            return Recipes.FindAll(delegate(Recipe r)
            {
                bool matches = (r.Name + " " + r.Simulation + " " + r.Category + " " + r.SourceSite).IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches) return false;
                if (simulation != null && simulation != "All simulations" &&
                    !r.Simulation.Equals(simulation, StringComparison.OrdinalIgnoreCase)) return false;
                switch (filter)
                {
                    case "All": return true;
                    case "Compatible":
                        return r.IsImported ? r.CompatStatus == "XT30_COMPATIBLE" : r.CompatibilityIssues().Count == 0;
                    case "Favorites": return r.Favorite;
                    case "Imported": return r.IsImported;
                    case "Local": return !r.IsImported;
                    case "Video": return r.IsVideo;
                    case "Photo": return !r.IsVideo;
                    case "B&W": return r.IsBlackAndWhite;
                    case "Color": return !r.IsBlackAndWhite;
                    default: return filter == r.Category;
                }
            });
        }
        public List<string> Simulations()
        {
            List<string> sims = new List<string>();
            foreach (Recipe r in Recipes)
            {
                string s = r.Simulation;
                if (s != "Not specified" && !sims.Contains(s)) sims.Add(s);
            }
            sims.Sort(StringComparer.OrdinalIgnoreCase);
            return sims;
        }
        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            List<object> items = new List<object>();
            List<object> importedFavorites = new List<object>();
            foreach (Recipe r in Recipes)
            {
                // Les recettes importées ne sont jamais réécrites dans library.json ;
                // seul leur favori (par identifiant) est persistant.
                if (r.IsImported) { if (r.Favorite) importedFavorites.Add(r.Id); continue; }
                items.Add(new Dictionary<string, object> {
                    { "id", r.Id }, { "name", r.Name }, { "category", r.Category }, { "cover", r.Cover }, { "kind", r.Kind },
                    { "favorite", r.Favorite }, { "demonstration", r.Demonstration }, { "source", "LOCAL" }, { "values", r.Values.ToDictionary(x => x.Key, x => (object)x.Value) }
                });
            }
            string file = Path.Combine(DirectoryPath, "library.json");
            string temp = file + ".tmp";
            File.WriteAllText(temp, Json.Serialize(new Dictionary<string, object> { { "version", 1 }, { "extendedScan", ExtendedScan }, { "recipes", items }, { "importedFavorites", importedFavorites } }));
            if (File.Exists(file)) File.Replace(temp, file, file + ".previous", true); else File.Move(temp, file);
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        public void ToggleFavorite(Recipe recipe) { recipe.Favorite = !recipe.Favorite; Save(); }
        public void Add(Recipe recipe) { Recipes.Add(recipe); Save(); }
        public string Backup()
        {
            Save(); string dir = Path.Combine(DirectoryPath, "backups"); Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "library-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.Copy(Path.Combine(DirectoryPath, "library.json"), file); return file;
        }
    }
}
