using AcManager.Controls.ViewModels;

namespace AcManager.Pages.Dialogs {
    public partial class AssistsDialog {
        public AssistsDialog(AssistsViewModel viewModel) {
            DataContext = viewModel;
            InitializeComponent();
            Buttons = new[] { CloseButton };
        }
    }
}
