using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    public sealed class RecipesPage : Panel
    {
        public readonly SearchBar Search=new SearchBar();
        public readonly RecipeGrid Grid=new RecipeGrid();
        public readonly ActionButton NewRecipe=new ActionButton("New Recipe",true){IconName="Plus"};
        public readonly ComboBox SimulationFilter=new ComboBox(){DropDownStyle=ComboBoxStyle.DropDownList};
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
            string[] filters={"All","Compatible","Favorites","Fuji X Weekly","Local","B&W","Color","Portrait","Street","Night","Vintage","Cinematic"};
            foreach(string filter in filters){ActionButton button=new ActionButton(filter,false){Quiet=true};button.Text=filter;_filters.Add(button);Controls.Add(button);button.Click+=delegate{SetFilter(button.Text);};}
            Grid.OpenRecipe+=delegate(Recipe r){if(OpenRecipe!=null)OpenRecipe(r);};Grid.FavoriteRequested+=delegate(Recipe r){if(FavoriteRequested!=null)FavoriteRequested(r);RefreshRecipes();};
            Search.SearchChanged+=delegate{RefreshRecipes();};SetFilter("All");
        }
        public void ReloadSimulations()
        {
            SimulationFilter.Items.Clear();SimulationFilter.Items.Add("All simulations");
            foreach(string sim in _library.Simulations())SimulationFilter.Items.Add(sim);
            SimulationFilter.SelectedIndex=0;
        }
        public void SetFilter(string filter){_filter=filter;foreach(ActionButton b in _filters){b.Quiet=b.Text!=filter;b.ForeColor=b.Text==filter?Theme.Green:Theme.Muted;b.Invalidate();}RefreshRecipes();}
        public void RefreshRecipes(){string sim=SimulationFilter.SelectedItem as string;Grid.SetRecipes(_library.Query(Search.Input.Text,_filter,sim??"All simulations"));PerformLayout();Invalidate();}
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(Grid==null||SimulationFilter==null)return;int width=ClientSize.Width-56-(VerticalScroll.Visible?17:0);Point scroll=AutoScrollPosition;
            Search.SetBounds(28+scroll.X,3+scroll.Y,Math.Min(340,width-388),42);
            SimulationFilter.SetBounds(Search.Right+10,10+scroll.Y,180,30);
            NewRecipe.SetBounds(width-138+scroll.X,5+scroll.Y,166,38);
            int x=28,filterY=64;foreach(ActionButton b in _filters){int w=b.Text=="Compatible"?110:b.Text=="Fuji X Weekly"?128:b.Text=="Favorites"?100:90;if(x+w>width+28){x=28;filterY+=42;}b.SetBounds(x+scroll.X,filterY+scroll.Y,w,36);x+=w+5;}
            Grid.SetBounds(28+scroll.X,filterY+84+scroll.Y,Math.Max(280,width),Math.Max(250,Grid.Height));AutoScrollMinSize=new Size(0,Grid.Height+filterY+110);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.TextAt(e.Graphics,Grid.ItemCount+" recipes   ·   SOURCE badges: FUJI X WEEKLY (imported, read-only) / LOCAL",12,false,Theme.Muted,new Rectangle(28,Grid.Top-33,Width-56,25));}
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
            _recipe=recipe;Text=recipe.Name+(recipe.IsFromCamera?" · Camera bank":recipe.IsImported?" · Fuji X Weekly":" · Local recipe");BackColor=Color.White;Font=Theme.Font(14,false);StartPosition=FormStartPosition.CenterParent;
            // lignes affichées : paramètres standard + réglages étendus présents
            _rows.AddRange(Recipe.ParameterOrder);
            foreach(string key in Recipe.AdditionalParameters)if(recipe.Values.ContainsKey(key))_rows.Add(key);
            _noteTop=141+_rows.Count*32+12;
            // Hauteur ajustée au contenu : avec 18 paramètres, une fenêtre fixe reléguait
            // la rangée de boutons hors de la zone visible.
            int needed=_noteTop+170;
            int maxHeight=Screen.PrimaryScreen.WorkingArea.Height-60;
            ClientSize=new Size(970,Math.Max(660,Math.Min(maxHeight,needed)));MinimumSize=new Size(780,620);
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch(Exception){}
            _hero=new PictureHero(recipe.Cover);Controls.Add(_hero);Controls.Add(_content);_content.Controls.Add(_badge);_content.Controls.Add(_edit);_content.Controls.Add(_article);_content.Controls.Add(_camera);_content.Controls.Add(_compare);_badge.Recipe=recipe;
            // Guide de saisie manuelle : proposé seulement quand de vraies banques ont
            // été lues, et jamais pour une banque comparée à elle-même.
            _compare.Visible=library!=null&&library.SlotsAreFromCamera&&!recipe.IsFromCamera;
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
                ?"SOURCE: CAMERA  ·  READ FROM YOUR X-T30"
                :_recipe.IsImported
                ?"SOURCE: FUJI X WEEKLY"+(_recipe.Author!=""?"  ·  "+_recipe.Author.ToUpperInvariant():"")+(_recipe.PublishedAt.Length>=10?"  ·  "+_recipe.PublishedAt.Substring(0,10):"")
                :"SOURCE: LOCAL"+(_recipe.Demonstration?"  ·  DEMONSTRATION RECIPE":"  ·  YOUR LIBRARY");
            Theme.TextAt(g,origin,10,true,Theme.Muted,new Rectangle(31,62+y,w-56,18));
            int top=141;
            foreach(string key in _rows)
            {Theme.TextAt(g,key,13,false,Theme.Muted,new Rectangle(31,top+y,206,25));Theme.TextAt(g,_recipe.Get(key),13,true,Theme.Text,new Rectangle(246,top+y,w-271,25));using(Pen p=new Pen(Color.FromArgb(241,241,241)))g.DrawLine(p,31,top+29+y,w-28,top+29+y);top+=32;}
            List<string> issues=_recipe.CompatibilityIssues();
            string note;
            Color noteColor;
            if(_recipe.IsImported&&_recipe.CompatStatus=="XT30_INCOMPATIBLE"){note=_recipe.CompatReason;noteColor=Color.FromArgb(196,74,64);}
            else if(issues.Count==0){note="These parameters are compatible with the X-T30 feature set. Camera transfer remains disabled.";noteColor=Theme.Muted;}
            else{note=string.Join("\n",issues.ToArray());noteColor=Theme.Amber;}
            Theme.Lines(g,note,12,noteColor,new Rectangle(31,_noteTop+y,w-60,72));
            string footer=_recipe.IsFromCamera
                ?"Read from your camera's settings file. Only what that file stores is shown; ISO, WB shift and exposure are not in it and stay unspecified."
                :_recipe.IsImported
                ?"Imported from the public Fuji X Weekly article. Values are shown exactly as published; missing values stay unspecified."
                :(_recipe.Demonstration?"Example values for this interface — not an authenticated recipe.":"Stored on this computer only.");
            Theme.Lines(g,footer,11,Theme.Muted,new Rectangle(31,_noteTop+78+y,w-55,28));
        }
        protected override void Dispose(bool disposing){if(disposing)_tips.Dispose();base.Dispose(disposing);}
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
            string name = _recipe.Name;
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
                message.AppendLine("To load it into the camera:");
                message.AppendLine("  1. Camera on, USB mode RAW CONV./BACKUP RESTORE");
                message.AppendLine("  2. Open Fujifilm's Tether App (or X Acquire)");
                message.AppendLine("  3. Choose RESTORE CAMERA SETTINGS and pick this file");
                message.AppendLine();
                message.AppendLine("Keep the original file: it restores your camera exactly as it was.");
                MessageBox.Show(this, message.ToString(), "Camera settings file ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    public sealed class PictureHero : Control
    {
        public string Cover;
        public PictureHero(string cover){Cover=cover;DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){Assets.Cover(e.Graphics,Cover,ClientRectangle);}
    }
    public sealed class RecipeEditorForm : Form
    {
        readonly Dictionary<string,TextBox> _values=new Dictionary<string,TextBox>();
        readonly TextBox _name=new TextBox();
        readonly ComboBox _category=new ComboBox();
        readonly Panel _fields=new Panel(){AutoScroll=true};
        readonly Recipe _original;
        string _cover;
        public Recipe Result;
        public RecipeEditorForm(Recipe original)
        {
            _original=original;_cover=original==null?"pacific":original.Cover;
            Text=original==null?"New local recipe":"Edit local recipe";ClientSize=new Size(570,760);MinimumSize=new Size(550,600);StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;Font=Theme.Font(14,false);
            Label heading=new Label(){Text="LOCAL RECIPE · No camera writing",Font=Theme.Font(18,true),Location=new Point(25,19),Size=new Size(510,34)};Controls.Add(heading);
            _fields.SetBounds(25,65,520,615);_fields.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(_fields);
            AddFieldLabel("Name",0);_name.SetBounds(222,3,270,27);_name.Text=original==null?"":original.Name;_name.MaxLength=80;_fields.Controls.Add(_name);
            AddFieldLabel("Category",39);_category.SetBounds(222,42,270,27);_category.DropDownStyle=ComboBoxStyle.DropDownList;_category.Items.AddRange(new object[]{"Portrait","Street","Night","Vintage","Cinematic","Travel"});_category.SelectedItem=original==null?"Vintage":original.Category;if(_category.SelectedIndex<0)_category.SelectedIndex=0;_fields.Controls.Add(_category);
            int y=83;foreach(string key in Recipe.ParameterOrder){AddParameter(key,y,original);y+=37;}foreach(string key in Recipe.AdditionalParameters){AddParameter(key,y,original);y+=37;}_fields.AutoScrollMinSize=new Size(0,y+15);
            ActionButton photo=new ActionButton("Choose photo…",false){Location=new Point(25,707),Size=new Size(152,36),Anchor=AnchorStyles.Left|AnchorStyles.Bottom};Controls.Add(photo);
            photo.Click+=delegate{using(OpenFileDialog dialog=new OpenFileDialog(){Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)_cover=dialog.FileName;};
            ActionButton save=new ActionButton("Save local recipe",true){Location=new Point(348,707),Size=new Size(196,36),Anchor=AnchorStyles.Right|AnchorStyles.Bottom};Controls.Add(save);save.Click+=delegate{SaveRecipe();};
        }
        void AddFieldLabel(string key,int y){_fields.Controls.Add(new Label(){Text=key,Font=Theme.Font(13,false),ForeColor=Theme.Muted,Location=new Point(0,y),Size=new Size(218,29),TextAlign=ContentAlignment.MiddleLeft});}
        void AddParameter(string key,int y,Recipe original)
        {AddFieldLabel(key,y);TextBox box=new TextBox(){Location=new Point(222,y+3),Size=new Size(270,27),Text=original==null?Default(key):original.Get(key),AccessibleName=key};_fields.Controls.Add(box);_values[key]=box;}
        static string Default(string key){if(key=="Film Simulation")return "Classic Chrome";if(key=="Dynamic Range")return "DR100";if(key=="ISO"||key=="White Balance")return "Auto";if(key=="Grain Size")return "Not specified";if(key.Contains("Effect")||key=="Dynamic Range Priority"||key=="Color Chrome FX Blue")return "Off";return "0";}
        void SaveRecipe()
        {
            if(string.IsNullOrWhiteSpace(_name.Text)){MessageBox.Show(this,"Please enter a recipe name.");return;}
            Recipe r=new Recipe();if(_original!=null){r.Id=_original.Id;r.Favorite=_original.Favorite;}
            r.Name=_name.Text.Trim();r.Category=Convert.ToString(_category.SelectedItem);r.Cover=_cover;r.Source=DataSource.LOCAL;r.Demonstration=_original!=null&&_original.Demonstration;
            foreach(var pair in _values)r.Values[pair.Key]=pair.Value.Text.Trim();
            List<string> issues=r.CompatibilityIssues();if(issues.Count>0&&MessageBox.Show(this,string.Join("\n",issues.ToArray())+"\n\nKeep these settings in the local recipe? Nothing will be removed or sent to the camera.","Compatibility warning",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
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
        readonly ToolTip _tips=new ToolTip();
        public event Action<Recipe> OpenRecipe;
        public PacksPage(RecipeLibrary library)
        {
            _library=library;BackColor=Theme.Background;AutoScroll=true;_tabs=new ActionButton[library.Packs.Count];
            for(int i=0;i<_tabs.Length;i++){int index=i;ActionButton b=new ActionButton(library.Packs[i].Name,false);_tabs[i]=b;Controls.Add(b);b.Click+=delegate{SelectPack(index);};}
            Controls.Add(_card);Controls.Add(_load);_tips.SetToolTip(_load,CameraWritePolicy.Explanation);SelectPack(0);
        }
        public void SelectPack(int index)
        {
            foreach(CustomSlotRow r in _rows)r.Dispose();_rows.Clear();_card.Controls.Clear();RecipePack pack=_library.Packs[index];pack.Validate();
            foreach(CustomSlot slot in pack.Slots){CustomSlotRow row=new CustomSlotRow(slot);row.OpenRecipe+=delegate(Recipe r){if(OpenRecipe!=null)OpenRecipe(r);};_rows.Add(row);_card.Controls.Add(row);}
            for(int i=0;i<_tabs.Length;i++){_tabs[i].ForeColor=i==index?Theme.Green:Theme.Text;_tabs[i].Invalidate();}PerformLayout();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(_tabs==null)return;Point scroll=AutoScrollPosition;for(int i=0;i<_tabs.Length;i++)if(_tabs[i]!=null)_tabs[i].SetBounds(28+i*150,4+scroll.Y,132,40);
            int w=Math.Min(780,ClientSize.Width-56);_card.SetBounds(28,96+scroll.Y,w,632);for(int i=0;i<_rows.Count;i++)_rows[i].SetBounds(1,1+i*90,w-2,90);_load.SetBounds(28,748+scroll.Y,204,39);AutoScrollMinSize=new Size(0,821);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.TextAt(e.Graphics,"Exactly 7 recipes per pack · LOCAL demonstration · camera transfer disabled",13,false,Theme.Muted,new Rectangle(28,58+AutoScrollPosition.Y,Width-56,27));}
        protected override void Dispose(bool disposing){if(disposing)_tips.Dispose();base.Dispose(disposing);}
    }
}
