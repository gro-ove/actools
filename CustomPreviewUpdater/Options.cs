using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CommandLine;
using CommandLine.Text;

namespace CustomPreviewUpdater {
    internal class Options {
        [ValueList(typeof(List<string>), MaximumElements = -1)]
        public IList<string> Ids { get; set; }

        [Option('r', "root", HelpText = "AC root folder.")]
        public string AcRoot { get; set; }

        [Option('s', "showroom", DefaultValue = null, HelpText = "Showroom ID or its KN5 filename.")]
        public string Showroom { get; set; }

        //[Option('s', "showroom", DefaultValue = "previews", HelpText = "Showroom to shot previews at.")]
        //public string Showroom { get; set; }

        /* CAMERA */
        [Option('c', "camera", DefaultValue = "3.867643, 1.42359, 4.70381", HelpText = "Camera position.")]
        public string CameraPosition { get; set; }

        [Option('l', "look-at", DefaultValue = "0.0, 0.7, 0.5", HelpText = "Look at.")]
        public string LookAt { get; set; }

        [Option('v', "fov", DefaultValue = 30.0, HelpText = "Field of view.")]
        public double Fov { get; set; }

        [Option('a', "align", HelpText = "Align car’s model to X=0 and Z=0.")]
        public bool AlignCar { get; set; }

        [Option("align-camera", HelpText = "Align car’s model in screen space.")]
        public bool AlignCamera { get; set; }

        [Option("align-camera-offset", DefaultValue = "0.0, 0.0, 0.0", HelpText = "Camera offset after aligning car’s model in screenspace.")]
        public string AlignCameraOffset { get; set; }

        /* OPTIONS */
        [Option("bloom", DefaultValue = 1d, HelpText = "Bloom radius multiplier.")]
        public double BloomRadiusMultiplier { get; set; }

        [Option("width", DefaultValue = 1022, HelpText = "Previews width.")]
        public int PreviewWidth { get; set; }

        [Option("height", DefaultValue = 575, HelpText = "Previews height.")]
        public int PreviewHeight { get; set; }

        /* AA-RELATED */
        [Option("ssaa", DefaultValue = 4d, HelpText = "SSAA multiplier.")]
        public double SsaaMultiplier { get; set; }

        [Option("fxaa", HelpText = "Enable FXAA.")]
        public bool UseFxaa { get; set; }

        [Option("msaa", HelpText = "Enable MSAA.")]
        public bool UseMsaa { get; set; }

        [Option("msaa-count", DefaultValue = 4, HelpText = "Samples count for MSAA.")]
        public int MsaaSampleCount { get; set; }

        [Option("software-downscale", HelpText = "Use software downscale, notably slower.")]
        public bool SoftwareDownsize { get; set; }

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

        [Option("blur-mirror", HelpText = "Blur flat mirror (if enabled).")]
        public bool FlatMirrorBlurred { get; set; } = false;

        [Option("mirror-reflectiveness", HelpText = "Reflectiveness level for flat mirror.")]
        public double FlatMirrorReflectiveness { get; set; } = 1.0;

        [Option("reflection-multiplier", HelpText = "Reflection multiplier for materials.")]
        public double ReflectionMultiplier { get; set; } = 1.0;

        [Option("reflection-from-camera", HelpText = "Build reflection cubemap at camera position (only with showroom).")]
        public bool ReflectionCubemapAtCamera { get; set; }

        [Option("no-shadows-with-reflections", HelpText = "Another option to match Kunos apps.")]
        public bool NoShadowsWithReflections { get; set; }

        [Option("ssao", HelpText = "Enable screen-space ambient occlusion.")]
        public bool UseSsao { get; set; } = false;

        [Option("sslr", HelpText = "Enable screen-space local reflections.")]
        public bool UseSslr { get; set; } = false;

        [Option("background", HelpText = "Background color.")]
        public string BackgroundColor { get; set; } = "#000000";

        [Option("light", HelpText = "Light color.")]
        public string LightColor { get; set; } = "#ffffff";

        [Option("light-direction", DefaultValue = "0.2, 1.0, 0.8", HelpText = "Light direction.")]
        public string LightDirection { get; set; }

        [Option("shadows", HelpText = "Enable shadows.")]
        public bool EnableShadows { get; set; } = false;

        [Option("shadows-size", HelpText = "Shadows resolution.")]
        public int ShadowMapSize { get; set; } = 4096;

        [Option("pcss", HelpText = "Enable smartly blurred shadows.")]
        public bool UsePcss { get; set; } = false;

        [Option("ambient-from", HelpText = "First ambient color (at the bottom).")]
        public string AmbientDown { get; set; } = "#96b4b4";

        [Option("ambient-to", HelpText = "Second ambient color (at the top).")]
        public string AmbientUp { get; set; } = "#b4b496";

        [Option("ambient-brightness", HelpText = "Ambient multiplier.")]
        public double AmbientBrightness { get; set; } = 2d;

        [Option("light-brightness", HelpText = "Light multiplier.")]
        public double LightBrightness { get; set; } = 1.5;

        [Option("tone-mapping", HelpText = "Enable tone mapping.")]
        public bool UseToneMapping { get; set; } = false;

        [Option("exposure", HelpText = "Exposure for tone mapping.")]
        public double ToneExposure { get; set; } = 0.8;

        [Option("gamma", HelpText = "Gamma for tone mapping.")]
        public double ToneGamma { get; set; } = 0.8;

        [Option("white-point", HelpText = "Whit point for tone mapping.")]
        public double WhitePoint { get; set; } = 1.66;

        [Option("color-grading", HelpText = "Color grading filename.")]
        public string ColorGradingFilename { get; set; } = null;

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
                Copyright = new CopyrightInfo("AcClub", 2020),
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