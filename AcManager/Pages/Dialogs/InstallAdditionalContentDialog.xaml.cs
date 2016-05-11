using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using AcManager.Annotations;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers.AdditionalContentInstallation;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class InstallAdditionalContentDialog : INotifyPropertyChanged {
        public string Filename { get; set; }

        public class EntryWrapper : NotifyPropertyChanged {
            public AdditionalContentEntry Entry { get; }

            private bool _installEntry;

            public bool InstallEntry {
                get { return _installEntry; }
                set {
                    if (Equals(value, _installEntry)) return;
                    _installEntry = value;
                    OnPropertyChanged();
                }
            }

            private UpdateOption _selectedOption;

            public UpdateOption SelectedOption {
                get { return _selectedOption; }
                set {
                    if (Equals(value, _selectedOption)) return;
                    _selectedOption = value;
                    OnPropertyChanged();
                }
            }

            private readonly UpdateOption[] _updateOptionsList;

            public IReadOnlyList<UpdateOption> UpdateOptionsList => _updateOptionsList;

            public EntryWrapper(AdditionalContentEntry entry, bool isNew) {
                Entry = entry;
                InstallEntry = true;
                IsNew = isNew;

                if (isNew) return;
                _updateOptionsList = UpdateOption.GetByType(entry.Type).ToArray();
                SelectedOption = _updateOptionsList[0];
            }

            public bool IsNew { get; set; }

            public string DisplayName => (IsNew ? @"New " : @"Existed ") + Entry.Type.GetDescription() + ": " + Entry.Name;
        }

        public IReadOnlyList<EntryWrapper> Entries {
            get { return _entries; }
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));

                foreach (var wrapper in _entries) {
                    wrapper.PropertyChanged += (sender, args) => {
                        if (args.PropertyName == nameof(EntryWrapper.InstallEntry)) {
                            InstallCommand.OnCanExecuteChanged();
                        }
                    };
                }
            }
        }

        public bool IsEmpty => _entries?.Any() != true;

        private IAdditionalContentInstallator _installator;
        private CancellationTokenSource _cancellationTokenSource;

        [CanBeNull]
        private IReadOnlyList<EntryWrapper> _entries;

        public InstallAdditionalContentDialog(string filename) {
            Filename = filename;

            DataContext = this;
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton(FirstFloor.ModernUI.Resources.Ok, InstallCommand),
                CancelButton
            };
        }

        private bool _loaded;

        private void InstallAdditionalContentDialog_OnLoaded(object sender, RoutedEventArgs args) {
            if (_loaded) return;
            _loaded = true;

            CreateInstallator();
        }

        private async void CreateInstallator() {
            try {
                _installator = await AdditionalContentInstallation.FromFile(Filename);
            } catch (Exception e) {
                NonfatalError.Notify("Can't install content from file", e);
                Close();
                return;
            }

            var msg = @"Archive is encrypted. Input password:";
            while (_installator.IsPasswordRequired && !_installator.IsPasswordCorrect) {
                var password = Controls.Pages.Dialogs.Prompt.Show(@"Password required", msg, passwordMode: true);
                if (password == null) {
                    Close();
                    return;
                }

                try {
                    await _installator.TrySetPasswordAsync(password);
                    break;
                } catch (PasswordException) {
                    msg = @"Password is invalid, try again:";
                }
            }

            UpdateEntries();
        }

        private void InstallAdditionalContentDialog_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            _cancellationTokenSource?.Cancel();
            DisposeHelper.Dispose(ref _installator);
        }

        private IAcManagerNew GetManagerByType(AdditionalContentType type) {
            switch (type) {
                case AdditionalContentType.Car:
                    return CarsManager.Instance;

                case AdditionalContentType.Track:
                    return TracksManager.Instance;

                case AdditionalContentType.Showroom:
                    return ShowroomsManager.Instance;

                case AdditionalContentType.Font:
                    return FontsManager.Instance;

                case AdditionalContentType.CarSkin:
                    throw new NotImplementedException();

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private async void UpdateEntries() {
            Loading.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;

            try {
                using (_cancellationTokenSource = new CancellationTokenSource()) {
                    Entries = (await _installator.GetEntriesAsync(null, _cancellationTokenSource.Token)).Select(x => {
                        var manager = GetManagerByType(x.Type);
                        if (manager == null) return null;
                        var existed = manager.GetObjectById(x.Id);
                        return new EntryWrapper(x, existed == null);
                    }).Where(x => x != null).ToArray();
                }
            } catch (PasswordException e) {
                NonfatalError.Notify(@"Password is incorrect", e);
                Close();
            } catch (Exception e) {
                NonfatalError.Notify(@"Can't unpack", @"Maybe archive is damaged or password is incorrect.", e);
                Close();
            } finally {
                _cancellationTokenSource = null;
            }

            Loading.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
        }

        private AsyncCommand _installCommand;

        public AsyncCommand InstallCommand => _installCommand ?? (_installCommand = new AsyncCommand(async o => {
            using (var waiting = new WaitingDialog()) {
                foreach (var wrapper in Entries.Where(entry => entry.InstallEntry)) {
                    waiting.Title = $@"Installing {wrapper.Entry.Name}…";
                    if (waiting.CancellationToken.IsCancellationRequested) return;

                    try {
                        var manager = GetManagerByType(wrapper.Entry.Type) as IFileAcManager;
                        if (manager == null) continue;

                        var directory = manager.PrepareForAdditionalContent(wrapper.Entry.Id,
                                wrapper.SelectedOption != null && wrapper.SelectedOption.RemoveExisting);
                        await _installator.InstallEntryToAsync(wrapper.Entry, wrapper.SelectedOption?.Filter, directory, waiting, waiting.CancellationToken);
                    } catch (Exception e) {
                        NonfatalError.Notify(@"Can't install " + wrapper.Entry.Name, e);
                    }
                }
            }

            Close();
        }, o => Entries?.Any(x => x.InstallEntry) == true));

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
