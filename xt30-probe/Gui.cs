using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Xt30Probe.Presentation;

namespace Xt30Probe
{
    public static class GuiProgram
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string title);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException+=delegate(object s,UnhandledExceptionEventArgs e){Program.LogCrash(e.ExceptionObject);};
            bool smoke=args.Length>0&&args[0]=="--ui-smoke";
            bool capture=args.Length>0&&args[0]=="--ui-capture";
            if(args.Length>0&&!smoke&&!capture)return Program.Main(args);
            Mutex instance=null;bool ownsInstance=false;
            // One live GUI prevents competing scans and a locked probe-session.log.
            // Offline validation never opens the engine and may run independently.
            if(!smoke)
            {
                instance=new Mutex(true,"Local\\XT30RecipeManager.Gui",out ownsInstance);
                if(!ownsInstance)
                {
                    IntPtr window=FindWindow(null,"XT30 Recipe Manager");
                    if(window!=IntPtr.Zero){ShowWindow(window,9);SetForegroundWindow(window);}
                    instance.Dispose();return 0;
                }
            }
            Application.ThreadException+=delegate(object s,System.Threading.ThreadExceptionEventArgs e){Program.LogCrash(e.Exception);if(smoke||capture)Environment.Exit(1);MessageBox.Show("Erreur interne (voir crash.log) : "+e.Exception.Message,"XT30 Recipe Manager");};
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Assets.Initialize();
                if(smoke)return UiValidation.Run();
                using(MainForm form=new MainForm(false,null))
                {
                    if(capture)UiValidation.ConfigureLiveCapture(form,args);
                    Application.Run(form);
                }
                return 0;
            }
            catch(Exception ex){Program.LogCrash(ex);if(!smoke&&!capture)MessageBox.Show(ex.Message,"XT30 Recipe Manager could not start");return 1;}
            finally{Assets.Dispose();if(instance!=null){if(ownsInstance)instance.ReleaseMutex();instance.Dispose();}}
        }
    }
}
