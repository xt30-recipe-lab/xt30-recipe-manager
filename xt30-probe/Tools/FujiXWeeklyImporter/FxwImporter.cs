// ============================================================================
// FxwImporter — Importeur de métadonnées de recettes Fuji X Weekly
//
// Module TOTALEMENT SÉPARÉ du moteur caméra : aucun using Xt30Probe, aucun
// accès USB/PTP/WPD. Il ne fait que du HTTP public (fujixweekly.com) et écrit
// une bibliothèque locale sous library\.
//
// Règles :
//  - aucune valeur devinée : donnée absente => null ;
//  - uniquement les pages publiques (pas de Patreon/App) ;
//  - aucune image n'est collectée, référencée ou téléchargée ;
//  - la collecte complète reste verrouillée jusqu'à confirmation d'autorisation.
//
// Usage :
//   fxw-importer --help
//   fxw-importer --test          (5 recettes X-Trans IV + III)
//   fxw-importer --xt30          (X-Trans IV + III, défaut)
//   fxw-importer --all --permission-confirmed
//   fxw-importer --limit N       (N premières recettes du périmètre)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FxwImporter
{
    // ------------------------- JSON minimal (autonome) -------------------------
    public static class Json
    {
        public static string Serialize(object o)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, o, 0);
            return sb.ToString();
        }
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

    public class RecipeLink
    {
        public string Url;
        public string SourceListUrl;
        public string Group;       // texte du titre de section sous lequel le lien apparaît
    }

    public class Catalog
    {
        public string Url;
        public string Generation;
        // Catalogue dont les recettes sont réalisables sur un X-T30 : les
        // générations X-Trans I à IV n'utilisent que des réglages présents sur
        // ce boîtier. X-Trans V introduit des simulations qu'il n'a pas.
        public bool Xt30Relevant;
        public Catalog(string url, string generation) : this(url, generation, false) { }
        public Catalog(string url, string generation, bool xt30Relevant)
        { Url = url; Generation = generation; Xt30Relevant = xt30Relevant; }
    }

    public class FxwRecipe
    {
        public string Name;
        public string Slug;
        public string CanonicalUrl;
        public string PublishedAt;
        public string Author;
        public string Group;
        public List<string> CompatibleCameras = new List<string>();
        public Dictionary<string, string> Settings = new Dictionary<string, string>(); // null => absent
        public string Compat;      // XT30_COMPATIBLE / XT30_PARTIAL / XT30_INCOMPATIBLE / UNVERIFIED
        public string CompatReason;
        public string ExtractionStatus; // VERIFIED / PARTIAL
        public List<string> MissingFields = new List<string>();
    }

    public static class Program
    {
        const string ListXTrans4 = "https://fujixweekly.com/fujifilm-x-trans-iv-recipes/";
        const string ListXTrans3 = "https://fujixweekly.com/fujifilm-x-trans-iii-recipes/";
        static readonly Catalog[] AllCatalogs = {
            new Catalog("https://fujixweekly.com/fujifilm-x-trans-v-recipes/", "X-Trans V"),
            new Catalog(ListXTrans4, "X-Trans IV", true),
            new Catalog(ListXTrans3, "X-Trans III", true),
            new Catalog("https://fujixweekly.com/fujifilm-x-trans-ii-recipes/", "X-Trans II", true),
            new Catalog("https://fujixweekly.com/fujifilm-x-trans-i-recipes/", "X-Trans I", true),
            new Catalog("https://fujixweekly.com/fujifilm-exr-cmos-film-simulation-recipes/", "EXR-CMOS"),
            new Catalog("https://fujixweekly.com/fujifilm-bayer-recipes/", "Bayer"),
            new Catalog("https://fujixweekly.com/fujifilm-gfx-recipes/", "GFX"),
            new Catalog("https://fujixweekly.com/full-spectrum-recipes/", "Full Spectrum IR"),
            new Catalog("https://fujixweekly.com/video-recipes/", "Video")
        };
        static readonly string[] SettingKeys = {
            "filmSimulation","dynamicRange","dRangePriority","whiteBalance","wbShiftR","wbShiftB",
            "highlight","shadow","color","sharpness","highIsoNR","grain","grainSize",
            "colorChrome","colorChromeFXBlue","smoothSkin","clarity","iso","exposureCompensation"
        };

        static string LibraryDir;
        // Fichier texte d'URL d'articles, une par ligne (option --urls).
        static string UrlListFile;
        static List<string> LogLines = new List<string>();

        static void Log(string fmt, params object[] args)
        {
            string line = args.Length > 0 ? string.Format(fmt, args) : fmt;
            Console.WriteLine(line);
            LogLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + line);
        }

        public static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            int limit = -1;
            bool test = false;
            bool allCatalogs = false;
            bool permissionConfirmed = false;
            LibraryDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "library"));
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help" || args[i] == "-h" || args[i] == "/?") { PrintHelp(); return 0; }
                else if (args[i] == "--test") { test = true; limit = 5; }
                else if (args[i] == "--limit" && i + 1 < args.Length) { limit = int.Parse(args[++i]); }
                else if (args[i] == "--full" || args[i] == "--xt30") { allCatalogs = false; limit = -1; }
                else if (args[i] == "--all") { allCatalogs = true; }
                else if (args[i] == "--permission-confirmed") { permissionConfirmed = true; }
                else if (args[i] == "--urls" && i + 1 < args.Length) { UrlListFile = Path.GetFullPath(args[++i]); }
                else if (args[i] == "--out" && i + 1 < args.Length) { LibraryDir = Path.GetFullPath(args[++i]); }
                else { Console.Error.WriteLine("Argument inconnu : " + args[i]); PrintHelp(); return 2; }
            }
            if (allCatalogs && !permissionConfirmed)
            {
                Console.Error.WriteLine("Import complet non lancé : ajoutez --permission-confirmed uniquement après réception de l'autorisation écrite.");
                return 3;
            }
            Log("FxwImporter — bibliothèque : {0}", LibraryDir);
            Directory.CreateDirectory(LibraryDir);
            Directory.CreateDirectory(Path.Combine(LibraryDir, "recipes"));
            Directory.CreateDirectory(Path.Combine(LibraryDir, "index"));
            Directory.CreateDirectory(Path.Combine(LibraryDir, "reports"));

            List<RecipeLink> links = new List<RecipeLink>();
            List<object> failed = new List<object>();
            List<Catalog> selectedCatalogs = new List<Catalog>();
            if (UrlListFile != null)
            {
                // Liste d'articles fournie explicitement (une URL par ligne). Utile pour
                // reprendre les recettes citées par une page de sélection éditoriale, qui
                // n'apparaissent pas forcément dans les catalogues par génération.
                foreach (string raw in File.ReadAllLines(UrlListFile))
                {
                    string url = raw.Trim();
                    if (url.Length == 0 || url.StartsWith("#")) continue;
                    links.Add(new RecipeLink { Url = url, SourceListUrl = UrlListFile, Group = "Liste fournie" });
                }
                Log("Liste fournie : {0} URL(s) depuis {1}", links.Count, UrlListFile);
            }
            else
            {
                foreach (Catalog catalog in AllCatalogs)
                    if (allCatalogs || catalog.Xt30Relevant)
                        selectedCatalogs.Add(catalog);
                foreach (Catalog catalog in selectedCatalogs)
                {
                    try { CollectLinks(catalog.Url, catalog.Generation, links); }
                    catch (Exception ex) { Log("ECHEC liste {0} : {1}", catalog.Generation, ex.Message); failed.Add(Fail(catalog.Url, ex.Message)); }
                }
            }

            // Déduplication par URL canonique de lien
            Dictionary<string, RecipeLink> unique = new Dictionary<string, RecipeLink>();
            foreach (RecipeLink l in links)
            {
                string key = Canonicalize(l.Url);
                if (!unique.ContainsKey(key)) unique[key] = l;
            }
            List<RecipeLink> all = new List<RecipeLink>(unique.Values);
            Log("Recettes détectées (liens uniques) : {0}", all.Count);

            List<RecipeLink> work = all;
            if (test)
            {
                // 5 recettes d'âges/structures variés : extrêmes + milieu des deux listes
                work = new List<RecipeLink>();
                List<RecipeLink> iv = all.FindAll(delegate(RecipeLink x) { return x.SourceListUrl == ListXTrans4; });
                List<RecipeLink> iii = all.FindAll(delegate(RecipeLink x) { return x.SourceListUrl == ListXTrans3; });
                if (iv.Count > 0) work.Add(iv[0]);
                if (iv.Count > 2) work.Add(iv[iv.Count / 2]);
                if (iv.Count > 1) work.Add(iv[iv.Count - 1]);
                if (iii.Count > 0) work.Add(iii[0]);
                if (iii.Count > 1) work.Add(iii[iii.Count - 1]);
            }
            else if (limit > 0 && all.Count > limit) work = all.GetRange(0, limit);

            List<FxwRecipe> recipes = new List<FxwRecipe>();
            HashSet<string> seenSlugs = new HashSet<string>();
            foreach (RecipeLink link in work)
            {
                try
                {
                    FxwRecipe r = ParseRecipe(link);
                    if (r == null) { failed.Add(Fail(link.Url, "page sans recette exploitable")); continue; }
                    if (seenSlugs.Contains(r.Slug)) { Log("  doublon ignoré : {0}", r.Slug); continue; }
                    seenSlugs.Add(r.Slug);
                    WriteRecipeJson(r);
                    recipes.Add(r);
                    Log("OK  [{0}] {1}  ({2}, métadonnées uniquement)", r.Compat, r.Name, r.ExtractionStatus);
                    System.Threading.Thread.Sleep(250); // politesse envers le site
                }
                catch (Exception ex)
                {
                    Log("ECHEC {0} : {1}", link.Url, ex.Message);
                    failed.Add(Fail(link.Url, ex.Message));
                }
            }

            WriteIndexes(recipes, failed, all.Count, selectedCatalogs,
                UrlListFile != null ? "CURATED_SELECTION" : allCatalogs ? "ALL_CATALOGS" : "XT30_RELEVANT");
            Log("");
            Log("Terminé : {0} recettes extraites, {1} échecs, aucune image collectée.", recipes.Count, failed.Count);
            return 0;
        }

        static void PrintHelp()
        {
            Console.WriteLine("FxwImporter — import de réglages/métadonnées, sans aucune image");
            Console.WriteLine("  --xt30 | --full     X-Trans IV + III (défaut)");
            Console.WriteLine("  --all               Tous les catalogues publics");
            Console.WriteLine("  --permission-confirmed  Requis avec --all après autorisation écrite");
            Console.WriteLine("  --test               Limite le périmètre X-T30 à 5 recettes");
            Console.WriteLine("  --limit N            Limite le nombre de recettes");
            Console.WriteLine("  --out CHEMIN         Dossier library de destination");
        }

        static Dictionary<string, object> Fail(string url, string reason)
        {
            return new Dictionary<string, object> { { "url", url }, { "reason", reason } };
        }

        // ------------------------- HTTP -------------------------
        static string Fetch(string url)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) XT30RecipeManager-Importer/1.0";
                try { return wc.DownloadString(url); }
                catch (WebException)
                {
                    System.Threading.Thread.Sleep(1500);
                    return wc.DownloadString(url); // un seul retry
                }
            }
        }

        // ------------------------- Listes -------------------------
        static void CollectLinks(string listUrl, string generation, List<RecipeLink> output)
        {
            string html = Fetch(listUrl);
            // Parcours séquentiel : on retient le dernier titre de section rencontré
            // pour donner un groupe à chaque lien (ex : « X-T3 & X-T30 » vs « X100V... »).
            string group = generation;
            Regex token = new Regex("<(h[1-6])[^>]*>(.*?)</\\1>|<a\\s+[^>]*href=\"(https?://fujixweekly\\.com/20[0-9]{2}/[^\"#]+)\"[^>]*>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int count = 0;
            foreach (Match m in token.Matches(html))
            {
                if (m.Groups[2].Success)
                {
                    string heading = CleanText(m.Groups[2].Value);
                    if (heading.Length > 0 && heading.Length < 120) group = generation + " — " + heading;
                }
                else if (m.Groups[3].Success)
                {
                    string url = m.Groups[3].Value;
                    if (url.Contains("/comment") || url.EndsWith(".jpg") || url.EndsWith(".png")) continue;
                    output.Add(new RecipeLink { Url = url, SourceListUrl = listUrl, Group = group });
                    count++;
                }
            }
            Log("Liste {0} : {1} liens.", listUrl, count);
        }

        static string Canonicalize(string url)
        {
            return url.TrimEnd('/').ToLowerInvariant();
        }

        // ------------------------- Extraction d'une recette -------------------------
        static FxwRecipe ParseRecipe(RecipeLink link)
        {
            string html = Fetch(link.Url);
            FxwRecipe r = new FxwRecipe();
            r.Group = link.Group;
            r.CanonicalUrl = Meta(html, "<link[^>]+rel=\"canonical\"[^>]+href=\"([^\"]+)\"") ?? link.Url;
            r.Slug = SlugFromUrl(r.CanonicalUrl);
            r.Name = CleanText(Meta(html, "<meta[^>]+property=\"og:title\"[^>]+content=\"([^\"]+)\"") ?? Meta(html, "<title>([^<]+)</title>") ?? r.Slug);
            r.Name = Regex.Replace(r.Name, "\\s*[·|–-]\\s*FUJI X WEEKLY.*$", "", RegexOptions.IgnoreCase).Trim();
            r.PublishedAt = Meta(html, "<meta[^>]+property=\"article:published_time\"[^>]+content=\"([^\"]+)\"");
            r.Author = CleanText(Meta(html, "<meta[^>]+name=\"author\"[^>]+content=\"([^\"]+)\"")
                ?? Meta(html, "class=\"author[^\"]*\"[^>]*>(?:<a[^>]*>)?([^<]+)"));

            // Corps de l'article
            string body = html;
            Match content = Regex.Match(html, "<div[^>]+class=\"[^\"]*entry-content[^\"]*\"[^>]*>", RegexOptions.IgnoreCase);
            if (content.Success) body = html.Substring(content.Index);
            int footer = body.IndexOf("<footer", StringComparison.OrdinalIgnoreCase);
            if (footer > 0) body = body.Substring(0, footer);

            ParseSettings(body, r);

            // Garde anti-éditorial : une page recette doit fournir la simulation
            // ET un minimum de réglages ; sinon on la rejette explicitement.
            if (!r.Settings.ContainsKey("filmSimulation") || r.Settings.Count < 4) return null;
            // « Comments on: … » est le flux de commentaires d'un article, pas une recette.
            if (Regex.IsMatch(r.Name, "^(which|why|how|when|top \\d|best |the best |comparing|ranking|no edit|comments on)", RegexOptions.IgnoreCase)) return null;

            DetectCameras(body, r);
            Classify(r);

            // Statut d'extraction
            foreach (string key in SettingKeys)
                if (!r.Settings.ContainsKey(key)) r.MissingFields.Add(key);
            int found = SettingKeys.Length - r.MissingFields.Count;
            r.ExtractionStatus = (r.Settings.ContainsKey("filmSimulation") && found >= 6) ? "VERIFIED" : "PARTIAL";
            return r;
        }

        static string Meta(string html, string pattern)
        {
            Match m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value.Trim()) : null;
        }

        static string SlugFromUrl(string url)
        {
            string[] parts = url.TrimEnd('/').Split('/');
            return parts[parts.Length - 1].ToLowerInvariant();
        }

        static string CleanText(string html)
        {
            string s = Regex.Replace(html ?? "", "<[^>]+>", " ");
            s = WebUtility.HtmlDecode(s);
            return Regex.Replace(s, "\\s+", " ").Trim();
        }

        // Découpe le corps en lignes de texte et cherche « Clé : Valeur ».
        static void ParseSettings(string body, FxwRecipe r)
        {
            string text = Regex.Replace(body, "<(br|/p|/li|/h[1-6]|/div)[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", "");
            text = WebUtility.HtmlDecode(text);
            string[] lines = text.Split('\n');
            foreach (string raw in lines)
            {
                string line = Regex.Replace(raw, "\\s+", " ").Trim();
                if (line.Length == 0 || line.Length > 200) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) { DetectBareFilmSim(line, r); continue; }
                string key = line.Substring(0, colon).Trim().TrimStart('•', '-', '*').Trim();
                string value = line.Substring(colon + 1).Trim();
                if (value.Length == 0) continue;
                AssignSetting(key, value, r);
            }
        }

        static void DetectBareFilmSim(string line, FxwRecipe r)
        {
            // Certaines vieilles pages donnent la simulation seule sur sa ligne.
            if (r.Settings.ContainsKey("filmSimulation")) return;
            string[] sims = { "PROVIA", "Velvia", "ASTIA", "Classic Chrome", "PRO Neg. Hi", "PRO Neg. Std",
                "PRO Neg Hi", "PRO Neg Std", "Eterna", "ETERNA", "Acros", "ACROS", "Monochrome", "Sepia",
                "Classic Negative", "Eterna Bleach Bypass", "Nostalgic Neg", "Reala Ace" };
            foreach (string sim in sims)
                if (line.Equals(sim, StringComparison.OrdinalIgnoreCase) ||
                    (line.StartsWith(sim, StringComparison.OrdinalIgnoreCase) && line.Length <= sim.Length + 12 && Regex.IsMatch(line, "\\+(Ye|R|G|Yellow|Red|Green)", RegexOptions.IgnoreCase)))
                { r.Settings["filmSimulation"] = line; return; }
        }

        static void AssignSetting(string key, string value, FxwRecipe r)
        {
            string k = key.ToLowerInvariant();
            // ordre : clés longues d'abord pour éviter les collisions (color chrome vs color)
            if (k.Contains("film simulation")) Set(r, "filmSimulation", value);
            else if (k.Contains("dynamic range priority") || k.Contains("d-range priority") || k.Contains("d range priority") || k == "drp") Set(r, "dRangePriority", value);
            else if (k.Contains("dynamic range")) Set(r, "dynamicRange", value);
            else if (k.Contains("color chrome") && k.Contains("blue")) Set(r, "colorChromeFXBlue", value);
            else if (k.Contains("color chrome")) Set(r, "colorChrome", value);
            else if (k.Contains("grain") && k.Contains("size")) Set(r, "grainSize", value);
            else if (k.Contains("grain")) ParseGrain(value, r);
            else if (k.Contains("smooth skin")) Set(r, "smoothSkin", value);
            else if (k.Contains("white balance") || k == "wb") ParseWhiteBalance(value, r);
            else if (k.Contains("highlight")) Set(r, "highlight", value);
            else if (k.Contains("shadow")) Set(r, "shadow", value);
            else if (k.Contains("sharp")) Set(r, "sharpness", value);
            else if (k.Contains("noise reduction") || k.Contains("high iso nr") || k == "nr") Set(r, "highIsoNR", value);
            else if (k.Contains("clarity")) Set(r, "clarity", value);
            else if (k.Contains("exposure compensation")) Set(r, "exposureCompensation", value);
            else if (k == "iso" || k.StartsWith("iso ")) Set(r, "iso", value);
            else if (k == "color" || k == "colour") Set(r, "color", value);
            else if (k.Contains("toning") || k.Contains("monochromatic color")) Set(r, "monochromaticColor", value);
        }

        static void Set(FxwRecipe r, string key, string value)
        {
            if (!r.Settings.ContainsKey(key)) r.Settings[key] = value.Trim();
        }

        static void ParseGrain(string value, FxwRecipe r)
        {
            // « Weak, Small » (récents) ou « Strong » (anciens)
            Match m = Regex.Match(value, "^(off|weak|strong)\\s*,\\s*(small|large)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                Set(r, "grain", Cap(m.Groups[1].Value));
                Set(r, "grainSize", Cap(m.Groups[2].Value));
            }
            else Set(r, "grain", value);
        }

        static string Cap(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant(); }

        static void ParseWhiteBalance(string value, FxwRecipe r)
        {
            // Exemple : « Auto, +2 Red & -5 Blue » ou « 5200K, -2 Red & -5 Blue »
            Match shift = Regex.Match(value, "([+-]?\\s?\\d+)\\s*Red.*?([+-]?\\s?\\d+)\\s*Blue", RegexOptions.IgnoreCase);
            if (shift.Success)
            {
                Set(r, "wbShiftR", shift.Groups[1].Value.Replace(" ", ""));
                Set(r, "wbShiftB", shift.Groups[2].Value.Replace(" ", ""));
                int comma = value.IndexOf(',');
                Set(r, "whiteBalance", comma > 0 ? value.Substring(0, comma).Trim() : value.Trim());
            }
            else Set(r, "whiteBalance", value);
        }

        static void DetectCameras(string body, FxwRecipe r)
        {
            string text = CleanText(body);
            string[] cams = { "X-T30 II", "X-T30", "X-T3", "X-T4", "X100V", "X100F", "X-Pro3", "X-Pro2", "X-S10", "X-E4", "X-E3", "X-T2", "X-T20", "X-H1", "X-T5", "X-H2", "X-S20", "X100VI" };
            foreach (string cam in cams)
                if (Regex.IsMatch(text, Regex.Escape(cam) + "(?![\\w-])") && !r.CompatibleCameras.Contains(cam))
                    r.CompatibleCameras.Add(cam);
        }

        // ------------------------- Compatibilité X-T30 gen 1 -------------------------
        static readonly string[] Xt30Sims = { "provia", "velvia", "astia", "classic chrome", "pro neg", "eterna", "acros", "monochrome", "sepia" };
        static readonly string[] BannedSims = { "classic negative", "classic neg", "bleach bypass", "nostalgic", "reala" };

        static void Classify(FxwRecipe r)
        {
            List<string> hard = new List<string>();
            List<string> partial = new List<string>();
            string sim;
            if (!r.Settings.TryGetValue("filmSimulation", out sim))
            {
                r.Compat = "UNVERIFIED";
                r.CompatReason = "Film simulation not found on the page; compatibility cannot be assessed.";
                return;
            }
            string simLow = sim.ToLowerInvariant();
            foreach (string banned in BannedSims)
                if (simLow.Contains(banned)) hard.Add("Film simulation \"" + sim + "\" does not exist on the X-T30.");
            if (hard.Count == 0)
            {
                bool known = false;
                foreach (string okSim in Xt30Sims) if (simLow.Contains(okSim)) { known = true; break; }
                if (!known) partial.Add("Film simulation \"" + sim + "\" could not be matched to the X-T30 set.");
            }
            string v;
            if (r.Settings.TryGetValue("clarity", out v) && NotNeutral(v)) partial.Add("Clarity " + v + " is unavailable on the X-T30 (set it to 0 / ignore).");
            if (r.Settings.TryGetValue("colorChromeFXBlue", out v) && NotNeutral(v)) partial.Add("Color Chrome FX Blue " + v + " is unavailable on the X-T30.");
            if (r.Settings.TryGetValue("grainSize", out v) && NotNeutral(v)) partial.Add("Grain size (" + v + ") cannot be chosen on the X-T30 (single grain size).");
            if (r.Settings.TryGetValue("smoothSkin", out v) && NotNeutral(v)) partial.Add("Smooth Skin Effect " + v + " is unavailable on the X-T30.");
            if (r.Settings.TryGetValue("highlight", out v) && IsHalfStep(v)) partial.Add("Half-step tone value Highlight " + v + " must be rounded on the X-T30.");
            if (r.Settings.TryGetValue("shadow", out v) && IsHalfStep(v)) partial.Add("Half-step tone value Shadow " + v + " must be rounded on the X-T30.");

            if (hard.Count > 0)
            {
                r.Compat = "XT30_INCOMPATIBLE";
                r.CompatReason = string.Join(" ", hard.ToArray());
            }
            else if (partial.Count > 0)
            {
                r.Compat = "XT30_PARTIAL";
                r.CompatReason = string.Join(" ", partial.ToArray());
            }
            else
            {
                r.Compat = "XT30_COMPATIBLE";
                r.CompatReason = r.Group != null && r.Group.Contains("X-Trans III")
                    ? "All published settings exist on the X-T30 (X-Trans III recipe; rendering may differ slightly on X-Trans IV)."
                    : "All published settings exist on the X-T30 feature set.";
            }
        }

        static bool NotNeutral(string v)
        {
            string s = v.Trim().ToLowerInvariant();
            return s != "0" && s != "off" && s != "none" && s != "n/a" && s != "-" && s != "not available";
        }

        static bool IsHalfStep(string v)
        {
            return Regex.IsMatch(v, "[+-]?\\d+[.,]5|1/2");
        }

        // ------------------------- Sorties -------------------------
        static Dictionary<string, object> RecipeToJson(FxwRecipe r)
        {
            Dictionary<string, object> settings = new Dictionary<string, object>();
            foreach (string key in SettingKeys)
            {
                string v;
                settings[key] = r.Settings.TryGetValue(key, out v) ? (object)v : null;
            }
            string mono;
            if (r.Settings.TryGetValue("monochromaticColor", out mono)) settings["monochromaticColor"] = mono;
            return new Dictionary<string, object> {
                { "name", r.Name }, { "slug", r.Slug },
                { "source", new Dictionary<string, object> {
                    { "site", "Fuji X Weekly" }, { "articleUrl", r.CanonicalUrl },
                    { "author", r.Author }, { "publishedAt", r.PublishedAt } } },
                { "compatibility", new Dictionary<string, object> {
                    { "xt30Original", r.Compat }, { "group", r.Group }, { "reason", r.CompatReason },
                    { "camerasMentioned", r.CompatibleCameras } } },
                { "settings", settings },
                { "images", new Dictionary<string, object> {
                    { "cover", null }, { "examples", new List<object>() }, { "all", new List<object>() } } },
                { "extraction", new Dictionary<string, object> {
                    { "status", r.ExtractionStatus }, { "missingFields", r.MissingFields } } }
            };
        }

        static void WriteRecipeJson(FxwRecipe r)
        {
            string dir = Path.Combine(LibraryDir, "recipes", r.Slug);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "recipe.json"), Json.Serialize(RecipeToJson(r)), new UTF8Encoding(false));
        }

        static void WriteIndexes(List<FxwRecipe> recipes, List<object> failed, int detected, List<Catalog> selectedCatalogs, string scope)
        {
            List<object> items = new List<object>();
            foreach (FxwRecipe r in recipes) items.Add(RecipeToJson(r));
            Dictionary<string, object> index = new Dictionary<string, object> {
                { "generatedAt", DateTime.Now.ToString("o") },
                { "site", "Fuji X Weekly" }, { "idPrefix", "fxw-" },
                { "source", "Fuji X Weekly (recipe metadata; no images copied)" },
                { "target", "Fujifilm X-T30 (first generation)" },
                { "scope", scope },
                { "detectedLinks", detected }, { "extracted", recipes.Count },
                { "images", 0 }, { "recipes", items } };
            // Un import par liste d'URL écrit son propre index : il complète le
            // catalogue par génération au lieu de l'écraser.
            string stem = UrlListFile == null ? "fuji_x_weekly_xt30_full" : "fuji_x_weekly_selection";
            File.WriteAllText(Path.Combine(LibraryDir, "index", stem + ".json"), Json.Serialize(index), new UTF8Encoding(false));

            // CSV
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("name;slug;compat;filmSimulation;dynamicRange;whiteBalance;wbShiftR;wbShiftB;highlight;shadow;color;sharpness;highIsoNR;grain;grainSize;colorChrome;colorChromeFXBlue;clarity;iso;exposureCompensation;publishedAt;articleUrl");
            foreach (FxwRecipe r in recipes)
            {
                string[] cols = { r.Name, r.Slug, r.Compat,
                    Val(r,"filmSimulation"), Val(r,"dynamicRange"), Val(r,"whiteBalance"), Val(r,"wbShiftR"), Val(r,"wbShiftB"),
                    Val(r,"highlight"), Val(r,"shadow"), Val(r,"color"), Val(r,"sharpness"), Val(r,"highIsoNR"),
                    Val(r,"grain"), Val(r,"grainSize"), Val(r,"colorChrome"), Val(r,"colorChromeFXBlue"), Val(r,"clarity"),
                    Val(r,"iso"), Val(r,"exposureCompensation"), r.PublishedAt ?? "", r.CanonicalUrl };
                for (int i = 0; i < cols.Length; i++) { if (cols[i] == null) cols[i] = ""; cols[i] = cols[i].Replace(';', ','); }
                csv.AppendLine(string.Join(";", cols));
            }
            File.WriteAllText(Path.Combine(LibraryDir, "index", stem + ".csv"), csv.ToString(), new UTF8Encoding(true));

            // Rapports
            int compat = 0, partial = 0, incompatible = 0, unverified = 0, verified = 0;
            List<object> perRecipe = new List<object>();
            foreach (FxwRecipe r in recipes)
            {
                if (r.Compat == "XT30_COMPATIBLE") compat++;
                else if (r.Compat == "XT30_PARTIAL") partial++;
                else if (r.Compat == "XT30_INCOMPATIBLE") incompatible++;
                else unverified++;
                if (r.ExtractionStatus == "VERIFIED") verified++;
                perRecipe.Add(new Dictionary<string, object> { { "slug", r.Slug }, { "compat", r.Compat }, { "reason", r.CompatReason }, { "extraction", r.ExtractionStatus } });
            }
            File.WriteAllText(Path.Combine(LibraryDir, "reports", "compatibility_report.json"), Json.Serialize(new Dictionary<string, object> {
                { "generatedAt", DateTime.Now.ToString("o") },
                { "XT30_COMPATIBLE", compat }, { "XT30_PARTIAL", partial },
                { "XT30_INCOMPATIBLE", incompatible }, { "UNVERIFIED", unverified },
                { "extractionVerified", verified }, { "withCover", 0 },
                { "recipes", perRecipe } }), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(LibraryDir, "reports", "failed_urls.json"), Json.Serialize(failed), new UTF8Encoding(false));
            List<object> sourceLists = new List<object>();
            foreach (Catalog catalog in selectedCatalogs) sourceLists.Add(catalog.Url);
            File.WriteAllText(Path.Combine(LibraryDir, "reports", "sources.json"), Json.Serialize(new Dictionary<string, object> {
                { "site", "Fuji X Weekly — https://fujixweekly.com" },
                { "lists", sourceLists },
                { "scope", scope },
                { "imagePolicy", "No image is collected, referenced, or downloaded." },
                { "note", "Recipe metadata remains attributed to Fuji X Weekly and its authors. Full-catalog execution requires written authorization." } }), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(LibraryDir, "reports", "import.log"), string.Join("\r\n", LogLines.ToArray()), new UTF8Encoding(false));
        }

        static string Val(FxwRecipe r, string key)
        {
            string v; return r.Settings.TryGetValue(key, out v) ? v : "";
        }
    }
}
