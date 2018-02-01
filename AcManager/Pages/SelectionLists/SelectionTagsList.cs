using System;
using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public abstract class SelectionTagsList<TObject> : SelectionList<TObject, SelectTag> where TObject : AcJsonObjectNew {
        protected SelectionTagsList([NotNull] BaseAcManager<TObject> manager) : base(manager, true) {}

        protected sealed override SelectTag LoadFromCache(string serialized) {
            return SelectTag.Deserialize(serialized);
        }

        private readonly List<string> _ignored = new List<string>(50);

        protected sealed override void AddNewIfMissing(IList<SelectTag> list, TObject obj) {
            var value = obj.Tags;
            for (var j = value.Count - 1; j >= 0; j--) {
                var tagValue = value[j];

                if (_ignored.Contains(tagValue)) {
                    continue;
                }

                var isSpecial = tagValue.StartsWith(@"#");
                var tagName = isSpecial ? tagValue.Substring(1).TrimStart() : tagValue;

                for (var i = list.Count - 1; i >= 0; i--) {
                    var item = list[i];
                    if (string.Equals(item.DisplayName, tagName, StringComparison.Ordinal)) {
                        IncreaseCounter(obj, item);
                        item.Accented |= isSpecial;
                        goto Next;
                    }
                }

                if (!isSpecial && (AcStringValues.CountryFromTag(tagValue) != null || IsIgnored(obj, tagValue))) {
                    _ignored.Add(tagValue);
                    continue;
                }

                AddNewIfMissing(list, obj, new SelectTag(tagName, tagValue));
                Next:;
            }
        }

        protected abstract bool IsIgnored([NotNull] TObject obj, [NotNull] string tagValue);

        protected sealed override SelectTag GetSelectedItem(IList<SelectTag> list, TObject obj) {
            var value = obj?.Tags;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (value != null) {
                for (var j = value.Count - 1; j >= 0; j--) {
                    var tagValue = value[j];
                    var isSpecial = tagValue.StartsWith(@"#");
                    var tagName = isSpecial ? tagValue.Substring(1).TrimStart() : tagValue;

                    for (var i = list.Count - 1; i >= 0; i--) {
                        var item = list[i];
                        if (string.Equals(item.DisplayName, tagName, StringComparison.Ordinal)) return item;
                    }
                }
            }

            return null;
        }

        protected sealed override bool OnObjectPropertyChanged(TObject obj, PropertyChangedEventArgs e) {
            return e.PropertyName == nameof(obj.Tags);
        }
    }
}