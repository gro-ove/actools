using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Render.Temporary;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using SlimDX.DXGI;
using Factory = SlimDX.DirectWrite.Factory;

namespace AcTools.Render.Kn5SpecificForward {
    internal class SourceReady : IDisposable {
        public readonly ShaderResourceView View;
        public readonly EffectSpecialPaintShop.ChannelsParams ChannelsAssignments;
        public readonly Size Size;

        private readonly bool _cached;

        public SourceReady(ShaderResourceView view, EffectSpecialPaintShop.ChannelsParams channels, Size size, bool cached) {
            View = view;
            ChannelsAssignments = channels;
            Size = size;
            _cached = cached;
        }

        public void Dispose() {
            if (_cached) return;
            View?.Dispose();
        }
    }

    internal static class SourceReadyExtension {
        public static void Set([CanBeNull] this SourceReady ready, [NotNull] EffectOnlyResourceVariable resource,
                [NotNull] EffectSpecialPaintShop.EffectStructChannelsParamsVariable channelsParams) {
            if (ready == null) {
                resource.SetResource(null);
                channelsParams.Set(default(EffectSpecialPaintShop.ChannelsParams));
                return;
            }

            resource.SetResource(ready.View);
            channelsParams.Set(ready.ChannelsAssignments);
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
        private Dictionary<string, TargetResourceTexture> _paintShopTextures,
                _paintShopMaskedTextures;

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

        public delegate void Step(EffectSpecialPaintShop effect, IReadOnlyList<ShaderResourceView> previousSteps);

        [NotNull]
        private TargetResourceTexture GetTextureStep([CanBeNull] string textureName, bool hdrMode, int stepId, Step update,
                IReadOnlyList<ShaderResourceView> previousSteps, Size size) {
            if (_paintShopTextures == null) {
                _paintShopTextures = new Dictionary<string, TargetResourceTexture>(10);
            }

            var key = $"{textureName ?? "*"}:{(hdrMode ? "hdr" : "plain")}:{stepId}";
            if (!_paintShopTextures.TryGetValue(key, out var tex)) {
                tex = _paintShopTextures[key] = TargetResourceTexture.Create(hdrMode ? Format.R32G32B32A32_Float : Format.R8G8B8A8_UNorm);
            }

            if (size.Height < 0) size.Height = size.Width;
            tex.Resize(DeviceContextHolder, size.Width, size.Height, null);
            UseEffect(e => update(e, previousSteps), tex);
            return tex;
        }

        [NotNull]
        private TargetResourceTexture GetTexture([CanBeNull] string textureName, bool hdrMode, IReadOnlyList<Step> update, Size size) {
            if (update.Count == 1) {
                return GetTextureStep(textureName, hdrMode, 0, update[0], new ShaderResourceView[0], size);
            }

            var previous = new List<ShaderResourceView>(update.Count - 1);
            for (var i = 0; i < update.Count; i++) {
                var step = update[i];
                var tex = GetTextureStep(textureName, hdrMode, i, step, previous, size);
                if (i == update.Count - 1) return tex;
                previous.Add(tex.View);
            }

            throw new ArgumentException("Value cannot be an empty collection.", nameof(update));
        }

        [CanBeNull]
        private Dictionary<string, TargetResourceTexture> _paintShopIndependantFix;

        [CanBeNull]
        private TargetResourceTexture GetIndependant(Size size, int independantLayer) {
            if (size == default(Size)) return null;

            if (_paintShopIndependantFix == null) {
                _paintShopIndependantFix = new Dictionary<string, TargetResourceTexture>();
            }

            var key = $"{size}:{independantLayer}";
            if (!_paintShopIndependantFix.TryGetValue(key, out var t)) {
                t = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                t.Resize(DeviceContextHolder, size.Width, size.Height, null);
                _paintShopIndependantFix[key] = t;
            }

            return t;
        }

        [CanBeNull]
        private TargetResourceTexture CopyToIndependant([CanBeNull] ShaderResourceView b, Size size, int independantLayer) {
            if (b == null || size == default(Size)) return null;

            var t = GetIndependant(size, independantLayer);
            if (t == null) return null;

            DeviceContext.Rasterizer.SetViewports(t.Viewport);
            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, b, t.TargetView);
            return t;
        }

        [NotNull]
        private TargetResourceTexture MaskTexture([NotNull] PaintShopDestination destination,
                [NotNull] TargetResourceTexture source, [CanBeNull] PaintShopSource mask, [CanBeNull] IPaintShopObject obj) {
            var underlay = obj?.GetTexture(DeviceContextHolder, destination.TextureName)?.Resource;
            var underlaySize = underlay == null ? null : GetSize(underlay);
            var underlayClone = CopyToIndependant(underlay, underlaySize ?? default(Size), 0);
            var size = new Size(Math.Max(underlaySize?.Width ?? 1, source.Width), Math.Max(underlaySize?.Height ?? 1, source.Height));

            using (var maskView = mask == null ? null : mask.UseInput ? GetOriginal(new PaintShopSource(destination.TextureName) {
                DoNotCache = true
            }.SetFrom(mask), Math.Max(size.Width, size.Height)) : GetOriginal(mask, Math.Max(size.Width, size.Height))) {
                if (_paintShopMaskedTextures == null) {
                    _paintShopMaskedTextures = new Dictionary<string, TargetResourceTexture>(10);
                }

                var key = $"{destination.TextureName}:{(destination.PreferredFormat.IsHdr() ? "hdr" : "plain")}";
                if (!_paintShopMaskedTextures.TryGetValue(key, out var tex)) {
                    tex = _paintShopMaskedTextures[key] =
                            TargetResourceTexture.Create(destination.PreferredFormat.IsHdr() ? Format.R32G32B32A32_Float : Format.R8G8B8A8_UNorm);
                }

                tex.Resize(DeviceContextHolder, size.Width, size.Height, null);
                UseEffect(e => {
                    e.FxInputMap.SetResource(source.View);
                    e.FxInputParams.Set(new EffectSpecialPaintShop.ChannelsParams {
                        Map = GetChannelsAssignments(destination),
                        Add = GetChannelsAdd(destination),
                        Multiply = GetChannelsMultiply(destination),
                    });
                    e.FxUnderlayMap.SetResource(underlayClone?.View);
                    e.FxUseMask.Set(maskView != null);
                    maskView.Set(e.FxMaskMap, e.FxMaskParams);
                    e.TechMaskThreshold.DrawAllPasses(DeviceContext, 6);
                }, tex);

                return tex;
            }
        }

        private static Vector4 GetChannelsAssignments(PaintShopDestination source) {
            return new Vector4(GetIndex(source.RedFrom), GetIndex(source.GreenFrom), GetIndex(source.BlueFrom), GetIndex(source.AlphaFrom));
        }

        private static Vector4 GetChannelsAdd(PaintShopDestination source) {
            return new Vector4(source.RedAdjustment.Add, source.GreenAdjustment.Add, source.BlueAdjustment.Add, source.AlphaAdjustment.Add);
        }

        private static Vector4 GetChannelsMultiply(PaintShopDestination source) {
            return new Vector4(source.RedAdjustment.Multiply, source.GreenAdjustment.Multiply, source.BlueAdjustment.Multiply, source.AlphaAdjustment.Multiply);
        }

        private static Vector4 GetChannelsAssignments(PaintShopSource source) {
            return new Vector4(GetIndex(source.RedFrom), GetIndex(source.GreenFrom), GetIndex(source.BlueFrom), GetIndex(source.AlphaFrom));
        }

        private static Vector4 GetChannelsAdd(PaintShopSource source) {
            return new Vector4(source.RedAdjustment.Add, source.GreenAdjustment.Add, source.BlueAdjustment.Add, source.AlphaAdjustment.Add);
        }

        private static Vector4 GetChannelsMultiply(PaintShopSource source) {
            return new Vector4(source.RedAdjustment.Multiply, source.GreenAdjustment.Multiply, source.BlueAdjustment.Multiply, source.AlphaAdjustment.Multiply);
        }

        private bool OverrideTexture([NotNull] PaintShopDestination destination, [NotNull] IReadOnlyList<Step> update, Size size) {
            var car = CarNode;
            if (car == null) return false;

            var tx = GetTexture(destination.TextureName, destination.PreferredFormat.IsHdr(), update, size);
            if (destination.OutputMask != null || destination.AnyChannelAdjusted || destination.AnyChannelMapped) {
                tx = MaskTexture(destination, tx, destination.OutputMask, car);
            }

            return car.OverrideTexture(DeviceContextHolder, destination.TextureName, tx.View, false);
        }

        private async Task SaveTextureAsync([NotNull] string location, [NotNull] PaintShopDestination destination, [NotNull] IReadOnlyList<Step> update,
                Size size) {
            var tx = GetTexture(null, destination.PreferredFormat.IsHdr(), update, size);
            if (destination.OutputMask != null || destination.AnyChannelAdjusted || destination.AnyChannelMapped) {
                tx = MaskTexture(destination, tx, destination.OutputMask, CarNode);
            }

            using (var s = new MemoryStream()) {
                var format = destination.PreferredFormat;

                if (format.IsHdr()) {
                    if (format != PreferredDdsFormat.AutoHDR || IsHdrTexture(tx.View, size)) {
                        Texture2D.ToStream(DeviceContext, tx.Texture, ImageFileFormat.Dds, s);
                        await FileUtils.WriteAllBytesAsync(Path.Combine(location, destination.TextureName), s.ToArray());
                        return;
                    }

                    tx = GetTexture(null, false, update, size);
                    format = PreferredDdsFormat.AutoTransparency;
                }

                Texture2D.ToStream(DeviceContext, tx.Texture, ImageFileFormat.Tiff, s);
                await Task.Run(() => DdsEncoder.SaveAsDds(Path.Combine(location, destination.TextureName), s.ToArray(),
                        format, null));
            }
        }

        // get things from PaintShopSource
        [CanBeNull, ContractAnnotation("source: null => null")]
        private static string GetCacheKey([CanBeNull] PaintShopSource source, int maxSize) {
            if (source == null || source.DoNotCache) return null;

            if (!source.UseInput) {
                if (source.Data != null) return "data:" + source.Data.GetHashCode() + ":" + maxSize;
                if (source.Name != null) return "kn5:" + source.Name + ":" + maxSize;
            }

            return null;
        }

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
                    using (var texture = DeviceContextHolder.CreateTexture(4, 4, (x, y) => source.Color.Value))
                    using (var stream = new MemoryStream()) {
                        Texture2D.ToStream(DeviceContext, texture, ImageFileFormat.Png, stream);
                        return stream.ToArray();
                    }
                }

                if (source.ColorRef != null) {
                    using (var texture = DeviceContextHolder.CreateTexture(4, 4, (x, y) => source.ColorRef.GetValue() ?? Color.Black))
                    using (var stream = new MemoryStream()) {
                        Texture2D.ToStream(DeviceContext, texture, ImageFileFormat.Png, stream);
                        return stream.ToArray();
                    }
                }
            }

            AcToolsLogging.Write("Can’t get bytes: " + source);
            return null;
        }

        private Dictionary<string, Tuple<ShaderResourceView, Size>> _paintShopCache;

        private static Size? GetSize([NotNull] ResourceView view) {
            var d = (view.Resource as Texture2D)?.Description;
            if (d == null) return null;
            return new Size(d.Value.Width, d.Value.Height);
        }

        [CanBeNull]
        private ShaderResourceView GetShaderResourceView([CanBeNull] PaintShopSource source, int maxSize, out Size size, out bool cached) {
            if (source == null) {
                size = default(Size);
                cached = false;
                return null;
            }

            if (source.TextureRef != null) {
                var view = source.TextureRef.GetValue(DeviceContextHolder, CarNode);
                if (view == null) {
                    AcToolsLogging.Write("Failed to find: " + source.TextureRef);
                    size = default(Size);
                    cached = false;
                    return null;
                }

                // Set cached to true to avoid view being disposed afterwards
                cached = true;

                size = GetSize(view) ?? new Size(32, 32);
                return view;
            }

            var key = GetCacheKey(source, maxSize);
            if (key != null) {
                if (_paintShopCache == null) {
                    _paintShopCache = new Dictionary<string, Tuple<ShaderResourceView, Size>>();
                }

                if (_paintShopCache.TryGetValue(key, out var value)) {
                    size = value.Item2;
                    cached = true;
                    return value.Item1;
                }
            }

            var decoded = GetBytes(source);
            if (decoded == null) {
                if (key != null) {
                    _paintShopCache[key] = Tuple.Create((ShaderResourceView)null, default(Size));
                }

                size = default(Size);
                cached = key != null;
                return null;
            }

            ShaderResourceView original;
            using (var texture = Texture2D.FromMemory(Device, decoded)) {
                original = new ShaderResourceView(Device, texture);
                size = new Size(texture.Description.Width, texture.Description.Height);

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

            if (key != null) {
                _paintShopCache[key] = Tuple.Create(original, size);
            }

            cached = key != null;
            return original;
        }

        // prepare texture using DirectX
        [CanBeNull]
        private static ShaderResourceView Prepare([CanBeNull] ShaderResourceView original, [CanBeNull] Func<ShaderResourceView, ShaderResourceView> preparation,
                ref bool originalCached) {
            if (original == null) return null;

            var prepared = preparation?.Invoke(original);
            if (prepared == null || ReferenceEquals(prepared, original)) return original;

            if (!originalCached) {
                original.Dispose();
            } else {
                originalCached = false;
            }

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
                    // TODO: Register error properly!
                    AcToolsLogging.Write("Invalid value for channel: " + channel);
                    return -2f;
            }
        }

        private static readonly EffectSpecialPaintShop.ChannelsParams DefaultChannelParams = new EffectSpecialPaintShop.ChannelsParams {
            Map = new Vector4(0, 1, 2, 3),
            Add = Vector4.Zero,
            Multiply = new Vector4(1)
        };

        private EffectSpecialPaintShop.ChannelsParams GetChannelsParams(PaintShopSource source) {
            return new EffectSpecialPaintShop.ChannelsParams {
                Map = GetChannelsAssignments(source),
                Add = GetChannelsAdd(source),
                Multiply = GetChannelsMultiply(source),
            };
        }

        private Size? Max(Size? baseSize, Size? additionalSize) {
            if (!baseSize.HasValue) return additionalSize;
            return additionalSize.HasValue && additionalSize.Value.Width * additionalSize.Value.Height >
                    baseSize.Value.Width * baseSize.Value.Height ? additionalSize.Value : baseSize;
        }

        [CanBeNull]
        private SourceReady GetOriginal([NotNull] PaintShopSource source, int maxSize,
                Func<ShaderResourceView, ShaderResourceView> preparation = null) {
            if (MainSlot.Kn5 == null) return null;

            try {
                ShaderResourceView original;
                Size size;
                bool cached;

                if (source.DefinedByChannels) {
                    using (var red = source.RedChannelSource == null ? null : GetOriginal(source.RedChannelSource, maxSize))
                    using (var green = source.GreenChannelSource == null ? null : GetOriginal(source.GreenChannelSource, maxSize))
                    using (var blue = source.BlueChannelSource == null ? null : GetOriginal(source.BlueChannelSource, maxSize))
                    using (var alpha = source.AlphaChannelSource == null ? null : GetOriginal(source.AlphaChannelSource, maxSize)) {
                        size = Max(red?.Size, Max(green?.Size, Max(blue?.Size, alpha?.Size))) ?? new Size(16, 16);

                        if (size.Width > maxSize || size.Height > maxSize) {
                            size = new Size(maxSize, maxSize);
                        }

                        using (var combined = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                            combined.Resize(DeviceContextHolder, size.Width, size.Height, null);
                            UseEffect(e => {
                                red.Set(e.FxAoMap, e.FxAoParams);
                                green.Set(e.FxInputMap, e.FxInputParams);
                                blue.Set(e.FxMaskMap, e.FxMaskParams);
                                alpha.Set(e.FxOverlayMap, e.FxOverlayParams);
                                e.TechCombineChannels.DrawAllPasses(DeviceContext, 6);
                            }, combined);

                            combined.KeepView = true;
                            original = combined.View;
                        }
                    }

                    cached = false;
                } else {
                    original = GetShaderResourceView(source, maxSize, out size, out cached);
                }

                if (source.DesaturateMax) {
                    original = Prepare(original, view => DesaturateMax(view, size), ref cached);
                }

                if (source.Desaturate) {
                    original = Prepare(original, view => Desaturate(view, size), ref cached);
                }

                if (source.NormalizeMax) {
                    original = Prepare(original, view => NormalizeMax(view, size), ref cached);
                }

                original = Prepare(original, preparation, ref cached);
                return new SourceReady(original, GetChannelsParams(source), size, cached);
            } catch (Exception e) {
                AcToolsLogging.NonFatalErrorNotify("Can’t load texture", null, e);
                return null;
            }
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

        private TextBlockRenderer GetPatternTextRenderer([NotNull] PaintShopPatternNumber description) {
            InitializePatternTextRenderer();

            var hashCode = description.GetFontHashCode();
            if (_patternTextRenderers.TryGetValue(hashCode, out var result)) {
                return result;
            }

            result = new TextBlockRenderer(_patternSprite, GetFontCollectionProvider(description.Font), description.Font.FamilyName,
                    description.Weight, description.Style, description.Stretch, (float)description.Size, description.KerningAdjustment);
            _patternTextRenderers[hashCode] = result;
            return result;
        }

        private void PatternDrawText([CanBeNull] Color[] c, [NotNull] PaintShopPatternNumber p, string s, double multiplier) {
            GetPatternTextRenderer(p).DrawString(s,
                    new Vector2((float)(p.Left * multiplier), (float)(p.Top * multiplier)), ((float)p.Angle).ToRadians(),
                    p.GetTextAlignment(), (float)(p.Size * multiplier), p.ColorRef.GetValue(c));
        }

        private Dictionary<string, SourceReady> _paintShopFlags = new Dictionary<string, SourceReady>();

        private SourceReady GetFlagTexture([NotNull] string filename) {
            if (!_paintShopFlags.TryGetValue(filename, out var ready)) {
                var view = ShaderResourceView.FromFile(Device, filename);
                _paintShopFlags[filename] = ready = new SourceReady(view, DefaultChannelParams, GetSize(view) ?? new Size(100, 100), true);
            }

            return ready;
        }

        private void PatternDrawDecal(PaintShopPatternFlag p, [CanBeNull] SourceReady source, double multiplier, Size size, Color color,
                EffectSpecialPaintShop e) {
            if (source == null) {
                AcToolsLogging.Write("Source not found: " + p);
                return;
            }

            var textureSize = source.Size;
            var pos = new Vector2((float)(p.Left * multiplier), (float)(p.Top * multiplier));
            var width = (float)(p.Size * multiplier);
            var aspect = (float)p.AspectMultiplier * textureSize.Height / textureSize.Width;
            var height = width;
            var angle = ((float)p.Angle).ToRadians();
            var scale = new Vector2(size.Width / width, size.Height / height);
            var translate = new Vector2(scale.X * (-pos.X / size.Width + 0.5f), scale.Y * (-pos.Y / size.Height + 0.5f));
            e.FxTransform.SetMatrix(Matrix.Transformation2D(new Vector2(0.5f), 0f, scale, new Vector2(0.5f), 0f, translate) *
                    Matrix.AffineTransformation2D(1f, new Vector2(0.5f, 0.5f), angle, Vector2.Zero) *
                    Matrix.Transformation2D(new Vector2(0.5f), 0f, new Vector2(1f, 1f / aspect), Vector2.Zero, 0f, Vector2.Zero));
            e.FxColor.Set(new Color4(color));
            source.Set(e.FxInputMap, e.FxInputParams);
            e.TechPiece.DrawAllPasses(DeviceContext, 6);
        }

        private void PatternDrawFlag(PaintShopPatternFlag p, [NotNull] string f, double multiplier, Size size, EffectSpecialPaintShop e) {
            PatternDrawDecal(p, GetFlagTexture(f), multiplier, size, Color.White, e);
        }

        private class PaintShopActionPreparation : IDisposable {
            [NotNull]
            private readonly PaintShopOverrideBase _value;

            [NotNull]
            private readonly ToolsKn5ObjectRenderer _parent;

            [NotNull]
            private readonly List<IDisposable> _disposeLater = new List<IDisposable>();

            private PreferredDdsFormat _format;
            private Size? _size;
            private int _maxPreviewSize = 1024;

            public PaintShopActionPreparation([NotNull] PaintShopOverrideBase value, [NotNull] ToolsKn5ObjectRenderer parent) {
                _value = value;
                _parent = parent;
                _format = value.Destination.PreferredFormat;
                _size = value.Destination.ForceSize;
            }

            [CanBeNull]
            private SourceReady Get(PaintShopSource source) {
                if (source == null) return null;

                SourceReady result;
                if (source.UseInput) {
                    result = _parent.GetOriginal(new PaintShopSource(_value.Destination.TextureName) {
                        DoNotCache = true
                    }.SetFrom(source), _maxPreviewSize);
                } else {
                    result = _parent.GetOriginal(source, _maxPreviewSize);
                }

                if (result == null) return null;
                if (!_size.HasValue) {
                    _size = result.Size;
                }

                _disposeLater.Add(result);
                return result;
            }

            [ContractAnnotation("a:null, b:null => null; a:notnull => notnull; b:notnull => notnull")]
            private static Size? MaxSize(Size? a, Size? b) {
                return a.HasValue ? b.HasValue ? a.Value.Width * a.Value.Height > b.Value.Width * b.Value.Height ? a : b : a : b;
            }

            /*private static Size? MaxSize([ItemCanBeNull] params Size?[] sizes) {
                if (sizes.Length == 0) return null;
                var result = sizes[0];
                for (var i = 1; i < sizes.Length; i++) {
                    result = MaxSize(result, sizes[i]);
                }

                return result;
            }*/

            private static Size? MaxSize([ItemCanBeNull] params SourceReady[] sizes) {
                if (sizes.Length == 0) return null;
                var result = sizes[0]?.Size;
                for (var i = 1; i < sizes.Length; i++) {
                    result = MaxSize(result, sizes[i]?.Size);
                }

                return result;
            }

            private static Size GetTexturePatternSizeLimited(Size size, out double multiplier) {
                if (size.Width > OptionMaxPatternSize || size.Height > OptionMaxPatternSize) {
                    multiplier = (double)OptionMaxPatternSize / Math.Max(size.Width, size.Height);
                    size.Width = (int)(multiplier * size.Width);
                    size.Height = (int)(multiplier * size.Height);
                } else {
                    multiplier = 1d;
                }
                return size;
            }

            public PaintShopAction GetAction() {
                try {
                    Step preparation = null, draw;

                    switch (_value) {
                        case PaintShopOverrideWithTexture p: {
                            // TODO: On save, if source is specified as a byte[], simply copy?
                            var original = Get(p.Source);
                            draw = (e, previous) => {
                                original.Set(e.FxInputMap, e.FxInputParams);
                                e.TechReplacement.DrawAllPasses(_parent.DeviceContext, 6);
                            };
                            break;
                        }

                        case PaintShopOverrideWithColor p: {
                            _maxPreviewSize = p.Size;
                            draw = (e, previous) => {
                                e.FxColor.Set(p.Color.ToVector4());
                                if (p.Flakes > 0d) {
                                    e.FxFlakes.Set((float)p.Flakes);
                                    e.TechFlakes.DrawAllPasses(_parent.DeviceContext, 6);
                                } else {
                                    e.TechFill.DrawAllPasses(_parent.DeviceContext, 6);
                                }
                            };
                            break;
                        }

                        case PaintShopOverridePattern p: {
                            _maxPreviewSize = OptionMaxPatternSize;

                            var ao = Get(p.Ao);
                            var pattern = Get(p.Pattern);
                            var overlay = Get(p.Overlay);
                            var underlay = Get(p.Underlay);

                            var size = GetTexturePatternSizeLimited(p.Destination.ForceSize ?? MaxSize(ao, pattern, overlay, underlay) ??
                                    new Size(_maxPreviewSize, _maxPreviewSize), out var multiplier);
                            _size = size;

                            if (p.Flags?.Count > 0 && p.SkinFlagFilename != null ||
                                    p.Numbers?.Count > 0 && p.SkinNumber.HasValue ||
                                    p.Labels?.Count > 0) {
                                preparation = (e, previous) => {
                                    var colors = p.Colors;

                                    var flags = p.Flags;
                                    if (flags?.Count > 0 && p.SkinFlagFilename != null) {
                                        _parent.DeviceContext.OutputMerger.BlendState = _parent.DeviceContextHolder.States.TransparentBlendState;
                                        for (var i = 0; i < flags.Count; i++) {
                                            if (flags[i] != null) {
                                                _parent.PatternDrawFlag(flags[i], p.SkinFlagFilename, multiplier, size, e);
                                            }
                                        }
                                        _parent.DeviceContext.OutputMerger.BlendState = null;
                                    }

                                    var decals = p.Decals;
                                    if (decals?.Count > 0) {
                                        _parent.DeviceContext.OutputMerger.BlendState = _parent.DeviceContextHolder.States.TransparentBlendState;
                                        for (var i = 0; i < decals.Count; i++) {
                                            var d = decals[i];
                                            if (d != null) {
                                                _parent.PatternDrawDecal(d, Get(d.Source), multiplier, size, d.ColorRef.GetValue(colors), e);
                                            }
                                        }
                                        _parent.DeviceContext.OutputMerger.BlendState = null;
                                    }

                                    var patternTextRendererInitialized = false;

                                    void EnsurePatternTextRendererInitialized() {
                                        if (!patternTextRendererInitialized) {
                                            patternTextRendererInitialized = true;
                                            _parent.InitializePatternTextRenderer();
                                            _parent._patternSprite.RefreshViewport();
                                        }
                                    }

                                    var numbers = p.Numbers;
                                    if (numbers != null && p.SkinNumber.HasValue) {
                                        for (var i = 0; i < numbers.Count; i++) {
                                            if (numbers[i] != null) {
                                                EnsurePatternTextRendererInitialized();
                                                _parent.PatternDrawText(colors, numbers[i], p.SkinNumber.Value.ToInvariantString(), multiplier);
                                            }
                                        }
                                    }

                                    var labels = p.Labels;
                                    if (labels != null && p.SkinLabels != null) {
                                        for (var i = 0; i < labels.Count; i++) {
                                            if (labels[i] != null && p.SkinLabels.TryGetValue(labels[i].Role, out var value) &&
                                                    !string.IsNullOrWhiteSpace(value)) {
                                                EnsurePatternTextRendererInitialized();
                                                _parent.PatternDrawText(colors, labels[i], value, multiplier);
                                            }
                                        }
                                    }

                                    if (patternTextRendererInitialized) {
                                        _parent._patternSprite.Flush();
                                    }
                                };
                            }

                            draw = (e, previous) => {
                                pattern.Set(e.FxInputMap, e.FxInputParams);
                                ao.Set(e.FxAoMap, e.FxAoParams);
                                overlay.Set(e.FxOverlayMap, e.FxOverlayParams);
                                underlay.Set(e.FxUnderlayMap, e.FxUnderlayParams);
                                e.FxDetailsMap.SetResource(previous.FirstOrDefault());

                                var colors = p.Colors;
                                if (colors?.Length > 0) {
                                    var vColors = new Vector4[3];
                                    for (var i = 0; i < colors.Length; i++) {
                                        vColors[i] = colors[i].ToVector4();
                                    }

                                    AcToolsLogging.Write(p.BackgroundColorHint);
                                    e.FxColor.Set(p.BackgroundColorHint);
                                    e.FxColors.Set(vColors);
                                    e.TechColorfulPattern.DrawAllPasses(_parent.DeviceContext, 6);
                                } else {
                                    e.TechPattern.DrawAllPasses(_parent.DeviceContext, 6);
                                }
                            };

                            break;
                        }

                        case PaintShopOverrideMaps p: {
                            _maxPreviewSize = OptionMaxMapSize;
                            var original = Get(p.Source);
                            var mask = Get(p.Mask);
                            if (_format.IsAuto()) {
                                _format = Math.Max(_size?.Width ?? 0, _size?.Height ?? 0) >= 1024 ||
                                        p.Reflection.CloseToFullRange() && p.Gloss.CloseToFullRange() && p.Specular.CloseToFullRange() ?
                                        PreferredDdsFormat.DXT1 : PreferredDdsFormat.NoCompression;
                            }

                            draw = (e, previous) => {
                                original.Set(e.FxInputMap, e.FxInputParams);
                                mask.Set(e.FxMaskMap, e.FxMaskParams);
                                e.FxUseMask.Set(mask != null);
                                e.FxColors.Set(new[] {
                                    new Vector4(p.Specular.Add, p.Gloss.Add, p.Reflection.Add, 0f),
                                    new Vector4(p.Specular.Multiply, p.Gloss.Multiply, p.Reflection.Multiply, 0f)
                                });

                                e.TechMaps.DrawAllPasses(_parent.DeviceContext, 6);
                            };
                            break;
                        }

                        case PaintShopOverrideTint p: {
                            _maxPreviewSize = OptionMaxTintSize;
                            var original = Get(p.Source);
                            var mask = Get(p.Mask);
                            var overlay = Get(p.Overlay);
                            draw = (e, previous) => {
                                original.Set(e.FxInputMap, e.FxInputParams);
                                overlay.Set(e.FxOverlayMap, e.FxOverlayParams);
                                e.FxAlphaAdjustments.Set(new Vector2(p.Alpha.Add, p.Alpha.Multiply));

                                var colors = p.Colors;
                                e.FxColor.Set(colors != null ?
                                        new Vector4(colors[0].R / 255f, colors[0].G / 255f, colors[0].B / 255f, 0f) :
                                        Vector4.Zero);

                                if (mask != null) {
                                    mask.Set(e.FxMaskMap, e.FxMaskParams);

                                    var vColors = new Vector4[3];
                                    var i = 0;
                                    if (colors != null) {
                                        for (; i < colors.Length - 1; i++) {
                                            vColors[i] = colors[i + 1].ToVector4();
                                        }
                                    }

                                    for (; i < 3; i++) {
                                        vColors[i] = new Vector4(1f);
                                    }

                                    e.FxColors.Set(vColors);
                                    e.TechTintMask.DrawAllPasses(_parent.DeviceContext, 6);
                                } else {
                                    e.TechTint.DrawAllPasses(_parent.DeviceContext, 6);
                                }
                            };
                            break;
                        }

                        default:
                            throw new NotSupportedException($"Not supported: {_value.GetType().Name}");
                    }

                    return new PaintShopAction(
                            new PaintShopDestination(_value.Destination.TextureName, _format, _value.Destination.OutputMask,
                                    _value.Destination.ForceSize).InheritExtendedPropertiesFrom(_value.Destination),
                            preparation == null ? new[] { draw } : new[] { preparation, draw },
                            _size ?? new Size(_maxPreviewSize, _maxPreviewSize), _maxPreviewSize);
                } catch (Exception) {
                    _disposeLater.DisposeEverything();
                    throw;
                }
            }

            public void Dispose() {
                _disposeLater.DisposeEverything();
            }
        }

        private class PaintShopAction {
            [NotNull]
            public readonly PaintShopDestination Destination;

            [NotNull]
            public readonly IReadOnlyList<Step> DrawAction;

            public readonly Size Size;
            public readonly int? MaxPreviewSize;

            public PaintShopAction([NotNull] PaintShopDestination destination,
                    [NotNull] Step drawAction, Size size, int? maxPreviewSize = null) {
                Destination = destination;
                DrawAction = new[]{ drawAction };
                Size = size;
                MaxPreviewSize = maxPreviewSize;
            }

            public PaintShopAction([NotNull] PaintShopDestination destination,
                    [NotNull] IReadOnlyList<Step> drawAction, Size size, int? maxPreviewSize = null) {
                Destination = destination;
                DrawAction = drawAction;
                Size = size;
                MaxPreviewSize = maxPreviewSize;
            }
        }

        public bool Reset(string textureName) {
            return CarNode == null ||
                    CarNode.OverrideTexture(DeviceContextHolder, textureName, null, false);
        }

        public bool Override(PaintShopOverrideBase value) {
            using (var n = new PaintShopActionPreparation(value, this)) {
                var a = n.GetAction();

                var s = a.Size;
                if (a.MaxPreviewSize.HasValue) {
                    var m = a.MaxPreviewSize.Value;
                    if (s.Width > m) {
                        s = new Size(m, s.Height * m / s.Width);
                    }

                    if (s.Height > m) {
                        s = new Size(s.Width * m / s.Height, m);
                    }
                }

                return OverrideTexture(a.Destination, a.DrawAction, s);
            }
        }

        public async Task SaveAsync(string location, PaintShopOverrideBase value) {
            using (var n = new PaintShopActionPreparation(value, this)) {
                var a = n.GetAction();
                await SaveTextureAsync(location, a.Destination, a.DrawAction, a.Size).ConfigureAwait(false);
            }
        }

        public void SetCurrentSkinActive(bool active) {
            CarNode?.SetCurrentSkinActive(active);
            IsDirty = true;
        }

        // disposal
        public void DisposePaintShop() {
            _patternTextRenderers?.DisposeEverything();
            DisposeHelper.Dispose(ref _patternSprite);

            _paintShopTextures?.DisposeEverything();
            _paintShopTextures = null;
            _paintShopMaskedTextures?.DisposeEverything();
            _paintShopMaskedTextures = null;
            _paintShopIndependantFix?.DisposeEverything();
            _paintShopIndependantFix = null;
            _paintShopCache?.Select(x => x.Value.Item1).DisposeEverything();
            _paintShopCache = null;

            _paintShopFlags?.DisposeEverything();
            _patternFontsCollections?.DisposeEverything();

            // _override?.DisposeEverything();
            /*_patternBase?.DisposeEverything();
            _patternBase?.DisposeEverything();
            _overlayBase?.DisposeEverything();
            _mapsBase?.DisposeEverything();
            _mapsMasks?.DisposeEverything();*/
            /*_tintBase?.DisposeEverything();
            _tintMask?.DisposeEverything();
            _tintOverlay?.DisposeEverything();*/
        }
    }
}