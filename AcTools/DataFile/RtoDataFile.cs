using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AcTools.Utils.Helpers;

namespace AcTools.DataFile {
    public class RtoDataFile : DataFileBase {
        public RtoDataFile(string filename) : base(filename) {}
        public RtoDataFile() {}

        public readonly Dictionary<string, double> Values = new Dictionary<string, double>();

        protected override void ParseString(string data) {
            Clear();

            foreach (var line in data.Split('\n')) {
                var sep = line.LastIndexOf('|');
                if (sep == -1) continue;

                double value;
                if (FlexibleParser.TryParseDouble(line.Substring(sep + 1), out value)) {
                    Values[line.Substring(0, sep).Replace("//", "/")] = value;
                }
            }
        }

        public override void Clear() {
            Values.Clear();
        }

        public override string Stringify() {
            var sb = new StringBuilder(Values.Count * 4);
            foreach (var pair in Values) {
                sb.Append(pair.Key).Append("|")
                  .Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append("\n");
            }
            return sb.ToString();
        }

        public bool IsEmptyOrDamaged() {
            return Values.Count == 0;
        }
    }
}