using System;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public interface IMoveable {
        void Move(Vector3 delta);
        void Rotate(Quaternion delta);
        void Scale(Vector3 scale);

        [CanBeNull]
        IMoveable Clone();
    }

    public interface IMousePositionProvider {
        Vector2 GetRelative();
    }

    [Flags]
    public enum MoveableRotationAxis : uint {
        None = 0,
        X = 1, Y = 2, Z = 4,
        All = 7
    }

    public class MoveableHelper : IRenderableObject {
        private DebugLinesObject _arrowX, _arrowY, _arrowZ;

        [CanBeNull]
        private DebugLinesObject _circleX, _circleY, _circleZ, _scale;

        private Vector3 _arrowHighlighted, _circleHighlighted;
        private bool _scaleHighlighted, _keepHighlight;

        private readonly IMoveable _parent;
        private readonly MoveableRotationAxis _rotationAxis;
        private readonly bool _allowScaling;

        public MoveableHelper(IMoveable parent, MoveableRotationAxis rotationAxis = MoveableRotationAxis.Y, bool allowScaling = false) {
            _parent = parent;
            _rotationAxis = rotationAxis;
            _allowScaling = allowScaling;
        }

        public bool MoveObject(Vector2 relativeFrom, Vector2 relativeDelta, CameraBase camera, bool tryToClone, [CanBeNull] out IMoveable cloned) {
            if (_keepHighlight) {
                tryToClone = false;
            } else {
                _keepHighlight = true;
            }

            cloned = null;

            if (_arrowHighlighted != default(Vector3)) {
                var plane = new Plane(ParentMatrix.GetTranslationVector(), -camera.Look);
                var rayFrom = camera.GetPickingRay(relativeFrom, new Vector2(1f, 1f));
                var rayTo = camera.GetPickingRay(relativeFrom + relativeDelta, new Vector2(1f, 1f));
                if (!Ray.Intersects(rayFrom, plane, out var distanceFrom) ||
                        !Ray.Intersects(rayTo, plane, out var distanceTo)) return false;

                var pointDelta =  rayTo.Direction * distanceTo - rayFrom.Direction * distanceFrom;
                if (tryToClone) {
                    cloned = _parent.Clone();
                }

                var totalDistance = pointDelta.Length();
                var resultMovement = new Vector3(
                        pointDelta.X * _arrowHighlighted.X,
                        pointDelta.Y * _arrowHighlighted.Y,
                        pointDelta.Z * _arrowHighlighted.Z);
                resultMovement.Normalize();
                resultMovement *= totalDistance;

                _parent.Move(resultMovement);
                UpdateBoundingBox();
                return true;
            }

            if (_circleHighlighted != default(Vector3)) {
                var rotationAxis = _circleHighlighted.X * _circleHighlighted.Y * _circleHighlighted.Z != 0f ?
                        camera.Look : _circleHighlighted;

                if (tryToClone) {
                    cloned = _parent.Clone();
                }

                _parent.Rotate(Quaternion.RotationAxis(rotationAxis, relativeDelta.X * 10f));
                UpdateBoundingBox();
                return true;
            }

            if (_scaleHighlighted) {
                var v = relativeDelta.X + relativeDelta.Y;
                _parent.Scale(new Vector3(v > 0f ? 1.01f : 1f / 1.01f));
                UpdateBoundingBox();
                return true;
            }

            return false;
        }

        public void StopMovement() {
            _keepHighlight = false;
        }

        public void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            const float arrowSize = 0.08f;
            const float circleSize = 0.06f;
            const float boxSize = 0.14f;

            if (_arrowX == null) {
                _arrowX = DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitX, new Color4(0f, 1f, 0f, 0f), arrowSize);
                _arrowY = DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitY, new Color4(0f, 0f, 1f, 0f), arrowSize);
                _arrowZ = DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitZ, new Color4(0f, 0f, 0f, 1f), arrowSize);

                if (_rotationAxis.HasFlag(MoveableRotationAxis.X)) {
                    _circleX = DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitX, new Color4(0f, 1f, 0f, 0f), radius: circleSize);
                }

                if (_rotationAxis.HasFlag(MoveableRotationAxis.Y)) {
                    _circleY = DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitY, new Color4(0f, 0f, 1f, 0f), radius: circleSize);
                }

                if (_rotationAxis.HasFlag(MoveableRotationAxis.Z)) {
                    _circleZ = DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitZ, new Color4(0f, 0f, 0f, 1f), radius: circleSize);
                }

                if (_allowScaling) {
                    _scale = DebugLinesObject.GetLinesBox(Matrix.Identity, new Vector3(boxSize), new Color4(0f, 1f, 1f, 0f));
                }
            }

            var matrix = ParentMatrix.GetTranslationVector().ToFixedSizeMatrix(camera);
            if (_arrowX.ParentMatrix != matrix) {
                _arrowX.ParentMatrix = matrix;
                _arrowY.ParentMatrix = matrix;
                _arrowZ.ParentMatrix = matrix;

                _arrowX.UpdateBoundingBox();
                _arrowY.UpdateBoundingBox();
                _arrowZ.UpdateBoundingBox();

                if (_circleX != null) {
                    _circleX.ParentMatrix = matrix;
                    _circleX.UpdateBoundingBox();
                }

                if (_circleY != null) {
                    _circleY.ParentMatrix = matrix;
                    _circleY.UpdateBoundingBox();
                }

                if (_circleZ != null) {
                    _circleZ.ParentMatrix = matrix;
                    _circleZ.UpdateBoundingBox();
                }

                if (_scale != null) {
                    _scale.ParentMatrix = matrix;
                    _scale.UpdateBoundingBox();
                }
            }

            if (_keepHighlight) {
                _arrowX.Draw(holder, camera, _arrowHighlighted.X == 0f ? SpecialRenderMode.Simple : SpecialRenderMode.Outline);
                _arrowY.Draw(holder, camera, _arrowHighlighted.Y == 0f ? SpecialRenderMode.Simple : SpecialRenderMode.Outline);
                _arrowZ.Draw(holder, camera, _arrowHighlighted.Z == 0f ? SpecialRenderMode.Simple : SpecialRenderMode.Outline);
                _circleX?.Draw(holder, camera, _circleHighlighted.X == 0f ? SpecialRenderMode.Simple : SpecialRenderMode.Outline);
                _circleY?.Draw(holder, camera, _circleHighlighted.Y == 0f ? SpecialRenderMode.Simple : SpecialRenderMode.Outline);
                _circleZ?.Draw(holder, camera, _circleHighlighted.Z == 0f ? SpecialRenderMode.Simple : SpecialRenderMode.Outline);
                _scale?.Draw(holder, camera, _scaleHighlighted ? SpecialRenderMode.Outline : SpecialRenderMode.Simple);
            } else {
                var mousePosition = holder.TryToGet<IMousePositionProvider>()?.GetRelative();
                var rayN = mousePosition == null ? null : (camera as CameraBase)?.GetPickingRay(mousePosition.Value, new Vector2(1f, 1f));
                if (!rayN.HasValue) return;

                var ray = rayN.Value;
                _arrowHighlighted = new Vector3(
                        _arrowX.DrawHighlighted(ray, holder, camera) ? 1f : 0f,
                        _arrowY.DrawHighlighted(ray, holder, camera) ? 1f : 0f,
                        _arrowZ.DrawHighlighted(ray, holder, camera) ? 1f : 0f);

                if (_arrowHighlighted == Vector3.Zero) {
                    _circleHighlighted = new Vector3(
                            _circleX?.DrawHighlighted(ray, holder, camera) ?? false ? 1f : 0f,
                            _circleY?.DrawHighlighted(ray, holder, camera) ?? false ? 1f : 0f,
                            _circleZ?.DrawHighlighted(ray, holder, camera) ?? false ? 1f : 0f);
                } else {
                    _circleHighlighted = Vector3.Zero;
                    _circleX?.Draw(holder, camera, SpecialRenderMode.Simple);
                    _circleY?.Draw(holder, camera, SpecialRenderMode.Simple);
                    _circleZ?.Draw(holder, camera, SpecialRenderMode.Simple);
                }

                if (_arrowHighlighted == Vector3.Zero && _circleHighlighted == Vector3.Zero) {
                    _scaleHighlighted = _scale?.DrawHighlighted(ray, holder, camera) ?? false;
                } else {
                    _scaleHighlighted = false;
                    _scale?.Draw(holder, camera, SpecialRenderMode.Simple);
                }
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _arrowX);
            DisposeHelper.Dispose(ref _arrowY);
            DisposeHelper.Dispose(ref _arrowZ);
            DisposeHelper.Dispose(ref _circleX);
            DisposeHelper.Dispose(ref _circleY);
            DisposeHelper.Dispose(ref _circleZ);
            DisposeHelper.Dispose(ref _scale);
        }

        public string Name => "__movable";

        public Matrix ParentMatrix { get; set; }

        public bool IsEnabled { get; set; } = true;

        public bool IsReflectable { get; set; } = false;

        public int GetTrianglesCount() {
            return 0;
        }

        public int GetObjectsCount() {
            return 0;
        }

        public BoundingBox? BoundingBox => default(BoundingBox);

        public void UpdateBoundingBox() {}

        public IRenderableObject Clone() {
            return new MoveableHelper(_parent, _rotationAxis, _allowScaling);
        }

        public float? CheckIntersection(Ray ray) {
            return null;
        }
    }
}