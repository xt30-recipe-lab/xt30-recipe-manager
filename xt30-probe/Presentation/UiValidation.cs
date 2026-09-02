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
                RecipeLibrary library=new RecipeLibrary(data);Require(library.Recipes.FindAll(x=>!x.IsImported).Count==7,"Demo seed");
                Require(library.Recipes.FindAll(x=>x.IsImported).Count==library.ImportedCount,"Imported provenance");
                foreach(Recipe imported in library.Recipes)if(imported.IsImported)Require(imported.SourceSite=="FUJI X WEEKLY"&&imported.CompatStatus!="","Imported source/compat metadata");
                if(library.ImportedCount>0){Require(library.Query("","Fuji X Weekly").Count==library.ImportedCount,"Imported filter");passed.Add("Fuji X Weekly library loaded ("+library.ImportedCount+" imported, read-only, labeled)");}
                Recipe recipe=library.Recipes[0];library.ToggleFavorite(recipe);RecipeLibrary loaded=new RecipeLibrary(data);Require(loaded.Recipes[0].Favorite,"Favorite persisted across reload");passed.Add("Favorite persistence and JSON round trip");
                Recipe unsupported=library.Recipes.First(x=>x.Simulation=="Classic Negative");Require(unsupported.CompatibilityIssues().Count>0,"Unsupported film simulation warning");unsupported.Values["Clarity"]="3";unsupported.Values["Grain Size"]="Large";unsupported.Values["Color Chrome FX Blue"]="Strong";library.Save();loaded=new RecipeLibrary(data);Recipe read=loaded.Recipes.First(x=>x.Id==unsupported.Id);Require(read.Get("Clarity")=="3"&&read.CompatibilityIssues().Count==4,"Unsupported values preserved");passed.Add("Unsupported settings retained and individually warned");
                Require(loaded.Query("Pacific","Local").Count==1,"Search");Require(loaded.Query("","Favorites").FindAll(x=>!x.IsImported).Count==1,"Favorites filter");Require(!loaded.Query("","Compatible").Contains(read),"Compatibility filter");passed.Add("Search and category/compatibility/favorite filters");
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
                Require(File.Exists(library.Backup()),"Local backup");passed.Add("Local library backup saved without camera access");
                using(MainForm form=new MainForm(true,data))
                {
                    form.ClientSize=new Size(1536,1024);form.Show();Application.DoEvents();
                    foreach(string page in new string[]{"Camera","Recipes","Camera Slots","Packs","Backups","Diagnostics","Settings"})
                    {form.SwitchPage(page);Application.DoEvents();form.SaveScreenshot(Path.Combine(root,"offline-"+page.Replace(" ","-").ToLowerInvariant()+".png"));}
                    foreach(ConnectionPhase state in new ConnectionPhase[]{ConnectionPhase.Connected,ConnectionPhase.Communicating,ConnectionPhase.Error,ConnectionPhase.Disconnected}){form.SetOfflineState(state);Application.DoEvents();Require(form.Camera.State.Phase==state,"UI connection state");}
                    form.SwitchPage("Camera");form.ClientSize=new Size(1200,780);form.SaveScreenshot(Path.Combine(root,"offline-camera-medium.png"));form.ClientSize=new Size(960,700);form.SaveScreenshot(Path.Combine(root,"offline-camera-small.png"));form.SwitchPage("Recipes");form.SaveScreenshot(Path.Combine(root,"offline-recipes-small.png"));
                    using(RecipeDetailForm detail=new RecipeDetailForm(form.Library.Recipes[0])){detail.Show(form);Application.DoEvents();using(Bitmap b=new Bitmap(detail.Width,detail.Height)){detail.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"recipe-detail.png"));}detail.Close();}
                    using(RecipeEditorForm editor=new RecipeEditorForm(null)){editor.Show(form);Application.DoEvents();editor.Close();}
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
