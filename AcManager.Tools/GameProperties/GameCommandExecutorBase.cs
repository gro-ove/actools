using System;
using System.Diagnostics;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public abstract class GameCommandExecutorBase : Game.AdditionalProperties, IDisposable {
        public static int OptionCommandTimeout = 3000;

        private readonly Game.StartProperties _properties;

        protected GameCommandExecutorBase(Game.StartProperties properties) {
            _properties = properties;
        }

        public static void Execute(string commandResolved, string workingDirectory) {
            if (string.IsNullOrWhiteSpace(commandResolved)) return;
            Logging.Write($"Executing command: “{commandResolved}”");

            try {
                var proc = Process.Start(new ProcessStartInfo {
                    FileName = "cmd",
                    Arguments = $"/C \"{commandResolved}\"",
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                if (proc == null) {
                    throw new Exception(ToolsStrings.GameCommand_UnknownProblem);
                }

                proc.OutputDataReceived += (s, e) => {
                    if (!string.IsNullOrWhiteSpace(e.Data)) {
                        Logging.Write("Output: " + e.Data);
                    }
                };
                proc.ErrorDataReceived += (s, e) => {
                    if (!string.IsNullOrWhiteSpace(e.Data)) {
                        Logging.Write("Error: " + e.Data);
                    }
                };

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                if (!proc.WaitForExit(OptionCommandTimeout)) {
                    proc.Kill();
                    throw new InformativeException(ToolsStrings.GameCommand_TimeoutExceeded,
                            string.Format(ToolsStrings.GameCommand_TimeoutExceeded_Commentary, (double)OptionCommandTimeout / 1000));
                }
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.GameCommand_CannotExecute, e);
            }
        }

        protected void Execute(string command) {
            if (string.IsNullOrWhiteSpace(command)) return;
            Execute(VariablesReplacement.Process(command, _properties, null), AcPaths.GetDocumentsDirectory());
        }

        public abstract void Dispose();
    }
}