using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using JetBrains.Annotations;

namespace AcManager {
    /// <summary>
    /// Can be safely used before references are loaded.
    /// </summary>
    [Localizable(false)]
    public static class AppArguments {
        private static Regex _regex;
        private static Dictionary<AppFlag, string> _args;

        public static IReadOnlyList<string> Values { get; private set; }

        public static void Initialize(IEnumerable<string> args) {
            var list = args.ToList();

            _args = list.TakeWhile(x => x != "-")
                .Where(x => x.StartsWith("--"))
                .Select(x => x.Split(new[] { '=' }, 2))
                .Select(x => new {
                    Key = ArgStringToFlag(x[0]),
                    Value = x.Length == 2 ? x[1] : null
                })
                .Where(x => x.Key != null)
                .ToDictionary(x => x.Key.Value, x => x.Value);

            Values = list.Where(x => !x.StartsWith("-"))
                         .Union(list.SkipWhile(x => x != "-").Skip(1).Where(x => x.StartsWith("-")))
                         .Where(x => x != "/dev")
                         .ToList();
        }

        public static void AddFromFile(string filename) {
            if (!File.Exists(filename)) return;

            foreach (var pair in File.ReadAllLines(filename).Where(x => x.StartsWith("--"))
                                     .Select(x => x.Split(new[] { '=' }, 2).Select(y => y.Trim()).ToArray())
                                     .Select(x => new {
                                         Key = ArgStringToFlag(x[0]),
                                         Value = x.Length == 2 ? x[1] : null
                                     })
                                     .Where(x => x.Key != null)) {
                _args[pair.Key.Value] = pair.Value;
            }
        }

        private static AppFlag? ArgStringToFlag(string arg) {
            var s = string.Join("", arg.Split('-').Where(x => x.Length > 0).Select(x => (char)(x[0] + 'A' - 'a') + (x.Length > 1 ? x.Substring(1) : "")));
            return Enum.TryParse(s, out AppFlag result) ? result : (AppFlag?)null;
        }

        public static bool Has(AppFlag flag) {
            return _args != null && _args.ContainsKey(flag);
        }

        [CanBeNull]
        public static string Get(AppFlag flag) {
            return _args != null && _args.ContainsKey(flag) ? _args[flag] : FlagDefaultValueAttribute.GetValue(flag);
        }

        public static bool GetBool(AppFlag flag, bool defaultValue = false) {
            Set(flag, ref defaultValue);
            return defaultValue;
        }

        public static double GetDouble(AppFlag flag, double defaultValue = 0d) {
            Set(flag, ref defaultValue);
            return defaultValue;
        }

        public static void Set(AppFlag flag, ref bool option) {
            var value = Get(flag);
            if (value == null) {
                if (Has(flag)) {
                    option = true;
                }
                return;
            }

            if (value == "1" ||
                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "y", StringComparison.OrdinalIgnoreCase)) {
                option = true;
            } else if (value == "0" ||
                    string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "not", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "n", StringComparison.OrdinalIgnoreCase)) {
                option = false;
            }
        }

        public static void Set(AppFlag flag, ref int option) {
            var value = Get(flag);
            if (value == null) return;
            if (!int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out option)) {
                MessageBox.Show($"Can’t parse option value: “{value}”");
            }
        }

        private static bool ParseSize(string size, out long bytes) {
            var split = -1;

            for (var i = 0; i < size.Length; i++) {
                if (size[i] >= 'a' && size[i] <= 'z' || size[i] >= 'A' && size[i] <= 'Z') {
                    split = i;
                    break;
                }
            }

            if (split == -1) {
                return long.TryParse(size, NumberStyles.Any, CultureInfo.InvariantCulture, out bytes);
            }

            if (!double.TryParse(size.Substring(0, split).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val)) {
                bytes = 0;
                return false;
            }

            var postfix = size.Substring(split).Trim().ToLower();
            switch (postfix) {
                case "b":
                    bytes = (long)val;
                    return true;
                case "kb":
                    bytes = (long)(1024 * val);
                    return true;
                case "mb":
                    bytes = (long)(1048576 * val);
                    return true;
                case "gb":
                    bytes = (long)(1073741824 * val);
                    return true;
                case "tb":
                    bytes = (long)(1099511627776 * val);
                    return true;
                default:
                    MessageBox.Show($"Unknown postfix: {postfix}");
                    bytes = (long)val;
                    return false;
            }
        }

        public static void SetSize(AppFlag flag, ref long option) {
            var value = Get(flag);
            if (value == null) return;

            if (!ParseSize(value, out option)) {
                MessageBox.Show($"Can’t parse option value: “{value}”");
            }
        }

        public static void Set(AppFlag flag, ref double option) {
            var value = Get(flag);
            if (value == null) return;
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out option)) {
                MessageBox.Show($"Can’t parse option value: “{value}”");
            }
        }

        public static void Set(AppFlag flag, ref string option) {
            var value = Get(flag);
            if (value == null) return;
            option = value;
        }

        private static bool TryParse(string value, ref TimeSpan timeSpan) {
            var p = value.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            try {
                double result;
                switch (p.Length) {
                    case 0:
                        return false;
                    case 1:
                        result = double.Parse(p[0], CultureInfo.InvariantCulture);
                        break;
                    case 2:
                        result = double.Parse(p[0], CultureInfo.InvariantCulture) * 60 + double.Parse(p[1], CultureInfo.InvariantCulture);
                        break;
                    case 3:
                        result = (double.Parse(p[0], CultureInfo.InvariantCulture) * 60 + double.Parse(p[1], CultureInfo.InvariantCulture)) * 60 +
                                double.Parse(p[2], CultureInfo.InvariantCulture);
                        break;
                    default:
                        result = ((double.Parse(p[0], CultureInfo.InvariantCulture) * 24 + double.Parse(p[1], CultureInfo.InvariantCulture)) * 60 +
                                double.Parse(p[2], CultureInfo.InvariantCulture)) * 60 + double.Parse(p[3], CultureInfo.InvariantCulture);
                        break;
                }

                timeSpan = TimeSpan.FromSeconds(result);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        public static void Set(AppFlag flag, ref TimeSpan option) {
            var value = Get(flag);
            if (value == null) return;
            if (!TryParse(value, ref option)) {
                MessageBox.Show($"Can’t parse option value: “{value}”");
            }
        }
    }
}
