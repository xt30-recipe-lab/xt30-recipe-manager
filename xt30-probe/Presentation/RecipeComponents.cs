using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    public sealed class RecipeCard : Control
    {
        public readonly Recipe Recipe;
        public event Action<Recipe> OpenRecipe;
        public event Action<Recipe> FavoriteRequested;
        bool _hover;
        public RecipeCard(Recipe recipe)
        {Recipe=recipe;Size=new Size(153,215);BackColor=Color.White;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.PushButton;AccessibleName=recipe.Name+(recipe.IsVideo?", video recipe":recipe.IsImported?", imported recipe":", local recipe");SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        // Sigle court affiché sur la vignette : initiales des mots du site.
        static string SiteTag(string site)
        {
            if(site=="FUJI X WEEKLY")return "FXW";
            if(site=="FILMSIM RECIPES")return "FSR";
            if(site=="FILM RECIPES")return "FLR";
            string tag="";foreach(string word in site.Split(' '))if(word!="")tag+=word.Substring(0,1);
            // Deux initiales se lisent mal : on préfère alors les trois premières lettres.
            if(tag.Length<3)tag=site.Replace(" ","").PadRight(3).Substring(0,3).Trim().ToUpperInvariant();
            return tag==""?"IMPORTED":tag;
        }
        public void Activate(){if(OpenRecipe!=null)OpenRecipe(Recipe);}
        public void ToggleFavorite(){if(FavoriteRequested!=null)FavoriteRequested(Recipe);Invalidate();}
        protected override void OnMouseEnter(EventArgs e){_hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){_hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseClick(MouseEventArgs e){if(e.X>=Width-39&&e.Y>=Height-38)ToggleFavorite();else Activate();base.OnMouseClick(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){Activate();e.Handled=true;}if(e.KeyCode==Keys.F){ToggleFavorite();e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;int imageHeight=Height-88;
            Theme.Round(g,ClientRectangle,Color.White,_hover?Color.FromArgb(172,184,173):Theme.Border,8);
            using(var path=Theme.Rounded(new RectangleF(1,1,Width-2,Height-2),7)){var state=g.Save();g.SetClip(path);Assets.Cover(g,Recipe.Cover,new Rectangle(1,1,Width-2,imageHeight));g.Restore(state);}
            Theme.TextAt(g,Recipe.Name,12,true,Theme.Text,new Rectangle(12,imageHeight+8,Width-21,23));
            Theme.TextAt(g,Recipe.Simulation,12,false,Theme.Text,new Rectangle(12,imageHeight+34,Width-22,20));
            string bottom=Recipe.IsVideo?Recipe.Get("Movie Mode"):Recipe.Get("Dynamic Range");
            // Le sigle du site vient de sa vraie source : plusieurs catalogues coexistent.
            if(Recipe.IsImported){string tag=SiteTag(Recipe.SourceSite);bottom=bottom=="Not specified"?tag:bottom+"  ·  "+tag;}
            Theme.TextAt(g,bottom,11,false,Recipe.IsImported?Theme.Muted:Theme.Text,new Rectangle(12,Height-33,Width-57,21));
            if(Recipe.IsVideo)
            {
                Theme.Round(g,new Rectangle(Width-53,7,46,21),Color.FromArgb(232,238,248),Color.FromArgb(232,238,248),6);
                Theme.TextAt(g,"VIDEO",9,true,Color.FromArgb(64,96,160),new Rectangle(Width-46,7,42,20));
            }
            Theme.Icon(g,"Heart",new Rectangle(Width-31,Height-31,17,17),Recipe.Favorite?Theme.Green:Theme.Muted);
            if(Recipe.Favorite)Theme.Dot(g,Width-26,Height-25,Theme.Green);
            bool incompatible=Recipe.IsImported&&Recipe.CompatStatus=="XT30_INCOMPATIBLE";
            if(incompatible){Theme.Round(g,new Rectangle(7,7,23,21),Color.FromArgb(250,228,226),Color.FromArgb(250,228,226),6);Theme.TextAt(g,"✕",12,true,Color.FromArgb(196,74,64),new Rectangle(14,7,14,20));}
            else if(Recipe.CompatibilityIssues().Count>0){Theme.Round(g,new Rectangle(7,7,23,21),Color.FromArgb(255,247,222),Color.FromArgb(255,247,222),6);Theme.TextAt(g,"!",13,true,Theme.Amber,new Rectangle(15,7,12,20));}
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-7,Height-7));
        }
    }
    public sealed class RecipeGrid : Panel
    {
        public bool SingleRow;
        public int DesiredCardWidth=190;
        readonly List<RecipeCard> _cards=new List<RecipeCard>();
        // Bibliothèque complète et nombre de vignettes réellement créées : avec un
        // millier de recettes, instancier autant de contrôles WinForms rendrait la
        // page inutilisable. On en affiche une page à la fois.
        readonly List<Recipe> _all=new List<Recipe>();
        public int PageSize=120;
        public event Action<Recipe> OpenRecipe;
        public event Action<Recipe> FavoriteRequested;
        public event EventHandler ShownCountChanged;
        public int ItemCount{get{return _cards.Count;}}
        public int TotalCount{get{return _all.Count;}}
        public bool HasMore{get{return _cards.Count<_all.Count;}}
        public RecipeGrid(){DoubleBuffered=true;BackColor=Color.White;}
        public void SetRecipes(IList<Recipe> recipes)
        {
            _all.Clear();foreach(Recipe r in recipes)_all.Add(r);
            SuspendLayout();foreach(RecipeCard card in _cards)card.Dispose();_cards.Clear();Controls.Clear();ResumeLayout(false);
            AddPage();
        }
        public void ShowMore(){AddPage();}
        void AddPage()
        {
            SuspendLayout();
            int target=SingleRow?_all.Count:Math.Min(_all.Count,_cards.Count+Math.Max(1,PageSize));
            for(int i=_cards.Count;i<target;i++)
            {
                RecipeCard card=new RecipeCard(_all[i]);
                card.OpenRecipe+=delegate(Recipe recipe){if(OpenRecipe!=null)OpenRecipe(recipe);};
                card.FavoriteRequested+=delegate(Recipe recipe){if(FavoriteRequested!=null)FavoriteRequested(recipe);};
                _cards.Add(card);Controls.Add(card);
            }
            ResumeLayout();PerformLayout();Invalidate();
            if(ShownCountChanged!=null)ShownCountChanged(this,EventArgs.Empty);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(_cards==null||_cards.Count==0)return;int columns=SingleRow?_cards.Count:Math.Max(1,(Width+16)/(DesiredCardWidth+16));
            int width=Math.Max(90,(Width-16*(columns-1))/columns);int height=SingleRow?215:Math.Min(305,(int)(width*.82)+88);
            for(int i=0;i<_cards.Count;i++)_cards[i].SetBounds((i%columns)*(width+16),(i/columns)*(height+22),width,height);
            if(!SingleRow)Height=((_cards.Count+columns-1)/columns)*(height+22);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(_cards.Count==0){Theme.TextAt(e.Graphics,Strings.T(EmptyTitle),20,true,Theme.Text,new Rectangle(20,35,Width-40,35));Theme.Lines(e.Graphics,Strings.T(EmptyHint),14,Theme.Muted,new Rectangle(20,72,Width-40,60));}}
        // Message affiché quand rien ne correspond : il dépend du filtre en cours,
        // « aucune recette » n'aide pas quand on vient de choisir Vidéo.
        public string EmptyTitle="No recipes found";
        public string EmptyHint="Try a different search or filter, or create a new recipe.";
    }
    public sealed class CompatibilityBadge : Control
    {
        public Recipe Recipe;
        public CompatibilityBadge(){Size=new Size(226,32);}
        protected override void OnPaint(PaintEventArgs e)
        {
            string text;Color fore,back;
            string status=Recipe==null?"":Recipe.IsImported?Recipe.CompatStatus:(Recipe.CompatibilityIssues().Count==0?"XT30_COMPATIBLE":"XT30_PARTIAL");
            switch(status)
            {
                case "XT30_COMPATIBLE":text="✓  X-T30 Compatible";fore=Theme.Green;back=Color.FromArgb(234,244,234);break;
                case "XT30_INCOMPATIBLE":text="✕  Incompatible with X-T30";fore=Color.FromArgb(196,74,64);back=Color.FromArgb(250,228,226);break;
                case "UNVERIFIED":text="?  Compatibility unverified";fore=Theme.Muted;back=Color.FromArgb(238,238,240);break;
                default:text="!  Partially compatible with X-T30";fore=Theme.Amber;back=Color.FromArgb(255,247,223);break;
            }
            Theme.Round(e.Graphics,ClientRectangle,back,back,7);
            Theme.TextAt(e.Graphics,Strings.T(text),12,true,fore,new Rectangle(12,0,Width-20,Height));
        }
    }
    public sealed class RecentRecipes : RoundedCard
    {
        public readonly RecipeGrid Grid=new RecipeGrid(){SingleRow=true};
        public readonly ActionButton ViewAll=new ActionButton("View all",false){Quiet=true,ForeColor=Theme.Green};
        public RecentRecipes(){Controls.Add(Grid);Controls.Add(ViewAll);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ViewAll.SetBounds(Width-84,14,70,31);Grid.SetBounds(20,61,Width-44,215);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.TextAt(e.Graphics,Strings.T("Recent Recipes"),15,true,Theme.Text,new Rectangle(21,14,190,30));Theme.TextAt(e.Graphics,Strings.T("LOCAL LIBRARY"),9,false,Theme.Muted,new Rectangle(173,17,112,26));}
    }
}
