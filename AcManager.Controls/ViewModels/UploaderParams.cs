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
            SelectedUploader = UploadersList.GetByIdOrDefault(Storage.Get<string>(KeySelectedUploader)) ??
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
                UpdateDirectoriesCommand.Execute(default(CancellationToken));
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
            try {
                IsBusy++;
                await SelectedUploader.PrepareAsync(default(CancellationToken));
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
                SelectedUploader.DestinationDirectoryId = value?.Id;
            }
        }

        private DirectoryEntry[] _uploaderDirectories;

        [CanBeNull]
        public DirectoryEntry[] UploaderDirectories {
            get {
                if (_uploaderDirectories == null && SelectedUploader.IsReady) {
                    UpdateDirectoriesCommand.Execute(default(CancellationToken));
                }

                return _uploaderDirectories;
            }
            set {
                if (Equals(value, _uploaderDirectories)) return;
                _uploaderDirectories = value;
                OnPropertyChanged();
            }
        }

        private AsyncCommand<CancellationToken?> _logOutCommand;

        public AsyncCommand<CancellationToken?> LogOutCommand => _logOutCommand ?? (_logOutCommand = new AsyncCommand<CancellationToken?>(async c => {
            try {
                await SelectedUploader.ResetAsync(c ?? default(CancellationToken));
            } catch (Exception e) when (e.IsCancelled()) {
            } catch (WebException e) {
                NonfatalError.Notify("Can’t log out", ToolsStrings.Common_MakeSureInternetWorks, e);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t log out", e);
            }

            _signInCommand?.RaiseCanExecuteChanged();
            _logOutCommand?.RaiseCanExecuteChanged();
            _updateDirectoriesCommand?.RaiseCanExecuteChanged();
        }, c => SelectedUploader.IsReady));

        private AsyncCommand<CancellationToken?> _signInCommand;

        public AsyncCommand<CancellationToken?> SignInCommand => _signInCommand ?? (_signInCommand = new AsyncCommand<CancellationToken?>(async c => {
            try {
                await SelectedUploader.SignInAsync(c ?? default(CancellationToken));
            } catch (Exception e) when (e.IsCancelled()) {
            } catch (WebException e) {
                NonfatalError.Notify("Can’t sign in", ToolsStrings.Common_MakeSureInternetWorks, e);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t sign in", e);
            }

            _signInCommand?.RaiseCanExecuteChanged();
            _logOutCommand?.RaiseCanExecuteChanged();
            _updateDirectoriesCommand?.RaiseCanExecuteChanged();
        }, c => !SelectedUploader.IsReady));

        private AsyncCommand<CancellationToken?> _updateDirectoriesCommand;

        public AsyncCommand<CancellationToken?> UpdateDirectoriesCommand
            => _updateDirectoriesCommand ?? (_updateDirectoriesCommand = new AsyncCommand<CancellationToken?>(async c => {
                try {
                    IsBusy++;
                    UploaderDirectories = await SelectedUploader.GetDirectoriesAsync(c ?? default(CancellationToken));
                    UploaderDirectory = UploaderDirectories.GetChildByIdOrDefault(SelectedUploader.DestinationDirectoryId);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load list of directories", ToolsStrings.Common_MakeSureInternetWorks, e);
                } finally {
                    IsBusy--;
                }
            }, c => SelectedUploader.SupportsDirectories && SelectedUploader.IsReady));
    }
}