using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Xt30Probe.AppCamera
{
    public enum ConnectionPhase { Searching, Disconnected, Connected, Communicating, Error }
    public sealed class CameraState
    {
        public ConnectionPhase Phase = ConnectionPhase.Searching;
        public string Name = "FUJIFILM X-T30";
        public string PnpId = "";
        public string Firmware = "—";
        public string UsbMode = "Not reported";
        public string VidPid = "— / —";
        public string Protocol = "Not scanned";
        public string Status = "Waiting for camera";
        public string Message = "Looking for your camera…";
        public string ReportError = "";
        public DateTime? LastScan;
        public bool HasReport;
        public bool Historical;
        public Dictionary<string, object> Report;
        public string ConnectionText { get { return Phase == ConnectionPhase.Connected ? "Connected" : Phase == ConnectionPhase.Communicating ? "Scanning camera…" : Phase == ConnectionPhase.Error ? "Communication error" : Phase == ConnectionPhase.Searching ? "Looking for camera…" : "Disconnected"; } }
    }

    // Boundary between existing engine and presentation. No new PTP operations.
    public sealed class CameraPresenter : IDisposable
    {
        readonly string _outDir;
        readonly SynchronizationContext _ui;
        readonly bool _offline;
        readonly object _gate = new object();
        readonly StringBuilder _log = new StringBuilder();
        volatile bool _disposed;
        volatile bool _running;
        public CameraState State = new CameraState();
        public event EventHandler Changed;
        public event EventHandler ScanFinished;
        public int LastExitCode = -1;
        public bool Running { get { return _running; } }
        public string OutputDirectory { get { return _outDir; } }
        public CameraPresenter(string outDir, bool offline)
        {
            _outDir = outDir; _offline = offline;
            _ui = SynchronizationContext.Current ?? new SynchronizationContext();
            LoadReport();
            if (offline) { State.Phase = ConnectionPhase.Disconnected; State.Message = "Offline UI preview · no camera accessed"; }
        }
        public void Start()
        {
            if (_offline) { Notify(); return; }
            Thread watch = new Thread(delegate()
            {
                while (!_disposed)
                {
                    if (!_running) DetectOnce();
                    for (int i = 0; i < 40 && !_disposed; i++) Thread.Sleep(100);
                }
            });
            watch.IsBackground = true; watch.Name = "XT30 WPD discovery"; watch.Start();
        }
        void Dispatch(Action action) { _ui.Post(delegate(object ignored) { if (!_disposed) action(); }, null); }
        void Notify() { if (Changed != null) Changed(this, EventArgs.Empty); }
        public string Log { get { lock (_log) return _log.ToString(); } }
        void AppendLog(string line) { lock (_log) { _log.AppendLine(line); } }
        public void DetectOnce()
        {
            // Same 4-second WPD enumeration and Fujifilm matching as the original GUI.
            lock (_gate)
            {
                if (_running || _disposed) return;
                try
                {
                    string found = null, name = null;
                    foreach (string id in MtpDevice.ListDeviceIds())
                    {
                        string friendly = MtpDevice.GetDeviceString(id, 0);
                        string manufacturer = MtpDevice.GetDeviceString(id, 1);
                        bool fuji = id.ToLowerInvariant().Contains("vid_04cb") || manufacturer.ToUpperInvariant().Contains("FUJI") || friendly.ToUpperInvariant().Contains("FUJI");
                        if (fuji && found == null) { found = id; name = friendly; }
                    }
                    string deviceId = found, friendlyName = name;
                    Dispatch(delegate()
                    {
                        if (_running) return;
                        bool different = State.PnpId != (deviceId ?? "");
                        State.PnpId = deviceId ?? "";
                        State.Phase = deviceId == null ? ConnectionPhase.Disconnected : ConnectionPhase.Connected;
                        State.Status = deviceId == null ? "Connect your camera" : "Ready to scan";
                        State.Message = deviceId == null ? "Camera disconnected. Connect your X-T30 to get started." : "Camera connected. Ready for a read-only scan.";
                        if (deviceId != null) { State.Name = string.IsNullOrEmpty(friendlyName) ? "FUJIFILM" : "FUJIFILM " + friendlyName.Replace("FUJIFILM", "").Trim(); State.VidPid = ParseIds(deviceId); }
                        ApplyReportFields();
                        if (!different && LastExitCode == 0 && deviceId != null) State.Message = "Camera scan completed successfully.";
                        Notify();
                    });
                }
                catch (Exception ex)
                {
                    string error = ex.Message;
                    Dispatch(delegate() { if (_running) return; State.Phase = ConnectionPhase.Error; State.Status = "Detection error"; State.Message = "Camera detection failed: " + error; Notify(); });
                }
            }
        }
        public void Scan(bool sweep)
        {
            if (_running || _offline || _disposed) return;
            _running = true; LastExitCode = -1;
            State.Phase = ConnectionPhase.Communicating; State.Status = "Scanning · read only"; State.Message = "Scanning camera. Reading properties only…";
            lock (_log) _log.Length = 0;
            Notify();
            Thread thread = new Thread(delegate()
            {
                int result;
                lock (_gate)
                {
                    Action<string> previous = Program.LogSink;
                    Program.LogSink = AppendLog;
                    try { result = Program.Run(false, sweep, _outDir); }
                    catch (Exception ex) { Program.LogCrash(ex); AppendLog("UNEXPECTED ERROR: " + ex); result = 1; }
                    finally { Program.LogSink = previous; }
                }
                Dispatch(delegate()
                {
                    _running = false; LastExitCode = result; LoadReport();
                    if(result==0){State.PnpId=Value(Object(State.Report,"device"),"pnpId");ApplyReportFields();}
                    State.Phase = result == 0 ? ConnectionPhase.Connected : result == 2 ? ConnectionPhase.Disconnected : ConnectionPhase.Error;
                    State.Status = result == 0 ? "Ready" : result == 2 ? "No camera detected" : result == 3 ? "Camera busy" : "Scan failed";
                    State.Message = result == 0 ? "Camera scan completed successfully." : result == 2 ? "No Fujifilm camera detected." : result == 3 ? "Camera is busy. Close other camera applications and try again." : "Scan failed. Open Diagnostics for details.";
                    if (result == 0 && !string.IsNullOrEmpty(State.ReportError)) State.Message = "Scan ended, but the report could not be loaded. View Diagnostics.";
                    Notify(); if (ScanFinished != null) ScanFinished(this, EventArgs.Empty);
                });
            });
            thread.IsBackground = true; thread.Name = "XT30 read-only scan"; thread.Start();
        }
        public void LoadReport()
        {
            string path = Path.Combine(_outDir, "xt30_report.json");
            if (!File.Exists(path)) return;
            try
            {
                State.Report = Json.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                if (State.Report == null) throw new InvalidDataException("Invalid report");
                DateTime parsed; if (DateTime.TryParse(Value(State.Report, "generatedAt"), out parsed)) State.LastScan = parsed;
                State.HasReport = true; State.ReportError = ""; ApplyReportFields();
            }
            catch (Exception ex) { State.ReportError = ex.Message; }
        }
        void ApplyReportFields()
        {
            if (State.Report == null) return;
            Dictionary<string, object> dev = Object(State.Report, "device");
            bool same = !string.IsNullOrEmpty(State.PnpId) && string.Equals(State.PnpId, Value(dev, "pnpId"), StringComparison.OrdinalIgnoreCase);
            State.Historical = !same && string.IsNullOrEmpty(State.PnpId) && !string.IsNullOrEmpty(Value(dev,"pnpId"));
            if (!same && !string.IsNullOrEmpty(State.PnpId)) { State.Firmware = "—"; State.Protocol = "Not scanned"; State.UsbMode = "Not reported"; return; }
            Dictionary<string, object> info = Object(dev, "deviceInfo");
            string model = Value(info, "model");
            if (model != "") State.Name = "FUJIFILM " + model;
            State.Firmware = EmptyDash(Value(info, "deviceVersion"));
            State.VidPid = ParseIds(Value(dev, "pnpId"));
            State.Protocol = Value(dev, "getDeviceInfoResponse").StartsWith("0x2001") ? "PTP (MTP)" : "Not confirmed";
            State.UsbMode = "Not reported";
            object props;
            if (dev.TryGetValue("properties", out props)) foreach (object p in (List<object>)props)
            {
                Dictionary<string, object> property = p as Dictionary<string, object>;
                if (Value(property, "code") == "0xD16E" && Value(property, "valueResponse").StartsWith("0x2001"))
                {
                    string hex = Value(property, "valueRawHex").Replace(" ", "");
                    if (hex.Length >= 2) { string code = hex.Substring(0, 2); State.UsbMode = code == "06" ? "RAW CONV./BACKUP RESTORE" : code == "05" ? "USB Tether" : "Reported: " + hex; }
                }
            }
        }
        public static Dictionary<string, object> Object(Dictionary<string, object> d, string k)
        { object v; return d != null && d.TryGetValue(k, out v) && v is Dictionary<string, object> ? (Dictionary<string, object>)v : new Dictionary<string, object>(); }
        public static string Value(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? Convert.ToString(v) : ""; }
        static string EmptyDash(string s) { return string.IsNullOrEmpty(s) ? "—" : s; }
        static string ParseIds(string id)
        { Match m = Regex.Match(id ?? "", "vid_([0-9a-f]{4}).*pid_([0-9a-f]{4})", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value.ToUpperInvariant() + " / " + m.Groups[2].Value.ToUpperInvariant() : "— / —"; }
        public void Dispose() { _disposed = true; }
    }
}
