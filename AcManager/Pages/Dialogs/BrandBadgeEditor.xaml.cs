using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Pages.Dialogs {
    public partial class BrandBadgeEditor : IInvokingNotifyPropertyChanged {
        private FilesStorage.ContentEntry _selected;
        public CarObject Car { get; }

        public BetterObservableCollection<FilesStorage.ContentEntry> Icons { get; }

        public FilesStorage.ContentEntry Selected {
            get => _selected;
            set => this.Apply(value, ref _selected);
        }

        public BrandBadgeEditor(CarObject car) {
            Car = car;

            InitializeComponent();
            DataContext = this;
            Buttons = new[] {
                OkButton,
                CreateExtraDialogButton(AppStrings.Common_SelectFile, SelectFile),
                CreateExtraDialogButton(AppStrings.Common_ViewUserFolder, () => FilesStorage.Instance.OpenContentDirectoryInExplorer(ContentCategory.BrandBadges)),
                CancelButton
            };

            Closing += OnClosing;

            FilesStorage.Instance.Watcher(ContentCategory.BrandBadges).Update += OnBrandBadgesUpdate;
            Icons = new BetterObservableCollection<FilesStorage.ContentEntry>(FilesStorage.Instance.GetContentFiles(ContentCategory.BrandBadges));
            UpdateSelected();
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            FilesStorage.Instance.Watcher(ContentCategory.BrandBadges).Update -= OnBrandBadgesUpdate;

            if (MessageBoxResult != MessageBoxResult.OK) return;
            if (Selected == null) return;

            try {
                if (File.Exists(Car.BrandBadge)) {
                    FileUtils.Recycle(Car.BrandBadge);
                }

                File.Copy(Selected.Filename, Car.BrandBadge);
                BetterImage.Refresh(Car.BrandBadge);
            } catch (IOException ex) {
                NonfatalError.Notify(AppStrings.BrandBadge_CannotChange, AppStrings.BrandBadge_CannotChange_Commentary, ex);
            } catch (Exception ex) {
                NonfatalError.Notify(AppStrings.BrandBadge_CannotChange, ex);
            }
        }

        private void OnBrandBadgesUpdate(object sender, EventArgs e) {
            Icons.ReplaceEverythingBy(FilesStorage.Instance.GetContentFiles(ContentCategory.BrandBadges));
            UpdateSelected();
        }

        private void UpdateSelected() {
            if (Icons.Contains(Selected)) return;
            var brandLower = Car.Brand?.ToLower(CultureInfo.CurrentUICulture);
            Selected = Icons.FirstOrDefault(x => x.Name.ToLower() == brandLower) ?? (Icons.Count > 0 ? Icons[0] : null);
        }

        private void SelectFile() {
            var dialog = new OpenFileDialog {
                Filter = FileDialogFilters.ImagesFilter,
                Title = AppStrings.BrandBadge_SelectNew
            };

            if (dialog.ShowDialog() == true) {
                ApplyFile(dialog.FileName);
            }
        }

        private void BrandBadgeEditor_OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var file = ((string[])e.Data.GetData(DataFormats.FileDrop))?.FirstOrDefault();
            if (file != null) {
                ApplyFile(file);
            }
        }

        private void ApplyFile(string filename) {
            var cropped = ImageEditor.Proceed(filename, new Size(128, 128));
            if (cropped == null) return;

            try {
                cropped.SaveAsPng(Car.BrandBadge);
            } catch (IOException ex) {
                NonfatalError.Notify(AppStrings.BrandBadge_CannotChange, AppStrings.BrandBadge_CannotChange_Commentary, ex);
                return;
            } catch (Exception ex) {
                NonfatalError.Notify(AppStrings.BrandBadge_CannotChange, ex);
                return;
            }

            var saveAs = Prompt.Show(AppStrings.BrandBadge_AddAs, AppStrings.Common_AddToLibrary, Path.GetFileNameWithoutExtension(filename));
            if (saveAs == null) return;

            try {
                FilesStorage.Instance.AddUserContentToDirectory(ContentCategory.BrandBadges, Car.BrandBadge, saveAs);
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Common_CannotAddToLibrary, e);
            }

            Close();
        }

        private void BrandBadge_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) {
                e.Handled = true;
                CloseWithResult(MessageBoxResult.OK);
            } else if (e.ChangedButton == MouseButton.Right) {
                e.Handled = true;
                if (((FrameworkElement)sender).DataContext is FilesStorage.ContentEntry entry && entry.UserFile) {
                    var contextMenu = new ContextMenu();

                    var item = new MenuItem { Header = AppStrings.Toolbar_Delete };
                    item.Click += (o, args) => FilesStorage.Instance.Remove(entry);
                    contextMenu.Items.Add(item);
                    contextMenu.IsOpen = true;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void IInvokingNotifyPropertyChanged.OnPropertyChanged(string propertyName) {
            OnPropertyChanged(propertyName);
        }
    }
}
