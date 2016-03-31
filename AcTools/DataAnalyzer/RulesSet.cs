using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AcTools.DataAnalyzer {
    public partial class RulesSet {
        public enum RuleType {
            Number, String, Vector2, Vector3, Vector4, File
        }

        public static RuleType RuleTypeFromString(string s) {
            switch (s.ToLower()) {
                case "file":
                    return RuleType.File;
                    
                case "string":
                    return RuleType.String;

                case "number":
                    return RuleType.Number;
                    
                case "vector":
                case "vector3":
                    return RuleType.Vector3;

                case "vector2":
                    return RuleType.Vector2;

                case "vector4":
                    return RuleType.Vector4;

                default:
                    throw new InvalidDataException();
            }
        }

        public class Rule {
            public RuleType Type;
            public string Filename, Section, Property;
            public string[] Params;

            public double GetDoubleParam(int index, double defaultValue) {
                return index < Params.Length ? double.Parse(Params[index], CultureInfo.InvariantCulture) : defaultValue;
            }

            public override string ToString() {
                return Filename + "/" + Section + "/" + Property;
            }

            /* internal Rule(RuleType type, string filename, string section, string[] arguments) {
                
             }*/
        }

        private readonly Rule[] _rules;

        public readonly string Id;

        private RulesSet(string id, Rule[] rules) {
            Id = id;
            _rules = rules;
        }

        public static RulesSet FromLines(string[] strings) {
            var id = strings[0].Trim();
            if (!Regex.IsMatch(id, @"^\w+$")) {
                throw new InvalidDataException();
            }

            var regex = new Regex(@"\s{2,}", RegexOptions.Compiled);
            var list = new List<Rule>(strings.Length);

            string filename = null, section = null;
            foreach (var raw in strings.Skip(1)) {
                var splitted = regex.Split(raw.Trim());
                if (splitted.Length == 0 || splitted[0].Length == 0) continue;

                var path = splitted[0].Split(new [] { '/' }, 3);
                var type = RuleTypeFromString(splitted.Length == 1 ? "number" : splitted[1]);

                if (type == RuleType.File) {
                    throw new NotImplementedException();
                } else {
                    var property = path[path.Length - 1];

                    if (path.Length > 1) {
                        section = path[path.Length - 2];
                    }

                    if (path.Length > 2) {
                        filename = path[path.Length - 3];
                    }

                    if (filename == null || section == null) {
                        Console.Error.WriteLine("invalid field '{0}/{1}/{2}'", filename, section, property);
                        continue;
                    }

                    list.Add(new Rule {
                        Type = type,
                        Filename = filename,
                        Section = section,
                        Property = property,
                        Params = splitted.Skip(2).ToArray()
                    });
                }
            }

            return new RulesSet(id, list.ToArray());
        }

        public static RulesSet FromText(string text) {
            return FromLines(text.Split('\n'));
        }

        public static RulesSet FromFile(string filename) {
            return FromLines(File.ReadAllLines(filename));
        }
    }
}
