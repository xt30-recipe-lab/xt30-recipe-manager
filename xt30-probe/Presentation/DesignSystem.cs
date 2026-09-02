using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Xt30Probe.Presentation
{
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(247, 247, 247);
        public static readonly Color Sidebar = Color.FromArgb(250, 250, 250);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(17, 17, 17);
        public static readonly Color Muted = Color.FromArgb(102, 102, 102);
        public static readonly Color Border = Color.FromArgb(228, 228, 228);
        public static readonly Color Green = Color.FromArgb(62, 133, 72);
        public static readonly Color Amber = Color.FromArgb(194, 137, 24);
        public static readonly Color Error = Color.FromArgb(180, 65, 62);
        static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();
        public static Font Font(float size, bool bold)
        {
            string key = size + ":" + bold;
            if (!Fonts.ContainsKey(key))
            {
                string family = bold ? "Segoe UI Semibold" : "Segoe UI";
                using (System.Drawing.Text.InstalledFontCollection collection = new System.Drawing.Text.InstalledFontCollection())
                    foreach (FontFamily f in collection.Families) if (f.Name == "Segoe UI Variable Text") { family = f.Name; break; }
                Fonts[key] = new Font(family, size, bold && family == "Segoe UI Variable Text" ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            }
            return Fonts[key];
        }
        public static void TextAt(Graphics g, string text, float size, bool bold, Color color, Rectangle rect)
        {
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(SolidBrush brush=new SolidBrush(color))using(StringFormat format=new StringFormat(){Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})
                g.DrawString(text??"",Font(size,bold),brush,rect,format);
        }
        public static void Lines(Graphics g, string text, float size, Color color, Rectangle rect)
        {
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(SolidBrush brush=new SolidBrush(color))using(StringFormat format=new StringFormat(){Trimming=StringTrimming.EllipsisWord})g.DrawString(text??"",Font(size,false),brush,rect,format);
        }
        public static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0) { path.AddRectangle(rect); return path; }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        public static void Round(Graphics g, Rectangle rect, Color fill, Color border, int radius)
        {
            if (rect.Width < 2 || rect.Height < 2) return;
            using (GraphicsPath p = Rounded(new RectangleF(rect.X + .5f, rect.Y + .5f, rect.Width - 1, rect.Height - 1), radius))
            { using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p); using (Pen pen = new Pen(border)) g.DrawPath(pen, p); }
        }
        public static void Dot(Graphics g, int x, int y, Color color)
        { using (SolidBrush b = new SolidBrush(color)) g.FillEllipse(b, x, y, 9, 9); }
        public static void Icon(Graphics g, string name, Rectangle r, Color color)
        {
            GraphicsState state = g.Save(); g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 24f, r.Height / 24f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(color, 1.4f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                if (name == "Camera") { g.DrawRectangle(p, 2, 6, 20, 15); g.DrawEllipse(p, 8, 9, 8, 8); g.DrawLines(p, new Point[] { new Point(6,6), new Point(8,3), new Point(14,3), new Point(16,6) }); }
                else if (name == "Recipes") { for (int y = 4; y < 23; y += 7) { g.DrawRectangle(p, 3, y, 3, 3); g.DrawLine(p, 10, y+1, 21, y+1); } }
                else if (name == "Camera Slots" || name == "Packs") { g.DrawRectangle(p, 7, 3, 14, 17); g.DrawLines(p, new Point[] { new Point(5,7),new Point(3,7),new Point(3,23),new Point(16,23),new Point(16,22) }); g.DrawLine(p,10,8,18,8); g.DrawLine(p,10,12,18,12); }
                else if (name == "Backups" || name == "Lock") { g.DrawArc(p, 7, 2, 10, 13, 180, 180); g.DrawRectangle(p,4,9,16,13); g.DrawEllipse(p,11,13,2,3); g.DrawLine(p,12,16,12,18); }
                else if (name == "Diagnostics") { g.DrawLine(p,5,2,5,22); g.DrawLine(p,12,2,12,22); g.DrawLine(p,19,2,19,22); g.DrawEllipse(p,2,6,6,6); g.DrawEllipse(p,9,13,6,6); g.DrawEllipse(p,16,4,6,6); }
                else if (name == "Settings") { g.DrawEllipse(p,5,5,14,14); g.DrawEllipse(p,9,9,6,6); for(int i=0;i<8;i++){ double a=i*Math.PI/4; g.DrawLine(p,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*11,12+(float)Math.Sin(a)*11); } }
                else if (name == "Folder") { g.DrawLines(p,new Point[]{new Point(2,21),new Point(2,5),new Point(9,5),new Point(12,8),new Point(22,8),new Point(22,21),new Point(2,21)}); }
                else if (name == "Refresh") { g.DrawArc(p,3,3,18,18,35,280); g.DrawLines(p,new Point[]{new Point(22,3),new Point(21,9),new Point(16,7)}); }
                else if (name == "Help") { g.DrawEllipse(p,1,1,22,22); g.DrawArc(p,8,5,8,7,180,220);g.DrawLine(p,12,12,12,15);g.DrawEllipse(p,11.5f,18,1,1); }
                else if (name == "More") { for(int y=5;y<=19;y+=7) g.DrawEllipse(p,11,y,1,1); }
                else if (name == "Heart") { g.DrawBezier(p,12,21, -7,9, 3,-1,12,7); g.DrawBezier(p,12,7,21,-1,31,9,12,21); }
                else if (name == "Book") { g.DrawLines(p,new Point[]{new Point(12,4),new Point(3,2),new Point(2,20),new Point(12,22),new Point(22,20),new Point(21,2),new Point(12,4),new Point(12,22)}); g.DrawLine(p,6,6,6,13); g.DrawLine(p,17,6,17,13); }
                else if (name == "Search") { g.DrawEllipse(p,3,3,13,13); g.DrawLine(p,15,15,22,22); }
                else if (name == "Check") { g.DrawLines(p,new Point[]{new Point(4,12),new Point(9,17),new Point(20,6)}); }
                else if (name == "Plus") { g.DrawLine(p,4,12,20,12); g.DrawLine(p,12,4,12,20); }
                else if (name == "Minimize") { g.DrawLine(p,4,12,20,12); }
                else if (name == "Maximize") { g.DrawRectangle(p,5,5,14,14); }
                else if (name == "Close") { g.DrawLine(p,5,5,19,19); g.DrawLine(p,19,5,5,19); }
            }
            g.Restore(state);
        }
    }

    public class RoundedCard : Panel
    {
        public Color FillColor = Theme.Card;
        public Color BorderColor = Theme.Border;
        public int Radius = 10;
        public RoundedCard()
        { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = Theme.Card; }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; Theme.Round(e.Graphics, ClientRectangle, FillColor, BorderColor, Radius); base.OnPaint(e); }
    }

    public class ActionButton : Control
    {
        public bool Primary;
        public bool Quiet;
        public string IconName = "";
        bool _hover;
        public ActionButton(string text, bool primary)
        { Text = text; Primary = primary; Size = new Size(170, 36); Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.PushButton; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? Theme.Green : Color.FromArgb(244,244,245);
            if (_hover && Enabled) fill = Primary ? Color.FromArgb(49,114,59) : Color.FromArgb(235,237,235);
            if (!Enabled) fill = Color.FromArgb(241,241,241);
            if (!Quiet || _hover) Theme.Round(g, ClientRectangle, fill, Primary ? fill : Theme.Border, 7);
            Color text = !Enabled ? Color.FromArgb(148,148,148) : Primary ? Color.White : ForeColor == Color.Empty ? Theme.Text : ForeColor;
            if (Quiet) text = Enabled ? ForeColor : Theme.Muted;
            if (!string.IsNullOrEmpty(IconName)) Theme.Icon(g, IconName, new Rectangle(string.IsNullOrEmpty(Text)?(Width-18)/2:10,(Height-18)/2,18,18),text);
            Rectangle bounds = new Rectangle(string.IsNullOrEmpty(IconName) ? 3 : 34,0, Width-(string.IsNullOrEmpty(IconName)?6:38),Height);
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        // Text reste la clé anglaise (les comparaisons de l'application s'appuient
            // dessus) ; seule la peinture passe par la traduction.
            using(SolidBrush brush=new SolidBrush(text))using(StringFormat format=new StringFormat(){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(Strings.T(Text),Theme.Font(13,Primary),brush,bounds,format);
            if(Focused&&ShowFocusCues) ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-7,Height-7),text,fill);
        }
    }
    public static class Ui
    {
        // Affiche des libellés traduits tout en gardant la valeur anglaise dans
        // l'élément : les filtres et les enregistrements continuent de fonctionner.
        public static void TranslateItems(ComboBox combo)
        {
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                e.DrawBackground();
                if (e.Index >= 0 && e.Index < combo.Items.Count)
                    using (SolidBrush brush = new SolidBrush(e.ForeColor))
                        e.Graphics.DrawString(Strings.T(Convert.ToString(combo.Items[e.Index])), e.Font, brush, e.Bounds.X + 1, e.Bounds.Y + 1);
                e.DrawFocusRectangle();
            };
        }
    }
    public sealed class StatusBadge : Control
    {
        public StatusBadge() { Size=new Size(117,34); AccessibleName="Read only. Camera writing is disabled."; }
        // « LECTURE SEULE » est plus large que « READ ONLY » : la pastille se
        // dimensionne sur son libellé traduit au lieu d'être tronquée.
        public int PreferredWidth { get { return TextRenderer.MeasureText(Strings.T("READ ONLY"),Theme.Font(13,true)).Width+48; } }
        protected override void OnPaint(PaintEventArgs e)
        { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; Theme.Round(e.Graphics,ClientRectangle,Color.FromArgb(236,237,238),Color.FromArgb(236,237,238),10); Theme.Dot(e.Graphics,13,13,Theme.Green); Theme.TextAt(e.Graphics,Strings.T("READ ONLY"),13,true,Theme.Text,new Rectangle(32,0,Width-34,Height)); }
    }
    public sealed class SearchBar : RoundedCard
    {
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,string lParam);
        public readonly TextBox Input = new TextBox();
        public event EventHandler SearchChanged;
        public SearchBar()
        { Height=40; Input.BorderStyle=BorderStyle.None; Input.Font=Theme.Font(14,false); Input.BackColor=Color.White; Input.AccessibleName="Search recipes";Input.HandleCreated+=delegate{SendMessage(Input.Handle,0x1501,new IntPtr(1),Strings.T("Search recipes..."));}; Controls.Add(Input); Input.TextChanged+=delegate{Invalidate();if(SearchChanged!=null)SearchChanged(this,EventArgs.Empty);}; }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Input.SetBounds(37,11,Math.Max(40,Width-50),23); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); Theme.Icon(e.Graphics,"Search",new Rectangle(12,12,17,17),Theme.Muted); }
    }

    // Source rectangles are rendered by the UI, never a flattened screenshot of the UI.
    // The reference mockup supplies temporary demonstration photography only.
    public static class Assets
    {
        static Image _sheet, _wordmark, _logo;
        static readonly Dictionary<string,Image> External = new Dictionary<string,Image>();
        static string Root { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets"); } }
        static Image Load(string file) { using(Image original=Image.FromFile(Path.Combine(Root,file))) return new Bitmap(original); }
        public static void Initialize() { _sheet=Load("reference-mockup.png"); _wordmark=Load("wordmark.png"); _logo=Load("app-logo.png"); }
        public static void Wordmark(Graphics g, Rectangle target) { g.DrawImage(_wordmark,target,new Rectangle(199,293,1528,320),GraphicsUnit.Pixel); }
        public static void Logo(Graphics g, Rectangle target) { g.DrawImage(_logo,target); }
        public static void Camera(Graphics g, Rectangle target) { Fit(g,_sheet,new Rectangle(301,166,237,177),target,false); }
        public static void Cover(Graphics g, string name, Rectangle target)
        {
            Rectangle source;
            switch(name)
            {
                case "cuban":source=new Rectangle(466,485,151,125);break;
                case "gold":source=new Rectangle(636,485,151,125);break;
                case "cinestill":source=new Rectangle(805,485,151,125);break;
                case "portra":source=new Rectangle(974,485,151,125);break;
                case "kodachrome":source=new Rectangle(1231,390,66,67);break;
                case "summer":source=new Rectangle(1231,480,66,67);break;
                case "slot-portra":source=new Rectangle(1231,210,66,67);break;
                case "slot-gold":source=new Rectangle(1231,300,66,67);break;
                case "pacific":source=new Rectangle(298,485,151,125);break;
                default:
                    if(File.Exists(name))
                    {
                        try
                        {
                            if(!External.ContainsKey(name))
                                using(Image img=Image.FromFile(name))
                                {
                                    // Miniature plafonnée : indispensable avec des centaines de covers importées.
                                    float scale=Math.Min(1f,480f/Math.Max(img.Width,img.Height));
                                    Bitmap thumb=new Bitmap(Math.Max(1,(int)(img.Width*scale)),Math.Max(1,(int)(img.Height*scale)));
                                    using(Graphics tg=Graphics.FromImage(thumb)){tg.InterpolationMode=InterpolationMode.HighQualityBicubic;tg.DrawImage(img,0,0,thumb.Width,thumb.Height);}
                                    External[name]=thumb;
                                }
                            Image picture=External[name]; Fit(g,picture,new Rectangle(Point.Empty,picture.Size),target,true); return;
                        }
                        catch(Exception) { }
                    }
                    source=new Rectangle(298,485,151,125);break;
            }
            Fit(g,_sheet,source,target,true);
        }
        static void Fit(Graphics g,Image image,Rectangle source,Rectangle target,bool cover)
        {
            float ratio=cover?Math.Max((float)target.Width/source.Width,(float)target.Height/source.Height):Math.Min((float)target.Width/source.Width,(float)target.Height/source.Height);
            RectangleF dest=new RectangleF(target.X+(target.Width-source.Width*ratio)/2,target.Y+(target.Height-source.Height*ratio)/2,source.Width*ratio,source.Height*ratio);
            GraphicsState saved=g.Save();g.SetClip(target);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,dest,source,GraphicsUnit.Pixel);g.Restore(saved);
        }
        public static void Dispose() { if(_sheet!=null)_sheet.Dispose();if(_wordmark!=null)_wordmark.Dispose();if(_logo!=null)_logo.Dispose();foreach(Image i in External.Values)i.Dispose();External.Clear(); }
    }
}
