using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Special {
    public class Pieces {
        public readonly string CentralPiece;
        public readonly string TopPiece;
        public readonly string TopRightPiece;
        public readonly string RightPiece;
        public readonly string BottomRightPiece;
        public readonly string BottomPiece;
        public readonly string BottomLeftPiece;
        public readonly string LeftPiece;
        public readonly string TopLeftPiece;

        public string[] ToArray() {
            return new[] {
                CentralPiece,
                TopPiece,
                TopRightPiece,
                RightPiece,
                BottomRightPiece,
                BottomPiece,
                BottomLeftPiece,
                LeftPiece,
                TopLeftPiece,
            };
        }

        public Pieces(string directory, string format, int y, int x) {
            CentralPiece = Path.Combine(directory, string.Format(format, y, x));
            TopPiece = Path.Combine(directory, string.Format(format, y - 1, x));
            TopRightPiece = Path.Combine(directory, string.Format(format, y - 1, x + 1));
            RightPiece = Path.Combine(directory, string.Format(format, y, x + 1));
            BottomRightPiece = Path.Combine(directory, string.Format(format, y + 1, x + 1));
            BottomPiece = Path.Combine(directory, string.Format(format, y + 1, x));
            BottomLeftPiece = Path.Combine(directory, string.Format(format, y + 1, x - 1));
            LeftPiece = Path.Combine(directory, string.Format(format, y, x - 1));
            TopLeftPiece = Path.Combine(directory, string.Format(format, y - 1, x - 1));
        }
    }

    public class PiecesBlender : BaseRenderer {
        private readonly float _padding;

        public PiecesBlender(int pieceWidth, int pieceHeight, float padding) {
            _padding = padding;
            Width = pieceWidth;
            Height = pieceHeight;
        }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;
        protected override void ResizeInner() {}
        protected override void OnTickOverride(float dt) {}

        private EffectSpecialPiecesBlender _effect;

        protected override void InitializeInner() {
            _effect = DeviceContextHolder.GetEffect<EffectSpecialPiecesBlender>();
        }

        private class CacheEntry : IDisposable {
            public readonly string Key;
            public readonly ShaderResourceView Value;
            public readonly long Size;
            public int UsedId;

            public CacheEntry(string key, ShaderResourceView value) {
                Key = key;
                Value = value;
                Size = value.GetBytesSize();
            }

            public void Dispose() {
                Value?.Dispose();
            }
        }

        private long _totalSize = 0;
        public static long OptionMaxCacheSize = 256 * 1024 * 1024; // 256 MB

        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private int _usedId;

        [CanBeNull]
        private ShaderResourceView LoadTexture(string filename) {
            if (_cache.TryGetValue(filename, out var cacheEntry)) {
                if (cacheEntry == null) return null;

                cacheEntry.UsedId = ++_usedId;
                return cacheEntry.Value;
            }

            if (!File.Exists(filename)) {
                _cache[filename] = null;
                return null;
            }

            if (_totalSize > OptionMaxCacheSize) {
                // AcToolsLogging.Write($"Limit exceeded: {_totalSize.ToReadableSize()} out of {OptionMaxCacheSize.ToReadableSize()} (cached: {_cache.Count})");

                var e = _cache.Values.NonNull().Where(x => x.Key != filename).MinEntryOrDefault(x => x.UsedId);
                if (e != null) {
                    _totalSize -= e.Size;
                    _cache.Remove(e.Key);
                    e.Dispose();
                    GCHelper.CleanUp();
                }
            }

            ShaderResourceView view;
            try {
                view = ShaderResourceView.FromFile(Device, filename);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                _cache[filename] = null;
                return null;
            }

            cacheEntry = new CacheEntry(filename, view);
            _totalSize += cacheEntry.Size;
            _cache[filename] = cacheEntry;

            cacheEntry.UsedId = ++_usedId;
            return cacheEntry.Value;
        }

        public void Process(Pieces pieces, [NotNull] Stream stream) {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var textures = pieces.ToArray().Select(LoadTexture).ToArray();
            _effect.FxInputMaps.SetResourceArray(textures);
            _effect.FxTexMultiplier.Set(new Vector2(1f / _padding));
            _effect.FxPaddingSize.Set(new Vector2(2f / (_padding - 1f)));

            Draw();

            Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
        }

        protected override void DisposeOverride() {
            _cache.DisposeEverything();
            base.DisposeOverride();
        }

        protected override void DrawOverride() {
            DeviceContext.OutputMerger.SetTargets(RenderTargetView);
            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);
            _effect.TechBlend.DrawAllPasses(DeviceContextHolder.DeviceContext, 6);
        }
    }
}
