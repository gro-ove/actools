using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Buffer = SlimDX.Direct3D11.Buffer;

namespace AcTools.Render.Base.Objects {
    public class LinesRenderableObject<T> : BaseRenderableObject where T : struct, InputLayouts.ILayout {
        protected bool IsEmpty { get; private set; }

        public T[] Vertices { get; private set; }

        public ushort[] Indices { get; private set; }

        public int IndicesCount { get; private set; }

        private DataStream _verticesStream, _indicesStream;
        private Buffer _verticesBuffer, _indicesBuffer;
        private VertexBufferBinding _verticesBufferBinding;

        public LinesRenderableObject([CanBeNull] string name, T[] vertices, ushort[] indices) : base(name) {
            Vertices = vertices;
            Indices = indices;
            IndicesCount = Indices.Length;
            IsEmpty = IndicesCount == 0;
        }

        private static ushort[] GetIndices(int verticesCount) {
            var result = new ushort[verticesCount];
            for (var i = 0; i < result.Length; i++) {
                result[i] = (ushort)i;
            }
            return result;
        }

        public LinesRenderableObject([CanBeNull] string name, T[] vertices) : this(name, vertices, GetIndices(vertices.Length)) {}

        public override bool IsEnabled {
            get { return !IsEmpty && base.IsEnabled; }
            set { base.IsEnabled = value; }
        }

        public override int GetTrianglesCount() {
            return IndicesCount / 2;
        }

        public override void UpdateBoundingBox() {
            BoundingBox = IsEmpty ? (BoundingBox?)null : Vertices.Select(x => Vector3.TransformCoordinate(x.Position, ParentMatrix)).ToBoundingBox();
        }

        public override BaseRenderableObject Clone() {
            return new LinesRenderableObject<T>(Name + "_copy", Vertices, Indices) {
                IsReflectable = IsReflectable,
                IsEnabled = IsEnabled
            };
        }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            if (IsEmpty) return;

            var vbd = new BufferDescription(Vertices[0].Stride * Vertices.Length, ResourceUsage.Immutable,
                    BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _verticesStream = new DataStream(Vertices, false, false);
            _verticesBuffer = new Buffer(contextHolder.Device, _verticesStream, vbd);
            _verticesBufferBinding = new VertexBufferBinding(_verticesBuffer, Vertices[0].Stride, 0);

            var ibd = new BufferDescription(sizeof(ushort) * Indices.Length, ResourceUsage.Immutable,
                    BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _indicesStream = new DataStream(Indices, false, false);
            _indicesBuffer = new Buffer(contextHolder.Device, _indicesStream, ibd);
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            var assembler = contextHolder.DeviceContext.InputAssembler;
            assembler.PrimitiveTopology = PrimitiveTopology.LineList;
            assembler.SetVertexBuffers(0, _verticesBufferBinding);
            assembler.SetIndexBuffer(_indicesBuffer, Format.R16_UInt, 0);
        }

        public override void Dispose() {
            Vertices = new T[0];
            Indices = new ushort[0];
            IndicesCount = 0;
            IsEmpty = true;

            DisposeHelper.Dispose(ref _verticesBuffer);
            DisposeHelper.Dispose(ref _indicesBuffer);
            DisposeHelper.Dispose(ref _verticesStream);
            DisposeHelper.Dispose(ref _indicesStream);
        }
    }
}