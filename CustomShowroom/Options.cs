using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace CustomShowroom {
    public class Options {
        [Option('m', "mode", DefaultValue =
#if DEBUG
                Mode.Lite
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
}