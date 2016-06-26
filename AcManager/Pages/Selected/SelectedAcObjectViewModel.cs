using System;
using System.Linq;
using System.Windows;
using AcManager.Annotations;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;
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
            (Application.Current.MainWindow as MainWindow)?.MenuLinkGroups.OfType<LinkGroupFilterable>().FirstOrDefault(x =>
                    string.Equals(x.DisplayName, FilterTabType, StringComparison.OrdinalIgnoreCase))?.AddAndSelect(filter);
        }

        private RelayCommand _filterYearCommand;

        public RelayCommand FilterYearCommand => _filterYearCommand ?? (_filterYearCommand = new RelayCommand(o => {
            NewFilterTab(SelectedObject.Year.HasValue ? $"year:{SelectedObject.Year}" : "!year>0");
        }));

        private RelayCommand _filterDecadeCommand;

        public RelayCommand FilterDecadeCommand => _filterDecadeCommand ?? (_filterDecadeCommand = new RelayCommand(o => {
            var start = (int)Math.Floor(SelectedObject.Year ?? 0 / 10d) * 10;
            NewFilterTab($"year>{start - 1} & year<{start + 10}");
        }, o => SelectedObject.Year.HasValue));

        private RelayCommand _filterCountryCommand;

        public RelayCommand FilterCountryCommand => _filterCountryCommand ?? (_filterCountryCommand = new RelayCommand(o => {
            NewFilterTab($"country:{Filter.Encode((SelectedObject as AcJsonObjectNew)?.Country)}");
        }, o => SelectedObject is AcJsonObjectNew));

        private RelayCommand _filterAuthorCommand;

        public RelayCommand FilterAuthorCommand => _filterAuthorCommand ?? (_filterAuthorCommand = new RelayCommand(o => {
            NewFilterTab($"author:{Filter.Encode((SelectedObject as AcJsonObjectNew)?.Author)}");
        }, o => SelectedObject is AcJsonObjectNew));
        #endregion
    }
}