using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class ReplaysListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ReplayObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class CategoryGroupDescription : GroupDescription {
            public override object GroupNameFromItem(object item, int level, CultureInfo culture) {
                var category = ((item as AcItemWrapper)?.Value as ReplayObject)?.EditableCategory;
                return category == ReplayObject.AutosaveCategory ? "Autosave"  : category ?? "";
            }
        }

        private class ViewModel : AcListPageViewModel<ReplayObject> {
            public ViewModel(IFilter<ReplayObject> listFilter)
                    : base(ReplaysManager.Instance, listFilter) {
                GroupBy(nameof(ReplayObject.EditableCategory), new CategoryGroupDescription());
            }

            protected override string GetSubject() {
                return AppStrings.List_Replays;
            }
        }
    }
}
