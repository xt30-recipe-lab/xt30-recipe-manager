using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Xt30Probe.Presentation
{
    // Native window movement only. This module has no camera/USB responsibilities.
    public sealed class WindowTitleBar : Panel
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        readonly ActionButton _min=new ActionButton("",false){Quiet=true,IconName="Minimize",TabStop=false};
        readonly ActionButton _max=new ActionButton("",false){Quiet=true,IconName="Maximize",TabStop=false};
        readonly ActionButton _close=new ActionButton("",false){Quiet=true,IconName="Close",TabStop=false};
        public WindowTitleBar()
        {
            BackColor=Color.FromArgb(248,249,250);DoubleBuffered=true;Controls.AddRange(new Control[]{_min,_max,_close});
            _min.AccessibleName="Minimize";_max.AccessibleName="Maximize or restore";_close.AccessibleName="Close";
            _min.Click+=delegate{FindForm().WindowState=FormWindowState.Minimized;};_max.Click+=delegate{ToggleMaximize();};_close.Click+=delegate{FindForm().Close();};
        }
        void ToggleMaximize(){MainForm form=FindForm() as MainForm;if(form!=null)form.ToggleMaximizeWindow();}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);_min.SetBounds(Width-165,1,55,42);_max.SetBounds(Width-110,1,55,42);_close.SetBounds(Width-55,1,55,42);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(FindForm().Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
        protected override void OnDoubleClick(EventArgs e){ToggleMaximize();base.OnDoubleClick(e);}
        protected override void OnPaint(PaintEventArgs e)
        {base.OnPaint(e);Assets.Logo(e.Graphics,new Rectangle(22,11,22,22));Theme.TextAt(e.Graphics,"XT30 Recipe Manager",14,false,Theme.Text,new Rectangle(52,0,Width-235,Height));using(Pen p=new Pen(Theme.Border))e.Graphics.DrawLine(p,0,Height-1,Width,Height-1);}
    }
}
