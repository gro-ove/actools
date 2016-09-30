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

        protected override void ParseString(string data) {
            Clear();
            
            var started = -1;
            var key = double.NaN;
            var malformed = -1;
            var line = 1;

            for (var i = 0; i < data.Length; i++) {
                switch (data[i]) {
                    case '|':
                        if (started != -1) {
                            if (!double.IsNaN(key) || !FlexibleParser.TryParseDouble(data.Substring(started, i - started), out key)) {
                                if (malformed == -1) malformed = line;
                                SkipLine(data, ref i, ref line);
                                key = double.NaN;
                            }
                            started = -1;
                        }
                        break;

                    case '\n':
                        Finish(Values, data, i, line, ref key, ref started, ref malformed);
                        line++;
                        break;

                    case '/':
                        if (i + 1 < data.Length && data[i + 1] == '/') goto case ';';
                        goto default;

                    case ';':
                        Finish(Values, data, i, line, ref key, ref started, ref malformed);
                        SkipLine(data, ref i, ref line);
                        break;

                    default:
                        if (started == -1) started = i;
                        break;
                }
            }

            Finish(Values, data, data.Length, line, ref key, ref started, ref malformed);

            if (malformed != -1) {
                ErrorsCatcher?.Catch(this, malformed);
            }
        }

        private static void SkipLine(string data, ref int index, ref int line) {
            do { index++; } while (index < data.Length && data[index] != '\n');
            line++;
        }

        private static void Finish(Dictionary<double, double> values, string data, int index, int line, ref double key, ref int started, ref int malformed) {
            if (started != -1) {
                if (double.IsNaN(key)) {
                    if (malformed == -1) malformed = line;
                } else {
                    double value;
                    if (FlexibleParser.TryParseDouble(data.Substring(started, index - started), out value)) {
                        values[key] = value;
                    } else {
                        if (malformed == -1) malformed = line;
                    }
                    key = double.NaN;
                }
                started = -1;
            } else if (!double.IsNaN(key)) {
                key = double.NaN;
            }
        }

        protected static Dictionary<double, double> ParseStringNew(string data) {
            var Values = new Dictionary<double, double>();

            return Values;
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