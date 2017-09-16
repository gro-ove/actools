using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            AffectedTextures.Add(mapsTexture.FileName);
        }

        private bool _complexMode;
        private bool? _previousComplexMode;

        [JsonProperty("complex")]
        public bool ComplexMode {
            get => _complexMode;
            set {
                if (Equals(value, _complexMode)) return;
                _complexMode = value;
                OnPropertyChanged();
            }
        }

        private double _reflection = 1d;
        private double _previousReflection = -1d;

        [JsonProperty("reflection")]
        public double Reflection {
            get => _reflection;
            set {
                if (Equals(value, _reflection)) return;
                _reflection = value;
                OnPropertyChanged();
            }
        }

        private double _gloss = 1d;
        private double _previousGloss = -1d;

        [JsonProperty("gloss")]
        public double Gloss {
            get => _gloss;
            set {
                if (Equals(value, _gloss)) return;
                _gloss = value;
                OnPropertyChanged();
            }
        }

        private double _specular = 1d;
        private double _previousSpecular = -1d;

        [JsonProperty("specular")]
        public double Specular {
            get => _specular;
            set {
                if (Equals(value, _specular)) return;
                _specular = value;
                OnPropertyChanged();
            }
        }

        protected override void OnEnabledChanged() {
            base.OnEnabledChanged();
            _previousGloss = _previousReflection = _previousSpecular = -1d;
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            base.ApplyOverride(renderer);
            if (_previousComplexMode != _complexMode || Math.Abs(_previousReflection - Reflection) > 0.001 || Math.Abs(_previousGloss - Gloss) > 0.001 ||
                    Math.Abs(_previousSpecular - Specular) > 0.001) {
                if (ComplexMode) {
                    renderer.OverrideTextureMaps(MapsTexture.FileName, Reflection, Gloss, Specular, FixGloss,
                            MapsDefaultTexture, MapsMask);
                } else {
                    renderer.OverrideTexture(MapsTexture.FileName, null);
                }

                _previousComplexMode = _complexMode;
                _previousReflection = Reflection;
                _previousGloss = Gloss;
                _previousSpecular = Specular;
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            base.ResetOverride(renderer);
            renderer.OverrideTexture(MapsTexture.FileName, null);
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            await base.SaveOverrideAsync(renderer, location, cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (ComplexMode) {
                await renderer.SaveTextureMapsAsync(Path.Combine(location, MapsTexture.FileName), MapsTexture.PreferredFormat, Reflection, Gloss, Specular,
                        FixGloss, MapsDefaultTexture, MapsMask);
            }
        }
    }
}