using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Xt30Probe.AppModel;
using Xt30Probe.AppCamera;

namespace Xt30Probe.Presentation
{
    public sealed class RecipesPage : Panel
    {
        public readonly SearchBar Search=new SearchBar();
        public readonly RecipeGrid Grid=new RecipeGrid();
        public readonly ActionButton NewRecipe=new ActionButton("New Recipe",true){IconName="Plus"};
        public readonly ComboBox SimulationFilter=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDownList};
        public readonly ActionButton ShowMore=new ActionButton("Show more recipes",false){Visible=false};
        readonly RecipeLibrary _library;
        readonly List<ActionButton> _filters=new List<ActionButton>();
        string _filter="All";
        public event Action<Recipe> OpenRecipe;
        public event Action<Recipe> FavoriteRequested;
        public RecipesPage(RecipeLibrary library)
        {
            _library=library;BackColor=Theme.Background;AutoScroll=true;
            Controls.Add(Search);Controls.Add(NewRecipe);Controls.Add(Grid);Controls.Add(SimulationFilter);Grid.BackColor=Theme.Background;
            SimulationFilter.Font=Theme.Font(13,false);SimulationFilter.AccessibleName="Film simulation filter";
            ReloadSimulations();
            SimulationFilter.SelectedIndexChanged+=delegate{RefreshRecipes();};
            string[] filters={"All","Compatible","Favorites","Imported","Local","Photo","Video","B&W","Color","Portrait","Street","Night","Vintage","Cinematic"};
            foreach(string filter in filters){ActionButton button=new ActionButton(filter,false){Quiet=true};button.Text=filter;_filters.Add(button);Controls.Add(button);button.Click+=delegate{SetFilter(button.Text);};}
            Controls.Add(ShowMore);ShowMore.Click+=delegate{Grid.ShowMore();PerformLayout();Invalidate();};
            Grid.ShownCountChanged+=delegate{ShowMore.Visible=Grid.HasMore;Invalidate();};
            Grid.OpenRecipe+=delegate(Recipe r){if(OpenRecipe!=null)OpenRecipe(r);};Grid.FavoriteRequested+=delegate(Recipe r){if(FavoriteRequested!=null)FavoriteRequested(r);RefreshRecipes();};
            Search.SearchChanged+=delegate{RefreshRecipes();};SetFilter("All");
        }
        public void ReloadSimulations()
        {
            SimulationFilter.Items.Clear();SimulationFilter.Items.Add(Strings.T("All simulations"));
            foreach(string sim in _library.Simulations())SimulationFilter.Items.Add(sim);
            SimulationFilter.SelectedIndex=0;
        }
        public void SetFilter(string filter){_filter=filter;foreach(ActionButton b in _filters){b.Quiet=b.Text!=filter;b.ForeColor=b.Text==filter?Theme.Green:Theme.Muted;b.Invalidate();}RefreshRecipes();}
        // Le premier élément est traduit à l'écran mais reste « toutes » côté requête.
        public void RefreshRecipes(){string sim=SimulationFilter.SelectedIndex<=0?"All simulations":Convert.ToString(SimulationFilter.SelectedItem);Grid.SetRecipes(_library.Query(Search.Input.Text,_filter,sim));PerformLayout();Invalidate();}
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(Grid==null||SimulationFilter==null)return;int width=ClientSize.Width-56-(VerticalScroll.Visible?17:0);Point scroll=AutoScrollPosition;
            Search.SetBounds(28+scroll.X,3+scroll.Y,Math.Min(340,width-388),42);
            SimulationFilter.SetBounds(Search.Right+10,10+scroll.Y,180,30);
            NewRecipe.SetBounds(width-138+scroll.X,5+scroll.Y,166,38);
            // Largeur mesurée sur le libellé traduit : « Kompatibel » ou « Compatibles »
            // n'ont pas la largeur de « Compatible ».
            int x=28,filterY=64;
            foreach(ActionButton b in _filters)
            {
                int w=Math.Max(80,TextRenderer.MeasureText(Strings.T(b.Text),Theme.Font(13,false)).Width+26);
                if(x+w>width+28){x=28;filterY+=42;}
                b.SetBounds(x+scroll.X,filterY+scroll.Y,w,36);x+=w+5;
            }
            Grid.SetBounds(28+scroll.X,filterY+84+scroll.Y,Math.Max(280,width),Math.Max(250,Grid.Height));
            int below=filterY+84+Grid.Height+14;
            ShowMore.SetBounds(28+scroll.X,below+scroll.Y,240,42);
            AutoScrollMinSize=new Size(0,below+(ShowMore.Visible?70:26));
        }
        // La legende nomme les catalogues reellement charges : plusieurs sites
        // coexistent desormais, et la liste ne doit jamais en inventer un.
        string Caption()
        {
            int local=0,video=0;List<string> sites=new List<string>();
            foreach(Recipe r in _library.Recipes)
            {
                if(r.IsVideo)video++;
                if(!r.IsImported){local++;continue;}
                if(r.SourceSite!=""&&!sites.Contains(r.SourceSite))sites.Add(r.SourceSite);
            }
            sites.Sort(StringComparer.OrdinalIgnoreCase);
            string caption=(Grid.HasMore
                ?Strings.T("{0} of {1} shown",Grid.ItemCount,Grid.TotalCount)
                :Strings.T("{0} shown",Grid.ItemCount))
                +"   ·   "+Strings.T("{0} LOCAL (yours, editable)",local);
            if(video>0)caption+="   ·   "+Strings.T("{0} VIDEO",video);
            if(sites.Count>0)caption+="   ·   "+Strings.T("{0} imported and read-only from {1}",_library.ImportedCount,string.Join(", ",sites.ToArray()));
            return caption;
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.TextAt(e.Graphics,Caption(),12,false,Theme.Muted,new Rectangle(28,Grid.Top-33,Width-56,25));}
    }
    public sealed class RecipeDetailForm : Form
    {
        readonly Recipe _recipe;
        readonly PictureHero _hero;
        readonly Panel _content=new Panel(){AutoScroll=true,BackColor=Color.White};
        readonly CompatibilityBadge _badge=new CompatibilityBadge();
        readonly ActionButton _edit=new ActionButton("Edit local recipe",false);
        readonly ActionButton _article=new ActionButton("View article",false);
        readonly ActionButton _camera=new ActionButton("Write to camera",true){Enabled=false};
        readonly ToolTip _tips=new ToolTip();
        readonly List<string> _rows=new List<string>();
        readonly ActionButton _compare=new ActionButton("Compare with camera",false);
        int _noteTop;
        public event Action EditRequested;
        public RecipeDetailForm(Recipe recipe):this(recipe,null){}
        public RecipeDetailForm(Recipe recipe,RecipeLibrary library)
        {
            _recipe=recipe;Text=recipe.Name+" · "+Strings.T(recipe.IsFromCamera?"Camera bank":recipe.IsVideo?"Video recipe":recipe.IsImported?"Imported recipe":"Local recipe");BackColor=Color.White;Font=Theme.Font(14,false);StartPosition=FormStartPosition.CenterParent;
            // lignes affichées : paramètres du mode concerné + réglages étendus présents
            _rows.AddRange(recipe.Parameters);
            foreach(string key in Recipe.AdditionalParameters)if(recipe.Values.ContainsKey(key))_rows.Add(key);
            _noteTop=141+_rows.Count*32+12;
            // Hauteur ajustée au contenu : avec 18 paramètres, une fenêtre fixe reléguait
            // la rangée de boutons hors de la zone visible.
            int needed=_noteTop+170;
            int maxHeight=Screen.PrimaryScreen.WorkingArea.Height-60;
            ClientSize=new Size(970,Math.Max(660,Math.Min(maxHeight,needed)));MinimumSize=new Size(780,620);
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch(Exception){}
            _hero=new PictureHero(recipe.Cover,recipe.Gallery);Controls.Add(_hero);Controls.Add(_content);_content.Controls.Add(_badge);_content.Controls.Add(_edit);_content.Controls.Add(_article);_content.Controls.Add(_camera);_content.Controls.Add(_compare);_badge.Recipe=recipe;
            // Guide de saisie manuelle : proposé seulement quand de vraies banques ont
            // été lues, et jamais pour une banque comparée à elle-même.
            // Une recette vidéo ne se compare à aucune banque : le boîtier n'y range pas ses réglages film.
            _compare.Visible=library!=null&&library.SlotsAreFromCamera&&!recipe.IsFromCamera&&!recipe.IsVideo;
            _camera.Visible=!recipe.IsVideo;
            _compare.Click+=delegate{using(CameraCompareForm f=new CameraCompareForm(_recipe,library))f.ShowDialog(this);};
            _tips.SetToolTip(_camera,CameraWritePolicy.Explanation);_edit.Click+=delegate{if(EditRequested!=null)EditRequested();};
            _edit.Visible=!recipe.IsImported&&!recipe.IsFromCamera;
            _article.Visible=recipe.IsImported&&recipe.ArticleUrl!="";
            _article.Click+=delegate{try{System.Diagnostics.Process.Start(_recipe.ArticleUrl);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Open article");}};
            _content.Paint+=PaintContent;_content.AutoScrollMinSize=new Size(0,_noteTop+206);Layout+=delegate{LayoutContent();};LayoutContent();
        }
        void LayoutContent()
        {
            int photo=Math.Max(275,ClientSize.Width*39/100);_hero.SetBounds(0,0,photo,ClientSize.Height);_content.SetBounds(photo,0,ClientSize.Width-photo,ClientSize.Height);
            int sy=_content.AutoScrollPosition.Y;
            _badge.SetBounds(30,85+sy,268,32);
            int buttons=_noteTop+108;
            // Les trois actions tiennent sur une seule ligne : une seconde rangée
            // sortait de la zone visible sur les recettes à nombreux paramètres.
            _edit.SetBounds(30,buttons+sy,150,36);_article.SetBounds(30,buttons+sy,150,36);
            _compare.SetBounds(190,buttons+sy,188,36);_camera.SetBounds(386,buttons+sy,160,36);
        }
        void PaintContent(object sender,PaintEventArgs e)
        {
            Graphics g=e.Graphics;int w=_content.ClientSize.Width;int y=_content.AutoScrollPosition.Y;
            Theme.TextAt(g,_recipe.Name,25,true,Theme.Text,new Rectangle(30,25+y,w-55,38));
            string origin=_recipe.IsFromCamera
                ?Strings.T("SOURCE: CAMERA  ·  READ FROM YOUR X-T30")
                :_recipe.IsVideo
                ?Strings.T("SOURCE: LOCAL  ·  VIDEO RECIPE  ·  MOVIE MODE")
                :_recipe.IsImported
                // Le nom du site vient de la recette : plusieurs catalogues coexistent.
                ?Strings.T("SOURCE")+": "+_recipe.SourceSite+(_recipe.Author!=""?"  ·  "+_recipe.Author.ToUpperInvariant():"")+(_recipe.PublishedAt.Length>=10?"  ·  "+_recipe.PublishedAt.Substring(0,10):"")
                :Strings.T("SOURCE")+": "+Strings.T("LOCAL")+(_recipe.Demonstration?"  ·  "+Strings.T("DEMONSTRATION RECIPE"):"  ·  "+Strings.T("YOUR LIBRARY"));
            Theme.TextAt(g,origin,10,true,Theme.Muted,new Rectangle(31,62+y,w-56,18));
            int top=141;
            foreach(string key in _rows)
            {Theme.TextAt(g,Strings.T(key),13,false,Theme.Muted,new Rectangle(31,top+y,206,25));Theme.TextAt(g,Strings.T(_recipe.Get(key)),13,true,Theme.Text,new Rectangle(246,top+y,w-271,25));using(Pen p=new Pen(Color.FromArgb(241,241,241)))g.DrawLine(p,31,top+29+y,w-28,top+29+y);top+=32;}
            List<string> issues=_recipe.CompatibilityIssues();
            string note;
            Color noteColor;
            if(_recipe.IsImported&&_recipe.CompatStatus=="XT30_INCOMPATIBLE"){note=_recipe.CompatReason;noteColor=Color.FromArgb(196,74,64);}
            else if(issues.Count==0)
            {
                note=Strings.T(_recipe.IsVideo
                    ?"These settings all exist on the X-T30 in movie mode. Set them on the camera; nothing is written from here."
                    :"These parameters are compatible with the X-T30 feature set. Camera transfer remains disabled.");
                noteColor=Theme.Muted;
            }
            else{note=string.Join("\n",issues.ToArray());noteColor=Theme.Amber;}
            Theme.Lines(g,note,12,noteColor,new Rectangle(31,_noteTop+y,w-60,72));
            string footer=Strings.T(_recipe.IsFromCamera
                ?"Read from your camera's settings file. Only what that file stores is shown; ISO, WB shift and exposure are not in it and stay unspecified."
                :_recipe.IsVideo
                ?"Movie recipe. The X-T30 keeps its movie image settings in its own menus, not in the C1-C7 banks and not in the settings file, so this one is set by hand on the camera."
                :_recipe.IsImported
                ?"Imported from a public recipe article. Values are shown exactly as published; missing values stay unspecified."
                :(_recipe.Demonstration?"Example values for this interface — not an authenticated recipe.":"Stored on this computer only."));
            Theme.Lines(g,footer,11,Theme.Muted,new Rectangle(31,_noteTop+78+y,w-55,28));
        }
        protected override void Dispose(bool disposing){if(disposing)_tips.Dispose();base.Dispose(disposing);}
    }
    // Envoi d'un fichier de réglages vers le boîtier. L'écriture est faite par la
    // FUJIFILM Tether App ; cette application ne parle jamais à l'appareil.
    public static class CameraSend
    {
        public static void Send(IWin32Window owner, string datFile) { Send(owner, datFile, null); }
        public static void Send(IWin32Window owner, string datFile, RecipeLibrary library)
        {
            if (!CameraRestore.Available)
            {
                MessageBox.Show(owner, "The restore helper is missing. Load the file yourself with Fujifilm's Tether App:\n\n"
                    + datFile + "\n\nCamera menu → Restauration des paramètres de l'appareil.",
                    "Load it manually", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // Fenêtre d'attente : l'envoi passe par la Tether App et prend une
            // vingtaine de secondes, pendant lesquelles l'utilisateur doit voir
            // ce qui se passe plutôt qu'une fenêtre figée.
            bool refreshed = false;
            CameraRestore.Result result = ProgressForm.Run(owner,
                Strings.T("Sending to your camera"),
                Strings.T("Opening the Fujifilm Tether App…"),
                delegate(Action<string> report)
                {
                    CameraRestore.Result r = CameraRestore.Run(datFile, false, 90);
                    if (r.Success)
                    {
                        report(Strings.T("Re-reading the banks the camera now holds…"));
                        refreshed = CameraRestore.RefreshDecodedBanks(datFile);
                    }
                    return r;
                });

            if (result.Success)
            {
                // Les banques affichées reflètent désormais ce qui vient d'être envoyé.
                if (refreshed && library != null) library.ReloadCameraBanks();
                MessageBox.Show(owner,
                    "Sent to the camera.\n\n" + result.Output.Trim()
                    + (refreshed ? "\n\nCamera Slots now shows the banks you just sent." : "")
                    + "\n\nCheck the banks on the camera screen. If anything looks wrong, restore your original settings file.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show(owner,
                    "The camera was not updated.\n\n" + result.Error
                    + (result.Output.Trim() == "" ? "" : "\n\n" + result.Output.Trim())
                    + "\n\nNothing was changed. Make sure the camera is awake, in RAW CONV./BACKUP RESTORE mode, "
                    + "and that the Fujifilm Tether App is open and in the foreground.",
                    "Not sent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Compare une recette de la bibliothèque avec une banque RÉELLEMENT lue dans le
    // boîtier et liste les réglages à changer à la main. Aucune écriture : c'est un
    // guide de saisie, pas un transfert.
    public sealed class CameraCompareForm : Form
    {
        readonly Recipe _recipe;
        readonly RecipeLibrary _library;
        readonly ComboBox _bank = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ListView _list = new ListView();
        public CameraCompareForm(Recipe recipe, RecipeLibrary library)
        {
            _recipe = recipe; _library = library;
            Text = "Apply \"" + recipe.Name + "\" by hand";
            BackColor = Color.White; Font = Theme.Font(14, false);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(770, 600); MinimumSize = new Size(700, 560);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            Label title = new Label() { Text = "What to change in the camera menu", Font = Theme.Font(19, true), Location = new Point(24, 20), Size = new Size(700, 32) };
            Label sub = new Label() { Text = "Nothing is written to the camera. This compares the recipe with the bank actually read from your X-T30.", Font = Theme.Font(12, false), ForeColor = Theme.Muted, Location = new Point(25, 54), Size = new Size(700, 40) };
            Controls.Add(title); Controls.Add(sub);
            Label pick = new Label() { Text = "Camera bank:", Location = new Point(25, 100), Size = new Size(100, 26), ForeColor = Theme.Muted };
            Controls.Add(pick);
            _bank.Location = new Point(128, 96); _bank.Size = new Size(300, 28); _bank.Font = Theme.Font(13, false);
            foreach (CustomSlot slot in library.Slots) _bank.Items.Add("C" + slot.Number + " — " + slot.Recipe.Name);
            if (_bank.Items.Count > 0) _bank.SelectedIndex = 0;
            _bank.SelectedIndexChanged += delegate { Compare(); };
            Controls.Add(_bank);
            _list.View = View.Details; _list.FullRowSelect = true; _list.Location = new Point(25, 140);
            _list.Size = new Size(710, 440); _list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _list.Font = Theme.Font(12, false); _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Columns.Add("Setting", 210); _list.Columns.Add("In the camera now", 170);
            _list.Columns.Add("Recipe asks for", 170); _list.Columns.Add("Action", 140);
            Controls.Add(_list);
            _list.Height = 380;
            ActionButton prepare = new ActionButton("Create a camera settings file…", true)
            { Location = new Point(25, 536), Size = new Size(300, 38), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            Controls.Add(prepare);
            prepare.Click += delegate { PrepareFile(); };
            Label hint = new Label()
            {
                Text = "Writes a .dat file on this computer. You then load it with Fujifilm's own\nTether App (BACKUP RESTORE) — this application never writes to the camera.",
                Font = Theme.Font(11, false), ForeColor = Theme.Muted,
                Location = new Point(336, 536), Size = new Size(400, 42),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            Controls.Add(hint);
            Compare();
        }

        void PrepareFile()
        {
            string source = CameraBankFile.FindLatestSettingsFile(AppDomain.CurrentDomain.BaseDirectory);
            if (source == null)
            { MessageBox.Show(this, "No camera settings file has been read yet, so there is nothing to modify.", "Read the camera first"); return; }
            int slot = _bank.SelectedIndex;
            if (slot < 0) return;
            // Le décalage WB ne tient pas dans la banque : il part dans son nom.
            string name = CameraBankFile.BuildBankName(_recipe, _recipe.Name);
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Fujifilm settings backup|*.dat";
                dialog.FileName = "xt30-settings-C" + (slot + 1) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".dat";
                dialog.InitialDirectory = Path.GetDirectoryName(source);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                byte[] blob;
                try { blob = File.ReadAllBytes(source); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not read the source file"); return; }
                CameraBankFile.PatchResult result = CameraBankFile.Prepare(blob, slot, _recipe, name, dialog.FileName);
                if (!result.Success) { MessageBox.Show(this, result.Error, "The file was not created"); return; }
                System.Text.StringBuilder message = new System.Text.StringBuilder();
                message.AppendLine("File created:");
                message.AppendLine(result.OutputPath);
                message.AppendLine();
                message.AppendLine("Bank C" + (slot + 1) + " now holds \"" + name + "\".");
                message.AppendLine("Only that bank changed; the other six and every other camera setting are untouched.");
                message.AppendLine();
                message.AppendLine("Written: " + string.Join(", ", result.Written.ToArray()));
                if (result.Skipped.Count > 0)
                {
                    message.AppendLine();
                    message.AppendLine("Not written (set these by hand if you want them):");
                    foreach (string s in result.Skipped) message.AppendLine("  · " + s);
                }
                message.AppendLine();
                message.AppendLine("Keep your original settings file: it restores the camera exactly as it was.");
                message.AppendLine();
                message.AppendLine("Send it to the camera now?");
                message.AppendLine("The Fujifilm Tether App performs the write; this application never does.");
                if (MessageBox.Show(this, message.ToString(), "Camera settings file ready",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    CameraSend.Send(this, result.OutputPath);
            }
        }
        static string Norm(string v)
        {
            string s = (v ?? "").Trim().ToUpperInvariant().Replace(" ", "");
            if (s.StartsWith("+")) s = s.Substring(1);
            if (s.StartsWith("DR") && s.Length > 2) s = s.Substring(2);
            if (s.EndsWith("K") && s.Length > 1) s = s.Substring(0, s.Length - 1);
            return s;
        }
        void Compare()
        {
            _list.Items.Clear();
            if (_bank.SelectedIndex < 0 || _bank.SelectedIndex >= _library.Slots.Count) return;
            Recipe bank = _library.Slots[_bank.SelectedIndex].Recipe;
            List<string> keys = new List<string>(Recipe.ParameterOrder);
            foreach (string extra in Recipe.AdditionalParameters) if (_recipe.Values.ContainsKey(extra)) keys.Add(extra);
            int changes = 0;
            foreach (string key in keys)
            {
                string want = _recipe.Get(key), have = bank.Get(key);
                bool wantKnown = want != "Not specified" && want.Trim() != "";
                bool haveKnown = have != "Not specified" && have.Trim() != "";
                // Les réglages étendus n'existent pas sur le X-T30 : ne jamais demander
                // de les saisir, ce serait envoyer l'utilisateur chercher un menu absent.
                bool unavailable = Array.IndexOf(Recipe.AdditionalParameters, key) >= 0;
                string action; Color color;
                if (unavailable) { action = "not on the X-T30 — skip"; color = Theme.Muted; }
                else if (!wantKnown) { action = "recipe does not say"; color = Theme.Muted; }
                else if (key == "WB Shift R" || key == "WB Shift B") { action = "carried in the bank name"; color = Theme.Amber; changes++; }
                else if (!haveKnown) { action = "set by hand (not stored in the file)"; color = Theme.Amber; changes++; }
                else if (Norm(want) == Norm(have)) { action = "already correct"; color = Theme.Green; }
                else { action = "CHANGE"; color = Color.FromArgb(196, 74, 64); changes++; }
                ListViewItem item = new ListViewItem(key);
                item.SubItems.Add(haveKnown ? have : "—");
                item.SubItems.Add(wantKnown ? want : "—");
                item.SubItems.Add(action);
                item.ForeColor = color;
                _list.Items.Add(item);
            }
            List<string> issues = _recipe.CompatibilityIssues();
            foreach (string issue in issues)
            {
                ListViewItem warn = new ListViewItem("X-T30 limitation");
                warn.SubItems.Add("—"); warn.SubItems.Add("—"); warn.SubItems.Add(issue);
                warn.ForeColor = Theme.Amber; _list.Items.Add(warn);
            }
            Text = changes == 0 ? "Nothing to change" : changes + " setting(s) to change by hand";
        }
    }

    // Visionneuse : toutes les photos publiées avec la recette, pas seulement la
    // couverture. Bande de vignettes en bas, flèches sur les côtés, molette et
    // flèches du clavier.
    public sealed class PictureHero : Control
    {
        public string Cover;
        readonly List<string> _images=new List<string>();
        int _index;
        const int StripHeight=76, Thumb=64, Gap=8, Arrow=38;

        public PictureHero(string cover):this(cover,null){}
        public PictureHero(string cover,IList<string> images)
        {
            Cover=cover;DoubleBuffered=true;TabStop=true;
            if(images!=null)foreach(string path in images)if(path!=null&&path!="")_images.Add(path);
            if(_images.Count==0&&cover!=null)_images.Add(cover);
            Cursor=_images.Count>1?Cursors.Hand:Cursors.Default;
            AccessibleName=_images.Count>1?_images.Count+" photos for this recipe":"Recipe photo";
        }

        public int Count{get{return _images.Count;}}
        string Current{get{return _images.Count==0?Cover:_images[Math.Max(0,Math.Min(_images.Count-1,_index))];}}
        bool HasStrip{get{return _images.Count>1&&Height>StripHeight+140;}}

        public void Show(int index)
        {
            if(_images.Count==0)return;
            _index=((index%_images.Count)+_images.Count)%_images.Count;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            if(_images.Count<2)return;
            if(HasStrip&&e.Y>=Height-StripHeight)
            {
                int i=(e.X-Gap)/(Thumb+Gap);
                if(i>=0&&i<_images.Count)Show(i);
                return;
            }
            // Moitié gauche : photo précédente ; moitié droite : suivante.
            Show(_index+(e.X<Width/2?-1:1));
        }
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(_images.Count>1)Show(_index+(e.Delta<0?1:-1));}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(_images.Count>1&&(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right)){Show(_index+(e.KeyCode==Keys.Left?-1:1));e.Handled=true;}
            base.OnKeyDown(e);
        }
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||base.IsInputKey(key);}

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool strip=HasStrip;
            Rectangle main=new Rectangle(0,0,Width,strip?Height-StripHeight:Height);
            Assets.Cover(g,Current,main);
            if(_images.Count<2)return;

            // Compteur et flèches, posés sur la photo.
            string counter=(_index+1)+" / "+_images.Count;
            Rectangle pill=new Rectangle(Width-84,16,68,26);
            Theme.Round(g,pill,Color.FromArgb(190,0,0,0),Color.FromArgb(190,0,0,0),13);
            using(SolidBrush b=new SolidBrush(Color.White))
            using(StringFormat f=new StringFormat(){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                g.DrawString(counter,Theme.Font(12,true),b,pill,f);
            DrawArrow(g,new Rectangle(12,main.Height/2-Arrow/2,Arrow,Arrow),true);
            DrawArrow(g,new Rectangle(Width-Arrow-12,main.Height/2-Arrow/2,Arrow,Arrow),false);

            if(!strip)return;
            using(SolidBrush b=new SolidBrush(Color.FromArgb(24,24,24)))g.FillRectangle(b,0,Height-StripHeight,Width,StripHeight);
            for(int i=0;i<_images.Count;i++)
            {
                int x=Gap+i*(Thumb+Gap);
                if(x+Thumb>Width)break;
                Rectangle box=new Rectangle(x,Height-StripHeight+6,Thumb,Thumb);
                using(var path=Theme.Rounded(box,5)){var state=g.Save();g.SetClip(path);Assets.Cover(g,_images[i],box);g.Restore(state);}
                if(i==_index)using(Pen p=new Pen(Theme.Green,2))g.DrawRectangle(p,box.X+1,box.Y+1,box.Width-2,box.Height-2);
            }
        }

        static void DrawArrow(Graphics g,Rectangle box,bool left)
        {
            Theme.Round(g,box,Color.FromArgb(150,0,0,0),Color.FromArgb(150,0,0,0),Arrow/2);
            int cx=box.X+box.Width/2+(left?2:-2),cy=box.Y+box.Height/2;
            using(Pen p=new Pen(Color.White,2f))
            {
                p.StartCap=System.Drawing.Drawing2D.LineCap.Round;p.EndCap=System.Drawing.Drawing2D.LineCap.Round;
                int d=left?1:-1;
                g.DrawLine(p,cx+d*5,cy-7,cx-d*3,cy);
                g.DrawLine(p,cx-d*3,cy,cx+d*5,cy+7);
            }
        }
    }
    public sealed class RecipeEditorForm : Form
    {
        readonly Dictionary<string,Control> _values=new Dictionary<string,Control>();
        readonly TextBox _name=new TextBox();
        readonly ComboBox _category=new ComboBox();
        readonly ComboBox _kind=new ComboBox();
        readonly Panel _fields=new Panel(){AutoScroll=true};
        readonly Label _summary=new Label();
        readonly Label _bankName=new Label();
        readonly Recipe _original;
        // Valeurs déjà saisies, conservées quand on bascule photo <-> vidéo pour ne
        // pas perdre ce que l'utilisateur a tapé sur les réglages communs.
        readonly Dictionary<string,string> _carry=new Dictionary<string,string>();
        bool _video;
        string _cover;
        public Recipe Result;

        // Chaque paramètre propose les valeurs réelles du X-T30 : impossible de saisir
        // une valeur que le boîtier ne comprendrait pas, ni de faire une faute de frappe.
        static readonly string[] MovieModes={"4K 30P","4K 25P","4K 24P","FHD 60P","FHD 50P","FHD 30P","FHD 25P","FHD 24P","FHD 120P (high speed)"};
        static readonly string[] LogModes={"Off","F-Log"};
        static readonly string[] MovieDynamicRanges={"DR100","DR200","DR400"};
        static string[] Choices(string key,bool video)
        {
            switch(key)
            {
                case "Movie Mode": return MovieModes;
                case "F-Log": return LogModes;
                case "Film Simulation": return CameraBankFile.FilmSimulations;
                // Le mode film n'offre pas la priorité plage dynamique : pas de DR-P ici.
                case "Dynamic Range": return video?MovieDynamicRanges:CameraBankFile.DynamicRanges;
                case "Dynamic Range Priority": return CameraBankFile.DrPriorities;
                case "White Balance": return CameraBankFile.WhiteBalances();
                case "Grain Effect": return CameraBankFile.GrainEffects;
                case "Color Chrome Effect": return CameraBankFile.ChromeEffects;
                case "WB Shift R": case "WB Shift B": return CameraBankFile.Scale(-9,9);
                case "Highlight": case "Shadow": case "Color": case "Sharpness":
                case "Noise Reduction": case "Monochromatic Color": return CameraBankFile.Scale(-4,4);
                default: return null;   // ISO : texte libre
            }
        }

        public RecipeEditorForm(Recipe original)
        {
            _original=original;_cover=original==null?"pacific":original.Cover;
            Text=Strings.T(original==null?"New recipe":"Edit recipe");
            ClientSize=new Size(820,660);MinimumSize=new Size(780,600);StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;Font=Theme.Font(14,false);
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch(Exception){}

            Label heading=new Label(){Text=Strings.T(original==null?"Create a recipe":"Edit recipe"),Font=Theme.Font(21,true),ForeColor=Theme.Text,Location=new Point(26,18),Size=new Size(600,32)};
            Label sub=new Label(){Text=Strings.T("Only values your X-T30 actually accepts are offered. Stored on this computer; sending is a separate step."),
                Font=Theme.Font(12,false),ForeColor=Theme.Muted,Location=new Point(27,50),Size=new Size(700,22)};
            Controls.Add(heading);Controls.Add(sub);

            _fields.SetBounds(26,84,768,470);_fields.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(_fields);

            _name.SetBounds(150,3,220,27);_name.Text=original==null?"":original.Name;_name.MaxLength=80;
            _name.TextChanged+=delegate{UpdateSummary();};
            _category.SetBounds(150,40,220,27);_category.DropDownStyle=ComboBoxStyle.DropDownList;
            _category.Items.AddRange(new object[]{"Portrait","Street","Night","Vintage","Cinematic","Travel"});
            // La valeur stockée reste anglaise (les filtres s'en servent) ; seul le
            // libellé affiché est traduit.
            Ui.TranslateItems(_category);
            _category.SelectedItem=original==null?"Vintage":original.Category;if(_category.SelectedIndex<0)_category.SelectedIndex=0;
            // Photo ou vidéo : le jeu de réglages change entièrement d'un mode à l'autre.
            _kind.SetBounds(540,3,220,27);_kind.DropDownStyle=ComboBoxStyle.DropDownList;
            _kind.Items.AddRange(new object[]{Strings.T("Photo — camera bank C1-C7"),Strings.T("Video — movie mode")});
            _kind.SelectedIndex=original!=null&&original.IsVideo?1:0;
            _kind.SelectedIndexChanged+=delegate{if(_video==IsVideoSelected())return;Carry();_video=IsVideoSelected();BuildFields();UpdateSummary();};
            if(original!=null)foreach(KeyValuePair<string,string> pair in original.Values)_carry[pair.Key]=pair.Value;
            _video=IsVideoSelected();
            BuildFields();

            _summary.SetBounds(26,566,520,22);_summary.Font=Theme.Font(12,false);_summary.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;Controls.Add(_summary);
            _bankName.SetBounds(26,588,570,22);_bankName.Font=Theme.Font(12,false);_bankName.ForeColor=Theme.Muted;_bankName.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;Controls.Add(_bankName);

            ActionButton photo=new ActionButton("Choose photo…",false){Location=new Point(26,616),Size=new Size(152,34),Anchor=AnchorStyles.Left|AnchorStyles.Bottom};Controls.Add(photo);
            photo.Click+=delegate{using(OpenFileDialog dialog=new OpenFileDialog(){Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)_cover=dialog.FileName;};
            ActionButton save=new ActionButton("Save recipe",true){Location=new Point(614,616),Size=new Size(180,34),Anchor=AnchorStyles.Right|AnchorStyles.Bottom};Controls.Add(save);
            save.Click+=delegate{SaveRecipe();};
            UpdateSummary();
        }

        void AddLabel(string text,int x,int y)
        {_fields.Controls.Add(new Label(){Text=Strings.T(text),Font=Theme.Font(13,false),ForeColor=Theme.Muted,Location=new Point(x,y),Size=new Size(146,27),TextAlign=ContentAlignment.MiddleLeft});}

        bool IsVideoSelected(){return _kind.SelectedIndex==1;}

        // Mémorise la saisie en cours avant de changer de jeu de réglages.
        void Carry(){foreach(KeyValuePair<string,Control> pair in _values){string v=ValueOf(pair.Value).Trim();if(v!="")_carry[pair.Key]=v;}}

        void BuildFields()
        {
            _fields.Controls.Clear();_values.Clear();
            AddLabel("Name",0,0);_fields.Controls.Add(_name);
            AddLabel("Category",0,37);_fields.Controls.Add(_category);
            AddLabel("This recipe is for",390,0);_fields.Controls.Add(_kind);
            string[] keys=_video?Recipe.VideoParameterOrder:Recipe.ParameterOrder;
            // Deux colonnes : tout tient sans faire défiler.
            int perColumn=(keys.Length+1)/2;
            for(int i=0;i<keys.Length;i++)AddParameter(keys[i],(i/perColumn)*390,80+(i%perColumn)*37);
            _fields.AutoScrollMinSize=new Size(0,80+perColumn*37+10);
        }

        void AddParameter(string key,int x,int y)
        {
            AddLabel(key,x,y);
            string current;
            if(!_carry.TryGetValue(key,out current)||current.Trim()=="")current=Default(key);
            string[] choices=Choices(key,_video);
            Control input;
            if(choices==null)
            {
                TextBox box=new TextBox(){Location=new Point(x+150,y+3),Size=new Size(220,27),Text=current,AccessibleName=key,BorderStyle=BorderStyle.FixedSingle};
                box.TextChanged+=delegate{UpdateSummary();};input=box;
            }
            else
            {
                ComboBox combo=new ComboBox(){Location=new Point(x+150,y+2),Size=new Size(220,27),DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=key,FlatStyle=FlatStyle.Flat};
                combo.Items.AddRange(choices);
                combo.SelectedItem=current;
                if(combo.SelectedIndex<0)
                {
                    // Valeur venue d'une recette importée et absente du boîtier : on la
                    // montre telle quelle plutôt que de la remplacer en silence.
                    if(current!="Not specified"&&current.Trim()!=""){combo.Items.Insert(0,current);combo.SelectedIndex=0;}
                    else combo.SelectedIndex=0;
                }
                combo.SelectedIndexChanged+=delegate{UpdateSummary();};input=combo;
            }
            _fields.Controls.Add(input);_values[key]=input;
        }

        static string ValueOf(Control c){ComboBox combo=c as ComboBox;return combo!=null?Convert.ToString(combo.SelectedItem):((TextBox)c).Text;}

        static string Default(string key)
        {
            switch(key)
            {
                case "Film Simulation": return "Classic Chrome";
                case "Dynamic Range": return "DR100";
                case "Movie Mode": return "FHD 30P";
                case "F-Log": return "Off";
                case "ISO": return "Auto";
                case "White Balance": return "Auto";
                case "Dynamic Range Priority": case "Grain Effect": case "Color Chrome Effect": return "Off";
                default: return "0";
            }
        }

        // Retour immédiat : combien de réglages partiront réellement dans la banque,
        // et à quoi ressemblera son nom une fois le décalage WB ajouté.
        void UpdateSummary()
        {
            Recipe preview=Build();
            if(_video)
            {
                int filled=0;
                foreach(string key in Recipe.VideoParameterOrder)
                {string v=preview.Get(key);if(v!="Not specified"&&v.Trim()!="")filled++;}
                _summary.Text=Strings.T("{0} of {1} movie settings filled in.",filled,Recipe.VideoParameterOrder.Length);
                _summary.ForeColor=filled==Recipe.VideoParameterOrder.Length?Theme.Green:Theme.Muted;
                _bankName.Text=Strings.T("Set in the camera's movie menus — never written to a C1-C7 bank.");
                return;
            }
            int transferable=0,total=0;
            foreach(string key in Recipe.ParameterOrder)
            {
                if(!CameraBankFile.IsTransferable(key))continue;
                total++;
                string v=preview.Get(key);
                if(v!="Not specified"&&v.Trim()!="")transferable++;
            }
            _summary.Text=Strings.T("{0} of {1} settings will be written into the camera bank.",transferable,total);
            _summary.ForeColor=transferable==total?Theme.Green:Theme.Amber;
            string name=CameraBankFile.BuildBankName(preview,preview.Name==""?"RECIPE":preview.Name);
            _bankName.Text=Strings.T("Bank name on the camera:  \"{0}\"   ({1}/{2} characters)",name,name.Length,CameraBankFile.NameMax);
        }

        Recipe Build()
        {
            Recipe r=new Recipe();
            if(_original!=null){r.Id=_original.Id;r.Favorite=_original.Favorite;}
            r.Name=_name.Text.Trim();r.Category=Convert.ToString(_category.SelectedItem);r.Cover=_cover;
            r.Kind=_video?"Video":"Photo";
            r.Source=DataSource.LOCAL;r.Demonstration=false;
            foreach(var pair in _values){string v=ValueOf(pair.Value).Trim();if(v!="")r.Values[pair.Key]=v;}
            return r;
        }

        void SaveRecipe()
        {
            if(string.IsNullOrWhiteSpace(_name.Text)){MessageBox.Show(this,Strings.T("Please enter a recipe name."),Strings.T("Name required"));return;}
            Recipe r=Build();
            List<string> issues=r.CompatibilityIssues();
            if(issues.Count>0&&MessageBox.Show(this,string.Join("\n",issues.ToArray())+"\n\nKeep these settings? Nothing is removed or sent to the camera.",
                "Compatibility warning",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            Result=r;DialogResult=DialogResult.OK;Close();
        }
    }
    public sealed class PacksPage : Panel
    {
        readonly RecipeLibrary _library;
        readonly ActionButton[] _tabs;
        readonly List<CustomSlotRow> _rows=new List<CustomSlotRow>();
        readonly RoundedCard _card=new RoundedCard();
        readonly ActionButton _load=new ActionButton("Load Pack to Camera",true){Enabled=false};
        readonly ActionButton _file=new ActionButton("Create camera file for this pack…",true);
        readonly ToolTip _tips=new ToolTip();
        int _selected;
        public event Action<Recipe> OpenRecipe;
        public PacksPage(RecipeLibrary library)
        {
            _library=library;BackColor=Theme.Background;AutoScroll=true;_tabs=new ActionButton[library.Packs.Count];
            for(int i=0;i<_tabs.Length;i++){int index=i;ActionButton b=new ActionButton(library.Packs[i].Name,false);_tabs[i]=b;Controls.Add(b);b.Click+=delegate{SelectPack(index);};}
            Controls.Add(_card);Controls.Add(_load);_tips.SetToolTip(_load,CameraWritePolicy.Explanation);
            Controls.Add(_file);_file.Click+=delegate{CreatePackFile();};
            _tips.SetToolTip(_file,"Writes one .dat file containing all seven banks. You load it once with Fujifilm's Tether App; this application never writes to the camera.");
            SelectPack(0);
        }
        public void SelectPack(int index)
        {
            _selected=index;
            foreach(CustomSlotRow r in _rows)r.Dispose();_rows.Clear();_card.Controls.Clear();RecipePack pack=_library.Packs[index];pack.Validate();
            foreach(CustomSlot slot in pack.Slots){CustomSlotRow row=new CustomSlotRow(slot);row.OpenRecipe+=delegate(Recipe r){if(OpenRecipe!=null)OpenRecipe(r);};_rows.Add(row);_card.Controls.Add(row);}
            for(int i=0;i<_tabs.Length;i++){_tabs[i].ForeColor=i==index?Theme.Green:Theme.Text;_tabs[i].Invalidate();}PerformLayout();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(_tabs==null)return;Point scroll=AutoScrollPosition;for(int i=0;i<_tabs.Length;i++)if(_tabs[i]!=null)_tabs[i].SetBounds(28+i*150,4+scroll.Y,132,40);
            int w=Math.Min(780,ClientSize.Width-56);_card.SetBounds(28,96+scroll.Y,w,632);for(int i=0;i<_rows.Count;i++)_rows[i].SetBounds(1,1+i*90,w-2,90);
            _load.SetBounds(28,748+scroll.Y,204,39);_file.SetBounds(240,748+scroll.Y,286,39);AutoScrollMinSize=new Size(0,821);
        }
        void CreatePackFile()
        {
            string source=CameraBankFile.FindLatestSettingsFile(AppDomain.CurrentDomain.BaseDirectory);
            if(source==null){MessageBox.Show(this,"No camera settings file has been read yet, so there is nothing to modify.","Read the camera first");return;}
            RecipePack pack=_library.Packs[_selected];
            Dictionary<int,Recipe> assignments=new Dictionary<int,Recipe>();
            Dictionary<int,string> names=new Dictionary<int,string>();
            for(int i=0;i<pack.Slots.Count;i++){assignments[i]=pack.Slots[i].Recipe;names[i]=CameraBankFile.BuildBankName(pack.Slots[i].Recipe,pack.Slots[i].Recipe.Name);}
            using(SaveFileDialog dialog=new SaveFileDialog())
            {
                dialog.Filter="Fujifilm settings backup|*.dat";
                dialog.FileName="xt30-pack-"+pack.Name.ToLowerInvariant()+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".dat";
                dialog.InitialDirectory=Path.GetDirectoryName(source);
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                byte[] blob;
                try{blob=File.ReadAllBytes(source);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not read the source file");return;}
                CameraBankFile.PatchResult result=CameraBankFile.PrepareMany(blob,assignments,names,dialog.FileName);
                if(!result.Success){MessageBox.Show(this,result.Error,"The file was not created");return;}
                System.Text.StringBuilder m=new System.Text.StringBuilder();
                m.AppendLine("Pack \""+pack.Name+"\" written to:");m.AppendLine(result.OutputPath);m.AppendLine();
                m.AppendLine("All seven banks are set in this single file, so one restore applies the whole pack.");
                m.AppendLine();
                for(int i=0;i<pack.Slots.Count;i++)m.AppendLine("  C"+(i+1)+"  "+pack.Slots[i].Recipe.Name);
                if(result.Skipped.Count>0){m.AppendLine();m.AppendLine(result.Skipped.Count+" value(s) could not be stored in the file (see the recipe details for what to set by hand).");}
                m.AppendLine();m.AppendLine("Keep your original settings file: it puts the camera back exactly as it was.");
                m.AppendLine();m.AppendLine("Send it to the camera now?");
                if(MessageBox.Show(this,m.ToString(),"Pack file ready",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)
                    CameraSend.Send(this,result.OutputPath);
            }
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.TextAt(e.Graphics,"Exactly 7 recipes per pack · one file loads all seven banks at once",13,false,Theme.Muted,new Rectangle(28,58+AutoScrollPosition.Y,Width-56,27));}
        protected override void Dispose(bool disposing){if(disposing)_tips.Dispose();base.Dispose(disposing);}
    }
}
