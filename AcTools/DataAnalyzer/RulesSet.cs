using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.DataAnalyzer {
    public partial class RulesSet {
        public enum RuleType {
            Number,
            String,
            Vector2,
            Vector3,
            Vector4,
            Lut
        }

        public class Rule {
            public RuleType Type;
            public string FileName, Section, Property;
            public string[] Params;
            public double?[] DoubleParams;
            public Func<double, bool>[] Tests;
            public double Weight;

            public double GetDoubleParam(int index, double defaultValue) {
                if (DoubleParams == null) {
                    DoubleParams = Params.Select(x => FlexibleParser.TryParseDouble(x)).ToArray();
                }

                return index < Params.Length ? DoubleParams[index] ?? defaultValue : defaultValue;
            }

            public override string ToString() {
                return FileName + "/" + Section + "/" + Property;
            }

            protected bool Equals(Rule other) {
                return Type == other.Type && string.Equals(FileName, other.FileName) && string.Equals(Section, other.Section) &&
                        string.Equals(Property, other.Property) && Equals(Params, other.Params);
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Rule)obj);
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = (int)Type;
                    hashCode = (hashCode * 397) ^ (FileName?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (Section?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (Property?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (Params?.GetEnumerableHashCode() ?? 0);
                    return hashCode;
                }
            }
        }

        private readonly Rule[] _rules;

        public int Size => _rules.Length;

        public override int GetHashCode() {
            return _rules.GetEnumerableHashCode();
        }

        private RulesSet(Rule[] rules) {
            _rules = rules;
        }

        private static readonly Regex FileNameRegex = new Regex(@"^[\w-]+\.[\w]{2,4}$", RegexOptions.Compiled);
        private static readonly Regex SpacesRegex = new Regex(@"\t|\s{2,}", RegexOptions.Compiled);

        private static RuleType RuleTypeFromString([NotNull] string s) {
            return (RuleType)Enum.Parse(typeof(RuleType), s, true);
        }

        [CanBeNull]
        private static Func<double, bool> GetTest([NotNull] string extra) {
            if (extra.Length < 1) return null;
            var v = FlexibleParser.TryParseDouble(extra.Substring(1)) ?? 0d;
            switch (extra[0]) {
                case '=': return c => c == v;
                case '≠': return c => c != v;
                case '<': return c => c < v;
                case '>': return c => c > v;
                case '≤': return c => c <= v;
                case '≥': return c => c >= v;
            }
            return null;
        }

        [NotNull]
        private static RulesSet FromLines([NotNull] string[] strings) {
            var list = new List<Rule>(strings.Length);
            string fileName = null, section = null;
            Match sectionParsed = null;
            IEnumerable<string> sectionNumbered = null;
            foreach (var raw in strings) {
                var splitted = SpacesRegex.Split(raw.Trim());
                if (splitted.Length == 0 || splitted[0].Length == 0) continue;

                var path = splitted[0].Split(new [] { '/' }, 3);

                string property = null;
                if (FileNameRegex.IsMatch(path[0])) {
                    fileName = path[0];
                    section = path.Length >= 2 ? path[1] : null;
                    sectionParsed = null;

                    if (path.Length >= 3) {
                        property = path[2];
                    } else if (splitted.Length == 1) {
                        continue;
                    }
                } else {
                    property = path[path.Length - 1];
                    if (path.Length > 1) {
                        section = path[path.Length - 2];
                        sectionParsed = null;
                    }
                    if (path.Length > 2) {
                        fileName = path[path.Length - 3];
                    }
                }

                var type = RuleTypeFromString(splitted.Length == 1 ? "number" : splitted[1]);

                if (type != RuleType.Lut) {
                    if (fileName == null || section == null) {
                        Console.Error.WriteLine("Invalid field '{0}/{1}/{2}'", fileName, section, property);
                        continue;
                    }
                }

                var additionalParams = splitted.Skip(2).ToList();
                var extra = additionalParams.Where(x => x.StartsWith("×") ||
                        x.StartsWith("=") || x.StartsWith("≠") ||
                        x.StartsWith("<") || x.StartsWith(">") ||
                        x.StartsWith("≤") || x.StartsWith("≥")).ToList();
                var actualParams = additionalParams.ApartFrom(extra).ToArray();

                var weight = FlexibleParser.TryParseDouble(extra.FirstOrDefault(x => x.StartsWith("×"))?.Substring(1)) ?? 1d;
                var tests = extra.Select(GetTest).NonNull().ToArray();

                if (sectionParsed == null && section != null) {
                    sectionParsed = Regex.Match(section, @"(.*)\{(.+)\}(.*)");
                    if (sectionParsed.Success) {
                        var prefix = sectionParsed.Groups[1].Value;
                        var postfix = sectionParsed.Groups[3].Value;
                        sectionNumbered = Diapason.CreateInt32(sectionParsed.Groups[2].Value).SetLimits(0, 100).Select(x => prefix + x + postfix);
                    } else {
                        sectionNumbered = new[] { section };
                    }
                }

                list.AddRange(from s in sectionNumbered ?? new string[0]
                              select new Rule {
                                  Type = type, FileName = fileName, Section = s, Property = property, Params = actualParams, Tests = tests, Weight = weight
                              });
            }

            return new RulesSet(list.ToArray());
        }

        [NotNull]
        public static RulesSet FromText([NotNull] string text) {
            return FromLines(text.Split('\n'));
        }

        [NotNull]
        public static RulesSet FromFile([NotNull] string filename) {
            return FromLines(File.ReadAllLines(filename));
        }
    }
}
