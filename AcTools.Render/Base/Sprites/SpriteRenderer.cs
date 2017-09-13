using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;
using Buffer = SlimDX.Direct3D11.Buffer;
using Device = SlimDX.Direct3D11.Device;
using MapFlags = SlimDX.Direct3D11.MapFlags;

namespace AcTools.Render.Base.Sprites {
    /// <summary>
    /// Specifies, how coordinates are interpreted.
    /// </summary>
    public enum CoordinateType {
        /// <summary>
        /// Coordinates are in the range from 0 to 1. (0, 0) is the top left corner; (1, 1) is the bottom right corner.
        /// </summary>
        UNorm,

        /// <summary>
        /// Coordinates are in the range from -1 to 1. (-1, -1) is the bottom left corner; (1, 1) is the top right corner. This is the DirectX standard interpretation.
        /// </summary>
        SNorm,

        /// <summary>
        /// Coordinates are in the range of the relative screen size. (0, 0) is the top left corner; (ScreenSize.X, ScreenSize.Y) is the bottom right corner. A variable screen size is used. Use <see cref="SpriteRenderer.ScreenSize"/>.
        /// </summary>
        Relative,

        /// <summary>
        /// Coordinates are in the range of the actual screen size. (0, 0) is the top left corner; (Viewport.Width, Viewport.Height) is the bottom right corner. Use <see cref="SpriteRenderer.RefreshViewport"/> for updates to the used viewport.
        /// </summary>
        Absolute
    }

    /// <summary>
    /// Specify the alpha blending mode of the texture being drawn
    /// </summary>
    public enum AlphaBlendModeType {
        /// <summary>
        /// Alpha blending is postmultiplied (straight)
        /// </summary>
        PostMultiplied,

        /// <summary>
        /// The sprite renderer assumes alpha channel is premultiplied
        /// </summary>
        PreMultiplied
    }

    /// <summary>
    /// This class is responsible for rendering 2D sprites. Typically, only one instance of this class is necessary.
    /// </summary>
    public class SpriteRenderer : IDisposable {
        private readonly int _bufferSize;
        private Viewport _viewport;

        /// <summary>
        /// The blend state to use for rendering sprites
        /// </summary>
        protected BlendState BlendState;

        /// <summary>
        /// The depth stencil state to use for rendering sprites
        /// </summary>
        protected DepthStencilState DepthStencilState;

        /// <summary>
        /// Returns the Direct3D device that this SpriteRenderer was created for.
        /// </summary>
        public Device Device { get; }

        private readonly DeviceContext _context;

        /// <summary>
        /// Gets or sets, if this SpriteRenderer handles DepthStencilState
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sprites have to be drawn with depth test disabled. If HandleDepthStencilState is set to true, the
        /// SpriteRenderer sets the DepthStencilState to a predefined state before drawing and resets it to
        /// the previous state after that. Set this value to false, if you want to handle states yourself.
        /// </para>
        /// <para>
        /// The default value is true.
        /// </para>
        /// </remarks>
        public bool HandleDepthStencilState { get; set; }

        /// <summary>
        /// Gets or sets, if this SpriteRenderer handles BlendState
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sprites have to be drawn with simple alpha blending. If HandleBlendState is set to true, the
        /// SpriteRenderer sets the BlendState to a predefined state before drawing and resets it to
        /// the previous state after that. Set this value to false, if you want to handle states yourself.
        /// </para>
        /// <para>
        /// The default value is true.
        /// </para>
        /// </remarks>
        public bool HandleBlendState { get; set; }

        /// <summary>
        /// Gets or sets the alpha blending mode of this sprite renderer
        /// <para>
        /// If AlphaBlendMode is changed a flush operation immedietly occurs.
        /// </para>
        /// </summary>
        public AlphaBlendModeType AlphaBlendMode {
            get { return _alphaBlendMode; }
            set {
                if (_alphaBlendMode != value) {
                    Flush();
                    _alphaBlendMode = value;
                    UpdateAlphaBlend();
                }
            }
        }

        // This variable has to be protected from cross-thread access.
        private bool _lockDeviceOnDraw;

        /// <summary>
        /// Gets or sets whether to lock the device when rendering sprites. This can be used for multi-threaded rendering.
        /// However, locking comes with performance penalties.
        /// <remarks>The default value is false.</remarks>
        /// </summary>
        public bool LockDeviceOnDraw {
            get { return _lockDeviceOnDraw; }
            set {
                lock (Device) {
                    _lockDeviceOnDraw = value;
                }
            }
        }

        /// <summary>
        /// Set to true, if the order of draw calls can be rearranged for better performance.
        /// </summary>
        /// <remarks>
        /// Sprites are not drawn immediately, but only on a call to <see cref="SpriteRenderer.Flush"/>.
        /// Rendering performance can be improved, if the order of sprites can be changed, so that sprites
        /// with the same texture can be drawn with one draw call. However, this will not preserve the z-order.
        /// Use <see cref="SpriteRenderer.ClearReorderBuffer"/> to force a set of sprites to be drawn before another set.
        /// </remarks>
        /// <example>
        /// Consider the following pseudo code:
        /// <code>
        /// Draw left intense red circle
        /// Draw middle light red circle
        /// Draw right intense red circle
        /// </code>
        /// <para>With AllowReorder set to true, this will result in the following image:<br/>
        /// <img src="../Reorder1.jpg" alt=""/><br/>
        /// That is because the last circle is reordered to be drawn together with the first circle.
        /// </para>
        /// <para>With AllowReorder set to false, this will result in the following image:<br/>
        /// <img src="../Reorder2.jpg" alt=""/><br/>
        /// No optimization is applied. Performance may be slightly worse than with reordering.
        /// </para>
        /// </example>
        public bool AllowReorder { get; set; }

        /// <summary>
        /// When using relative coordinates, the screen size has to be set. Typically the screen size in pixels is used. However, other values are possible as well.
        /// </summary>
        public Vector2 ScreenSize { get; set; }

        /// <summary>
        /// A list of all sprites to draw. Sprites are drawn in the order in this list.
        /// </summary>
        private readonly List<SpriteSegment> _sprites = new List<SpriteSegment>();

        /// <summary>
        /// Allows direct access to the according SpriteSegments based on the texture
        /// </summary>
        private readonly Dictionary<object, List<SpriteSegment>> _textureSprites = new Dictionary<object, List<SpriteSegment>>();

        /// <summary>
        /// The number of currently buffered sprites
        /// </summary>
        private int _spriteCount = 0;

        /// <summary>
        /// The active Alpha blending mode
        /// </summary>
        private AlphaBlendModeType _alphaBlendMode;

        /// <summary>
        /// Create a new SpriteRenderer instance.
        /// </summary>
        /// <param name="contextHolder">Device context holder.</param>
        /// <param name="bufferSize">The number of elements that can be stored in the sprite buffer.</param>
        /// <remarks>
        /// Sprites are not drawn immediately, but buffered instead. The buffer size defines, how much sprites can be buffered.
        /// If the buffer is full, according draw calls will be issued on the GPU clearing the buffer. Its size should be as big as
        /// possible without wasting empty space.
        /// </remarks>
        public SpriteRenderer(DeviceContextHolder contextHolder, int bufferSize = 128) {
            _bufferSize = bufferSize;

            AllowReorder = true;
            HandleDepthStencilState = true;
            HandleBlendState = true;
            _lockDeviceOnDraw = false;

            Device = contextHolder.Device;
            _context = contextHolder.DeviceContext;

            Initialize(contextHolder);
        }

        #region ### private SlimDX field memebers ###
        private Buffer _vb;
        private VertexBufferBinding _vbBinding;
        #endregion

        #region ### Public draw interface ###
        /// <summary>
        /// Draws a region of a texture on the screen.
        /// </summary>
        /// <param name="texture">The shader resource view of the texture to draw</param>
        /// <param name="position">Position of the center of the texture in the chosen coordinate system</param>
        /// <param name="size">Size of the texture in the chosen coordinate system. The size is specified in the screen's coordinate system.</param>
        /// <param name="coordinateType">A custom coordinate system in which to draw the texture</param>
        /// <param name="color">The color with which to multiply the texture</param>
        /// <param name="texCoords">Texture coordinates for the top left corner</param>
        /// <param name="texCoordsSize">Size of the region in texture coordinates</param>
        public void Draw(ShaderResourceView texture, Vector2 position, Vector2 size, Vector2 texCoords, Vector2 texCoordsSize, Color4 color, CoordinateType coordinateType) {
            Draw(texture, position, size, Vector2.Zero, 0, texCoords, texCoordsSize, color, coordinateType);
        }
        #endregion

        #region ### Template method hooks ###

        protected Viewport QueryViewport() {
            return Device.ImmediateContext.Rasterizer.GetViewports()[0];
        }

        protected void CreateVertexBuffer(int elementByteSize, int elements) {
            _vb = new Buffer(Device, elementByteSize * elements, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None,
                    elementByteSize) { DebugName = "Sprites Vertexbuffer" };
            _vbBinding = new VertexBufferBinding(_vb, elementByteSize, 0);
        }

        protected void CreateDepthStencilAndBlendState() {
            var dssd = new DepthStencilStateDescription() {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero
            };
            DepthStencilState = DepthStencilState.FromDescription(Device, dssd);

            var blendDesc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            blendDesc.RenderTargets[0].BlendOperation = BlendOperation.Add;
            blendDesc.RenderTargets[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            blendDesc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
            blendDesc.RenderTargets[0].BlendEnable = true;
            blendDesc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
            blendDesc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
            blendDesc.RenderTargets[0].SourceBlendAlpha = BlendOption.SourceAlpha;
            blendDesc.RenderTargets[0].DestinationBlendAlpha = BlendOption.InverseSourceAlpha;
            BlendState = BlendState.FromDescription(Device, blendDesc);
        }

        protected void UpdateVertexBufferData<T>(T[] vertices) where T : struct {
            var data = _context.MapSubresource(_vb, MapMode.WriteDiscard, MapFlags.None);
            data.Data.WriteRange(vertices);
            _context.UnmapSubresource(_vb, 0);
        }

        protected void InitRendering() {
            Device.ImmediateContext.InputAssembler.InputLayout = _shader.LayoutSpriteSpecific;
            Device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
            Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, _vbBinding);
        }

        protected void Draw(object texture, int count, int offset) {
            _shader.FxTex.SetResource((ShaderResourceView)texture);
            _shader.TechRender.GetPassByIndex(0).Apply(_context);
            Device.ImmediateContext.Draw(count, offset);
        }

        protected void DisposeOfResources() {
            _vb.Dispose();
        }
        #endregion

        protected void UpdateAlphaBlend() {
            throw new NotImplementedException();
        }

        private EffectSpriteShader _shader;

        /// <summary>
        /// Initializes the sprite renderer so it is set up for use.
        /// </summary>
        protected void Initialize(DeviceContextHolder device) {
            _shader = device.GetEffect<EffectSpriteShader>();

            CreateVertexBuffer(VerticeSpriteSpecific.StrideValue, _bufferSize);
            CreateDepthStencilAndBlendState();
            RefreshViewport();
        }

        /// <summary>
        /// Updates the viewport used for absolute positioning. The first current viewport of the device's rasterizer will be used.
        /// </summary>
        public void RefreshViewport() {
            _viewport = QueryViewport();
        }

        /// <summary>
        /// Closes a reorder session. Further draw calls will not be drawn together with previous draw calls.
        /// </summary>
        public void ClearReorderBuffer() {
            lock (_textureSprites) {
                _textureSprites.Clear();
            }
        }

        private Vector2 ConvertCoordinate(Vector2 coordinate, CoordinateType coordinateType) {
            switch (coordinateType) {
                case CoordinateType.SNorm:
                    return coordinate;
                case CoordinateType.UNorm:
                    coordinate.X = (coordinate.X - 0.5f) * 2;
                    coordinate.Y = -(coordinate.Y - 0.5f) * 2;
                    return coordinate;
                case CoordinateType.Relative:
                    coordinate.X = coordinate.X / ScreenSize.X * 2 - 1;
                    coordinate.Y = -(coordinate.Y / ScreenSize.Y * 2 - 1);
                    return coordinate;
                case CoordinateType.Absolute:
                    coordinate.X = coordinate.X / _viewport.Width * 2 - 1;
                    coordinate.Y = -(coordinate.Y / _viewport.Height * 2 - 1);
                    return coordinate;
            }
            return Vector2.Zero;
        }

        /// <summary>
        /// Draws a complete texture on the screen.
        /// </summary>
        /// <param name="texture">The shader resource view of the texture to draw</param>
        /// <param name="position">Position of the top left corner of the texture in the chosen coordinate system</param>
        /// <param name="size">Size of the texture in the chosen coordinate system. The size is specified in the screen's coordinate system.</param>
        /// <param name="coordinateType">A custom coordinate system in which to draw the texture</param>
        protected internal void Draw(object texture, Vector2 position, Vector2 size, CoordinateType coordinateType) {
            Draw(texture, position, size, Vector2.Zero, 0, coordinateType);
        }

        /// <summary>
        /// Draws a complete texture on the screen.
        /// </summary>
        /// <param name="texture">The shader resource view of the texture to draw</param>
        /// <param name="position">Position of the top left corner of the texture in the chosen coordinate system</param>
        /// <param name="size">Size of the texture in the chosen coordinate system. The size is specified in the screen's coordinate system.</param>
        /// <param name="center">Specify the texture's center in the chosen coordinate system. The center is specified in the texture's local coordinate system. E.g. for <paramref name="coordinateType"/>=CoordinateType.SNorm, the texture's center is defined by (0, 0).</param>
        /// <param name="rotationAngle">The angle in radians to rotate the texture. Positive values mean counter-clockwise rotation. Rotations can only be applied for relative or absolute coordinates. Consider using the Degrees or Radians helper structs.</param>
        /// <param name="coordinateType">A custom coordinate system in which to draw the texture</param>
        protected internal void Draw(object texture, Vector2 position, Vector2 size, Vector2 center, double rotationAngle, CoordinateType coordinateType) {
            Draw(texture, position, size, center, rotationAngle, new Color4(1, 1, 1, 1), coordinateType);
        }

        /// <summary>
        /// Draws a complete texture on the screen.
        /// </summary>
        /// <param name="texture">The shader resource view of the texture to draw</param>
        /// <param name="position">Position of the top left corner of the texture in the chosen coordinate system</param>
        /// <param name="size">Size of the texture in the chosen coordinate system. The size is specified in the screen's coordinate system.</param>
        /// <param name="coordinateType">A custom coordinate system in which to draw the texture</param>
        /// <param name="color">The color with which to multiply the texture</param>
        protected internal void Draw(object texture, Vector2 position, Vector2 size, Color4 color, CoordinateType coordinateType) {
            Draw(texture, position, size, Vector2.Zero, 0, Vector2.Zero, new Vector2(1, 1), color, coordinateType);
        }

        /// <summary>
        /// Draws a complete texture on the screen.
        /// </summary>
        /// <param name="texture">The shader resource view of the texture to draw</param>
        /// <param name="position">Position of the top left corner of the texture in the chosen coordinate system</param>
        /// <param name="size">Size of the texture in the chosen coordinate system. The size is specified in the screen's coordinate system.</param>
        /// <param name="center">Specify the texture's center in the chosen coordinate system. The center is specified in the texture's local coordinate system. E.g. for <paramref name="coordinateType"/>=CoordinateType.SNorm, the texture's center is defined by (0, 0).</param>
        /// <param name="rotationAngle">The angle in radians to rotate the texture. Positive values mean counter-clockwise rotation. Rotations can only be applied for relative or absolute coordinates. Consider using the Degrees or Radians helper structs.</param>
        /// <param name="coordinateType">A custom coordinate system in which to draw the texture</param>
        /// <param name="color">The color with which to multiply the texture</param>
        protected internal void Draw(object texture, Vector2 position, Vector2 size, Vector2 center, double rotationAngle, Color4 color,
                CoordinateType coordinateType) {
            Draw(texture, position, size, center, rotationAngle, Vector2.Zero, new Vector2(1, 1), color, coordinateType);
        }

        internal static Vector2 Rotate(Vector2 v, float sine, float cosine) {
            return new Vector2(cosine * v.X + sine * v.Y, -sine * v.X + cosine * v.Y);
        }

        /// <summary>
        /// Draws a region of a texture on the screen.
        /// </summary>
        /// <param name="texture">The shader resource view of the texture to draw</param>
        /// <param name="position">Position of the center of the texture in the chosen coordinate system</param>
        /// <param name="size">Size of the texture in the chosen coordinate system. The size is specified in the screen's coordinate system.</param>
        /// <param name="center">Specify the texture's center in the chosen coordinate system. The center is specified in the texture's local coordinate system. E.g. for <paramref name="coordinateType"/>=CoordinateType.SNorm, the texture's center is defined by (0, 0).</param>
        /// <param name="rotationAngle">The angle in radians to rotate the texture. Positive values mean counter-clockwise rotation. Rotations can only be applied for relative or absolute coordinates. Consider using the Degrees or Radians helper structs.</param>
        /// <param name="coordinateType">A custom coordinate system in which to draw the texture</param>
        /// <param name="color">The color with which to multiply the texture</param>
        /// <param name="texCoords">Texture coordinates for the top left corner</param>
        /// <param name="texCoordsSize">Size of the region in texture coordinates</param>
        protected internal void Draw(object texture, Vector2 position, Vector2 size, Vector2 center, double rotationAngle, Vector2 texCoords,
                Vector2 texCoordsSize, Color4 color, CoordinateType coordinateType) {
            Draw(texture, position, size, center,
                    rotationAngle == 0d ? 0f : (float)Math.Sin(rotationAngle),
                    rotationAngle == 0d ? 0f : (float)Math.Cos(rotationAngle),
                    texCoords, texCoordsSize, color, coordinateType);
        }

        protected internal void Draw(object texture, Vector2 position, Vector2 size, Vector2 center, float rotationSine, float rotationCosine, Vector2 texCoords,
                Vector2 texCoordsSize, Color4 color, CoordinateType coordinateType) {
            if (texture == null) return;

            size.X = Math.Abs(size.X);
            size.Y = Math.Abs(size.Y);

            // Difference vectors from the center to the texture edges (in screen coordinates).
            Vector2 left, up, right, down;
            if (coordinateType == CoordinateType.UNorm) {
                left = new Vector2(0 - center.X * size.X, 0);
                up = new Vector2(0, 0 - center.Y * size.Y);
                right = new Vector2((1 - center.X) * size.X, 0);
                down = new Vector2(0, (1 - center.Y) * size.Y);
            } else if (coordinateType == CoordinateType.SNorm) {
                left = new Vector2((-1 - center.X) * size.X / 2, 0);
                up = new Vector2(0, (1 - center.Y) * size.Y / 2);
                right = new Vector2((1 - center.X) * size.X / 2, 0);
                down = new Vector2(0, (-1 - center.Y) * size.Y / 2);
            } else {
                left = new Vector2(-center.X, 0);
                up = new Vector2(0, -center.Y);
                right = new Vector2(size.X - center.X, 0);
                down = new Vector2(0, size.Y - center.Y);
            }

            if (rotationSine != 0 || rotationCosine != 0) {
                if (coordinateType != CoordinateType.Absolute && coordinateType != CoordinateType.Relative) {
                    // Normalized coordinates tend to be skewed when applying rotation
                    throw new ArgumentException("Rotation is only allowed for relative or absolute coordinates");
                }

                left = Rotate(left, rotationSine, rotationCosine);
                right = Rotate(right, rotationSine, rotationCosine);
                up = Rotate(up, rotationSine, rotationCosine);
                down = Rotate(down, rotationSine, rotationCosine);
            }

            var data = new VerticeSpriteSpecific {
                TexCoord = texCoords,
                TexCoordSize = texCoordsSize,
                Color = color.ToArgb(),
                TopLeft = ConvertCoordinate(position + up + left, coordinateType),
                TopRight = ConvertCoordinate(position + up + right, coordinateType),
                BottomLeft = ConvertCoordinate(position + down + left, coordinateType),
                BottomRight = ConvertCoordinate(position + down + right, coordinateType)
            };

            if (AllowReorder) {
                // Is there already a sprite for this texture?
                var addNew = false;
                lock (_textureSprites) {
                    if (_textureSprites.ContainsKey(texture)) {
                        // Add the sprite to the last segment for this texture
                        var segment = _textureSprites[texture].Last();
                        AddIn(segment, data);
                    } else {
                        // Add a new segment for this texture
                        addNew = true;
                    }
                }

                if (addNew){
                    AddNew(texture, data);
                }
            } else {
                // Add a new segment for this texture
                AddNew(texture, data);
            }
        }

        private void AddNew(object texture, VerticeSpriteSpecific data) {
            if (texture == null) return;

            lock (_textureSprites) {
                // Create new segment with initial values
                var newSegment = new SpriteSegment {
                    Texture = texture,
                    Sprites = { data }
                };

                _sprites.Add(newSegment);

                // Create reference for segment in dictionary
                if (!_textureSprites.ContainsKey(texture)) {
                    // Thread.Sleep(100);
                    _textureSprites.Add(texture, new List<SpriteSegment>());
                    // Task.Run(() => AcToolsLogging.Write(s));
                }

                _textureSprites[texture].Add(newSegment);
                _spriteCount++;
                CheckForFullBuffer();
            }
        }

        /// <summary>
        /// If the buffer is full, then draw all sprites and clear it.
        /// </summary>
        private void CheckForFullBuffer() {
            if (_spriteCount >= _bufferSize) Flush();
        }

        private void AddIn(SpriteSegment segment, VerticeSpriteSpecific data) {
            segment.Sprites.Add(data);
            _spriteCount++;
            CheckForFullBuffer();
        }

        /// <summary>
        /// This method causes the SpriteRenderer to immediately draw all buffered sprites.
        /// </summary>
        /// <remarks>
        /// This method should be called at the end of a frame in order to draw the last sprites that are in the buffer.
        /// </remarks>
        public void Flush() {
            if (_spriteCount == 0) return;

            if (LockDeviceOnDraw) {
                Monitor.Enter(Device);
            }

            try {
                // Update DepthStencilState if necessary
                DepthStencilState oldDsState = null;
                BlendState oldBlendState = null;

                if (HandleDepthStencilState) {
                    oldDsState = Device.ImmediateContext.OutputMerger.DepthStencilState;
                    Device.ImmediateContext.OutputMerger.DepthStencilState = DepthStencilState;
                }

                if (HandleBlendState) {
                    oldBlendState = Device.ImmediateContext.OutputMerger.BlendState;
                    Device.ImmediateContext.OutputMerger.BlendState = BlendState;
                }

                // Construct vertexbuffer
                UpdateVertexBufferData(_sprites.SelectMany(t => t.Sprites).ToArray());


                // Initialize render calls
                InitRendering();

                // Draw
                var offset = 0;
                foreach (var segment in _sprites) {
                    var count = segment.Sprites.Count;
                    Draw(segment.Texture, count, offset);
                    offset += count;
                }

                if (HandleDepthStencilState) {
                    Device.ImmediateContext.OutputMerger.DepthStencilState = oldDsState;
                }

                if (HandleBlendState) {
                    Device.ImmediateContext.OutputMerger.BlendState = oldBlendState;
                }
            } finally {
                if (LockDeviceOnDraw) {
                    Monitor.Exit(Device);
                }
            }

            // Reset buffers
            _spriteCount = 0;
            _sprites.Clear();

            lock (_textureSprites) {
                _textureSprites.Clear();
            }
        }

        #region IDisposable Support
        private bool _disposed;

        private void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    // There are no managed resources to dispose
                }

                DisposeOfResources();

                // In case there are several SpriteRenderers created
                try {
                    DepthStencilState.Dispose();
                } catch (ObjectDisposedException) { }
                try {
                    BlendState.Dispose();
                } catch (ObjectDisposedException) { }
            }

            _disposed = true;
        }

        /// <summary>
        /// Disposes of the SpriteRenderer.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
