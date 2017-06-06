using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using AcManager.Tools;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public partial class LiveryIconEditorDialog {
        public LiveryIconEditorDialog(CarSkinObject skin) {
            InitializeComponent();
            DataContext = new LiveryIconEditor(skin, false, false, null, Result);
            Buttons = new[] { OkButton, CancelButton };
            GuessColors();
        }

        private async void GuessColors() {
            ResultLoading.Value = true;
            await Model.GuessColorsAsync();
            ResultLoading.Value = false;
        }

        private LiveryIconEditor Model => (LiveryIconEditor)DataContext;

        public new bool ShowDialog() {
            base.ShowDialog();
            return IsResultOk;
        }

        private void OnClosing(object sender, CancelEventArgs args) {
            if (IsResultOk) {
                if (Model.Skin != null && Model.Value != @"0" && Model.Value != Model.Skin.SkinNumber &&
                        ShowMessage(string.Format(AppStrings.LiveryIcon_ChangeNumber_Message, Model.Value), AppStrings.LiveryIcon_ChangeNumber_Title,
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    Model.Skin.SkinNumber = Model.Value;
                }

                try {
                    Model.CreateNewIcon();
                } catch (IOException e) {
                    NonfatalError.Notify(AppStrings.LiveryIcon_CannotChange, AppStrings.LiveryIcon_CannotChange_Commentary, e);
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.LiveryIcon_CannotChange, e);
                }
            }
        }
    }
}