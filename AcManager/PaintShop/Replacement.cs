using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class Replacement : AspectsPaintableItem {
        [NotNull]
        private readonly PaintShopDestination[] _textures;

        public Dictionary<string, PaintShopSource> Replacements { get; }

        public Replacement([NotNull] PaintShopDestination[] textures, [NotNull] Dictionary<string, PaintShopSource> replacements) : base(false) {
            _textures = textures;
            Replacements = replacements;
            Value = Replacements.FirstOrDefault();
        }

        private KeyValuePair<string, PaintShopSource> _value;

        public KeyValuePair<string, PaintShopSource> Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();

                foreach (var a in Aspects) {
                    a.IsEnabled = value.Value != null;
                    a.SetDirty();
                }
            }
        }

        protected override void Initialize() {
            base.Initialize();
            foreach (var texture in _textures) {
                RegisterAspect(texture, name => new PaintShopOverrideWithTexture {
                    Source = Value.Value
                }, Value.Value != null).Subscribe(Replacements.Select(x => x.Value), c => Value.Value == c);
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