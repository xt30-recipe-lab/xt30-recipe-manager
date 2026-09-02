using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xt30Probe.Presentation
{
    // Didacticiel d'accueil : six étapes qui suivent l'ordre réel d'utilisation,
    // de la mise en mode USB jusqu'à l'envoi des sept banques. Il ne déclenche
    // aucune action sur l'appareil : il explique, il ne fait rien à votre place.
    public sealed class TutorialForm : Form
    {
        sealed class Step
        {
            public string Icon, Title, Body;
            public Step(string icon, string title, string body) { Icon = icon; Title = title; Body = body; }
        }

        static readonly Step[] Steps = {
            new Step("Book", "Welcome to XT30 Recipe Manager",
                "This application keeps your film recipes on this computer and loads them into the C1-C7 custom banks of your Fujifilm X-T30.\n\n"
                + "It never sends a write command to the camera. When you send recipes, the file is written by Fujifilm's own Tether App, exactly as if you had loaded it yourself."),
            new Step("Camera", "1 · Put the camera in the right USB mode",
                "On the camera:\n\nMENU → SET UP → CONNECTION SETTING → USB CONNECTION MODE → USB RAW CONV./BACKUP RESTORE\n\n"
                + "Then turn the camera off and on again, and connect it with the USB cable. Without this mode the camera behaves like a card reader and its settings stay out of reach."),
            new Step("Refresh", "2 · Read your camera",
                "Open Camera Slots and press \"Read my camera\".\n\n"
                + "The seven banks are copied from the camera and decoded. Only what the camera actually stores is displayed; anything it does not store stays marked as not specified, never guessed."),
            new Step("Recipes", "3 · Find or create a recipe",
                "The Recipes page holds your own recipes plus the imported catalogues, filtered by film simulation, category, compatibility or favourites.\n\n"
                + "\"New Recipe\" creates your own. Only values your X-T30 really accepts are offered, so a recipe made here is always transferable. Choose Photo for the C1-C7 banks, or Video for the movie mode."),
            new Step("Folder", "4 · Load recipes into C1-C7",
                "In Camera Slots, press \"Load recipes into C1-C7…\", pick a recipe per bank and send.\n\n"
                + "All seven banks travel in a single file, so the camera is updated in one restore. Banks you leave unchanged, and every other camera setting, are untouched."),
            new Step("Lock", "5 · What the camera cannot store",
                "A few values do not travel: the white balance shift, which the camera does not keep per bank, and settings whose position in the settings file has not been identified yet, such as ISO.\n\n"
                + "The application never invents them: it lists them after each send so you can set them in the camera menu. The white balance shift is written into the bank name instead, which is why names look like \"PACIFIC R+1 B-3\".\n\n"
                + "Keep your original settings file: it restores the camera exactly as it was."),
        };

        readonly ActionButton _back = new ActionButton("Back", false);
        readonly ActionButton _next = new ActionButton("Next", true);
        readonly ActionButton _skip = new ActionButton("Skip", false) { Quiet = true };
        readonly CheckBox _never = new CheckBox();
        int _index;

        // true si l'utilisateur a demandé à ne plus voir le didacticiel au démarrage.
        public bool Suppress { get { return _never.Checked; } }

        public TutorialForm(bool showSuppressBox)
        {
            Text = Strings.T("Tutorial");
            BackColor = Color.White; Font = Theme.Font(14, false);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(780, 520); MinimumSize = new Size(700, 480);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            DoubleBuffered = true;

            Controls.AddRange(new Control[] { _back, _next, _skip, _never });
            _never.Text = Strings.T("Do not show this again");
            _never.Font = Theme.Font(12, false); _never.ForeColor = Theme.Muted;
            _never.Visible = showSuppressBox; _never.Checked = showSuppressBox;
            _skip.ForeColor = Theme.Muted;
            _back.Click += delegate { Advance(-1); };
            _next.Click += delegate { Advance(1); };
            _skip.Click += delegate { Close(); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter) { Advance(1); e.Handled = true; }
                else if (e.KeyCode == Keys.Left) { Advance(-1); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) Close();
            };
            UpdateButtons();
        }

        public static int StepCount { get { return Steps.Length; } }
        // Utilisé par la validation hors ligne pour rendre chaque étape.
        public void GoToStep(int index)
        { _index = Math.Max(0, Math.Min(Steps.Length - 1, index)); UpdateButtons(); Invalidate(); }

        void Advance(int direction)
        {
            if (_index + direction >= Steps.Length) { Close(); return; }
            _index = Math.Max(0, Math.Min(Steps.Length - 1, _index + direction));
            UpdateButtons(); Invalidate();
        }

        void UpdateButtons()
        {
            _back.Enabled = _index > 0;
            _next.Text = _index == Steps.Length - 1 ? "Finish" : "Next";
            _skip.Visible = _index < Steps.Length - 1;
            _next.Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_back == null) return;
            int bottom = ClientSize.Height - 58;
            _skip.SetBounds(26, bottom, 96, 40);
            _next.SetBounds(ClientSize.Width - 156, bottom, 130, 40);
            _back.SetBounds(ClientSize.Width - 296, bottom, 130, 40);
            _never.SetBounds(150, bottom + 10, 230, 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Step step = Steps[_index];

            // Bandeau d'illustration : le vert Fujifilm sert de repère d'étape.
            Rectangle banner = new Rectangle(0, 0, ClientSize.Width, 118);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(240, 245, 240))) g.FillRectangle(brush, banner);
            using (Pen pen = new Pen(Color.FromArgb(224, 233, 225))) g.DrawLine(pen, 0, banner.Bottom, banner.Right, banner.Bottom);
            Theme.Round(g, new Rectangle(30, 29, 60, 60), Color.White, Color.FromArgb(214, 227, 216), 12);
            Theme.Icon(g, step.Icon, new Rectangle(46, 45, 28, 28), Theme.Green);
            Theme.TextAt(g, Strings.T(step.Title), 21, true, Theme.Text, new Rectangle(110, 32, ClientSize.Width - 140, 32));
            Theme.TextAt(g, Strings.T("Step {0} of {1}", _index + 1, Steps.Length), 12, false, Theme.Muted, new Rectangle(111, 64, 300, 24));

            Theme.Lines(g, Strings.T(step.Body), 14, Theme.Text, new Rectangle(31, 146, ClientSize.Width - 62, ClientSize.Height - 232));

            // Pastilles de progression
            int dots = Steps.Length, x = ClientSize.Width / 2 - (dots * 16) / 2, y = ClientSize.Height - 84;
            for (int i = 0; i < dots; i++)
                using (SolidBrush brush = new SolidBrush(i == _index ? Theme.Green : Color.FromArgb(216, 218, 216)))
                    g.FillEllipse(brush, x + i * 16, y, 9, 9);
        }
    }
}
