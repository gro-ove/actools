using System.Collections.Generic;
using System.IO;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public class AcLocaleProvider {
        public const string CategoryTag = "tag";

        private readonly string _locale;
        private readonly string _directory;
        private readonly bool _exists;

        public AcLocaleProvider(string acRoot, string locale, string section = null) {
            _locale = locale;
            _directory = Path.Combine(acRoot, "system", "locales");
            if (section != null) {
                _directory = Path.Combine(_directory, section);
            }

            _exists = Directory.Exists(_directory);
        }

        [NotNull]
        private static string GetSupported(string locale, string directory, string extension) {
            var filename = Path.Combine(directory, $"{locale}.{extension}");
            if (File.Exists(filename) || locale == "en") return filename;

            var index = locale.IndexOf('-');
            if (index != -1) {
                filename = Path.Combine(directory, $"{locale.Substring(0, index)}.{extension}");
                if (File.Exists(filename)) return filename;
            }

            return Path.Combine(directory, $"en.{extension}");
        }

        private Dictionary<string, string> _tag;
        private IniFile _ini;

        [CanBeNull]
        public string GetString(string category, [CanBeNull] string key) {
            if (!_exists || key == null) return null;
            if (category == CategoryTag) {
                if (_tag == null) {
                    _tag = TagFile.FromFile(GetSupported(_locale, _directory, @"tag"));
                }

                return _tag.GetValueOrDefault(key);
            }

            if (_ini == null) {
                _ini = new IniFile(GetSupported(_locale, _directory, @"ini"));
            }

            return _ini[category].GetNonEmpty(key);
        }
    }
}