using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.LargeFilesSharing;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsSharing {
        public class SharingViewModel : NotifyPropertyChanged {
            public SettingsHolder.SharingSettings Sharing => SettingsHolder.Sharing;
            public BetterObservableCollection<SharedEntry> History => SharingHelper.Instance.History;
            public ILargeFileUploader[] UploadersList => Uploaders.List;

            public SharingViewModel() {
                SelectedUploader = UploadersList.FirstOrDefault();
            }

            private ILargeFileUploader _selectedUploader;

            public ILargeFileUploader SelectedUploader {
                get => _selectedUploader;
                set {
                    if (Equals(value, _selectedUploader)) return;
                    _selectedUploader = value;
                    OnPropertyChanged();
                    _signInCommand?.RaiseCanExecuteChanged();
                    _updateDirectoriesCommand?.RaiseCanExecuteChanged();
                    _resetCommand?.RaiseCanExecuteChanged();

                    SelectedUploader.Prepare(default(CancellationToken)).Forget();
                }
            }

            private DirectoryEntry _uploaderDirectory;

            public DirectoryEntry UploaderDirectory {
                get => _uploaderDirectory;
                set {
                    if (Equals(value, _uploaderDirectory)) return;
                    _uploaderDirectory = value;
                    OnPropertyChanged();
                    SelectedUploader.DestinationDirectoryId = value.Id;
                }
            }

            private DirectoryEntry[] _uploaderDirectories;

            public DirectoryEntry[] UploaderDirectories {
                get => _uploaderDirectories;
                set {
                    if (Equals(value, _uploaderDirectories)) return;
                    _uploaderDirectories = value;
                    OnPropertyChanged();
                }
            }

            private CommandBase _signInCommand;

            public ICommand SignInCommand => _signInCommand ?? (_signInCommand = new AsyncCommand(async () => {
                if (SelectedUploader == null) return;

                try {
                    await SelectedUploader.SignIn(default(CancellationToken));
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t sign in", "Make sure Internet-connection works.", e);
                }

                _signInCommand?.RaiseCanExecuteChanged();
                _updateDirectoriesCommand?.RaiseCanExecuteChanged();
            }, () => SelectedUploader?.IsReady == false));

            private CommandBase _resetCommand;

            public ICommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
                SelectedUploader.Reset();

                _signInCommand?.RaiseCanExecuteChanged();
                _updateDirectoriesCommand?.RaiseCanExecuteChanged();
            }, () => SelectedUploader?.IsReady == true));

            private CommandBase _updateDirectoriesCommand;

            public ICommand UpdateDirectoriesCommand => _updateDirectoriesCommand ?? (_updateDirectoriesCommand = new AsyncCommand(async () => {
                if (SelectedUploader == null) return;

                try {
                    UploaderDirectories = await SelectedUploader.GetDirectories(default(CancellationToken));
                    UploaderDirectory = UploaderDirectories.GetChildByIdOrDefault(SelectedUploader.DestinationDirectoryId);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load list of directories", "Make sure Internet-connection works.", e);
                }
            }, () => SelectedUploader?.SupportsDirectories == true && SelectedUploader.IsReady));
        }

        public SettingsSharing() {
            InitializeComponent();
            DataContext = new SharingViewModel();
            Model.PropertyChanged += OnPropertyChanged;
        }

        public SharingViewModel Model => (SharingViewModel)DataContext;

        private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            Model.UploaderDirectory = UploaderDirectoriesTreeView.SelectedItem as DirectoryEntry ?? Model.UploaderDirectories?.FirstOrDefault();
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SharingViewModel.UploaderDirectories):
                    break;

                case nameof(SharingViewModel.UploaderDirectory):
                    UploaderDirectoriesTreeView.SetSelectedItem(Model.UploaderDirectory);
                    break;
            }
        }

        private void OnScrollMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e) {
            var value = HistoryDataGrid.SelectedValue as SharedEntry;
            if (value != null) {
                Process.Start(value.Url + "#noauto");
            }
        }
    }
}
