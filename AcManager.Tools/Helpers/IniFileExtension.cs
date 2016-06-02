using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class IniFileExtension {
        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, string key, IList<SettingEntry> entries, [NotNull] SettingEntry defaultValue) {
            if (defaultValue == null) throw new ArgumentNullException(nameof(defaultValue));
            var value = section.Get(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? defaultValue;
        }

        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, string key, IList<SettingEntry> entries, [NotNull] string defaultValueId) {
            if (defaultValueId == null) throw new ArgumentNullException(nameof(defaultValueId));
            var value = section.Get(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? entries.GetByIdOrDefault(defaultValueId) ?? entries.First();
        }

        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, string key, IList<SettingEntry> entries) {
            var value = section.Get(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? entries.First();
        }

        public static void Set(this IniFileSection section, string key, SettingEntry entry) {
            section.Set(key, entry.Value);
        }
    }
}