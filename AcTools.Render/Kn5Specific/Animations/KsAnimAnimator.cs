using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcTools.KsAnimFile;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Animations {
    public class KsAnimAnimator {
        private class Wrapper {
            private readonly RenderableList _object;
            private readonly Matrix[] _frames;

            public Wrapper(RenderableList parent, KsAnimEntry entry) {
                _object = parent.GetDummyByName(entry.NodeName);
                _frames = ConvertFrames(entry.KeyFrames);
            }

            private static Matrix ConvertFrame(KsAnimKeyframe ks) {
                var rotation = ks.Rotation.ToQuaternion();
                var translation = ks.Transformation.ToVector3();
                var scale = ks.Scale.ToVector3();

                translation.X *= -1;

                var axis = rotation.Axis;
                var angle = rotation.Angle;

                if (angle.Abs() < 0.0001f) {
                    return Matrix.Scaling(scale) * Matrix.Translation(translation);
                }

                axis.Y *= -1;
                axis.Z *= -1;
                rotation = Quaternion.RotationAxis(axis, angle);

                return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
            }

            private static Matrix[] ConvertFrames(KsAnimKeyframe[] ksAnimKeyframes) {
                var result = new Matrix[ksAnimKeyframes.Length];

                for (var i = 0; i < result.Length; i++) {
                    result[i] = ConvertFrame(ksAnimKeyframes[i]);
                }

                return result;
            }

            public void Set(float position) {
                if (_object == null) return;

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

        public KsAnimAnimator(string filename, float duration) {
            _duration = duration;
            _original = KsAnim.FromFile(filename);
        }

        public void Initialize(RenderableList parent) {
            _wrappers = _original.Entries.Values.Select(x => new Wrapper(parent, x)).ToArray();
        }

        public void Update(RenderableList parent, float position) {
            if (_parent != parent) {
                _parent = parent;
                Initialize(parent);
            }

            if (_duration <= 0f) {
                Set(position);
            } else {
                _targetPosition = position;
            }
        }

        private void Set(float position) {
            for (var i = 0; i < _wrappers.Length; i++) {
                _wrappers[i].Set(position);
            }
        }

        public bool OnTick(float dt) {
            if (_duration > 0 && _targetPosition != _currentPosition) {
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
