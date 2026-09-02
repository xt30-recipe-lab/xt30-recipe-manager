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
        {Recipe=recipe;Size=new Size(153,215);BackColor=Color.White;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.PushButton;AccessibleName=recipe.Name+", local recipe";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
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
            string bottom=Recipe.Get("Dynamic Range");
            if(Recipe.IsImported)bottom=bottom=="Not specified"?"FXW":bottom+"  ·  FXW";
            Theme.TextAt(g,bottom,11,false,Recipe.IsImported?Theme.Muted:Theme.Text,new Rectangle(12,Height-33,Width-57,21));
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
        public event Action<Recipe> OpenRecipe;
        public event Action<Recipe> FavoriteRequested;
        public int ItemCount{get{return _cards.Count;}}
        public RecipeGrid(){DoubleBuffered=true;BackColor=Color.White;}
        public void SetRecipes(IList<Recipe> recipes)
        {
            SuspendLayout();foreach(RecipeCard card in _cards)card.Dispose();_cards.Clear();Controls.Clear();
            foreach(Recipe r in recipes){RecipeCard card=new RecipeCard(r);card.OpenRecipe+=delegate(Recipe recipe){if(OpenRecipe!=null)OpenRecipe(recipe);};card.FavoriteRequested+=delegate(Recipe recipe){if(FavoriteRequested!=null)FavoriteRequested(recipe);};_cards.Add(card);Controls.Add(card);}
            ResumeLayout();PerformLayout();Invalidate();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(_cards==null||_cards.Count==0)return;int columns=SingleRow?_cards.Count:Math.Max(1,(Width+16)/(DesiredCardWidth+16));
            int width=Math.Max(90,(Width-16*(columns-1))/columns);int height=SingleRow?215:Math.Min(305,(int)(width*.82)+88);
            for(int i=0;i<_cards.Count;i++)_cards[i].SetBounds((i%columns)*(width+16),(i/columns)*(height+22),width,height);
            if(!SingleRow)Height=((_cards.Count+columns-1)/columns)*(height+22);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(_cards.Count==0){Theme.TextAt(e.Graphics,"No recipes found",20,true,Theme.Text,new Rectangle(20,35,Width-40,35));Theme.TextAt(e.Graphics,"Try a different search or filter, or create a new recipe.",14,false,Theme.Muted,new Rectangle(20,75,Width-40,30));}}
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
            Theme.TextAt(e.Graphics,text,12,true,fore,new Rectangle(12,0,Width-20,Height));
        }
    }
    public sealed class RecentRecipes : RoundedCard
    {
        public readonly RecipeGrid Grid=new RecipeGrid(){SingleRow=true};
        public readonly ActionButton ViewAll=new ActionButton("View all",false){Quiet=true,ForeColor=Theme.Green};
        public RecentRecipes(){Controls.Add(Grid);Controls.Add(ViewAll);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ViewAll.SetBounds(Width-84,14,70,31);Grid.SetBounds(20,61,Width-44,215);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Theme.TextAt(e.Graphics,"Recent Recipes",15,true,Theme.Text,new Rectangle(21,14,190,30));Theme.TextAt(e.Graphics,"LOCAL LIBRARY",9,false,Theme.Muted,new Rectangle(173,17,112,26));}
    }
}
