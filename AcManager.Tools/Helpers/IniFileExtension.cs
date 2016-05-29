using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers {
    internal static class IniFileExtension {
        public static SettingEntry GetEntry(this IniFileSection section, string key, IList<SettingEntry> entries, SettingEntry defaultValue) {
            var value = section.Get(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? defaultValue;
        }

        public static SettingEntry GetEntry(this IniFileSection section, string key, IList<SettingEntry> entries, string defaultValueId) {
            var value = section.Get(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? entries.GetByIdOrDefault(defaultValueId) ?? entries.FirstOrDefault();
        }

        public static SettingEntry GetEntry(this IniFileSection section, string key, IList<SettingEntry> entries) {
            return section.GetEntry(key, entries, entries.FirstOrDefault());
        }

        public static void Set(this IniFileSection section, string key, SettingEntry entry) {
            section.Set(key, entry.Value);
        }
    }
}