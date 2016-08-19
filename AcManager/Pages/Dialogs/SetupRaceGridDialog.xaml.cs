using AcManager.Controls.ViewModels;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    public partial class SetupRaceGridDialog {
        public RaceGridViewModel Model { get; set; }

        public SetupRaceGridDialog(RaceGridViewModel model) {
            Model = model;
            DataContext = model;
            InitializeComponent();
            
            // TODO: Buttons = new[] { OkButton, CancelButton }; 
            Buttons = new[] { CloseButton }; 
        }
    }
}
