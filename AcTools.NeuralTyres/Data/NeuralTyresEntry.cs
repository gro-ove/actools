using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;

namespace AcTools.NeuralTyres.Data {
    public class NeuralTyresEntry : NeuralTyresSource {
        public static readonly string[] TemporaryKeys = {
            "PROFILE"
        };

        public readonly int Version;

        private readonly Dictionary<string, double> _values;
        private readonly Dictionary<string, Lut> _luts;

        public IEnumerable<string> Keys => _values.Keys;
        public IReadOnlyDictionary<string, Lut> Luts => _luts;

        public double this[string key] {
            get => _values.GetValueOrDefault(key);
            set {
                _values[key] = value;
                if (key == NeuralTyresOptions.InputProfile) {
                    _values[NeuralTyresOptions.InputRimRadius] = this[NeuralTyresOptions.InputRadius] - value;
                }
            }
        }

        public NeuralTyresEntry(string name, string shortName, int version) {
            Name = name;
            ShortName = shortName;
            Version = version;
            _values = new Dictionary<string, double>();
            // TODO: Luts
        }

        private NeuralTyresEntry(string carId, Tuple<int, IniFileSection, IniFileSection> data) {
            CarId = carId;
            Version = data.Item1;

            Name = data.Item2.GetNonEmpty("NAME");
            ShortName = data.Item2.GetNonEmpty("SHORT_NAME");

            _values = new Dictionary<string, double>(data.Item2.Count);
            foreach (var v in data.Item2) {
                _values[v.Key] = FlexibleParser.ParseDouble(v.Value, 0d);
            }

            foreach (var v in data.Item3) {
                _values["THERMAL@" + v.Key] = FlexibleParser.ParseDouble(v.Value, 0d);
            }

            _luts = new Dictionary<string, Lut> {
                ["WEAR_CURVE"] = data.Item2.GetLut("WEAR_CURVE"),
                ["THERMAL@PERFORMANCE_CURVE"] = data.Item3.GetLut("PERFORMANCE_CURVE")
            };

            foreach (var v in data.Item2) {
                _values[v.Key] = FlexibleParser.ParseDouble(v.Value, 0d);
            }

            this["PROFILE"] = this["RADIUS"] - this["RIM_RADIUS"];
        }

        public NeuralTyresEntry(string carId, int version, IniFileSection section, IniFileSection thermal)
                : this(carId, Tuple.Create(version, section, thermal)) { }

        public NeuralTyresEntry(DataWrapper data, bool isFront, int id)
                : this(Path.GetFileName(data.ParentDirectory), Load(data, isFront, id)) { }

        private static Tuple<int, IniFileSection, IniFileSection> Load(DataWrapper data, bool isFront, int id) {
            var tyresIniFile = data.GetIniFile("tyres.ini");
            var sectionKey = (isFront ? "FRONT" : "REAR") + (id == 0 ? "" : "_" + id);
            return Tuple.Create(
                    tyresIniFile["HEADER"].GetInt("VERSION", 0),
                    tyresIniFile[sectionKey], tyresIniFile["THERMAL_" + sectionKey]);
        }

        public static IEnumerable<NeuralTyresEntry> Get(DataWrapper data) {
            var front = data.GetIniFile("tyres.ini").GetSections("FRONT", -1).Select((x, i) => new NeuralTyresEntry(data, true, i));
            var rear = data.GetIniFile("tyres.ini").GetSections("REAR", -1).Select((x, i) => new NeuralTyresEntry(data, false, i));
            return front.Concat(rear);
        }

        protected bool Equals(NeuralTyresEntry other) {
            return Version == other.Version
                    && _values.SequenceEqual(other._values);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((NeuralTyresEntry)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Version;
                hashCode = (hashCode * 397) ^ (_values != null ? _values.GetEnumerableHashCode() : 0);
                return hashCode;
            }
        }
    }
}