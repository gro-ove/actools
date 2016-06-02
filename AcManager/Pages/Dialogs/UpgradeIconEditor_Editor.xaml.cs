using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    /// <summary>
    /// Interaction logic for UpgradeIconEditor_Editor.xaml
    /// </summary>
    public partial class UpgradeIconEditor_Editor : UserControl, IFinishableControl {
        private readonly string _key;

        public CarObject Car { get; private set; }

        public UpgradeIconEditor_Editor() {
            var mainDialog = UpgradeIconEditor.Instance;
            if (mainDialog != null) {
                Car = mainDialog.Car;
                _key = "__upgradeiconeditor_" + Car.Id;
            }

            DataContext = this;
            InitializeComponent();

            NewIconLabel.Text = _key != null ? ValuesStorage.GetString(_key, "S1") : "?";
            NewIconLabel_UpdateFontSize();

            FocusLabel();
        }

        private async void FocusLabel() {
            await Task.Delay(100);
            NewIconLabel.SelectAll();
            NewIconLabel.Focus();
        }

        private readonly static Action EmptyDelegate = delegate() {};

        public void Finish(bool result) {
            if (!result) return;

            NewIconLabel.IsReadOnly = true;
            NewIconLabel.IsReadOnlyCaretVisible = false;
            NewIconLabel.SelectionLength = 0;
            NewIconLabel.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
            CreateNewIcon();
        }

        private async void CreateNewIcon() {
            await Task.Delay(100);
            if (Car == null) return;

            ValuesStorage.Set(_key, NewIconLabel.Text);
            // TODO: Save style?

            var bmp = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(NewIcon);

            try {
                bmp.SaveAsPng(Car.UpgradeIcon);
            } catch (Exception) {
                ModernDialog.ShowMessage(@"Can’t change upgrade icon.", @"Fail", MessageBoxButton.OK);
            }
        }

        private void NewIconLabel_UpdateFontSize() {
            var len = NewIconLabel.Text.Length;
            NewIconLabel.FontSize = len < 3 ? 15 : len > 8 ? 7 : 17 - len;
        }

        private void NewIconLabel_OnTextChanged(object sender, TextChangedEventArgs e) {
            NewIconLabel_UpdateFontSize();
        }

        private void Command_ToggleBold_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Command_ToggleBold_Executed(object sender, ExecutedRoutedEventArgs e) {
            NewIconLabel.FontWeight = NewIconLabel.FontWeight == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;
        }

        private void Command_ToggleItalic_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Command_ToggleItalic_Executed(object sender, ExecutedRoutedEventArgs e) {
            NewIconLabel.FontStyle = NewIconLabel.FontStyle == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic;
        }
    }
}
