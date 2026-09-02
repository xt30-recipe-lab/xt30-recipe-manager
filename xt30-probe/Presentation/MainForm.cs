using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Xt30Probe.AppCamera;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    public sealed class MainForm : Form
    {
        readonly Sidebar _sidebar=new Sidebar();
        readonly WindowTitleBar _titleBar=new WindowTitleBar();
        readonly Panel _header=new Panel(){BackColor=Theme.Background};
        readonly Panel _host=new Panel(){BackColor=Theme.Background};
        readonly StatusBadge _readOnly=new StatusBadge();
        readonly ActionButton _help=new ActionButton("",false){Quiet=true,IconName="Help"};
        readonly ActionButton _menu=new ActionButton("",false){Quiet=true,IconName="More"};
        readonly StatusBar _status=new StatusBar();
        readonly Panel _cameraPage=new Panel(){BackColor=Theme.Background,AutoScroll=true};
        readonly CameraOverview _overview=new CameraOverview();
        readonly RecentRecipes _recent=new RecentRecipes();
        readonly QuickHelp _quick=new QuickHelp();
        readonly CustomSettingsPanel _custom;
        readonly RecipesPage _recipes;
        readonly PacksPage _packs;
        readonly Panel _slotsPage=new Panel(){AutoScroll=true,BackColor=Theme.Background};
        readonly CustomSettingsPanel _slots;
        readonly ActionButton _readBanks=new ActionButton("Read my camera",false);
        // Panneau de préparation tenu ouvert à côté de l'état réel des banques.
        BankPlanPanel _plan;
        readonly RoundedCard _planCard=new RoundedCard();
        readonly Panel _backups=new Panel(){BackColor=Theme.Background,AutoScroll=true};
        readonly Panel _settings=new Panel(){BackColor=Theme.Background};
        readonly ListBox _backupList=new ListBox();
        readonly ToolTip _tips=new ToolTip();
        readonly Timer _logTimer=new Timer();
        readonly bool _offline;
        public readonly CameraPresenter Camera;
        public readonly RecipeLibrary Library;
        public readonly DiagnosticPanel Diagnostics;
        public string CurrentPage="Camera";
        // Le mode capture ne doit pas ouvrir de fenêtre modale : elle bloquerait
        // la minuterie qui prend la copie d'écran.
        public bool SuppressTutorial;

        public MainForm(bool offline,string dataDirectory)
        {
            _offline=offline;Text="XT30 Recipe Manager";FormBorderStyle=FormBorderStyle.None;BackColor=Theme.Background;Font=Theme.Font(14,false);StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(960,700);AutoScaleMode=AutoScaleMode.None;KeyPreview=true;
            Rectangle screen=Screen.PrimaryScreen.WorkingArea;ClientSize=new Size(Math.Min(1536,screen.Width-30),Math.Min(1024,screen.Height-30));
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch(Exception){}
            SetStyle(ControlStyles.OptimizedDoubleBuffer,true);
            // La langue est chargée avant toute construction de page : les libellés
            // fixés à la création (étiquettes, listes) partent déjà traduits.
            string dataDirectory2=dataDirectory??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
            Strings.Load(dataDirectory2);
            Library=new RecipeLibrary(dataDirectory2);
            Camera=new CameraPresenter(AppDomain.CurrentDomain.BaseDirectory,offline);
            _custom=new CustomSettingsPanel(Library.Slots,Library);_slots=new CustomSettingsPanel(Library.Slots,Library);_recipes=new RecipesPage(Library);_packs=new PacksPage(Library);Diagnostics=new DiagnosticPanel(Camera);
            Controls.AddRange(new Control[]{_titleBar,_sidebar,_header,_host,_status});_header.Controls.AddRange(new Control[]{_readOnly,_help,_menu});
            _header.Paint+=PaintHeader;_help.AccessibleName="Camera connection help";_menu.AccessibleName="Application menu";
            _cameraPage.Controls.AddRange(new Control[]{_overview,_recent,_quick,_custom});_slotsPage.Controls.AddRange(new Control[]{_slots,_readBanks,_planCard});
            _plan=new BankPlanPanel(Library){Dock=DockStyle.Fill};_planCard.Controls.Add(_plan);
            // Aperçu en direct : chaque changement dans le panneau se reflète
            // immédiatement sur la ligne de la banque concernée.
            _plan.PlanChanged+=delegate{_slots.SetPlanned(_plan.Plan);};
            _slots.SetPlanned(_plan.Plan);
            _readBanks.Enabled=!offline;_readBanks.Click+=delegate{ReadCameraBanks();};
            _tips.SetToolTip(_readBanks,"Copies the camera's settings file to this computer and decodes the seven banks. Nothing is written to the camera.");
            _host.Controls.AddRange(new Control[]{_cameraPage,_recipes,_slotsPage,_packs,_backups,Diagnostics,_settings});
            foreach(Control page in _host.Controls)page.Dock=DockStyle.Fill;
            _sidebar.Navigate+=SwitchPage;_status.Diagnostics.Click+=delegate{SwitchPage("Diagnostics");};
            _overview.ScanButton.Click+=delegate{Camera.Scan(Library.ExtendedScan);};_overview.TxtButton.Click+=delegate{OpenReport("xt30_report.txt");};_overview.JsonButton.Click+=delegate{OpenReport("xt30_report.json");};_overview.FolderButton.Click+=delegate{OpenFolder(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rapports"));};
            _recent.ViewAll.Click+=delegate{SwitchPage("Recipes");};_recent.Grid.SetRecipes(Library.Recipes.Take(5).ToList());
            _recent.Grid.OpenRecipe+=OpenRecipe;_recent.Grid.FavoriteRequested+=ToggleFavorite;_recipes.OpenRecipe+=OpenRecipe;_recipes.FavoriteRequested+=ToggleFavorite;
            _recipes.NewRecipe.Click+=delegate{EditRecipe(null);};_custom.OpenRecipe+=OpenRecipe;_slots.OpenRecipe+=OpenRecipe;_packs.OpenRecipe+=OpenRecipe;
            _help.Click+=delegate{MessageBox.Show(this,"MENU → SET UP → CONNECTION SETTING → USB CONNECTION MODE\n\nSelect USB RAW CONV./BACKUP RESTORE, then turn the camera off and on.\n\nThe current USB mode is shown only when confirmed by a camera report. All camera operations remain read-only.","Quick Help",MessageBoxButtons.OK,MessageBoxIcon.Information);};
            _menu.Click+=delegate{ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("Open reports folder",null,delegate{OpenFolder(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rapports"));});menu.Items.Add("About XT30 Recipe Manager",null,delegate{MessageBox.Show(this,"XT30 Recipe Manager 1.0.0\nWindows companion · Read only\n\nLocal recipe examples and slot assignments are demonstration data. Camera writing is disabled.","About");});menu.Closed+=delegate{menu.Dispose();};menu.Show(_menu,new Point(0,_menu.Height));};
            _tips.SetToolTip(_overview,"Model, firmware and protocol come from the last matching scan. A connected USB device does not confirm the camera USB mode.");
            BuildBackups();BuildSettings();Camera.Changed+=delegate{UpdateCamera();};            // Après une lecture ou un envoi, la bibliothèque reconstruit ses slots :
            // les lignes doivent être recréées, sinon elles montrent l'état précédent.
            Library.Changed+=delegate{_recent.Grid.Invalidate(true);_custom.Reload();_slots.Reload();if(_plan!=null)_slots.SetPlanned(_plan.Plan);};
            _logTimer.Interval=150;_logTimer.Tick+=delegate{if(CurrentPage=="Diagnostics")Diagnostics.RefreshLog();};_logTimer.Start();
            _cameraPage.Resize+=delegate{LayoutCamera();};_slotsPage.Resize+=delegate{LayoutSlots();};
            // Changer de langue redessine l'application entière : les textes sont
            // traduits au moment du dessin, il n'y a rien à reconstruire.
            Strings.Changed+=delegate{_recipes.ReloadSimulations();_recipes.RefreshRecipes();_sidebar.Invalidate(true);Refresh();};
            Shown+=delegate{Camera.Start();if(Library.LoadWarning!="")MessageBox.Show(this,Library.LoadWarning,"Library recovery");if(!_offline&&!SuppressTutorial)ShowTutorial(false);};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(Camera.Running){e.Cancel=true;MessageBox.Show(this,"A read-only scan is still running. Please wait until the report is saved before closing.","Scan in progress");}};
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.F){SwitchPage("Recipes");_recipes.Search.Input.Focus();e.Handled=true;}if(e.KeyCode==Keys.Escape)SwitchPage("Camera");};
            SwitchPage("Camera");UpdateCamera();
        }
        public void SwitchPage(string page)
        {
            CurrentPage=page;_sidebar.SelectPage(page);
            _cameraPage.Visible=page=="Camera";_recipes.Visible=page=="Recipes";_slotsPage.Visible=page=="Camera Slots";_packs.Visible=page=="Packs";_backups.Visible=page=="Backups";Diagnostics.Visible=page=="Diagnostics";_settings.Visible=page=="Settings";
            if(page=="Recipes")_recipes.RefreshRecipes();if(page=="Diagnostics")Diagnostics.RefreshReport();if(page=="Backups")RefreshBackups();
            _header.Invalidate();PerformLayout();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(_sidebar==null||_host==null)return;int sidebar=ClientSize.Width<1100?210:250;
            _titleBar.SetBounds(0,0,ClientSize.Width,44);_sidebar.SetBounds(0,44,sidebar,ClientSize.Height-44);_header.SetBounds(sidebar,44,ClientSize.Width-sidebar,92);_host.SetBounds(sidebar,136,ClientSize.Width-sidebar,ClientSize.Height-182);_status.SetBounds(sidebar,ClientSize.Height-46,ClientSize.Width-sidebar,46);
            int badge=_readOnly.PreferredWidth;_readOnly.SetBounds(_header.Width-113-badge,32,badge,34);_help.SetBounds(_header.Width-93,32,35,34);_menu.SetBounds(_header.Width-48,32,30,34);LayoutCamera();LayoutSlots();
        }
        void PaintHeader(object sender,PaintEventArgs e)
        {Theme.TextAt(e.Graphics,CurrentPage=="Camera"?(Camera==null?"FUJIFILM X-T30":Camera.State.Name):Strings.T(CurrentPage),29,true,Theme.Text,new Rectangle(34,24,_header.Width-280,43));}
        void LayoutCamera()
        {
            if(_custom==null)return;int available=_cameraPage.ClientSize.Width;bool wide=ClientSize.Width>=1390;int right=wide?350:0;int center=available-(wide?46:56)-(wide?right+20:0)-(_cameraPage.VerticalScroll.Visible?17:0);int sy=_cameraPage.AutoScrollPosition.Y;
            _overview.SetBounds(28,sy,center,268);_recent.SetBounds(28,286+sy,center,300);_quick.SetBounds(28,604+sy,center,218);_custom.Visible=wide;_custom.SetBounds(28+center+20,sy,right,822);_cameraPage.AutoScrollMinSize=new Size(0,842);
            int count=center<740?3:5;if(_recent.Grid.ItemCount!=count)_recent.Grid.SetRecipes(Library.Recipes.Take(count).ToList());
        }
        void LayoutSlots()
        {
            if(_slots==null||_readBanks==null)return;
            // État réel des banques à gauche, préparation à droite : les deux
            // restent visibles pour que la modification se lise en direct.
            int sy=_slotsPage.AutoScrollPosition.Y;
            int available=_slotsPage.ClientSize.Width-56-(_slotsPage.VerticalScroll.Visible?17:0);
            bool wide=available>=1000;
            int planHeight=_plan==null?700:_plan.PreferredHeight+8;
            _readBanks.SetBounds(28,4+sy,196,42);
            if(wide)
            {
                int w=Math.Min(660,available-390);
                _slots.SetBounds(28,62+sy,Math.Max(320,w),822);
                _planCard.SetBounds(28+w+20,4+sy,370,Math.Max(700,planHeight));
                _slotsPage.AutoScrollMinSize=new Size(0,Math.Max(900,planHeight+20));
            }
            else
            {
                // Fenêtre étroite : la préparation passe sous l'état des banques
                // plutôt que de disparaître.
                int w=Math.Max(320,Math.Min(660,available));
                _slots.SetBounds(28,62+sy,w,822);
                _planCard.SetBounds(28,900+sy,w,planHeight);
                _slotsPage.AutoScrollMinSize=new Size(0,900+planHeight+28);
            }
        }
        // Lecture des sept banques sans aucune manipulation : l'outil BackupRead ne
        // connaît que GetObjectInfo et GetObject, restreints au handle 0.
        void ReadCameraBanks()
        {
            if(_offline||!CameraBanksReader.Available)
            {MessageBox.Show(this,"Camera reading is unavailable in this session.","Read my camera");return;}
            _readBanks.Enabled=false;
            CameraBanksReader.Result result;
            try
            {
                result=ProgressForm.Run(this,Strings.T("Reading your camera"),Strings.T("Copying the settings file from the X-T30…"),
                    delegate(Action<string> report)
                    {
                        CameraBanksReader.Result r=CameraBanksReader.Read();
                        report(Strings.T("Decoding the seven banks…"));
                        return r;
                    });
            }
            finally{_readBanks.Enabled=true;}
            if(!result.Success)
            {
                MessageBox.Show(this,result.Error+"\n\nCheck that the camera is on, connected by USB, and set to\nMENU → SET UP → CONNECTION SETTING → USB CONNECTION MODE → USB RAW CONV./BACKUP RESTORE."
                    +(result.Output==""?"":"\n\n"+result.Output),"The camera was not read",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            Library.ReloadCameraBanks();_slots.Reload();_custom.Reload();
            MessageBox.Show(this,"The seven banks were read from your X-T30 and decoded."
                +(result.ClosedTetherApp?"\n\nThe Fujifilm Tether App was holding the USB connection and was closed to let the read through.":"")
                +"\n\nFile: "+result.SettingsFile,"Camera read",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        // Didacticiel : proposé une fois au premier lancement, puis à la demande
        // depuis les réglages. Il n'exécute aucune action sur l'appareil.
        public void ShowTutorial(bool forced)
        {
            string flag=Path.Combine(Library.DirectoryPath,"tutorial-done.txt");
            if(!forced&&File.Exists(flag))return;
            using(TutorialForm tutorial=new TutorialForm(true))
            {
                tutorial.ShowDialog(this);
                try
                {
                    Directory.CreateDirectory(Library.DirectoryPath);
                    if(tutorial.Suppress)File.WriteAllText(flag,DateTime.Now.ToString("o"));
                    else if(File.Exists(flag))File.Delete(flag);
                }
                catch(Exception){}
            }
        }
        void UpdateCamera()
        {
            _overview.State=Camera.State;_sidebar.CameraCard.State=Camera.State;_status.State=Camera.State;
            _overview.ScanButton.Enabled=!Camera.Running&&!_offline;_overview.ScanButton.Text=Camera.Running?"Scanning…":"Scan Camera";
            _overview.TxtButton.Enabled=File.Exists(Path.Combine(Camera.OutputDirectory,"xt30_report.txt"));_overview.JsonButton.Enabled=File.Exists(Path.Combine(Camera.OutputDirectory,"xt30_report.json"));
            _overview.Invalidate();_sidebar.CameraCard.Invalidate();_status.Invalidate();_header.Invalidate();if(CurrentPage=="Diagnostics")Diagnostics.RefreshReport();
        }
        void ToggleFavorite(Recipe recipe){try{Library.ToggleFavorite(recipe);_recent.Grid.Invalidate(true);_recipes.Grid.Invalidate(true);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not save favorite");}}
        public void OpenRecipe(Recipe recipe)
        {
            using(RecipeDetailForm detail=new RecipeDetailForm(recipe,Library))
            {detail.EditRequested+=delegate{detail.Close();EditRecipe(recipe);};detail.ShowDialog(this);}
        }
        public void EditRecipe(Recipe original)
        {
            using(RecipeEditorForm editor=new RecipeEditorForm(original))
            {
                if(editor.ShowDialog(this)!=DialogResult.OK)return;
                try
                {
                    if(original==null)Library.Add(editor.Result);else{original.Name=editor.Result.Name;original.Category=editor.Result.Category;original.Cover=editor.Result.Cover;original.Values=editor.Result.Values;Library.Save();}
                    _recipes.RefreshRecipes();_recent.Grid.SetRecipes(Library.Recipes.Take(5).ToList());_custom.Invalidate(true);_slots.Invalidate(true);
                }
                catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not save recipe");}
            }
        }
        void OpenReport(string name){try{Diagnostics.OpenFile(name);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Open report");}}
        public void OpenFolder(string path){try{Directory.CreateDirectory(path);Process.Start("explorer.exe","\""+path.TrimEnd('\\')+"\"");}catch(Exception ex){MessageBox.Show(this,ex.Message,"Open folder");}}
        void BuildBackups()
        {
            RoundedCard card=new RoundedCard(){Location=new Point(28,0),Size=new Size(780,380),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};_backups.Controls.Add(card);
            card.Paint+=delegate(object s,PaintEventArgs e){Theme.TextAt(e.Graphics,Strings.T("Local library backups"),19,true,Theme.Text,new Rectangle(24,20,600,30));Theme.Lines(e.Graphics,Strings.T("Save a snapshot of your local recipes and favorites. This does not read or restore camera settings."),14,Theme.Muted,new Rectangle(24,60,card.Width-48,50));};
            ActionButton save=new ActionButton("Back up local library",true){Location=new Point(24,122),Size=new Size(210,38)};card.Controls.Add(save);save.Click+=delegate{try{string path=Library.Backup();RefreshBackups();MessageBox.Show(this,"Local library backed up to:\n"+path,"Backup complete");}catch(Exception ex){MessageBox.Show(this,ex.Message,"Backup failed");}};
            ActionButton folder=new ActionButton("Open backups folder",false){Location=new Point(248,122),Size=new Size(205,38)};card.Controls.Add(folder);folder.Click+=delegate{OpenFolder(Path.Combine(Library.DirectoryPath,"backups"));};
            _backupList.SetBounds(24,185,710,160);_backupList.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;_backupList.Font=Theme.Font(13,false);_backupList.BorderStyle=BorderStyle.None;card.Controls.Add(_backupList);
            ReadOnlyNotice notice=new ReadOnlyNotice(){Location=new Point(28,402),Size=new Size(530,103)};_backups.Controls.Add(notice);
        }
        void RefreshBackups(){_backupList.Items.Clear();string dir=Path.Combine(Library.DirectoryPath,"backups");if(Directory.Exists(dir))foreach(string file in Directory.GetFiles(dir,"*.json").OrderByDescending(x=>x))_backupList.Items.Add(Path.GetFileName(file));if(_backupList.Items.Count==0)_backupList.Items.Add(Strings.T("No local backups yet."));}
        void BuildSettings()
        {
            RoundedCard card=new RoundedCard(){Location=new Point(28,0),Size=new Size(780,404),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};_settings.Controls.Add(card);
            card.Paint+=delegate(object s,PaintEventArgs e){Theme.TextAt(e.Graphics,Strings.T("Application settings"),19,true,Theme.Text,new Rectangle(25,20,500,35));Theme.TextAt(e.Graphics,Strings.T("Language"),14,true,Theme.Text,new Rectangle(25,83,220,26));Theme.TextAt(e.Graphics,Strings.T("Appearance"),14,true,Theme.Text,new Rectangle(25,132,220,26));Theme.TextAt(e.Graphics,"Light · Segoe UI · Fujifilm green",14,false,Theme.Muted,new Rectangle(255,132,card.Width-280,26));Theme.TextAt(e.Graphics,Strings.T("Camera access"),14,true,Theme.Text,new Rectangle(25,181,220,26));Theme.TextAt(e.Graphics,Strings.T("Read only — always enforced by the engine"),13,false,Theme.Muted,new Rectangle(255,181,card.Width-280,26));Theme.Lines(e.Graphics,Strings.T("Extended scanning uses the existing property sweep. It takes longer and does not enable any camera writes."),13,Theme.Muted,new Rectangle(25,292,card.Width-50,58));};
            // Langue : appliquée immédiatement, l'interface est redessinée à la volée.
            ComboBox language=new ComboBox(){Location=new Point(255,79),Size=new Size(220,28),DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Font(14,false),FlatStyle=FlatStyle.Flat,AccessibleName="Language"};
            foreach(string code in Strings.Available)language.Items.Add(Strings.DisplayName(code));
            language.SelectedIndex=Math.Max(0,Array.IndexOf(Strings.Available,Strings.Current));
            language.SelectedIndexChanged+=delegate{Strings.Use(Strings.Available[language.SelectedIndex]);};
            card.Controls.Add(language);
            CheckBox sweep=new CheckBox(){Text=Strings.T("Extended property scan (existing --sweep)"),Checked=Library.ExtendedScan,Location=new Point(26,245),Size=new Size(560,29),Font=Theme.Font(14,false)};card.Controls.Add(sweep);sweep.CheckedChanged+=delegate{try{Library.ExtendedScan=sweep.Checked;Library.Save();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Settings could not be saved");}};
            ActionButton tutorial=new ActionButton("Show the tutorial again",false){Location=new Point(26,350),Size=new Size(240,38)};card.Controls.Add(tutorial);
            tutorial.Click+=delegate{ShowTutorial(true);};
            Strings.Changed+=delegate{sweep.Text=Strings.T("Extended property scan (existing --sweep)");RefreshBackups();Refresh();};
        }
        public void SaveScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));Refresh();Application.DoEvents();
            using(Bitmap bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
        }
        public void SetOfflineState(ConnectionPhase phase)
        {if(!_offline)throw new InvalidOperationException("Simulated states are only allowed in offline UI validation.");Camera.State.Phase=phase;Camera.State.Message="OFFLINE UI TEST — simulated "+phase;Camera.State.Status="UI simulation";UpdateCamera();}
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if(m.Msg==0x84&&WindowState!=FormWindowState.Maximized)
            {
                long lp=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(lp&0xffff),(short)((lp>>16)&0xffff)));int edge=5;
                bool l=p.X<edge,r=p.X>=Width-edge,t=p.Y<edge,b=p.Y>=Height-edge;
                if(l&&t)m.Result=new IntPtr(13);else if(r&&t)m.Result=new IntPtr(14);else if(l&&b)m.Result=new IntPtr(16);else if(r&&b)m.Result=new IntPtr(17);else if(l)m.Result=new IntPtr(10);else if(r)m.Result=new IntPtr(11);else if(t)m.Result=new IntPtr(12);else if(b)m.Result=new IntPtr(15);
            }
        }
        public void ToggleMaximizeWindow(){MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
        protected override void Dispose(bool disposing)
        {if(disposing){_logTimer.Stop();_logTimer.Dispose();_tips.Dispose();if(Camera!=null)Camera.Dispose();}base.Dispose(disposing);}
    }
}
