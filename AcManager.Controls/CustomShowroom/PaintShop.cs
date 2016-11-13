using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX;

namespace AcManager.Controls.CustomShowroom {
    public static class PaintShop {
        public abstract class PaintableItem : Displayable {
            protected PaintableItem(string diffuseTexture) {
                DiffuseTexture = diffuseTexture;
            }

            public string DiffuseTexture { get; }

            [CanBeNull]
            protected ToolsKn5ObjectRenderer Renderer { get; private set; }

            public void SetRenderer(ToolsKn5ObjectRenderer renderer) {
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

            private async void Update() {
                if (_updating) return;

                try {
                    _updating = true;
                    await Task.Delay(20);
                    if (_enabled) {
                        Apply();
                    }
                } finally {
                    _updating = false;
                }
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
                    : base(diffuseTexture) {
                NormalsTexture = normalsTexture;
            }

            public string NormalsTexture { get; }

            public override string DisplayName { get; set; } = "License plate";

            protected override void Apply() {
                throw new NotImplementedException();
            }

            public override Task SaveAsync(string location) {
                throw new NotImplementedException();
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

            protected override void Apply() {
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
            private double _previousReflection = 1d;

            public double Reflection {
                get { return _reflection; }
                set {
                    if (Equals(value, _reflection)) return;
                    _reflection = value;
                    OnPropertyChanged();
                }
            }

            private double _blur = 1d;
            private double _previousBlur = 1d;

            public double Blur {
                get { return _blur; }
                set {
                    if (Equals(value, _blur)) return;
                    _blur = value;
                    OnPropertyChanged();
                }
            }

            private double _specular = 1d;
            private double _previousSpecular = 1d;

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
                if (Math.Abs(_previousReflection - Reflection) > 0.001 || Math.Abs(_previousBlur - Blur) > 0.001 ||
                        Math.Abs(_previousSpecular - Specular) > 0.001) {
                    // Renderer?.OverrideTextureMaps(MapsTexture, Reflection, Blur, Specular);
                    _previousReflection = Reflection;
                    _previousBlur = Blur;
                    _previousSpecular = Specular;
                }
            }
        }

        public static IEnumerable<PaintableItem> GetPaintableItems(string carId, [CanBeNull] Kn5 kn5) {
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

            return kn5 == null ? new PaintableItem[0] : new PaintableItem[] {
                kn5.Textures.ContainsKey("Plate_D.dds") && kn5.Textures.ContainsKey("Plate_NM.dds") ?
                        new LicensePlate(LicensePlate.LicenseFormat.Europe) : null,
                new[] { "metal_detail.dds", "car_paint.dds", "carpaint.dds" }.Where(x => kn5.Textures.ContainsKey(x))
                                                                             .Select(x => new CarPaint(x))
                                                                             .FirstOrDefault(),
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