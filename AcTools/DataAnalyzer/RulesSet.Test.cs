using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using JetBrains.Annotations;

namespace AcTools.DataAnalyzer {
    public partial class RulesSet {
        [NotNull]
        public byte[] GetHash([NotNull] string carDir) {
            return GetHash(DataWrapper.FromCarDirectory(carDir));
        }

        [NotNull, Pure]
        public byte[] GetHash([NotNull] DataWrapper data) {
            using (var memory = new MemoryStream(_rules.Length * sizeof(int))) {
                using (var writer = new BinaryWriter(memory)) {
                    for (var i = 0; i < _rules.Length; i++) {
                        var r = _rules[i];
                        switch (r.Type) {
                            case RuleType.Vector2:
                                writer.Write(data.GetIniFile(r.FileName)[r.Section]
                                        .GetVector2(r.Property)
                                        .Select(x => (x / r.GetDoubleParam(0, 1d)).Round(0.001))
                                        .GetEnumerableHashCode());
                                break;

                            case RuleType.Vector3:
                                writer.Write(data.GetIniFile(r.FileName)[r.Section]
                                        .GetVector3(r.Property)
                                        .Select(x => (x / r.GetDoubleParam(0, 1d)).Round(0.001))
                                        .GetEnumerableHashCode());
                                break;

                            case RuleType.Vector4:
                                writer.Write(data.GetIniFile(r.FileName)[r.Section]
                                        .GetVector4(r.Property)
                                        .Select(x => (x / r.GetDoubleParam(0, 1d)).Round(0.001))
                                        .GetEnumerableHashCode());
                                break;

                            case RuleType.Number:
                                var testedValue = data.GetIniFile(r.FileName)[r.Section]
                                        .GetDouble(r.Property, 0d);
                                if (r.Tests?.Any(x => x(testedValue) != true) == true) return new byte[_rules.Length * sizeof(int)];
                                writer.Write((testedValue / r.GetDoubleParam(0, 1d)).Round(0.001).GetHashCode());
                                break;

                            case RuleType.String:
                                writer.Write(data.GetIniFile(r.FileName)[r.Section]
                                        .GetPossiblyEmpty(r.Property)?.GetHashCode() ?? -1);
                                break;

                            case RuleType.Lut:
                                Lut lut;
                                if (r.Section != null) {
                                    var section = data.GetIniFile(r.FileName)[r.Section];
                                    var value = section.GetNonEmpty(r.Property);

                                    if (value != null && value.IndexOf('=') != -1) {
                                        lut = section.GetLut(r.Property);
                                    } else if (value?.EndsWith(".lut", StringComparison.OrdinalIgnoreCase) == true) {
                                        lut = data.GetLutFile(value).Values;
                                    } else {
                                        writer.Write(0);
                                        continue;
                                    }
                                } else {
                                    lut = data.GetLutFile(r.FileName).Values;
                                }
                                writer.Write(lut?
                                        .SelectMany(x => (new[] { x.X, (x.Y / r.GetDoubleParam(0, 1d)).Round(0.001) }))
                                        .GetEnumerableHashCode().GetHashCode() ?? -1);
                                break;

                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
                
                return memory.ToArray();
            }
        }

        public static unsafe double CompareHashes([NotNull] byte[] lhs, [NotNull] byte[] rhs, [NotNull] RulesSet rules, bool keepWorkedRules,
                [CanBeNull] out Rule[] workedRules) {
            if (lhs.Length != rhs.Length) {
                throw new Exception("Different amount of enties");
            }

            if (lhs.Length % sizeof(int) != 0) {
                throw new Exception("Array of integers required");
            }

            var workedRulesList = keepWorkedRules ? new List<Rule>() : null;

            var a = lhs.Length / sizeof(int);
            var total = 0d;
            var same = 0d;
            var zeros = true;

            fixed (byte* lhp = lhs)
            fixed (byte* rhp = rhs) {
                int* ld = (int*)lhp, rd = (int*)rhp;
                for (var i = 0; i < a; i++) {
                    var rule = rules._rules[i];
                    total += rule.Weight;

                    var l = ld[i];
                    if (l == rd[i]) {
                        workedRulesList?.Add(rule);
                        same += rule.Weight;

                        if (l != 0) {
                            zeros = false;
                        }
                    }
                }
            }

            if (zeros) {
                workedRules = keepWorkedRules ? new Rule[0] : null;
                return 0d;
            }

            workedRules = workedRulesList?.ToArray();
            return same / total;
        }
    }
}
