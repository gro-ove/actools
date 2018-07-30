using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public partial class WeatherProceduralHelper {
        public class LuaIniFile : IEnumerable<KeyValuePair<string, LuaIniSection>> {
            public LuaIniFile(string source) : this(new IniFile(File.Exists(source + @"~cm_bak") ? source + @"~cm_bak" : source)) { }

            public LuaIniFile(IniFile source) {
                foreach (var pair in source) {
                    var section = this[pair.Key];
                    foreach (var value in pair.Value) {
                        section[value.Key] = ToLuaValue(value.Key, value.Value);
                    }
                }
            }

            private static object ToLuaValue(string key, string iniValue) {
                if (string.IsNullOrWhiteSpace(iniValue)) return null;

                var pieces = iniValue.Split(',');
                if (pieces.Length == 1) {
                    return pieces[0].Trim();
                }

                if (key == @"LIST") {
                    return pieces.ToList();
                }

                var numbers = new double[pieces.Length];
                for (var i = 0; i < pieces.Length; i++) {
                    var piece = pieces[i].Trim();
                    pieces[i] = piece;
                    numbers[i] = piece == string.Empty ? 0d : piece.As(double.NaN);
                }

                return new Vector(numbers);
            }

            private readonly Dictionary<string, LuaIniSection> _content = new Dictionary<string, LuaIniSection>();

            public Dictionary<string, LuaIniSection>.KeyCollection Keys => _content.Keys;
            public Dictionary<string, LuaIniSection>.ValueCollection Values => _content.Values;

            [NotNull]
            public LuaIniSection this[[NotNull, LocalizationRequired(false)] string key] {
                get {
                    if (_content.TryGetValue(key, out var result)) return result;
                    result = new LuaIniSection();
                    _content[key] = result;
                    return result;
                }
            }

            public void Clear() {
                _content.Clear();
            }

            public int Count => _content.Count;

            public IEnumerator<KeyValuePair<string, LuaIniSection>> GetEnumerator() {
                return _content.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public class Vector : IEnumerable<double> {
            private readonly List<double> _list;

            public Vector() {
                _list = new List<double>();
            }

            public Vector([NotNull] IEnumerable<double> collection) {
                _list = new List<double>(collection);
            }

            public double X => this[1];
            public double Y => this[2];
            public double Z => this[3];
            public double W => this[4];
            public double R => this[1];
            public double G => this[2];
            public double B => this[3];
            public double A => this[4];

            // ReSharper disable InconsistentNaming
            public double x => this[1];
            public double y => this[2];
            public double z => this[3];
            public double w => this[4];
            public double r => this[1];
            public double g => this[2];
            public double b => this[3];
            public double a => this[4];
            // ReSharper restore InconsistentNaming

            public double this[int key] {
                get => key >= 1 && key <= _list.Count ? _list[key - 1] : 0d;
                set {
                    if (key < 1) return;
                    if (key <= _list.Count) {
                        _list[key - 1] = value;
                    } else {
                        while (_list.Count < key - 1) {
                            _list.Add(0d);
                        }
                        _list.Add(value);
                    }
                }
            }

            public void Clear() {
                _list.Clear();
            }

            public int Count => _list.Count;

            public static Vector operator ++(Vector a) {
                return a + 1;
            }

            public static Vector operator --(Vector a) {
                return a - 1;
            }

            public static Vector operator +(Vector a, double b) {
                return new Vector(a.Select(x => x + b));
            }

            public static Vector operator +(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x + y));
            }

            public static Vector operator -(Vector a, double b) {
                return new Vector(a.Select(x => x - b));
            }

            public static Vector operator -(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x - y));
            }

            public static Vector operator *(Vector a, double b) {
                return new Vector(a.Select(x => x * b));
            }

            public static Vector operator *(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x * y));
            }

            public static Vector operator /(Vector a, double b) {
                return new Vector(a.Select(x => x / b));
            }

            public static Vector operator /(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x / y));
            }

            public static Vector operator %(Vector a, double b) {
                return new Vector(a.Select(x => x % b));
            }

            public static Vector operator %(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x % y));
            }

            public static bool operator ==(Vector a, double b) {
                return a?.All(x => x == b) == true;
            }

            public static bool operator !=(Vector a, double b) {
                return !(a == b);
            }

            public static bool operator ==(double b, Vector a) {
                return b == a;
            }

            public static bool operator !=(double b, Vector a) {
                return !(b == a);
            }

            public static bool operator <(Vector a, double b) {
                return a?.All(x => x < b) == true;
            }

            public static bool operator >(Vector a, double b) {
                return a?.All(x => x > b) == true;
            }

            public static bool operator <=(Vector a, double b) {
                return a?.All(x => x <= b) == true;
            }

            public static bool operator >=(Vector a, double b) {
                return a?.All(x => x >= b) == true;
            }

            public static bool operator <(double b, Vector a) {
                return a < b;
            }

            public static bool operator >(double b, Vector a) {
                return a > b;
            }

            public static bool operator <=(double b, Vector a) {
                return a <= b;
            }

            public static bool operator >=(double b, Vector a) {
                return a >= b;
            }

            public static bool operator ==(Vector a, Vector b) {
                return a == b || a?.Equals(b) == true;
            }

            public static bool operator !=(Vector a, Vector b) {
                return !(a == b);
            }

            protected bool Equals(Vector other) {
                return this.SequenceEqual(other);
            }

            public IEnumerator<double> GetEnumerator() {
                return _list.GetEnumerator();
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GetType() == GetType() && Equals((Vector)obj);
            }

            public override int GetHashCode() {
                return this.GetEnumerableHashCode();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public class LuaIniSection : Dictionary<string, object> {
            public IEnumerable<KeyValuePair<string, string>> ToIniValues() {
                return this.Select(x => new KeyValuePair<string, string>(x.Key, ToIniFileValue(x.Value)));
            }
        }
    }
}