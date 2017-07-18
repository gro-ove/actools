using System;
using System.Collections.Generic;
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

                        IniFileSection iniFileSection;
                        if (r.Section != null) {
                            var iniFile = data.GetIniFile(r.FileName);
                            if (iniFile.ContainsKey(r.Section)) {
                                iniFileSection = iniFile[r.Section];
                            } else {
                                writer.Write(-1);
                                continue;
                            }
                        } else {
                            iniFileSection = null;
                        }

                        switch (r.Type) {
                            case RuleType.Vector2:
                                writer.Write(iniFileSection?
                                        .GetVector2(r.Property)
                                        .Select(x => (x / r.GetDoubleParam(0, 1d)).Round(0.001))
                                        .GetEnumerableHashCode() ?? -1);
                                break;

                            case RuleType.Vector3:
                                writer.Write(iniFileSection?
                                        .GetVector3(r.Property)
                                        .Select(x => (x / r.GetDoubleParam(0, 1d)).Round(0.001))
                                        .GetEnumerableHashCode() ?? -1);
                                break;

                            case RuleType.Vector4:
                                writer.Write(iniFileSection?
                                        .GetVector4(r.Property)
                                        .Select(x => (x / r.GetDoubleParam(0, 1d)).Round(0.001))
                                        .GetEnumerableHashCode() ?? -1);
                                break;

                            case RuleType.Number:
                                var testedValue = iniFileSection?
                                        .GetDouble(r.Property, 0d) ?? 0d;
                                if (r.Tests?.Any(x => x(testedValue) != true) == true) return new byte[_rules.Length * sizeof(int)];
                                writer.Write((testedValue / r.GetDoubleParam(0, 1d)).Round(0.001).GetHashCode());
                                break;

                            case RuleType.String:
                                writer.Write(iniFileSection?
                                        .GetPossiblyEmpty(r.Property)?.GetHashCode() ?? -1);
                                break;

                            case RuleType.Lut:
                                Lut lut;
                                if (r.Section != null) {
                                    var value = iniFileSection?.GetNonEmpty(r.Property);
                                    if (value != null && value.IndexOf('=') != -1) {
                                        lut = iniFileSection.GetLut(r.Property);
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

                    var l = ld[i];
                    var r = rd[i];

                    if (l == r) {
                        if (l == -1) continue;

                        workedRulesList?.Add(rule);
                        same += rule.Weight;

                        if (l != 0) {
                            zeros = false;
                        }
                    }

                    total += rule.Weight;
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
