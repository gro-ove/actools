using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.PaintShop {
    public class ComplexCarPaint : CarPaint {
        public TextureFileName MapsTexture { get; }

        [CanBeNull]
        public PaintShopSource MapsMask { get; }

        [NotNull]
        public PaintShopSource MapsDefaultTexture { get; }

        public bool FixGloss { get; internal set; }

        public ComplexCarPaint([Localizable(false)] TextureFileName mapsTexture, [NotNull] PaintShopSource mapsSource, [CanBeNull] PaintShopSource mapsMask) {
            MapsTexture = mapsTexture;
            MapsMask = mapsMask;
            MapsDefaultTexture = mapsSource;
        }

        private bool _complexMode;

        [JsonProperty("complex")]
        public bool ComplexMode {
            get => _complexMode;
            set {
                if (Equals(value, _complexMode)) return;
                _complexMode = value;
                OnPropertyChanged();

                if (MapAspect != null) {
                    MapAspect.IsEnabled = value;
                }
            }
        }

        private double _reflection = 1d;

        [JsonProperty("reflection")]
        public double Reflection {
            get => _reflection;
            set {
                if (Equals(value, _reflection)) return;
                _reflection = value;
                OnPropertyChanged();
                MapAspect?.SetDirty();
            }
        }

        private double _gloss = 1d;

        [JsonProperty("gloss")]
        public double Gloss {
            get => _gloss;
            set {
                if (Equals(value, _gloss)) return;
                _gloss = value;
                OnPropertyChanged();
                MapAspect?.SetDirty();
            }
        }

        private double _specular = 1d;

        [JsonProperty("specular")]
        public double Specular {
            get => _specular;
            set {
                if (Equals(value, _specular)) return;
                _specular = value;
                OnPropertyChanged();
                MapAspect?.SetDirty();
            }
        }

        [CanBeNull]
        protected PaintableItemAspect MapAspect { get; private set; }

        protected override void Initialize() {
            base.Initialize();
            MapAspect = RegisterAspect(MapsTexture,
                    (name, renderer) => renderer.OverrideTextureMaps(name.FileName,
                            Reflection, Gloss, Specular, FixGloss, MapsDefaultTexture, MapsMask),
                    (location, name, renderer) => renderer.SaveTextureMapsAsync(Path.Combine(location, name.FileName), name.PreferredFormat,
                            Reflection, Gloss, Specular, FixGloss, MapsDefaultTexture, MapsMask),
                    _complexMode)
                    .Subscribe(MapsDefaultTexture, MapsMask);
        }
    }

    /*public class TxMaps : AspectsPaintableItem {
        public TextureFileName MapsTexture { get; }

        [CanBeNull]
        public PaintShopSource MapsMask { get; }

        [NotNull]
        public PaintShopSource MapsDefaultTexture { get; }

        public bool FixGloss { get; internal set; }

        public TxMaps([Localizable(false)] TextureFileName mapsTexture, [NotNull] PaintShopSource mapsSource, [CanBeNull] PaintShopSource mapsMask)
                : base(false) {
            MapsTexture = mapsTexture;
            MapsMask = mapsMask;
            MapsDefaultTexture = mapsSource;
        }

        private bool _complexMode;

        [JsonProperty("complex")]
        public bool ComplexMode {
            get => _complexMode;
            set {
                if (Equals(value, _complexMode)) return;
                _complexMode = value;
                OnPropertyChanged();

                if (MapAspect != null) {
                    MapAspect.IsEnabled = value;
                }
            }
        }

        private double _reflection = 1d;

        [JsonProperty("reflection")]
        public double Reflection {
            get => _reflection;
            set {
                if (Equals(value, _reflection)) return;
                _reflection = value;
                OnPropertyChanged();
                MapAspect?.SetDirty();
            }
        }

        private double _gloss = 1d;

        [JsonProperty("gloss")]
        public double Gloss {
            get => _gloss;
            set {
                if (Equals(value, _gloss)) return;
                _gloss = value;
                OnPropertyChanged();
                MapAspect?.SetDirty();
            }
        }

        private double _specular = 1d;

        [JsonProperty("specular")]
        public double Specular {
            get => _specular;
            set {
                if (Equals(value, _specular)) return;
                _specular = value;
                OnPropertyChanged();
                MapAspect?.SetDirty();
            }
        }

        [CanBeNull]
        protected PaintableItemAspect MapAspect { get; private set; }

        protected override void Initialize() {
            base.Initialize();
            MapAspect = RegisterAspect(MapsTexture,
                    (name, renderer) => renderer.OverrideTextureMaps(name.FileName,
                            Reflection, Gloss, Specular, FixGloss, MapsDefaultTexture, MapsMask),
                    (location, name, renderer) => renderer.SaveTextureMapsAsync(Path.Combine(location, name.FileName), name.PreferredFormat,
                            Reflection, Gloss, Specular, FixGloss, MapsDefaultTexture, MapsMask),
                    _complexMode)
                    .Subscribe(MapsDefaultTexture, MapsMask);
        }

        public override Color? GetColor(int colorIndex) {
            return null;
        }
    }*/
}