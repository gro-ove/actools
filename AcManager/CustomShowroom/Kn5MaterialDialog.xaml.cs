using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.CustomShowroom {
    public partial class Kn5MaterialDialog : IDisposable {
        private static readonly string PresetableKey = "Kn5 Material";
        private ViewModel Model => (ViewModel)DataContext;

        private IDisposable _dispose;

        public Kn5MaterialDialog([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] IKn5 kn5,
                uint materialId) {
            ValuesStorage.Remove("__userpresets_p_" + PresetableKey);
            ValuesStorage.Remove("__userpresets_c_" + PresetableKey);

            var material = kn5.GetMaterial(materialId);
            if (material != null && renderer != null) {
                var properties = material.ShaderProperties.Select(x => x.Clone()).ToArray();
                var depthMode = material.DepthMode;
                var alphaTested = material.AlphaTested;
                var blendMode = material.BlendMode;
                _dispose = new ActionAsDisposable(() => {
                    foreach (var property in material.ShaderProperties) {
                        property.CopyFrom(properties.FirstOrDefault(x => x.Name == property.Name));
                    }

                    material.DepthMode = depthMode;
                    material.AlphaTested = alphaTested;
                    material.BlendMode = blendMode;
                    (renderer as IKn5ObjectRenderer)?.RefreshMaterial(kn5, materialId);
                });
            }

            DataContext = new ViewModel(renderer, kn5, activeSkin, materialId) { Close = Close };

            InitializeComponent();
            Buttons = new[] {
                kn5.IsEditable ? CreateExtraDialogButton(AppStrings.Toolbar_Save, new AsyncCommand(async () => {
                    var d = _dispose;
                    try {
                        _dispose = null;
                        await Model.UpdateKn5AndClose(false);
                    } catch (Exception e) {
                        _dispose = d;
                        NonfatalError.Notify("Can’t save material", e);
                    }
                }, () => Model.IsChanged).ListenOn(Model, nameof(Model.IsChanged)), true) : null,
                CancelButton
            };
        }

        protected override void OnClosingOverride(CancelEventArgs e) {
            if (_dispose != null && Model.IsChanged && ShowMessage(
                    "Some values are changed. Are you sure you want to dismiss changes?", "Some values are changed", MessageBoxButton.YesNo) !=
                    MessageBoxResult.Yes) {
                e.Cancel = true;
                return;
            }

            base.OnClosingOverride(e);
        }

        public void Dispose() {
            Model.Dispose();
            _dispose?.Dispose();
        }

        public abstract class MaterialValueBase : Displayable, IWithId {
            public string Id { get; }
            private readonly string _name;

            protected MaterialValueBase(string name) {
                Id = name;
                _name = BbCodeBlock.Encode(name);
            }

            public override string DisplayName {
                get => IsChanged ? $"[i]{_name}[/i]" : _name;
                set { }
            }

            private bool _isChanged;

            public bool IsChanged {
                get => _isChanged;
                protected set {
                    if (Equals(value, _isChanged)) return;
                    _isChanged = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }

            public void Apply() {
                ApplyOverride();
                IsChanged = false;
            }

            protected abstract void ApplyOverride();
        }

        public sealed class MaterialValueSingle : MaterialValueBase {
            public MaterialValueSingle(string name, float value) : base(name) {
                Value = (OriginalValue = value).ToInvariantString();
            }

            private float _originalValue;

            public float OriginalValue {
                get => _originalValue;
                set {
                    if (value.Equals(_originalValue)) return;
                    _originalValue = value;
                    OnPropertyChanged();
                }
            }

            private string _value;

            public string Value {
                get => _value;
                set {
                    if (value == _value) return;
                    _value = value;
                    IsChanged = value.As<float>() != OriginalValue;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _resetCommand;

            public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => { Value = OriginalValue.ToInvariantString(); }));

            protected override void ApplyOverride() {
                OriginalValue = Value.As<float>();
            }
        }

        public sealed class MaterialValue3D : MaterialValueBase {
            public MaterialValue3D(string name, Vec3 value) : base(name) {
                X = OriginalX = value.X.ToInvariantString();
                Y = OriginalY = value.Y.ToInvariantString();
                Z = OriginalZ = value.Z.ToInvariantString();
                OriginalValue = $"X={X}, Y={Y}, Z={Z}";
            }

            private string _originalX;

            public string OriginalX {
                get => _originalX;
                set {
                    if (value.Equals(_originalX)) return;
                    _originalX = value;
                    OnPropertyChanged();
                }
            }

            private string _originalY;

            public string OriginalY {
                get => _originalY;
                set {
                    if (value.Equals(_originalY)) return;
                    _originalY = value;
                    OnPropertyChanged();
                }
            }

            private string _originalZ;

            public string OriginalZ {
                get => _originalZ;
                set {
                    if (value.Equals(_originalZ)) return;
                    _originalZ = value;
                    OnPropertyChanged();
                }
            }

            private string _originalValue;

            public string OriginalValue {
                get => _originalValue;
                private set => Apply(value, ref _originalValue);
            }

            protected override void ApplyOverride() {
                OriginalX = X;
                OriginalY = Y;
                OriginalZ = Z;
                OriginalValue = $"X={X}, Y={Y}, Z={Z}";
            }

            private string _x;

            public string X {
                get => _x;
                set {
                    if (Equals(value, _x)) return;
                    _x = value;
                    IsChanged = _x != OriginalX || _y != OriginalY || _z != OriginalZ;
                    OnPropertyChanged();
                }
            }

            private string _y;

            public string Y {
                get => _y;
                set {
                    if (Equals(value, _y)) return;
                    _y = value;
                    IsChanged = _x != OriginalX || _y != OriginalY || _z != OriginalZ;
                    OnPropertyChanged();
                }
            }

            private string _z;

            public string Z {
                get => _z;
                set {
                    if (Equals(value, _z)) return;
                    _z = value;
                    IsChanged = _x != OriginalX || _y != OriginalY || _z != OriginalZ;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _resetCommand;

            public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
                X = OriginalX;
                Y = OriginalY;
                Z = OriginalZ;
            }));
        }

        public enum MaterialAlphaMode {
            [Description("Normal")]
            AlphaOff = 0,

            [Description("Alpha blending")]
            AlphaBlend = 1,

            [Description("Alpha testing")]
            AlphaTest = 2
        }

        public enum MaterialDepthMode {
            [Description("Normal")]
            DepthNormal = 0,

            [Description("Read-only")]
            DepthNoWrite = 1,

            [Description("Off")]
            DepthOff = 2
        }

        public class ViewModel : NotifyPropertyChanged, IUserPresetable, IDisposable {
            internal Action Close;

            [CanBeNull]
            private readonly BaseRenderer _renderer;

            [CanBeNull]
            private readonly CarSkinObject _activeSkin;

            private readonly IKn5 _kn5;
            private readonly uint _materialId;

            public ChangeableObservableCollection<MaterialValueSingle> ValuesSingle { get; } = new ChangeableObservableCollection<MaterialValueSingle>();
            public ChangeableObservableCollection<MaterialValue3D> Values3D { get; } = new ChangeableObservableCollection<MaterialValue3D>();

            private Kn5Material _material;

            [CanBeNull]
            public Kn5Material Material {
                get => _material;
                set => Apply(value, ref _material);
            }

            private string _usedFor;

            public string UsedFor {
                get => _usedFor;
                set => Apply(value, ref _usedFor);
            }

            public bool IsForkAvailable { get; }

            public bool IsChangeAvailable { get; }

            private bool _isChanged;

            public bool IsChanged {
                get => _isChanged;
                set => Apply(value, ref _isChanged);
            }

            private MaterialAlphaMode _originalAlphaMode;

            public MaterialAlphaMode OriginalAlphaMode {
                get => _originalAlphaMode;
                set {
                    if (Equals(value, _originalAlphaMode)) return;
                    _originalAlphaMode = value;
                    OnPropertyChanged();
                    UpdateIfChanged();
                }
            }

            private DelegateCommand _resetAlphaModeCommand;

            public DelegateCommand ResetAlphaModeCommand
                => _resetAlphaModeCommand ?? (_resetAlphaModeCommand = new DelegateCommand(() => { AlphaMode = OriginalAlphaMode; }));

            private MaterialAlphaMode _alphaMode;

            public MaterialAlphaMode AlphaMode {
                get => _alphaMode;
                set {
                    if (Equals(value, _alphaMode)) return;
                    _alphaMode = value;
                    OnPropertyChanged();
                    UpdateIfChanged();
                    Changed?.Invoke(this, EventArgs.Empty);

                    if (_material != null) {
                        _material.AlphaTested = value == MaterialAlphaMode.AlphaTest;
                        _material.BlendMode = value == MaterialAlphaMode.AlphaBlend ? Kn5MaterialBlendMode.AlphaBlend : Kn5MaterialBlendMode.Opaque;
                        (_renderer as IKn5ObjectRenderer)?.RefreshMaterial(_kn5, _materialId);
                    }
                }
            }

            public MaterialAlphaMode[] AlphaModes { get; } = EnumExtension.GetValues<MaterialAlphaMode>();

            private MaterialDepthMode _originalDepthMode;

            public MaterialDepthMode OriginalDepthMode {
                get => _originalDepthMode;
                set {
                    if (Equals(value, _originalDepthMode)) return;
                    _originalDepthMode = value;
                    OnPropertyChanged();
                    UpdateIfChanged();
                }
            }

            private DelegateCommand _resetDepthModeCommand;

            public DelegateCommand ResetDepthModeCommand
                => _resetDepthModeCommand ?? (_resetDepthModeCommand = new DelegateCommand(() => { DepthMode = OriginalDepthMode; }));

            private MaterialDepthMode _depthMode;

            public MaterialDepthMode DepthMode {
                get => _depthMode;
                set {
                    if (Equals(value, _depthMode)) return;
                    _depthMode = value;
                    OnPropertyChanged();
                    UpdateIfChanged();
                    Changed?.Invoke(this, EventArgs.Empty);

                    if (_material != null) {
                        _material.DepthMode = (Kn5MaterialDepthMode)value;
                        (_renderer as IKn5ObjectRenderer)?.RefreshMaterial(_kn5, _materialId);
                    }
                }
            }

            public MaterialDepthMode[] DepthModes { get; } = EnumExtension.GetValues<MaterialDepthMode>();

            private void UpdateIfChanged() {
                IsChanged = ValuesSingle.Any(x => x.IsChanged) || Values3D.Any(x => x.IsChanged) ||
                        _depthMode != _originalDepthMode || _alphaMode != _originalAlphaMode;
            }

            public ViewModel([CanBeNull] BaseRenderer renderer, [NotNull] IKn5 kn5, [CanBeNull] CarSkinObject activeSkin,
                    uint materialId) {
                _renderer = renderer;
                _activeSkin = activeSkin;
                _kn5 = kn5;
                _materialId = materialId;

                Material = kn5.GetMaterial(materialId);

                var usedFor = (from node in kn5.Nodes
                    where node.MaterialId == _materialId
                    orderby node.Name
                    select node.Name).ToList();
                IsForkAvailable = usedFor.Count > 1;
                IsChangeAvailable = kn5.TexturesData.Count > 1;
                UsedFor = usedFor.JoinToString(", ");

                if (Material != null && _kn5.IsEditable) {
                    _depthMode = _originalDepthMode = (MaterialDepthMode)Material.DepthMode;
                    _alphaMode = _originalAlphaMode = Material.AlphaTested ? MaterialAlphaMode.AlphaTest : Material.BlendMode == Kn5MaterialBlendMode.Opaque ?
                            MaterialAlphaMode.AlphaOff : MaterialAlphaMode.AlphaBlend;

                    ValuesSingle.ReplaceEverythingBy(Material.ShaderProperties.Where(x =>
                            x.Name != "ksEmissive" && x.Name != "damageZones" && x.Name != "groundNormal" && x.Name != "boh").Select(
                                    x => new MaterialValueSingle(x.Name, x.ValueA)));
                    Values3D.ReplaceEverythingBy(Material.ShaderProperties.Where(x => x.Name == "ksEmissive").Select(
                            x => new MaterialValue3D(x.Name, x.ValueC)));
                    ValuesSingle.ItemPropertyChanged += OnValueSingleChanged;
                    Values3D.ItemPropertyChanged += OnValue3DChanged;
                }
            }

            private void OnValueSingleChanged(object sender, PropertyChangedEventArgs args) {
                if (_disposed || args.PropertyName != nameof(MaterialValueSingle.Value)) return;

                UpdateIfChanged();
                Changed?.Invoke(this, EventArgs.Empty);

                if (_renderer is IKn5ObjectRenderer kn5Renderer) {
                    var v = (MaterialValueSingle)sender;
                    kn5Renderer.UpdateMaterialPropertyA(_kn5, _materialId, v.Id, v.Value.As<float>());
                }
            }

            private void OnValue3DChanged(object sender, PropertyChangedEventArgs args) {
                if (_disposed || args.PropertyName != nameof(MaterialValue3D.X) &&
                        args.PropertyName != nameof(MaterialValue3D.Y) &&
                        args.PropertyName != nameof(MaterialValue3D.Z)) return;

                UpdateIfChanged();
                Changed?.Invoke(this, EventArgs.Empty);

                if (_renderer is IKn5ObjectRenderer kn5Renderer) {
                    var v = (MaterialValue3D)sender;
                    kn5Renderer.UpdateMaterialPropertyC(_kn5, _materialId, v.Id, new Vec3(v.X.As<float>(), v.Y.As<float>(), v.Z.As<float>()));
                }
            }

            public void ApplyValues() {
                if (_disposed) return;
                ValuesSingle.ForEach(x => x.Apply());
                Values3D.ForEach(x => x.Apply());
                IsChanged = false;
            }

            public void OnLoaded() { }

            public async Task UpdateKn5AndClose(bool updateModel) {
                await _kn5.UpdateKn5(updateModel ? _renderer : null, _activeSkin);
                Close?.Invoke();
            }

            private AsyncCommand _renameCommand;

            public AsyncCommand RenameCommand => _renameCommand ?? (_renameCommand = new AsyncCommand(async () => {
                if (Material == null) return;

                var newName = (await Prompt.ShowAsync("New material name:", "Rename material", Material.Name, "?", required: true, maxLength: 120))?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try {
                    if (_kn5.Materials.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t rename material", "Name’s already taken.");
                    }

                    var oldName = Material.Name;
                    _kn5.Materials.Remove(oldName);
                    _kn5.Materials[newName] = Material;
                    Material.Name = newName;
                    await UpdateKn5AndClose(true);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t rename material", e);
                }
            }, () => Material != null && _kn5.IsEditable));

            private AsyncCommand _forkCommand;

            public AsyncCommand ForkCommand => _forkCommand ?? (_forkCommand = new AsyncCommand(async () => {
                var material = Material;
                if (material == null) return;

                var selectedObject = (_renderer as ToolsKn5ObjectRenderer)?.SelectedObject;
                if (selectedObject == null) return;

                var newName = (await Prompt.ShowAsync("New material name:", "Fork material", material.Name, "?", required: true, maxLength: 120))?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try {
                    if (_kn5.Materials.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t fork material", "Name’s already taken.");
                    }

                    using (WaitingDialog.Create("Forking…")) {
                        var newMaterialId = (uint)_kn5.Materials.Count;
                        var newMaterial = material.Clone();
                        newMaterial.Name = newName;
                        _kn5.Materials[newName] = newMaterial;
                        selectedObject.OriginalNode.MaterialId = newMaterialId;
                        await UpdateKn5AndClose(true);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t fork material", e);
                }
            }, () => Material != null && _kn5.IsEditable));

            private AsyncCommand _changeMaterialCommand;

            public AsyncCommand ChangeMaterialCommand => _changeMaterialCommand ?? (_changeMaterialCommand = new AsyncCommand(async () => {
                var material = Material;
                if (material == null) return;

                var selectedObject = (_renderer as ToolsKn5ObjectRenderer)?.SelectedObject;
                if (selectedObject == null) return;

                var newName = (await Prompt.ShowAsync("Select material:", "Change material", material.Name, "?", required: true, maxLength: 120,
                        suggestions: _kn5.Materials.Keys.OrderBy(x => x), suggestionsFixed: true))?.Trim();
                if (string.IsNullOrEmpty(newName) || newName == material.Name) return;

                try {
                    var index = _kn5.Materials.Keys.IndexOf(newName);
                    if (index == -1) {
                        throw new InformativeException("Can’t change material", "Material with that name not found.");
                    }

                    using (WaitingDialog.Create("Changing…")) {
                        selectedObject.OriginalNode.MaterialId = (uint)index;

                        var usedElsewhere = _kn5.Nodes.Any(n => n.MaterialId == _materialId);
                        if (!usedElsewhere) {
                            _kn5.Materials.Remove(material.Name);
                        }

                        await UpdateKn5AndClose(true);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t change material", e);
                }
            }, () => Material != null && _kn5.IsEditable));

            public bool CanBeSaved => Material != null && _kn5.IsEditable;

            public PresetsCategory PresetableCategory { get; } = new PresetsCategory(
                    Path.Combine(AcPaths.GetDocumentsDirectory(), "Editor", "Materials library"), ".material");

            string IUserPresetable.PresetableKey => PresetableKey;

            // It’s not the best format, but it’s compatible with patched KsEditor
            public string ExportToPresetData() {
                return new JObject {
                    ["ShaderName"] = _material.ShaderName,
                    ["BlendMode"] = (int)AlphaMode,
                    ["DepthMode"] = (int)DepthMode,
                    ["Properties"] = new JArray(_material.ShaderProperties.Select(x => new JObject {
                        ["PropertyName"] = x.Name,
                        ["A"] = x.ValueA,
                        ["B"] = new JObject { ["X"] = x.ValueB.X, ["Y"] = x.ValueB.Y },
                        ["C"] = new JObject { ["X"] = x.ValueC.X, ["Y"] = x.ValueC.Y, ["Z"] = x.ValueC.Y },
                        ["D"] = new JObject { ["X"] = x.ValueD.X, ["Y"] = x.ValueD.Y, ["Z"] = x.ValueD.Z, ["W"] = x.ValueD.W },
                    })),
                    ["Textures"] = new JArray(_material.TextureMappings.Select(x => x.Texture)),
                }.ToString(Formatting.Indented);
            }

            public void ImportFromPresetData(string data) {
                if (_disposed) return;

                try {
                    var j = JObject.Parse(data);
                    var p = (j["Properties"] as JArray)?.OfType<JObject>().ToList();
                    if (p != null) {
                        foreach (var s in ValuesSingle) {
                            var jv = p.FirstOrDefault(x => (string)x["PropertyName"] == s.Id)?["A"];
                            if (jv != null) {
                                s.Value = ((float)jv.As<double>()).ToInvariantString();
                            }
                        }

                        foreach (var s in Values3D) {
                            if (p.FirstOrDefault(x => (string)x["PropertyName"] == s.Id)?["C"] is JArray jv) {
                                s.X = ((float)jv["X"].As<double>()).ToInvariantString();
                                s.Y = ((float)jv["Y"].As<double>()).ToInvariantString();
                                s.Z = ((float)jv["Z"].As<double>()).ToInvariantString();
                            }
                        }
                    }

                    AlphaMode = (MaterialAlphaMode)j["BlendMode"].As<int>();
                    DepthMode = (MaterialDepthMode)j["DepthMode"].As<int>();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            public event EventHandler Changed;

            private bool _disposed;

            public void Dispose() {
                _disposed = true;
            }
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            Model.OnLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
        }
    }
}