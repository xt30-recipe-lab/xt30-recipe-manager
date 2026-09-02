using System;
using System.Drawing;
using System.Windows.Forms;
using Xt30Probe.AppCamera;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    public static class CameraWritePolicy
    {
        public static bool Available { get { return false; } }
        public const string Explanation = "Camera writing not available yet";
    }
    public sealed class NavigationItem : Control
    {
        public bool Selected;
        bool _hover;
        public NavigationItem(string text)
        { Text=text; Height=56; Cursor=Cursors.Hand; TabStop=true; AccessibleRole=AccessibleRole.PageTab; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true); }
        protected override void OnMouseEnter(EventArgs e){_hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){_hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if(Selected||_hover)Theme.Round(g,ClientRectangle,Color.FromArgb(239,240,240),Color.FromArgb(239,240,240),8);
            if(Selected)using(Pen p=new Pen(Theme.Green,3))g.DrawLine(p,1,5,1,Height-5);
            Color c=Selected?Theme.Green:Theme.Text;Theme.Icon(g,Text,new Rectangle(21,17,23,23),Selected?Theme.Green:Theme.Muted);
            Theme.TextAt(g,Text,15,Selected,c,new Rectangle(65,0,Width-70,Height));
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-11,Height-11));
        }
    }
    public sealed class Sidebar : Panel
    {
        public readonly CameraStatusCard CameraCard=new CameraStatusCard();
        public readonly NavigationItem[] Items;
        public event Action<string> Navigate;
        public Sidebar()
        {
            BackColor=Theme.Sidebar;DoubleBuffered=true;
            string[] names={"Camera","Recipes","Camera Slots","Packs","Backups","Diagnostics","Settings"};Items=new NavigationItem[names.Length];
            for(int i=0;i<names.Length;i++){NavigationItem item=new NavigationItem(names[i]);Items[i]=item;Controls.Add(item);item.Click+=delegate{SelectPage(item.Text);if(Navigate!=null)Navigate(item.Text);};}
            Controls.Add(CameraCard);SelectPage("Camera");
        }
        public void SelectPage(string name){foreach(NavigationItem item in Items){item.Selected=item.Text==name;item.Invalidate();}}
        protected override void OnLayout(LayoutEventArgs e)
        {base.OnLayout(e);if(Items==null)return;for(int i=0;i<Items.Length;i++)if(Items[i]!=null)Items[i].SetBounds(10,112+i*59,Width-20,56);CameraCard.Visible=Height>=750;CameraCard.SetBounds(16,Math.Max(556,Height-233),Width-32,136);}
        protected override void OnPaint(PaintEventArgs e)
        {base.OnPaint(e);Assets.Wordmark(e.Graphics,new Rectangle(27,33,Width-54,46));using(Pen p=new Pen(Theme.Border))e.Graphics.DrawLine(p,Width-1,0,Width-1,Height);Theme.TextAt(e.Graphics,"v1.0.0",11,false,Theme.Muted,new Rectangle(28,Height-(CameraCard.Visible?83:39),100,20));}
    }
    public sealed class CameraStatusCard : RoundedCard
    {
        public CameraState State;
        public static Color StateColor(CameraState s)
        {return s==null?Theme.Muted:s.Phase==ConnectionPhase.Connected?Theme.Green:s.Phase==ConnectionPhase.Communicating?Theme.Amber:s.Phase==ConnectionPhase.Error?Theme.Error:Color.FromArgb(151,154,156);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;Assets.Camera(g,new Rectangle(12,28,66,56));
            Theme.TextAt(g,State==null?"FUJIFILM X-T30":State.Name,13,true,Theme.Text,new Rectangle(88,19,Width-96,23));
            Theme.Dot(g,90,51,StateColor(State));Theme.TextAt(g,State==null?"Disconnected":State.ConnectionText,13,false,Theme.Text,new Rectangle(106,43,Width-111,24));
            Theme.Lines(g,State!=null&&State.UsbMode!="Not reported"?State.UsbMode.Replace("/","/\n"):"USB mode\nnot reported",12,Theme.Muted,new Rectangle(89,80,Width-97,45));
        }
    }
    public sealed class CameraOverview : RoundedCard
    {
        public CameraState State;
        public readonly ActionButton ScanButton=new ActionButton("Scan Camera",true);
        public readonly ActionButton TxtButton=new ActionButton("Open TXT Report",false);
        public readonly ActionButton JsonButton=new ActionButton("Open JSON Report",false);
        public readonly ActionButton FolderButton=new ActionButton("Open Reports Folder",false);
        public CameraOverview(){Controls.AddRange(new Control[]{ScanButton,TxtButton,JsonButton,FolderButton});FolderButton.IconName="Folder";AccessibleName="Camera overview. Live connection with last scan metadata.";}
        protected override void OnLayout(LayoutEventArgs e)
        {base.OnLayout(e);int w=Width<770?151:170;int x=Width-w-27;ScanButton.SetBounds(x,82,w,35);TxtButton.SetBounds(x,125,w,35);JsonButton.SetBounds(x,168,w,35);FolderButton.SetBounds(x,211,w,35);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;int actionWidth=Width<770?151:170;int actionX=Width-actionWidth-27;int pictureWidth=Width<770?160:245;int infoX=pictureWidth+49;
            Assets.Camera(g,new Rectangle(24,30,pictureWidth,195));
            Theme.Dot(g,infoX,29,CameraStatusCard.StateColor(State));Theme.TextAt(g,State==null?"Looking for camera…":State.ConnectionText,17,true,Theme.Text,new Rectangle(infoX+19,18,actionX-infoX-28,30));
            string subtitle=State!=null&&State.Historical?"Last scan data · camera is not connected":State!=null&&State.UsbMode!="Not reported"?State.UsbMode:"USB mode not reported by the camera";
            Theme.TextAt(g,subtitle,12,false,Theme.Text,new Rectangle(infoX,52,actionX-infoX-22,24));
            string[] labels={"Model","Firmware","USB Mode","VID / PID","Protocol","Status"};
            string[] values=State==null?new string[]{"—","—","Not reported","— / —","Not scanned","Waiting for camera"}:new string[]{State.Name,State.Firmware,State.UsbMode,State.VidPid,State.Protocol,State.Status};
            int labelWidth=Width<770?76:105;
            for(int i=0;i<labels.Length;i++){Theme.TextAt(g,labels[i],12,false,Theme.Text,new Rectangle(infoX,89+i*27,labelWidth-5,22));Theme.TextAt(g,values[i],12,false,Theme.Text,new Rectangle(infoX+labelWidth,89+i*27,Math.Max(40,actionX-infoX-labelWidth-24),22));}
            using(Pen p=new Pen(Theme.Border))g.DrawLine(p,actionX-22,60,actionX-22,245);
            Theme.TextAt(g,"ACTIONS",10,false,Theme.Muted,new Rectangle(actionX,55,actionWidth,20));
        }
    }
    public sealed class QuickHelp : RoundedCard
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;Theme.Icon(g,"Book",new Rectangle(23,21,21,21),Theme.Text);Theme.TextAt(g,"Quick Help",15,true,Theme.Text,new Rectangle(55,16,150,30));
            string[] steps={"MENU","SET UP","CONNECTION SETTING","USB CONNECTION MODE"};
            for(int i=0;i<steps.Length;i++)Theme.TextAt(g,steps[i],10,false,Theme.Text,new Rectangle(22,61+i*22,163,20));
            Theme.Lines(g,"USB RAW CONV./\nBACKUP RESTORE",11,Theme.Green,new Rectangle(22,153,156,38));
            using(Pen p=new Pen(Theme.Border))g.DrawLine(p,188,63,188,193);
            int diagram=Width>=780?265:0;int textWidth=Width-230-diagram;
            Theme.Lines(g,"For full communication with your X-T30, please set the USB CONNECTION MODE to:",13,Theme.Text,new Rectangle(212,63,textWidth,50));
            Theme.Lines(g,"USB RAW CONV./BACKUP RESTORE",13,Theme.Green,new Rectangle(212,115,textWidth,39));
            Theme.TextAt(g,"Then turn the camera off and on again.",13,false,Theme.Text,new Rectangle(212,160,textWidth,25));
            if(diagram>0)
            {
                int x=Width-278;Theme.Round(g,new Rectangle(x+218,87,28,47),Color.White,Color.LightGray,8);Theme.Round(g,new Rectangle(x+177,51,61,145),Color.White,Color.LightGray,7);Theme.Round(g,new Rectangle(x+180,43,37,12),Color.White,Color.LightGray,2);
                Theme.Round(g,new Rectangle(x,52,206,144),Color.White,Color.LightGray,7);Theme.Round(g,new Rectangle(x+17,69,169,111),Color.FromArgb(240,240,240),Color.LightGray,2);
                for(int i=0;i<5;i++){using(SolidBrush b=new SolidBrush(Color.FromArgb(192,192,192)))g.FillRectangle(b,x+44,75+i*18,130-i%3*27,8);Theme.Icon(g,"Camera",new Rectangle(x+22,76+i*18,11,11),Theme.Muted);}
                Theme.Round(g,new Rectangle(x+40,143,140,15),Color.White,Color.FromArgb(164,188,167),1);Theme.TextAt(g,"USB RAW CONV./BACKUP RESTORE",6.5f,false,Theme.Text,new Rectangle(x+43,143,138,15));
            }
        }
    }
    public sealed class ReadOnlyNotice : RoundedCard
    {
        public ReadOnlyNotice(){FillColor=Color.FromArgb(255,249,232);BorderColor=Color.FromArgb(242,216,154);Radius=8;}
        protected override void OnPaint(PaintEventArgs e)
        {base.OnPaint(e);Theme.Icon(e.Graphics,"Lock",new Rectangle(16,15,18,18),Theme.Amber);Theme.TextAt(e.Graphics,"READ ONLY",12,true,Theme.Text,new Rectangle(43,11,Width-50,27));Theme.Lines(e.Graphics,"Direct camera slot access is currently unavailable.\nWriting to the camera is disabled.",12,Theme.Text,new Rectangle(21,46,Width-39,61));}
    }
    public sealed class CustomSlotRow : Control
    {
        public readonly CustomSlot Slot;
        public event Action<Recipe> OpenRecipe;
        public CustomSlotRow(CustomSlot slot)
        {Slot=slot;Height=90;BackColor=Color.White;Cursor=Cursors.Hand;TabStop=true;AllowDrop=false;AccessibleRole=AccessibleRole.PushButton;AccessibleName="C"+slot.Number+", "+slot.Source+", "+slot.Recipe.Name;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnClick(EventArgs e){base.OnClick(e);if(OpenRecipe!=null)OpenRecipe(Slot.Recipe);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Theme.TextAt(g,"C"+Slot.Number,20,true,Theme.Text,new Rectangle(18,0,37,Height));
            using(var path=Theme.Rounded(new Rectangle(60,10,68,68),6)){var state=g.Save();g.SetClip(path);Assets.Cover(g,Slot.PreviewCover??Slot.Recipe.Cover,new Rectangle(60,10,68,68));g.Restore(state);}
            Theme.TextAt(g,Slot.Recipe.Name,13,true,Theme.Text,new Rectangle(142,20,Width-175,22));
            string line2=Slot.Recipe.Simulation;
            if(Slot.Source==DataSource.CAMERA)
            {
                string dr=Slot.Recipe.Get("Dynamic Range");
                if(dr!="Not specified")line2+="  ·  "+(dr.StartsWith("DR-P")?"DR-P":dr);
            }
            Theme.TextAt(g,line2,12,false,Theme.Text,new Rectangle(142,42,Width-175,20));
            bool camera=Slot.Source==DataSource.CAMERA;
            string provenance=camera?"CAMERA":Slot.Source.ToString();
            if(camera&&Slot.Recipe.MatchedLibraryRecipe!=null)provenance+="  ·  matches \""+Slot.Recipe.MatchedLibraryRecipe.Name+"\" in library";
            Theme.TextAt(g,provenance,9,true,camera?Theme.Green:Theme.Muted,new Rectangle(142,64,Width-175,15));
            Theme.Icon(g,"More",new Rectangle(Width-33,35,18,20),Theme.Muted);
            using(Pen p=new Pen(Color.FromArgb(239,239,239)))g.DrawLine(p,0,Height-1,Width,Height-1);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-7,Height-7));
        }
    }
    public sealed class CustomSettingsPanel : RoundedCard
    {
        readonly CustomSlotRow[] _rows=new CustomSlotRow[7];
        readonly ReadOnlyNotice _notice=new ReadOnlyNotice();
        readonly ActionButton _refresh=new ActionButton("",false){Quiet=true,IconName="Refresh"};
        readonly ActionButton _more=new ActionButton("",false){Quiet=true,IconName="More"};
        readonly ToolTip _tips=new ToolTip();
        public event Action<Recipe> OpenRecipe;
        readonly RecipeLibrary _library;
        readonly System.Collections.Generic.List<CustomSlot> _slots;
        public CustomSettingsPanel(System.Collections.Generic.List<CustomSlot> slots):this(slots,null){}
        public CustomSettingsPanel(System.Collections.Generic.List<CustomSlot> slots,RecipeLibrary library)
        {
            _library=library;_slots=slots;
            BuildRows();
            Controls.Add(_notice);Controls.Add(_refresh);Controls.Add(_more);
            bool fromCamera=library!=null&&library.SlotsAreFromCamera;
            _refresh.AccessibleName=fromCamera?"Reload camera banks from the last settings file":"Refresh local demonstration slots";
            _tips.SetToolTip(_refresh,fromCamera?"Re-read the decoded settings file from disk. No camera command is sent.":"Refresh local display only. Camera slot reading is unavailable.");
            // La bibliothèque reconstruit sa liste de slots : il faut recréer les lignes,
            // sinon elles resteraient liées aux anciens objets.
            _refresh.Click+=delegate{if(_library!=null)_library.ReloadCameraBanks();BuildRows();PerformLayout();Invalidate();};
            _more.AccessibleName="About camera custom settings";
            _more.Click+=delegate{MessageBox.Show(this,Explain(),"Camera Custom Settings",MessageBoxButtons.OK,MessageBoxIcon.Information);};
        }
        void BuildRows()
        {
            bool fromCamera=_library!=null&&_library.SlotsAreFromCamera;
            for(int i=0;i<7;i++)
            {
                if(_rows[i]!=null){Controls.Remove(_rows[i]);_rows[i].Dispose();}
                CustomSlot slot=i<_slots.Count?_slots[i]:null;
                if(slot==null)continue;
                _rows[i]=new CustomSlotRow(slot);
                _rows[i].OpenRecipe+=delegate(Recipe r){if(OpenRecipe!=null)OpenRecipe(r);};
                _tips.SetToolTip(_rows[i],(fromCamera?"Read from your X-T30 settings file · ":"Local demonstration · ")+CameraWritePolicy.Explanation);
                Controls.Add(_rows[i]);
                _rows[i].BringToFront();
            }
        }
        string Explain()
        {
            if(_library==null||!_library.SlotsAreFromCamera)
                return "These seven slots are LOCAL demonstration data. They have not been read from your X-T30.\n\nNo drag-and-drop or camera writing is enabled.";
            CameraBanksSnapshot s=_library.CameraBanks;
            return "These seven banks were READ FROM YOUR CAMERA.\n\nModel: "+s.Model+"\nSerial: "+s.Serial
                +"\nRead on: "+s.ReadAt.ToString("dd/MM/yyyy HH:mm")
                +"\n\nSource: the camera settings file (PTP object handle 0, format 0x5000), decoded with the "+s.Layout+" layout.\n\n"
                +"Only the values actually stored in that file are shown. Settings it does not contain (ISO, WB shift, exposure) stay \"Not specified\" rather than being guessed.\n\n"
                +"Reading only — camera writing remains disabled.";
        }
        protected override void OnLayout(LayoutEventArgs e)
        {base.OnLayout(e);_refresh.SetBounds(Width-87,20,34,32);_more.SetBounds(Width-47,20,28,32);for(int i=0;i<7;i++)if(_rows[i]!=null)_rows[i].SetBounds(1,63+i*90,Width-2,90);_notice.SetBounds(16,Height-113,Width-32,98);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.TextAt(e.Graphics,"Camera Custom Settings",15,true,Theme.Text,new Rectangle(19,18,Width-103,26));
            bool fromCamera=_library!=null&&_library.SlotsAreFromCamera;
            string subtitle=fromCamera
                ?"READ FROM CAMERA · "+_library.CameraBanks.Model+" · "+_library.CameraBanks.ReadAt.ToString("dd/MM/yyyy HH:mm")
                :"LOCAL DEMONSTRATION";
            Theme.TextAt(e.Graphics,subtitle,9,false,fromCamera?Theme.Green:Theme.Muted,new Rectangle(19,43,Width-110,17));
        }
        protected override void Dispose(bool disposing){if(disposing)_tips.Dispose();base.Dispose(disposing);}
    }
    public sealed class StatusBar : Panel
    {
        public CameraState State;
        public readonly ActionButton Diagnostics=new ActionButton("View Diagnostics",false){Quiet=true,ForeColor=Theme.Green};
        public StatusBar(){BackColor=Color.White;Height=46;DoubleBuffered=true;Controls.Add(Diagnostics);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Diagnostics.SetBounds(Width-154,5,139,36);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);using(Pen p=new Pen(Theme.Border))e.Graphics.DrawLine(p,0,0,Width,0);
            Color color=CameraStatusCard.StateColor(State);Theme.Dot(e.Graphics,30,19,color);
            Theme.TextAt(e.Graphics,State==null?"Waiting for camera…":State.Message,12,false,Theme.Muted,new Rectangle(51,0,Math.Max(150,Width-485),Height));
            Theme.TextAt(e.Graphics,"Last scan: "+(State!=null&&State.LastScan.HasValue?State.LastScan.Value.ToString("dd/MM/yyyy  HH:mm"):"—"),12,false,Theme.Muted,new Rectangle(Width-391,0,232,Height));
        }
    }
}
