using System;
using System.Threading.Tasks;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        [CanBeNull]
        private IAcCarSound _sound;

        [CanBeNull]
        private IAcCarSound Sound => _isSoundActive ? _sound : null;

        private bool _isSoundAvailable;

        public bool IsSoundAvailable {
            get => _isSoundAvailable;
            set {
                if (Equals(value, _isSoundAvailable)) return;
                _isSoundAvailable = value;
                OnPropertyChanged();
            }
        }

        private bool _isSoundActive = true;

        public bool IsSoundActive {
            get => IsSoundAvailable && _isSoundActive;
            set {
                if (Equals(value, _isSoundActive)) return;
                _isSoundActive = value;
                OnPropertyChanged();
                ResetSoundEmitters();

                if (_sound != null && !value) {
                    _sound.Horn(false);
                    _sound.Turbo(null);
                    _sound.Engine(null, _soundRpm.Value, _soundThrottle.Value);
                    _sound.Limiter(null);
                }
            }
        }

        private bool _soundEngineActive;

        public bool SoundEngineActive {
            get => _soundEngineActive;
            set {
                if (Equals(value, _soundEngineActive)) return;
                _soundEngineActive = value;
                OnPropertyChanged();
            }
        }

        private bool _soundEngineExternal = true;

        public bool SoundEngineExternal {
            get => _soundEngineExternal;
            set {
                if (Equals(value, _soundEngineExternal)) return;
                _soundEngineExternal = value;
                OnPropertyChanged();
            }
        }

        private float _soundMinimumRpm;

        public float SoundMinimumRpm {
            get => _soundMinimumRpm;
            set {
                if (Equals(value, _soundMinimumRpm)) return;
                _soundMinimumRpm = value;
                OnPropertyChanged();
            }
        }

        private float _soundMaximumRpm;

        public float SoundMaximumRpm {
            get => _soundMaximumRpm;
            set {
                if (Equals(value, _soundMaximumRpm)) return;
                _soundMaximumRpm = value;
                OnPropertyChanged();
            }
        }

        private readonly SmoothValue _soundRpm = new SmoothValue();

        public float SoundRpm {
            get => _soundRpm.Target;
            set {
                if (Equals(value, _soundRpm.Target)) return;
                _soundRpm.Target = value;
                OnPropertyChanged();
            }
        }

        private readonly SmoothValue _soundThrottle = new SmoothValue();

        public float SoundThrottle {
            get => _soundThrottle.Target;
            set {
                if (Equals(value, _soundThrottle.Target)) return;
                _soundThrottle.Target = value;
                OnPropertyChanged();
            }
        }

        private readonly SmoothValue _soundTurbo = new SmoothValue();

        public float SoundTurbo {
            get => _soundTurbo.Target;
            set {
                if (Equals(value, _soundTurbo.Target)) return;
                _soundTurbo.Target = value;
                OnPropertyChanged();
            }
        }

        private bool _soundHorn;

        public bool SoundHorn {
            get => _soundHorn;
            set {
                if (Equals(value, _soundHorn)) return;
                _soundHorn = value;
                OnPropertyChanged();
                Sound?.Horn(IsSoundActive && value);
            }
        }

        private async Task InitializeSoundAsync([NotNull] IAcCarSoundFactory soundFactory) {
            LoadEngineParams();

            _sound = await soundFactory.CreateAsync(_rootDirectory);
            IsSoundAvailable = _sound != null;
        }

        private void LoadEngineParams() {
            SoundMinimumRpm = _carData.GetEngineMinimumRpm();
            SoundMaximumRpm = _carData.GetEngineMaximumRpm();
            SoundRpm = SoundRpm.Clamp(SoundMinimumRpm, SoundMaximumRpm);
        }

        private class SoundEmitter {
            private readonly Action<Vector3, Vector3, Vector3, Vector3> _updateFn;

            private Vector3 _position;
            private Vector3 _direction;
            private Vector3 _up;

            private Matrix _lastMatrix;

            private Vector3 _speedSmooth;
            private Vector3? _lastTransformedPosition;

            public SoundEmitter(Vector3 position, Vector3 direction, Vector3 up, Action<Vector3, Vector3, Vector3, Vector3> updateFn) {
                _position = position;
                _direction = direction;
                _up = up;
                _updateFn = updateFn;
            }

            public void Move(Vector3 position, Vector3 direction, Vector3 up) {
                _position = position;
                _direction = direction;
                _up = up;
                _lastMatrix = default(Matrix);
            }

            public void Update(Matrix matrix, float dt) {
                if (_lastMatrix == matrix) return;
                _lastMatrix = matrix;

                var position = Vector3.TransformCoordinate(_position, matrix);
                var direction = Vector3.TransformNormal(_direction, matrix);
                var up = Vector3.TransformNormal(_up, matrix);

                Vector3 speed;
                if (_lastTransformedPosition != null) {
                    if (dt <= 0f) {
                        speed = Vector3.Zero;
                    } else {
                        var delta = position - _lastTransformedPosition.Value;
                        speed = delta / dt;
                    }
                } else {
                    speed = Vector3.Zero;
                }

                _speedSmooth += (speed - _speedSmooth) * (dt * 10f).Saturate();
                _lastTransformedPosition = position;

                _updateFn?.Invoke(direction, position, up, _speedSmooth);
            }
        }

        private class SmoothValue {
            public float Value;
            public float Target;

            public void Update(float dt) {
                Value += (Target - Value) * (dt * 10f).Saturate();
            }
        }

        private void ResetSoundEmitters() {
            _engineSoundEmitter.Reset();
        }

        private void CreateSoundEmittersAndSmoothers() {
            _carSoundEmitter = Lazier.Create(() => Sound == null ? null :
                new SoundEmitter(Vector3.Zero, Vector3.Zero, Vector3.UnitY, Sound.UpdateCarPosition));

            _hornSoundEmitter = Lazier.Create(() => {
                if (Sound == null) return null;

                var carLength = BoundingBox?.GetSize().Z ?? 3f;
                return new SoundEmitter(Vector3.UnitZ * (carLength * 0.3f), Vector3.UnitZ, Vector3.UnitY, Sound.UpdateHornPosition);
            });

            _engineSoundEmitter = Lazier.Create(() => {
                if (Sound == null) return null;

                var engineOffset = _carData.GetEngineOffset(BoundingBox?.GetSize().Z ?? 3f);
                return new SoundEmitter(engineOffset, Vector3.Normalize(engineOffset), Vector3.UnitY, Sound.UpdateEnginePosition);
            });
        }

        private Lazier<SoundEmitter> _carSoundEmitter;
        private Lazier<SoundEmitter> _hornSoundEmitter;
        private Lazier<SoundEmitter> _engineSoundEmitter;

        // If car is not being re-drawn during a frame, call this method!
        public void UpdateSound(IDeviceContextHolder holder, [CanBeNull] ICamera camera) {
            if (camera == null || Sound == null || !IsSoundActive) return;

            var dt = (float)holder.LastFrameTime;
            _soundRpm.Update(dt);
            _soundThrottle.Update(dt);
            _soundTurbo.Update(dt);

            Sound.Turbo(SoundEngineActive ? _soundTurbo.Value : (float?)null);
            Sound.Engine(SoundEngineActive ? SoundEngineExternal : (bool?)null, _soundRpm.Value, _soundThrottle.Value);
            Sound.Limiter(SoundEngineActive ? ((_soundRpm.Value - (SoundMaximumRpm - 45f)) / 50f).Saturate() : (float?)null);

            var matrix = Matrix * Matrix.LookAtRH(camera.Position, camera.Position + camera.Look, camera.Up);
            _carSoundEmitter.Value?.Update(matrix, dt);
            _hornSoundEmitter.Value?.Update(matrix, dt);
            _engineSoundEmitter.Value?.Update(matrix, dt);
        }
    }
}