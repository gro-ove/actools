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

namespace AcManager.PaintShop {
    public class LicensePlate : PaintableItem {
        public enum LicenseFormat {
            Europe,
            Japan
        }

        public LicensePlate(LicenseFormat format, [CanBeNull] string diffuseTexture = "Plate_D.dds", [CanBeNull] string normalsTexture = "Plate_NM.dds")
                : this(format.ToString(), diffuseTexture, normalsTexture) { }

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

        [CanBeNull]
        public List<FilesStorage.ContentEntry> Styles {
            get => _styles;
            private set => Apply(value, ref _styles);
        }

        public void SetStyles(List<FilesStorage.ContentEntry> styles) {
            Styles = styles;
            SelectedStyleEntry = Styles.FirstOrDefault(x => x.Name == SelectedStyleEntry?.Name) ??
                    Styles.FirstOrDefault(x => String.Equals(x.Name, SuggestedStyleName, StringComparison.OrdinalIgnoreCase)) ??
                            Styles.FirstOrDefault(x => x.Name.IndexOf(SuggestedStyleName, StringComparison.OrdinalIgnoreCase) == 0) ??
                                    Styles.FirstOrDefault();
        }

        private LicensePlatesStyle _selectedStyle;
        private int _selectedStyleUsed;

        [CanBeNull]
        protected LicensePlatesStyle SelectedStyle {
            get => _selectedStyle;
            private set {
                if (Equals(value, _selectedStyle)) return;

                if (_selectedStyleUsed == 0) {
                    _selectedStyle?.Dispose();
                }

                _selectedStyle = value;
                _onlyPreviewModeChanged = false;
                OnPropertyChanged();

                if (value != null) {
                    InputParams.ReplaceEverythingBy(value.InputParams.Select(x => x.Clone()));
                } else {
                    InputParams.Clear();
                }
                Update();
            }
        }

        private class StyleHolder : IDisposable {
            public LicensePlatesStyle Style { get; set; }

            public Action Callback { get; set; }

            public void Dispose() {
                Callback?.Invoke();
            }
        }

        private StyleHolder Hold() {
            var styleSet = false;
            LicensePlatesStyle style = null;
            ActionExtension.InvokeInMainThreadAsync(() => {
                _selectedStyleUsed++;
                style = _selectedStyle;
                for (var i = 0; i < style.InputParams.Count; i++) {
                    style.InputParams[i].Value = InputParams.ElementAtOrDefault(i)?.Value;
                }
                styleSet = true;
            });
            while (!styleSet) {
                Thread.Sleep(10);
            }
            return new StyleHolder {
                Style = style,
                Callback = () => {
                    if (_selectedStyle != style) {
                        style?.Dispose();
                        return;
                    }
                    ActionExtension.InvokeInMainThreadAsync(() => {
                        if (_selectedStyle == style) {
                            _selectedStyleUsed--;
                        } else {
                            style.Dispose();
                        }
                    });
                }
            };
        }

        public ChangeableObservableCollection<PlateValueBase> InputParams { get; }
            = new ChangeableObservableCollection<PlateValueBase>();

        private static readonly string KeyPreviewMode = @"__PaintShop.LicensePlate.PreviewMode";
        private bool _previewMode = ValuesStorage.Get(KeyPreviewMode, true);

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

        private void ApplyTexture(Action<IPaintShopRenderer> action) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var renderer = _renderer;
                if (renderer != null) {
                    action(_renderer);
                }
            });
        }

        private void ApplyTexture(string name, byte[] data) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                _renderer?.Override(new PaintShopOverrideWithTexture {
                    Destination = new PaintShopDestination(name),
                    Source = data == null ? null : new PaintShopSource(data) { DoNotCache = true }
                });
            });
        }

        private void ApplyQuick(LicensePlatesStyle style) {
            var applyId = ++_applyId;
            var diffuseTexture = DiffuseTexture;
            var normalsTexture = NormalsTexture;

            if (diffuseTexture != null) {
                var diffuse = style?.CreateDiffuseMap(true, LicensePlatesStyle.Format.Png);
                if (_applyId != applyId) return;
                ApplyTexture(diffuseTexture, diffuse);
            }

            if (normalsTexture != null && !_flatNormals) {
                _flatNormals = true;
                ApplyTexture(r => r.Override(new PaintShopOverrideWithColor {
                    Destination = new PaintShopDestination(normalsTexture),
                    Color = Color.FromRgb(127, 127, 255).ToColor()
                }));
            }
        }

        private void ApplySlowDiffuse(LicensePlatesStyle style) {
            var applyId = ++_applyId;
            var diffuseTexture = DiffuseTexture;
            if (diffuseTexture == null) return;

            var diffuse = style?.CreateDiffuseMap(false, LicensePlatesStyle.Format.Png);
            if (_applyId != applyId) return;
            ApplyTexture(diffuseTexture, diffuse);
        }

        private void ApplySlowNormals(LicensePlatesStyle style) {
            var applyId = ++_applyId;
            var normalsTexture = NormalsTexture;
            if (normalsTexture == null) return;

            var normals = style?.CreateNormalsMap(PreviewMode, LicensePlatesStyle.Format.Png);
            if (_applyId != applyId) return;
            ApplyTexture(normalsTexture, normals);
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
                                        using (var held = Hold()) {
                                            ApplySlowNormals(held.Style);
                                        }
                                    } else {
                                        Update:
                                        using (var held = Hold()) {
                                            ApplyQuick(held.Style);
                                        }
                                        _dirty = false;

                                        for (var i = 0; i < 10; i++) {
                                            if (!_keepGoing) return;
                                            Monitor.Wait(_threadObj, 50);

                                            if (!_keepGoing) return;
                                            if (_dirty) goto Update;
                                        }

                                        using (var held = Hold()) {
                                            ApplySlowDiffuse(held.Style);
                                            ApplySlowNormals(held.Style);
                                        }
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
                } catch (ThreadAbortException) { } catch (Exception e) {
                    NonfatalError.Notify("Can’t keep License Plates Generator thread running", e);
                }
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
                renderer.Reset(DiffuseTexture);
            }

            if (NormalsTexture != null) {
                renderer.Reset(NormalsTexture);
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

            var obj = new JObject { [@"style"] = SelectedStyleEntry.Name };
            foreach (var p in SelectedStyle.InputParams.Where(x => x.Value != null)) {
                obj[@"param" + PaintShop.NameToId(p.Name, true)] = p.Value;
            }

            return obj;
        }

        public override void Deserialize(JObject data) {
            if (data == null) return;

            var style = data[@"style"]?.ToString();
            var selected = Styles?.FirstOrDefault(x => string.Equals(x?.Name, style, StringComparison.OrdinalIgnoreCase));
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
                    var p = SelectedStyle.InputParams.FirstOrDefault(x => PaintShop.NameToId(x.Name, true) == pair.Key.Substring(5));
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