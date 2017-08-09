using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Kn5Specific.Animations;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        [CanBeNull]
        private CarLight[] _carLights;

        private KsAnimAnimator[] _carLightsAnimators;

        protected IEnumerable<T> LoadLights<T>() where T : CarLight, new() {
            return _carData.GetLights().Select(x => {
                var light = new T();
                light.Initialize(x, RootObject);
                return light;
            });
        }

        public IEnumerable<CarLight> GetCarLights() {
            var carLights = _carLights;
            if (carLights == null) {
                carLights = LoadLights().ToArray();
                _carLights = carLights;
            }

            return _carLights;
        }

        protected virtual IEnumerable<CarLight> LoadLights() {
            return LoadLights<CarLight>();
        }

        [ItemNotNull]
        private IEnumerable<KsAnimAnimator> LoadLightsAnimators() {
            return _carData.GetLightsAnimations()
                           .Select(x => CreateAnimator(_rootDirectory, x))
                           .NonNull();
        }

        private void ResetLights() {
            if (_carLights != null) {
                foreach (var carLight in _carLights) {
                    carLight.IsHeadlightEnabled = false;
                    carLight.IsBrakeEnabled = false;
                }
            }

            if (_carLightsAnimators != null) {
                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    _carLightsAnimators[i].SetTarget(RootObject, _headlightsEnabled ? 1f : 0f, _skinsWatcherHolder);
                    _carLightsAnimators[i].OnTick(float.PositiveInfinity);
                }
            }

            _carLightsAnimators = null;
            ReenableLights();
        }

        private void ReenableLights() {
            if (_carLights == null) return;

            _carLights = LoadLights().ToArray();

            for (var i = 0; i < _carLights.Length; i++) {
                _carLights[i].IsHeadlightEnabled = _headlightsEnabled;
                _carLights[i].IsBrakeEnabled = _brakeLightsEnabled;
            }

            if (_carLightsAnimators != null) {
                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    _carLightsAnimators[i].SetTarget(RootObject, _headlightsEnabled ? 1f : 0f, _skinsWatcherHolder);
                    _carLightsAnimators[i].OnTick(float.PositiveInfinity);
                }
            }
        }

        private bool _headlightsEnabled;

        public bool HeadlightsEnabled {
            get => _headlightsEnabled;
            set {
                if (Equals(value, _headlightsEnabled)) return;

                var carLights = _carLights;
                if (carLights == null) {
                    carLights = LoadLights().ToArray();
                    _carLights = carLights;
                }

                if (_carLightsAnimators == null) {
                    _carLightsAnimators = LoadLightsAnimators().ToArray();
                }

                _headlightsEnabled = value;

                for (var i = 0; i < carLights.Length; i++) {
                    carLights[i].IsHeadlightEnabled = value;
                }

                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    _carLightsAnimators[i].SetTarget(RootObject, value ? 1f : 0f, _skinsWatcherHolder);
                }

                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();
            }
        }

        public TimeSpan? GetApproximateHeadlightsDelay() {
            var carLights = _carLights;
            if (carLights == null) {
                carLights = LoadLights().ToArray();
                _carLights = carLights;
            }

            var total = TimeSpan.Zero;
            var count = 0;

            for (var i = 0; i < carLights.Length; i++) {
                var l = carLights[i];
                if (l.Description?.HeadlightColor == null) continue;

                var v = l.GetDuration();
                if (v.HasValue) {
                    total += v.Value;
                    count++;
                }
            }

            if (count == 0) return null;
            return TimeSpan.FromSeconds(total.TotalSeconds / count);
        }

        public TimeSpan? GetApproximateBrakeLightsDelay() {
            var carLights = _carLights;
            if (carLights == null) {
                carLights = LoadLights().ToArray();
                _carLights = carLights;
            }

            var total = TimeSpan.Zero;
            var count = 0;

            for (var i = 0; i < carLights.Length; i++) {
                var l = carLights[i];
                if (l.Description?.BrakeColor == null) continue;

                var v = l.GetDuration();
                if (v.HasValue) {
                    total += v.Value;
                    count++;
                }
            }

            if (count == 0) return null;
            return TimeSpan.FromSeconds(total.TotalSeconds / count);
        }

        private bool _brakeLightsEnabled;

        public bool BrakeLightsEnabled {
            get => _brakeLightsEnabled;
            set {
                if (Equals(value, _brakeLightsEnabled)) return;

                if (_carLights == null) {
                    _carLights = LoadLights().ToArray();
                }

                _brakeLightsEnabled = value;

                for (var i = 0; i < _carLights.Length; i++) {
                    _carLights[i].IsBrakeEnabled = value;
                }

                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();
            }
        }
    }
}