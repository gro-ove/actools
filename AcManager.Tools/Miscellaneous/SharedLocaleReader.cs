using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using SharpCompress.Archive.Zip;

namespace AcManager.Tools.Miscellaneous {
    [Localizable(false)]
    public static class SharedLocaleReader {
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
                                    .Descendants(n + "c").Select(x => new { Node = x, Location = x.Attribute("r")?.Value }).ToList();
                foreach (var x in c.Where(x => x.Location?.StartsWith("B") == true && x.Location != "B1")) {
                    var a = x.Location.Replace("B", "A");
                    var d = x.Location.Replace("B", "D");
                    var g = s.ElementAtOrDefault(c.FirstOrDefault(y => y.Location == a)?.Node.Value.AsInt() ?? -1);
                    var t = s.ElementAtOrDefault(c.FirstOrDefault(y => y.Location == d)?.Node.Value.AsInt() ?? -1);
                    var k = s.ElementAtOrDefault(x.Node.Element(n + "v")?.Value.AsInt() ?? -1);
                    if (k == null || t == null || g == null) continue;

                    Dictionary<string, string> gd;
                    if (r.TryGetValue(g, out gd)) {
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
