using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcObjectNew : AcPlaceholderNew {
        public static string OptionDebugLoading = @"abarth500";

        protected readonly IAcManagerNew Manager;

        // Not really a nullable, but don’t rely on it too much.
        [CanBeNull]
        public IAcManagerNew GetManager() {
            return Manager;
        }

        protected AcObjectNew(IAcManagerNew manager, string id, bool enabled)
                : base(id, enabled) {
            Manager = manager;

            var typeName = GetType().Name;
            _isFavouriteKey = $"{typeName}:{id}:favourite";
            _ratingKey = $"{typeName}:{id}:rating";
        }

        public virtual void Reload() {
            Manager.Reload(Id);
        }

        protected class Measurement {
            private readonly string _m;
            private readonly string _p;
            private readonly Stopwatch _s;

            public Measurement(string s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
                Logging.Debug(s == null ? "Measurement started" : "Measurement started: " + s, m, p, l);
                _s = Stopwatch.StartNew();
                _m = m;
                _p = p;
            }

            public void Step(string s, [CallerLineNumber] int l = -1) {
                Logging.Debug($"{s}: {_s.Elapsed.TotalMilliseconds:F1} ms", _m, _p, l);
                _s.Restart();
            }
        }

        [CanBeNull]
        protected Measurement Measure(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            return OptionDebugLoading == @"*" || OptionDebugLoading == Id ? new Measurement(s?.ToString() ?? $@"0x{GetHashCode():X6}", m, p, l) : null;
        }

        protected void Verbose(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            if (OptionDebugLoading == @"*" || OptionDebugLoading == Id) {
                Logging.Debug(s, m, p, l);
            }
        }

        public abstract void Load();

        public virtual void PastLoad() {}

        private string _name;

        [CanBeNull]
        public virtual string Name {
            get => _name;
            protected set {
                if (Equals(value, _name)) return;
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public override string DisplayName => Name ?? Id;

        public override string ToString() {
            return DisplayName;
        }

        public bool Outdated { get; private set; }

        /// <summary>
        /// Call this from AcManager when object is being replaced or something else.
        /// </summary>
        public void Outdate() {
            Outdated = true;
            OnPropertyChanged(nameof(Outdated));
            OnAcObjectOutdated();
        }

        protected virtual void OnAcObjectOutdated() {
            AcObjectOutdated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AcObjectOutdated;

        #region Date, age, is new
        private bool _isNew;

        public bool IsNew {
            get => _isNew;
            set => Apply(value, ref _isNew);
        }

        public TimeSpan Age => DateTime.Now - CreationDateTime;

        public DateTime CreationDateTime { get; protected set; }

        public void CheckIfNew() {
            try {
                IsNew = DateTime.Now - CreationDateTime < SettingsHolder.Content.NewContentPeriod.TimeSpan;
            } catch (Exception) {
                IsNew = false;
            }
        }
        #endregion

        #region Rating
        public static void MoveRatings(Type type, string oldId, string newId, bool keepOld) {
            var typeName = type.Name;
            var isFavouriteOldKey = $"{typeName}:{oldId}:favourite";
            var ratingOldKey = $"{typeName}:{oldId}:rating";
            var isFavouriteNewKey = $"{typeName}:{newId}:favourite";
            var ratingNewKey = $"{typeName}:{newId}:rating";

            if (RatingsStorage.Contains(isFavouriteOldKey)) {
                RatingsStorage.Set(isFavouriteNewKey, RatingsStorage.Get<string>(isFavouriteOldKey));
                if (!keepOld) {
                    RatingsStorage.Remove(isFavouriteOldKey);
                }
            }

            if (RatingsStorage.Contains(ratingOldKey)) {
                RatingsStorage.Set(ratingNewKey, RatingsStorage.Get<string>(ratingOldKey));
                if (!keepOld) {
                    RatingsStorage.Remove(ratingOldKey);
                }
            }
        }

        public static void MoveRatings<T>(string oldId, string newId, bool keepOld) {
            MoveRatings(typeof(T), oldId, newId, keepOld);
        }

        private static Storage _ratingsStorage;

        private static Storage RatingsStorage
            => _ratingsStorage ?? (_ratingsStorage = new Storage(FilesStorage.Instance.GetFilename("Progress", "Ratings.data")));

        private readonly string _isFavouriteKey;
        private bool? _isFavourite;

        public bool IsFavourite {
            get => _isFavourite ?? (_isFavourite = RatingsStorage.Get<bool>(_isFavouriteKey)).Value;
            set {
                if (Equals(value, _isFavourite)) return;
                _isFavourite = value;

                if (value) {
                    RatingsStorage.Set(_isFavouriteKey, true);
                } else {
                    RatingsStorage.Remove(_isFavouriteKey);
                }

                OnPropertyChanged();
            }
        }

        private DelegateCommand _toggleFavoriteCommand;

        public DelegateCommand ToggleFavouriteCommand => _toggleFavoriteCommand ?? (_toggleFavoriteCommand = new DelegateCommand(() => {
            IsFavourite = !IsFavourite;
        }));

        private readonly string _ratingKey;
        private bool _ratingLoaded;
        private double? _rating;

        public double? Rating {
            get {
                if (!_ratingLoaded) {
                    _ratingLoaded = true;
                    _rating = RatingsStorage.Get<double?>(_ratingKey);
                }
                return _rating;
            }
            set {
                if (Equals(value, _rating)) return;
                _rating = value;
                _ratingLoaded = true;

                if (value.HasValue) {
                    RatingsStorage.Set(_ratingKey, value.Value);
                } else {
                    RatingsStorage.Remove(_ratingKey);
                }

                OnPropertyChanged();
            }
        }
        #endregion

        /*#region Custom attributes to save time (for UI and what have you)
        private readonly Dictionary<string, object> _something = new Dictionary<string, object>();

        public void Set<T>([NotNull] string key, [CanBeNull] T obj) where T : class {
            _something[key] = obj;
        }

        public void Remove([NotNull] string key) {
            _something.Remove(key);
        }

        [ContractAnnotation(@"=> result:null, false; => result:canbenull, true")]
        public bool TryGet<T>([NotNull] string key, out T result) where T : class {
            if (_something.TryGetValue(key, out var value) && value is T t) {
                result = t;
                return true;
            }

            result = default(T);
            return false;
        }
        #endregion*/
    }
}
