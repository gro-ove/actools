using System;
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

        public bool IsEnabled { get; set; } = true;

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
        }

        public int TrianglesCount => this.Aggregate(0, (a, b) => a + b.TrianglesCount);

        public int ObjectsCount => this.Aggregate(0, (a, b) => a + b.ObjectsCount);

        public BoundingBox? BoundingBox { get; private set; }

        public void UpdateBoundingBox() {
            BoundingBox? bb = null;

            for (var i = 0; i < Count; i++) {
                var c = this[i];
                if (!c.IsEnabled) continue;

                c.UpdateBoundingBox();
                var cb = c.BoundingBox;
                if (!cb.HasValue) continue;

                bb = bb?.ExtendBy(cb.Value) ?? c.BoundingBox;
            }

            BoundingBox = bb;
        }
        
        public new void Add(IRenderableObject obj) {
            base.Add(obj);
            obj.ParentMatrix = Matrix;
        }
        
        public new void AddRange(IEnumerable<IRenderableObject> objs) {
            foreach (var o in objs) {
                base.Add(o);
                o.ParentMatrix = Matrix;
            }
        }

        public new void Remove(IRenderableObject obj) {
            base.Remove(obj);
            obj.ParentMatrix = Matrix;
        }

        public virtual void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!IsEnabled) return;
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;
            if (BoundingBox == null || !camera.Visible(BoundingBox.Value)) return;
            foreach (var child in this.Where(x => x.IsEnabled)) {
                child.Draw(contextHolder, camera, mode);
            }
        }

        public virtual void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter) {
            if (!IsEnabled || !filter(this)) return;
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;
            if (BoundingBox == null || !camera.Visible(BoundingBox.Value)) return;
            foreach (var child in this.Where(x => x.IsEnabled)) {
                child.Draw(contextHolder, camera, mode, filter);
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
