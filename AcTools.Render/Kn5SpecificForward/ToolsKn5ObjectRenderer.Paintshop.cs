using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using SlimDX.DXGI;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;
using Factory = SlimDX.DirectWrite.Factory;

namespace AcTools.Render.Kn5SpecificForward {
    internal class SourceReady {
        public readonly ShaderResourceView View;
        public readonly Vector4 ChannelsAssignments;

        public SourceReady(ShaderResourceView view, Vector4 channels) {
            View = view;
            ChannelsAssignments = channels;
        }
    }

    internal static class SourceReadyExtension {
        public static void Set([CanBeNull] this SourceReady ready, [NotNull] EffectOnlyResourceVariable resource,
                [NotNull] EffectOnlyVector4Variable channelsAssignments) {
            resource.SetResource(ready?.View);
            channelsAssignments.Set(ready?.ChannelsAssignments ?? default(Vector4));
        }
    }

    public partial class ToolsKn5ObjectRenderer : IPaintShopRenderer {
        public static int OptionPaintShopRandomSize = 512;
        public static int OptionColorSize = 16;
        public static int OptionMaxTintSize = 1024;
        public static int OptionMaxMapSize = 1024;
        public static int OptionMaxPatternSize = 2048;

        // to generate real-time preview, we’re going to use a special shader
        [CanBeNull]
        private Dictionary<string, TargetResourceTexture> _paintShopTextures;

        [CanBeNull]
        private EffectSpecialPaintShop _paintShopEffect;

        private void UseEffect(Action<EffectSpecialPaintShop> fn, TargetResourceTexture tex) {
            if (_paintShopEffect == null) {
                _paintShopEffect = DeviceContextHolder.GetEffect<EffectSpecialPaintShop>();

                var s = Stopwatch.StartNew();
                _paintShopEffect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(OptionPaintShopRandomSize, OptionPaintShopRandomSize));
                AcToolsLogging.Write($"Random texture: {s.Elapsed.TotalMilliseconds:F1} ms");
            }

            using (DeviceContextHolder.SaveRenderTargetAndViewport()) {
                DeviceContextHolder.PrepareQuad(_paintShopEffect.LayoutPT);
                DeviceContext.Rasterizer.SetViewports(tex.Viewport);
                DeviceContext.OutputMerger.SetTargets(tex.TargetView);
                DeviceContext.ClearRenderTargetView(tex.TargetView, new Color4(0f, 0f, 0f, 0f));
                DeviceContext.OutputMerger.BlendState = null;
                DeviceContext.OutputMerger.DepthStencilState = null;
                DeviceContext.Rasterizer.State = null;

                _paintShopEffect.FxNoiseMultipler.Set((float)Math.Max(tex.Width, tex.Height) / OptionPaintShopRandomSize);
                _paintShopEffect.FxSize.Set(new Vector4(tex.Width, tex.Height, 1f / tex.Width, 1f / tex.Height));

                fn?.Invoke(_paintShopEffect);
            }
        }

        private TargetResourceTexture GetTexture([CanBeNull] string textureName, Action<EffectSpecialPaintShop> update, Size size) {
            if (_paintShopTextures == null) {
                _paintShopTextures = new Dictionary<string, TargetResourceTexture>(10);
            }

            TargetResourceTexture tex;
            if (textureName == null) textureName = "";
            if (!_paintShopTextures.TryGetValue(textureName, out tex)) {
                tex = _paintShopTextures[textureName] = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            }

            if (size.Height < 0) size.Height = size.Width;
            tex.Resize(DeviceContextHolder, size.Width, size.Height, null);
            UseEffect(update, tex);
            return tex;
        }

        private bool OverrideTexture(string textureName, Action<EffectSpecialPaintShop> update, Size size) {
            return CarNode == null ||
                    CarNode.OverrideTexture(DeviceContextHolder, textureName, GetTexture(textureName, update, size).View, false);
        }

        private bool OverrideTexture(string textureName, Action<EffectSpecialPaintShop> update, int width, int height = -1) {
            return OverrideTexture(textureName, update, new Size(width, height));
        }

        private async Task SaveTextureAsync(string filename, PreferredDdsFormat format, Action<EffectSpecialPaintShop> update, Size size) {
            using (var s = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, GetTexture(null, update, size).Texture, ImageFileFormat.Tiff, s);
                await Task.Run(() => DdsEncoder.SaveAsDds(filename, s.ToArray(), format, null));
            }
        }

        private Task SaveTextureAsync(string filename, PreferredDdsFormat format, Action<EffectSpecialPaintShop> update, int width, int height = -1) {
            return SaveTextureAsync(filename, format, update, new Size(width, height));
        }

        // get things from PaintShopSource
        [CanBeNull, ContractAnnotation("source: null => null")]
        private byte[] GetBytes([CanBeNull] PaintShopSource source) {
            if (source == null) return null;

            if (!source.UseInput) {
                if (source.Data != null) {
                    return source.Data;
                }

                if (source.Name != null && MainSlot.Kn5?.TexturesData.ContainsKey(source.Name) == true) {
                    return MainSlot.Kn5.TexturesData[source.Name];
                }

                if (source.Color != null) {
                    using (var texture = DeviceContextHolder.CreateTexture(1, 1, (x, y) => source.Color.Value))
                    using (var stream = new MemoryStream()) {
                        Texture2D.ToStream(DeviceContext, texture, ImageFileFormat.Dds, stream);
                        return stream.ToArray();
                    }
                }
            }

            AcToolsLogging.Write("Can’t get bytes: " + source);
            return null;
        }

        // prepare texture using DirectX
        private Dictionary<int, Size> _sizes;

        private Size? GetSize([NotNull] PaintShopSource source) {
            Size result;
            return _sizes.TryGetValue(source.GetHashCode(), out result) ? (Size?)result : null;
        }

        private ShaderResourceView Prepare(ShaderResourceView original, Func<ShaderResourceView, ShaderResourceView> preparation = null) {
            var prepared = preparation?.Invoke(original);
            if (prepared == null || ReferenceEquals(prepared, original)) return original;

            original.Dispose();
            return prepared;
        }

        private static float GetIndex(PaintShopSourceChannel channel) {
            switch (channel) {
                case PaintShopSourceChannel.Red:
                    return 0f;
                case PaintShopSourceChannel.Green:
                    return 1f;
                case PaintShopSourceChannel.Blue:
                    return 2f;
                case PaintShopSourceChannel.Alpha:
                    return 3f;
                case PaintShopSourceChannel.Zero:
                    return -2f;
                case PaintShopSourceChannel.One:
                    return -1f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        private static Vector4 GetChannelAssignments(PaintShopSource source) {
            return new Vector4(GetIndex(source.RedFrom), GetIndex(source.GreenFrom), GetIndex(source.BlueFrom), GetIndex(source.AlphaFrom));
        }

        private Size? Max(Size? baseSize, Size? additionalSize) {
            if (!baseSize.HasValue) return additionalSize;
            return additionalSize.HasValue && additionalSize.Value.Width * additionalSize.Value.Height >
                    baseSize.Value.Width * baseSize.Value.Height ? additionalSize.Value : baseSize;
        }

        [CanBeNull]
        private SourceReady GetOriginal(ref Dictionary<int, ShaderResourceView> storage, [NotNull] PaintShopSource source, int maxSize,
                Func<ShaderResourceView, ShaderResourceView> preparation = null) {
            if (MainSlot.Kn5 == null) return null;

            try {
                if (storage == null) {
                    storage = new Dictionary<int, ShaderResourceView>(2);
                    if (_sizes == null) {
                        _sizes = new Dictionary<int, Size>();
                    }
                }

                ShaderResourceView original;
                var sourceHashCode = source.GetHashCode();
                var hashCode = (sourceHashCode * 397) ^ maxSize.GetHashCode();
                if (!storage.TryGetValue(hashCode, out original)) {
                    Size size;

                    if (source.ByChannels) {
                        var red = source.RedChannelSource == null ? null : GetOriginal(ref storage, source.RedChannelSource, maxSize);
                        var green = source.GreenChannelSource == null ? null : GetOriginal(ref storage, source.GreenChannelSource, maxSize);
                        var blue = source.BlueChannelSource == null ? null : GetOriginal(ref storage, source.BlueChannelSource, maxSize);
                        var alpha = source.AlphaChannelSource == null ? null : GetOriginal(ref storage, source.AlphaChannelSource, maxSize);

                        var redSize = source.RedChannelSource == null ? null : GetSize(source.RedChannelSource);
                        var greenSize = source.GreenChannelSource == null ? null : GetSize(source.GreenChannelSource);
                        var blueSize = source.BlueChannelSource == null ? null : GetSize(source.BlueChannelSource);
                        var alphaSize = source.AlphaChannelSource == null ? null : GetSize(source.AlphaChannelSource);

                        size = Max(redSize, Max(greenSize, Max(blueSize, alphaSize))) ?? new Size(16, 16);
                        _sizes[sourceHashCode] = size;

                        if (size.Width > maxSize || size.Height > maxSize) {
                            size = new Size(maxSize, maxSize);
                        }

                        using (var combined = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                            combined.Resize(DeviceContextHolder, size.Width, size.Height, null);
                            UseEffect(e => {
                                red.Set(e.FxAoMap, e.FxAoMapChannels);
                                green.Set(e.FxInputMap, e.FxInputMapChannels);
                                blue.Set(e.FxMaskMap, e.FxMaskMapChannels);
                                alpha.Set(e.FxOverlayMap, e.FxOverlayMapChannels);
                                e.TechCombineChannels.DrawAllPasses(DeviceContext, 6);
                            }, combined);

                            combined.KeepView = true;
                            original = combined.View;
                        }
                    } else {
                        var decoded = GetBytes(source);
                        if (decoded == null) return null;

                        using (var texture = Texture2D.FromMemory(Device, decoded)) {
                            original = new ShaderResourceView(Device, texture);

                            size = new Size(texture.Description.Width, texture.Description.Height);
                            _sizes[sourceHashCode] = size;

                            if (size.Width > maxSize || size.Height > maxSize) {
                                size = new Size(maxSize, maxSize);

                                using (var resized = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                                    resized.Resize(DeviceContextHolder, maxSize, maxSize, null);
                                    DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder,
                                            original, new Vector2(texture.Description.Width, texture.Description.Height),
                                            resized.TargetView, new Vector2(maxSize, maxSize));
                                    original.Dispose();

                                    resized.KeepView = true;
                                    original = resized.View;
                                }
                            }
                        }
                    }

                    if (source.Desaturate) {
                        original = Prepare(original, view => Desaturate(view, size));
                    }

                    if (source.NormalizeMax) {
                        original = Prepare(original, view => NormalizeMax(view, size));
                    }

                    storage[hashCode] = Prepare(original, preparation);
                }

                return new SourceReady(original, GetChannelAssignments(source));
            } catch (Exception e) {
                AcToolsLogging.NonFatalErrorNotify("Can’t load texture", null, e);
                return null;
            }
        }

        // simple replacement
        private Action<EffectSpecialPaintShop> OverrideAction(SourceReady input) => e => {
            input.Set(e.FxInputMap, e.FxInputMapChannels);
            e.TechReplacement.DrawAllPasses(DeviceContext, 6);
        };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _override;

        public bool OverrideTexture(string textureName, PaintShopSource source) {
            if (source?.Custom != true) {
                return CarNode == null || CarNode.OverrideTexture(DeviceContextHolder, textureName, GetBytes(source));
            }

            if (source.UseInput) source = new PaintShopSource(textureName).SetFrom(source);
            var original = GetOriginal(ref _override, source, OptionMaxMapSize);
            return original != null && OverrideTexture(textureName, OverrideAction(original), OptionMaxMapSize);
        }

        public Task SaveTextureAsync(string filename, PreferredDdsFormat format, PaintShopSource source) {
            if (source.Custom != true) {
                if (source.UseInput) {
                    // we don’t have to save anything here — why waste space and override texture by itself?
                    // source = new PaintShopSource(Path.GetFileName(filename) ?? "").SetFrom(source);
                    return Task.Delay(0);
                }

                var bytes = GetBytes(source);
                return bytes != null ? FileUtils.WriteAllBytesAsync(filename, bytes) : Task.Delay(0);
            }

            if (source.UseInput) source = new PaintShopSource(Path.GetFileName(filename) ?? "").SetFrom(source);
            var original = GetOriginal(ref _override, source, int.MaxValue);
            return original == null ? Task.Delay(0) : SaveTextureAsync(filename, format, OverrideAction(original),
                    GetSize(source) ?? new Size(OptionMaxMapSize, OptionMaxMapSize));
        }

        // solid color with alpha
        private Action<EffectSpecialPaintShop> ColorAction(Color color, double alpha) => e => {
            e.FxColor.Set(Color.FromArgb((byte)(255 * alpha), color).ToVector4());
            e.TechFill.DrawAllPasses(DeviceContext, 6);
        };

        public bool OverrideTexture(string textureName, Color color, double alpha) {
            return OverrideTexture(textureName, ColorAction(color, alpha), 1);
        }

        public Task SaveTextureAsync(string filename, PreferredDdsFormat format, Color color, double alpha) {
            return SaveTextureAsync(filename, format, ColorAction(color, alpha), OptionColorSize);
        }

        // car paint with flakes
        private Action<EffectSpecialPaintShop> FlakesAction(Color color, double flakes) => e => {
            e.FxColor.Set(new Vector4(color.ToVector3(), 1f));
            e.FxFlakes.Set((float)flakes);
            e.TechFlakes.DrawAllPasses(DeviceContext, 6);
        };

        public bool OverrideTextureFlakes(string textureName, Color color, int size, double flakes) {
            return OverrideTexture(textureName, FlakesAction(color, flakes), size);
        }

        public Task SaveTextureFlakesAsync(string filename, PreferredDdsFormat format, Color color, int size, double flakes) {
            return SaveTextureAsync(filename, format, FlakesAction(color, flakes), size);
        }

        // pattern
        private SpriteRenderer _patternSprite;
        private Dictionary<int, TextBlockRenderer> _patternTextRenderers;

        private void InitializePatternTextRenderer() {
            if (_patternSprite == null) {
                _patternSprite = new SpriteRenderer(DeviceContextHolder);
                _patternTextRenderers = new Dictionary<int, TextBlockRenderer>();
            }
        }

        private Dictionary<string, IFontCollectionProvider> _patternFontsCollections = new Dictionary<string, IFontCollectionProvider>();

        private class FontCollectionProvider : IFontCollectionProvider {
            private readonly PaintShopFontSource _source;
            private FontCollection _collection;

            public FontCollectionProvider(PaintShopFontSource source) {
                _source = source;
            }

            public FontCollection GetCollection(Factory factory) {
                if (_collection != null) return _collection;
                _collection = factory.CreateCustomFontCollection(_source.Filename);
                return _collection;
            }

            public void Dispose() {
                try {
                    _collection?.Dispose();
                } catch (ObjectDisposedException e) {
                    AcToolsLogging.Write(e);
                }
            }
        }

        private IFontCollectionProvider GetFontCollectionProvider(PaintShopFontSource fontSource) {
            var filename = fontSource.Filename;
            if (filename == null) return null;

            if (!_patternFontsCollections.TryGetValue(filename, out var result)) {
                _patternFontsCollections[filename] = result = new FontCollectionProvider(fontSource);
            }

            return result;
        }

        private TextBlockRenderer GetPatternTextRenderer([NotNull] PaintShopPatternNumbers description) {
            InitializePatternTextRenderer();

            var hashCode = description.GetFontHashCode();
            if (_patternTextRenderers.TryGetValue(hashCode, out var result)) {
                return result;
            }

            result = new TextBlockRenderer(_patternSprite, GetFontCollectionProvider(description.Font), description.Font.FamilyName,
                    description.Weight, description.Style, description.Stretch, (float)description.Size);
            _patternTextRenderers[hashCode] = result;
            return result;
        }

        private void PatternDrawNumber([NotNull] Color[] c, [NotNull] PaintShopPatternNumbers p, int n, double multiplier) {
            GetPatternTextRenderer(p).DrawString(n.ToInvariantString(),
                    new Vector2((float)(p.Left * multiplier), (float)(p.Top * multiplier)), ((float)p.Angle).ToRadians(),
                    p.GetTextAlignment(), (float)(p.Size * multiplier), p.ColorRef.GetValue(c));
        }

        private Action<EffectSpecialPaintShop> PatternAction(SourceReady patternView, SourceReady aoView, SourceReady overlayView,
                Color[] colors, int? number, IReadOnlyList<PaintShopPatternNumbers> numbers, double multiplier) => e => {
                    patternView.Set(e.FxInputMap, e.FxInputMapChannels);
                    aoView.Set(e.FxAoMap, e.FxAoMapChannels);
                    overlayView.Set(e.FxOverlayMap, e.FxOverlayMapChannels);

                    if (colors.Length > 0) {
                        var vColors = new Vector4[3];
                        for (var i = 0; i < colors.Length; i++) {
                            vColors[i] = colors[i].ToVector4();
                        }

                        e.FxColors.Set(vColors);
                        e.TechColorfulPattern.DrawAllPasses(DeviceContext, 6);
                    } else {
                        e.TechPattern.DrawAllPasses(DeviceContext, 6);
                    }

                    if (numbers.Count > 0 && number.HasValue) {
                        InitializePatternTextRenderer();
                        _patternSprite.RefreshViewport();
                        for (var i = 0; i < numbers.Count; i++) {
                            if (numbers[i] != null) {
                                PatternDrawNumber(colors, numbers[i], number.Value, multiplier);
                            }
                        }
                        _patternSprite.Flush();
                    }
                };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _aoBase, _patternBase, _overlayBase;

        private Size? MaxSize(Size? a, Size? b) {
            return a.HasValue ? b.HasValue ? a.Value.Width * a.Value.Height > b.Value.Width * b.Value.Height ? a : b : a : b;
        }

        private Size GetTexturePatternSize([NotNull] PaintShopSource ao, [NotNull] PaintShopSource pattern, [CanBeNull] PaintShopSource overlay) {
            var patternSize = GetSize(pattern);
            var aoSize = GetSize(ao);
            var overlaySize = overlay == null ? null : GetSize(overlay);
            return MaxSize(MaxSize(patternSize, aoSize), overlaySize) ??
                    new Size(OptionMaxPatternSize, OptionMaxPatternSize);
        }

        private Size GetTexturePatternSizeLimited(Size size, out double multiplier) {
            if (size.Width > OptionMaxPatternSize || size.Height > OptionMaxPatternSize) {
                multiplier = (double)OptionMaxPatternSize / Math.Max(size.Width, size.Height);
                size.Width = (int)(multiplier * size.Width);
                size.Height = (int)(multiplier * size.Height);
            } else {
                multiplier = 1d;
            }
            return size;
        }

        public bool OverrideTexturePattern(string textureName, PaintShopSource ao, PaintShopSource pattern, PaintShopSource overlay,
                Color[] colors, int? number, IReadOnlyList<PaintShopPatternNumbers> numbers, Size? forceSize) {
            if (ao.UseInput) ao = new PaintShopSource(textureName).SetFrom(ao);

            var aoView = GetOriginal(ref _aoBase, ao, OptionMaxPatternSize);
            if (aoView == null) return false;

            var patternView = GetOriginal(ref _patternBase, pattern, OptionMaxPatternSize);
            if (patternView == null) return false;

            var overlayView = overlay == null ? null : GetOriginal(ref _overlayBase, overlay, OptionMaxPatternSize);
            var size = GetTexturePatternSizeLimited(forceSize ?? GetTexturePatternSize(ao, pattern, overlay), out var multiplier);
            return OverrideTexture(textureName,
                    PatternAction(patternView, aoView, overlayView, colors, number, numbers, multiplier),
                    size);
        }

        public Task SaveTexturePatternAsync(string filename, PreferredDdsFormat format, PaintShopSource ao, PaintShopSource pattern, PaintShopSource overlay,
                Color[] colors, int? number, IReadOnlyList<PaintShopPatternNumbers> numbers, Size? forceSize) {
            if (ao.UseInput) ao = new PaintShopSource(Path.GetFileName(filename) ?? "").SetFrom(ao);

            var aoView = GetOriginal(ref _aoBase, ao, int.MaxValue);
            if (aoView == null) return Task.Delay(0);

            var patternView = GetOriginal(ref _patternBase, pattern, int.MaxValue);
            if (patternView == null) return Task.Delay(0);

            var overlayView = overlay == null ? null : GetOriginal(ref _overlayBase, overlay, OptionMaxPatternSize);
            return SaveTextureAsync(filename, format,
                    PatternAction(patternView, aoView, overlayView, colors, number, numbers, 1d),
                    forceSize ?? GetTexturePatternSize(ao, pattern, overlay));
        }

        // txMaps
        private Action<EffectSpecialPaintShop> MapsAction(SourceReady original, SourceReady mask,
                double reflection, double gloss, double specular, bool fixGloss)
                => e => {
                    original.Set(e.FxInputMap, e.FxInputMapChannels);
                    mask.Set(e.FxMaskMap, e.FxMaskMapChannels);
                    e.FxUseMask.Set(mask != null);
                    e.FxColor.Set(new Vector4((float)specular, (float)gloss, (float)reflection, 1f));
                    (fixGloss ? e.TechMapsFillGreen : e.TechMaps).DrawAllPasses(DeviceContext, 6);
                };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _mapsBase, _mapsMasks;

        public bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular, bool fixGloss,
                PaintShopSource source, PaintShopSource maskSource) {
            if (source.UseInput) source = new PaintShopSource(textureName).SetFrom(source);
            var original = GetOriginal(ref _mapsBase, source, OptionMaxMapSize);
            var mask = maskSource == null ? null : GetOriginal(ref _mapsMasks, maskSource, int.MaxValue);
            return original != null && OverrideTexture(textureName,
                    MapsAction(original, mask, reflection, gloss, specular, fixGloss),
                    OptionMaxMapSize);
        }

        public Task SaveTextureMapsAsync(string filename, PreferredDdsFormat format, double reflection, double gloss, double specular, bool fixGloss,
                PaintShopSource source, PaintShopSource maskSource) {
            if (source.UseInput) source = new PaintShopSource(Path.GetFileName(filename) ?? "").SetFrom(source);
            var original = GetOriginal(ref _mapsBase, source, int.MaxValue);
            var mask = maskSource == null ? null : GetOriginal(ref _mapsMasks, maskSource, int.MaxValue);
            var size = GetSize(source) ?? new Size(OptionMaxMapSize, OptionMaxMapSize);

            if (format.IsAuto()) {
                format = Math.Max(size.Width, size.Height) >= 1024 || reflection >= 0.7 && gloss >= 0.7 && specular >= 0.7 ?
                        PreferredDdsFormat.DXT1 : PreferredDdsFormat.NoCompression;
            }

            return original == null ? Task.Delay(0) : SaveTextureAsync(filename, format,
                    MapsAction(original, mask, reflection, gloss, specular, fixGloss), size);
        }

        // tint texture
        private Action<EffectSpecialPaintShop> TintAction(SourceReady original, [CanBeNull] SourceReady mask, [CanBeNull] SourceReady overlay,
                Color[] colors, double alphaAdd) => e => {
                    original.Set(e.FxInputMap, e.FxInputMapChannels);
                    overlay.Set(e.FxOverlayMap, e.FxOverlayMapChannels);

                    e.FxColor.Set(new Vector4(colors[0].R / 255f, colors[0].G / 255f, colors[0].B / 255f, (float)alphaAdd));

                    if (mask != null) {
                        mask.Set(e.FxMaskMap, e.FxMaskMapChannels);

                        var vColors = new Vector4[3];
                        var i = 0;
                        for (; i < colors.Length - 1; i++) {
                            vColors[i] = colors[i + 1].ToVector4();
                        }

                        for (; i < 3; i++) {
                            vColors[i] = new Vector4(1f);
                        }

                        e.FxColors.Set(vColors);
                        e.TechTintMask.DrawAllPasses(DeviceContext, 6);
                    } else {
                        e.TechTint.DrawAllPasses(DeviceContext, 6);
                    }
                };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _tintBase, _tintMask, _tintOverlay;

        public bool OverrideTextureTint(string textureName, Color[] colors, double alphaAdd, PaintShopSource source, PaintShopSource maskSource,
                PaintShopSource overlaySource) {
            if (source.UseInput) source = new PaintShopSource(textureName).SetFrom(source);
            var original = GetOriginal(ref _tintBase, source, OptionMaxTintSize);
            var mask = maskSource == null ? null : GetOriginal(ref _tintMask, maskSource, OptionMaxTintSize);
            var overlay = overlaySource == null ? null : GetOriginal(ref _tintOverlay, overlaySource, OptionMaxTintSize);
            return original != null && OverrideTexture(textureName,
                    TintAction(original, mask, overlay, colors, alphaAdd),
                    OptionMaxTintSize);
        }

        public Task SaveTextureTintAsync(string filename, PreferredDdsFormat format, Color[] colors, double alphaAdd, PaintShopSource source, PaintShopSource maskSource,
                PaintShopSource overlaySource) {
            if (source.UseInput) source = new PaintShopSource(Path.GetFileName(filename) ?? "").SetFrom(source);
            var original = GetOriginal(ref _tintBase, source, int.MaxValue);
            var mask = maskSource == null ? null : GetOriginal(ref _tintMask, maskSource, int.MaxValue);
            var overlay = overlaySource == null ? null : GetOriginal(ref _tintOverlay, overlaySource, int.MaxValue);
            return original == null ? Task.Delay(0) : SaveTextureAsync(filename, format,
                    TintAction(original, mask, overlay, colors, alphaAdd),
                    GetSize(source) ?? new Size(OptionMaxTintSize, OptionMaxTintSize));
        }

        public void SetCurrentSkinActive(bool active) {
            CarNode?.SetCurrentSkinActive(active);
            IsDirty = true;
        }

        // disposal
        private void DisposePaintShop() {
            _patternTextRenderers?.DisposeEverything();
            DisposeHelper.Dispose(ref _patternSprite);

            _paintShopTextures?.DisposeEverything();
            _override?.DisposeEverything();
            _patternBase?.DisposeEverything();
            _patternBase?.DisposeEverything();
            _overlayBase?.DisposeEverything();
            _mapsBase?.DisposeEverything();
            _mapsMasks?.DisposeEverything();
            _tintBase?.DisposeEverything();
            _tintMask?.DisposeEverything();
            _tintOverlay?.DisposeEverything();
        }
    }
}