using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct2D;
using SlimDX.Direct3D10;
using SlimDX.DirectWrite;
using SlimDX.DXGI;
using Device1 = SlimDX.Direct3D10_1.Device1;
using Factory = SlimDX.DirectWrite.Factory;
using FactoryType = SlimDX.DirectWrite.FactoryType;
using FontStretch = SlimDX.DirectWrite.FontStretch;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using FontWeight = SlimDX.DirectWrite.FontWeight;
using Resource = SlimDX.DXGI.Resource;
using ShaderResourceView = SlimDX.Direct3D11.ShaderResourceView;

namespace AcTools.Render.Base.Sprites {
    /// <summary>
    /// Defines how a text is aligned in a rectangle. Use OR-combinations of vertical and horizontal alignment.
    /// </summary>
    /// <example>
    /// This example aligns the textblock on the top edge of the rectangle horizontally centered:
    /// <code lang="cs">var textAlignment = TextAlignment.Top | TextAlignment.HorizontalCenter</code>
    /// <code lang="vb">Dim textAlignment = TextAlignment.Top Or TextAlignment.HorizontalCenter</code>
    /// </example>
    [Flags]
    public enum TextAlignment {
        /// <summary>
        /// The top edge of the text is aligned at the top edge of the rectangle.
        /// </summary>
        Top = 1,

        /// <summary>
        /// The vertical center of the text is aligned at the vertical center of the rectangle.
        /// </summary>
        VerticalCenter = 2,

        /// <summary>
        /// The bottom edge of the text is aligned at the bottom edge of the rectangle.
        /// </summary>
        Bottom = 4,

        /// <summary>
        /// The left edge of the text is aligned at the left edge of the rectangle.
        /// </summary>
        Left = 8,

        /// <summary>
        /// The horizontal center of the text is aligned at the horizontal center of the rectangle. Each line is aligned independently.
        /// </summary>
        HorizontalCenter = 16,

        /// <summary>
        /// The right edge of the text is aligned at the right edge of the rectangle. Each line is aligned independently.
        /// </summary>
        Right = 32
    }

    /// <summary>
    /// Holds references to all library-specific devices and factories that are used for rendering text.
    /// </summary>
    public class DeviceDescriptor {
        /// <summary>
        /// The DirectX10 device to use to render fonts. This device is shared across all TextBlockRenderer instances
        /// </summary>
        public IDisposable D3DDevice10;

        /// <summary>
        /// The DirectWrite factory to use. This factory is shared across all TextBlockRenderer instances.
        /// </summary>
        public Factory WriteFactory;

        /// <summary>
        /// The Direct2D factory to use. This factory is shared across all TextBlockRenderer instances.
        /// </summary>
        public IDisposable D2DFactory;

        /// <summary>
        /// Holds the number of active TextBlockRenderer instances
        /// </summary>
        public int ReferenceCount;

        /// <summary>
        /// Disposes of all devices and factories of this description.
        /// </summary>
        public void DisposeAll() {
            D3DDevice10.Dispose();
            WriteFactory.Dispose();
            D2DFactory.Dispose();
        }
    }

    public interface IFontCollectionProvider : IDisposable {
        [NotNull]
        FontCollection GetCollection(Factory factory);
    }

    /// <summary>
    /// This class is responsible for rendering arbitrary text. Every TextRenderer is specialized for a specific font and relies on
    /// a SpriteRenderer for rendering the text.
    /// </summary>
    public class TextBlockRenderer : IDisposable {
        private static readonly Dictionary<Type, DeviceDescriptor> DeviceDescriptors;

        private DeviceDescriptor _desc;
        private static readonly object LockObject;

        protected readonly SpriteRenderer Sprite;
        protected readonly TextFormat Font;

        public readonly float FontSize;
        public readonly int KerningAdjustment;
        public static bool PixCompatible { get; }

        static TextBlockRenderer() {
            PixCompatible = false;
            LockObject = new object();
            DeviceDescriptors = new Dictionary<Type, DeviceDescriptor>();
        }

        private readonly Dictionary<byte, CharTableDescription> _charTables = new Dictionary<byte, CharTableDescription>();
        private readonly RenderTargetProperties _rtp;

        public TextBlockRenderer(SpriteRenderer sprite, string fontName, FontWeight fontWeight,
                FontStyle fontStyle, FontStretch fontStretch, float fontSize, int kerningAdjustment = 0)
                : this(sprite, null, fontName, fontWeight, fontStyle, fontStretch, fontSize, kerningAdjustment) { }

        public TextBlockRenderer(SpriteRenderer sprite, [CanBeNull] IFontCollectionProvider collection, string fontName, FontWeight fontWeight,
                FontStyle fontStyle, FontStretch fontStretch, float fontSize, int kerningAdjustment = 0) {
            AssertDevice();
            IncRefCount();
            Sprite = sprite;
            FontSize = fontSize;
            KerningAdjustment = kerningAdjustment;

            Monitor.Enter(sprite.Device);
            try {
                _rtp = new RenderTargetProperties {
                    HorizontalDpi = 96,
                    VerticalDpi = 96,
                    Type = RenderTargetType.Default,
                    PixelFormat = new PixelFormat(Format.R8G8B8A8_UNorm, AlphaMode.Premultiplied),
                    MinimumFeatureLevel = FeatureLevel.Direct3D10
                };

                if (collection != null) {
                    var c = collection.GetCollection(WriteFactory);
                    if (c.FindFamilyName(fontName) == -1) {
                        fontName = c[0].FamilyNames.get_String(0);
                    }
                    Font = WriteFactory.CreateTextFormat(fontName, c, fontWeight, fontStyle, fontStretch, fontSize,
                            CultureInfo.CurrentCulture.Name);
                } else {
                    Font = WriteFactory.CreateTextFormat(fontName, fontWeight, fontStyle, fontStretch, fontSize, CultureInfo.CurrentCulture.Name);
                }
            } finally {
                Monitor.Exit(sprite.Device);
            }

            CreateCharTable(0);
        }

        protected TextLayout GetTextLayout(string s) {
            return new TextLayout(WriteFactory, s, Font);
        }

        protected IDisposable CreateFontMapTexture(int width, int height, CharRenderCall[] drawCalls) {
            var texDesc = new Texture2DDescription {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = Format.R8G8B8A8_UNorm,
                Height = height,
                Width = width,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.Shared,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            };

            var device10 = (Device1)D3DDevice10;
            var texture = new Texture2D(device10, texDesc);

            var rtv = new RenderTargetView(device10, texture);
            device10.ClearRenderTargetView(rtv, new Color4(0, 1, 1, 1));
            // device10.ClearRenderTargetView(rtv, new Color4(1, 0, 0, 0));
            var surface = texture.AsSurface();
            var target = RenderTarget.FromDXGI((SlimDX.Direct2D.Factory)D2DFactory, surface, _rtp);
            var color = new SolidColorBrush(target, new Color4(1, 1, 1, 1));

            target.BeginDraw();

            foreach (var drawCall in drawCalls) {
                target.DrawTextLayout(drawCall.Position, (TextLayout)drawCall.TextLayout, color);
            }

            target.EndDraw();

            color.Dispose();

            // This is a workaround for Windows 8.1 machines.
            // If these lines would not be present, the shared resource would be empty.
            // TODO: find a nicer solution
            using (var ms = new MemoryStream()) Texture2D.ToStream(texture, ImageFileFormat.Bmp, ms);

            target.Dispose();

            surface.Dispose();
            rtv.Dispose();
            return texture;
        }

        protected void CreateDeviceCompatibleTexture(int width, int height, IDisposable texture10, out IDisposable texture11, out IDisposable srv11) {
            var texture = (Texture2D)texture10;
            var device11 = Sprite.Device;

            lock (device11) {
                var dxgiResource = new Resource(texture);

                SlimDX.Direct3D11.Texture2D tex11;
                if (PixCompatible) {
                    tex11 = new SlimDX.Direct3D11.Texture2D(device11, new SlimDX.Direct3D11.Texture2DDescription {
                        ArraySize = 1,
                        BindFlags = SlimDX.Direct3D11.BindFlags.ShaderResource | SlimDX.Direct3D11.BindFlags.RenderTarget,
                        CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.None,
                        Format = Format.R8G8B8A8_UNorm,
                        Height = height,
                        Width = width,
                        MipLevels = 1,
                        OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.Shared,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = SlimDX.Direct3D11.ResourceUsage.Default
                    });
                } else {
                    tex11 = device11.OpenSharedResource<SlimDX.Direct3D11.Texture2D>(dxgiResource.SharedHandle);
                }
                srv11 = new ShaderResourceView(device11, tex11);
                texture11 = tex11;
                dxgiResource.Dispose();
            }
        }

        protected DeviceDescriptor CreateDevicesAndFactories() {
            return new DeviceDescriptor {
                D3DDevice10 = new Device1(DeviceCreationFlags.BgraSupport, SlimDX.Direct3D10_1.FeatureLevel.Level_10_0),
                WriteFactory = new Factory(FactoryType.Shared),
                D2DFactory = new SlimDX.Direct2D.Factory(SlimDX.Direct2D.FactoryType.SingleThreaded)
            };
        }

        /// <summary>
        /// Creates the texture and necessary structures for 256 chars whose unicode number starts with the given byte.
        /// The table containing ASCII has a prefix of 0 (0x00/00 - 0x00/FF).
        /// </summary>
        /// <param name="bytePrefix">The byte prefix of characters.</param>
        private void CreateCharTable(byte bytePrefix) {
            // Get appropriate texture width height and layout accoring to 'Font' field member
            GenerateTextLayout(bytePrefix, out var sizeX, out var sizeY, out var tl);

            // Get Draw calls and table description
            GenerateDrawCalls(sizeX, sizeY, KerningAdjustment, tl, out var tableDesc, out var drawCalls);

            // Create font map texture from previously created draw calls
            var fontMapTexture = CreateFontMapTexture(sizeX, sizeY, drawCalls);

            // Create a texture to be used by the associated sprite renderer's graphics device from the font map texture.
            CreateDeviceCompatibleTexture(sizeX, sizeY, fontMapTexture, out tableDesc.Texture, out tableDesc.Srv);

            fontMapTexture.Dispose();

            foreach (var layout in tl) {
                layout.Dispose();
            }

            _charTables.Add(bytePrefix, tableDesc);
        }

        private void GenerateTextLayout(byte bytePrefix, out int sizeX, out int sizeY, out TextLayout[] tl) {
            sizeX = (int)(FontSize * 12);
            sizeX = (int)Math.Pow(2, Math.Ceiling(Math.Log(sizeX, 2)));
            // Try how many lines are needed:
            tl = new TextLayout[256];
            int line = 0, xPos = 0, yPos = 0;
            for (var i = 0; i < 256; ++i) {
                tl[i] = GetTextLayout(Convert.ToChar(i + (bytePrefix << 8)).ToString());
                var charWidth = 2 + (int)Math.Ceiling(tl[i].Metrics.LayoutWidth + tl[i].OverhangMetrics.Left + tl[i].OverhangMetrics.Right);
                var charHeight = 2 + (int)Math.Ceiling(tl[i].Metrics.LayoutHeight + tl[i].OverhangMetrics.Top + tl[i].OverhangMetrics.Bottom);
                // TODO: LayoutWidth?
                line = Math.Max(line, charHeight);
                if (xPos + charWidth >= sizeX) {
                    xPos = 0;
                    yPos += line;
                    line = 0;
                }
                xPos += charWidth;
            }

            sizeY = line + yPos;
            sizeY = (int)Math.Pow(2, Math.Ceiling(Math.Log(sizeY, 2)));
        }

        private static void GenerateDrawCalls(int sizeX, int sizeY, int kernelAdjustment, TextLayout[] tl, out CharTableDescription tableDesc,
                out CharRenderCall[] drawCalls) {
            drawCalls = new CharRenderCall[256];
            tableDesc = new CharTableDescription();
            int line = 0, xPos = 0, yPos = 0;
            for (var i = 0; i < 256; ++i) {
                // 1 additional pixel on each side
                var charWidth = 2 + (int)Math.Ceiling(tl[i].Metrics.LayoutWidth + tl[i].OverhangMetrics.Left + tl[i].OverhangMetrics.Right);
                var charHeight = 2 + (int)Math.Ceiling(tl[i].Metrics.LayoutHeight + tl[i].OverhangMetrics.Top + tl[i].OverhangMetrics.Bottom);
                line = Math.Max(line, charHeight);
                if (xPos + charWidth >= sizeX) {
                    xPos = 0;
                    yPos += line;
                    line = 0;
                }
                var charDesc = new CharDescription();

                charDesc.CharSize = new Vector2(tl[i].Metrics.WidthIncludingTrailingWhitespace, tl[i].Metrics.Height);
                charDesc.OverhangLeft = tl[i].OverhangMetrics.Left + 1;
                charDesc.OverhangTop = tl[i].OverhangMetrics.Top + 1;
                // Make XPos + CD.Overhang.Left an integer number in order to draw at integer positions
                charDesc.OverhangLeft += (float)Math.Ceiling(xPos + charDesc.OverhangLeft) - (xPos + charDesc.OverhangLeft);
                // Make YPos + CD.Overhang.Top an integer number in order to draw at integer positions
                charDesc.OverhangTop += (float)Math.Ceiling(yPos + charDesc.OverhangTop) - (yPos + charDesc.OverhangTop);

                charDesc.OverhangRight = charWidth - charDesc.CharSize.X - charDesc.OverhangLeft;
                charDesc.OverhangBottom = charHeight - charDesc.CharSize.Y - charDesc.OverhangTop;

                charDesc.TexCoordsStart = new Vector2((float)xPos / sizeX, (float)yPos / sizeY);
                charDesc.TexCoordsSize = new Vector2((float)charWidth / sizeX, (float)charHeight / sizeY);

                charDesc.TableDescription = tableDesc;

                tableDesc.Chars[i] = charDesc;

                drawCalls[i] = new CharRenderCall {
                    Position = new PointF(xPos + charDesc.OverhangLeft, yPos + charDesc.OverhangTop),
                    TextLayout = tl[i]
                };

                xPos += charWidth;
            }
        }

        public StringMetrics DrawString(string text, Vector2 position, float angle, float realFontSize, Color4 color, CoordinateType coordinateType) {
            IterateStringEm(text, position, angle, true, realFontSize, color, coordinateType, out var sm);
            return sm;
        }

        public StringMetrics DrawString(string text, float angle, Vector2 position, Color4 color) {
            return DrawString(text, position, angle, FontSize, color, CoordinateType.Absolute);
        }

        public StringMetrics MeasureString(string text) {
            IterateString(text, Vector2.Zero, 0f, false, 1, new Color4(), CoordinateType.Absolute, out var sm);
            return sm;
        }

        public StringMetrics MeasureString(string text, float angle, float realFontSize, CoordinateType coordinateType) {
            IterateStringEm(text, Vector2.Zero, angle, false, realFontSize, new Color4(), coordinateType, out var sm);
            return sm;
        }

        public StringMetrics DrawString(string text, Vector2 position, float angle, TextAlignment align, float realFontSize, Color4 color) {
            float left, top, width, height;

            if (align.HasFlag(TextAlignment.HorizontalCenter)) {
                left = -10e3f + position.X;
                width = 20e3f;
            } else if (align.HasFlag(TextAlignment.Right)) {
                left = -20e3f + position.X;
                width = 20e3f;
            } else {
                left = position.X;
                width = 20e3f;
            }

            if (align.HasFlag(TextAlignment.VerticalCenter)) {
                top = -10e3f + position.Y;
                height = 20e3f;
            } else if (align.HasFlag(TextAlignment.Bottom)) {
                top = -20e3f + position.Y;
                height = 20e3f;
            } else {
                top = position.Y;
                height = 20e3f;
            }

            return DrawString(text, new RectangleF(left, top, width, height), angle, align, realFontSize, color, CoordinateType.Absolute);
        }

        // TODO: support for multiline strings plus angle
        public StringMetrics DrawString(string text, RectangleF rect, float angle, TextAlignment align, float realFontSize, Color4 color,
                CoordinateType coordinateType) {
            var hl = align.HasFlag(TextAlignment.Left);
            var vt = align.HasFlag(TextAlignment.Top);
            if (hl && vt) {
                return DrawString(text, new Vector2(rect.X, rect.Y), angle, realFontSize, color, coordinateType);
            }

            var hc = align.HasFlag(TextAlignment.HorizontalCenter);
            var vc = align.HasFlag(TextAlignment.VerticalCenter);

            var m = MeasureString(angle == 0f ? text : "00", angle, realFontSize, coordinateType);
            var p = new Vector2(
                    hl ? rect.X : hc ? rect.X + rect.Width / 2 - m.Size.X / 2 : rect.Right - m.Size.X,
                    vt ? rect.Y : vc ? rect.Y + rect.Height / 2 - m.Size.Y / 2 : rect.Bottom - m.Size.Y);

            if (angle != 0f) {
                var o = m.Size / 2f;
                o.Y /= 2f;
                p += o - SpriteRenderer.Rotate(o, (float)Math.Sin(angle), (float)Math.Cos(angle));
                p -= SpriteRenderer.Rotate(
                        GetOffset(MeasureString(text, 0f, realFontSize, coordinateType))
                                - GetOffset(MeasureString("00", 0f, realFontSize, coordinateType)),
                        (float)Math.Sin(angle), (float)Math.Cos(angle));
            }

            return DrawString(text, p, angle, realFontSize, color, coordinateType);

            Vector2 GetOffset(StringMetrics stringMetrics) {
                var xm = hl ? 0f : hc ? stringMetrics.Size.X / 2 : stringMetrics.Size.X;
                var ym = vt ? 0f : vc ? stringMetrics.Size.Y / 2 : stringMetrics.Size.Y;
                return new Vector2(xm, ym);
            }
        }

        public StringMetrics DrawString(string text, RectangleF rect, float angle, TextAlignment align, Color4 color) {
            return DrawString(text, rect, angle, align, FontSize, color, CoordinateType.Absolute);
        }

        private void IterateStringEm(string text, Vector2 position, float angle, bool draw, float realFontSize, Color4 color, CoordinateType coordinateType,
                out StringMetrics metrics) {
            var scale = realFontSize / FontSize;
            IterateString(text, position, angle, draw, scale, color, coordinateType, out metrics);
        }

        private void IterateString(string text, Vector2 position, float angle, bool draw, float scale, Color4 color, CoordinateType coordinateType,
                out StringMetrics metrics) {
            metrics = new StringMetrics();
            float scalY = coordinateType == CoordinateType.SNorm ? -1 : 1;

            var visualText = NBidi.NBidi.LogicalToVisual(text);
            var codePoints = Helpers.ConvertToCodePointArray(visualText);

            float sine, cosine;
            if (angle != 0f) {
                sine = (float)Math.Sin(angle);
                cosine = (float)Math.Cos(angle);
            } else {
                sine = cosine = 0f;
            }

            var x = 0f;
            var y = Lazier.Create(() => Math.Abs(GetCharDescription('M').CharSize.Y * scale * scalY));

            for (var i = 0; i < codePoints.Length; i++) {
                var c = codePoints[i];
                var charDesc = GetCharDescription(c);
                var charMetrics = charDesc.ToStringMetrics(position, scale, scale * scalY);
                metrics.Merge(charMetrics);

                if (c != '\r' && c != '\n') {
                    if (draw) {
                        var h = charMetrics.FullRectSize.Y;
                        if (h != 0 && charMetrics.FullRectSize.X != 0) {
                            var fix = new Vector2(-charMetrics.OverhangLeft, -scalY * charMetrics.OverhangTop);
                            if (angle != 0f) {
                                fix = SpriteRenderer.Rotate(fix, sine, cosine);
                            }

                            Sprite.Draw(charDesc.TableDescription.Srv, position + fix, charMetrics.FullRectSize,
                                    Vector2.Zero, sine, cosine, charDesc.TexCoordsStart, charDesc.TexCoordsSize, color, coordinateType);
                        }
                    }
                }

                var delta = new Vector2(charMetrics.Size.X + KerningAdjustment, 0f);

                switch (c) {
                    case '\n':
                        delta.Y += y.Value;
                        goto case '\r';
                    case '\r':
                        delta.X = -x;
                        x = 0f;
                        break;
                    default:
                        x += delta.X;
                        break;
                }

                if (angle != 0f) {
                    delta = SpriteRenderer.Rotate(delta, sine, cosine);
                }

                position += delta;
            }
        }

        private CharDescription GetCharDescription(int c) {
            var b = (byte)(c & 0x000000FF);
            var bytePrefix = (byte)((c & 0x0000FF00) >> 8);
            if (!_charTables.ContainsKey(bytePrefix)) CreateCharTable(bytePrefix);
            return _charTables[bytePrefix].Chars[b];
        }

        void AssertDevice() {
            DeviceDescriptor desc;
            lock (LockObject) {
                if (!DeviceDescriptors.TryGetValue(GetType(), out desc)) DeviceDescriptors[GetType()] = desc = CreateDevicesAndFactories();
            }

            _desc = desc;
        }

        private void DecRefCount() {
            if (DeviceDescriptors.TryGetValue(GetType(), out var desc)) desc.ReferenceCount--;
            if (desc != null && desc.ReferenceCount == 0) {
                desc.DisposeAll();
                DeviceDescriptors.Remove(GetType());
            }
        }

        private void IncRefCount() {
            if (DeviceDescriptors.TryGetValue(GetType(), out var desc)) desc.ReferenceCount++;
        }

        protected IDisposable D3DDevice10 => _desc.D3DDevice10;
        protected Factory WriteFactory => _desc.WriteFactory;
        protected IDisposable D2DFactory => _desc.D2DFactory;

        #region IDisposable Support
        private bool _disposed;

        /// <summary>
        /// Disposes of the SpriteRenderer.
        /// </summary>
        public void Dispose() {
            if (!_disposed) {
                // _fcp?.Dispose();
                Font.Dispose();

                foreach (var table in _charTables) {
                    table.Value.Srv.Dispose();
                    table.Value.Texture.Dispose();
                }

                DecRefCount();
                _disposed = true;
            }
        }
        #endregion
    }
}