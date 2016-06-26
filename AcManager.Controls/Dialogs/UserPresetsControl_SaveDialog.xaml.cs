using System.Windows.Controls;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Dialogs {
    /// <summary>
    /// Interaction logic for UserPresetsControl_SaveDialog.xaml
    /// </summary>
    public partial class UserPresetsControl_SaveDialog : ModernDialog {
        public UserPresetsControl_SaveDialog() {
            InitializeComponent();

            // define the dialog buttons
            this.Buttons = new Button[] { this.OkButton, this.CancelButton };
        }
    }
}
