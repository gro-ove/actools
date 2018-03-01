using System;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Lists {
    public class ToolFilteringLink : ToolLink {
        public string FilterDescription { get; set; }

        public string DefaultFilter { get; set; }

        protected override Uri LaunchSource {
            get {
                var key = $@".FilteredToolLink:{Source.OriginalString}";
                var defaultValue = ValuesStorage.Get(key, DefaultFilter);
                var filter = Prompt.Show(FilterDescription ?? "Optional filter:", "Optional filter", placeholder: @"*", defaultValue: defaultValue,
                        suggestions: ValuesStorage.GetStringList("AcObjectListBox:FiltersHistory:car"));
                if (filter != null) {
                    ValuesStorage.Set(key, filter);
                }

                return string.IsNullOrWhiteSpace(filter) ? base.LaunchSource : base.LaunchSource.AddQueryParam("Filter", filter);
            }
        }
    }
}