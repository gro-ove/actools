using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class MultiReplacement : AspectsPaintableItem {
        public Dictionary<string, Dictionary<PaintShopDestination, PaintShopSource>> Replacements { get; }

        public MultiReplacement(Dictionary<string, Dictionary<PaintShopDestination, PaintShopSource>> replacements) : base(false) {
            Replacements = replacements;
            Value = Replacements.FirstOrDefault();
        }

        private KeyValuePair<string, Dictionary<PaintShopDestination, PaintShopSource>> _value;

        public KeyValuePair<string, Dictionary<PaintShopDestination, PaintShopSource>> Value {
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
                RegisterAspect(texture, name => new PaintShopOverrideWithTexture {
                    Source = Value.Value.GetValueOrDefault(name)
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