using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Pages.Dialogs {
    public partial class UpgradeIconEditor {
        [Localizable(false), CanBeNull]
        public static string TryToGuessLabel(string carName) {
            if (carName == null) return null;
            if (Regex.IsMatch(carName, @"\b(?:drift|d(?:\s*|-)spec)\b", RegexOptions.IgnoreCase)) return "D";
            if (Regex.IsMatch(carName, @"\b(?:s|step|stage)\s*3\b", RegexOptions.IgnoreCase)) return "S3";
            if (Regex.IsMatch(carName, @"\b(?:s|step|stage)\s*2\b", RegexOptions.IgnoreCase)) return "S2";
            if (Regex.IsMatch(carName, @"\b(?:s|step|stage)\s*1\b", RegexOptions.IgnoreCase)) return "S1";
            if (Regex.IsMatch(carName, @"\b(?:race|hotlap)\b", RegexOptions.IgnoreCase)) return "Race";
            if (Regex.IsMatch(carName, @"\b(?:turbo)\b", RegexOptions.IgnoreCase)) return "Turbo";
            return null;
        }

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
                CreateExtraDialogButton(AppStrings.Common_SelectFile, SelectFile), 
                CreateExtraDialogButton(AppStrings.Common_ViewUserFolder, () => FilesStorage.Instance.OpenContentDirectoryInExplorer(ContentCategory.UpgradeIcons)),
                CancelButton
            };

            Closing += UpgradeIconEditor_Closing;
        }

        private void UpgradeIconEditor_Closing(object sender, CancelEventArgs e) {
            var currentPage = Tabs.Frame.Content as IFinishableControl;
            currentPage?.Finish(MessageBoxResult == MessageBoxResult.OK);
        }

        private void SelectFile() {
            var dialog = new OpenFileDialog {
                Filter = FileDialogFilters.ImagesFilter,
                Title = AppStrings.UpgradeIcon_SelectNew
            };
            if (dialog.ShowDialog() == true) {
                ApplyFile(dialog.FileName);
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 1 && files[0] != null) {
                ApplyFile(files[0]);
            }
        }

        private void ApplyFile(string filename) {
            var cropped = ImageEditor.Proceed(filename, new Size(64, 64));
            if (cropped == null) return;

            try {
                cropped.SaveAsPng(Car.UpgradeIcon);
            } catch (IOException ex) {
                NonfatalError.Notify(AppStrings.UpgradeIcon_CannotChange, AppStrings.UpgradeIcon_CannotChange_Commentary, ex);
                return;
            } catch (Exception ex) {
                NonfatalError.Notify(AppStrings.UpgradeIcon_CannotChange, ex);
                return;
            }

            var saveAs = Prompt.Show(AppStrings.UpgradeIcon_AddAs, AppStrings.Common_AddToLibrary, Path.GetFileNameWithoutExtension(filename));
            if (saveAs == null) return;

            try {
                FilesStorage.Instance.AddUserContentToDirectory(ContentCategory.UpgradeIcons, Car.UpgradeIcon, saveAs);
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Common_CannotAddToLibrary, e);
            }

            Close();
        }
    }
}
