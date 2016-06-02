using System;
using System.Diagnostics;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.SemiGui {
    public class GameCommandExecutor : Game.AdditionalProperties, IDisposable {
        public static int OptionCommandTimeout = 3000;

        private readonly Game.StartProperties _properties;

        public GameCommandExecutor(Game.StartProperties properties) {
            _properties = properties;
        }

        private void Execute(string command) {
            if (string.IsNullOrWhiteSpace(command)) return;

            command = VariablesReplacement.Process(command, _properties, null);
            Logging.Write($"[GAMECOMMANDEXECUTOR] Executing command: “{command}”");

            try {
                var proc = Process.Start(new ProcessStartInfo {
                    FileName = "cmd",
                    Arguments = $"/C \"{command}\"",
                    UseShellExecute = false,
                    WorkingDirectory = FileUtils.GetDocumentsDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                if (proc == null) {
                    throw new Exception("Unknown problem (Process=null)");
                }

                proc.OutputDataReceived += Process_OutputDataReceived;
                proc.ErrorDataReceived += Process_ErrorDataReceived;

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (!proc.WaitForExit(OptionCommandTimeout)) {
                    proc.Kill();
                    throw new Exception("Timeout exceeded");
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t execute command", e);
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrWhiteSpace(e.Data)) {
                Logging.Write("[GAMECOMMANDEXECUTOR] Output: " + e.Data);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrWhiteSpace(e.Data)) {
                Logging.Write("[GAMECOMMANDEXECUTOR] Error: " + e.Data);
            }
        }

        public override IDisposable Set() {
            Execute(SettingsHolder.Drive.PreCommand);
            return this;
        }

        public void Dispose() {
            Execute(SettingsHolder.Drive.PostCommand);
        }
    }
}