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
using FirstFloor.ModernUI.Presentation;
using SevenZip;

namespace AcManager.Pages.Dialogs {
    public partial class InstallAdditionalContentDialog : INotifyPropertyChanged {
        public string Filename { get; set; }

        public class EntryWrapper : NotifyPropertyChanged {
            public AdditionalContentEntry Entry { get; private set; }

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

            public IReadOnlyList<UpdateOption> UpdateOptionsList {
                get { return _updateOptionsList; }
            }

            public class UpdateOption : NotifyPropertyChanged {
                private bool _enabled = true;

                public string Name { get; set; }

                public Func<string, bool> Filter { get; set; }

                public bool RemoveExisting { get; set; }

                public bool Enabled {
                    get { return _enabled; }
                    set {
                        if (Equals(value, _enabled)) return;
                        _enabled = value;
                        OnPropertyChanged();
                    }
                }

                public override string ToString() {
                    return Name;
                }
            }

            public EntryWrapper(AdditionalContentEntry entry, bool isNew) {
                Entry = entry;
                InstallEntry = true;
                IsNew = isNew;

                if (!isNew) {
                    _updateOptionsList = new[] {
                        new UpdateOption { Name = "Update Everytything" }, 
                        new UpdateOption { Name = "Remove Existing First", RemoveExisting = true },

                        entry.Type == AdditionalContentType.Car ? new UpdateOption { Name = "Keep Skins Previews" } : null,
                        entry.Type == AdditionalContentType.CarSkin ? new UpdateOption { Name = "Keep Preview" } : null,
                        entry.Type == AdditionalContentType.CarSkin ? new UpdateOption { Name = "Keep UI Information & Preview" } : null,
                    }.Union(UpdateOptions(entry.Type)).Where(x => x != null).ToArray();

                    SelectedOption = _updateOptionsList[0];
                }
            }

            private IEnumerable<UpdateOption> UpdateOptions(AdditionalContentType type) {
                switch (type) {
                    case AdditionalContentType.Car: {
                            Func<string, bool> uiFilter =
                                x => x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (
                                    !x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json")
                                );
                            Func<string, bool> previewsFilter =
                                x => !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");
                            yield return new UpdateOption { Name = "Keep UI Information", Filter = uiFilter };
                            yield return new UpdateOption { Name = "Keep Skins Previews", Filter = previewsFilter };
                            yield return new UpdateOption { Name = "Keep UI Information & Skins Previews", Filter = x => uiFilter(x) && previewsFilter(x) };
                            break;
                        }

                    case AdditionalContentType.Track: {
                            Func<string, bool> uiFilter =
                                x => !x.StartsWith(@"ui\") ||
                                     !x.EndsWith(@"\ui_track.json") && !x.EndsWith(@"\preview.png") &&
                                     !x.EndsWith(@"\outline.png");
                            yield return new UpdateOption { Name = "Keep UI Information", Filter = uiFilter };
                            break;
                        }

                    case AdditionalContentType.CarSkin: {
                            Func<string, bool> uiFilter = x => x != @"ui_skin.json";
                            Func<string, bool> previewFilter = x => x != @"preview.jpg";
                            yield return new UpdateOption { Name = "Keep UI Information", Filter = uiFilter };
                            yield return new UpdateOption { Name = "Keep Skins Preview", Filter = previewFilter };
                            yield return new UpdateOption { Name = "Keep UI Information & Skins Preview", Filter = x => uiFilter(x) && previewFilter(x) };
                            break;
                        }

                    case AdditionalContentType.Showroom: {
                            Func<string, bool> uiFilter =
                                x => x != @"ui\ui_showroom.json";
                            yield return new UpdateOption { Name = "Keep UI Information", Filter = uiFilter };
                            break;
                        }

                    case AdditionalContentType.Font:
                        break;
                }
            }

            public bool IsNew { get; set; }

            public string DisplayName {
                get {
                    var type = Entry.Type == AdditionalContentType.Car ? @"car " : Entry.Type == AdditionalContentType.CarSkin ? @"skin " : Entry.Type == AdditionalContentType.Track ? @"track " : Entry.Type == AdditionalContentType.Showroom ? @"showroom " : null;
                    return (IsNew ? @"New " : @"Existed ") + (type ?? "") + Entry.Name;
                }
            }
        }

        public IReadOnlyList<EntryWrapper> Entries {
            get { return _entries; }
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();
                OnPropertyChanged("IsEmpty");
            }
        }

        public bool IsEmpty {
            get { return !_entries.Any(); }
        }

        private IAdditionalContentInstallator _installator;
        private IReadOnlyList<EntryWrapper> _entries;

        public InstallAdditionalContentDialog(string filename) {
            Filename = filename;

            InitializeComponent();
            DataContext = this;

            Buttons = new[] { OkButton, CancelButton };
        }

        private void InstallAdditionalContentDialog_OnLoaded(object sender, RoutedEventArgs e) {
            CreateInstallator();
            if (!_installator.IsPasswordRequired || _installator.IsPasswordCorrect) {
                UpdateEntries();
            }
        }

        private void InstallAdditionalContentDialog_OnUnloaded(object sender, RoutedEventArgs e) {
            if (_installator != null) {
                _installator.Dispose();
                _installator = null;
            }
        }

        private void RequirePassword() {
            var password = Prompt.Show("Password required", "Archive is encrypted. Input password:", "");
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
            var password = Prompt.Show("Password required", "Password is invalid, try again:", oldPassword);
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
            throw new NotImplementedException();
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
                ShowMessage(e.Message, @"Can't unpack", MessageBoxButton.OK);
                Close();
            } catch (ExtractionFailedException) {
                ShowMessage(@"Archive is damaged or password is incorrect.", @"Can't unpack", MessageBoxButton.OK);
                Close();
            } catch (Exception e) {
                ShowMessage(@"Exception: " + e, @"Can't unpack", MessageBoxButton.OK);
                Close();
            }

            Loading.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
        }

        private void InstallEntry(EntryWrapper wrapper) {
            var manager = GetManagerByType(wrapper.Entry.Type) as IFileAcManager;
            if (manager == null) {
                return;
            }

            var directory = manager.PrepareForAdditionalContent(wrapper.Entry.Id,
                                                                wrapper.SelectedOption != null && wrapper.SelectedOption.RemoveExisting);
            _installator.InstallEntryTo(wrapper.Entry, wrapper.SelectedOption?.Filter, directory);
        }

        private void InstallAdditionalContentDialog_OnClosing(object sender, CancelEventArgs eventArgs) {
            if (!IsResultOk || _installator == null) return;

            try {
                foreach (var entry in Entries.Where(entry => entry.InstallEntry)) {
                    InstallEntry(entry);
                }
            } catch (Exception e) {
                ShowMessage(@"Exception: " + e, @"Can't unpack", MessageBoxButton.OK);
                eventArgs.Cancel = true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
