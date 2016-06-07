using AcManager.Annotations;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Selected {
    public class SelectedAcObjectViewModel<T> : NotifyPropertyChanged, ISelectedAcObjectViewModel where T : AcCommonObject {
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
    }
}