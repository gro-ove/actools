using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Render.Kn5SpecificForward;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class MultiReplacement : PaintableItem {
        public Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> Replacements { get; }

        public MultiReplacement(Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> replacements) : base(false) {
            Replacements = replacements;
            Value = Replacements.FirstOrDefault();
            AffectedTextures.AddRange(Replacements.Values.SelectMany(x => x.Keys.Select(y => y.FileName)));
        }

        private KeyValuePair<string, Dictionary<TextureFileName, PaintShopSource>> _value;

        public KeyValuePair<string, Dictionary<TextureFileName, PaintShopSource>> Value {
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
            foreach (var pair in value) {
                renderer.OverrideTexture(pair.Key.FileName, pair.Value);
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            foreach (var tex in AffectedTextures) {
                renderer.OverrideTexture(tex, null);
            }
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            var value = Value.Value;
            if (value == null) return;
            foreach (var pair in value) {
                await renderer.SaveTextureAsync(Path.Combine(location, pair.Key.FileName), pair.Key.PreferredFormat, pair.Value);
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