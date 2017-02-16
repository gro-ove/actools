using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CommandLine;
using CommandLine.Text;

namespace CustomPreviewUpdater {
    internal enum PseudoBool {
        True = 1,
        On = 1,
        Yes = 1,
        Y = 1,
        False = 0,
        Off = 0,
        No = 0,
        N = 0
    }

    internal class Options {
        [ValueList(typeof(List<string>), MaximumElements = -1)]
        public IList<string> Ids { get; set; }

        [Option('r', "root", HelpText = "AC root folder.")]
        public string AcRoot { get; set; }

        //[Option('s', "showroom", DefaultValue = "previews", HelpText = "Showroom to shot previews at.")]
        //public string Showroom { get; set; }
        
        /* CAMERA */
        [Option('c', "camera", DefaultValue = "3.867643, 1.42359, 4.70381", HelpText = "Camera position.")]
        public string CameraPosition { get; set; }

        [Option('l', "look-at", DefaultValue = "0.0, 0.7, 0.5", HelpText = "Look at.")]
        public string LookAt { get; set; }

        [Option('v', "fov", DefaultValue = 30.0, HelpText = "Field of view.")]
        public double Fov { get; set; }

        [Option('a', "align", HelpText = "Align car to the center horizontally and then offset using look-at value.")]
        public bool AlignCar { get; set; }
        
        /* OPTIONS */
        [Option("bloom", DefaultValue = 1d, HelpText = "Bloom radius multipler.")]
        public double BloomRadiusMultipler { get; set; }

        [Option("width", DefaultValue = 1022, HelpText = "Previews width.")]
        public int PreviewWidth { get; set; }

        [Option("height", DefaultValue = 575, HelpText = "Previews height.")]
        public int PreviewHeight { get; set; }

        /* AA-RELATED */
        [Option("ssaa", DefaultValue = 4d, HelpText = "SSAA multipler.")]
        public double SsaaMultipler { get; set; }

        [Option("fxaa", HelpText = "Enable FXAA.")]
        public bool UseFxaa { get; set; }

        [Option("msaa", HelpText = "Enable MSAA.")]
        public bool UseMsaa { get; set; }

        [Option("msaa-count", DefaultValue = 4, HelpText = "Samples count for MSAA.")]
        public int MsaaSampleCount { get; set; }

        [Option("software-downscale", HelpText = "Use software downscale, notably slower.")]
        public bool SoftwareDownscale { get; set; }

        /* MISC PARAMS */
        [Option("name", DefaultValue = "preview.jpg", HelpText = "Names of preview files.")]
        public string FileName { get; set; }

        [Option("attempts", DefaultValue = 3, HelpText = "Number of attempts if there are any problems.")]
        public int AttemptsCount { get; set; }

        [Option("single-thread", HelpText = "Do not use separate threads to encode image.")]
        public bool SingleThread { get; set; }

        /* MODES */
        [Option("wireframe", HelpText = "Wireframe mode.")]
        public bool WireframeMode { get; set; }

        [Option("mesh-debug", HelpText = "Mesh debug mode.")]
        public bool MeshDebugMode { get; set; }

        [Option("suspension-debug", HelpText = "Suspension debug mode.")]
        public bool SuspensionDebugMode { get; set; }

        /* CAR */
        [Option("headlights", HelpText = "Enable headlights.")]
        public bool HeadlightsEnabled { get; set; } = false;

        [Option("brakelights", HelpText = "Enable brake lights.")]
        public bool BrakeLightsEnabled { get; set; } = false;

        [Option("left-door-open", HelpText = "Open left door.")]
        public bool LeftDoorOpen { get; set; } = false;

        [Option("right-door-open", HelpText = "Open right door.")]
        public bool RightDoorOpen { get; set; } = false;

        [Option("steer", HelpText = "Steer front wheels at specified angle.")]
        public double SteerAngle { get; set; } = 0d;

        /* SCENE */
        [Option("mirror", HelpText = "Flat mirror at the ground.")]
        public bool FlatMirror { get; set; } = false;

        [Option("background", HelpText = "Background color.")]
        public string BackgroundColor { get; set; } = "#000000";

        [Option("light", HelpText = "Light color.")]
        public string LightColor { get; set; } = "#ffffff";

        [Option("ambient-from", HelpText = "First ambient color (at the bottom).")]
        public string AmbientDown { get; set; } = "#96b4b4";

        [Option("ambient-to", HelpText = "Second ambient color (at the top).")]
        public string AmbientUp { get; set; } = "#b4b496";

        [Option("ambient-brightness", HelpText = "Ambient multipler.")]
        public double AmbientBrightness { get; set; } = 2d;

        [Option("light-brightness", HelpText = "Light multipler.")]
        public double LightBrightness { get; set; } = 1.5;

        [Option('w', "without-previews-only", DefaultValue = false, HelpText = "Update skins only without previews.")]
        public bool WithoutPreviews { get; set; }

        [Option("filter-test", DefaultValue = false, HelpText = "Just list filtered cars and exit.")]
        public bool FilterTest { get; set; }

        [Option("verbose", HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() {
            var help = new HelpText {
                Heading = new HeadingInfo("CustomPreviewUpdater", FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location ?? "").FileVersion),
                Copyright = new CopyrightInfo("AcClub", 2017),
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("\r\nThis is free software. You may redistribute copies of it under the terms of");
            help.AddPreOptionsLine("the MS-PL License <https://opensource.org/licenses/MS-PL>.");
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("Usage: CustomPreviewUpdater -r <AC root> [options...] <car IDs or filters...>");
            help.AddOptions(this);
            return help;
        }
    }
}