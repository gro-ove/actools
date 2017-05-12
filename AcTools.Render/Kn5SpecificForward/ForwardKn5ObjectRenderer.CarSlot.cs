using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ForwardKn5ObjectRenderer {
        public static int OptionCacheSize = 0;

        private class PreviousCar {
            public string Id;
            public List<IRenderableObject> Objects;
        }

        private readonly List<PreviousCar> _previousCars = new List<PreviousCar>(2);

        public class CarSlot : INotifyPropertyChanged, IDisposable {
            private static int _id;
            public readonly int Id;

            private readonly ForwardKn5ObjectRenderer _renderer;

            private CarDescription _car;
            private bool _selectSkinLater;
            private string _selectSkin = Kn5RenderableCar.DefaultSkin;

            private CarDescription _loadingCar;

            private List<PreviousCar> PreviousCars => _renderer._previousCars;

            internal CarSlot(ForwardKn5ObjectRenderer renderer, CarDescription car, int? id) {
                Id = id ?? ++_id;

                _renderer = renderer;
                _car = car;
                CarWrapper = new RenderableList();
            }

            public void Initialize() {
                if (_car != null) {
                    var carNode = new Kn5RenderableCar(_car, Matrix.Identity, _selectSkinLater ? _selectSkin : Kn5RenderableCar.DefaultSkin,
                            asyncTexturesLoading: _renderer.AsyncTexturesLoading,
                            asyncOverrideTexturesLoading: _renderer.AsyncOverridesLoading,
                            allowSkinnedObjects: _renderer.AllowSkinnedObjects);
                    CarNode = carNode;
                    _renderer.CopyValues(carNode, null);

                    _selectSkinLater = false;
                    CarWrapper.Add(carNode);
                    _carBoundingBox = null;

                    _renderer.ExtendCar(this, CarNode, CarWrapper);
                }
            }

            // used, so reflections or auto-aligned camera won’t move while, for example, one door is opened
            private BoundingBox? _carBoundingBox;

            public void ResetCarBoundingBox() {
                _carBoundingBox = null;
            }

            public BoundingBox? GetCarBoundingBox() {
                return _carBoundingBox ?? (_carBoundingBox = CarNode?.BoundingBox);
            }

            public void SelectPreviousSkin() {
                CarNode?.SelectPreviousSkin(_renderer.DeviceContextHolder);
            }

            public void SelectNextSkin() {
                CarNode?.SelectNextSkin(_renderer.DeviceContextHolder);
            }

            public void SelectSkin(string skinId) {
                if (CarNode == null) {
                    _selectSkinLater = true;
                    _selectSkin = skinId;
                } else {
                    CarNode?.SelectSkin(_renderer.DeviceContextHolder, skinId);
                }
            }

            private Matrix? _setLocalMatrixLater;

            public Matrix? LocalMatrix {
                get { return CarNode?.LocalMatrix ?? _setLocalMatrixLater; }
                set {
                    if (!value.HasValue) {
                        _setLocalMatrixLater = null;
                        return;
                    }

                    if (CarNode != null) {
                        CarNode.LocalMatrix = value.Value;
                    } else {
                        _setLocalMatrixLater = value;
                    }
                }
            }

            private Kn5RenderableCar _carNode;

            [CanBeNull]
            public Kn5RenderableCar CarNode {
                get { return _carNode; }
                private set {
                    if (Equals(value, _carNode)) return;

                    if (_carNode != null) {
                        _carNode.ObjectsChanged += OnCarNodeObjectsChanged;
                    }

                    if (_setLocalMatrixLater != null && value != null) {
                        value.LocalMatrix = _setLocalMatrixLater.Value;
                    }

                    _carNode = value;
                    _renderer.IsDirty = true;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CarCenter));
                    OnPropertyChanged(nameof(Kn5));
                    OnPropertyChanged(nameof(SelectedLod));
                    OnPropertyChanged(nameof(LodsCount));

                    ResetCarBoundingBox();

                    if (value != null) {
                        value.ObjectsChanged += OnCarNodeObjectsChanged;
                    }
                }
            }

            [CanBeNull]
            public Kn5 Kn5 => CarNode?.OriginalFile;

            public Vector3 CarCenter {
                get {
                    var result = CarNode?.BoundingBox?.GetCenter() ?? Vector3.Zero;
                    result.Y = 0f;
                    return result;
                }
            }

            private void OnCarNodeObjectsChanged(object sender, EventArgs e) {
                _renderer.OnCarObjectsChanged();
            }

            private int? _selectLod;

            public int SelectedLod => _selectLod ?? (CarNode?.CurrentLod ?? -1);

            public int LodsCount => CarNode?.LodsCount ?? 0;

            public void SelectPreviousLod() {
                if (CarNode == null) return;
                SelectLod((CarNode.CurrentLod + CarNode.LodsCount - 1) % CarNode.LodsCount);
            }

            public void SelectNextLod() {
                if (CarNode == null) return;
                SelectLod((CarNode.CurrentLod + 1) % CarNode.LodsCount);
            }

            public void SelectLod(int lod) {
                if (CarNode == null) {
                    _selectLod = lod;
                    return;
                }

                CarNode.CurrentLod = lod;
            }

            public readonly RenderableList CarWrapper;

            private void ClearExisting() {
                if (_car != null && OptionCacheSize > 0) {
                    var existing = PreviousCars.FirstOrDefault(x => x.Id == _car.MainKn5File);
                    if (existing != null) {
                        PreviousCars.Remove(existing);
                        PreviousCars.Add(existing);
                    } else if (CarWrapper.OfType<Kn5RenderableCar>().Any()) {
                        if (PreviousCars.Count >= OptionCacheSize) {
                            var toRemoval = PreviousCars[0];
                            toRemoval.Objects.DisposeEverything();
                            PreviousCars.RemoveAt(0);
                        }

                        PreviousCars.Add(new PreviousCar {
                            Id = _car.MainKn5File,
                            Objects = CarWrapper.ToList()
                        });

                        CarWrapper.Clear();
                        return;
                    }
                }

                CarWrapper.DisposeEverything();
                GCHelper.CleanUp();
            }

            public void SetCar(CarDescription car, string skinId = Kn5RenderableCar.DefaultSkin) {
                _renderer.ClearBeforeChangingCar();

                try {
                    _loadingCar = car;

                    if (CarWrapper == null) {
                        _car = car;
                        return;
                    }

                    if (car == null) {
                        ClearExisting();
                        CarNode = null;
                        _car = null;
                        _renderer.Scene.UpdateBoundingBox();
                        return;
                    }

                    Kn5RenderableCar loaded;

                    var previous = PreviousCars.FirstOrDefault(x => x.Id == car.MainKn5File);
                    if (previous != null) {
                        PreviousCars.Remove(previous);

                        ClearExisting();
                        CarWrapper.AddRange(previous.Objects);
                        _car = car;
                        loaded = previous.Objects.OfType<Kn5RenderableCar>().First();
                        _renderer.CopyValues(loaded, CarNode);
                        CarNode = loaded;

                        if (_selectSkinLater) {
                            CarNode.SelectSkin(_renderer.DeviceContextHolder, _selectSkin);
                            _selectSkinLater = false;
                        } else {
                            CarNode.SelectSkin(_renderer.DeviceContextHolder, skinId);
                        }

                        _renderer.Scene.UpdateBoundingBox();
                        return;
                    }

                    loaded = new Kn5RenderableCar(car, Matrix.Identity, _selectSkinLater ? _selectSkin : skinId,
                            asyncTexturesLoading: _renderer.AsyncTexturesLoading,
                            asyncOverrideTexturesLoading: _renderer.AsyncOverridesLoading,
                            allowSkinnedObjects: _renderer.AllowSkinnedObjects);
                    _selectSkinLater = false;
                    _renderer.CopyValues(loaded, CarNode);

                    ClearExisting();

                    CarWrapper.Add(loaded);
                    _renderer.ExtendCar(this, loaded, CarWrapper);

                    _car = car;
                    _selectSkin = null;
                    CarNode = loaded;

                    _renderer.IsDirty = true;
                    _renderer.Scene.UpdateBoundingBox();
                } catch (Exception e) {
                    MessageBox.Show(e.ToString());
                    throw;
                } finally {
                    if (ReferenceEquals(_loadingCar, car)) {
                        _loadingCar = null;
                    }
                }
            }

            public async Task SetCarAsync(CarDescription car, string skinId = Kn5RenderableCar.DefaultSkin,
                    CancellationToken cancellationToken = default(CancellationToken)) {
                _renderer.ClearBeforeChangingCar();

                try {
                    _loadingCar = car;

                    if (CarWrapper == null) {
                        _car = car;
                        return;
                    }

                    if (car == null) {
                        ClearExisting();
                        CarNode = null;
                        _car = null;
                        _renderer.Scene.UpdateBoundingBox();
                        return;
                    }

                    Kn5RenderableCar loaded = null;

                    var previous = PreviousCars.FirstOrDefault(x => x.Id == car.MainKn5File);
                    if (previous != null) {
                        PreviousCars.Remove(previous);

                        ClearExisting();
                        CarWrapper.AddRange(previous.Objects);
                        _car = car;
                        loaded = previous.Objects.OfType<Kn5RenderableCar>().First();
                        _renderer.CopyValues(loaded, CarNode);
                        CarNode = loaded;

                        if (_selectSkinLater) {
                            CarNode.SelectSkin(_renderer.DeviceContextHolder, _selectSkin);
                            _selectSkinLater = false;
                        } else {
                            CarNode.SelectSkin(_renderer.DeviceContextHolder, skinId);
                        }

                        _renderer.Scene.UpdateBoundingBox();
                        return;
                    }

                    await car.LoadAsync();
                    if (cancellationToken.IsCancellationRequested) return;

                    await Task.Run(() => {
                        loaded = new Kn5RenderableCar(car, Matrix.Identity, _selectSkinLater ? _selectSkin : skinId,
                                asyncTexturesLoading: _renderer.AsyncTexturesLoading,
                                asyncOverrideTexturesLoading: _renderer.AsyncOverridesLoading,
                                allowSkinnedObjects: _renderer.AllowSkinnedObjects);
                        _selectSkinLater = false;
                        if (cancellationToken.IsCancellationRequested) return;

                        _renderer.CopyValues(loaded, CarNode);
                        if (cancellationToken.IsCancellationRequested) return;

                        loaded.Draw(_renderer.DeviceContextHolder, null, SpecialRenderMode.InitializeOnly);
                    });

                    if (cancellationToken.IsCancellationRequested || _loadingCar != car) {
                        loaded?.Dispose();
                        return;
                    }

                    ClearExisting();

                    CarWrapper.Add(loaded);
                    _renderer.ExtendCar(this, loaded, CarWrapper);

                    _car = car;
                    _selectSkin = null;
                    CarNode = loaded;

                    _renderer.IsDirty = true;
                    _renderer.Scene.UpdateBoundingBox();
                } catch (Exception e) {
                    MessageBox.Show(e.ToString());
                    throw;
                } finally {
                    if (ReferenceEquals(_loadingCar, car)) {
                        _loadingCar = null;
                    }
                }
            }

            public bool MoveObject(Vector2 relativeFrom, Vector2 relativeDelta, BaseCamera camera, bool tryToClone) {
                IMoveable c;
                if (CarNode?.Movable.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out c) == true) {
                    ResetCarBoundingBox();
                    return true;
                }

                return false;
            }

            public void StopMovement() {
                CarNode?.Movable.StopMovement();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public void Dispose() {
                CarWrapper?.Dispose();
            }
        }

        protected virtual void CopyValues([NotNull] Kn5RenderableCar newCar, [CanBeNull] Kn5RenderableCar oldCar) {
            newCar.HeadlightsEnabled = oldCar?.HeadlightsEnabled ?? CarLightsEnabled;
            newCar.BrakeLightsEnabled = oldCar?.BrakeLightsEnabled ?? CarBrakeLightsEnabled;
            newCar.LeftDoorOpen = oldCar?.LeftDoorOpen ?? false;
            newCar.RightDoorOpen = oldCar?.RightDoorOpen ?? false;
            newCar.SteerDeg = oldCar?.SteerDeg ?? 0f;

            if (oldCar != null) {
                oldCar.CamerasChanged -= OnCamerasChanged;
                oldCar.ExtraCamerasChanged -= OnExtraCamerasChanged;
            }

            newCar.CamerasChanged += OnCamerasChanged;
            newCar.ExtraCamerasChanged += OnExtraCamerasChanged;
        }

        protected virtual void ClearBeforeChangingCar() { }

        protected virtual void OnCarObjectsChanged() {
            Scene.UpdateBoundingBox();
            IsDirty = true;
        }

        private bool _carLightsEnabled;

        public bool CarLightsEnabled {
            get { return MainSlot.CarNode?.HeadlightsEnabled ?? _carLightsEnabled; }
            set {
                _carLightsEnabled = value;
                foreach (var carSlot in CarSlots) {
                    if (carSlot.CarNode != null) {
                        carSlot.CarNode.HeadlightsEnabled = value;
                    }
                }
            }
        }

        private bool _carBrakeLightsEnabled;

        public bool CarBrakeLightsEnabled {
            get { return MainSlot.CarNode?.BrakeLightsEnabled ?? _carBrakeLightsEnabled; }
            set {
                _carBrakeLightsEnabled = value;
                foreach (var carSlot in CarSlots) {
                    if (carSlot.CarNode != null) {
                        carSlot.CarNode.BrakeLightsEnabled = value;
                    }
                }
            }
        }

        public void SelectPreviousSkin() {
            MainSlot.SelectPreviousSkin();
        }

        public void SelectNextSkin() {
            MainSlot.SelectNextSkin();
        }

        public void SelectSkin(string skinId) {
            MainSlot.SelectSkin(skinId);
        }
    }
}
