using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Selected {
    public interface ISelectedAcObjectViewModel {
        AcCommonObject SelectedAcObject { get; }

        void Load();

        void Unload();

        RelayCommand FindInformationCommand { get; }
    }
}