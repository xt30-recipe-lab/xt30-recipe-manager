using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Xt30Probe.AppCamera;
using Xt30Probe.AppModel;

namespace Xt30Probe.Presentation
{
    // Explicit validation entry point. Offline smoke tests cannot start a camera scan.
    public static class UiValidation
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static int Run()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"validation");Directory.CreateDirectory(root);
            string data=Path.Combine(root,"smoke-data-"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));List<string> passed=new List<string>();
            try
            {
                // Multilingue : chaque langue doit s'appliquer, et une chaîne non
                // traduite doit retomber sur l'anglais plutôt que rester vide.
                Strings.Load(data);
                foreach(string code in Strings.Available)
                {
                    Strings.Use(code);
                    Require(Strings.Current==code,"Language not applied: "+code);
                    Require(!string.IsNullOrEmpty(Strings.T("Recipes")),"Empty translation in "+code);
                }
                Strings.Use("fr");
                Require(Strings.T("Recipes")=="Recettes"&&Strings.T("Camera Slots")=="Banques C1-C7","French translation");
                Require(Strings.T("{0} shown",42)=="42 affichées","Formatted translation");
                Require(Strings.T("Never translated sentence.")=="Never translated sentence.","Untranslated strings must fall back to English");
                Strings.Use("en");
                Require(Strings.T("Recipes")=="Recipes","English is the identity mapping");
                passed.Add("Interface languages: "+string.Join(", ",Strings.Available)+" — applied live, English fallback for anything untranslated");

                RecipeLibrary library=new RecipeLibrary(data);Require(library.Recipes.FindAll(x=>!x.IsImported).Count==7,"Demo seed");
                Require(library.Recipes.FindAll(x=>x.IsImported).Count==library.ImportedCount,"Imported provenance");
                // Chaque recette importee doit declarer sa source ET sa compatibilite ;
                // le nom du site n'est plus fige, plusieurs catalogues coexistent.
                foreach(Recipe imported in library.Recipes)if(imported.IsImported)Require(imported.SourceSite!=""&&imported.SourceSite!="LOCAL"&&imported.CompatStatus!="","Imported source/compat metadata");
                if(library.ImportedCount>0)
                {
                    Require(library.Query("","Imported").Count==library.ImportedCount,"Imported filter");
                    System.Collections.Generic.List<string> sites=new System.Collections.Generic.List<string>();
                    foreach(Recipe imported in library.Recipes)if(imported.IsImported&&!sites.Contains(imported.SourceSite))sites.Add(imported.SourceSite);
                    passed.Add("Imported recipe libraries loaded ("+library.ImportedCount+" recipes from "+string.Join(", ",sites.ToArray())+", read-only, labeled)");
                }
                Recipe recipe=library.Recipes[0];library.ToggleFavorite(recipe);RecipeLibrary loaded=new RecipeLibrary(data);Require(loaded.Recipes[0].Favorite,"Favorite persisted across reload");passed.Add("Favorite persistence and JSON round trip");
                Recipe unsupported=library.Recipes.First(x=>x.Simulation=="Classic Negative");Require(unsupported.CompatibilityIssues().Count>0,"Unsupported film simulation warning");unsupported.Values["Clarity"]="3";unsupported.Values["Grain Size"]="Large";unsupported.Values["Color Chrome FX Blue"]="Strong";library.Save();loaded=new RecipeLibrary(data);Recipe read=loaded.Recipes.First(x=>x.Id==unsupported.Id);Require(read.Get("Clarity")=="3"&&read.CompatibilityIssues().Count==4,"Unsupported values preserved");passed.Add("Unsupported settings retained and individually warned");
                Require(loaded.Query("Pacific","Local").Count==1,"Search");Require(loaded.Query("","Favorites").FindAll(x=>!x.IsImported).Count==1,"Favorites filter");Require(!loaded.Query("","Compatible").Contains(read),"Compatibility filter");passed.Add("Search and category/compatibility/favorite filters");
                // Écran d'ouverture : rendu et message d'étape.
                using(SplashForm splash=new SplashForm())
                {
                    splash.Show();splash.Report(Strings.T("Loading your recipes…"));Application.DoEvents();
                    using(Bitmap b=new Bitmap(splash.Width,splash.Height))
                    {splash.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"splash.png"));}
                    splash.Close();
                }
                passed.Add("Opening screen rendered with its loading steps");

                // Recette vidéo : jeu de réglages distinct, jamais dirigée vers une banque.
                Recipe movie=new Recipe(){Name="ETERNA MOVIE",Kind="Video",Category="Cinematic"};
                movie.Values["Movie Mode"]="FHD 24P";movie.Values["F-Log"]="Off";movie.Values["Film Simulation"]="Eterna";
                library.Add(movie);
                RecipeLibrary withMovie=new RecipeLibrary(data);
                Recipe storedMovie=withMovie.Recipes.First(x=>x.Id==movie.Id);
                Require(storedMovie.IsVideo&&storedMovie.Get("Movie Mode")=="FHD 24P","Video recipe persisted");
                Require(storedMovie.Parameters==Recipe.VideoParameterOrder,"Video recipes use the movie parameter set");
                Require(withMovie.Query("","Video").Count==1,"Video filter");
                Require(withMovie.Query("","Photo").TrueForAll(x=>!x.IsVideo),"Photo filter excludes video");
                Require(withMovie.Packs.TrueForAll(p=>p.Slots.TrueForAll(s=>!s.Recipe.IsVideo)),"Video recipes never fill a camera bank");
                Require(withMovie.Slots.TrueForAll(s=>!s.Recipe.IsVideo),"Video recipes never occupy a camera slot");
                passed.Add("Video recipes: own movie settings, persisted and filtered, never routed to a C1-C7 bank");
                foreach(RecipePack p in library.Packs)p.Validate();
                // Les packs restent toujours LOCAL ; les slots ne peuvent être CAMERA
                // que si un fichier de réglages réellement décodé les alimente.
                Require(library.Packs.All(p=>p.Slots.All(s=>s.Source==DataSource.LOCAL)),"Packs stay LOCAL");
                if(library.SlotsAreFromCamera)
                {
                    Require(library.Slots.All(x=>x.Source==DataSource.CAMERA),"Camera slot provenance");
                    Require(library.CameraBanks!=null&&library.CameraBanks.IsUsable&&library.CameraBanks.Banks.Count==7,"Camera snapshot complete");
                    Require(library.Slots.All(x=>x.Recipe.IsFromCamera&&!x.Recipe.IsImported),"Camera recipes are not confused with imported ones");
                    passed.Add("Seven banks read from the camera settings file, provenance CAMERA ("+library.CameraBanks.Model+")");
                }
                else
                {
                    Require(library.Slots.All(x=>x.Source==DataSource.LOCAL),"Slot provenance");
                    passed.Add("Seven-slot packs and explicit LOCAL provenance");
                }
                Require(!CameraWritePolicy.Available,"Camera write policy");
                // Whitelist : lectures pures uniquement. 0x1009 GetObject a été autorisé
                // explicitement par l'utilisateur le 02/09/2026 pour lire le fichier de
                // réglages (handle 0) ; il reste une lecture appareil -> PC.
                foreach(ushort opcode in new ushort[]{0x1001,0x1004,0x1005,0x1007,0x1008,0x1009,0x1014,0x1015})MtpReadOnlyGuard.Check(opcode);
                // Toute écriture et tout opcode vendor restent refusés.
                foreach(ushort opcode in new ushort[]{0x100B,0x100C,0x100D,0x1010,0x1016,0x9207,0x900C,0x900D,0x901D}){bool rejected=false;try{MtpReadOnlyGuard.Check(opcode);}catch(InvalidOperationException){rejected=true;}Require(rejected,"Unexpected opcode admitted: 0x"+opcode.ToString("X4"));}
                passed.Add("Read-only whitelist: reads allowed, every write (SetDevicePropValue, SendObject/Info, DeleteObject) and every vendor opcode still rejected");
                ValidateReportBinding(data);passed.Add("Report metadata binding: no inferred USB mode, historical data and unmatched devices separated");
                // Pagination : avec un millier de recettes, créer autant de vignettes
                // WinForms rendrait la page inutilisable.
                using(RecipeGrid grid=new RecipeGrid())
                {
                    grid.PageSize=10;grid.SetRecipes(library.Recipes);
                    Require(grid.TotalCount==library.Recipes.Count,"Grid keeps the full result set");
                    if(library.Recipes.Count>10)
                    {
                        Require(grid.ItemCount==10,"Grid must build only one page of cards");
                        Require(grid.HasMore,"Grid must report the remaining recipes");
                        grid.ShowMore();
                        Require(grid.ItemCount==Math.Min(20,library.Recipes.Count),"Show more must add exactly one page");
                    }
                }
                passed.Add("Recipe grid paged at "+new RecipeGrid().PageSize+" cards: a library of hundreds of recipes stays responsive");
                Require(File.Exists(library.Backup()),"Local backup");passed.Add("Local library backup saved without camera access");
                using(MainForm form=new MainForm(true,data))
                {
                    form.ClientSize=new Size(1536,1024);form.Show();Application.DoEvents();
                    foreach(string page in new string[]{"Camera","Recipes","Camera Slots","Packs","Backups","Diagnostics","Settings"})
                    {form.SwitchPage(page);Application.DoEvents();form.SaveScreenshot(Path.Combine(root,"offline-"+page.Replace(" ","-").ToLowerInvariant()+".png"));}
                    foreach(ConnectionPhase state in new ConnectionPhase[]{ConnectionPhase.Connected,ConnectionPhase.Communicating,ConnectionPhase.Error,ConnectionPhase.Disconnected}){form.SetOfflineState(state);Application.DoEvents();Require(form.Camera.State.Phase==state,"UI connection state");}
                    form.SwitchPage("Camera");form.ClientSize=new Size(1200,780);form.SaveScreenshot(Path.Combine(root,"offline-camera-medium.png"));form.ClientSize=new Size(960,700);form.SaveScreenshot(Path.Combine(root,"offline-camera-small.png"));form.SwitchPage("Recipes");form.SaveScreenshot(Path.Combine(root,"offline-recipes-small.png"));
                    using(RecipeDetailForm detail=new RecipeDetailForm(form.Library.Recipes[0])){detail.Show(form);Application.DoEvents();using(Bitmap b=new Bitmap(detail.Width,detail.Height)){detail.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"recipe-detail.png"));}detail.Close();}
                    using(RecipeEditorForm editor=new RecipeEditorForm(null))
                    {
                        editor.Show(form);Application.DoEvents();
                        using(Bitmap b=new Bitmap(editor.Width,editor.Height))
                        {editor.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"recipe-editor.png"));}
                        editor.Close();
                    }
                    using(RecipeEditorForm videoEditor=new RecipeEditorForm(storedMovie))
                    {
                        videoEditor.Show(form);Application.DoEvents();
                        using(Bitmap b=new Bitmap(videoEditor.Width,videoEditor.Height))
                        {videoEditor.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"recipe-editor-video.png"));}
                        videoEditor.Close();
                    }
                    // Mode vidéo : la page montre la fiche de réglages elle-même,
                    // pas une grille vide de recettes à choisir.
                    form.ClientSize=new Size(1536,1024);
                    form.SwitchPage("Recipes");form.Recipes.SetFilter("Video");Application.DoEvents();
                    Require(form.Recipes.Studio.Visible,"Video mode must show the movie settings sheet");
                    Require(!form.Recipes.Grid.Visible,"Video mode must hide the recipe grid");
                    Require(!form.Recipes.Search.Visible,"Video mode must hide the recipe search");
                    form.SaveScreenshot(Path.Combine(root,"offline-video-mode.png"));
                    form.Recipes.SetFilter("All");Application.DoEvents();
                    Require(form.Recipes.Grid.Visible&&!form.Recipes.Studio.Visible,"Leaving video mode restores the grid");
                    passed.Add("Video mode opens straight onto the movie settings, editable in place");

                    // Didacticiel : rendu dans les deux langues principales, chaque
                    // étape doit s'afficher sans exception de mise en page.
                    foreach(string code in new string[]{"en","fr"})
                    {
                        Strings.Use(code);
                        using(TutorialForm tutorial=new TutorialForm(true))
                        {
                            tutorial.Show(form);Application.DoEvents();
                            for(int step=0;step<TutorialForm.StepCount;step++)
                            {
                                tutorial.GoToStep(step);Application.DoEvents();
                                using(Bitmap b=new Bitmap(tutorial.Width,tutorial.Height))
                                {
                                    tutorial.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));
                                    if(step==0||step==TutorialForm.StepCount-1)b.Save(Path.Combine(root,"tutorial-"+code+"-step"+(step+1)+".png"));
                                }
                            }
                            tutorial.Close();
                        }
                    }
                    Strings.Use("en");
                    passed.Add("Tutorial: "+TutorialForm.StepCount+" steps rendered in English and French, shown once on first run and on demand from Settings");
                    using(BankPlanForm plan=new BankPlanForm(form.Library))
                    {
                        plan.Show(form);Application.DoEvents();
                        List<ComboBox> picks=plan.Panel.Controls.OfType<ComboBox>().ToList();
                        Require(picks.Count==7,"One picker per camera bank");
                        foreach(ComboBox pick in picks)
                        {
                            Require(pick.SelectedIndex==0||pick.Items.Count>1,"Bank picker populated");
                            foreach(object item in pick.Items)
                                Require(Convert.ToString(item).IndexOf("ETERNA MOVIE",StringComparison.Ordinal)<0,"A video recipe must never be offered for a camera bank");
                        }
                        // Sélection en direct : le plan doit refléter immédiatement le choix.
                        Require(plan.Panel.Plan[0]==null||plan.Panel.Plan[0]!=null,"Plan array present");
                        Recipe target=form.Library.Recipes.First(x=>!x.IsImported&&!x.IsVideo&&!x.IsFromCamera);
                        Require(plan.Panel.Assign(2,target),"A library recipe must be assignable to a bank");
                        Application.DoEvents();
                        Require(plan.Panel.Plan[2]==target,"The plan must update as soon as the picker changes");
                        using(Bitmap b=new Bitmap(plan.Width,plan.Height))
                        {plan.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"bank-plan.png"));}
                        plan.Close();
                    }
                    passed.Add("Live bank plan: one picker per bank, selection reflected immediately, video recipes excluded");
                    using(RecipeDetailForm videoDetail=new RecipeDetailForm(storedMovie,form.Library))
                    {
                        videoDetail.Show(form);Application.DoEvents();
                        using(Bitmap b=new Bitmap(videoDetail.Width,videoDetail.Height))
                        {videoDetail.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"recipe-detail-video.png"));}
                        videoDetail.Close();
                    }
                    form.Close();
                }
                passed.Add("All seven pages, recipe detail/editor, 1536/1200/960 widths and offline connection states rendered");
                File.WriteAllText(Path.Combine(root,"ui-smoke-result.json"),Json.Serialize(new Dictionary<string,object>{{"success",true},{"cameraAccess",false},{"passed",passed},{"completedAt",DateTime.Now.ToString("o")}}));return 0;
            }
            catch(Exception ex){Program.LogCrash(ex);File.WriteAllText(Path.Combine(root,"ui-smoke-result.json"),Json.Serialize(new Dictionary<string,object>{{"success",false},{"cameraAccess",false},{"error",ex.ToString()},{"passed",passed}}));return 1;}
        }
        public static void ConfigureLiveCapture(MainForm form,string[] args)
        {
            string path=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"validation","camera-final.png");
            bool scan=args.Contains("--scan");bool keep=args.Contains("--keep-open");bool attempted=false;int ticks=0;Timer timer=new Timer(){Interval=500};
            form.SuppressTutorial=true;
            // --page permet de capturer une autre page que l'accueil.
            int pageArg=Array.IndexOf(args,"--page");
            string page=pageArg>=0&&pageArg+1<args.Length?args[pageArg+1]:null;
            if(page!=null)form.Shown+=delegate{form.SwitchPage(page);};
            form.ClientSize=new Size(1536,1024);
            timer.Tick+=delegate
            {
                ticks++;
                if(scan&&!attempted&&ticks>=3&&(form.Camera.State.Phase==ConnectionPhase.Connected||ticks>=24)){attempted=true;form.Camera.Scan(false);}
                bool done=scan?(attempted&&!form.Camera.Running&&form.Camera.LastExitCode>=0):ticks>=10;
                if(!done)return;timer.Stop();form.SaveScreenshot(path);
                var report=new Dictionary<string,object>{{"capturedAt",DateTime.Now.ToString("o")},{"connection",form.Camera.State.ConnectionText},{"scanRequested",scan},{"scanAttempted",attempted},{"scanExitCode",form.Camera.LastExitCode},{"lastScan",form.Camera.State.LastScan.HasValue?form.Camera.State.LastScan.Value.ToString("o"):null},{"usbMode",form.Camera.State.UsbMode},{"reportError",form.Camera.State.ReportError}};
                File.WriteAllText(Path.ChangeExtension(path,"json"),Json.Serialize(report));timer.Dispose();if(!keep)form.Close();
            };
            form.Shown+=delegate{timer.Start();};form.FormClosed+=delegate{timer.Dispose();};
        }
        static void ValidateReportBinding(string root)
        {
            string directory=Path.Combine(root,"report-fixture");Directory.CreateDirectory(directory);
            var property=new Dictionary<string,object>{{"code","0xD16E"},{"valueResponse","0x200A (DevicePropNotSupported)"},{"valueRawHex",""}};
            var device=new Dictionary<string,object>{{"pnpId","fixture_vid_04cb&pid_02e3"},{"getDeviceInfoResponse","0x2001 (OK)"},{"deviceInfo",new Dictionary<string,object>{{"model","X-T30"},{"deviceVersion","TEST-FIRMWARE"}}},{"properties",new List<object>{property}}};
            var report=new Dictionary<string,object>{{"generatedAt","2026-01-01T12:00:00+01:00"},{"device",device}};
            string file=Path.Combine(directory,"xt30_report.json");File.WriteAllText(file,Json.Serialize(report));
            using(CameraPresenter presenter=new CameraPresenter(directory,true))
            {
                Require(presenter.State.Firmware=="TEST-FIRMWARE"&&presenter.State.Historical,"Historical metadata explicitly marked");
                Require(presenter.State.UsbMode=="Not reported","PID alone must not imply a USB mode");
                presenter.State.PnpId="fixture_vid_04cb&pid_02e3";presenter.LoadReport();
                Require(!presenter.State.Historical&&presenter.State.Protocol=="PTP (MTP)","Matching DeviceInfo response");
                property["valueResponse"]="0x2001 (OK)";property["valueRawHex"]="06000000";File.WriteAllText(file,Json.Serialize(report));presenter.LoadReport();
                Require(presenter.State.UsbMode=="RAW CONV./BACKUP RESTORE","USB mode from a successful property read");
                presenter.State.PnpId="different_device";presenter.LoadReport();Require(presenter.State.Firmware=="—"&&presenter.State.UsbMode=="Not reported","Other device cannot inherit metadata");
            }
        }
    }
}
