using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        protected PaintableItem(bool enabledByDefault) {
            _enabled = enabledByDefault;
        }

        [CanBeNull]
        private IPaintShopRenderer _renderer;

        public void SetRenderer(IPaintShopRenderer renderer) {
            _renderer = renderer;
            Update();
        }

        [NotNull]
        public virtual Dictionary<int, Color> LiveryColors => new Dictionary<int, Color>(0);

        public int LiveryPriority { get; set; }

        private bool _enabled;

        [JsonProperty("enabled")]
        public bool Enabled {
            get => _enabled;
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnEnabledChanged();
                OnPropertyChanged();
            }
        }

        private bool _guessed;

        public bool Guessed {
            get => _guessed;
            internal set {
                if (Equals(value, _guessed)) return;
                _guessed = value;
                OnPropertyChanged();
            }
        }

        protected virtual void OnEnabledChanged() {}

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

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            base.OnPropertyChanged(propertyName);

            if (_renderer != null) {
                Update();
            }
        }

        protected virtual bool IsActive() {
            return Enabled;
        }

        protected void UpdateOverride() {
            var renderer = _renderer;
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

        public List<string> AffectedTextures { get; } = new List<string>(5);

        protected abstract void ApplyOverride([NotNull] IPaintShopRenderer renderer);

        protected abstract void ResetOverride([NotNull] IPaintShopRenderer renderer);

        [NotNull]
        protected abstract Task SaveOverrideAsync([NotNull] IPaintShopRenderer renderer, string location, CancellationToken cancellation);

        [NotNull]
        public async Task SaveAsync(string location, CancellationToken cancellation) {
            var renderer = _renderer;
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
            if (_renderer != null) {
                ResetOverride(_renderer);
            }

            _disposed = true;
        }

        [CanBeNull]
        public virtual JObject Serialize() {
            return JObject.FromObject(this);
        }

        [CanBeNull]
        protected JArray SerializeColors([CanBeNull] CarPaintColors target) {
            return target == null ? null : JArray.FromObject(target.ActualColors.Select(x => ColorExtension.ToHexString(x)));
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
    }
}