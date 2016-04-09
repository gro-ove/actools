using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Annotations;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers.AdditionalContentInstallation;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using SevenZip;

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

        private void InstallAdditionalContentDialog_OnLoaded(object sender, RoutedEventArgs e) {
            CreateInstallator();
            if (!_installator.IsPasswordRequired || _installator.IsPasswordCorrect) {
                UpdateEntries();
            }
        }

        private void InstallAdditionalContentDialog_OnUnloaded(object sender, RoutedEventArgs e) {
            DisposeHelper.Dispose(ref _installator);
        }

        private void RequirePassword() {
            var password = Prompt.Show(@"Password required", @"Archive is encrypted. Input password:", passwordMode: true);
            if (password == null) {
                Close();
                return;
            }

            try {
                _installator.PasswordValue = password;
            } catch (PasswordException) {
                RequirePasswordAgain(password);
            }
        }

        private void RequirePasswordAgain(string oldPassword) {
            var password = Prompt.Show(@"Password required", @"Password is invalid, try again:", oldPassword, passwordMode: true);
            if (password == null) {
                Close();
                return;
            }

            try {
                _installator.PasswordValue = password;
            } catch (PasswordException) {
                RequirePasswordAgain(password);
            }
        }

        private void CreateInstallator() {
            _installator = AdditionalContentInstallation.FromFile(Filename);
            if (_installator.IsPasswordRequired) {
                RequirePassword();
            }
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
                Entries = await Task.Run(() => _installator.Entries.Select(x => {
                    var manager = GetManagerByType(x.Type);
                    if (manager == null) {
                        // TODO
                        return null;
                    }

                    var existed = manager.GetObjectById(x.Id);
                    return new EntryWrapper(x, existed == null);
                }).Where(x => x != null).ToArray());
            } catch (PasswordException e) {
                NonfatalError.Notify(@"Can't unpack", e);
                Close();
            } catch (ExtractionFailedException e) {
                NonfatalError.Notify(@"Can't unpack", @"Archive is damaged or password is incorrect.", e);
                Close();
            } catch (Exception e) {
                NonfatalError.Notify(@"Can't unpack", e);
                Close();
            }

            Loading.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
        }

        private void InstallEntry(EntryWrapper wrapper) {
            var manager = GetManagerByType(wrapper.Entry.Type) as IFileAcManager;
            if (manager == null) return;

            var directory = manager.PrepareForAdditionalContent(wrapper.Entry.Id, wrapper.SelectedOption != null && wrapper.SelectedOption.RemoveExisting);
            _installator.InstallEntryTo(wrapper.Entry, wrapper.SelectedOption?.Filter, directory);
        }

        private AsyncCommand _installCommand;

        public AsyncCommand InstallCommand => _installCommand ?? (_installCommand = new AsyncCommand(async o => {
            foreach (var entry in Entries.Where(entry => entry.InstallEntry)) {
                try {
                    using (WaitingDialog.Create("Installation in progress…")) {
                        await Task.Run(() => InstallEntry(entry));
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(@"Can't install " + entry.DisplayName, e);
                }
            }

            Close();
        }, o => Entries?.Any(x => x.InstallEntry) == true));

        private void InstallAdditionalContentDialog_OnClosing(object sender, CancelEventArgs eventArgs) {
            if (!IsResultOk || _installator == null) return;

        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
