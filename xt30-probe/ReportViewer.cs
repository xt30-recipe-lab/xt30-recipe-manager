// ============================================================================
// xt30-probe — Visualiseur de rapports de scan et de recettes détectées
// Lit les fichiers xt30_report*.json (aucun accès à l'appareil).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Xt30Probe
{
    public class RecipesForm : Form
    {
        static readonly Color Body = Color.FromArgb(28, 28, 30);
        static readonly Color LcdBg = Color.FromArgb(8, 11, 9);
        static readonly Color ColOk = Color.FromArgb(110, 230, 120);
        static readonly Color ColWait = Color.FromArgb(240, 195, 95);
        static readonly Color ColErr = Color.FromArgb(245, 115, 100);
        static readonly Color Silver = Color.FromArgb(210, 210, 214);

        // Codes du bloc recette, dans l'ordre d'affichage
        static readonly ushort[] RecipeCodes = new ushort[] {
            0xD18C, 0xD18D, 0xD192, 0xD190, 0xD191, 0xD195, 0xD196, 0xD197, 0xD198,
            0xD199, 0xD19C, 0xD19A, 0xD19B, 0xD19D, 0xD19E, 0xD19F, 0xD1A0, 0xD1A1,
            0xD1A2, 0xD193, 0xD194
        };

        string _baseDir;
        ComboBox _combo;
        Label _device;
        ListView _recipe;
        ListView _all;
        Label _recipeTitle;

        public RecipesForm(string baseDir)
        {
            _baseDir = baseDir;

            Text = "XT30 Recipe Manager — Recettes détectées";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(860, 640);
            MinimumSize = new Size(700, 480);
            BackColor = Body;
            Font = new Font("Segoe UI", 9f);

            // ---- Bandeau ----
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 76;
            top.BackColor = Body;

            Label title = new Label();
            title.Text = "R E C E T T E S   /   R A P P O R T S   D E   S C A N";
            title.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
            title.ForeColor = Silver;
            title.AutoSize = true;
            title.Location = new Point(16, 10);

            Label comboLbl = new Label();
            comboLbl.Text = "Scan :";
            comboLbl.ForeColor = Color.FromArgb(160, 160, 165);
            comboLbl.AutoSize = true;
            comboLbl.Location = new Point(18, 44);

            _combo = new ComboBox();
            _combo.DropDownStyle = ComboBoxStyle.DropDownList;
            _combo.Location = new Point(62, 40);
            _combo.Size = new Size(420, 26);
            _combo.BackColor = Color.FromArgb(45, 45, 49);
            _combo.ForeColor = Silver;
            _combo.FlatStyle = FlatStyle.Flat;
            _combo.SelectedIndexChanged += delegate(object s, EventArgs e) { LoadSelected(); };

            Button refresh = new Button();
            refresh.Text = "ACTUALISER";
            refresh.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            refresh.Size = new Size(100, 26);
            refresh.Location = new Point(492, 40);
            refresh.FlatStyle = FlatStyle.Flat;
            refresh.BackColor = Color.FromArgb(45, 45, 49);
            refresh.ForeColor = Silver;
            refresh.FlatAppearance.BorderColor = Color.FromArgb(105, 105, 112);
            refresh.Click += delegate(object s, EventArgs e) { FillCombo(); };

            top.Controls.Add(title);
            top.Controls.Add(comboLbl);
            top.Controls.Add(_combo);
            top.Controls.Add(refresh);

            // ---- Ligne appareil ----
            _device = new Label();
            _device.Dock = DockStyle.Top;
            _device.Height = 30;
            _device.Font = new Font("Consolas", 10f, FontStyle.Bold);
            _device.ForeColor = ColWait;
            _device.BackColor = LcdBg;
            _device.TextAlign = ContentAlignment.MiddleLeft;
            _device.Padding = new Padding(14, 0, 0, 0);
            _device.Text = "—";

            // ---- Bloc recette ----
            _recipeTitle = new Label();
            _recipeTitle.Dock = DockStyle.Top;
            _recipeTitle.Height = 26;
            _recipeTitle.Text = "  BLOC RECETTE (réglages exposés par l'appareil)";
            _recipeTitle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _recipeTitle.ForeColor = Silver;
            _recipeTitle.BackColor = Body;
            _recipeTitle.TextAlign = ContentAlignment.BottomLeft;

            _recipe = MakeList();
            _recipe.Dock = DockStyle.Top;
            _recipe.Height = 230;
            _recipe.Columns.Add("Paramètre", 260);
            _recipe.Columns.Add("Valeur décodée", 300);
            _recipe.Columns.Add("Brut", 180);

            // ---- Table complète ----
            Label allTitle = new Label();
            allTitle.Dock = DockStyle.Top;
            allTitle.Height = 26;
            allTitle.Text = "  TOUTES LES PROPRIÉTÉS SONDÉES";
            allTitle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            allTitle.ForeColor = Silver;
            allTitle.BackColor = Body;
            allTitle.TextAlign = ContentAlignment.BottomLeft;

            _all = MakeList();
            _all.Dock = DockStyle.Fill;
            _all.Columns.Add("Code", 70);
            _all.Columns.Add("Nom", 250);
            _all.Columns.Add("Statut", 130);
            _all.Columns.Add("Type", 70);
            _all.Columns.Add("Écriture ?", 70);
            _all.Columns.Add("Valeur", 220);

            Controls.Add(_all);
            Controls.Add(allTitle);
            Controls.Add(_recipe);
            Controls.Add(_recipeTitle);
            Controls.Add(_device);
            Controls.Add(top);

            // Charger apres l'affichage : des items ajoutes avant la creation du
            // handle de la ListView peuvent ne jamais etre peints.
            Shown += delegate(object s, EventArgs e) { FillCombo(); };
        }

        ListView MakeList()
        {
            ListView lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.BackColor = LcdBg;
            lv.ForeColor = Color.FromArgb(180, 215, 185);
            lv.Font = new Font("Consolas", 9f);
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.BorderStyle = BorderStyle.None;
            return lv;
        }

        class ReportEntry
        {
            public string Label;
            public string Path;
            public override string ToString() { return Label; }
        }

        void FillCombo()
        {
            _combo.Items.Clear();
            string current = Path.Combine(_baseDir, "xt30_report.json");
            if (File.Exists(current))
            {
                ReportEntry e = new ReportEntry();
                e.Label = "Dernier scan (xt30_report.json — " +
                    File.GetLastWriteTime(current).ToString("dd/MM/yyyy HH:mm") + ")";
                e.Path = current;
                _combo.Items.Add(e);
            }
            string archDir = Path.Combine(_baseDir, "rapports");
            if (Directory.Exists(archDir))
            {
                string[] files = Directory.GetFiles(archDir, "xt30_report_*.json");
                Array.Sort(files);
                Array.Reverse(files);
                foreach (string f in files)
                {
                    ReportEntry e = new ReportEntry();
                    e.Label = "Archive : " + Path.GetFileNameWithoutExtension(f).Replace("xt30_report_", "");
                    e.Path = f;
                    _combo.Items.Add(e);
                }
            }
            if (_combo.Items.Count > 0) _combo.SelectedIndex = 0;
            else
            {
                _device.Text = "AUCUN RAPPORT TROUVÉ — lancez d'abord un scan.";
                _device.ForeColor = ColErr;
            }
        }

        static Dictionary<string, object> AsDict(object o) { return o as Dictionary<string, object>; }
        static List<object> AsList(object o) { return o as List<object>; }
        static string S(object o) { return o == null ? "" : o.ToString(); }

        void LoadSelected()
        {
            ReportEntry entry = _combo.SelectedItem as ReportEntry;
            if (entry == null) return;
            _recipe.BeginUpdate();
            _all.BeginUpdate();
            try
            {
                LoadSelectedCore(entry);
            }
            finally
            {
                _recipe.EndUpdate();
                _all.EndUpdate();
            }
        }

        void LoadSelectedCore(ReportEntry entry)
        {
            _recipe.Items.Clear();
            _all.Items.Clear();
            Dictionary<string, object> report;
            try
            {
                report = AsDict(Json.Parse(File.ReadAllText(entry.Path)));
                if (report == null) throw new FormatException("racine JSON invalide");
            }
            catch (Exception ex)
            {
                _device.Text = "RAPPORT ILLISIBLE : " + ex.Message;
                _device.ForeColor = ColErr;
                return;
            }

            Dictionary<string, object> dev = AsDict(report.ContainsKey("device") ? report["device"] : null);
            Dictionary<string, object> di = (dev != null && dev.ContainsKey("deviceInfo")) ? AsDict(dev["deviceInfo"]) : null;

            if (di != null)
            {
                _device.Text = string.Format("{0} {1}  |  firmware {2}  |  s/n {3}  |  scan du {4}",
                    S(di["manufacturer"]), S(di["model"]), S(di["deviceVersion"]),
                    Shorten(S(di["serialNumber"]), 14),
                    report.ContainsKey("generatedAt") ? FormatDate(S(report["generatedAt"])) : "?");
                _device.ForeColor = ColOk;
            }
            else
            {
                _device.Text = "Aucun appareil dans ce rapport (scan sans Fujifilm connecté).";
                _device.ForeColor = ColErr;
            }

            // Index des propriétés par code
            Dictionary<ushort, Dictionary<string, object>> byCode = new Dictionary<ushort, Dictionary<string, object>>();
            List<object> props = (dev != null && dev.ContainsKey("properties")) ? AsList(dev["properties"]) : null;
            List<object> sweep = (dev != null && dev.ContainsKey("sweepDiscoveries")) ? AsList(dev["sweepDiscoveries"]) : null;
            List<Dictionary<string, object>> allProps = new List<Dictionary<string, object>>();
            if (props != null) foreach (object o in props) { Dictionary<string, object> d = AsDict(o); if (d != null) allProps.Add(d); }
            if (sweep != null) foreach (object o in sweep) { Dictionary<string, object> d = AsDict(o); if (d != null) allProps.Add(d); }
            foreach (Dictionary<string, object> p in allProps)
            {
                try
                {
                    ushort code = Convert.ToUInt16(S(p["code"]).Replace("0x", ""), 16);
                    byCode[code] = p;
                }
                catch (Exception) { }
            }

            // ---- Bloc recette ----
            int found = 0;
            foreach (ushort code in RecipeCodes)
            {
                Dictionary<string, object> p;
                if (!byCode.TryGetValue(code, out p)) continue;
                Dictionary<string, object> desc = p.ContainsKey("desc") ? AsDict(p["desc"]) : null;
                if (desc == null) continue;
                found++;
                object cur = desc.ContainsKey("currentValue") ? desc["currentValue"] : null;
                string decoded;
                if (cur is long) decoded = RecipeDecode.For(code, (long)cur);
                else decoded = S(cur);
                ListViewItem it = new ListViewItem(string.Format("0x{0:X4}  {1}", code, KnownProps.NameOf(code)));
                it.SubItems.Add(decoded);
                it.SubItems.Add(S(cur));
                it.ForeColor = ColOk;
                _recipe.Items.Add(it);
            }
            if (found == 0)
            {
                ListViewItem it = new ListViewItem("AUCUNE PROPRIÉTÉ RECETTE EXPOSÉE PAR L'APPAREIL DANS CE SCAN");
                it.ForeColor = ColErr;
                _recipe.Items.Add(it);
                ListViewItem it2 = new ListViewItem("(mode USB actuel / génération X-T30 — voir docs/01-synthese-recherche.md)");
                it2.ForeColor = Color.FromArgb(150, 150, 155);
                _recipe.Items.Add(it2);
                ListViewItem it3 = new ListViewItem("Les slots C1-C7 ne sont pas accessibles par propriétés PTP sur ce scan.");
                it3.ForeColor = Color.FromArgb(150, 150, 155);
                _recipe.Items.Add(it3);
            }

            // ---- Table complète ----
            List<ushort> codes = new List<ushort>(byCode.Keys);
            codes.Sort();
            foreach (ushort code in codes)
            {
                Dictionary<string, object> p = byCode[code];
                Dictionary<string, object> desc = p.ContainsKey("desc") ? AsDict(p["desc"]) : null;
                ListViewItem it = new ListViewItem(string.Format("0x{0:X4}", code));
                it.SubItems.Add(KnownProps.NameOf(code));
                if (desc != null)
                {
                    bool writable = desc.ContainsKey("writableAccordingToDescriptor") &&
                                    desc["writableAccordingToDescriptor"] is bool &&
                                    (bool)desc["writableAccordingToDescriptor"];
                    object cur = desc.ContainsKey("currentValue") ? desc["currentValue"] : null;
                    it.SubItems.Add("SUPPORTÉE");
                    it.SubItems.Add(S(desc.ContainsKey("datatype") ? desc["datatype"] : ""));
                    it.SubItems.Add(writable ? "OUI" : "non");
                    string val;
                    if (cur is long) val = RecipeDecode.For(code, (long)cur);
                    else val = Shorten(S(cur), 40);
                    it.SubItems.Add(val);
                    it.ForeColor = ColOk;
                }
                else
                {
                    string status = S(p.ContainsKey("descResponse") ? p["descResponse"] : "erreur");
                    int par = status.IndexOf('(');
                    if (par >= 0) status = status.Substring(par + 1).TrimEnd(')');
                    it.SubItems.Add(status);
                    it.SubItems.Add("-");
                    it.SubItems.Add("-");
                    it.SubItems.Add("-");
                    it.ForeColor = Color.FromArgb(130, 130, 135);
                }
                _all.Items.Add(it);
            }
        }

        static string Shorten(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        static string FormatDate(string iso)
        {
            DateTime dt;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                return dt.ToString("dd/MM/yyyy HH:mm");
            return iso;
        }
    }
}
