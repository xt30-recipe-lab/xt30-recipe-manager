using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;
using Xt30Probe.AppCamera;

namespace Xt30Probe.Presentation
{
    public sealed class DiagnosticPanel : Panel
    {
        readonly CameraPresenter _camera;
        readonly TabControl _tabs=new TabControl();
        readonly TextBox _overview=ReadBox(false),_usb=ReadBox(true),_ptp=ReadBox(true),_log=ReadBox(true);
        readonly DataGridView _properties=new DataGridView();
        readonly List<ActionButton> _buttons=new List<ActionButton>();
        public DiagnosticPanel(CameraPresenter camera)
        {
            _camera=camera;BackColor=Theme.Background;Padding=new Padding(28,0,28,22);_tabs.Font=Theme.Font(14,false);Controls.Add(_tabs);
            AddTab("Overview",_overview);AddTab("USB",_usb);AddTab("PTP",_ptp);AddTab("Properties",_properties);AddTab("Logs",_log);
            _properties.BackgroundColor=Color.White;_properties.BorderStyle=BorderStyle.None;_properties.ReadOnly=true;_properties.AllowUserToAddRows=false;_properties.AllowUserToDeleteRows=false;_properties.RowHeadersVisible=false;_properties.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;_properties.DefaultCellStyle.Font=Theme.Font(12,false);_properties.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;_properties.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            _properties.Columns.Add("Code","Property");_properties.Columns.Add("Name","Name");_properties.Columns.Add("Descriptor","Descriptor response");_properties.Columns.Add("Value","Value / response");_properties.Columns[0].FillWeight=40;_properties.Columns[1].FillWeight=135;
            AddButton("Export diagnostics",Export);AddButton("Open crash.log",delegate{OpenFile("crash.log");});AddButton("Open probe-session.log",delegate{OpenFile("probe-session.log");});AddButton("Open JSON report",delegate{OpenFile("xt30_report.json");});
            AddButton("Legacy report viewer",delegate{new RecipesForm(_camera.OutputDirectory).Show(FindForm());});RefreshReport();
        }
        static TextBox ReadBox(bool mono){return new TextBox(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Color.White,BorderStyle=BorderStyle.None,Font=mono?new Font("Consolas",12,FontStyle.Regular,GraphicsUnit.Pixel):Theme.Font(14,false)};}
        void AddTab(string name,Control content){TabPage p=new TabPage(name){BackColor=Color.White,Padding=new Padding(19)};content.Dock=DockStyle.Fill;p.Controls.Add(content);_tabs.TabPages.Add(p);}
        void AddButton(string text,Action action){ActionButton b=new ActionButton(text,false);_buttons.Add(b);Controls.Add(b);b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Diagnostics",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};}
        protected override void OnLayout(LayoutEventArgs e)
        {base.OnLayout(e);if(_buttons==null)return;int x=28,y=0;foreach(ActionButton b in _buttons){int w=b.Text.Contains("session")?182:160;if(x+w>Width-28){x=28;y+=46;}b.SetBounds(x,y,w,36);x+=w+10;}_tabs.SetBounds(28,y+57,Width-56,Math.Max(230,Height-y-81));}
        public void RefreshLog(){string log=_camera.Log;if(_log.Text!=log){_log.Text=log;_log.SelectionStart=_log.TextLength;_log.ScrollToCaret();}}
        public void RefreshReport()
        {
            CameraState s=_camera.State;var dev=CameraPresenter.Object(s.Report,"device");var info=CameraPresenter.Object(dev,"deviceInfo");
            _overview.Text="READ ONLY — camera writing is disabled\r\n\r\nConnection: "+s.ConnectionText+"\r\nModel: "+s.Name+"\r\nUSB mode: "+s.UsbMode+"\r\nStatus: "+s.Status+"\r\n\r\nLast saved scan: "+(s.LastScan.HasValue?s.LastScan.Value.ToString("F"):"No report")+"\r\n\r\nReport fields below describe the last saved scan, not a new live read.\r\n\r\nDirect C1–C7 access is unavailable. All displayed slot assignments are LOCAL demonstrations.\r\n\r\nAllowed read operations remain 0x1001, 0x1014 and 0x1015.\r\nNo USB write operation was added.\r\n\r\n"+s.ReportError;
            _usb.Text="VID / PID: "+s.VidPid+"\r\nTransport: Windows WPD / MTP passthrough\r\nCurrent WPD device: "+(s.PnpId==""?"Not connected":s.PnpId)+"\r\n\r\nLast report DeviceInfo:\r\n"+Json.Serialize(info);
            StringBuilder ptp=new StringBuilder();ptp.AppendLine("GetDeviceInfo: "+CameraPresenter.Value(dev,"getDeviceInfoResponse"));
            foreach(string k in new string[]{"operationsSupported","devicePropertiesSupported","eventsSupported","vendorExtensionId"}){object v;if(info.TryGetValue(k,out v))ptp.AppendLine("\r\n"+k+":\r\n"+Json.Serialize(v));}
            _ptp.Text=ptp.ToString();_properties.Rows.Clear();object props;
            if(dev.TryGetValue("properties",out props))foreach(object p in (List<object>)props)
            {var d=p as Dictionary<string,object>;var desc=CameraPresenter.Object(d,"desc");string v=CameraPresenter.Value(desc,"currentValue");if(v=="")v=CameraPresenter.Value(d,"valueResponse");_properties.Rows.Add(CameraPresenter.Value(d,"code"),CameraPresenter.Value(d,"name"),CameraPresenter.Value(d,"descResponse"),v);}
            RefreshLog();
        }
        public void OpenFile(string name)
        {string path=Path.Combine(_camera.OutputDirectory,name);if(!File.Exists(path)){MessageBox.Show(this,"This file has not been generated yet: "+name,"No file available");return;}Process.Start("notepad.exe","\""+path+"\"");}
        void Export()
        {
            using(SaveFileDialog dialog=new SaveFileDialog(){Filter="Diagnostics archive|*.zip",FileName="xt30-diagnostics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                using(FileStream file=new FileStream(dialog.FileName,FileMode.Create))using(ZipArchive zip=new ZipArchive(file,ZipArchiveMode.Create))
                {
                    foreach(string name in new string[]{"xt30_report.json","xt30_report.txt","crash.log","probe-session.log"})
                    {string path=Path.Combine(_camera.OutputDirectory,name);if(File.Exists(path)){var entry=zip.CreateEntry(name);using(Stream output=entry.Open())using(FileStream input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))input.CopyTo(output);}}
                }
                MessageBox.Show(this,"Diagnostics exported locally. The archive may contain the camera serial number.","Export complete");
            }
        }
    }
}
