using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AcTools.AcdFile;
using AcTools.Utils.Helpers;

namespace AcTools.DataFile {
    public class LutDataFile : AbstractDataFile {
        public LutDataFile(string carDir, string filename, Acd loadedAcd) : base(carDir, filename, loadedAcd) {}
        public LutDataFile(string carDir, string filename) : base(carDir, filename) {}
        public LutDataFile(string filename) : base(filename) {}
        public LutDataFile() {}

        public readonly Dictionary<double, double> Values = new Dictionary<double, double>(); 

        protected override void ParseString(string file) {
            Clear();

            var lines = file.Split('\n');
            foreach (var val in lines.Select(line => line.Trim().Split('|'))) {
                double key, value;
                if (!FlexibleParser.TryParseDouble(val[0], out key) || !FlexibleParser.TryParseDouble(val[1], out value)) {
                    continue;
                }

                Values[key] = value;
            }
        }

        public override void Clear() {
            Values.Clear();
        }

        public override string Stringify() {
            var sb = new StringBuilder(Values.Count * 4);
            foreach (var pair in Values.OrderBy(x => x.Key)) {
                sb.Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append("|")
                    .Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append("\n");
            }
            return sb.ToString();
        }

        public bool IsEmptyOrDamaged() {
            return Values.Count == 0;
        }
    }
}