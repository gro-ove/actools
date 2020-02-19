using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using CommandLine.Text;
using LicensePlates;

namespace LicensePlatesGenerator {
    internal class Options {
        [ValueList(typeof(List<string>), MaximumElements = -1)]
        public IList<string> Params { get; set; }

        [Option('s', "style", Required = true, HelpText = "Directory with style.")]
        public string Style { get; set; }

        [Option('p', "preview", HelpText = "Preview (more primitive) mode.")]
        public bool PreviewMode { get; set; }

        [Option("diffuse", DefaultValue = "Plate_D", HelpText = "Name for diffuse texture.")]
        public string DiffuseName { get; set; }

        [Option("normals", DefaultValue = "Plate_NM", HelpText = "Name for normals texture.")]
        public string NormalsName { get; set; }

        [Option('f', "format", DefaultValue = "png", HelpText = "Result format (such as PNG or DDS).")]
        public string Format { get; set; }

        [Option('d', "destination", HelpText = "Destination directory.")]
        public string Target { get; set; }

        [Option('l', "list-params", HelpText = "List style params.")]
        public bool ListParams { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        public string GetUsage() {
            var help = new HelpText {
                Heading = new HeadingInfo("License Plates Generator",
                        FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()?.Location ?? "").FileVersion),
                Copyright = new CopyrightInfo("AcClub", 2017),
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("\r\nThis is free software. You may redistribute copies of it under the terms of");
            help.AddPreOptionsLine("the MS-PL License <https://opensource.org/licenses/MS-PL>.");
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("Usage: LicensePlatesGenerator -s <style> -d <destination> [params]");
            help.AddPreOptionsLine("       LicensePlatesGenerator -s <style> -l");
            help.AddOptions(this);
            return help;
        }
    }

    public static class Program {
#if PLATFORM_X86
        private static readonly string Platform = "x86";
#else
        private static readonly string Platform = "x64";
#endif

        [STAThread]
        private static int Main(string[] a) {
            var helper = new PackedHelper("AcTools_LicensePlatesGenerator", "References", null);
            helper.PrepareUnmanaged($"Magick.NET-Q8-{Platform}.Native");
            AppDomain.CurrentDomain.AssemblyResolve += helper.Handler;

            try {
                return MainInner(a);
            } catch (Exception e) {
                Console.Error.WriteLine("Fatal error: " + e.Message + ".");
                Console.ReadLine();
                return 10;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int MainInner(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;

            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) {
                Console.Error.WriteLine(options.GetUsage());
                return 1;
            }

            try {
                using (var style = new LicensePlatesStyle(new DirectoryInfo(options.Style).FullName)) {
                    if (options.ListParams) {
                        Console.WriteLine("Style params:");
                        foreach (var p in style.InputParams) {
                            if (p is InputSelectValue s) {
                                Console.WriteLine($"- {s.Name}: {string.Join(", ", s.Values)}");
                            } else {
                                if (p is InputNumberValue n) {
                                    Console.WriteLine($"- {n.Name}: from {n.From} to {n.To}");
                                } else {
                                    if (p is InputTextValue t) {
                                        Console.WriteLine($"- {t.Name}: {(t.LengthMode == InputLength.Varying ? "up to " : "")}{t.Length} symbols");
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(p.RandomFunc?.Invoke())) {
                                Console.WriteLine($"    default value: {p.RandomFunc}");
                            }
                        }
                        return 0;
                    }

                    foreach (var param in options.Params) {
                        var s = param.Split(new[] { ':' }, 2);
                        if (s.Length != 2) {
                            Console.Error.WriteLine("Param should be in format “<name>:<value>”");
                            continue;
                        }

                        var p = style.InputParams.FirstOrDefault(x => string.Equals(
                                Regex.Replace(x.Name, @"\W+", ""),
                                Regex.Replace(s[0], @"\W+", ""), StringComparison.OrdinalIgnoreCase));
                        if (p == null) {
                            if (s[0].Length == 1) {
                                p = style.InputParams.FirstOrDefault(x => x.Name.Length > 0 && string.Equals(
                                        Regex.Replace(x.Name, @"\W+", "").Substring(0, 1),
                                        s[0], StringComparison.OrdinalIgnoreCase));
                            }

                            if (p == null) {
                                Console.Error.WriteLine($"Param with name “{s[0]}” not found");
                                continue;
                            }
                        }

                        p.Value = s[1];
                    }

                    style.CreateDiffuseMap(options.PreviewMode, Path.Combine(options.Target ?? "", $"{options.DiffuseName}.{options.Format.ToLowerInvariant()}"));
                    style.CreateNormalsMap(options.PreviewMode, Path.Combine(options.Target ?? "", $"{options.NormalsName}.{options.Format.ToLowerInvariant()}"));
                }

                return 0;
            } catch (Exception e) {
                Console.Error.WriteLine(e.ToString());
                return 2;
            }
        }
    }
}