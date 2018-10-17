using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.KsAnimFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Animations {
    public class KsAnimAnimator : IDisposable {
        private class Wrapper {
            private readonly bool _skipFixed;
            private readonly Kn5RenderableList _object;

            [NotNull]
            private readonly Matrix[] _frames;

            public Wrapper(RenderableList parent, KsAnimEntryBase entry, bool skipFixed) {
                _skipFixed = skipFixed;
                _object = parent.GetDummyByName(entry.NodeName);
                _frames = entry.GetMatrices();
            }

            public void Set(float? position) {
                if (_object == null || _frames.Length == 0) return;

                if (_frames.Length == 1) {
                    if (!_skipFixed) {
                        _object.LocalMatrix = _frames[0];
                    }
                    return;
                }

                if (position.HasValue) {
                    var framesPosition = position.Value * _frames.Length;
                    var frameA = ((int)framesPosition).Clamp(0, _frames.Length - 1);
                    var frameB = ((int)(framesPosition + 0.9999f)).Clamp(0, _frames.Length - 1);

                    if (frameA == frameB) {
                        _object.LocalMatrix = _frames[frameA];
                    } else {
                        var k = (framesPosition - frameA).Saturate();
                        _object.LocalMatrix = _frames[frameA] * (1f - k) + _frames[frameB] * k;
                    }
                } else {
                    _object.LocalMatrix = _object.OriginalNode.Transform.ToMatrix();
                }
            }
        }

        private string _filename;
        private KsAnim _original;
        private IDisposable _watcher;
        private RenderableList _parent;
        private Wrapper[] _wrappers;

        private float _currentPosition;
        private float _targetPosition;
        private readonly float _duration;
        private readonly bool _skipFixed;
        private WeakReference<IDeviceContextHolder> _holder;

        public float Position => _currentPosition;
        public bool ClampPosition { get; set; } = true;
        public readonly List<KsAnimAnimator> Linked = new List<KsAnimAnimator>();

        public event EventHandler Reset;

        public KsAnimAnimator(string filename, float duration, bool skipFixed) {
            _duration = duration;
            _skipFixed = skipFixed;
            _filename = filename;
            _original = File.Exists(_filename) ? KsAnim.FromFile(_filename) : KsAnim.CreateEmpty();
            _watcher = SimpleDirectoryWatcher.WatchFile(filename, Reload);
        }

        private void Reload() {
            _original = File.Exists(_filename) ? KsAnim.FromFile(_filename) : KsAnim.CreateEmpty();

            if (_wrappers != null) {
                for (var i = 0; i < _wrappers.Length; i++) {
                    _wrappers[i].Set(null);
                }

                Reset?.Invoke(this, EventArgs.Empty);
            }

            if (_parent != null) {
                Initialize(_parent);
                Set(_currentPosition);
            }

            if (_holder != null && _holder.TryGetTarget(out var holder)) {
                holder.RaiseSceneUpdated();
            }
        }

        private void Initialize(RenderableList parent) {
            _wrappers = _original.Entries.Values.Select(x => new Wrapper(parent, x, _skipFixed)).ToArray();
        }

        public void SetParent(RenderableList parent, [CanBeNull] IDeviceContextHolder holder) {
            if (_parent != parent) {
                _holder = holder == null ? null : new WeakReference<IDeviceContextHolder>(holder);
                _parent = parent;
                Initialize(parent);
            }
        }

        // Holder only used for FS watching and update
        public void SetTarget(RenderableList parent, float position, [CanBeNull] IDeviceContextHolder holder) {
            if (_parent != parent) {
                _holder = holder == null ? null : new WeakReference<IDeviceContextHolder>(holder);
                _parent = parent;
                Initialize(parent);

                if (Equals(position, _currentPosition)) {
                    Set(position);
                    return;
                }
            }

            _loopsRemain = 0;

            if (_duration <= 0f) {
                Set(position);
            } else {
                _targetPosition = position;
            }
        }

        public void SetImmediate(RenderableList parent, float position, [CanBeNull] IDeviceContextHolder holder) {
            if (_parent != parent) {
                _holder = holder == null ? null : new WeakReference<IDeviceContextHolder>(holder);
                _parent = parent;
                Initialize(parent);

                if (Equals(position, _currentPosition)) {
                    Set(position);
                    return;
                }
            }

            if (!Equals(position, _currentPosition)) {
                Set(position);
                _currentPosition = position;
            }
        }

        private int _loopsRemain;

        public void Loop(RenderableList parent, int limit = -1) {
            if (_parent != parent) {
                _parent = parent;
                Initialize(parent);

                if (_loopsRemain != 0) {
                    Set(_currentPosition);
                    return;
                }
            }

            _loopsRemain = limit;
        }

        public bool PingPongMode;

        private bool Set(float position) {
            if (_wrappers == null) return false;

            if (!ClampPosition) {
                if (position > 1f) {
                    position = position % 1f;
                } else if (position < 0f) {
                    position = (position % 1f + 1f) % 1f;
                }
            }

            var wrappersPosition = PingPongMode ?
                    position > 0.5f ? 2f - position * 2f : position * 2f :
                    position;
            var any = false;

            for (var i = 0; i < _wrappers.Length; i++) {
                _wrappers[i].Set(wrappersPosition);
                any = true;
            }

            for (var i = 0; i < Linked.Count; i++) {
                var animator = Linked[i];
                animator.Set(position);
                any = true;
            }

            return any;
        }

        public void Update() {
            Set(_currentPosition);
        }

        public bool OnTick(float dt) {
            if (_loopsRemain != 0) {
                if (float.IsPositiveInfinity(dt) || Equals(dt, float.MaxValue)) {
                    _currentPosition = 0f;
                } else {
                    var delta = dt / _duration;
                    _currentPosition += delta;
                }

                if (_currentPosition > 1f) {
                    _currentPosition = 0f;
                    if (_loopsRemain > 0) {
                        _loopsRemain--;
                    }
                }

                _targetPosition = _currentPosition;
                return Set(_currentPosition);
            }

            if (_duration > 0 && _targetPosition != _currentPosition) {
                if (float.IsPositiveInfinity(dt) || Equals(dt, float.MaxValue)) {
                    _currentPosition = _targetPosition;
                    return Set(_currentPosition);
                }

                var delta = dt / _duration;
                var left = (_targetPosition - _currentPosition).Abs();
                _currentPosition = left < delta ? _targetPosition :
                        _currentPosition + (_targetPosition < _currentPosition ? -delta : delta);
                return Set(_currentPosition);
            }

            return false;
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _watcher);
        }
    }
}
