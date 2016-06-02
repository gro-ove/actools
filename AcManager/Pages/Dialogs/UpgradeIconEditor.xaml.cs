using System;
using System.IO;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Pages.Dialogs {
    public partial class UpgradeIconEditor {
        private static WeakReference<UpgradeIconEditor> _instance;
        public static UpgradeIconEditor Instance {
            get {
                if (_instance == null) {
                    return null;
                }

                UpgradeIconEditor result;
                return _instance.TryGetTarget(out result) ? result : null;
            }
        }

        public CarObject Car { get; }

        public UpgradeIconEditor(CarObject car) {
            Car = car;
            _instance = new WeakReference<UpgradeIconEditor>(this);

            InitializeComponent();
            DataContext = this;

            Buttons = new[] {
                OkButton, 
                CreateExtraDialogButton("Select File", SelectFile), 
                CreateExtraDialogButton("View User Folder", () => FilesStorage.Instance.OpenContentFolderInExplorer(ContentCategory.UpgradeIcons)),
                CancelButton
            };

            Closing += UpgradeIconEditor_Closing;
        }

        private void UpgradeIconEditor_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            var currentPage = Tabs.Frame.Content as IFinishableControl;
            currentPage?.Finish(MessageBoxResult == MessageBoxResult.OK);
        }

        private void SelectFile() {
            var dialog = new OpenFileDialog {
                Filter = FileDialogFilters.ImagesFilter,
                Title = "Select New Upgrade Icon"
            };
            if (dialog.ShowDialog() == true) {
                ApplyFile(dialog.FileName);
            }
        }

        private void UpgradeIconEditor_OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ApplyFile(files[0]);
        }

        private void ApplyFile(string filename) {
            var cropped = ImageEditor.Proceed(filename, new Size(64, 64));
            if (cropped == null) return;

            try {
                cropped.SaveAsPng(Car.UpgradeIcon);
            } catch (Exception) {
                ShowMessage(@"Can’t change upgrade icon.", @"Fail", MessageBoxButton.OK);
                return;
            }

            // Car.RefreshUpgradeIcon();

            var saveAs = Controls.Pages.Dialogs.Prompt.Show(@"Add into the library?", @"Add as:", Path.GetFileNameWithoutExtension(filename));
            if (saveAs == null) return;

            try {
                FilesStorage.Instance.AddUserContentToDirectory(ContentCategory.UpgradeIcons, Car.UpgradeIcon, saveAs);
            } catch (Exception) {
                ShowMessage(@"Can’t add new element into the library.", @"Fail", MessageBoxButton.OK);
            }

            Close();
        }
    }
}
