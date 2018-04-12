using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Controls.Helpers;
using AcManager.Tools.Objects;
using AcTools;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    public partial class UpgradeIconEditor_Editor : IFinishableControl {
        private readonly string _key;

        public CarObject Car { get; }

        public UpgradeIconEditor_Editor() {
            var mainDialog = UpgradeIconEditor.Instance;
            if (mainDialog != null) {
                Car = mainDialog.Car;
                _key = @"__upgradeiconeditor_" + Car.Id;
            }

            DataContext = this;
            InitializeComponent();

            NewIconLabel.Text = _key != null ? ValuesStorage.Get(_key, UpgradeIconEditor.TryToGuessLabel(Car?.DisplayName)) ?? "S1" : @"?";
            NewIconLabel_UpdateFontSize();

            FocusLabel();
        }

        private async void FocusLabel() {
            await Task.Delay(100);
            NewIconLabel.SelectAll();
            NewIconLabel.Focus();
        }

        private static readonly Action EmptyDelegate = delegate {};

        public void Finish(bool result) {
            if (!result) return;

            NewIconLabel.IsReadOnly = true;
            NewIconLabel.IsReadOnlyCaretVisible = false;
            NewIconLabel.SelectionLength = 0;
            CreateNewIcon();
        }

        private void CreateNewIcon() {
            if (Car == null) return;

            ValuesStorage.Set(_key, NewIconLabel.Text);
            // TODO: Save style?

            var size = new Size(CommonAcConsts.UpgradeIconWidth, CommonAcConsts.UpgradeIconHeight);
            NewIcon.Measure(size);
            NewIcon.Arrange(new Rect(size));
            NewIcon.ApplyTemplate();
            NewIcon.UpdateLayout();

            var bmp = new RenderTargetBitmap(CommonAcConsts.UpgradeIconWidth, CommonAcConsts.UpgradeIconHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(NewIcon);

            try {
                bmp.SaveTo(Car.UpgradeIcon);
                BetterImage.Refresh(Car.UpgradeIcon);
            } catch (IOException ex) {
                NonfatalError.Notify(AppStrings.UpgradeIcon_CannotChange, AppStrings.UpgradeIcon_CannotChange_Commentary, ex);
            } catch (Exception ex) {
                NonfatalError.Notify(AppStrings.UpgradeIcon_CannotChange, ex);
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
