using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace CustomShowroom {
    public class Options {
        [Option('m', "mode", DefaultValue =
#if DEBUG
                Mode.Dark
#else
                Mode.Dark
#endif
                , HelpText = "App mode (apart from superior Dark, there are Fancy and Lite for special purposes).")]
        public Mode Mode { get; set; }

        [Option("msaa", DefaultValue = false, HelpText = "Use MSAA (only for Lite/Dark Showroom modes).")]
        public bool UseMsaa { get; set; }

        [Option('a', "ssaa", DefaultValue = false, HelpText = "Use SSAA (only for Lite/Dark Showroom modes).")]
        public bool UseSsaa { get; set; }

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
}