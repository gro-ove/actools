using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Security.Permissions;
using System.Windows.Forms;
using AcTools.Kn5Render.Kn5Render;
using CommandLine;
using CommandLine.Text;

namespace AcToolsShowroom {
    static class Program {
        private class Options {
            [ValueList(typeof(List<string>), MaximumElements = 1)]
            public IList<string> Files { get; set; }

            [Option('e', "editmode", DefaultValue = false, HelpText = "Start app in edit mode.")]
            public bool EditMode { get; set; }

            [Option('b', "bright", DefaultValue = false, HelpText = "Bright mode.")]
            public bool BrightMode { get; set; }

            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage() {
                return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        class RenderLogging : IRenderLogging {
            public void Write(string format, params object[] args) {
                Logging.Write(format, args);
            }

            public void Warning(string format, params object[] args) {
                Logging.Warning(format, args);
            }

            public void Error(string format, params object[] args) {
                Logging.Error(format, args);
            }
        }

        private static string _fileToOpen;

        [STAThread]
        static int Main(string[] args) {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(location);
            if (fileNameWithoutExtension.ToLower().Contains("log")) {
                Logging.Initialize(Path.Combine(Path.GetDirectoryName(location), fileNameWithoutExtension + "_log.txt"));
            }

            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) {
                Logging.Error("args are invalid");
                return 1;
            }

            SetUnhandledExceptionHandler();

            string fileToOpen;

            if (options.Files.Count == 0) {
                Logging.Write("filename is missing, dialog mode");
                var dialog = new OpenFileDialog {
                    Title = @"Select Kn5 to View",
                    Filter = @"KN5 Files (*.kn5)|*.kn5"
                };
                if (dialog.ShowDialog() != DialogResult.OK) {
                    Logging.Error("selection is cancelled");
                    return 2;
                }
                fileToOpen = dialog.FileName;
            } else {
                fileToOpen = options.Files[0];
            }

            _fileToOpen = fileToOpen;

            Render.Logging = new RenderLogging();
            using (
                var render = new Render(fileToOpen, 0,
                                        options.BrightMode
                                            ? Render.VisualMode.BRIGHT_ROOM
                                            : Render.VisualMode.DARK_ROOM)) {
                render.Form(1280, 720, options.EditMode);
            }

            return 0;

            //Process.Start(Showroom.ShotAll(@"D:\Games\Assetto Corsa", "trophy_truck", "studio_black", "10,5,3", "0,0.6,0", 30.0,
            //                  null, false));

            /* var file = @"D:\Games\Assetto Corsa\content\cars\acc_dodge_monaco_police\dodge_monaco_police.kn5";
            var dir = Path.GetDirectoryName(file);
            Kn5RenderWrapper.UpdateAmbientShadows(dir);
            Process.Start(dir + @"\body_shadow.png");*/
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public static void SetUnhandledExceptionHandler() {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            var e = args.ExceptionObject as Exception;

            var text = "Unhandled exception:\n\n" + (e == null ? "null" : e.ToString());

            try {
                MessageBox.Show(text, @"Oops!", MessageBoxButtons.OK);
            } catch (Exception) {
                // ignored
            }

            Logging.Error("FATAL ERROR: {0}", e);

            if (!Logging.IsInitialized()) {
                try {
                    var logFilename = AppDomain.CurrentDomain.BaseDirectory + "/ac_tools_showroom_crash_" +
                                      DateTime.Now.Ticks + ".txt";
                    File.WriteAllText(logFilename, _fileToOpen + "\n" + text);
                } catch (Exception) {
                    // ignored
                }
            }

            Environment.Exit(1);
        }
    }
}
