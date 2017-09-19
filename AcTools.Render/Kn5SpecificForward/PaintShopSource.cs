using System;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForward {
    public interface IPaintShopSourceReference {
        event EventHandler Updated;
    }

    public class ColorReference : IPaintShopSourceReference {
        private Lazier<Color?> _value;

        public ColorReference(Func<Color?> callback) {
            _value = Lazier.Create(callback);
        }

        public Color? GetValue() {
            return _value.Value;
        }

        public void RaiseUpdated() {
            _value.Reset();
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Updated;
    }

    public class TextureReference : IPaintShopSourceReference {
        public string TextureName { get; }

        [CanBeNull]
        private IPaintShopObject _object;

        [CanBeNull]
        private IRenderableTexture _texture;

        public TextureReference(string textureName) {
            TextureName = textureName;
        }

        [CanBeNull]
        public ShaderResourceView GetValue([NotNull] DeviceContextHolder device, [CanBeNull] IPaintShopObject obj) {
            if (obj != _object) {
                _object = obj;
                _texture = _object?.GetTexture(device, TextureName);
            }

            return _texture?.Resource;
        }

        public void RaiseUpdated() {
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Updated;

        public override string ToString() {
            return $"(ref:{TextureName})";
        }
    }

    public class PaintShopSource : PaintShopSourceParams {
        public static PaintShopSource InputSource => new PaintShopSource();
        public static PaintShopSource White => new PaintShopSource(System.Drawing.Color.White);
        public static PaintShopSource Transparent => new PaintShopSource(System.Drawing.Color.Transparent);

        public readonly bool UseInput;

        public readonly Color? Color;

        [CanBeNull]
        public readonly ColorReference ColorRef;

        [CanBeNull]
        public readonly TextureReference TextureRef;

        [CanBeNull]
        public readonly string Name;

        [CanBeNull]
        public readonly byte[] Data;

        public PaintShopSourceChannel RedFrom { get; private set; } = PaintShopSourceChannel.Red;
        public PaintShopSourceChannel GreenFrom { get; private set; } = PaintShopSourceChannel.Green;
        public PaintShopSourceChannel BlueFrom { get; private set; } = PaintShopSourceChannel.Blue;
        public PaintShopSourceChannel AlphaFrom { get; private set; } = PaintShopSourceChannel.Alpha;

        public bool ByChannels => RedChannelSource != null || BlueChannelSource != null ||
                GreenChannelSource != null || AlphaChannelSource != null;

        public bool Custom => Desaturate || NormalizeMax ||
                RedFrom != PaintShopSourceChannel.Red || GreenFrom != PaintShopSourceChannel.Green ||
                BlueFrom != PaintShopSourceChannel.Blue || AlphaFrom != PaintShopSourceChannel.Alpha ||
                ByChannels || TextureRef != null;

        public PaintShopSource() {
            UseInput = true;
        }

        [NotNull]
        public PaintShopSource MapChannels([CanBeNull] string postfix) {
            if (!string.IsNullOrWhiteSpace(postfix)) {
                postfix = postfix.ToLowerInvariant();
                var last = postfix[postfix.Length - 1];
                RedFrom = (PaintShopSourceChannel)postfix.ElementAtOr(0, last);
                GreenFrom = (PaintShopSourceChannel)postfix.ElementAtOr(1, last);
                BlueFrom = (PaintShopSourceChannel)postfix.ElementAtOr(2, last);
                AlphaFrom = (PaintShopSourceChannel)postfix.ElementAtOr(3, (char)PaintShopSourceChannel.Alpha);
            }

            return this;
        }

        public PaintShopSource(Color baseColor) {
            Color = baseColor;
        }

        public PaintShopSource([NotNull] ColorReference colorRef) {
            ColorRef = colorRef;
        }

        public PaintShopSource([NotNull] TextureReference textureRef) {
            TextureRef = textureRef;
        }

        public PaintShopSource([NotNull] string name) {
            Name = name;
        }

        public PaintShopSource([NotNull] byte[] data) {
            Data = data;
        }

        [CanBeNull]
        public IPaintShopSourceReference Reference => (IPaintShopSourceReference)ColorRef ?? TextureRef;

        public bool DoNotCache { get; set; }

        [CanBeNull]
        public readonly PaintShopSource RedChannelSource, GreenChannelSource, BlueChannelSource, AlphaChannelSource;

        public PaintShopSource(PaintShopSource red, PaintShopSource green, PaintShopSource blue, PaintShopSource alpha) {
            RedChannelSource = red;
            GreenChannelSource = green;
            BlueChannelSource = blue;
            AlphaChannelSource = alpha;
        }

        public PaintShopSource SetFrom([CanBeNull] PaintShopSourceParams baseParams) {
            if (baseParams == null || !baseParams.RequiresPreparation) return this;
            foreach (var p in typeof(PaintShopSourceParams).GetProperties().Where(p => p.CanWrite)) {
                p.SetValue(this, p.GetValue(baseParams, null), null);
            }
            return this;
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = UseInput ? -1 : Color?.GetHashCode() ?? ColorRef?.GetHashCode() ?? Name?.GetHashCode() ?? Data?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ Desaturate.GetHashCode();
                hashCode = (hashCode * 397) ^ NormalizeMax.GetHashCode();
                hashCode = (hashCode * 397) ^ (RedChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (GreenChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (BlueChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (AlphaChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ RedFrom.GetHashCode();
                hashCode = (hashCode * 397) ^ GreenFrom.GetHashCode();
                hashCode = (hashCode * 397) ^ BlueFrom.GetHashCode();
                hashCode = (hashCode * 397) ^ AlphaFrom.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() {
            if (UseInput) return "( PaintShopSource: use input )";
            if (Color != null) return $"( PaintShopSource: color={Color.Value} )";
            if (ColorRef != null) return $"( PaintShopSource: color ref={ColorRef.GetValue()} )";
            if (TextureRef != null) return $"( PaintShopSource: texture ref={TextureRef} )";
            if (Name != null) return $"( PaintShopSource: name={Name} )";
            if (Data != null) return $"( PaintShopSource: {Data} bytes )";

            if (RedChannelSource != null || BlueChannelSource != null || RedChannelSource != null ||
                    AlphaChannelSource != null) {
                return $"( PaintShopSource: (R={RedChannelSource}, G={GreenChannelSource}, B={BlueChannelSource}, A={AlphaChannelSource}) )";
            }

            return "( PaintShopSource: nothing )";
        }
    }
}