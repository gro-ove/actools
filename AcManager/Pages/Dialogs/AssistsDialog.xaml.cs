using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.ViewModels;

namespace AcManager.Pages.Dialogs {
    public partial class AssistsDialog {
        public AssistsDialog(AssistsViewModel viewModel) {
            DataContext = viewModel;
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        private void Label_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 1) return;

            var label = (Label)sender;
            Keyboard.Focus(label.Target);
        }

        private AssistsViewModel Model => (AssistsViewModel)DataContext;
    }
}
