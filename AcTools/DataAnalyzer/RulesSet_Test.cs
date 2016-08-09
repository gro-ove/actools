using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AcTools.AcdFile;
using AcTools.DataFile;

namespace AcTools.DataAnalyzer {
    public partial class RulesSet {
        public string GetHash(string carDir) {
            var acdFilename = Path.Combine(carDir, "data.acd");
            var acdFile = File.Exists(acdFilename) ? Acd.FromFile(acdFilename) : null;

            var files = new Dictionary<string, IniFile>();
            var result = new StringBuilder();

            foreach (var rule in _rules) {
                if (!files.ContainsKey(rule.Filename)) {
                    files[rule.Filename] = new IniFile(carDir, rule.Filename, acdFile);
                }

                var file = files[rule.Filename];

                if (file[rule.Section].ContainsKey(rule.Property)) {
                    var propertyValue = file[rule.Section].GetPossiblyEmpty(rule.Property)?.Trim();
                    if (propertyValue == null) continue;

                    switch (rule.Type) {
                        case RuleType.Vector2:
                        case RuleType.Vector3:
                        case RuleType.Vector4: {
                                if (propertyValue.Length == 0) break;

                                var value = propertyValue.Split(',').Select(x => {
                                    double parsed;
                                    double.TryParse(x.Trim(), NumberStyles.Any,
                                                 CultureInfo.InvariantCulture, out parsed);
                                    return parsed;
                                }).ToList();
                                var len = rule.Type == RuleType.Vector2 ? 2 : rule.Type == RuleType.Vector3 ? 3 : 4;
                                if (value.Count == len) {
                                    for (var i = 0; i < len; i++) {
                                        if (i > 0) {
                                            result.Append(",");
                                        }

                                        value[i] /= rule.GetDoubleParam(i, rule.GetDoubleParam(0, 1.0));
                                        result.Append(Math.Round(value[i]).ToString("F0"));
                                    }
                                }
                                break;
                            }

                        case RuleType.Number: {
                                double parsed;
                                double.TryParse(propertyValue, NumberStyles.Any, CultureInfo.InvariantCulture,
                                                out parsed);

                                parsed /= rule.GetDoubleParam(0, 1.0);
                                result.Append(Math.Round(parsed).ToString("F0"));
                                break;
                            }

                        case RuleType.String: {
                                result.Append(propertyValue);
                                break;
                            }
                    }
                }

                result.Append("\n");
            }

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(result.ToString()));
        }

        public static double CompareHashes(string lhs, string rhs) {
            var lhsLines = Encoding.UTF8.GetString(Convert.FromBase64String(lhs)).Split('\n');
            var rhsLines = Encoding.UTF8.GetString(Convert.FromBase64String(rhs)).Split('\n');

            var size = lhsLines.Length - 1;
            if (size != rhsLines.Length - 1) return 0.0;

            var same = 0;
            for (var i = 0; i < size; i++) {
                if (lhsLines[i] == rhsLines[i]) {
                    same++;
                }
            }

            return (double)same / size;
        }

        public static double CompareHashes(string lhs, string rhs, RulesSet rules, out Rule[] workedRules) {
            var lhsLines = Encoding.UTF8.GetString(Convert.FromBase64String(lhs)).Split('\n');
            var rhsLines = Encoding.UTF8.GetString(Convert.FromBase64String(rhs)).Split('\n');

            var size = lhsLines.Length - 1;
            if (size != rhsLines.Length - 1 || size != rules._rules.Length) {
                workedRules = new Rule[]{};
                return 0.0;
            }

            var workedRulesList = new List<Rule>();

            var same = 0;
            for (var i = 0; i < size; i++) {
                if (lhsLines[i] == rhsLines[i]) {
                    workedRulesList.Add(rules._rules[i]);
                    same++;
                }
            }

            workedRules = workedRulesList.ToArray();
            return (double)same / size;
        }
    }
}
