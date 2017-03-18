using System.Linq;
using AcTools.KsAnimFile;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Animations {
    public class KsAnimAnimator {
        private class Wrapper {
            private readonly Kn5RenderableList _object;

            [CanBeNull]
            private readonly Matrix[] _frames;

            public Wrapper(RenderableList parent, KsAnimEntryBase entry) {
                _object = parent.GetDummyByName(entry.NodeName);

                var v2 = entry as KsAnimEntryV2;
                _frames = v2 != null ? ConvertFrames(v2.KeyFrames) : ConvertFrames(((KsAnimEntryV1)entry).Matrices);
            }

            private static Matrix ConvertFrame(KsAnimKeyframe ks) {
                var rotation = ks.Rotation.ToQuaternion();
                var translation = ks.Transition.ToVector3();
                var scale = ks.Scale.ToVector3();
                return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
            }

            private static Matrix[] ConvertFrames(KsAnimKeyframe[] ksAnimKeyframes) {
                var result = new Matrix[ksAnimKeyframes.Length];
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

                return same ? null : result;
            }

            private static Matrix[] ConvertFrames(float[][] matrices) {
                var result = new Matrix[matrices.Length];
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

                return same ? null : result;
            }

            public void Set(float position) {
                if (_object == null || _frames == null) return;

                var framesPosition = position * _frames.Length;
                var frameA = ((int)framesPosition).Clamp(0, _frames.Length - 1);
                var frameB = ((int)(framesPosition + 0.9999f)).Clamp(0, _frames.Length - 1);

                if (frameA == frameB) {
                    _object.LocalMatrix = _frames[frameA];
                } else {
                    var k = (framesPosition - frameA).Saturate();
                    _object.LocalMatrix = _frames[frameA] * (1f - k) + _frames[frameB] * k;
                }
            }
        }

        private readonly KsAnim _original;
        private RenderableList _parent;
        private Wrapper[] _wrappers;

        private float _currentPosition;
        private float _targetPosition;
        private readonly float _duration;

        public float Position => _currentPosition;

        public bool ClampPosition { get; set; } = true;

        public KsAnimAnimator(string filename, float duration) {
            _duration = duration;
            _original = KsAnim.FromFile(filename);
        }

        public void Initialize(RenderableList parent) {
            _wrappers = _original.Entries.Values.Select(x => new Wrapper(parent, x)).ToArray();
        }

        public void SetTarget(RenderableList parent, float position) {
            if (_parent != parent) {
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

        public void SetImmediate(RenderableList parent, float position) {
            if (_parent != parent) {
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

        private void Set(float position) {
            if (!ClampPosition) {
                if (position > 1f) {
                    position = position % 1f;
                } else if (position < 0f) {
                    position = (position % 1f + 1f) % 1f;
                }
            }

            for (var i = 0; i < _wrappers.Length; i++) {
                _wrappers[i].Set(position);
            }
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
                Set(_currentPosition);
                return true;
            }

            if (_duration > 0 && _targetPosition != _currentPosition) {
                if (float.IsPositiveInfinity(dt) || Equals(dt, float.MaxValue)) {
                    _currentPosition = _targetPosition;
                    Set(_currentPosition);
                    return true;
                }

                var delta = dt / _duration;
                var left = (_targetPosition - _currentPosition).Abs();
                if (left < delta) {
                    _currentPosition = _targetPosition;
                    Set(_currentPosition);
                } else {
                    _currentPosition += _targetPosition < _currentPosition ? -delta : delta;
                    Set(_currentPosition);
                }

                return true;
            }

            return false;
        }
    }
}
