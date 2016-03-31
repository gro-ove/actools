using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Kn5Render.Kn5Render;
using AcTools.Kn5Render.Utils;
using AcTools.Utils;

namespace TrackMapGenerator {
    class Program {
        static void PrintNode(Kn5Node node, int spaces = 0) {
            for (var i = -1; i < spaces; i++) {
                Console.Write(@"  ");
            }

            if (node.NodeClass == Kn5NodeClass.Base) {
                Console.WriteLine(@"{0}:", node.Name);

                foreach (var child in node.Children) {
                    PrintNode(child, spaces + 1);
                }
            } else {
                Console.WriteLine(@"{0}", node.Name);
            }
        }

        static int Main(string[] args) {
            if (args.Length == 0) return 1;

            var kn5File = args[0];
            var trackDir = Path.GetDirectoryName(kn5File);
            
            if (trackDir == null || !Directory.Exists(trackDir)) return 2;

            Console.WriteLine(@"Track: " + kn5File);
            using (var render = new Render(kn5File, 0, Render.VisualMode.TRACK_MAP)) {
                Console.WriteLine(@"Track loaded");
                Console.WriteLine(@"  Nodes: {0}", render.LoadedKn5.NodesCount);
                Console.WriteLine(@"  Triangles: {0}", render.LoadedKn5.RootNode.TotalTrianglesCount);
                Console.WriteLine(@"  Vertices: {0}", render.LoadedKn5.RootNode.TotalVerticesCount);

                var surfaces = new List<string>();

                foreach (var surfacesFile in Directory.GetDirectories(trackDir).Select(sub => Path.Combine(sub, "data", "surfaces.ini")).Where(File.Exists)) {
                    surfaces.AddRange(from x in File.ReadAllLines(surfacesFile) 
                                        where x.Trim().StartsWith("KEY=") 
                                        select x.Substring(4));
                }

                var dataDir = Path.Combine(trackDir, "data");
                if (!Directory.Exists(dataDir)) {
                    Directory.CreateDirectory(dataDir);
                } else {
                    var surfacesFile = Path.Combine(dataDir, "surfaces.ini");
                    if (File.Exists(surfacesFile)) {
                        surfaces.AddRange(from x in File.ReadAllLines(surfacesFile) 
                                          where x.Trim().StartsWith("KEY=") 
                                          select x.Substring(4));
                    }
                }

                Console.WriteLine(@"  Found surfaces: {0}", string.Join(", ", surfaces));

                foreach (var filename in new[] {
                    Path.Combine(trackDir, "map.png"), 
                    Path.Combine(dataDir, "map.ini")
                }.Where(File.Exists)) {
                    FileUtils.Recycle(filename);
                }
            
                for (var i = 0;; i++) {
                    Regex surfaceFilter = null;

                    if (i == 0) {
                        Console.WriteLine(@"Trying to render ROAD|ASPHALT surface");
                        surfaceFilter = new Regex("ROAD|ASPHALT", RegexOptions.Compiled);
                    } else {
                        while (surfaceFilter == null) {
                            var input = Console.ReadLine();
                            if (input == null) return 0;

                            if (input.Length == 0) {
                                PrintNode(render.LoadedKn5.RootNode);
                                continue;
                            }

                            try {
                                surfaceFilter = new Regex(input, RegexOptions.Compiled);
                            } catch (ArgumentException e) {
                                Console.WriteLine(@"Invalid regular expression: " + e.Message);
                            }
                        }
                    }

                    try {
                        Render.TrackMapInformation information;

                        var mapImage = Path.Combine(trackDir, "map.png");
                        render.ShotTrackMap(mapImage, surfaceFilter, out information);
                        information.SaveTo(Path.Combine(dataDir, "map.ini"));

                        Process.Start(mapImage);
                    } catch (ShotException e) {
                        Console.WriteLine(@"Error: " + e.Message);
                    } catch (Exception e) {
                        Console.WriteLine(@"Unexpected error: " + e);
                    }

                    surfaceFilter = null;
                    Console.WriteLine(@"Try again?");
                    if (i == 0) {
                        Console.WriteLine(@"Input regular expression to filter required surface (keep input empty to see all nodes):");
                    }
                    Console.ReadKey();
                }
            }
        }
    }
}
