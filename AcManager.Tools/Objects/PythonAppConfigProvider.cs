using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigProvider : IPythonAppConfigValueProvider {
        private readonly PythonAppConfig _root;
        private Collection<IPythonAppConfigValue> _section;
        private Dictionary<string, string> _flags;
        private bool _sectionChanged;
        private static Dictionary<string, string> _values = new Dictionary<string, string>();

        public PythonAppConfigProvider([NotNull] PythonAppConfig root) {
            _root = root;
            _flags = root.ConfigParams.Flags;
            _values.Clear();
            _sectionChanged = true;
        }

        private static int LastIndexOf(string key) {
            int i0 = key.LastIndexOf('/'), i1 = key.LastIndexOf('\\');
            return i0 != -1 ? i1 != -1 ? i0 < i1 ? i1 : i0 : i0 : i1;
        }

        private static int LastIndexOf(string key, int from) {
            int i0 = key.LastIndexOf('/', from), i1 = key.LastIndexOf('\\', from);
            return i0 != -1 ? i1 != -1 ? i0 < i1 ? i1 : i0 : i0 : i1;
        }

        private static void Parse(string key, out string param, out string section, out string file) {
            var paramSep = LastIndexOf(key);
            if (paramSep <= 0) {
                param = paramSep == -1 ? key : key.Substring(1);
                section = file = null;
                return;
            }

            param = key.Substring(paramSep + 1);

            var sectionSep = LastIndexOf(key, paramSep - 1);
            if (sectionSep <= 0) {
                section = sectionSep == -1 ? key.Substring(0, paramSep) : key.Substring(1, paramSep - 1);
                file = null;
                return;
            }

            section = key.Substring(sectionSep + 1, paramSep - sectionSep - 1);
            file = key.Substring(0, sectionSep);
        }

        public string GetValue(string key) {
            if (_flags != null && _flags.TryGetValue(key, out var result)) return result;

            if (_sectionChanged) {
                _values.Clear();
            }

            return _values.GetValueOrSet(key, () => {
                Parse(key, out var param, out var section, out var file);
                #if DEBUG
                if (file != null) {
                    Logging.Warning($"FILE={file}, section={section}, key={key}");
                }
                #endif

                var sections = _root.SectionsOwn;
                var values = section == null ? _section
                        : sections.FirstOrDefault(x => string.Equals(x.Key, section, StringComparison.OrdinalIgnoreCase));
                return values?.FirstOrDefault(x => string.Equals(x.Id, param))?.Value;
            });
        }

        [CanBeNull]
        public IPythonAppConfigValue GetItem(string key) {
            Parse(key, out var param, out var section, out _);
            var sections = _root.SectionsOwn;
            var values = section == null ? _section
                    : sections.FirstOrDefault(x => string.Equals(x.Key, section, StringComparison.OrdinalIgnoreCase));
            return values?.FirstOrDefault(x => string.Equals(x.Id, param));
        }

        public void SetSection(Collection<IPythonAppConfigValue> section) {
            _sectionChanged = true;
            _section = section;
            _values.Clear();
        }
    }
}