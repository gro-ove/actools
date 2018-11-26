using System;
using System.Collections.Generic;
using System.ComponentModel;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class SettingEntry : Displayable, IWithId, IWithId<int?> {
        public SettingEntry([LocalizationRequired(false)] string value, string displayName) {
            DisplayName = displayName;
            Value = value;
            IntValue = value.ToInvariantInt();
        }

        public SettingEntry(int value, string displayName) {
            DisplayName = displayName;
            Value = value.ToInvariantString();
            IntValue = value;
        }

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }

        public string Tag { get; set; }

        [Localizable(false)]
        public string Value { get; }

        public int? IntValue { get; }

        [Localizable(false)]
        public string Id => Value;

        int? IWithId<int?>.Id => IntValue;
    }

    public class SettingEntry<T> : Displayable, IWithId<T> where T : struct {
        public SettingEntry([LocalizationRequired(false)] T value, string displayName) {
            DisplayName = displayName;
            Value = value;
        }

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }

        [Localizable(false)]
        public T Value { get; }

        T IWithId<T>.Id => Value;
    }

    public static class SettingEntryUtils {
        public static SettingEntry EnsureFrom([CanBeNull] this SettingEntry value, [NotNull] IEnumerable<SettingEntry> entries) {
            using (var e = entries.GetEnumerator()) {
                if (!e.MoveNext()) throw new ArgumentException("Input collection is empty");
                var first = e.Current;
                if (value == null) return first;
                if (first?.Id == value.Id) return first;
                while (e.MoveNext()) {
                    if (e.Current?.Id == value.Id) return e.Current;
                }
                return first;
            }
        }
    }
}