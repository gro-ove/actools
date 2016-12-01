using System.ComponentModel;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public class LinkGroup : Displayable {
        private string _groupKey;

        public string Id => $"{GroupKey}__{DisplayName}";

        private string KeySelected => @"LinkGroup.Selected_" + Id;

        private string KeyTemporary => @"LinkGroup.Temporary_" + Id;

        [Localizable(false)]
        public string GroupKey {
            get { return _groupKey ?? string.Empty; }
            set {
                if (_groupKey == value) return;
                _groupKey = value;
                OnPropertyChanged();
            }
        }

        private bool _initialized;

        public virtual void Initialize() {
            if (_initialized) return;
            _initialized = true;

            foreach (var p in ValuesStorage.GetStringList(KeyTemporary).Select(x => x.Split(new[] { '\n' }, 2))
                    .Where(x => x.Length == 2)) {
                var c = CustomLink.Deserialize(p[1]);
                if (c == null) {
                    continue;
                }

                if (string.IsNullOrEmpty(p[0])) {
                    Links.Insert(0, c);
                } else {
                    var after = Links.FirstOrDefault(x => x.Source.ToString() == p[0]);
                    if (after == null) continue;

                    var index = Links.IndexOf(after);
                    if (index < Links.Count - 1) {
                        Links.Insert(Links.IndexOf(after) + 1, c);
                    } else {
                        Links.Add(c);
                    }
                }
            }

            var source = ValuesStorage.GetUri(KeySelected);
            _selectedLink = Links.FirstOrDefault(x => x.Source == source) ?? Links.FirstOrDefault();
            Links.CollectionChanged += Links_CollectionChanged;
        }

        private void Links_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            ValuesStorage.Set(KeyTemporary, Links.Select((x, i) =>
                    x is CustomLink ? ((i > 0 ? Links[i - 1] : null)?.Source.ToString() ?? "") + '\n' + ((CustomLink)x).Serialize() : null).Where(x => x != null));
        }

        private Link _selectedLink;

        public virtual Link SelectedLink {
            get {
                Initialize();
                return _selectedLink;
            }
            set {
                Initialize();
                if (_selectedLink == value) return;
                _selectedLink = value;
                OnPropertyChanged();

                if (value?.Source != null) {
                    ValuesStorage.Set(KeySelected, value.Source);
                }
            }
        }

        [NotNull]
        public LinkCollection Links { get; } = new LinkCollection();

        private bool _isShown = true;

        public bool IsShown {
            get { return _isShown; }
            set {
                if (Equals(value, _isShown)) return;
                _isShown = value;
                OnPropertyChanged();
            }
        }
    }
}
