using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class Replacement : PaintableItem {
        [NotNull]
        private readonly TextureFileName[] _textures;

        public Dictionary<string, PaintShopSource> Replacements { get; }

        public Replacement([NotNull] TextureFileName[] textures, [NotNull] Dictionary<string, PaintShopSource> replacements) : base(false) {
            _textures = textures;
            Replacements = replacements;
            Value = Replacements.FirstOrDefault();
            AffectedTextures.AddRange(_textures.Select(x => x.FileName));
        }

        private KeyValuePair<string, PaintShopSource> _value;

        public KeyValuePair<string, PaintShopSource> Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            var value = Value.Value;
            if (value == null) return;
            foreach (var tex in _textures) {
                renderer.OverrideTexture(tex.FileName, value);
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            foreach (var tex in _textures) {
                renderer.OverrideTexture(tex.FileName, null);
            }
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            var value = Value.Value;
            if (value == null) return;
            foreach (var tex in _textures) {
                await renderer.SaveTextureAsync(Path.Combine(location, tex.FileName), tex.PreferredFormat, value);
                if (cancellation.IsCancellationRequested) return;
            }
        }

        public override JObject Serialize() {
            var result = base.Serialize();
            if (result != null) {
                result["value"] = PaintShop.NameToId(Value.Key, false);
            }

            return result;
        }

        public override void Deserialize(JObject data) {
            base.Deserialize(data);
            if (data != null) {
                var loaded = data["value"]?.ToString();
                var value = Replacements.FirstOrDefault(x => PaintShop.NameToId(x.Key, false) == loaded);
                if (value.Value != null) {
                    Value = value;
                }
            }
        }
    }
}