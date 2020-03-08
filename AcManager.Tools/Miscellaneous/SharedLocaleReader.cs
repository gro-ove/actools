using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using SharpCompress.Archives.Zip;

namespace AcManager.Tools.Miscellaneous {
    [Localizable(false)]
    public static class SharedLocaleReader {
        private class Cell {
            public Cell(XElement node) {
                Node = node;
                Location = node.Attribute("r")?.Value ?? "";
            }

            public XElement Node { get; }

            public string Location { get; }
        }

        private static string GetString(this IEnumerable<Cell> source, string location, IEnumerable<string> strings) {
            var cell = source.FirstOrDefault(x => x.Location == location);
            if (string.IsNullOrEmpty(cell?.Node.Value)) return null;

            var id = cell.Node.Value.As<int>();
            return strings.ElementAtOrDefault(id);
        }

        [NotNull, Pure]
        public static Dictionary<string, Dictionary<string, string>> Read([NotNull] string filename, [NotNull] string localeId) {
            var r = new Dictionary<string, Dictionary<string, string>>();
            using (var zip = ZipArchive.Open(filename)) {
                var n = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                var s = XDocument.Parse(zip.Entries.First(x => x.Key == "xl/sharedStrings.xml")
                                            .OpenEntryStream().ReadAsStringAndDispose())
                                    .Descendants(n + "si").Select(x => x.Element(n + "t")?.Value ?? "<NULL>").ToList();
                var i = XDocument.Parse(zip.Entries.First(x => x.Key == "xl/workbook.xml")
                                            .OpenEntryStream().ReadAsStringAndDispose())
                                    .Descendants(n + "sheet")
                                    .FirstOrDefault(x => x.Attribute("name")?.Value.IndexOf($"({localeId})", StringComparison.OrdinalIgnoreCase) != -1)?
                                    .Attribute("sheetId")?.Value;
                if (i == null) return r;
                var c = XDocument.Parse(zip.Entries.First(x => x.Key == $"xl/worksheets/sheet{i.ToInvariantString()}.xml")
                                            .OpenEntryStream().ReadAsStringAndDispose())
                                    .Descendants(n + "c").Select(x => new Cell(x)).ToList();
                foreach (var x in c.Where(x => x.Location?.StartsWith("B") == true && x.Location != "B1")) {
                    var g = c.GetString(x.Location.Replace("B", "A"), s);
                    var t = c.GetString(x.Location.Replace("B", "D"), s);
                    var k = s.ElementAtOrDefault(x.Node.Element(n + "v")?.Value.As<int>() ?? -1);
                    if (k == null || t == null || g == null) continue;
                    t = t.TrimEnd('\n');
                    if (r.TryGetValue(g, out var gd)) {
                        gd.Add(k, t);
                    } else {
                        gd = new Dictionary<string, string>(200) { { k, t } };
                        r[g] = gd;
                    }
                }

                return r;
            }
        }
    }
}
