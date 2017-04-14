using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ToolsKn5ObjectRenderer : IPaintShopRenderer {
        public static int OptionPaintShopRandomSize = 256;
        public static int OptionColorSize = 16;
        public static int OptionFlakesSize = 256;
        public static int OptionMaxTintSize = 1024;
        public static int OptionMaxMapSize = 1024;
        public static int OptionMaxPatternSize = 1024;

        // to generate real-time preview, we’re going to use a special shader
        [CanBeNull]
        private Dictionary<string, TargetResourceTexture> _paintShopTextures;

        [CanBeNull]
        private EffectSpecialPaintShop _paintShopEffect;

        private void UseEffect(Action<EffectSpecialPaintShop> fn, TargetResourceTexture tex) {
            if (_paintShopEffect == null) {
                _paintShopEffect = DeviceContextHolder.GetEffect<EffectSpecialPaintShop>();
                _paintShopEffect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(OptionPaintShopRandomSize, OptionPaintShopRandomSize));
            }

            DeviceContextHolder.PrepareQuad(_paintShopEffect.LayoutPT);
            DeviceContextHolder.SaveRenderTargetAndViewport();
            DeviceContext.Rasterizer.SetViewports(tex.Viewport);
            DeviceContext.OutputMerger.SetTargets(tex.TargetView);
            DeviceContext.ClearRenderTargetView(tex.TargetView, new Color4(0f, 0f, 0f, 0f));
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.State = null;

            _paintShopEffect.FxNoiseMultipler.Set(Math.Max(tex.Width, tex.Height) / OptionPaintShopRandomSize);
            _paintShopEffect.FxSize.Set(new Vector4(tex.Width, tex.Height, 1f / tex.Width, 1f / tex.Height));

            fn?.Invoke(_paintShopEffect);

            DeviceContextHolder.RestoreRenderTargetAndViewport();
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

            if (size.Height< 0) size.Height = size.Width;
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

        private Task SaveTextureAsync(string filename, Action<EffectSpecialPaintShop> update, Size size) {
            var tex = GetTexture(null, update, size);
            Texture2D.ToFile(DeviceContext, tex.Texture, ImageFileFormat.Dds, filename);
            return Task.Delay(0);
        }

        private Task SaveTextureAsync(string filename, Action<EffectSpecialPaintShop> update, int width, int height = -1) {
            return SaveTextureAsync(filename, update, new Size(width, height));
        }

        // get things from PaintShopSource
        [CanBeNull]
        private byte[] GetBytes(PaintShopSource source) {
            if (!source.UseInput) {
                if (source.Data != null) {
                    return source.Data;
                }

                if (source.Name != null && Kn5?.TexturesData.ContainsKey(source.Name) == true) {
                    return Kn5.TexturesData[source.Name];
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

        private Size? GetSize(PaintShopSource source) {
            Size result;
            return _sizes.TryGetValue(source.GetHashCode(), out result) ? (Size?)result : null;
        }

        [CanBeNull]
        private ShaderResourceView GetOriginal(ref Dictionary<int, ShaderResourceView> storage, [NotNull] PaintShopSource source, int maxSize,
                Func<ShaderResourceView, ShaderResourceView> preparation = null) {
            if (Kn5 == null) return null;

            if (storage == null) {
                storage = new Dictionary<int, ShaderResourceView>(2);
                if (_sizes == null) {
                    _sizes = new Dictionary<int, Size>();
                }
            }

            ShaderResourceView original;
            var hashCode = (source.GetHashCode() * 397) ^ maxSize.GetHashCode();
            if (!storage.TryGetValue(hashCode, out original)) {
                var decoded = GetBytes(source);
                if (decoded == null) return null;
                
                using (var texture = Texture2D.FromMemory(Device, decoded)) {
                    original = new ShaderResourceView(Device, texture);
                    _sizes[hashCode] = new Size(texture.Description.Width, texture.Description.Height);

                    if (texture.Description.Width > maxSize || texture.Description.Height > maxSize) {
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

                var prepared = preparation?.Invoke(original);
                if (prepared != null && !ReferenceEquals(prepared, original)) {
                    original.Dispose();
                    original = prepared;
                }

                storage[hashCode] = original;
            }

            return original;
        }

        // ready bytes/view
        public bool OverrideTexture(string textureName, byte[] textureBytes) {
            return CarNode == null || CarNode.OverrideTexture(DeviceContextHolder, textureName, textureBytes);
        }

        // override using PaintShopSource
        public bool OverrideTexture(string textureName, PaintShopSource source) {
            if (CarNode == null) return true;

            if (source == null || source.UseInput) {
                return CarNode.OverrideTexture(DeviceContextHolder, textureName, null);
            }

            return CarNode.OverrideTexture(DeviceContextHolder, textureName, GetBytes(source));
        }

        public Task SaveTextureAsync(string filename, PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(Path.GetFileName(filename) ?? "");
            var bytes = GetBytes(source);
            return bytes != null ? FileUtils.WriteAllBytesAsync(filename, bytes) : Task.Delay(0);
        }

        // solid color with alpha
        private Action<EffectSpecialPaintShop> ColorAction(Color color, double alpha) => e => {
            e.FxColor.Set(Color.FromArgb((byte)(255 * alpha), color).ToVector4());
            e.TechFill.DrawAllPasses(DeviceContext, 6);
        };

        public bool OverrideTexture(string textureName, Color color, double alpha) {
            return OverrideTexture(textureName, ColorAction(color, alpha), 1);
        }

        public Task SaveTextureAsync(string filename, Color color, double alpha) {
            return SaveTextureAsync(filename, ColorAction(color, alpha), OptionColorSize);
        }

        // car paint with flakes
        private Action<EffectSpecialPaintShop> FlakesAction(Color color, double flakes) => e => {
            e.FxColor.Set(new Vector4(color.ToVector3(), 1f));
            e.FxFlakes.Set((float)flakes);
            e.TechFlakes.DrawAllPasses(DeviceContext, 6);
        };

        public bool OverrideTextureFlakes(string textureName, Color color, double flakes) {
            return OverrideTexture(textureName, FlakesAction(color, flakes), OptionFlakesSize);
        }

        public Task SaveTextureFlakesAsync(string filename, Color color, double flakes) {
            return SaveTextureAsync(filename, FlakesAction(color, flakes), OptionFlakesSize);
        }

        // pattern
        private Action<EffectSpecialPaintShop> PatternAction(ShaderResourceView patternView, ShaderResourceView aoView, ShaderResourceView overlayView,
                Color[] colors) => e => {
                    e.FxInputMap.SetResource(patternView);
                    e.FxAoMap.SetResource(aoView);
                    e.FxOverlayMap.SetResource(overlayView);

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
                };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _aoBase, _patternBase, _overlayBase;

        public bool OverrideTexturePattern(string textureName, PaintShopSource ao, bool autoAdjustLevels, PaintShopSource pattern, PaintShopSource overlay, 
                Color[] colors) {
            if (ao.UseInput) ao = new PaintShopSource(textureName);

            var aoView = GetOriginal(ref _aoBase, ao, OptionMaxPatternSize, image => autoAdjustLevels ? NormalizeMax(image) : null);
            if (aoView == null) return false;

            var patternView = GetOriginal(ref _patternBase, pattern, OptionMaxPatternSize);
            if (patternView == null) return false;

            var overlayView = overlay == null ? null : GetOriginal(ref _overlayBase, overlay, OptionMaxPatternSize);
            return OverrideTexture(textureName,
                    PatternAction(patternView, aoView, overlayView, colors),
                    OptionMaxPatternSize);
        }

        public Task SaveTexturePatternAsync(string filename, PaintShopSource ao, bool autoAdjustLevels, PaintShopSource pattern, PaintShopSource overlay,
                Color[] colors) {
            if (ao.UseInput) ao = new PaintShopSource(Path.GetFileName(filename) ?? "");

            var aoView = GetOriginal(ref _aoBase, ao, int.MaxValue, image => autoAdjustLevels ? NormalizeMax(image) : null);
            if (aoView == null) return Task.Delay(0);

            var patternView = GetOriginal(ref _patternBase, pattern, int.MaxValue);
            if (patternView == null) return Task.Delay(0);

            var overlayView = overlay == null ? null : GetOriginal(ref _overlayBase, overlay, OptionMaxPatternSize);
            return SaveTextureAsync(filename,
                    PatternAction(patternView, aoView, overlayView, colors),
                    GetSize(pattern) ?? (overlay == null ? null : GetSize(overlay)) ??
                            GetSize(ao) ?? new Size(OptionMaxPatternSize, OptionMaxPatternSize));
        }

        // txMaps
        private Action<EffectSpecialPaintShop> MapsAction(ShaderResourceView original, double reflection, double gloss, double specular, bool fixGloss)
                => e => {
                    e.FxInputMap.SetResource(original);
                    e.FxColor.Set(new Vector4((float)specular, (float)gloss, (float)reflection, 1f));
                    (fixGloss ? e.TechMapsFillGreen : e.TechMaps).DrawAllPasses(DeviceContext, 6);
                };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _mapsBase;

        public bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular, bool autoAdjustLevels, bool fixGloss,
                PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(textureName);
            var original = GetOriginal(ref _mapsBase, source, OptionMaxMapSize, image => autoAdjustLevels ? NormalizeMax(image) : null);
            return original != null && OverrideTexture(textureName,
                    MapsAction(original, reflection, gloss, specular, fixGloss),
                    OptionMaxMapSize);
        }

        public Task SaveTextureMapsAsync(string filename, double reflection, double gloss, double specular, bool autoAdjustLevels, bool fixGloss,
                PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(Path.GetFileName(filename) ?? "");
            var original = GetOriginal(ref _mapsBase, source, int.MaxValue, image => autoAdjustLevels ? NormalizeMax(image) : null);
            return original == null ? Task.Delay(0) : SaveTextureAsync(filename,
                    MapsAction(original, reflection, gloss, specular, fixGloss),
                    GetSize(source) ?? new Size(OptionMaxMapSize, OptionMaxMapSize));
        }

        // tint texture
        private Action<EffectSpecialPaintShop> TintAction(ShaderResourceView original, Color color, double alphaAdd) => e => {
            e.FxInputMap.SetResource(original);
            e.FxColor.Set(new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, (float)alphaAdd));
            e.TechMaps.DrawAllPasses(DeviceContext, 6);
        };

        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _tintBase;

        public bool OverrideTextureTint(string textureName, Color color, bool autoAdjustLevels, double alphaAdd, PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(textureName);
            var original = GetOriginal(ref _tintBase, source, OptionMaxTintSize, image => autoAdjustLevels ? NormalizeMax(image) : null);
            return original != null && OverrideTexture(textureName,
                    TintAction(original, color, alphaAdd),
                    OptionMaxTintSize);
        }

        public Task SaveTextureTintAsync(string filename, Color color, bool autoAdjustLevels, double alphaAdd, PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(Path.GetFileName(filename) ?? "");
            var original = GetOriginal(ref _tintBase, source, int.MaxValue, image => autoAdjustLevels ? NormalizeMax(image) : null);
            return original == null ? Task.Delay(0) : SaveTextureAsync(filename,
                    TintAction(original, color, alphaAdd),
                    GetSize(source) ?? new Size(OptionMaxTintSize, OptionMaxTintSize));
        }

        // disposal
        private void DisposePaintShop() {
            _paintShopTextures?.DisposeEverything();
            _aoBase?.DisposeEverything();
            _patternBase?.DisposeEverything();
            _overlayBase?.DisposeEverything();
            _mapsBase?.DisposeEverything();
            _tintBase?.DisposeEverything();
        }
    }
}