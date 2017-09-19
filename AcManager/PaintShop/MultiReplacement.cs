using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class MultiReplacement : AspectsPaintableItem {
        public Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> Replacements { get; }

        public MultiReplacement(Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> replacements) : base(false) {
            Replacements = replacements;
            Value = Replacements.FirstOrDefault();
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

        protected override void Initialize() {
            base.Initialize();

            foreach (var texture in Replacements.Values.SelectMany(x => x.Keys).Distinct()) {
                RegisterAspect(texture, (name, renderer) => {
                    renderer.OverrideTexture(name.FileName, Value.Value.GetValueOrDefault(name));
                }, (location, name, renderer) => {
                    var value = Value.Value.GetValueOrDefault(name);
                    return value == null ? Task.Delay(0) : renderer.SaveTextureAsync(Path.Combine(location, name.FileName), name.PreferredFormat, value);
                }).Subscribe(Replacements.SelectMany(x => x.Value.Values).Distinct(), c => Value.Value.Values.Contains(c));
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

        public override Color? GetColor(int colorIndex) {
            return null;
        }
    }
}