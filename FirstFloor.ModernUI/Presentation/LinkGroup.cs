using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Presentation {
    public class LimitedQueue<T> : List<T> {
        public int Limit { get; }

        public LimitedQueue(int limit) : base(limit) {
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
        }

        public void Enqueue(T obj) {
            if (Count >= Limit) {
                RemoveAt(0);
            }
            Add(obj);
        }

        public T Peek() {
            if (Count == 0) throw new InvalidOperationException("Empty queue");
            return this[Count - 1];
        }

        public T Dequeue() {
            var result = Peek();
            RemoveAt(Count - 1);
            return result;
        }

        public T DequeueOrDefault() {
            if (Count == 0) return default(T);
            var result = Peek();
            RemoveAt(Count - 1);
            return result;
        }
    }

    public class LinkGroup : Displayable {
        private string _groupKey;

        public string Id => $"{GroupKey}__{DisplayName}";

        private string KeySelected => "LinkGroup.Selected_" + Id;

        private string KeyTemporary => "LinkGroup.Temporary_" + Id;

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
                    x is CustomLink ? ((i > 0 ? Links[i - 1] : null)?.Source.ToString() ?? "") + "\n" + ((CustomLink)x).Serialize() : null).Where(x => x != null));
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

        public LinkCollection Links { get; } = new LinkCollection();
    }

    public class LinkGroupFilterable : LinkGroup {
        private string KeyGroup => "__linkGroup_" + _source;

        private string KeySelected => "LinkGroupFilterable.Selected_" + _source;

        private string KeyRecentlyClosed => "LinkGroupFilterable.RecentlyClosed" + _source;

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

            var source = ValuesStorage.GetUri(KeySelected);
            _selectedLink = Links.FirstOrDefault(x => x.Source == source) ?? Links.FirstOrDefault();

            var rightLink = new LinkInputEmpty(Source);
            Links.Add(rightLink);
            rightLink.NewLink += Link_NewLink;

            RecentlyClosedQueue.AddRange(ValuesStorage.GetStringList(KeyRecentlyClosed));
        }

        private Link _selectedLink;
        public override Link SelectedLink {
            get {
                return _selectedLink;
            }
            set {
                if (_selectedLink == value) return;
                if (_selectedLink != null) {
                    PreviousSelectedQueue.Remove(value);
                    PreviousSelectedQueue.Enqueue(value);
                }

                _selectedLink = value;
                OnPropertyChanged();

                if (value?.Source != null) {
                    ValuesStorage.Set(KeySelected, value.Source);
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

        private void Link_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(LinkInput.DisplayName)) return;

            var link = sender as LinkInput;
            if (link == null) return;

            var sameValue = Links.OfType<LinkInput>().FirstOrDefault(x => x.DisplayName == link.DisplayName && x != link);
            if (sameValue != null) {
                Remove(sameValue);
            }

            SaveLinks();
        }

        private void AddAndSelect(string value) {
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
