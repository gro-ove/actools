// ReSharper disable RedundantUsingDirective
#define PACKED_MODE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Windows.Forms;
using AcTools.Render.Kn5SpecificDeferred;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Temporary;
using AcTools.Render.Wrapper;
using CommandLine;
using CommandLine.Text;

namespace CustomShowroom {
    public enum Mode {
        Custom, Lite, UpdateAmbientShadows, ExtractUv
    }

    public class Options {
        [Option('m', "mode", DefaultValue =
#if DEBUG
            Mode.ExtractUv
#else
            Mode.Lite
#endif
            , HelpText = "App mode (Custom for fanciness or Lite for work).")]
        public Mode Mode { get; set; }

        [Option('a', "msaa", DefaultValue = false, HelpText = "Use MSAA (only for Lite Showroom mode).")]
        public bool UseMsaa { get; set; }

        [Option('x', "fxaa", DefaultValue = true, HelpText = "Use FXAA.")]
        public bool UseFxaa { get; set; }

        [Option('s', "showroom", DefaultValue = "showroom", HelpText = "Specific showroom ID (only for the Custom Showroom mode).")]
        public string ShowroomId { get; set; }

        [Option('t', "extract-texture", DefaultValue = null, HelpText = "Texture for which UV will be extracted.")]
        public string ExtractUvTexture { get; set; }

        [Option('v', "verbose", DefaultValue = false, HelpText = "Write some stuff to Log.txt near to exe-file.")]
        public bool Verbose { get; set; }

        [ValueList(typeof(List<string>), MaximumElements = 2)]
        public IList<string> Items { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    public class Program {
        private static void UpdateAmbientShadows(string kn5) {
            using (var renderer = new AmbientShadowKn5ObjectRenderer(kn5)) {
                var dir = Path.GetDirectoryName(kn5);
                renderer.Shot(dir);
                Process.Start(Path.Combine(dir, "tyre_0_shadow.png"));
            }
        }

        private static void ExtractUv(string kn5, string extractUvTexture) {
            using (var renderer = new UvRenderer(kn5)) {
                var dir = Path.GetDirectoryName(kn5);
                var output = Path.Combine(dir, "extracted_uv.png");
                renderer.Shot(output, extractUvTexture);
                Process.Start(output);
            }
        }

        [STAThread]
        private static int Main(string[] a) {
            if (!Debugger.IsAttached) {
                SetUnhandledExceptionHandler();
            }

#if PACKED_MODE
            AppDomain.CurrentDomain.AssemblyResolve += new PackedHelper("AcTools_CustomShowroom", "CustomShowroom.References", false).Handler;
#endif
            return MainInner(a);
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public static void SetUnhandledExceptionHandler() {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MainInner(string[] args) {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) return 1;

            var filename = Assembly.GetEntryAssembly().Location;
            if (options.Verbose || filename.IndexOf("log", StringComparison.OrdinalIgnoreCase) != -1
                    || filename.IndexOf("debug", StringComparison.OrdinalIgnoreCase) != -1) {
                var log = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? "", "Log.txt");
                try {
                    File.WriteAllBytes(log, new byte[0]);
                    Logging.Initialize(log);
                } catch (Exception e) {
                    MessageBox.Show("Can't setup logging: " + e, @"Oops!", MessageBoxButtons.OK);
                }
            }

            var inputItems = options.Items;
#if DEBUG
            inputItems = inputItems.Any() ? inputItems : new[] { DebugHelper.GetCarKn5(), DebugHelper.GetShowroomKn5() };
#endif

            if (inputItems.Count == 0) {
                var dialog = new OpenFileDialog {
                    Title = @"Select KN5",
                    Filter = @"KN5 Files (*.kn5)|*.kn5"
                };
                if (dialog.ShowDialog() != DialogResult.OK) return 2;

                inputItems = new[] { dialog.FileName };
            }

            var kn5File = inputItems.ElementAtOrDefault(0);
            if (kn5File == null || !File.Exists(kn5File)) {
                MessageBox.Show(@"File is missing", @"Custom Showroom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 3;
            }

            if (options.Mode == Mode.UpdateAmbientShadows) {
                UpdateAmbientShadows(kn5File);
                return 0;
            }

            if (options.Mode == Mode.ExtractUv) {
                if (string.IsNullOrWhiteSpace(options.ExtractUvTexture)) {
                    MessageBox.Show(@"Texture to extract is not specified", @"Custom Showroom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 4;
                }
                ExtractUv(kn5File, options.ExtractUvTexture);
                return 0;
            }

            var showroomKn5File = inputItems.ElementAtOrDefault(1);
            if (showroomKn5File == null && options.ShowroomId != null) {
                showroomKn5File = Path.Combine(
                        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(kn5File))) ?? "",
                        "showroom", options.ShowroomId, options.ShowroomId + ".kn5");
            }

            if (!File.Exists(showroomKn5File)) {
                showroomKn5File = null;
            }
            
            if (options.Mode == Mode.Lite) {
                using (var renderer = new ForwardKn5ObjectRenderer(kn5File)) {
                    renderer.UseMsaa = options.UseMsaa;
                    renderer.UseFxaa = options.UseFxaa;
                    new LiteShowroomWrapper(renderer).Run();
                }
            } else {
                using (var renderer = new Kn5ObjectRenderer(kn5File, showroomKn5File)) {
                    renderer.UseFxaa = options.UseFxaa;
                    new FancyShowroomWrapper(renderer).Run();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            return 0;
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            var e = args.ExceptionObject as Exception;

            var text = "Unhandled exception:\n\n" + (e?.ToString() ?? "null");

            try {
                MessageBox.Show(text, @"Oops!", MessageBoxButtons.OK);
            } catch (Exception) {
                // ignored
            }

            try {
                var logFilename = AppDomain.CurrentDomain.BaseDirectory + "/custom_showroom_crash_" + DateTime.Now.Ticks + ".txt";
                File.WriteAllText(logFilename, text);
            } catch (Exception) {
                // ignored
            }

            Environment.Exit(1);
        }
    }
}
