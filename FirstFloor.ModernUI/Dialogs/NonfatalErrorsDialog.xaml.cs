using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Dialogs {
    public partial class NonfatalErrorsDialog {
        public NonfatalErrorsDialog() {
            InitializeComponent();
            Buttons = new[] { OkButton };
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var result in List.SelectedItems.OfType<NonfatalErrorEntry>()) {
                result.Unseen = false;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Delete) return;
            foreach (var result in List.SelectedItems.OfType<NonfatalErrorEntry>().ToList()) {
                NonfatalError.Instance.Errors.Remove(result);
            }

            NonfatalError.Instance.UpdateUnseen();
            List.SelectedItem = NonfatalError.Instance.Errors.FirstOrDefault();
        }
    }
}
