using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using StringBasedFilter;

namespace AcManager.Pages.Drive {
    public partial class Online {
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class OnlineQuickFilter : Displayable {
            public string Description { get; }
            public object Icon { get; }
            public bool Exclude { get; }
            public string Filter { get; }

            private bool _isEnabled;

            public bool IsEnabled {
                get => _isEnabled;
                set {
                    if (Equals(value, _isEnabled)) return;
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }

            [JsonConstructor]
            public OnlineQuickFilter(string name, string description, string icon, bool exclude, string filter) {
                DisplayName = ContentUtils.Translate(name);
                Description = ContentUtils.Translate(description);
                Icon = ContentUtils.GetIcon(ContentCategory.OnlineFilters, icon ?? name + ".png");
                Exclude = exclude;
                Filter = filter;
            }
        }

        public sealed class OnlineQuickFilters : ChangeableObservableCollection<OnlineQuickFilter>, IDisposable {
            private static readonly List<OnlineQuickFilters> Instances = new List<OnlineQuickFilters>();
            private static OnlineQuickFilter[] _filters;

            private static OnlineQuickFilter[] ReadFilters() {
                var file = FilesStorage.Instance.GetContentFile(ContentCategory.OnlineFilters, "Quick Filters.json");
                return file.Exists ? JsonConvert.DeserializeObject<OnlineQuickFilter[]>(File.ReadAllText(file.Filename)) : new OnlineQuickFilter[0];
            }

            private static OnlineQuickFilter[] Load() {
                if (_filters == null) {
                    _filters = ReadFilters();
                    FilesStorage.Instance.Watcher(ContentCategory.OnlineFilters).Update += OnUpdate;
                }

                return _filters;
            }

            private static void OnUpdate(object sender, EventArgs eventArgs) {
                _filters = ReadFilters();
                foreach (var i in Instances) {
                    i.Update();
                }
            }

            private void Update() {
                ReplaceEverythingBy_Direct(Load());
                LoadState();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public event EventHandler Changed;

            private readonly string _saveKey;
            private bool _loading;

            private void LoadState() {
                _loading = true;

                try {
                    var saved = LimitedStorage.Get(LimitedSpace.OnlineQuickFilter, _saveKey)?.Split('&');
                    if (saved != null) {
                        for (var i = 0; i < saved.Length; i++) {
                            var s = saved[i];
                            if (s.Length > 2 && s[0] == '(' && s[s.Length - 1] == ')') {
                                saved[i] = s.Substring(1, s.Length - 2);
                            }
                        }

                        foreach (var filter in this) {
                            filter.IsEnabled = Array.IndexOf(saved, filter.Filter) != -1;
                        }
                    }
                } finally {
                    _loading = false;
                }
            }

            public OnlineQuickFilters(string saveKey) : base(Load()) {
                Instances.Add(this);
                _saveKey = saveKey;
                LoadState();
            }

            private string GetFilterString() {
                return this.Select(x => x.IsEnabled ? $"({x.Filter})" : null).NonNull().JoinToString('&');
            }

            [CanBeNull]
            public IFilter<ServerEntry> CreateFilter() {
                var value = GetFilterString();
                return string.IsNullOrWhiteSpace(value) ? null : Filter.Create(ServerEntryTester.Instance, value);
            }

            protected override void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                base.OnItemPropertyChanged(sender, e);
                if (!_loading && e.PropertyName == nameof(OnlineQuickFilter.IsEnabled)) {
                    Changed?.Invoke(this, EventArgs.Empty);
                    LimitedStorage.Set(LimitedSpace.OnlineQuickFilter, _saveKey, GetFilterString());
                }
            }

            public void Dispose() {
                Instances.Remove(this);
            }
        }
    }
}