using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Xt30Probe.AppCamera;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    // Panneau de préparation des sept banques, tenu ouvert à côté de l'état réel
    // du boîtier : chaque changement de recette met à jour immédiatement le nom
    // que portera la banque et le résumé, sans ouvrir de fenêtre.
    //
    // Le fichier est produit ici ; l'écriture dans l'appareil reste faite par la
    // FUJIFILM Tether App, comme partout ailleurs dans l'application.
    public sealed class BankPlanPanel : Panel
    {
        // Entrée d'une liste déroulante : porte la recette et son libellé affiché.
        sealed class Choice
        {
            public readonly Recipe Recipe;
            readonly string _label;
            public Choice(Recipe recipe, string label) { Recipe = recipe; _label = label; }
            public override string ToString() { return _label; }
        }

        readonly RecipeLibrary _library;
        readonly ComboBox[] _picks = new ComboBox[CameraBankFile.Slots];
        readonly Label[] _previews = new Label[CameraBankFile.Slots];
        readonly Label[] _current = new Label[CameraBankFile.Slots];
        readonly Label _summary = new Label();
        readonly ActionButton _send = new ActionButton("Send all seven to the camera", true);
        readonly ActionButton _file = new ActionButton("Only create the file…", false);
        readonly ActionButton _reset = new ActionButton("Reset", false) { Quiet = true };
        bool _building;
        // Les contrôles sont créés un par un ; la mise en page ne doit pas s'exécuter
        // tant que les sept lignes ne sont pas toutes en place.
        bool _ready;

        // Recette prévue pour chaque banque ; null = banque laissée inchangée.
        public readonly Recipe[] Plan = new Recipe[CameraBankFile.Slots];
        public event EventHandler PlanChanged;

        public BankPlanPanel(RecipeLibrary library)
        {
            _library = library;
            BackColor = Color.White;
            DoubleBuffered = true;

            for (int i = 0; i < CameraBankFile.Slots; i++)
            {
                int y = RowTop(i);
                _current[i] = new Label() { Location = new Point(52, y + 2), Size = new Size(240, 18), Font = Theme.Font(11, false), ForeColor = Theme.Muted, BackColor = Color.Transparent };
                Controls.Add(_current[i]);

                ComboBox pick = new ComboBox()
                {
                    Location = new Point(52, y + 22),
                    Size = new Size(260, 28),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = Theme.Font(13, false),
                    FlatStyle = FlatStyle.Flat,
                    AccessibleName = "Recipe for bank C" + (i + 1)
                };
                pick.SelectedIndexChanged += delegate { if (!_building) { UpdatePlan(); } };
                _picks[i] = pick; Controls.Add(pick);

                _previews[i] = new Label() { Location = new Point(52, y + 52), Size = new Size(260, 18), Font = Theme.Font(11, false), ForeColor = Theme.Muted, BackColor = Color.Transparent };
                Controls.Add(_previews[i]);
            }

            _summary.Font = Theme.Font(12, false); _summary.BackColor = Color.Transparent;
            Controls.Add(_summary);
            Controls.AddRange(new Control[] { _send, _file, _reset });
            _send.Click += delegate { Apply(true); };
            _file.Click += delegate { Apply(false); };
            _reset.ForeColor = Theme.Muted;
            _reset.Click += delegate { Reload(); };

            _ready = true;
            Reload();
            PerformLayout();
        }

        static int RowTop(int index) { return 96 + index * 78; }

        // Recharge les listes depuis la bibliothèque : à appeler après une lecture
        // de l'appareil ou une modification des recettes.
        public void Reload()
        {
            _building = true;
            List<Choice> choices = BuildChoices();
            for (int i = 0; i < CameraBankFile.Slots; i++)
            {
                _picks[i].Items.Clear();
                foreach (Choice choice in choices) _picks[i].Items.Add(choice);
                _picks[i].SelectedIndex = 0;
                string now = i < _library.Slots.Count ? _library.Slots[i].Recipe.Name : "—";
                _current[i].Text = Strings.T("now: {0}", now);
            }
            PreselectFromCamera(choices);
            _building = false;
            UpdatePlan();
        }

        // Les recettes vidéo n'entrent jamais dans une banque : le boîtier n'y range
        // aucun réglage film. Les recettes locales viennent en tête, ce sont celles
        // que l'utilisateur a écrites lui-même.
        List<Choice> BuildChoices()
        {
            List<Choice> choices = new List<Choice>();
            choices.Add(new Choice(null, Strings.T("— leave this bank unchanged —")));
            List<Recipe> local = new List<Recipe>(), imported = new List<Recipe>();
            foreach (Recipe r in _library.Recipes)
            {
                if (r.IsVideo || r.IsFromCamera) continue;
                if (r.IsImported) imported.Add(r); else local.Add(r);
            }
            local.Sort(delegate(Recipe a, Recipe b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            imported.Sort(delegate(Recipe a, Recipe b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            foreach (Recipe r in local) choices.Add(new Choice(r, Strings.T("{0}   ·   yours", r.Name)));
            foreach (Recipe r in imported) choices.Add(new Choice(r, r.Name + "   ·   " + r.SourceSite));
            return choices;
        }

        // Si une banque du boîtier porte le nom d'une recette de la bibliothèque, on
        // la propose d'emblée : le plan par défaut reproduit ce qui est déjà en place.
        void PreselectFromCamera(List<Choice> choices)
        {
            if (!_library.SlotsAreFromCamera) return;
            for (int i = 0; i < CameraBankFile.Slots && i < _library.Slots.Count; i++)
            {
                Recipe match = _library.Slots[i].Recipe.MatchedLibraryRecipe;
                if (match == null) continue;
                for (int j = 0; j < _picks[i].Items.Count; j++)
                    if (((Choice)_picks[i].Items[j]).Recipe == match) { _picks[i].SelectedIndex = j; break; }
            }
        }

        Recipe Selected(int slot)
        {
            Choice choice = _picks[slot].SelectedItem as Choice;
            return choice == null ? null : choice.Recipe;
        }

        // Choisit une recette pour la première banque libre : permet d'envoyer une
        // recette depuis la page Recettes sans passer par les listes.
        public bool Assign(int slot, Recipe recipe)
        {
            if (slot < 0 || slot >= CameraBankFile.Slots) return false;
            for (int j = 0; j < _picks[slot].Items.Count; j++)
                if (((Choice)_picks[slot].Items[j]).Recipe == recipe) { _picks[slot].SelectedIndex = j; return true; }
            return false;
        }

        void UpdatePlan()
        {
            int planned = 0, tooLong = 0;
            for (int i = 0; i < CameraBankFile.Slots; i++)
            {
                Recipe r = Selected(i);
                Plan[i] = r;
                if (r == null) { _previews[i].Text = ""; continue; }
                planned++;
                string name = CameraBankFile.BuildBankName(r, r.Name);
                bool over = name.Length > CameraBankFile.NameMax;
                if (over) tooLong++;
                _previews[i].Text = "→ \"" + name + "\"  " + name.Length + "/" + CameraBankFile.NameMax;
                _previews[i].ForeColor = over ? Color.FromArgb(196, 74, 64) : Theme.Green;
            }
            _summary.Text = planned == 0
                ? Strings.T("No bank selected yet. Pick at least one recipe.")
                : Strings.T("{0} bank(s) will change; the other {1} and every other camera setting stay exactly as they are.", planned, CameraBankFile.Slots - planned)
                  + (tooLong > 0 ? "\n" + Strings.T("{0} name(s) are longer than {1} characters and will be shortened by the camera.", tooLong, CameraBankFile.NameMax) : "");
            _summary.ForeColor = tooLong > 0 ? Theme.Amber : Theme.Muted;
            _send.Enabled = planned > 0; _file.Enabled = planned > 0;
            Invalidate();
            if (PlanChanged != null) PlanChanged(this, EventArgs.Empty);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (!_ready) return;
            int width = Math.Max(300, ClientSize.Width - 32);
            for (int i = 0; i < CameraBankFile.Slots; i++)
            {
                _current[i].Width = width - 24;
                _picks[i].Width = width - 24;
                _previews[i].Width = width - 24;
            }
            int bottom = RowTop(CameraBankFile.Slots - 1) + 82;
            _summary.SetBounds(20, bottom, width, 42);
            _send.SetBounds(20, bottom + 52, Math.Min(268, width), 42);
            _file.SetBounds(20, bottom + 102, Math.Min(190, width - 90), 38);
            _reset.SetBounds(Math.Min(216, width - 84), bottom + 102, 84, 38);
        }

        public int PreferredHeight { get { return RowTop(CameraBankFile.Slots - 1) + 82 + 152; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Theme.TextAt(g, Strings.T("Load recipes into C1-C7"), 15, true, Theme.Text, new Rectangle(20, 18, ClientSize.Width - 40, 26));
            Theme.Lines(g, Strings.T("Pick a recipe per bank. One file updates all seven at once."), 11, Theme.Muted, new Rectangle(21, 44, ClientSize.Width - 42, 34));
            for (int i = 0; i < CameraBankFile.Slots; i++)
            {
                int y = RowTop(i);
                Theme.TextAt(g, "C" + (i + 1), 17, true, Plan[i] != null ? Theme.Green : Theme.Text, new Rectangle(20, y + 14, 34, 26));
                using (Pen pen = new Pen(Color.FromArgb(240, 240, 239)))
                    if (i > 0) g.DrawLine(pen, 20, y - 8, ClientSize.Width - 20, y - 8);
            }
        }

        // --- Production du fichier et envoi ------------------------------------

        IWin32Window Owner { get { return FindForm() ?? (IWin32Window)this; } }

        // Trouve le fichier de réglages de référence, en le lisant dans le boîtier
        // si aucune lecture n'a encore eu lieu. Lecture seule de bout en bout.
        string SourceFile()
        {
            string source = CameraBankFile.FindLatestSettingsFile(AppDomain.CurrentDomain.BaseDirectory);
            if (source != null) return source;
            if (!CameraBanksReader.Available)
            {
                MessageBox.Show(Owner, "No settings file has been read from the camera yet, and the reading tool is missing.", Strings.T("Read the camera first"));
                return null;
            }
            if (MessageBox.Show(Owner, "Your camera has not been read yet. Read it now?\n\nThis only copies the settings file from the camera; nothing is written.",
                    "Read the camera", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return null;
            CameraBanksReader.Result read = ProgressForm.Run(Owner,
                Strings.T("Reading your camera"),
                Strings.T("Copying the settings file from the X-T30…"),
                delegate(Action<string> report)
                {
                    CameraBanksReader.Result r = CameraBanksReader.Read();
                    report(Strings.T("Decoding the seven banks…"));
                    return r;
                });
            if (!read.Success)
            {
                MessageBox.Show(Owner, read.Error + "\n\nMake sure the camera is on, connected, and set to USB RAW CONV./BACKUP RESTORE.", Strings.T("The camera was not read"));
                return null;
            }
            _library.ReloadCameraBanks();
            Reload();
            return read.SettingsFile;
        }

        void Apply(bool send)
        {
            string source = SourceFile();
            if (source == null) return;

            Dictionary<int, Recipe> assignments = new Dictionary<int, Recipe>();
            Dictionary<int, string> names = new Dictionary<int, string>();
            for (int i = 0; i < CameraBankFile.Slots; i++)
            {
                Recipe r = Selected(i);
                if (r == null) continue;
                assignments[i] = r;
                names[i] = CameraBankFile.BuildBankName(r, r.Name);
            }
            if (assignments.Count == 0) return;

            byte[] blob;
            try { blob = File.ReadAllBytes(source); }
            catch (Exception ex) { MessageBox.Show(Owner, ex.Message, "Could not read the settings file"); return; }

            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "generated",
                "xt30-banks-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".dat");
            CameraBankFile.PatchResult result = CameraBankFile.PrepareMany(blob, assignments, names, output);
            if (!result.Success) { MessageBox.Show(Owner, result.Error, Strings.T("The file was not created")); return; }

            System.Text.StringBuilder message = new System.Text.StringBuilder();
            foreach (KeyValuePair<int, Recipe> pair in assignments)
                message.AppendLine("  C" + (pair.Key + 1) + "   " + names[pair.Key]);
            if (result.Skipped.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Set these by hand on the camera — the settings file cannot hold them:");
                foreach (string skipped in result.Skipped) message.AppendLine("  · " + skipped);
            }

            if (!send)
            {
                MessageBox.Show(Owner, "File created:\n" + result.OutputPath + "\n\n" + message
                    + "\nLoad it with Fujifilm's Tether App (BACKUP RESTORE) when you are ready.",
                    Strings.T("File ready"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(Owner, "Send this to the camera now?\n\n" + message
                    + "\nThe Fujifilm Tether App performs the write; this application never does.\nKeep your original settings file — it restores the camera exactly as it was.",
                    Strings.T("Send to the camera"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            CameraSend.Send(Owner, result.OutputPath, _library);
            Reload();
        }
    }

    // Même panneau dans une fenêtre, pour les fenêtres étroites où la colonne
    // latérale ne tient pas.
    public sealed class BankPlanForm : Form
    {
        public readonly BankPlanPanel Panel;
        public BankPlanForm(RecipeLibrary library)
        {
            Panel = new BankPlanPanel(library) { Dock = DockStyle.Fill };
            Text = Strings.T("Load recipes into C1-C7");
            BackColor = Color.White; Font = Theme.Font(14, false);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, Math.Min(Screen.PrimaryScreen.WorkingArea.Height - 80, Panel.PreferredHeight + 20));
            MinimumSize = new Size(380, 560);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            Controls.Add(Panel);
        }
    }
}
