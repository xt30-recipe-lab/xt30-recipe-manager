using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xt30Probe.AppCamera
{
    // Déclenche la restauration d'un fichier de réglages en pilotant la
    // FUJIFILM Tether App, via le script Tools/RestoreMacro.
    //
    // Cette classe n'ouvre AUCUNE connexion USB et n'envoie aucune commande à
    // l'appareil : c'est le logiciel de Fujifilm qui écrit, par sa fonction
    // « Restauration des paramètres de l'appareil », officiellement supportée
    // sur X-T30. Le moteur PTP de l'application reste en lecture seule.
    public static class CameraRestore
    {
        public sealed class Result
        {
            public bool Success;
            public string Output = "";
            public string Error = "";
        }

        public static string ScriptPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "RestoreMacro", "Restore-CameraSettings.ps1"); }
        }

        public static bool Available { get { return File.Exists(ScriptPath); } }

        // Après un envoi réussi, on redécode le fichier envoyé pour que l'application
        // affiche immédiatement le nouvel état des banques. C'est exact : ce fichier
        // EST ce que l'appareil vient de recevoir. Aucun accès à l'appareil n'est
        // nécessaire, et l'échec de cette étape n'invalide pas l'envoi.
        public static bool RefreshDecodedBanks(string datFile)
        {
            string decoder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "BackupDecoder", "xt30-backup-decoder.exe");
            if (!File.Exists(decoder) || !File.Exists(datFile)) return false;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(decoder);
                info.Arguments = "\"" + datFile + "\" --out \"" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "phase2-inventory") + "\"";
                info.UseShellExecute = false; info.CreateNoWindow = true;
                info.RedirectStandardOutput = true; info.RedirectStandardError = true;
                using (Process process = Process.Start(info))
                {
                    process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd();
                    return process.WaitForExit(20000) && process.ExitCode == 0;
                }
            }
            catch (Exception) { return false; }
        }

        // preview = navigue jusqu'à l'entrée de menu et s'arrête sans rien activer.
        public static Result Run(string datFile, bool preview, int timeoutSeconds)
        {
            Result result = new Result();
            if (!Available) { result.Error = "The restore helper script is missing:\n" + ScriptPath; return result; }
            if (!File.Exists(datFile)) { result.Error = "The settings file no longer exists:\n" + datFile; return result; }

            ProcessStartInfo info = new ProcessStartInfo("powershell.exe");
            info.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + ScriptPath + "\" -File \"" + datFile + "\""
                + (preview ? " -Preview" : "");
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.CreateNoWindow = true;
            info.StandardOutputEncoding = Encoding.UTF8;
            info.StandardErrorEncoding = Encoding.UTF8;

            try
            {
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(Math.Max(10, timeoutSeconds) * 1000))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        result.Error = "The restore took too long and was stopped. Check the Tether App before retrying.";
                        result.Output = output;
                        return result;
                    }
                    result.Output = output;
                    result.Success = process.ExitCode == 0;
                    if (!result.Success) result.Error = error.Trim() == "" ? "The restore helper reported a failure." : error.Trim();
                }
            }
            catch (Exception ex) { result.Error = ex.Message; }
            return result;
        }
    }
}
