using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Pages.Dialogs {
    public partial class BrandBadgeEditor : INotifyPropertyChanged {
        private ObservableCollection<FilesStorage.ContentEntry> _icons;
        private FilesStorage.ContentEntry _selected;
        public CarObject Car { get; }

        public ObservableCollection<FilesStorage.ContentEntry> Icons {
            get { return _icons; }
            private set {
                if (Equals(value, _icons)) return;
                _icons = value;
                OnPropertyChanged();
            }
        }

        public FilesStorage.ContentEntry Selected {
            get { return _selected; }
            private set {
                if (Equals(value, _selected)) return;
                _selected = value;
                OnPropertyChanged();
            }
        }

        public BrandBadgeEditor(CarObject car) {
            Car = car;

            InitializeComponent();
            DataContext = this;
            Buttons = new[] {
                OkButton,
                CreateExtraDialogButton("Select File", SelectFile), 
                CreateExtraDialogButton("View User Folder", o => FilesStorage.Instance.OpenContentFolderInExplorer(ContentCategory.BrandBadges)), 
                CancelButton
            };

            Closing += BrandBadgeEditor_Closing;

            FilesStorage.Instance.Watcher(ContentCategory.BrandBadges).Update += BrandBadgeEditor_Update;
            UpdateIcons();
        }

        private void BrandBadgeEditor_Closing(object sender, CancelEventArgs e) {
            FilesStorage.Instance.Watcher(ContentCategory.BrandBadges).Update -= BrandBadgeEditor_Update;

            if (MessageBoxResult != MessageBoxResult.OK) return;
            if (Selected == null) return;

            try {
                if (File.Exists(Car.BrandBadge)) {
                    FileUtils.Recycle(Car.BrandBadge);
                }

                File.Copy(Selected.Filename, Car.BrandBadge);
            } catch (Exception ex) {
                NonfatalError.Notify(@"Can't change brand badge.", "Make sure car's brand badge file is available to write.", ex);
            }
        }

        private void BrandBadgeEditor_Update(object sender, EventArgs e) {
            UpdateIcons();
        }

        private void UpdateIcons() {
            Icons = new ObservableCollection<FilesStorage.ContentEntry>(FilesStorage.Instance.GetContentDirectory(ContentCategory.BrandBadges));

            if (Icons.Contains(Selected)) return;
            var brandLower = Car.Brand?.ToLower(); // TODO: Weird bug?
            Selected = Icons.FirstOrDefault(x => x.Name.ToLower() == brandLower) ?? (Icons.Count > 0 ? Icons[0] : null);
        }

        private void SelectFile() {
            var dialog = new OpenFileDialog { Filter = FileDialogFilters.ImagesFilter, Title = "Select New Brand Badge" };
            if (dialog.ShowDialog() == true) {
                ApplyFile(dialog.FileName);
            }
        }

        private void BrandBadgeEditor_OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ApplyFile(files[0]);
        }

        private void ApplyFile(string filename) {
            var cropped = ImageEditor.Proceed(filename, new Size(128, 128));
            if (cropped == null) return;

            try {
                cropped.SaveAsPng(Car.BrandBadge);
            } catch (Exception) {
                ShowMessage(@"Can't change brand badge.", @"Fail", MessageBoxButton.OK);
                return;
            }

            var saveAs = Prompt.Show(@"Add into the library?", @"Add as:", Path.GetFileNameWithoutExtension(filename));
            if (saveAs == null) return;

            try {
                FilesStorage.Instance.AddUserContentToDirectory(ContentCategory.BrandBadges, Car.BrandBadge, saveAs);
            } catch (Exception) {
                ShowMessage(@"Can't add new element into the library.", @"Fail", MessageBoxButton.OK);
            }

            Close();
        }

        private void BrandBadge_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) {
                e.Handled = true;
                CloseWithResult(MessageBoxResult.OK);
            } else if (e.ChangedButton == MouseButton.Right) {
                e.Handled = true;
                var entry = ((FrameworkElement)sender).DataContext as FilesStorage.ContentEntry;
                if (entry == null || !entry.UserFile) return;

                var contextMenu = new ContextMenu();

                var item = new MenuItem { Header = "Remove" };
                item.Click += (o, args) => FilesStorage.Instance.Remove(entry);
                contextMenu.Items.Add(item);
                contextMenu.IsOpen = true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
