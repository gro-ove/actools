using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using AcManager.Annotations;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    public partial class UpgradeIconEditor_Library : IFinishableControl, INotifyPropertyChanged {
        private readonly string _key;
        private ObservableCollection<FilesStorage.ContentEntry> _icons;
        private FilesStorage.ContentEntry _selected;

        public CarObject Car { get; private set; }

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

        public UpgradeIconEditor_Library() {
            var mainDialog = UpgradeIconEditor.Instance;
            if (mainDialog != null) {
                Car = mainDialog.Car;
                _key = @"__upgradeiconeditor_" + Car.Id;
            }

            InitializeComponent();
            DataContext = this;

            FilesStorage.Instance.Watcher(ContentCategory.UpgradeIcons).Update += UpgradeIconEditor_Library_Update;
            UpdateIcons();
        }

        private void UpgradeIconEditor_Library_Update(object sender, EventArgs e) {
            UpdateIcons();
        }

        private void UpdateIcons() {
            Icons = new ObservableCollection<FilesStorage.ContentEntry>(FilesStorage.Instance.GetContentDirectory(ContentCategory.UpgradeIcons));

            if (Icons.Contains(Selected)) return;
            var previous = ValuesStorage.GetString(_key);
            Selected = (previous != null ? Icons.FirstOrDefault(x => x.Name == previous) : null) ?? (Icons.Count > 0 ? Icons[0] : null);
        }

        public void Finish(bool result) {
            FilesStorage.Instance.Watcher(ContentCategory.UpgradeIcons).Update -= UpgradeIconEditor_Library_Update;

            if (Selected == null) return;

            ValuesStorage.Set(_key, Selected.Name);

            try {
                if (File.Exists(Car.UpgradeIcon)) {
                    FileUtils.Recycle(Car.UpgradeIcon);
                }

                File.Copy(Selected.Filename, Car.UpgradeIcon);
            }  catch (Exception) {
                ModernDialog.ShowMessage("Can’t change upgrade icon.", ToolsStrings.Common_CannotDo, MessageBoxButton.OK);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
