using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using LicensePlates;
using SlimDX;

namespace AcManager.Controls.CustomShowroom {
    public static class PaintShop {
        public abstract class PaintableItem : Displayable, IDisposable {
            protected PaintableItem(string diffuseTexture) {
                DiffuseTexture = diffuseTexture;
            }

            public string DiffuseTexture { get; }

            [CanBeNull]
            protected IPaintShopRenderer Renderer { get; private set; }

            public void SetRenderer(IPaintShopRenderer renderer) {
                Renderer = renderer;
            }

            private bool _enabled = true;

            public bool Enabled {
                get { return _enabled; }
                set {
                    if (Equals(value, _enabled)) return;

                    if (value) {
                        try {
                            Apply();
                        } catch (NotImplementedException) {
                            return;
                        }
                    } else {
                        Reset();
                    }

                    _enabled = value;
                    OnPropertyChanged();
                }
            }

            private bool _updating;

            protected async void Update() {
                if (_updating) return;

                try {
                    _updating = true;
                    await Task.Delay(20);
                    if (_updating && _enabled && !_disposed) {
                        Apply();
                    }
                } finally {
                    _updating = false;
                }
            }

            public void ApplyImmediate() {
                if (!_updating) return;
                if (_enabled) {
                    Apply();
                }
                _updating = false;
            }

            protected override void OnPropertyChanged(string propertyName = null) {
                base.OnPropertyChanged(propertyName);
                if (_enabled) {
                    Update();
                }
            }

            protected abstract void Apply();

            protected virtual void Reset() {
                Renderer?.OverrideTexture(DiffuseTexture, null);
            }

            [NotNull]
            public abstract Task SaveAsync(string location);

            private bool _disposed;

            public virtual void Dispose() {
                _disposed = true;
            }
        }

        public class TransparentIfFlagged : PaintableItem {
            public TransparentIfFlagged(string diffuseTexture, bool byDefault, bool inverse) : base(diffuseTexture) {
                _flag = byDefault;
                _inverse = inverse;
            }

            private bool _flag;
            private readonly bool _inverse;

            public bool Flag {
                get { return _flag; }
                set {
                    if (Equals(value, _flag)) return;
                    _flag = value;
                    OnPropertyChanged();
                }
            }

            protected override void Apply() {
                if (_inverse ? !Flag : Flag) {
                    Renderer?.OverrideTexture(DiffuseTexture, System.Drawing.Color.Black, 0d);
                } else {
                    Renderer?.OverrideTexture(DiffuseTexture, null);
                }
            }

            public override Task SaveAsync(string location) {
                return ((_inverse ? !Flag : Flag) ? Renderer?.SaveTexture(Path.Combine(location, DiffuseTexture), System.Drawing.Color.Black, 0d) : null) ??
                        Task.Delay(0);
            }
        }

        public class LicensePlate : PaintableItem {
            public enum LicenseFormat {
                Europe, Japan
            }

            public LicensePlate(LicenseFormat format, string diffuseTexture = "Plate_D.dds", string normalsTexture = "Plate_NM.dds")
                    : this(format.ToString(), diffuseTexture, normalsTexture) {}

            public LicensePlate(string suggestedStyle, string diffuseTexture = "Plate_D.dds", string normalsTexture = "Plate_NM.dds")
                    : base(diffuseTexture) {
                SuggestedStyleName = suggestedStyle;
                NormalsTexture = normalsTexture;
            }

            public string SuggestedStyleName { get; }

            public string NormalsTexture { get; }

            public override string DisplayName { get; set; } = "License plate";

            private FilesStorage.ContentEntry _selectedStyleEntry;

            [CanBeNull]
            public FilesStorage.ContentEntry SelectedStyleEntry {
                get { return _selectedStyleEntry; }
                set {
                    if (Equals(value, _selectedStyleEntry)) return;
                    _selectedStyleEntry = value;
                    OnPropertyChanged();

                    SelectedStyle = value == null ? null : new LicensePlatesStyle(value.Filename);
                }
            }

            private List<FilesStorage.ContentEntry> _styles;

            public List<FilesStorage.ContentEntry> Styles {
                get { return _styles; }
                private set {
                    if (Equals(value, _styles)) return;
                    _styles = value;
                    OnPropertyChanged();
                }
            }

            public void SetStyles(List<FilesStorage.ContentEntry> styles) {
                Styles = styles;
                SelectedStyleEntry = Styles.FirstOrDefault(x => x.Name == SelectedStyleEntry?.Name) ??
                        Styles.FirstOrDefault(x => string.Equals(x.Name, SuggestedStyleName, StringComparison.OrdinalIgnoreCase)) ??
                                Styles.FirstOrDefault(x => x.Name.IndexOf(SuggestedStyleName, StringComparison.OrdinalIgnoreCase) == 0) ??
                                        Styles.FirstOrDefault();
            }

            private LicensePlatesStyle _selectedStyle;

            [CanBeNull]
            public LicensePlatesStyle SelectedStyle {
                get { return _selectedStyle; }
                private set {
                    if (Equals(value, _selectedStyle)) return;

                    if (_selectedStyle != null) {
                        foreach (var inputParam in _selectedStyle.InputParams) {
                            inputParam.PropertyChanged -= OnStyleValueChanged;
                        }
                    }

                    _selectedStyle?.Dispose();
                    _selectedStyle = value;
                    _onlyPreviewModeChanged = false;
                    OnPropertyChanged();

                    if (value != null) {
                        foreach (var inputParam in value.InputParams) {
                            inputParam.PropertyChanged += OnStyleValueChanged;
                        }
                    }
                }
            }

            private bool _previewMode = true;

            public bool PreviewMode {
                get { return _previewMode; }
                set {
                    if (Equals(value, _previewMode)) return;
                    _previewMode = value;
                    _onlyPreviewModeChanged = true;
                    OnPropertyChanged();
                }
            }

            private bool _updating;

            private async void OnStyleValueChanged(object sender, PropertyChangedEventArgs e) {
                _onlyPreviewModeChanged = false;
                if (_updating) return;

                try {
                    _updating = true;
                    await Task.Delay(50);
                    Update();
                } finally {
                    _updating = false;
                }
            }

            private int _applyId;
            private bool _keepGoing, _dirty;
            private Thread _thread;
            private readonly object _threadObj = new object();

            private bool _flatNormals, _onlyPreviewModeChanged;

            private void ApplyQuick() {
                var applyId = ++_applyId;
                
                var diffuse = SelectedStyle?.CreateDiffuseMap(true, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                Renderer?.OverrideTexture(DiffuseTexture, diffuse);
                if (_applyId != applyId) return;

                if (!_flatNormals) {
                    _flatNormals = true;
                    Renderer?.OverrideTexture(NormalsTexture, Color.FromRgb(127, 127, 255).ToColor());
                }
            }

            private void ApplySlowDiffuse() {
                var applyId = ++_applyId;

                var diffuse = SelectedStyle?.CreateDiffuseMap(false, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                Renderer?.OverrideTexture(DiffuseTexture, diffuse);
            }

            private void ApplySlowNormals() {
                var applyId = ++_applyId;

                var normals = SelectedStyle?.CreateNormalsMap(PreviewMode, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                Renderer?.OverrideTexture(NormalsTexture, normals);
                _flatNormals = false;
            }

            private void EnsureThreadCreated() {
                if (_thread != null) return;

                _thread = new Thread(() => {
                    try {
                        while (_keepGoing) {
                            lock (_threadObj) {
                                if (_dirty) {
                                    try {
                                        if (_onlyPreviewModeChanged) {
                                            _onlyPreviewModeChanged = false;
                                            ApplySlowNormals();
                                        } else {
                                            Update:
                                            ApplyQuick();
                                            _dirty = false;

                                            for (var i = 0; i < 10; i++) {
                                                if (!_keepGoing) return;
                                                Monitor.Wait(_threadObj, 50);

                                                if (!_keepGoing) return;
                                                if (_dirty) goto Update;
                                            }

                                            ApplySlowDiffuse();
                                            ApplySlowNormals();
                                        }
                                    } catch (Exception e) {
                                        NonfatalError.Notify("Can’t generate number plate", e);
                                    } finally {
                                        _dirty = false;
                                    }
                                }

                                if (!_keepGoing) return;
                                Monitor.Wait(_threadObj);
                            }
                        }
                    } catch (ThreadAbortException) { }
                }) {
                    Name = "License Plates Generator",
                    IsBackground = true,
                    Priority = ThreadPriority.Lowest
                };

                _keepGoing = true;
                _thread.Start();
            }

            protected override void Apply() {
                if (SelectedStyle == null) return;

                EnsureThreadCreated();
                lock (_threadObj) {
                    ++_applyId;
                    _dirty = true;
                    Monitor.PulseAll(_threadObj);
                }
            }

            protected override void Reset() {
                base.Reset();
                _onlyPreviewModeChanged = false;
                _flatNormals = false;
            }

            public override Task SaveAsync(string location) {
                if (SelectedStyle == null) return Task.Delay(0);
                return Task.Run(() => {
                    SelectedStyle?.CreateDiffuseMap(false, Path.Combine(location, DiffuseTexture));
                    SelectedStyle?.CreateNormalsMap(false, Path.Combine(location, NormalsTexture));
                });
            }

            public override void Dispose() {
                _keepGoing = false;

                base.Dispose();
                SelectedStyle?.Dispose();
                SelectedStyle = null;

                if (_thread != null) {
                    lock (_threadObj) {
                        Monitor.PulseAll(_threadObj);
                    }

                    _thread.Abort();
                    _thread = null;
                }
            }
        }

        public class ColoredItem : PaintableItem {
            public ColoredItem([Localizable(false)] string diffuseTexture, Color defaultColor) : base(diffuseTexture) {
                DefaultColor = defaultColor;
            }

            public Color DefaultColor { get; }

            private Color? _color;

            public Color Color {
                get { return _color ?? DefaultColor; }
                set {
                    if (Equals(value, _color)) return;
                    _color = value;
                    OnPropertyChanged();
                }
            }

            protected override void Apply() {
                Renderer?.OverrideTexture(DiffuseTexture, Color.ToColor());
            }

            public override Task SaveAsync(string location) {
                return Renderer?.SaveTexture(Path.Combine(location, DiffuseTexture), Color.ToColor()) ?? Task.Delay(0);
            }
        }

        public class TintedWindows : ColoredItem {
            public double DefaultAlpha { get; set; }

            public TintedWindows([Localizable(false)] string diffuseTexture, double defaultAlpha = 0.23, Color? defaultColor = null)
                    : base(diffuseTexture, defaultColor ?? Color.FromRgb(41, 52, 55)) {
                DefaultAlpha = defaultAlpha;
            }

            public override string DisplayName { get; set; } = "Windows";

            private double? _alpha;

            public double Alpha {
                get { return _alpha ?? DefaultAlpha; }
                set {
                    if (Equals(value, _alpha)) return;
                    _alpha = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Transparency));
                }
            }

            public double Transparency {
                get { return 1d - Alpha; }
                set { Alpha = 1d - value; }
            }

            protected override void Apply() {
                Renderer?.OverrideTexture(DiffuseTexture, Color.ToColor(), Alpha);
            }

            public override Task SaveAsync(string location) {
                return Renderer?.SaveTexture(Path.Combine(location, DiffuseTexture), Color.ToColor(), Alpha) ?? Task.Delay(0);
            }
        }

        public class CarPaint : ColoredItem {
            public CarPaint(string detailsTexture , Color? defaultColor = null)
                    : base(detailsTexture, defaultColor ?? Color.FromRgb(255, 255, 255)) {}

            public bool SupportsFlakes { get; internal set; } = true;

            public override string DisplayName { get; set; } = "Car paint";

            private bool _flakes = true;

            public bool Flakes {
                get { return _flakes; }
                set {
                    if (Equals(value, _flakes)) return;
                    _flakes = value;
                    OnPropertyChanged();
                }
            }

            private Color _previousColor;
            private bool _previousFlakes;

            protected override void Apply() {
                if (_previousColor == Color && _previousFlakes == Flakes) return;
                _previousColor = Color;
                _previousFlakes = Flakes;

                if (SupportsFlakes && Flakes) {
                    Renderer?.OverrideTextureFlakes(DiffuseTexture, Color.ToColor());
                } else {
                    Renderer?.OverrideTexture(DiffuseTexture, Color.ToColor());
                }
            }

            public override Task SaveAsync(string location) {
                if (Renderer == null) return Task.Delay(0);
                return SupportsFlakes && Flakes ? Renderer.SaveTextureFlakes(Path.Combine(location, DiffuseTexture), Color.ToColor()) :
                        Renderer.SaveTexture(Path.Combine(location, DiffuseTexture), Color.ToColor());
            }
        }

        public class ComplexCarPaint : CarPaint {
            public string MapsTexture { get; }

            [Localizable(false)]
            public string TakeOriginalMapsFromDiffuse { get; internal set; }

            public bool AutoAdjustOriginalMaps { get; internal set; }

            public ComplexCarPaint([Localizable(false)] string detailsTexture, [Localizable(false)] string mapsTexture, Color? defaultColor = null)
                    : base(detailsTexture, defaultColor) {
                MapsTexture = mapsTexture;
            }

            private double _reflection = 1d;
            private double _previousReflection = -1d;

            public double Reflection {
                get { return _reflection; }
                set {
                    if (Equals(value, _reflection)) return;
                    _reflection = value;
                    OnPropertyChanged();
                }
            }

            private double _gloss = 1d;
            private double _previousGloss = -1d;

            public double Gloss {
                get { return _gloss; }
                set {
                    if (Equals(value, _gloss)) return;
                    _gloss = value;
                    OnPropertyChanged();
                }
            }

            private double _specular = 1d;
            private double _previousSpecular = -1d;

            public double Specular {
                get { return _specular; }
                set {
                    if (Equals(value, _specular)) return;
                    _specular = value;
                    OnPropertyChanged();
                }
            }

            protected override void Apply() {
                base.Apply();

                if (Math.Abs(_previousReflection - Reflection) > 0.001 || Math.Abs(_previousGloss - Gloss) > 0.001 ||
                        Math.Abs(_previousSpecular - Specular) > 0.001) {
                    Renderer?.OverrideTextureMaps(MapsTexture, Reflection, Gloss, Specular);
                    _previousReflection = Reflection;
                    _previousGloss = Gloss;
                    _previousSpecular = Specular;
                }
            }

            public override async Task SaveAsync(string location) {
                var renderer = Renderer;
                if (renderer == null) return;
                
                await base.SaveAsync(location);
                await renderer.SaveTextureMaps(Path.Combine(location, MapsTexture), MapsTexture, Reflection, Gloss, Specular);
            }
        }

        public static IEnumerable<PaintableItem> GetPaintableItems(string carId, [CanBeNull] Kn5 kn5) {
            if (!PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId)) {
                return new PaintableItem[0];
            }

            switch (carId) {
                case "peugeot_504":
                    return new PaintableItem[] {
                        new LicensePlate(LicensePlate.LicenseFormat.Europe),
                        new ComplexCarPaint("car_paint.dds", "carpaint_MAP.dds", Colors.BurlyWood),
                        new ColoredItem("rims_color.dds", Colors.CadetBlue) {
                            DisplayName = "Rims",
                            Enabled = false
                        },
                        new ColoredItem("int_inside_color.dds", Colors.Wheat) {
                            DisplayName = "Interior",
                            Enabled = false
                        },
                        new ColoredItem("int_leather_color.dds", Colors.Brown) {
                            DisplayName = "Leather",
                            Enabled = false
                        },
                        new ColoredItem("int_cloth_color.dds", Colors.Wheat) {
                            DisplayName = "Cloth",
                            Enabled = false
                        },
                        new TransparentIfFlagged("ext_headlight_yellow.dds", true, true) {
                            DisplayName = "Yellow headlights"
                        },
                        new TintedWindows("ext_glass.dds") {
                            Enabled = false
                        },
                    };

                case "peugeot_504_tn":
                    return new PaintableItem[] {
                        new LicensePlate(LicensePlate.LicenseFormat.Europe),
                        new ComplexCarPaint("tn_car_paint.dds", "tn_carpaint_MAP.dds") {
                            TakeOriginalMapsFromDiffuse = "tn_carpaint_SKIN.dds"
                        },
                        new ColoredItem("tn_rims_color.dds", Colors.Black) {
                            DisplayName = "Rims",
                            Enabled = false
                        },
                        new ColoredItem("tn_int_inside_color.dds", Colors.Wheat) {
                            DisplayName = "Interior",
                            Enabled = false
                        },
                        new ColoredItem("tn_int_leather_color.dds", Colors.Black) {
                            DisplayName = "Leather",
                            Enabled = false
                        },
                        new ColoredItem("tn_int_cloth_color.dds", Colors.Wheat) {
                            DisplayName = "Cloth",
                            Enabled = false
                        },
                        new TintedWindows("ext_glass.dds") {
                            Enabled = false
                        },
                    };

                case "acc_porsche_914-6":
                    return new PaintableItem[] {
                        new LicensePlate(LicensePlate.LicenseFormat.Europe),
                        new ComplexCarPaint("car_paint.dds", "ext_MAP.dds"),
                        new ColoredItem("car_paint_rims.dds", Colors.Black) {
                            DisplayName = "Rims",
                            Enabled = false
                        },
                        new TintedWindows("ext_glass.dds") {
                            Enabled = false
                        },
                    };
            }

            if (kn5 == null) return new PaintableItem[0];

            var carPaint = new[] { "Metal_detail.dds", "carpaint_detail.dds", "metal_detail.dds", "car_paint.dds", "carpaint.dds" }
                    .FirstOrDefault(x => kn5.Textures.ContainsKey(x));
            var mapsMap = kn5.Materials.Values.Where(x => x.ShaderName == "ksPerPixelMultiMap_damage_dirt")
                             .Select(x => x.GetMappingByName(@"txMaps")?.Texture)
                             .NonNull()
                             .FirstOrDefault();

            return new PaintableItem[] {
                kn5.Textures.ContainsKey("Plate_D.dds") && kn5.Textures.ContainsKey("Plate_NM.dds") ?
                        new LicensePlate(LicensePlate.LicenseFormat.Europe) : null,
                carPaint == null ? null : mapsMap == null ? new CarPaint(carPaint) : new ComplexCarPaint(carPaint, mapsMap),
                new[] { "car_paint_rims.dds" }.Where(x => kn5.Textures.ContainsKey(x))
                                              .Select(x => new ColoredItem(x, Colors.AliceBlue) {
                                                  DisplayName = "Rims",
                                                  Enabled = false
                                              })
                                              .FirstOrDefault(),
                new[] { "car_paint_roll_cage.dds" }.Where(x => kn5.Textures.ContainsKey(x))
                                                   .Select(x => new ColoredItem(x, Colors.AliceBlue) {
                                                       DisplayName = "Roll cage",
                                                       Enabled = false
                                                   })
                                                   .FirstOrDefault(),
                new[] { "ext_glass.dds" }.Where(x => kn5.Textures.ContainsKey(x))
                                         .Select(x => new TintedWindows(x) {
                                             Enabled = false
                                         })
                                         .FirstOrDefault(),
            }.Where(x => x != null);
        }
    }
}