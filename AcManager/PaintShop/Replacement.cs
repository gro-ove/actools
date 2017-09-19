using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class Replacement : AspectsPaintableItem {
        [NotNull]
        private readonly TextureFileName[] _textures;

        public Dictionary<string, PaintShopSource> Replacements { get; }

        public Replacement([NotNull] TextureFileName[] textures, [NotNull] Dictionary<string, PaintShopSource> replacements) : base(false) {
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
                RegisterAspect(texture, Apply, Save, Value.Value != null)
                        .Subscribe(Replacements.Select(x => x.Value), c => Value.Value == c);
            }
        }

        private void Apply(TextureFileName name, IPaintShopRenderer renderer) {
            var value = Value.Value;
            if (value == null) return;
            renderer.OverrideTexture(name.FileName, value);
        }

        private Task Save(string location, TextureFileName name, IPaintShopRenderer renderer) {
            var value = Value.Value;
            return value == null ? Task.Delay(0) : renderer.SaveTextureAsync(Path.Combine(location, name.FileName), name.PreferredFormat, value);
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