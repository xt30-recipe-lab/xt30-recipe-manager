using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    // Valeurs proposées pour chaque réglage. Partagé par l'éditeur de recette et
    // par la page vidéo : une seule source, impossible qu'ils divergent.
    public static class RecipeFields
    {
        public static readonly string[] MovieModes = { "4K 30P", "4K 25P", "4K 24P", "FHD 60P", "FHD 50P", "FHD 30P", "FHD 25P", "FHD 24P", "FHD 120P (high speed)" };
        public static readonly string[] LogModes = { "Off", "F-Log" };
        // Réduction du bruit inter-image : réglage du menu vidéo, sans équivalent
        // photo. La notice ne donne que ON et OFF — pas de niveaux intermédiaires.
        public static readonly string[] InterframeNr = { "Off", "On" };
        // Le mode film n'offre pas la priorité plage dynamique : pas de DR-P ici.
        public static readonly string[] MovieDynamicRanges = { "DR100", "DR200", "DR400" };

        public static string[] Choices(string key, bool video)
        {
            switch (key)
            {
                case "Movie Mode": return MovieModes;
                case "F-Log": return LogModes;
                case "Interframe NR": return InterframeNr;
                case "Film Simulation": return CameraBankFile.FilmSimulations;
                case "Dynamic Range": return video ? MovieDynamicRanges : CameraBankFile.DynamicRanges;
                case "Dynamic Range Priority": return CameraBankFile.DrPriorities;
                case "White Balance": return CameraBankFile.WhiteBalances();
                case "Grain Effect": return CameraBankFile.GrainEffects;
                case "Color Chrome Effect": return CameraBankFile.ChromeEffects;
                case "WB Shift R": case "WB Shift B": return CameraBankFile.Scale(-9, 9);
                // Le X-T30 s'arrête à -2 en ton lumière et ton ombre ; le réglage
                // N&B (B&W ADJ) va lui de -9 à +9. Une seule échelle pour tout
                // proposait des valeurs que le boîtier n'a pas.
                case "Highlight": case "Shadow": return CameraBankFile.Scale(CameraBankFile.ToneFloorHighlightShadow, 4);
                case "Monochromatic Color": return CameraBankFile.Scale(-9, 9);
                case "Color": case "Sharpness": case "Noise Reduction": return CameraBankFile.Scale(-4, 4);
                default: return null;   // ISO : texte libre
            }
        }

        public static string Default(string key)
        {
            switch (key)
            {
                case "Film Simulation": return "Classic Chrome";
                case "Dynamic Range": return "DR100";
                case "Movie Mode": return "FHD 30P";
                case "F-Log": case "Interframe NR": return "Off";
                case "ISO": return "Auto";
                case "White Balance": return "Auto";
                case "Dynamic Range Priority": case "Grain Effect": case "Color Chrome Effect": return "Off";
                default: return "0";
            }
        }

        // Fiche à suivre sur le boîtier, dans l'ordre des menus.
        public static string Checklist(Recipe recipe)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.AppendLine(recipe.Name == "" ? "Movie settings" : recipe.Name);
            text.AppendLine();
            foreach (string key in Recipe.VideoParameterOrder)
            {
                string value = recipe.Get(key);
                if (value == "Not specified" || value.Trim() == "") continue;
                text.AppendLine("  " + key + " : " + value);
            }
            return text.ToString();
        }
    }

    // Page vidéo : les réglages du mode film, modifiables directement.
    //
    // Il n'y a pas de catalogue à parcourir ici — aucun site public ne publie de
    // recettes vidéo structurées pour ce boîtier — alors on affiche la fiche de
    // réglages elle-même. On peut l'enregistrer sous un nom pour la retrouver.
    //
    // Rien n'est jamais écrit dans l'appareil : le X-T30 ne range aucun réglage
    // du mode film dans son fichier de sauvegarde ni dans les banques C1-C7.
    public sealed class VideoStudioPanel : Panel
    {
        sealed class Saved
        {
            public readonly Recipe Recipe;
            public Saved(Recipe recipe) { Recipe = recipe; }
            public override string ToString() { return Recipe == null ? Strings.T("— new movie settings —") : Recipe.Name; }
        }

        readonly RecipeLibrary _library;
        readonly Dictionary<string, Control> _values = new Dictionary<string, Control>();
        // Étiquettes et champs dans l'ordre des réglages : la mise en page les
        // repositionne selon la largeur disponible, une ou deux colonnes.
        readonly List<Label> _labels = new List<Label>();
        readonly List<Control> _inputs = new List<Control>();
        readonly TextBox _name = new TextBox();
        readonly ComboBox _saved = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ActionButton _save = new ActionButton("Save these settings", true);
        readonly ActionButton _copy = new ActionButton("Copy as a checklist", false);
        readonly ActionButton _delete = new ActionButton("Delete", false) { Quiet = true };
        readonly Label _summary = new Label();
        bool _loading;
        bool _ready;

        public event EventHandler LibraryChanged;

        public VideoStudioPanel(RecipeLibrary library)
        {
            _library = library;
            BackColor = Color.White;
            AutoScroll = true;
            DoubleBuffered = true;

            _name.SetBounds(150, 96, 260, 27); _name.MaxLength = 80; _name.BorderStyle = BorderStyle.FixedSingle;
            _name.Font = Theme.Font(14, false);
            Controls.Add(_name);

            _saved.SetBounds(600, 95, 280, 28); _saved.Font = Theme.Font(13, false); _saved.FlatStyle = FlatStyle.Flat;
            _saved.AccessibleName = "Saved movie settings";
            _saved.SelectedIndexChanged += delegate { if (!_loading) LoadSelected(); };
            Controls.Add(_saved);

            foreach (string key in Recipe.VideoParameterOrder) AddParameter(key);

            _summary.Font = Theme.Font(12, false); _summary.ForeColor = Theme.Muted; Controls.Add(_summary);
            Controls.AddRange(new Control[] { _save, _copy, _delete });
            _save.Click += delegate { SaveSettings(); };
            _copy.Click += delegate { CopyChecklist(); };
            _delete.ForeColor = Theme.Muted;
            _delete.Click += delegate { DeleteSelected(); };

            _ready = true;
            Reload();
        }

        // Deux colonnes quand la fenêtre le permet, une seule sinon : la deuxième
        // colonne était coupée sur une fenêtre étroite.
        int Columns { get { return ClientSize.Width >= 900 ? 2 : 1; } }
        int PerColumn { get { int c = Columns; return (Recipe.VideoParameterOrder.Length + c - 1) / c; } }
        int BottomRow { get { return 156 + PerColumn * 40 + 16; } }

        void AddParameter(string key)
        {
            Label label = new Label()
            {
                Text = Strings.T(key), Font = Theme.Font(13, false), ForeColor = Theme.Muted,
                Size = new Size(176, 27), TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(label); _labels.Add(label);

            string current = RecipeFields.Default(key);
            string[] choices = RecipeFields.Choices(key, true);
            Control input;
            if (choices == null)
            {
                TextBox box = new TextBox() { Size = new Size(200, 27), Text = current, AccessibleName = key, BorderStyle = BorderStyle.FixedSingle };
                box.TextChanged += delegate { if (!_loading) UpdateSummary(); };
                input = box;
            }
            else
            {
                ComboBox combo = new ComboBox() { Size = new Size(200, 27), DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = key, FlatStyle = FlatStyle.Flat };
                combo.Items.AddRange(choices);
                combo.SelectedItem = current;
                if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                combo.SelectedIndexChanged += delegate { if (!_loading) UpdateSummary(); };
                input = combo;
            }
            Controls.Add(input); _values[key] = input; _inputs.Add(input);
        }

        static string ValueOf(Control c) { ComboBox combo = c as ComboBox; return combo != null ? Convert.ToString(combo.SelectedItem) : ((TextBox)c).Text; }

        static void SetValue(Control c, string value)
        {
            ComboBox combo = c as ComboBox;
            if (combo == null) { ((TextBox)c).Text = value; return; }
            int index = combo.Items.IndexOf(value);
            // Valeur venue d'ailleurs et absente du boîtier : on la montre telle
            // quelle plutôt que de la remplacer en silence.
            if (index < 0 && value != "" && value != "Not specified") { combo.Items.Insert(0, value); index = 0; }
            combo.SelectedIndex = Math.Max(0, index);
        }

        // Recharge la liste des fiches enregistrées en gardant la sélection.
        public void Reload()
        {
            if (!_ready) return;
            string selected = _saved.SelectedItem == null ? null : Convert.ToString(_saved.SelectedItem);
            _loading = true;
            _saved.Items.Clear();
            _saved.Items.Add(new Saved(null));
            foreach (Recipe r in _library.Recipes) if (r.IsVideo) _saved.Items.Add(new Saved(r));
            _saved.SelectedIndex = 0;
            if (selected != null)
                for (int i = 0; i < _saved.Items.Count; i++)
                    if (Convert.ToString(_saved.Items[i]) == selected) { _saved.SelectedIndex = i; break; }
            _loading = false;
            LoadSelected();
        }

        Recipe Selected { get { Saved s = _saved.SelectedItem as Saved; return s == null ? null : s.Recipe; } }

        void LoadSelected()
        {
            Recipe r = Selected;
            _loading = true;
            _name.Text = r == null ? "" : r.Name;
            foreach (KeyValuePair<string, Control> pair in _values)
            {
                string value = r == null ? RecipeFields.Default(pair.Key) : r.Get(pair.Key);
                if (value == "Not specified") value = RecipeFields.Default(pair.Key);
                SetValue(pair.Value, value);
            }
            _loading = false;
            _delete.Visible = r != null;
            UpdateSummary();
        }

        Recipe Build()
        {
            Recipe r = new Recipe();
            Recipe existing = Selected;
            if (existing != null) { r.Id = existing.Id; r.Favorite = existing.Favorite; r.Cover = existing.Cover; r.Category = existing.Category; }
            else r.Category = "Cinematic";
            r.Name = _name.Text.Trim();
            r.Kind = "Video";
            r.Source = DataSource.LOCAL; r.Demonstration = false;
            foreach (KeyValuePair<string, Control> pair in _values)
            { string v = ValueOf(pair.Value).Trim(); if (v != "") r.Values[pair.Key] = v; }
            return r;
        }

        void UpdateSummary()
        {
            int filled = 0;
            Recipe preview = Build();
            foreach (string key in Recipe.VideoParameterOrder)
            { string v = preview.Get(key); if (v != "Not specified" && v.Trim() != "") filled++; }
            _summary.Text = Strings.T("{0} of {1} movie settings filled in.", filled, Recipe.VideoParameterOrder.Length);
            _save.Enabled = _name.Text.Trim() != "";
            Invalidate();
        }

        void SaveSettings()
        {
            if (_name.Text.Trim() == "")
            { MessageBox.Show(FindForm(), Strings.T("Please enter a recipe name."), Strings.T("Name required")); return; }
            Recipe built = Build();
            Recipe existing = Selected;
            try
            {
                if (existing == null) _library.Add(built);
                else
                {
                    existing.Name = built.Name; existing.Kind = "Video"; existing.Values = built.Values;
                    _library.Save();
                }
            }
            catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Could not save"); return; }
            Reload();
            for (int i = 0; i < _saved.Items.Count; i++)
                if (Convert.ToString(_saved.Items[i]) == built.Name) { _saved.SelectedIndex = i; break; }
            if (LibraryChanged != null) LibraryChanged(this, EventArgs.Empty);
        }

        void DeleteSelected()
        {
            Recipe r = Selected;
            if (r == null) return;
            if (MessageBox.Show(FindForm(), Strings.T("Delete \"{0}\"?", r.Name), Strings.T("Delete"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _library.Recipes.Remove(r);
            _library.Save();
            Reload();
            if (LibraryChanged != null) LibraryChanged(this, EventArgs.Empty);
        }

        void CopyChecklist()
        {
            try { Clipboard.SetText(RecipeFields.Checklist(Build())); }
            catch (Exception) { return; }
            _copy.Text = "Copied"; _copy.Invalidate();
            System.Windows.Forms.Timer back = new System.Windows.Forms.Timer() { Interval = 1400 };
            back.Tick += delegate { _copy.Text = "Copy as a checklist"; _copy.Invalidate(); back.Stop(); back.Dispose(); };
            back.Start();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (!_ready) return;
            // Tout est repositionné à chaque mise en page : il faut donc y reporter
            // le défilement.
            int sy = AutoScrollPosition.Y;
            bool wide = Columns == 2;
            // En une colonne, la liste des fiches enregistrées passe sous le nom :
            // tout ce qui suit descend d'autant.
            int extra = wide ? 0 : 40;
            int perColumn = PerColumn, columnWidth = 420;
            for (int i = 0; i < _inputs.Count; i++)
            {
                int x = (i / perColumn) * columnWidth, row = i % perColumn;
                int top = 156 + extra + row * 40;
                _labels[i].SetBounds(x + 4, top + sy, 176, 27);
                _inputs[i].SetBounds(x + 186, top + sy + (_inputs[i] is ComboBox ? -1 : 0), 200, 27);
            }
            int fieldWidth = wide ? 260 : Math.Max(150, ClientSize.Width - 210);
            _name.SetBounds(150, 96 + sy, fieldWidth, 27);
            _saved.SetBounds(wide ? 600 : 150, (wide ? 95 : 135) + sy, wide ? 280 : fieldWidth, 28);
            int y = BottomRow + extra;
            _summary.SetBounds(4, y + sy, 500, 24);
            _save.SetBounds(4, y + 32 + sy, 230, 42);
            _copy.SetBounds(246, y + 32 + sy, 210, 42);
            _delete.SetBounds(470, y + 32 + sy, 110, 42);
            AutoScrollMinSize = new Size(0, y + 96);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int y = AutoScrollPosition.Y;
            Theme.TextAt(g, Strings.T("Movie settings"), 21, true, Theme.Text, new Rectangle(4, 4 + y, Width - 30, 30));
            Theme.Lines(g, Strings.T("Set these on the camera, in its movie menus. The X-T30 does not store movie settings in the C1-C7 banks, so nothing here is ever written to it."),
                12, Theme.Muted, new Rectangle(5, 36 + y, Math.Min(880, Width - 40), 40));
            bool wide = Columns == 2;
            Theme.TextAt(g, Strings.T("Name"), 13, false, Theme.Muted, new Rectangle(4, 96 + y, 140, 27));
            Theme.TextAt(g, Strings.T("Saved"), 13, false, Theme.Muted, new Rectangle(wide ? 456 : 4, (wide ? 96 : 136) + y, 140, 27));
            int rule = (wide ? 140 : 180) + y;
            using (Pen pen = new Pen(Color.FromArgb(238, 238, 237))) g.DrawLine(pen, 4, rule, Math.Min(880, Width - 30), rule);
        }
    }
}
