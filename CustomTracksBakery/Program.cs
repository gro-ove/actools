using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;
using CommandLine;
using CommandLine.Text;
using StringBasedFilter;

namespace CustomTracksBakery {
    public class Options {
        [Option('h', "help", DefaultValue = false, HelpText = "Show help message.")]
        public bool Help { get; set; }

        [Option('f', "filter", DefaultValue = "*", Required = false, HelpText = "Nodes to bake AO for.")]
        public string Filter { get; set; }

        [Option('s', "skip", DefaultValue = null, Required = false, HelpText = "Nodes to skip.")]
        public string IgnoreFilter { get; set; }

        [Option('t', "trees", DefaultValue = "shader:ksTree", Required = false, HelpText = "Nodes to process as trees (no self-shadowing, normals are facing up).")]
        public string TreeFilter { get; set; }

        [Option("skip-occluders", DefaultValue = null, Required = false, HelpText = "Nodes to skip from occlusion calculation.")]
        public string SkipOccludersFilter { get; set; }

        [Option("common-kn5s", DefaultValue = null, Required = false, HelpText = "KN5 files to load for occluders.")]
        public string CommonKn5Filter { get; set; }

        [Option('d', "destination", DefaultValue = null, Required = false, HelpText = "Optional destination.")]
        public string Destination { get; set; }

        [Option('o', "opacity", DefaultValue = 0.85f, Required = false, HelpText = "AO opacity.")]
        public float AoOpacity { get; set; }

        [Option('m', "multiplier", DefaultValue = 1.0f, Required = false, HelpText = "AO brightness multiplier.")]
        public float AoMultiplier { get; set; }

        [Option("saturation-gain", DefaultValue = 3.0f, Required = false, HelpText = "Saturation brightness gain.")]
        public float SaturationGain { get; set; }

        [Option("saturation-input-mult", DefaultValue = 2.0f, Required = false, HelpText = "Saturation input multiplier.")]
        public float SaturationInputMultiplier { get; set; }

        [Option("extra-pass-brightness-mult", DefaultValue = 1.1f, Required = false, HelpText = "Brightness multiplier for second pass.")]
        public float ExtraPassBrightnessGain { get; set; }

        [Option("camera-fov", DefaultValue = 120.0f, Required = false, HelpText = "Camera FOV.")]
        public float CameraFov { get; set; }

        [Option("camera-near", DefaultValue = 0.15f, Required = false, HelpText = "Camera near clipping distance.")]
        public float CameraNear { get; set; }

        [Option("camera-far", DefaultValue = 50.0f, Required = false, HelpText = "Camera far clipping distance.")]
        public float CameraFar { get; set; }

        [Option("camera-normal-offset-up", DefaultValue = 0.2f, Required = false, HelpText = "Camera direction offset, up.")]
        public float CameraNormalOffsetUp { get; set; }

        [Option("camera-offset-away", DefaultValue = 0.16f, Required = false, HelpText = "Camera position offset, from the surface.")]
        public float CameraOffsetAway { get; set; }

        [Option("camera-offset-up", DefaultValue = 0.08f, Required = false, HelpText = "Camera position offset, up.")]
        public float CameraOffsetUp { get; set; }

        [Option("occluders-distance-threshold", DefaultValue = 30.0f, Required = false, HelpText = "Occluders distance threshold.")]
        public float OccludersDistanceThreshold { get; set; }

        [Option("merge-vertices", DefaultValue = 50, Required = false, HelpText = "Range (in the list) to merge vertices within.")]
        public int MergeVertices { get; set; }

        [Option("merge-threshold", DefaultValue = 0.1f, Required = false, HelpText = "Range to merge vertices within.")]
        public float MergeThreshold { get; set; }

        [Option("queue-size", DefaultValue = 1000, Required = false, HelpText = "Size of rendering queue.")]
        public int QueueSize { get; set; }

        [Option("sample-resolution", DefaultValue = 16, Required = false, HelpText = "Sample resolution.")]
        public int SampleResolution { get; set; }

        [Option("hdr", DefaultValue = false, HelpText = "Make HDR samples.")]
        public bool HdrSamples { get; set; }

        [Option("extra-pass", DefaultValue = false, HelpText = "Make two passes to properly process bounced colors.")]
        public bool ExtraPass { get; set; }

        [Option("bake-into-kn5", DefaultValue = false, HelpText = "Bake shadows into KN5 instead of creating a small patch.")]
        public bool ModifyKn5Directly { get; set; }

        [ValueList(typeof(List<string>), MaximumElements = 1)]
        public IList<string> Items { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        public string GetUsage() {
            var help = new HelpText {
                Heading = new HeadingInfo("Custom Tracks Bakery", FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location ?? "").FileVersion),
                Copyright = new CopyrightInfo("AcClub", 2018),
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("\r\nThis is free software. You may redistribute copies of it under the terms of");
            help.AddPreOptionsLine("the MS-PL License <https://opensource.org/licenses/MS-PL>.");
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("Usage: CustomTracksBakery <model.kn5> [output.kn5]");
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("You can also put arguments in a file “Baked Shadows Params.txt” next to KN5, or");
            help.AddPreOptionsLine("to “Arguments.txt” next to CustomTracksBakery.exe.");
            help.AddOptions(this);
            return help;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ParentProcessUtilities {
        // These members must match PROCESS_BASIC_INFORMATION
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation,
                int processInformationLength, out int returnLength);

        /// <summary>
        /// Gets the parent process of the current process.
        /// </summary>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess() {
            return GetParentProcess(Process.GetCurrentProcess().Handle);
        }

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(int id) {
            var process = Process.GetProcessById(id);
            return GetParentProcess(process.Handle);
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(IntPtr handle) {
            var pbi = new ParentProcessUtilities();
            var status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
            if (status != 0)
                throw new Win32Exception(status);

            try {
                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            } catch (ArgumentException) {
                // not found
                return null;
            }
        }
    }

    internal class Program {
        private static IEnumerable<Kn5Node> FilterNodes(Kn5 kn5, IFilter<string> filter, Kn5Node node) {
            if (node.NodeClass == Kn5NodeClass.Base) {
                return node.Children.SelectMany(x => FilterNodes(kn5, filter, x));
            }

            if (!filter.Test(node.Name)
                    || kn5.GetMaterial(node.MaterialId)?.TextureMappings.Any(x => x.Name == "txNormal" || x.Name == "txNormalDetail") != false) {
                return new Kn5Node[0];
            }

            return new[] { node };
        }

        public static int Main(string[] args) {
            try {
                var argsLoaded = false;
                if (args.Length > 0) {
                    try {
                        if (File.Exists(args[0])) {
                            var extraFileArgs = Path.Combine(Path.GetDirectoryName(args[0]) ?? ".", "Baked Shadows Params.txt");
                            if (File.Exists(extraFileArgs)) {
                                args = File.ReadAllLines(extraFileArgs).Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("#"))
                                           .Union(args).ToArray();
                                argsLoaded = true;
                            }
                        }
                    } catch {
                        // ignored
                    }
                }

                if (!argsLoaded) {
                    var extraArgs = Path.Combine(MainExecutingFile.Directory, "Arguments.txt");
                    if (File.Exists(extraArgs)) {
                        args = File.ReadAllLines(extraArgs).Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("#"))
                                   .Union(args).ToArray();
                    }
                }

                var options = new Options();
                if (!Parser.Default.ParseArguments(args, options) || options.Items.Count == 0 || options.Help) {
                    (options.Help ? Console.Out : Console.Error).WriteLine(options.GetUsage());
                    return options.Help ? 0 : 1;
                }

                new MainBakery(options.Items[0], options.Filter, options.IgnoreFilter) {
                    AoOpacity = options.AoOpacity,
                    AoMultiplier = options.AoMultiplier,
                    SaturationGain = options.SaturationGain,
                    SaturationInputMultiplier = options.SaturationInputMultiplier,
                    CameraFov = options.CameraFov,
                    CameraNear = options.CameraNear,
                    CameraFar = options.CameraFar,
                    CameraNormalOffsetUp = options.CameraNormalOffsetUp,
                    CameraOffsetAway = options.CameraOffsetAway,
                    CameraOffsetUp = options.CameraOffsetUp,
                    OccludersDistanceThreshold = options.OccludersDistanceThreshold,
                    MergeVertices = options.MergeVertices,
                    MergeThreshold = options.MergeThreshold,
                    QueueSize = options.QueueSize,
                    SampleResolution = options.SampleResolution,
                    ExtraPassBrightnessGain = options.ExtraPassBrightnessGain,
                    HdrSamples = options.HdrSamples,
                    ExtraPass = options.ExtraPass,
                    CreatePatch = !options.ModifyKn5Directly,
                    TreeFilter = options.TreeFilter,
                    SkipOccludersFilter = options.SkipOccludersFilter,
                }.LoadExtraOccluders(options.CommonKn5Filter).Work(options.Destination ?? options.Items[0]);
                return 0;
            } catch (Exception e) {
                Console.Error.WriteLine(e.ToString());
                return 2;
            } finally {
                if (ParentProcessUtilities.GetParentProcess().ProcessName == "explorer") {
                    Console.ReadLine();
                }
            }
        }
    }
}