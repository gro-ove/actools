using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class RenderableList : List<IRenderableObject>, IRenderableObject {
        public string Name { get; }

        public Matrix Matrix { get; private set; }

        private Matrix _parentMatrix = Matrix.Identity;

        public Matrix ParentMatrix {
            get => _parentMatrix;
            set {
                if (Equals(_parentMatrix, value)) return;
                _parentMatrix = value;
                OnMatrixChanged();
            }
        }

        public bool IsReflectable { get; set; } = true;

        public bool IsEnabled { get; set; } = true;

        private Matrix _localMatrix = Matrix.Identity;

        public Matrix LocalMatrix {
            get => _localMatrix;
            set {
                if (Equals(_localMatrix, value)) return;
                _localMatrix = value;
                OnMatrixChanged();

                foreach (var clone in _clones) {
                    if (clone == null) continue;
                    clone.LocalMatrix = value;
                }
            }
        }

        public event EventHandler MatrixChanged;

        protected void OnMatrixChanged() {
            MatrixChanged?.Invoke(this, EventArgs.Empty);
            UpdateMatrix();
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
            for (var i = Count - 1; i >= 0; i--) {
                this[i].ParentMatrix = Matrix;
            }
        }

        private Vector3? _originalLocalPos;
        private Matrix _originalMatrix;
        private RenderableList _lookAt;

        public void LookAtDirMode(Vector3 globalPoint, Vector3 up) {
            if (!_originalLocalPos.HasValue) {
                _originalLocalPos = LocalMatrix.GetTranslationVector();
                _originalMatrix = LocalMatrix;
            }

            var globalPos = Vector3.TransformCoordinate(_originalLocalPos.Value, ParentMatrix);
            var yAxis = Vector3.TransformNormal(up, _originalMatrix * ParentMatrix);
            LocalMatrix = globalPos.LookAtMatrixXAxis(globalPoint, yAxis) * ParentMatrix.Invert_v2();
        }

        public void LookAt(Vector3 globalPoint, Vector3 up) {
            var globalPos = Vector3.TransformCoordinate(LocalMatrix.GetTranslationVector(), ParentMatrix);
            var yAxis = up;
            LocalMatrix = globalPos.LookAtMatrix(globalPoint, yAxis) * ParentMatrix.Invert_v2();
        }

        private Matrix _prevThisMatrix, _prevLookAtMatrix;

        private void UpdateLookAt() {
            if (_lookAt == null || Equals(Matrix, _prevThisMatrix) && Equals(_prevLookAtMatrix, _lookAt.Matrix)) return;
            _prevThisMatrix = Matrix;
            _prevLookAtMatrix = _lookAt.Matrix;
            LookAtDirMode(_prevLookAtMatrix.GetTranslationVector(), Vector3.UnitY);
        }

        public void LookAt(RenderableList target) {
            _lookAt = target;
            UpdateLookAt();
        }

        public int GetTrianglesCount() {
            return this.Where(x => x.IsEnabled).Aggregate(0, (a, b) => a + b.GetTrianglesCount());
        }

        public int GetObjectsCount() {
            return this.Where(x => x.IsEnabled).Aggregate(0, (a, b) => a + b.GetObjectsCount());
        }

        public IEnumerable<int> GetMaterialIds() {
            return this.Where(x => x.IsEnabled).SelectMany(x => x.GetMaterialIds());
        }

        public BoundingBox? BoundingBox { get; private set; }

        public void UpdateBoundingBox() {
            BoundingBox? bb = null;

            for (var i = Count - 1; i >= 0; i--) {
                var c = this[i];
                if (!c.IsEnabled) continue;

                c.UpdateBoundingBox();
                var cb = c.BoundingBox;
                if (!cb.HasValue) continue;

                bb = bb?.ExtendBy(cb.Value) ?? c.BoundingBox;
            }

            BoundingBox = bb;
        }

        public new void Add([NotNull] IRenderableObject obj) {
            base.Add(obj);
            obj.ParentMatrix = Matrix;
        }

        public new void AddRange([NotNull, ItemNotNull] IEnumerable<IRenderableObject> objs) {
            foreach (var o in objs) {
                base.Add(o);
                o.ParentMatrix = Matrix;
            }
        }

        public new void Insert(int index, [NotNull] IRenderableObject obj) {
            base.Insert(index, obj);
            obj.ParentMatrix = Matrix;
        }

        public new void InsertRange(int index, [NotNull, ItemNotNull] IEnumerable<IRenderableObject> objs) {
            foreach (var o in objs) {
                base.Insert(index++, o);
                o.ParentMatrix = Matrix;
            }
        }

        private void DrawChildren(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (!IsEnabled || filter?.Invoke(this) == false) return;
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;
            if (camera != null && BoundingBox != null && !camera.Visible(BoundingBox.Value)) return;

            UpdateLookAt();

#if DEBUG
            try {
#endif

                var c = Count;
                for (var i = 0; i < c; i++) {
                    var child = this[i];
                    if (child.IsEnabled) {
                        child.Draw(contextHolder, camera, mode, filter);
#if DEBUG
                        if (Count != c) {
                            throw new Exception("Collection modified: " + Name);
                        }
#endif
                    }
                }

                if (HighlightBoundingBoxes && mode == SpecialRenderMode.SimpleTransparent) {
                    if (_box == null) {
                        var box = GeometryGenerator.CreateLinesBox(new Vector3(1f));
                        _box = new DebugLinesObject(Matrix.Identity,
                                box.Vertices.Select(x => new InputLayouts.VerticePC(x.Position, new Color4(1f, 1f, 0.5f, 0f))).ToArray(),
                                box.Indices.ToArray());
                    }

                    for (var i = 0; i < c; i++) {
                        var child = this[i];
                        if (child.IsEnabled && child.BoundingBox.HasValue) {
                            var bb = child.BoundingBox.Value;
                            _box.ParentMatrix = Matrix.Scaling(bb.GetSize()) * Matrix.Translation(bb.GetCenter());
                            _box.Draw(contextHolder, camera, SpecialRenderMode.Simple);
                        }
                    }
                }

#if DEBUG
            } catch (Exception e) {
                throw new Exception("Collection exception: " + Name, e);
            }
#endif
        }

        public virtual void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            DrawChildren(holder, camera, mode, filter);
            if (HighlightDummy && mode == SpecialRenderMode.SimpleTransparent) {
                if (_lines == null) {
                    _lines = new DebugLinesObject(Matrix.Identity, new[] {
                        new InputLayouts.VerticePC(new Vector3(0f, 0f, 0f), new Color4(0, 1, 0)),
                        new InputLayouts.VerticePC(new Vector3(0f, 0.02f, 0f), new Color4(0, 1, 0)),
                        new InputLayouts.VerticePC(new Vector3(0f, 0f, 0f), new Color4(1, 0, 0)),
                        new InputLayouts.VerticePC(new Vector3(0.02f, 0f, 0f), new Color4(1, 0, 0)),
                        new InputLayouts.VerticePC(new Vector3(0f, 0f, 0f), new Color4(0, 0, 1)),
                        new InputLayouts.VerticePC(new Vector3(0f, 0f, 0.02f), new Color4(0, 0, 1)),
                    });
                }

                _lines.ParentMatrix = Matrix;
                _lines.Draw(holder, camera, SpecialRenderMode.Simple);
            }
        }

        private readonly WeakList<RenderableList> _clones = new WeakList<RenderableList>(1);

        public RenderableList Clone() {
            var result = new RenderableList(Name + "_copy", LocalMatrix, this.Select(x => x.Clone())) {
                IsEnabled = IsEnabled,
                IsReflectable = IsReflectable,
                ParentMatrix = ParentMatrix,
                BoundingBox = BoundingBox
            };
            _clones.Add(result);
            return result;
        }

        public float? CheckIntersection(Ray ray) {
            return null;
        }

        IRenderableObject IRenderableObject.Clone() {
            return Clone();
        }

        public bool HighlightDummy { get; set; }

        public bool HighlightBoundingBoxes { get; set; }

        private static DebugLinesObject _lines, _box;

        public virtual void Dispose() {
            DisposeHelper.Dispose(ref _lines);
            DisposeHelper.Dispose(ref _box);
            this.DisposeEverything();
        }
    }
}
