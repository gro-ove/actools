using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using ImageMagick;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ToolsKn5ObjectRenderer : IPaintShopRenderer {
        public static int OptionPaintShopRandomSize = 256;

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

        private TargetResourceTexture GetTexture([CanBeNull] string textureName, Action<EffectSpecialPaintShop> update, int width = 256, int height = -1) {
            if (_paintShopTextures == null) {
                _paintShopTextures = new Dictionary<string, TargetResourceTexture>(10);
            }

            TargetResourceTexture tex;
            if (textureName == null) textureName = "";
            if (!_paintShopTextures.TryGetValue(textureName, out tex)) {
                tex = _paintShopTextures[textureName] = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            }

            if (height < 0) height = width;
            tex.Resize(DeviceContextHolder, width, height, null);

            UseEffect(update, tex);
            return tex;
        }

        private bool OverrideTexture(string textureName, Action<EffectSpecialPaintShop> update, int width, int height = -1) {
            return CarNode == null ||
                    CarNode.OverrideTexture(DeviceContextHolder, textureName, GetTexture(textureName, update, width, height).View, false);
        }

        private Task SaveTextureAsync(string filename, Action<EffectSpecialPaintShop> update, int width, int height = -1) {
            var tex = GetTexture(null, update, width, height);
            Texture2D.ToFile(DeviceContext, tex.Texture, ImageFileFormat.Dds, filename);
            return Task.Delay(0);
        }

        // get texture from KN5
        [CanBeNull]
        private Dictionary<string, byte[]> _decodedToPng;

        [CanBeNull]
        private byte[] GetDecoded(string textureName) {
            if (Kn5 == null) return null;

            if (_decodedToPng == null) {
                _decodedToPng = new Dictionary<string, byte[]>(6);
            }

            byte[] result;
            if (!_decodedToPng.TryGetValue(textureName, out result)) {
                if (!Kn5.TexturesData.ContainsKey(textureName)) return null;

                Format format;
                _decodedToPng[textureName] = result = TextureReader.ToPng(DeviceContextHolder, Kn5.TexturesData[textureName], false, out format);
            }

            return result;
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
            }

            AcToolsLogging.Write("Can’t get bytes: " + source);
            return null;
        }

        private MagickImage GetImage(PaintShopSource source) {
            if (!source.UseInput) {
                if (source.Name != null) {
                    var decoded = GetDecoded(source.Name);
                    if (decoded != null) {
                        return new MagickImage(decoded);
                    }
                } else if (source.Data != null) {
                    Format format;
                    return new MagickImage(TextureReader.ToPng(DeviceContextHolder, source.Data, false, out format));
                }
            }

            AcToolsLogging.Write("Can’t convert PaintShopSource to MagickImage: " + source);
            return new MagickImage(MagickColors.Transparent, 4, 4);
        }

        // prepare texture using DirectX
        private Dictionary<int, Size> _sizes;

        private Size? GetSize(PaintShopSource source) {
            Size result;
            if (_sizes.TryGetValue(source.GetHashCode(), out result)) {
                return result;
            }

            return null;
        }

        [CanBeNull]
        private ShaderResourceView GetOriginal(ref Dictionary<int, ShaderResourceView> storage, PaintShopSource source, int maxSize = 512,
                Func<ShaderResourceView, ShaderResourceView> preparation = null) {
            if (Kn5 == null) return null;

            if (storage == null) {
                storage = new Dictionary<int, ShaderResourceView>(2);
                if (_sizes == null) {
                    _sizes = new Dictionary<int, Size>();
                }
            }

            ShaderResourceView original;
            var hashCode = source.GetHashCode();
            if (!storage.TryGetValue(hashCode, out original)) {
                var decoded = GetBytes(source);
                
                using (var texture = Texture2D.FromMemory(Device, decoded)) {
                    original = new ShaderResourceView(Device, texture);
                    _sizes[hashCode] = new Size(texture.Description.Width, texture.Description.Height);

                    if (texture.Description.Width > maxSize || texture.Description.Height > maxSize) {
                        using (var resized = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                            resized.Resize(DeviceContextHolder, maxSize, maxSize, null);
                            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, original, resized.TargetView);
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
            if (CarNode == null) return true;
            return CarNode.OverrideTexture(DeviceContextHolder, textureName, textureBytes);
        }

        // override using PaintShopSource
        public bool OverrideTexture(string textureName, PaintShopSource source) {
            if (CarNode == null) return true;

            if (source == null || source.UseInput) {
                return CarNode.OverrideTexture(DeviceContextHolder, textureName, null);
            }

            if (source.Data != null) {
                return CarNode.OverrideTexture(DeviceContextHolder, textureName, source.Data);
            }

            if (source.Name != null) {
                return CarNode.OverrideTexture(DeviceContextHolder, textureName,
                        Kn5?.TexturesData.GetValueOrDefault(source.Name)); // if null, reset texture
            }

            return false;
        }

        public Task SaveTextureAsync(string filename, PaintShopSource source) {
            if (!source.UseInput) {
                if (source.Data != null) {
                    return FileUtils.WriteAllBytesAsync(filename, source.Data);
                }

                if (source.Name != null && Kn5?.TexturesData.ContainsKey(source.Name) == true) {
                    return FileUtils.WriteAllBytesAsync(filename, Kn5.TexturesData[source.Name]);
                }
            }

            return Task.Delay(0);
        }
        
        // solid color
        public bool OverrideTexture(string textureName, Color color) {
            return CarNode == null || CarNode.OverrideTexture(DeviceContextHolder, textureName, DeviceContextHolder.CreateTextureView(1, 1, (x, y) => color), true);
        }

        public Task SaveTextureAsync(string filename, Color color) {
            return SaveAndDispose(filename, new MagickImage(new MagickColor(color), 16, 16), Compression.None);
        }

        // solid color with alpha
        public bool OverrideTexture(string textureName, Color color, double alpha) {
            return OverrideTexture(textureName, e => {
                e.FxColor.Set(Color.FromArgb((byte)(255 * alpha), color).ToVector4());
                e.TechFill.DrawAllPasses(DeviceContext, 6);
            }, 1);
        }

        public Task SaveTextureAsync(string filename, Color color, double alpha) {
            return SaveTextureAsync(filename, e => {
                e.FxColor.Set(Color.FromArgb((byte)(255 * alpha), color).ToVector4());
                e.TechFill.DrawAllPasses(DeviceContext, 6);
            }, 16);
        }

        // car paint with flakes
        public bool OverrideTextureFlakes(string textureName, Color color, double flakes) {
            return OverrideTexture(textureName, e => {
                e.FxColor.Set(new Vector4(color.ToVector3(), 1f));
                e.FxFlakes.Set((float)flakes);
                e.TechFlakes.DrawAllPasses(DeviceContext, 6);
            }, 256);
        }

        public Task SaveTextureFlakesAsync(string filename, Color color, double flakes) {
            return SaveTextureAsync(filename, e => {
                e.FxColor.Set(new Vector4(color.ToVector3(), 1f));
                e.FxFlakes.Set((float)flakes);
                e.TechFlakes.DrawAllPasses(DeviceContext, 6);
            }, 256);
        }

        // txMaps
        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _mapsBase;

        public bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular, bool autoAdjustLevels, PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(textureName);
            var original = GetOriginal(ref _mapsBase, source, 512, image => autoAdjustLevels ? NormalizeMax(image) : null);
            if (original == null) return false;
            return OverrideTexture(textureName, e => {
                e.FxInputMap.SetResource(original);
                e.FxColor.Set(new Vector4((float)specular, (float)gloss, (float)reflection, 1f));
                e.TechMaps.DrawAllPasses(DeviceContext, 6);
            }, 512);
        }

        public Task SaveTextureMapsAsync(string filename, double reflection, double gloss, double specular, bool autoAdjustLevels, PaintShopSource source) {
            var original = GetOriginal(ref _mapsBase, source, int.MaxValue, image => autoAdjustLevels ? NormalizeMax(image) : null);
            if (original == null) return Task.Delay(0);

            var size = GetSize(source) ?? new Size(512, 512);
            return SaveTextureAsync(filename, e => {
                e.FxInputMap.SetResource(original);
                e.FxColor.Set(new Vector4((float)specular, (float)gloss, (float)reflection, 1f));
                e.TechMaps.DrawAllPasses(DeviceContext, 6);
            }, size.Width, size.Height);
        }

        // tint texture
        [CanBeNull]
        private Dictionary<int, ShaderResourceView> _tintBase;

        public bool OverrideTextureTint(string textureName, Color color, bool autoAdjustLevels, double alphaAdd, PaintShopSource source) {
            if (source.UseInput) source = new PaintShopSource(textureName);
            var original = GetOriginal(ref _tintBase, source, 512, image => autoAdjustLevels ? NormalizeMax(image) : null);
            if (original == null) return false;
            return OverrideTexture(textureName, e => {
                e.FxInputMap.SetResource(original);
                e.FxColor.Set(new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, (float)alphaAdd));
                e.TechMaps.DrawAllPasses(DeviceContext, 6);
            }, 512);
        }

        public Task SaveTextureTintAsync(string filename, Color color, bool autoAdjustLevels, double alphaAdd, PaintShopSource source) {
            var original = GetOriginal(ref _tintBase, source, 512, image => autoAdjustLevels ? NormalizeMax(image) : null);
            if (original == null) return Task.Delay(0);

            var size = GetSize(source) ?? new Size(512, 512);
            return SaveTextureAsync(filename, e => {
                e.FxInputMap.SetResource(original);
                e.FxColor.Set(new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, (float)alphaAdd));
                e.TechMaps.DrawAllPasses(DeviceContext, 6);
            }, size.Width, size.Height);
        }

        private enum Compression {
            [Description("none")]
            None,

            // because of whatever reason, ImageMagick can’t save DDS properly
            [Description("none")]
            Dxt1,

            [Description("none")]
            Dxt5
        }

        private Task SaveAndDispose(string filename, MagickImage image, Compression compression) {
            try {
                if (File.Exists(filename)) {
                    FileUtils.Recycle(filename);
                }

                image.Quality = 100;
                image.Settings.SetDefine(MagickFormat.Dds, "compression", compression.GetDescription());
                image.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", "true");
                image.Settings.SetDefine(MagickFormat.Dds, "weight-by-alpha", "false");
                image.Settings.SetDefine(MagickFormat.Dds, "mipmaps", "false");
                var bytes = image.ToByteArray(MagickFormat.Dds);
                return FileUtils.WriteAllBytesAsync(filename, bytes);
            } finally {
                image.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeMagickNet() {
            _mapsBase?.DisposeEverything();
            _tintBase?.DisposeEverything();
        }

        private void DisposePaintShop() {
            _paintShopTextures?.DisposeEverything();
            if (ImageUtils.IsMagickSupported) {
                try {
                    DisposeMagickNet();
                } catch {
                    // ignored
                }
            }
        }
    }
}