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
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using LicensePlates;
using Newtonsoft.Json.Linq;

namespace AcManager.CustomShowroom {
    public static partial class PaintShop {
        public class LicensePlate : PaintableItem {
            public enum LicenseFormat {
                Europe, Japan
            }

            public LicensePlate(LicenseFormat format, [CanBeNull] string diffuseTexture = "Plate_D.dds", [CanBeNull] string normalsTexture = "Plate_NM.dds")
                    : this(format.ToString(), diffuseTexture, normalsTexture) {}

            public LicensePlate(string suggestedStyle, [CanBeNull] string diffuseTexture = "Plate_D.dds", [CanBeNull] string normalsTexture = "Plate_NM.dds")
                    : base(true) {
                SuggestedStyleName = suggestedStyle;
                DiffuseTexture = diffuseTexture;
                NormalsTexture = normalsTexture;
                UpdateDelay = 50;
                InputParams.ItemPropertyChanged += OnInputParamChanged;
            }

            private void OnInputParamChanged(object sender, PropertyChangedEventArgs e) {
                Update();
            }

            public string SuggestedStyleName { get; }

            [CanBeNull]
            public string DiffuseTexture { get; }

            [CanBeNull]
            public string NormalsTexture { get; }

            public override string DisplayName { get; set; } = "License plate";

            private FilesStorage.ContentEntry _selectedStyleEntry;

            [CanBeNull]
            public FilesStorage.ContentEntry SelectedStyleEntry {
                get => _selectedStyleEntry;
                set {
                    if (Equals(value, _selectedStyleEntry)) return;
                    _selectedStyleEntry = value;
                    OnPropertyChanged();

                    SelectedStyle = value == null ? null : new LicensePlatesStyle(value.Filename);
                }
            }

            private List<FilesStorage.ContentEntry> _styles;

            public List<FilesStorage.ContentEntry> Styles {
                get => _styles;
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
            private object _selectedStyleSync = new object();

            [CanBeNull]
            protected LicensePlatesStyle SelectedStyle {
                get => _selectedStyle;
                private set {
                    if (Equals(value, _selectedStyle)) return;

                    lock (_selectedStyleSync) {
                        _selectedStyle?.Dispose();
                        _selectedStyle = value;
                        _onlyPreviewModeChanged = false;
                        OnPropertyChanged();

                        lock (InputParams) {
                            if (value != null) {
                                InputParams.ReplaceEverythingBy(value.InputParams.Select(x => x.Clone()));
                            } else {
                                InputParams.Clear();
                            }
                        }
                    }
                }
            }

            public ChangeableObservableCollection<PlateValueBase> InputParams { get; }
                = new ChangeableObservableCollection<PlateValueBase>();

            private static readonly string KeyPreviewMode = @"__PaintShop.LicensePlate.PreviewMode";
            private bool _previewMode = ValuesStorage.GetBool(KeyPreviewMode, true);

            public bool PreviewMode {
                get => _previewMode;
                set {
                    if (Equals(value, _previewMode)) return;
                    _previewMode = value;
                    ValuesStorage.Set(KeyPreviewMode, value);
                    _onlyPreviewModeChanged = true;
                    OnPropertyChanged();
                }
            }

            private void OnStyleValueChanged(object sender, PropertyChangedEventArgs e) {
                _onlyPreviewModeChanged = false;
                Update();
            }

            private int _applyId;
            private bool _keepGoing, _dirty;
            private Thread _thread;

            private bool _flatNormals, _onlyPreviewModeChanged;
            private IPaintShopRenderer _renderer;

            private LicensePlatesStyle GetSelectedStyle() {
                lock (_selectedStyleSync) {
                    return _selectedStyle;
                }
            }

            private void ApplyTexture(Action<IPaintShopRenderer> action) {
                ActionExtension.InvokeInMainThreadAsync(() => {
                    var renderer = _renderer;
                    if (renderer != null) {
                        action(_renderer);
                    }
                });
            }

            private void ApplyQuick() {
                var applyId = ++_applyId;
                var diffuseTexture = DiffuseTexture;
                var normalsTexture = NormalsTexture;

                if (diffuseTexture != null) {
                    var diffuse = GetSelectedStyle()?.CreateDiffuseMap(true, LicensePlatesStyle.Format.Png);
                    if (_applyId != applyId) return;
                    ApplyTexture(r => r.OverrideTexture(diffuseTexture, diffuse == null ? null : new PaintShopSource(diffuse)));
                }

                if (normalsTexture != null && !_flatNormals) {
                    _flatNormals = true;
                    ApplyTexture(r => { r.OverrideTexture(normalsTexture, Color.FromRgb(127, 127, 255).ToColor(), 1d); });
                }
            }

            private void ApplySlowDiffuse() {
                var applyId = ++_applyId;
                var diffuseTexture = DiffuseTexture;
                if (diffuseTexture == null) return;

                var diffuse = GetSelectedStyle()?.CreateDiffuseMap(false, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                ApplyTexture(r => r.OverrideTexture(diffuseTexture, diffuse == null ? null : new PaintShopSource(diffuse)));
            }

            private void ApplySlowNormals() {
                var applyId = ++_applyId;
                var normalsTexture = NormalsTexture;
                if (normalsTexture == null) return;

                var normals = GetSelectedStyle()?.CreateNormalsMap(PreviewMode, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;

                ApplyTexture(r => r.OverrideTexture(normalsTexture, normals == null ? null : new PaintShopSource(normals)));
                _flatNormals = false;
            }

            private void SyncValues() {
                List<string> input;
                lock (InputParams) {
                    input = InputParams.Select(x => x.Value).ToList();
                }

                var style = GetSelectedStyle();
                for (var i = 0; i < style.InputParams.Count; i++) {
                    style.InputParams[i].Value = input.ElementAtOrDefault(i);
                }
            }

            private readonly object _threadObj = new object();

            private void EnsureThreadCreated() {
                if (_thread != null) return;

                _thread = new Thread(() => {
                    try {
                        lock (_threadObj) {
                            while (_keepGoing) {
                                if (_dirty) {
                                    try {
                                        if (_onlyPreviewModeChanged) {
                                            _onlyPreviewModeChanged = false;
                                            ApplySlowNormals();
                                        } else {
                                            Update:
                                            SyncValues();
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
                if (DiffuseTexture != null) {
                    renderer.OverrideTexture(DiffuseTexture, null);
                }

                if (NormalsTexture != null) {
                    renderer.OverrideTexture(NormalsTexture, null);
                }

                _onlyPreviewModeChanged = false;
                _flatNormals = false;
            }

            protected override Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                if (SelectedStyle == null) return Task.Delay(0);
                return Task.Run(() => {
                    if (DiffuseTexture != null) {
                        SelectedStyle?.CreateDiffuseMap(false, Path.Combine(location, DiffuseTexture));
                    }

                    if (NormalsTexture != null && !cancellation.IsCancellationRequested) {
                        SelectedStyle?.CreateNormalsMap(false, Path.Combine(location, NormalsTexture));
                    }
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
                foreach (var p in SelectedStyle.InputParams.Where(x => x.Value != null)) {
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
                        } else if (pair.Value.Type == JTokenType.String) {
                            var s = (string)pair.Value;
                            if (s != null) {
                                p.Value = s;
                            }
                        }
                    }
                }

                base.Deserialize(data);
            }
        }
    }
}