using FirstFloor.ModernUI.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AcManager.Controls.Pages.Dialogs {
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
