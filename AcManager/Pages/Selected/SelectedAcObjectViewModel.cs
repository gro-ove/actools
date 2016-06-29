using System;
using System.Linq;
using System.Windows;
using AcManager.Annotations;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;

namespace AcManager.Pages.Selected {
    public abstract class SelectedAcObjectViewModel<T> : NotifyPropertyChanged, ISelectedAcObjectViewModel where T : AcCommonObject {
        [NotNull]
        public T SelectedObject { get; }

        public AcCommonObject SelectedAcObject => SelectedObject;

        protected SelectedAcObjectViewModel([NotNull] T acObject) {
            SelectedObject = acObject;
        }

        public virtual void Load() { }

        public virtual void Unload() { }

        private RelayCommand _findInformationCommand;

        public RelayCommand FindInformationCommand => _findInformationCommand ?? (_findInformationCommand = new RelayCommand(o => {
            new FindInformationDialog((AcJsonObjectNew)SelectedAcObject).ShowDialog();
        }, o => SelectedAcObject is AcJsonObjectNew));

        #region Filter Commands
        public string FilterTabType { get; protected set; }

        public void NewFilterTab(string filter) {
            if (FilterTabType == null) throw new NotSupportedException();
            Application.Current.Windows.OfType<ModernWindow>().FirstOrDefault(x => x.IsActive)?.MenuLinkGroups.OfType<LinkGroupFilterable>().FirstOrDefault(x =>
                    string.Equals(x.DisplayName, FilterTabType, StringComparison.OrdinalIgnoreCase))?.AddAndSelect(filter);
        }

        private RelayCommand _filterTagCommand;

        public RelayCommand FilterTagCommand => _filterTagCommand ?? (_filterTagCommand = new RelayCommand(o => {
            NewFilterTab($"tag:{Filter.Encode(o as string ?? "")}");
        }, o => o is string));

        private RelayCommand _filterCommand;

        public RelayCommand FilterCommand => _filterCommand ?? (_filterCommand = new RelayCommand(o => FilterExec(o as string)));

        protected virtual void FilterExec(string type) {
            var jsonObject = SelectedObject as AcJsonObjectNew;
            switch (type) {
                case "author":
                    if (jsonObject == null) return;
                    NewFilterTab(string.IsNullOrWhiteSpace(jsonObject.Author) ? "author-" : $"author:{Filter.Encode(jsonObject.Author)}");
                    break;

                case "country":
                    if (jsonObject == null) return;
                    NewFilterTab(string.IsNullOrWhiteSpace(jsonObject.Country) ? "country-" : $"country:{Filter.Encode(jsonObject.Country)}");
                    break;

                case "year":
                    NewFilterTab(SelectedObject.Year.HasValue ? $"year:{SelectedObject.Year}" : "year-");
                    break;

                case "decade":
                    if (!SelectedObject.Year.HasValue) {
                        NewFilterTab("year-");
                    }

                    var start = (int)Math.Floor((SelectedObject.Year ?? 0) / 10d) * 10;
                    NewFilterTab($"year>{start - 1} & year<{start + 10}");
                    break;
            }
        }
        #endregion
    }
}