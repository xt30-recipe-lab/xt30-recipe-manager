using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xt30Probe.AppCamera
{
    // Lit les sept banques du boîtier sans aucune intervention manuelle.
    //
    // L'application n'ouvre elle-même AUCUNE session USB : elle lance l'outil
    // Tools/BackupRead, dont la liste d'opcodes se limite à GetObjectInfo (0x1008)
    // et GetObject (0x1009), restreint au handle 0. Rien n'est envoyé au boîtier.
    // Le fichier obtenu est ensuite décodé hors ligne par Tools/BackupDecoder.
    public static class CameraBanksReader
    {
        public sealed class Result
        {
            public bool Success;
            public string Output = "";
            public string Error = "";
            public string SettingsFile = "";
            public bool ClosedTetherApp;
        }

        static string Base { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string ReadTool { get { return Path.Combine(Base, "Tools", "BackupRead", "xt30-backup-read.exe"); } }
        public static string DecodeTool { get { return Path.Combine(Base, "Tools", "BackupDecoder", "xt30-backup-decoder.exe"); } }
        public static bool Available { get { return File.Exists(ReadTool) && File.Exists(DecodeTool); } }

        // La Tether App garde le périphérique USB ouvert : tant qu'elle tourne, toute
        // lecture échoue avec ERROR_BUSY. On ne la ferme donc qu'après un échec, et
        // seulement pour réessayer une fois.
        static bool CloseTetherApp()
        {
            bool closed = false;
            try
            {
                foreach (Process process in Process.GetProcessesByName("FUJIFILM_TetherApp"))
                {
                    try { if (!process.CloseMainWindow() || !process.WaitForExit(4000)) process.Kill(); closed = true; }
                    catch (Exception) { }
                    finally { process.Dispose(); }
                }
            }
            catch (Exception) { }
            if (closed) System.Threading.Thread.Sleep(2500);
            return closed;
        }

        static int Run(string exe, string arguments, int timeoutSeconds, out string output, out string error)
        {
            ProcessStartInfo info = new ProcessStartInfo(exe, arguments);
            info.WorkingDirectory = Base;
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.UTF8; info.StandardErrorEncoding = Encoding.UTF8;
            using (Process process = Process.Start(info))
            {
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(Math.Max(10, timeoutSeconds) * 1000))
                {
                    try { process.Kill(); } catch (Exception) { }
                    return -1;
                }
                return process.ExitCode;
            }
        }

        public static Result Read()
        {
            Result result = new Result();
            if (!Available)
            {
                result.Error = "The camera reading tools are missing:\n" + ReadTool + "\n" + DecodeTool;
                return result;
            }

            string output, error;
            int code;
            try { code = Run(ReadTool, "--out phase2-inventory", 90, out output, out error); }
            catch (Exception ex) { result.Error = ex.Message; return result; }

            if (code != 0 && CloseTetherApp())
            {
                // Second essai : la Tether App tenait le périphérique.
                result.ClosedTetherApp = true;
                try { code = Run(ReadTool, "--out phase2-inventory", 90, out output, out error); }
                catch (Exception ex) { result.Error = ex.Message; return result; }
            }

            result.Output = (output + "\n" + error).Trim();
            if (code != 0)
            {
                result.Error = code == -1
                    ? "The camera did not answer in time."
                    : "The camera could not be read (exit code " + code + ").";
                return result;
            }

            string file = Xt30Probe.AppModel.CameraBankFile.FindLatestSettingsFile(Base);
            if (file == null) { result.Error = "The read finished but no settings file was produced."; return result; }
            result.SettingsFile = file;

            string decodeOut, decodeErr;
            try { code = Run(DecodeTool, "\"" + file + "\" --out \"" + Path.Combine(Base, "phase2-inventory") + "\"", 60, out decodeOut, out decodeErr); }
            catch (Exception ex) { result.Error = "The file was read but could not be decoded: " + ex.Message; return result; }
            if (code != 0) { result.Error = "The file was read but could not be decoded.\n" + (decodeErr.Trim() == "" ? decodeOut.Trim() : decodeErr.Trim()); return result; }

            result.Success = true;
            return result;
        }
    }
}
