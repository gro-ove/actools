using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class RenderableList : List<IRenderableObject>, IRenderableObject {
        public string Name { get; }

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

        public RenderableList([CanBeNull] string name, Matrix localMatrix, IEnumerable<IRenderableObject> children) : base(children) {
            LocalMatrix = localMatrix;
            Name = name;
            UpdateMatrix();
        }

        public RenderableList([CanBeNull] string name, Matrix localMatrix) {
            LocalMatrix = localMatrix;
            Name = name;
            UpdateMatrix();
        }

        public RenderableList() : this(null, Matrix.Identity) { }

        private void UpdateMatrix() {
            Matrix = _localMatrix * _parentMatrix;
            foreach (var child in this) {
                child.ParentMatrix = Matrix;
            }
        }

        public int GetTrianglesCount() {
            return this.Where(x => x.IsEnabled).Aggregate(0, (a, b) => a + b.GetTrianglesCount());
        }

        public int GetObjectsCount() {
            return this.Where(x => x.IsEnabled).Aggregate(0, (a, b) => a + b.GetObjectsCount());
        }

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

        public new void Insert(int index, IRenderableObject obj) {
            base.Insert(index, obj);
            obj.ParentMatrix = Matrix;
        }

        public new void InsertRange(int index, IEnumerable<IRenderableObject> objs) {
            foreach (var o in objs) {
                base.Insert(index++, o);
                o.ParentMatrix = Matrix;
            }
        }

        public virtual void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (!IsEnabled || filter?.Invoke(this) == false) return;
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;
            if (camera != null && (BoundingBox == null || !camera.Visible(BoundingBox.Value))) return;

            var c = Count;
            for (var i = 0; i < c; i++) {
                var child = this[i];
                if (child.IsEnabled) {
                    child.Draw(contextHolder, camera, mode, filter);
                }
            }
        }

        public RenderableList Clone() {
            return new RenderableList(Name + "_copy", LocalMatrix, this.Select(x => x.Clone())) {
                IsEnabled = IsEnabled,
                IsReflectable = IsReflectable,
                ParentMatrix = ParentMatrix,
                BoundingBox = BoundingBox
            };
        }

        IRenderableObject IRenderableObject.Clone() {
            return Clone();
        }

        public virtual void Dispose() {
            this.DisposeEverything();
        }
    }
}
