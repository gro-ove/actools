using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
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

        [Option('g', "magick", DefaultValue = false, HelpText = "Load textures from PSD files if exist.")]
        public bool MagickOverride { get; set; }

        [Option('x', "fxaa", DefaultValue = true, HelpText = "Use FXAA.")]
        public bool UseFxaa { get; set; }

        [Option('s', "showroom", DefaultValue = null, HelpText = "Specific showroom ID (only for the Custom Showroom mode).")]
        public string ShowroomId { get; set; }

        [Option('t', "extract-texture", DefaultValue = null, HelpText = "Texture for which UV will be extracted.")]
        public string ExtractUvTexture { get; set; }

        [Option('v', "verbose", DefaultValue = false, HelpText = "Write some stuff to Log.txt near to exe-file.")]
        public bool Verbose { get; set; }

        [Option('h', "help", DefaultValue = false, HelpText = "Show help message.")]
        public bool Help { get; set; }

        [ValueList(typeof(List<string>), MaximumElements = 2)]
        public IList<string> Items { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        public string GetUsage() {
            var help = new HelpText {
                Heading = new HeadingInfo("Custom Showroom", FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location ?? "").FileVersion),
                Copyright = new CopyrightInfo("AcClub", 2017),
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("\r\nThis is free software. You may redistribute copies of it under the terms of");
            help.AddPreOptionsLine("the MS-PL License <https://opensource.org/licenses/MS-PL>.");
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("Usage: CustomShowroom <model.kn5>");
            help.AddOptions(this);
            return help;
        }
    }
}