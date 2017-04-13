using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcTools.Render.Kn5SpecificForward;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using LicensePlates;
using Newtonsoft.Json.Linq;

namespace AcManager.Controls.CustomShowroom {
    public static partial class PaintShop {
        public class LicensePlate : PaintableItem {
            public enum LicenseFormat {
                Europe, Japan
            }

            public LicensePlate(LicenseFormat format, string diffuseTexture = "Plate_D.dds", string normalsTexture = "Plate_NM.dds")
                    : this(format.ToString(), diffuseTexture, normalsTexture) {}

            public LicensePlate(string suggestedStyle, string diffuseTexture = "Plate_D.dds", string normalsTexture = "Plate_NM.dds")
                    : base(true) {
                SuggestedStyleName = suggestedStyle;
                DiffuseTexture = diffuseTexture;
                NormalsTexture = normalsTexture;
            }

            public string SuggestedStyleName { get; }

            public string DiffuseTexture { get; }

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
            private IPaintShopRenderer _renderer;

            private void ApplyQuick() {
                var applyId = ++_applyId;
                
                var diffuse = SelectedStyle?.CreateDiffuseMap(true, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                _renderer?.OverrideTexture(DiffuseTexture, diffuse == null ? null : new PaintShopSource(diffuse));
                if (_applyId != applyId) return;

                if (!_flatNormals) {
                    _flatNormals = true;
                    _renderer?.OverrideTexture(NormalsTexture, Color.FromRgb(127, 127, 255).ToColor(), 1d);
                }
            }

            private void ApplySlowDiffuse() {
                var applyId = ++_applyId;

                var diffuse = SelectedStyle?.CreateDiffuseMap(false, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                _renderer?.OverrideTexture(DiffuseTexture, diffuse == null ? null : new PaintShopSource(diffuse));
            }

            private void ApplySlowNormals() {
                var applyId = ++_applyId;

                var normals = SelectedStyle?.CreateNormalsMap(PreviewMode, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                _renderer?.OverrideTexture(NormalsTexture, normals == null ? null : new PaintShopSource(normals));
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

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                if (SelectedStyle == null) return;

                _renderer = renderer;
                EnsureThreadCreated();
                lock (_threadObj) {
                    ++_applyId;
                    _dirty = true;
                    Monitor.PulseAll(_threadObj);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                renderer.OverrideTexture(DiffuseTexture, null);
                renderer.OverrideTexture(NormalsTexture, null);
                _onlyPreviewModeChanged = false;
                _flatNormals = false;
            }

            protected override Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
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

            public override JObject Serialize() {
                if (SelectedStyleEntry == null || SelectedStyle == null) return null;

                var obj = new JObject { ["style"] = SelectedStyleEntry.Name };
                foreach (var p in SelectedStyle.InputParams) {
                    obj[@"param" + NameToId(p.Name, true)] = p.Value;
                }

                return obj;
            }

            public override void Deserialize(JObject data) {
                if (data == null) return;

                var style = data["style"]?.ToString();
                var selected = Styles.FirstOrDefault(x => string.Equals(x.Name, style, StringComparison.OrdinalIgnoreCase));
                if (selected == null) {
                    Logging.Warning($"Style not found: {style}");
                    return;
                }

                SelectedStyleEntry = selected;
                if (SelectedStyle == null) {
                    Logging.Unexpected();
                    return;
                }

                foreach (var pair in data) {
                    if (pair.Key.StartsWith(@"param")) {
                        var p = SelectedStyle.InputParams.FirstOrDefault(x => NameToId(x.Name, true) == pair.Key.Substring(5));
                        if (p == null) {
                            Logging.Warning($"Parameter not found: {pair.Key.Substring(6)}");
                        } else {
                            p.Value = pair.Value?.ToString();
                        }
                    }
                }

                base.Deserialize(data);
            }
        }
    }
}