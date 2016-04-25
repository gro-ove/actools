using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
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

        public bool IsReflectable { get; set; } = true;

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

        public RenderableList() : this(Matrix.Identity) {}

        private void UpdateMatrix() {
            Matrix = _localMatrix * _parentMatrix;
            foreach (var child in this) {
                child.ParentMatrix = Matrix;
            }

            UpdateLocalBoundingBox();
        }

        public BoundingBox? LocalBoundingBox { get; private set; }

        public BoundingBox? BoundingBox { get; private set; }

        public BoundingBox? WorldBoundingBox { get; private set; }

        public void UpdateLocalBoundingBox() {
            LocalBoundingBox = this.Skip(1).Aggregate(this.FirstOrDefault()?.BoundingBox,
                    (current, child) => child.BoundingBox.HasValue ? current?.ExtendBy(child.BoundingBox.Value) : current);
            BoundingBox = LocalBoundingBox?.Transform(LocalMatrix);
            WorldBoundingBox = LocalBoundingBox?.Transform(Matrix);
        }
        
        public new void Add(IRenderableObject obj) {
            base.Add(obj);
            obj.ParentMatrix = Matrix;
            UpdateLocalBoundingBox();
        }

        public new void Remove(IRenderableObject obj) {
            base.Remove(obj);
            obj.ParentMatrix = Matrix;
            UpdateLocalBoundingBox();
        }

        public virtual void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;
            if (WorldBoundingBox == null || !camera.Visible(WorldBoundingBox.Value)) return;
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
