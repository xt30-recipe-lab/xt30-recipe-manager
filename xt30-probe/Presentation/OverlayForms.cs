using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Xt30Probe.Presentation
{
    // Écran d'ouverture : affiché pendant le chargement de la bibliothèque, qui
    // peut représenter plusieurs centaines de recettes et leurs vignettes.
    public sealed class SplashForm : Form
    {
        string _status = "";
        int _phase;
        // Une pastille s'allume par étape franchie : les sept renvoient aux banques
        // C1-C7, qui sont la raison d'être de l'application.
        int _stepsDone;
        readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer() { Interval = 30 };
        readonly DateTime _openedAt = DateTime.Now;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 380);
            BackColor = Color.White;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Opacity = 0;
            _timer.Tick += delegate
            {
                _phase = (_phase + 1) % 300;
                // Fondu d'ouverture : la fenêtre ne surgit pas d'un coup.
                if (Opacity < 1) Opacity = Math.Min(1, Opacity + 0.12);
                Invalidate();
            };
            _timer.Start();
        }

        // Le message et l'animation avancent pendant un chargement synchrone :
        // sans pompage explicite, la fenêtre resterait figée.
        public void Report(string status)
        {
            _status = status;
            if (_stepsDone < 7) _stepsDone++;
            Refresh();
            Application.DoEvents();
        }

        // Un chargement rapide ne doit pas faire clignoter l'écran d'ouverture.
        public void CloseAfterMinimumTime(int milliseconds)
        {
            while ((DateTime.Now - _openedAt).TotalMilliseconds < milliseconds)
            { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            for (int i = 0; i < 8 && Opacity > 0.05; i++)
            { Opacity = Math.Max(0, Opacity - 0.15); Application.DoEvents(); System.Threading.Thread.Sleep(16); }
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int w = ClientSize.Width, h = ClientSize.Height;

            // Fond : blanc en haut, très légèrement chaud en bas.
            using (System.Drawing.Drawing2D.LinearGradientBrush brush =
                new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h),
                    Color.White, Color.FromArgb(246, 247, 245), 90f))
                g.FillRectangle(brush, 0, 0, w, h);

            // Bande verte discrète en haut, signature de l'application.
            using (SolidBrush brush = new SolidBrush(Theme.Green)) g.FillRectangle(brush, 0, 0, w, 4);
            using (Pen pen = new Pen(Color.FromArgb(226, 228, 225))) g.DrawRectangle(pen, 0, 0, w - 1, h - 1);

            // Motif d'ouverture : un obturateur qui tourne lentement derrière le logo.
            DrawShutter(g, new Point(w / 2, 116), 58, _phase);

            Assets.Wordmark(g, new Rectangle(w / 2 - 175, 202, 350, 50));

            using (SolidBrush brush = new SolidBrush(Theme.Muted))
            using (StringFormat format = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(_status, Theme.Font(12, false), brush, new Rectangle(40, 258, w - 80, 22), format);
                g.DrawString("FUJIFILM X-T30  ·  " + Strings.T("READ ONLY"), Theme.Font(10, false), brush,
                    new Rectangle(40, h - 38, w - 80, 18), format);
            }

            // Sept pastilles : une par banque, allumées au fil des étapes.
            int dots = 7, gap = 22, x0 = w / 2 - (dots * gap) / 2 + (gap - 9) / 2;
            for (int i = 0; i < dots; i++)
            {
                bool on = i < _stepsDone;
                // La pastille suivante respire pendant l'attente.
                bool pulse = i == _stepsDone && (_phase % 60) < 30;
                Color color = on ? Theme.Green : pulse ? Color.FromArgb(186, 208, 190) : Color.FromArgb(224, 226, 223);
                using (SolidBrush brush = new SolidBrush(color)) g.FillEllipse(brush, x0 + i * gap, 300, 9, 9);
            }
        }

        // Douze lames disposées en couronne : lisible même en petit, et sans image.
        static void DrawShutter(Graphics g, Point center, int radius, int phase)
        {
            float angle = phase * 0.6f;
            for (int i = 0; i < 12; i++)
            {
                double a = (angle + i * 30) * Math.PI / 180.0;
                int x = center.X + (int)(Math.Cos(a) * radius);
                int y = center.Y + (int)(Math.Sin(a) * radius);
                int size = 7 - i % 3;
                int alpha = 26 + (i * 9) % 60;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, Theme.Green)))
                    g.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
            }
            using (Pen pen = new Pen(Color.FromArgb(30, Theme.Green))) g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        }

        protected override void Dispose(bool disposing)
        { if (disposing) { _timer.Stop(); _timer.Dispose(); } base.Dispose(disposing); }
    }

    // Fenêtre d'attente pour les opérations longues sur l'appareil. Le travail est
    // exécuté sur un fil séparé pour que l'animation et le message restent vivants ;
    // le fil ne touche à aucun contrôle, il ne fait qu'appeler des outils externes.
    public sealed class ProgressForm : Form
    {
        readonly string _title;
        string _status;
        int _phase;
        readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer() { Interval = 45 };

        ProgressForm(string title, string status)
        {
            _title = title; _status = status;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 176);
            BackColor = Color.White;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            ControlBox = false;
            _timer.Tick += delegate { _phase = (_phase + 1) % 240; Invalidate(); };
            _timer.Start();
        }

        void Report(string status)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { Report(status); }); return; }
            _status = status; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            Theme.TextAt(g, _title, 17, true, Theme.Text, new Rectangle(28, 30, ClientSize.Width - 56, 28));
            Theme.Lines(g, _status, 12, Theme.Muted, new Rectangle(29, 64, ClientSize.Width - 58, 44));
            int trackX = 28, trackW = ClientSize.Width - 56, y = 128;
            Theme.Round(g, new Rectangle(trackX, y, trackW, 4), Color.FromArgb(238, 238, 237), Color.FromArgb(238, 238, 237), 2);
            int width = trackW / 4, travel = trackW + width;
            int x = trackX - width + (_phase * travel) / 240;
            Rectangle bar = Rectangle.Intersect(new Rectangle(x, y, width, 4), new Rectangle(trackX, y, trackW, 4));
            if (bar.Width > 1) Theme.Round(g, bar, Theme.Green, Theme.Green, 2);
        }

        // Exécute `work` en arrière-plan derrière cette fenêtre et renvoie son
        // résultat. `work` reçoit un rapporteur pour changer le message affiché.
        public static T Run<T>(IWin32Window owner, string title, string status, Func<Action<string>, T> work)
        {
            T result = default(T);
            Exception error = null;
            using (ProgressForm form = new ProgressForm(title, status))
            {
                Action<string> report = form.Report;
                Thread worker = new Thread(delegate()
                {
                    try { result = work(report); }
                    catch (Exception ex) { error = ex; }
                    finally
                    {
                        try { form.BeginInvoke((MethodInvoker)delegate { form.Close(); }); }
                        catch (Exception) { }
                    }
                });
                worker.IsBackground = true;
                form.Shown += delegate { worker.Start(); };
                form.ShowDialog(owner);
            }
            if (error != null) throw error;
            return result;
        }

        protected override void Dispose(bool disposing)
        { if (disposing) { _timer.Stop(); _timer.Dispose(); } base.Dispose(disposing); }
    }
}
