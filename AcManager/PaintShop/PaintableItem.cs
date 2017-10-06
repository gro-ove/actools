using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Base;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class PaintableItem : Displayable, IDisposable, IWithId {
        [CanBeNull]
        public string RefId { get; set; }

        protected PaintableItem(bool enabledByDefault) {
            _enabled = enabledByDefault;
        }

        [CanBeNull]
        protected IPaintShopRenderer Renderer { get; private set; }

        protected virtual void Initialize() { }

        public void SetRenderer(IPaintShopRenderer renderer) {
            Initialize();
            Renderer = renderer;
            Update();
        }

        private bool _enabled;

        [JsonProperty("enabled")]
        public bool Enabled {
            get => _enabled;
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnPropertyChanged();
                OnEnabledChanged();
                Update();
            }
        }

        protected virtual void OnEnabledChanged(){}

        private bool _guessed;

        public bool Guessed {
            get => _guessed;
            internal set {
                if (Equals(value, _guessed)) return;
                _guessed = value;
                OnPropertyChanged();
            }
        }

        private bool _updating;
        protected int UpdateDelay = 10;

        protected async void Update() {
            if (_updating) return;

            try {
                _updating = true;

                if (IsActive()) {
                    await Task.Delay(UpdateDelay);
                }

                if (_updating && !_disposed) {
                    UpdateOverride();
                }
            } finally {
                _updating = false;
            }
        }

        protected virtual bool IsActive() {
            return Enabled;
        }

        protected void UpdateOverride() {
            var renderer = Renderer;
            if (renderer == null) return;

            var r = (BaseRenderer)renderer;
            r.IsPaused = true;

            try {
                if (IsActive()) {
                    ApplyOverride(renderer);
                } else {
                    ResetOverride(renderer);
                }
            } catch (Exception e) {
                Logging.Error(e);
                Enabled = false;
            } finally {
                r.IsPaused = false;
            }
        }

        // public List<string> AffectedTextures { get; } = new List<string>(5);

        private readonly List<string> _affectedTextures = new List<string>(5);

        protected void AddAffectedTexture(PaintShopDestination destination) {
            if (destination.OutputMask == null) {
                _affectedTextures.Add(destination.TextureName);
            }
        }

        [Pure]
        public IEnumerable<string> GetAffectedTextures() {
            return _affectedTextures;
        }

        protected abstract void ApplyOverride([NotNull] IPaintShopRenderer renderer);
        protected abstract void ResetOverride([NotNull] IPaintShopRenderer renderer);

        [NotNull]
        protected abstract Task SaveOverrideAsync([NotNull] IPaintShopRenderer renderer, string location, CancellationToken cancellation);

        [NotNull]
        public async Task SaveAsync(string location, CancellationToken cancellation) {
            var renderer = Renderer;
            if (renderer == null) return;

            try {
                if (IsActive()) {
                    await SaveOverrideAsync(renderer, location, cancellation);
                }
            } catch (Exception e) {
                Logging.Error(e);
                Enabled = false;
            }
        }

        private bool _disposed;

        public virtual void Dispose() {
            if (Renderer != null) {
                ResetOverride(Renderer);
            }

            _disposed = true;
        }

        #region Livery-related
        [NotNull]
        public virtual Dictionary<int, Color> LiveryColors => new Dictionary<int, Color>(0);

        public int LiveryPriority { get; set; }
        #endregion

        #region Saving and loading
        [CanBeNull]
        public virtual JObject Serialize() {
            return JObject.FromObject(this);
        }

        [CanBeNull]
        protected JArray SerializeColors([CanBeNull] CarPaintColors target) {
            return target == null ? null : JArray.FromObject(target.ActualColors.Select(x => x.ToHexString()));
        }

        public virtual void Deserialize([CanBeNull] JObject data) {
            data?.Populate(this);
        }

        protected void DeserializeColors([NotNull] CarPaintColors target, JObject data, string key) {
            var colors = (data?[key] as JArray)?.ToObject<string[]>();
            if (colors != null) {
                for (var i = 0; i < colors.Length && i < target.Colors.Length; i++) {
                    target.Colors[i].Value = colors[i].ToColor() ?? Colors.White;
                }
            }
        }

        private string _id;
        public string Id => _id ?? (_id = PaintShop.NameToId(DisplayName, false));
        #endregion
    }

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class AspectsPaintableItem : PaintableItem {
        protected AspectsPaintableItem(bool enabledByDefault) : base(enabledByDefault) { }

        protected sealed override void ApplyOverride(IPaintShopRenderer renderer) {
            foreach (var aspect in _aspects.Where(x => !x.IsEnabled)) {
                if (aspect.Apply(renderer)) {
                    RaiseTextureChanged(aspect.TextureName);
                }
            }

            foreach (var aspect in _aspects.Where(x => x.IsEnabled)) {
                if (aspect.Apply(renderer)) {
                    RaiseTextureChanged(aspect.TextureName);
                }
            }
        }

        protected sealed override void ResetOverride(IPaintShopRenderer renderer) {
            foreach (var aspect in _aspects) {
                if (aspect.Reset(renderer)) {
                    RaiseTextureChanged(aspect.TextureName);
                }
            }
        }

        protected sealed override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            foreach (var aspect in _aspects.Where(x => x.IsEnabled)) {
                await aspect.SaveAsync(location, renderer);
                if (cancellation.IsCancellationRequested) return;
            }
        }

        #region Aspects
        private List<PaintableItemAspect> _aspects = new List<PaintableItemAspect>();
        protected IEnumerable<PaintableItemAspect> Aspects => _aspects;

        protected void SetAllDirty() {
            foreach (var value in _aspects) {
                value.SetDirty();
            }
        }

        protected PaintableItemAspect RegisterAspect([NotNull] PaintShopDestination destination,
                [NotNull] Action<PaintShopDestination, IPaintShopRenderer> apply, [NotNull] Func<string, PaintShopDestination, IPaintShopRenderer, Task> save,
                bool isEnabled = true) {
            AddAffectedTexture(destination);
            var created = new PaintableItemAspect(destination, apply, save, this) { IsEnabled = isEnabled };
            _aspects.Add(created);
            return created;
        }

        protected PaintableItemAspect RegisterAspect([NotNull] PaintShopDestination destination,
                [NotNull] Func<PaintShopDestination, PaintShopOverrideBase> getOverride,
                bool isEnabled = true) {
            AddAffectedTexture(destination);
            var created = new PaintableItemAspect(destination,
                    (d, r) => {
                        var o = getOverride(d);
                        if (o == null) {
                            r.Reset(d.TextureName);
                        } else {
                            if (o.Destination == null) {
                                o.Destination = d;
                            }

                            r.Override(o);
                        }
                    },
                    (l, d, r) => {
                        var o = getOverride(d);
                        if (o == null) return Task.Delay(0);

                        if (o.Destination == null) {
                            o.Destination = d;
                        }

                        return r.SaveAsync(l, o);
                    },
                    this) { IsEnabled = isEnabled };
            _aspects.Add(created);
            return created;
        }

        private void DirtyCallback() {
            if (Renderer != null) {
                Update();
            }
        }
        #endregion

        #region Referencial stuff
        private void Subscribe(IPaintShopSourceReference reference, Action callback, Func<bool> condition) {
            reference.Updated += (sender, args) => {
                if (condition?.Invoke() != false) {
                    callback();
                }
            };
        }

        private void Subscribe(PaintShopSource source, PaintableItemAspect aspect, Func<PaintShopSource, bool> condition) {
            var r = source.Reference;
            if (r != null) {
                Subscribe(r, aspect.SetDirty, condition == null ? (Func<bool>)null : () => condition(source));
            }
        }

        public abstract Color? GetColor(int colorIndex);
        public event EventHandler<ColorChangedEventArgs> ColorChanged;

        public void RaiseColorChanged(int? colorIndex) {
            ColorChanged?.Invoke(this, new ColorChangedEventArgs(colorIndex));
        }

        public event EventHandler<TextureChangedEventArgs> TextureChanged;

        public void RaiseTextureChanged(string textureName) {
            TextureChanged?.Invoke(this, new TextureChangedEventArgs(textureName));
        }
        #endregion

        public class PaintableItemAspect : NotifyPropertyChanged {
            public string TextureName => _destination.TextureName;

            private readonly PaintShopDestination _destination;
            private readonly Action<PaintShopDestination, IPaintShopRenderer> _apply;
            private readonly Func<string, PaintShopDestination, IPaintShopRenderer, Task> _save;
            private readonly AspectsPaintableItem _parent;

            public override string ToString() {
                return $"(PaintableItemAspect: {_destination.TextureName}, parent: {_parent.DisplayName})";
            }

            internal PaintableItemAspect(PaintShopDestination destination, Action<PaintShopDestination, IPaintShopRenderer> apply,
                    Func<string, PaintShopDestination, IPaintShopRenderer, Task> save, AspectsPaintableItem parent) {
                _destination = destination;
                _apply = apply;
                _save = save;
                _parent = parent;
            }

            public PaintableItemAspect Subscribe(params PaintShopSource[] source) {
                foreach (var s in source.NonNull()) {
                    _parent.Subscribe(s, this, null);
                }

                return this;
            }

            public PaintableItemAspect Subscribe([CanBeNull] IEnumerable<PaintShopSource> source, Func<PaintShopSource, bool> condition = null) {
                if (source != null) {
                    foreach (var s in source.NonNull()) {
                        _parent.Subscribe(s, this, condition);
                    }
                }
                return this;
            }

            private bool _isEnabled;

            public bool IsEnabled {
                get => _isEnabled;
                set {
                    if (value == _isEnabled) return;
                    _isEnabled = value;
                    IsDirty = true;
                    OnPropertyChanged();
                }
            }

            private bool _isDirty = true;

            public bool IsDirty {
                get => _isDirty;
                private set {
                    if (Equals(value, _isDirty)) return;
                    _isDirty = value;
                    OnPropertyChanged();

                    if (value) {
                        _parent.DirtyCallback();
                    }
                }
            }

            public void SetDirty() {
                if (IsEnabled) {
                    IsDirty = true;
                }
            }

            internal bool Apply(IPaintShopRenderer renderer) {
                if (!IsDirty) return false;
                IsDirty = false;

                if (IsEnabled) {
                    _apply.Invoke(_destination, renderer);
                } else {
                    renderer.Reset(_destination.TextureName);
                }

                return true;
            }

            internal bool Reset(IPaintShopRenderer renderer) {
                if (!IsEnabled) {
                    if (!IsDirty) return false;
                    IsDirty = false;
                } else {
                    IsDirty = true;
                }

                renderer.Reset(_destination.TextureName);
                return true;
            }

            internal Task SaveAsync(string location, IPaintShopRenderer renderer) {
                return IsEnabled ? _save.Invoke(location, _destination, renderer) : Task.Delay(0);
            }
        }
    }

    public class ColorChangedEventArgs : EventArgs {
        public readonly int? ColorIndex;

        public ColorChangedEventArgs(int? colorIndex) {
            ColorIndex = colorIndex;
        }
    }

    public class TextureChangedEventArgs : EventArgs {
        public readonly string TextureName;

        public TextureChangedEventArgs(string textureName) {
            TextureName = textureName;
        }
    }
}