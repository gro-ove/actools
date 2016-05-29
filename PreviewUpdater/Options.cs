using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PreviewUpdater {
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

        [Option('r', "root", Required = true, HelpText = "AC root folder.")]
        public string AcRoot { get; set; }

        [Option('s', "showroom", DefaultValue = "previews", HelpText = "Showroom to shot previews at.")]
        public string Showroom { get; set; }

        [Option('c', "camera", DefaultValue = "-3.867643, 1.42359, 4.70381", HelpText = "Camera position.")]
        public string CameraPosition { get; set; }

        [Option('l', "look-at", DefaultValue = "0.0, 0.7, 0.5", HelpText = "Look at.")]
        public string LookAt { get; set; }

        [Option('v', "fov", DefaultValue = 30.0, HelpText = "Field of view.")]
        public double Fov { get; set; }

        [Option('e', "exposure", DefaultValue = 0.0, HelpText = "Exposure (looks like doesn't work).")]
        public double Exposure { get; set; }

        [Option('f', "filter", DefaultValue = "S1-Showroom", HelpText = "PP filter.")]
        public string Filter { get; set; }

        [Option('a', "fxaa", DefaultValue = PseudoBool.True, HelpText = "Enable FXAA.")]
        public PseudoBool FxaaInner { get; set; }
        public bool Fxaa => (int)FxaaInner == 1;

        [Option('m', "maximize-settings", DefaultValue = PseudoBool.True, HelpText = "Maximize video settings.")]
        public PseudoBool MaximizeVideoInner { get; set; }
        public bool MaximizeVideo => (int)MaximizeVideoInner == 1;

        [Option('4', "4k-resolution", DefaultValue = PseudoBool.True, HelpText = "Shot in 3840x2160 for the best quality.")]
        public PseudoBool SpecialResolutionInner { get; set; }
        public bool SpecialResolution => (int)SpecialResolutionInner == 1;

        [Option('w', "without-previews-only", DefaultValue = false, HelpText = "Update skins only without previews.")]
        public bool WithoutPreviews { get; set; }

        [Option('t', "max-attempt", DefaultValue = 2, HelpText = "Max attempts per car if shooting is failed.")]
        public int MaxAttempts { get; set; }

        [Option('i', "ignore-errors", DefaultValue = false, HelpText = "Move to a next car if attempts number is exceeded.")]
        public bool IgnoreErrors { get; set; }

        [Option("filter-test", DefaultValue = false, HelpText = "Just list filtered cars and exit.")]
        public bool FilterTest { get; set; }

        [Option("verbose", HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}