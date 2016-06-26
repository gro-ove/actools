using System;
using System.ComponentModel;
using System.Linq;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Presentation {
    public class LinkChangedEventArgs : EventArgs {
        public LinkChangedEventArgs(string oldValue, string newValue) {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string OldValue { get; }

        public string NewValue { get; }
    }

    public class LinkGroupFilterable : LinkGroup {
        private string KeyGroup => "lgf_" + _source;

        private string KeySelected => ".lgf.s_" + _source;

        private string KeyRecentlyClosed => ".lgf.rc_" + _source;

        private bool _initialized;

        public override void Initialize() {
            if (_initialized) return;
            _initialized = true;

            Links.Clear();
            Links.Add(new Link {
                DisplayName = "All",
                Source = _source
            });

            foreach (var link in from x in ValuesStorage.GetStringList(KeyGroup)
                                 where !string.IsNullOrWhiteSpace(x)
                                 select new LinkInput(_source, x)) {
                Links.Add(link);
                link.PropertyChanged += Link_PropertyChanged;
                link.Close += Link_Close;
            }

            var source = ValuesStorage.GetString(KeySelected);
            _selectedLink = Links.FirstOrDefault(x => x.DisplayName == source) ?? Links.FirstOrDefault();

            var rightLink = new LinkInputEmpty(Source);
            Links.Add(rightLink);
            rightLink.NewLink += Link_NewLink;

            RecentlyClosedQueue.AddRange(ValuesStorage.GetStringList(KeyRecentlyClosed));
        }

        private Link _selectedLink;

        public override Link SelectedLink {
            get { return _selectedLink; }
            set {
                if (_selectedLink == value) return;
                if (_selectedLink != null) {
                    PreviousSelectedQueue.Remove(value);
                    PreviousSelectedQueue.Enqueue(value);
                }

                _selectedLink = value;
                OnPropertyChanged();

                if (value?.Source != null) {
                    ValuesStorage.Set(KeySelected, value.DisplayName);
                }
            }
        }

        private void SaveLinks() {
            ValuesStorage.Set(KeyGroup, from x in Links where x is LinkInput select x.DisplayName);
        }

        private void SaveRecentlyClosed() {
            ValuesStorage.Set(KeyRecentlyClosed, RecentlyClosedQueue);
        }

        public LimitedQueue<Link> PreviousSelectedQueue { get; } = new LimitedQueue<Link>(10);

        public static int OptionRecentlyClosedQueueSize = 10;

        public LimitedQueue<string> RecentlyClosedQueue { get; } = new LimitedQueue<string>(OptionRecentlyClosedQueueSize);

        public void RestoreLastClosed() {
            if (!RecentlyClosedQueue.Any()) return;
            AddAndSelect(RecentlyClosedQueue.Dequeue());
            SaveRecentlyClosed();
        }

        public event EventHandler<LinkChangedEventArgs> LinkChanged;

        private void Link_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(LinkInput.DisplayName)) return;

            var link = sender as LinkInput;
            if (link == null) return;

            LinkChanged?.Invoke(this, new LinkChangedEventArgs(link.PreviousValue, link.DisplayName));
            if (ReferenceEquals(link, SelectedLink)) {
                ValuesStorage.Set(KeySelected, link.DisplayName);
            }

            var sameValue = Links.OfType<LinkInput>().FirstOrDefault(x => x.DisplayName == link.DisplayName && x != link);
            if (sameValue != null) {
                Remove(sameValue);
            }

            SaveLinks();
        }

        public void AddAndSelect(string value) {
            var sameValue = Links.OfType<LinkInput>().FirstOrDefault(x => x.DisplayName == value);
            if (sameValue != null) {
                SelectedLink = sameValue;
                return;
            }

            var link = new LinkInput(_source, value);
            link.PropertyChanged += Link_PropertyChanged;
            link.Close += Link_Close;

            Links.Insert(Links.Count - 1, link);
            SelectedLink = link;

            SaveLinks();
        }

        private void Remove(LinkInput link) {
            PreviousSelectedQueue.Remove(link);

            link.PropertyChanged -= Link_PropertyChanged;
            link.Close -= Link_Close;

            if (SelectedLink == link) {
                SelectedLink = PreviousSelectedQueue.DequeueOrDefault() ?? Links[Links.IndexOf(link) - 1];
            }

            if (SelectedLink == link) {
                SelectedLink = Links.FirstOrDefault();
            }

            Links.Remove(link);
            SaveLinks();
        }

        private void Link_NewLink(object sender, NewLinkEventArgs e) {
            AddAndSelect(e.InputValue);
        }

        private void Link_Close(object sender, EventArgs e) {
            var link = sender as LinkInput;
            if (link == null) return;

            Remove(link);
            RecentlyClosedQueue.Enqueue(link.DisplayName);
            SaveRecentlyClosed();
        }

        private bool _isEnabled = true;

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private Uri _source;

        public Uri Source {
            get { return _source; }
            set {
                if (_source == value) return;
                _source = value;
                OnPropertyChanged();

                Initialize();
            }
        }
    }
}