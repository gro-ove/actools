using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Internal;
using AcManager.Tools.Helpers.Api;
using AcTools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Managers.Online {
    public class ThirdPartyOnlineSourcesManager : NotifyPropertyChanged {
        public static bool OptionDevLobbies = false;

        public ChangeableObservableCollection<ThirdPartyOnlineSource> List { get; } = new ChangeableObservableCollection<ThirdPartyOnlineSource>();

        private static ThirdPartyOnlineSourcesManager _instance;

        public static ThirdPartyOnlineSourcesManager Instance => _instance ?? (_instance = new ThirdPartyOnlineSourcesManager());

        private readonly Busy _updateBusy = new Busy(true);

        private ThirdPartyOnlineSourcesManager() {
            List.CollectionChanged += (sender, args) => _updateBusy.DoDelay(OnUpdate, 500);
            List.ItemPropertyChanged += (sender, args) => _updateBusy.DoDelay(OnUpdate, 500);
        }

        public class LobbyEntry {
            public string Url;
            public string Title;
            public string Description;
            public string Flags;
        }

        private void SyncBuiltInLobbies(string encoded) {
            var seen = new HashSet<string>();
            foreach (var entry in JsonConvert.DeserializeObject<LobbyEntry[]>(encoded)) {
                if (string.IsNullOrEmpty(entry.Title) || seen.Contains(entry.Url)) continue;
                var existing = List.FirstOrDefault(x => x.Url == entry.Url);
                if (existing == null) {
                    List.Add(new ThirdPartyOnlineSource(true, entry.Url, entry.Title) { Description = entry.Description, Flags = entry.Flags });
                    Logging.Debug($"Third-party lobby: {entry.Url}");
                } else {
                    existing.DisplayName = entry.Title;
                    existing.Description = entry.Description;
                    existing.Flags = entry.Flags;
                    existing.IsBuiltIn = true;
                }
                seen.Add(entry.Url);
            }
            foreach (var toRemove in List.Where(x => x.IsBuiltIn && !seen.Contains(x.Url)).ToList()) {
                List.Remove(toRemove);
            }
            HasCmLobbies = List.Any(x => x.HasFlag("cm-lobby-v1"));
        }

        private void LoadUserLobbies() {
            var lobbies = ValuesStorage.Get(".os.list.user", @"[]");
            foreach (var entry in JsonConvert.DeserializeObject<LobbyEntry[]>(lobbies)) {
                if (string.IsNullOrEmpty(entry.Title)) continue;
                var existing = List.FirstOrDefault(x => x.Url == entry.Url);
                if (existing?.IsBuiltIn == true) {
                    continue;
                }
                if (existing == null) {
                    List.Add(new ThirdPartyOnlineSource(false, entry.Url, entry.Title) { Description = entry.Description, Flags = entry.Flags });
                    Logging.Debug($"User third-party lobby: {entry.Url}");
                } else {
                    existing.DisplayName = entry.Title;
                    existing.Description = entry.Description;
                    existing.Flags = entry.Flags;
                }
            }
            HasCmLobbies = List.Any(x => x.HasFlag("cm-lobby-v1"));
        }

        private bool _hasCmLobbies;

        public bool HasCmLobbies {
            get => _hasCmLobbies;
            set => Apply(value, ref _hasCmLobbies);
        }

        private readonly Busy _savingBusy = new Busy();

        public void SaveUserLobbies() {
            HasCmLobbies = List.Any(x => x.HasFlag("cm-lobby-v1"));
            _savingBusy.DoDelay(() => {
                ValuesStorage.Set(".os.list.user", JsonConvert.SerializeObject(List.Where(x => !x.IsBuiltIn).Select(x => new LobbyEntry {
                    Title = x.DisplayName,
                    Url = x.Url,
                    Description = x.Description,
                    Flags = x.Flags,
                }).ToArray()));
            }, 500);
        }

        private bool _initialized;

        public void Initialize() {
            if (_initialized) return;
            _initialized = true;
            
            SyncBuiltInLobbies(ValuesStorage.Get(".os.list", @"[]"));
            LoadUserLobbies();
            if (OptionDevLobbies || DateTime.Now.ToUnixTimestamp() - CacheStorage.Get(".os.list.date", 0) > 12 * 60 * 60) {
                HttpClientHolder.Get().GetStringAsync(InternalUtils.GetLobbyRegistryUrl(OptionDevLobbies))
                        .ContinueWithInMainThread(r => {
                            try {
                                if (!r.IsCompleted) {
                                    Logging.Warning($"Failed to load list of third-party lobbies: {r.Exception}");
                                    return;
                                }
                                SyncBuiltInLobbies(r.Result);
                                ValuesStorage.Set(".os.list", r.Result);
                                CacheStorage.Set(".os.list.date", DateTime.Now.ToUnixTimestamp());
                            } catch (Exception e) {
                                Logging.Warning($"Failed to parse list of third-party lobbies: {e}");
                            }
                        });
            }
        }

        public event EventHandler Update;

        private void OnUpdate() {
            Update?.Invoke(this, EventArgs.Empty);
        }
    }
}