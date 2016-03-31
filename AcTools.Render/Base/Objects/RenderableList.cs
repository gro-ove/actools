using System.Collections.Generic;
using AcTools.Render.Base.Camera;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class RenderableList : List<IRenderableObject>, IRenderableObject {
        public Matrix Matrix { get; private set; }

        private Matrix _parentMatrix = Matrix.Identity;
        public Matrix ParentMatrix {
            get { return _parentMatrix; }
            set {
                if (Equals(_parentMatrix, value)) return;
                _parentMatrix = value;
                UpdateMatrix();
            }
        }

        private Matrix _localMatrix = Matrix.Identity;
        public Matrix LocalMatrix {
            get { return _localMatrix; }
            set {
                if (Equals(_localMatrix, value)) return;
                _localMatrix = value;
                UpdateMatrix();
            }
        }

        public RenderableList(Matrix localMatrix, IEnumerable<IRenderableObject> children) : base(children) {
            LocalMatrix = localMatrix;
            UpdateMatrix();
        }

        public RenderableList(Matrix localMatrix) {
            LocalMatrix = localMatrix;
            UpdateMatrix();
        }

        public RenderableList() : this(Matrix.Identity) {
        }

        private void UpdateMatrix() {
            Matrix = _localMatrix*_parentMatrix;
            foreach (var child in this) {
                child.ParentMatrix = Matrix;
            }
        }

        public new void Add(IRenderableObject obj) {
            base.Add(obj);
            obj.ParentMatrix = Matrix;
        }

        public virtual void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            foreach (var child in this) {
                child.Draw(contextHolder, camera, mode);
            }
        }

        public void Dispose() {
            foreach (var child in this) {
                child.Dispose();
            }
            Clear();
        }
    }
}
