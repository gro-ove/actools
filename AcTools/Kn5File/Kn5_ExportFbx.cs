using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public static string FbxConverterLocation;

        private static string GetFbxConverterLocation() {
            if (FbxConverterLocation != null) return FbxConverterLocation;
            var location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var fbxConverter = Path.Combine(location ?? "", "FbxConverter.exe");
            return fbxConverter;
        }

        public void ExportFbx(string filename) {
            var colladaFilename = filename + ".dae";
            ExportCollada(colladaFilename);
            ConvertColladaToFbx(colladaFilename, filename);
        }

        public void ConvertColladaToFbx(string colladaFilename, string fbxFilename) {
            var process = new Process();
            var outputStringBuilder = new StringBuilder();

            try {
                var arguments = "\"" + Path.GetFileName(colladaFilename) + "\" \"" + Path.GetFileName(fbxFilename) + "\" /sffCOLLADA /dffFBX /l /f201200";
                var colladaLocation = Path.GetDirectoryName(fbxFilename);
                process.StartInfo.FileName = GetFbxConverterLocation();
                process.StartInfo.WorkingDirectory = colladaLocation ?? "";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processExited = process.WaitForExit(60000);

                if (processExited == false) {
                    process.Kill();
                    throw new Exception("FbxConverter took too long to finish");
                }

                Console.WriteLine(@"\nAutodesk FBX Converter:\n    Arguments: {0}\n    Exit code: {1}\n{2}",
                        arguments, process.ExitCode, outputStringBuilder.ToString().Trim().Replace("\n", "\n    "));

                if (process.ExitCode == 0) {
                    File.Delete(colladaFilename);
                }
            } finally {
                process.Close();
            }
        }
    }
}
