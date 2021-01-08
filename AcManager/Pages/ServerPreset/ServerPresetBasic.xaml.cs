using System;
using System.Windows;
using AcManager.Controls;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetBasic {
        public ServerPresetBasic() {
            InitializeComponent();
            CspVersionAutoFill.IsEnabled = PatchHelper.IsFeatureSupported(PatchHelper.FeatureTestOnline);
            if (SettingsHolder.Online.ServerPresetsFitInFewerTabs) {
                WelcomeMessageTextArea.Height = 80;
            }
        }

        private void CspVersionAutoFillClick(object sender, RoutedEventArgs e) {
            if (DataContext is SelectedPage.ViewModel viewModel) {
                viewModel.SelectedObject.RequiredCspVersion = PatchHelper.GetInstalledBuild().As<int?>(null);
            }
        }

        private void CspExtendedConfigClick(object sender, RoutedEventArgs e) {
            if (DataContext is SelectedPage.ViewModel viewModel) {
                var editor = new TextEditor();
                AvalonExtension.SetInitialized(editor, true);
                AvalonExtension.SetMode(editor, AvalonEditMode.Ini);
                editor.Document = new TextDocument(viewModel.SelectedObject.CspExtraConfig ?? "");
                var dialog = new ModernDialog {
                    Title = "Edit CSP config",
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    LocationAndSizeKey = ".serverCspConfigEditor",
                    MinWidth = 400,
                    MinHeight = 240,
                    Width = 800,
                    Height = 640,
                    MaxWidth = 99999,
                    MaxHeight = 99999,
                    Content = editor,
                    Resources = new ResourceDictionary { Source = new Uri("/AcManager.Controls;component/Assets/TextEditor.xaml", UriKind.Relative) }
                };
                dialog.Buttons = MessageDialogButton.OKCancel.GetButtons(dialog);
                if (dialog.ShowDialog() == true) {
                    viewModel.SelectedObject.CspExtraConfig = editor.Text.Trim();
                }

                if (!string.IsNullOrWhiteSpace(viewModel.SelectedObject.CspExtraConfig)) {
                    viewModel.SelectedObject.RequiredCspVersion = Math.Max(viewModel.SelectedObject.RequiredCspVersion ?? 0, 1266);
                }
            }
        }
    }
}