using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.KsAnimFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Animations {
    public static class KnAnimExtension {
        public static KsAnimKeyframe ToKeyFrame(this Matrix matrix) {
            matrix.Decompose(out var scale, out var rotation, out var translation);
            return new KsAnimKeyframe(rotation.ToArray(), translation.ToArray(), scale.ToArray());
        }

        [NotNull]
        private static float[][] ConvertFramesV1(Matrix[] matrices, int? fillLength) {
            var v = new float[fillLength ?? matrices.Length][];
            var l = Matrix.Identity.ToArray();

            var i = 0;
            for (; i < matrices.Length; i++) {
                v[i] = l = matrices[i].ToArray();
            }

            for (; i < v.Length; i++) {
                v[i] = l;
            }

            return v;
        }

        [NotNull]
        private static KsAnimKeyframe[] ConvertFramesV2(Matrix[] matrices, int? fillLength) {
            var v = new KsAnimKeyframe[fillLength ?? matrices.Length];
            var l = Matrix.Identity.ToKeyFrame();

            var i = 0;
            for (; i < matrices.Length; i++) {
                v[i] = l = matrices[i].ToKeyFrame();
            }

            for (; i < v.Length; i++) {
                v[i] = l;
            }

            return v;
        }

        public static void SetMatrices(this KsAnimEntryBase animEntry, Matrix[] matrices, int? fillLength = null) {
            var v2 = animEntry as KsAnimEntryV2;
            if (v2 == null) {
                ((KsAnimEntryV1)animEntry).Matrices = ConvertFramesV1(matrices, fillLength);
            } else {
                v2.KeyFrames = ConvertFramesV2(matrices, fillLength);
            }
        }

        [NotNull]
        public static Matrix[] GetMatrices(this KsAnimEntryBase animEntry) {
            var v2 = animEntry as KsAnimEntryV2;
            return v2 != null ? ConvertFrames(v2.KeyFrames) : ConvertFrames(((KsAnimEntryV1)animEntry).Matrices);
        }

        private static Matrix ConvertFrame(KsAnimKeyframe ks) {
            var rotation = ks.Rotation.ToQuaternion();
            var translation = ks.Transition.ToVector3();
            var scale = ks.Scale.ToVector3();
            return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
        }

        [NotNull]
        private static Matrix[] ConvertFrames(KsAnimKeyframe[] ksAnimKeyframes) {
            var result = new Matrix[ksAnimKeyframes.Length];
            if (result.Length == 0) return result;

            var first = default(Matrix);
            var same = true;

            for (var i = 0; i < result.Length; i++) {
                var matrix = ConvertFrame(ksAnimKeyframes[i]);
                result[i] = matrix;

                if (i == 0) {
                    first = matrix;
                } else if (matrix != first) {
                    same = false;
                }
            }

            return same ? new[]{ first } : result;
        }

        [NotNull]
        private static Matrix[] ConvertFrames(float[][] matrices) {
            var result = new Matrix[matrices.Length];
            if (result.Length == 0) return result;

            var first = default(Matrix);
            var same = true;

            for (var i = 0; i < result.Length; i++) {
                var matrix = matrices[i].ToMatrix();
                result[i] = matrix;

                if (i == 0) {
                    first = matrix;
                }else if (matrix != first) {
                    same = false;
                }
            }

            return same ? new[]{ first } : result;
        }
    }

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
            _watcher = DirectoryWatcher.WatchFile(filename, Reload);
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
