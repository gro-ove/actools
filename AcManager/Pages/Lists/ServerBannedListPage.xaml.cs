using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.InnerHelpers;
using AcManager.Tools.ServerPlugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class ServerBannedListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(StringTester.Instance, filter));
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public BetterListCollectionView View { get; }

            public ViewModel([CanBeNull] IFilter<string> filter) {
                View = new BetterListCollectionView(BlacklistStorage.Instance.Items);
                if (filter != null) {
                    View.Filter = x => x is BlacklistItem v && (filter.Test(v.Guid) || v.KnownAs != null && filter.Test(v.KnownAs));
                }
            }
        }

        public class BlacklistItem : NotifyPropertyChanged {
            public BlacklistItem(string guid) {
                Guid = guid;
                _knownAs = Lazier.Create(() => ServerGuidsStorage.GetAssociatedUserName(Guid));
            }

            public string Guid { get; }

            private Lazier<string> _knownAs;

            [CanBeNull]
            public string KnownAs => _knownAs.Value;

            private DelegateCommand _deleteCommand;

            public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => { BlacklistStorage.Instance.Remove(this); }));

            private DelegateCommand _openSteamWebpageCommand;

            public DelegateCommand OpenSteamWebpageCommand => _openSteamWebpageCommand ?? (_openSteamWebpageCommand = new DelegateCommand(
                    () => WindowsHelper.ViewInBrowser($"https://steamcommunity.com/profiles/{Guid}"),
                    () => Regex.IsMatch(Guid, @"^\d{16,18}$")));
        }

        public class BlacklistStorage {
            public static BlacklistStorage Instance { get; } = new BlacklistStorage();

            public BetterObservableCollection<BlacklistItem> Items { get; }

            private readonly string _filename;

            private BlacklistStorage() {
                _filename = Path.Combine(AcRootDirectory.Instance.Value ?? "", "server", "blacklist.txt");
                Items = new BetterObservableCollection<BlacklistItem>();
                if (File.Exists(_filename)) {
                    Items.ReplaceEverythingBy_Direct(ReadSafe().Select(x => new BlacklistItem(x)));
                }

                var directory = Path.GetDirectoryName(_filename);
                if (directory != null) {
                    var watcher = new DirectoryWatcher(directory, "blacklist.txt");
                    watcher.Update += (sender, args) => {
                        if (_watcherBusy.Is) return;
                        Items.ReplaceEverythingBy_Direct(ReadSafe().Select(x => new BlacklistItem(x)));
                    };
                }
            }

            private IEnumerable<string> ReadSafe() {
                try {
                    return File.ReadAllLines(_filename);
                } catch (Exception e) {
                    Logging.Error(e);
                    return new string[0];
                }
            }

            private Busy _watcherBusy = new Busy();
            private Busy _saveBusy = new Busy();

            public void Remove(BlacklistItem item) {
                if (Items.Remove(item)) {
                    _saveBusy.DoDelay(() => {
                        try {
                            _watcherBusy.Delay(500);
                            File.WriteAllText(_filename, Items.Select(x => x.Guid).JoinToString("\n"));
                        } catch (Exception e) {
                            NonfatalError.NotifyBackground("Failed to save blacklist", e);
                        }
                    }, 500);
                }
            }
        }
    }
}