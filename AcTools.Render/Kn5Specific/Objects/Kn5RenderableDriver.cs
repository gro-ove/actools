using System;
using AcTools.Kn5File;
using AcTools.KnhFile;
using AcTools.KsAnimFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Animations;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
#pragma warning disable 649

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableDriver : Kn5RenderableSkinnable, IMoveable {
        private Knh _knh;

        public Kn5RenderableDriver(IKn5 kn5, Matrix matrix, string overridesDirectory, bool asyncTexturesLoading = true,
                bool asyncOverrideTexturesLoading = false, IKn5ToRenderableConverter converter = null)
                : base(kn5, matrix, overridesDirectory, asyncTexturesLoading, asyncOverrideTexturesLoading, converter) {
            /*foreach (var dummy in this.GetAllChildren().OfType<RenderableList>()) {
                dummy.HighlightBoundingBoxes = true;
            }*/
        }

        private void AlignNodes(KnhEntry entry, Matrix matrix) {
            var dummy = GetDummyByName(entry.Name);
            if (dummy != null) {
                dummy.LocalMatrix = entry.Transformation.ToMatrix();
            } else {
                matrix = entry.Transformation.ToMatrix() * matrix;
            }

            foreach (var child in entry.Children) {
                AlignNodes(child, matrix);
            }
        }

        public void AlignNodes(Knh node) {
            _knh = node;
            AlignNodes(_knh.RootEntry, Matrix.Identity);
        }

        public void RealignNodes() {
            if (_knh != null) {
                AlignNodes(_knh.RootEntry, Matrix.Identity);
            }
        }

        #region Movement
        public void ResetMovement() {
            ResetPosition();
        }

        public void SaveMovement(string steerKsAnimFilename) {
            if (RootNode == null) {
                throw new Exception("The root “DRIVER:DRIVER” node not found in model");
            }

            if (!_originalPosition.HasValue) {
                return;
            }

            LocalMatrix = _originalPosition.Value;
            _originalPosition = null;

            RootNode.LocalMatrix = _summaryMovement * RootNode.LocalMatrix;
            _summaryMovement = Matrix.Identity;
            _pivot.Reset();

            var anim = KsAnim.FromFile(steerKsAnimFilename);
            var root = anim.Entries.GetValueOrDefault("DRIVER:DRIVER");

            if (root == null) {
                throw new Exception("Animation do not have the root “DRIVER:DRIVER” node");
            }

            if (root.GetMatrices().Length > 1) {
                throw new Exception("The root “DRIVER:DRIVER” node is animated");
            }

            root.SetMatrices(new[] { RootNode.LocalMatrix }, root.Size);
            anim.SaveRecyclingOriginal(anim.OriginalFilename);
        }

        public void UpdateKnh() {
            if (RootNode == null) {
                throw new Exception("The root “DRIVER:DRIVER” node not found in model");
            }

            foreach (var child in _knh.RootEntry.Children.SelectManyRecursive(x => x?.Children)) {
                var dummy = GetDummyByName(child.Name);
                if (dummy != null) {
                    child.Transformation = dummy.LocalMatrix.ToArray();
                } else {
                    AcToolsLogging.Write("Dummy not found: " + child.Name);
                }
            }

            _knh.SaveRecyclingOriginal(_knh.OriginalFilename);
        }

        public bool Moved { get; set; }

        private LazierThis<RenderableList> _rootNode;

        [CanBeNull]
        private RenderableList RootNode => _rootNode.Get(() => GetDummyByName("DRIVER:DRIVER"));

        private LazierThis<Vector3> _pivot;

        private Vector3 Pivot => _pivot.Get(() => {
            UpdateBoundingBox();
            return Vector3.TransformCoordinate(BoundingBox?.GetCenter() ?? Vector3.Zero,
                    Matrix.Invert(Matrix));
        });

        private MoveableHelper _movable;
        public MoveableHelper Movable => _movable ?? (_movable = new MoveableHelper(this, MoveableRotationAxis.All, true));

        public void DrawMovementArrows(DeviceContextHolder holder, CameraBase camera) {
            Movable.ParentMatrix = Matrix.Translation(Vector3.TransformCoordinate(Pivot, Matrix));
            Movable.Draw(holder, camera, SpecialRenderMode.Simple);
        }

        private Matrix? _originalPosition;
        private Matrix _summaryMovement = Matrix.Identity;

        public void ResetPosition() {
            if (_originalPosition.HasValue) {
                LocalMatrix = _originalPosition.Value;
            }

            _summaryMovement = Matrix.Identity;
        }

        private void DeltaMatrix(Matrix delta) {
            if (!_originalPosition.HasValue) {
                _originalPosition = LocalMatrix;
            }

            Moved = true;
            _summaryMovement = delta * _summaryMovement;
                    LocalMatrix = delta * LocalMatrix;
            UpdateBoundingBox();
        }

        void IMoveable.Move(Vector3 delta) {
            DeltaMatrix(Matrix.Translation(Vector3.TransformNormal(delta, Matrix.Invert(ParentMatrix))));
        }

        void IMoveable.Rotate(Quaternion delta) {
            var pivot = Matrix.Translation(Pivot);
            DeltaMatrix(Matrix.Invert(pivot) * Matrix.RotationQuaternion(delta) * pivot);
        }

        void IMoveable.Scale(Vector3 scale) {
            DeltaMatrix(Matrix.Scaling(scale));
        }

        IMoveable IMoveable.Clone() {
            return null;
        }
        #endregion
    }
}