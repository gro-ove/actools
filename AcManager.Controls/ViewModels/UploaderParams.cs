using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.LargeFilesSharing;
using AcManager.Tools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class UploaderParams : NotifyPropertyChanged {
        public ILargeFileUploader[] UploadersList { get; }
        private string KeySelectedUploader = "selectedUploader";

        public UploaderParams(IStorage storage) {
            Storage = storage;
            UploadersList = Uploaders.GetUploaders(storage).ToArray();
            SelectedUploader = UploadersList.GetByIdOrDefault(Storage.GetString(KeySelectedUploader)) ??
                    UploadersList.First();
        }

        private ILargeFileUploader _selectedUploader;

        [NotNull]
        public ILargeFileUploader SelectedUploader {
            get => _selectedUploader;
            set {
                if (Equals(value, _selectedUploader)) return;

                var previous = _selectedUploader;
                if (previous != null) {
                    previous.PropertyChanged -= OnUploaderPropertyChanged;
                }

                _selectedUploader = value;
                _selectedUploader.PropertyChanged += OnUploaderPropertyChanged;

                OnPropertyChanged();
                _signInCommand?.RaiseCanExecuteChanged();
                _updateDirectoriesCommand?.RaiseCanExecuteChanged();
                _logOutCommand?.RaiseCanExecuteChanged();

                Storage.Set(KeySelectedUploader, value.Id);
                UploaderDirectories = null;

                if (previous != null) {
                    Prepare().Forget();
                }
            }
        }

        private string _uploaderId;

        public string UploaderId {
            get => _uploaderId;
            set {
                if (value == _uploaderId) return;
                _uploaderId = value;
                OnPropertyChanged();
            }
        }

        private string _directoryId;

        public string DirectoryId {
            get => _directoryId;
            set {
                if (value == _directoryId) return;
                _directoryId = value;
                OnPropertyChanged();
            }
        }

        public IStorage Storage { get; }

        private void OnUploaderPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName != nameof(SelectedUploader.IsReady)) return;
            if (SelectedUploader.IsReady) {
                UpdateDirectoriesCommand.Execute();
            } else {
                UploaderDirectories = null;
            }
        }

        private int _isBusy;

        public int IsBusy {
            get => _isBusy;
            set {
                if (Equals(value, _isBusy)) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public async Task Prepare() {
            if (SelectedUploader.IsReady && UploaderDirectories == null) {
                UpdateDirectoriesCommand.Execute();
                return;
            }

            try {
                IsBusy++;
                await SelectedUploader.Prepare(default(CancellationToken));
            } finally {
                IsBusy--;
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

        [CanBeNull]
        public DirectoryEntry[] UploaderDirectories {
            get => _uploaderDirectories;
            set {
                if (Equals(value, _uploaderDirectories)) return;
                _uploaderDirectories = value;
                OnPropertyChanged();
            }
        }

        private AsyncCommand _logOutCommand;

        public AsyncCommand LogOutCommand => _logOutCommand ?? (_logOutCommand = new AsyncCommand(async () => {
            try {
                await SelectedUploader.Reset();
            } catch (WebException e) {
                NonfatalError.Notify("Can’t log out", ToolsStrings.Common_MakeSureInternetWorks, e);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t log out", e);
            }

            _signInCommand?.RaiseCanExecuteChanged();
            _logOutCommand?.RaiseCanExecuteChanged();
            _updateDirectoriesCommand?.RaiseCanExecuteChanged();
        }, () => SelectedUploader.IsReady));

        private AsyncCommand _signInCommand;

        public AsyncCommand SignInCommand => _signInCommand ?? (_signInCommand = new AsyncCommand(async () => {
            try {
                await SelectedUploader.SignIn(default(CancellationToken));
            } catch (WebException e) {
                NonfatalError.Notify("Can’t sign in", ToolsStrings.Common_MakeSureInternetWorks, e);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t sign in", e);
            }

            _signInCommand?.RaiseCanExecuteChanged();
            _logOutCommand?.RaiseCanExecuteChanged();
            _updateDirectoriesCommand?.RaiseCanExecuteChanged();
        }, () => !SelectedUploader.IsReady));

        private AsyncCommand _updateDirectoriesCommand;

        public AsyncCommand UpdateDirectoriesCommand => _updateDirectoriesCommand ?? (_updateDirectoriesCommand = new AsyncCommand(async () => {
            try {
                IsBusy++;
                UploaderDirectories = await SelectedUploader.GetDirectories(default(CancellationToken));
                UploaderDirectory = UploaderDirectories.GetChildByIdOrDefault(SelectedUploader.DestinationDirectoryId);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t load list of directories", ToolsStrings.Common_MakeSureInternetWorks, e);
            } finally {
                IsBusy--;
            }
        }, () => SelectedUploader.SupportsDirectories && SelectedUploader.IsReady));
    }
}