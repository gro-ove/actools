using System;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Temporary;
using AcTools.Render.Utils;
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

    public class PaintShopDestination {
        [NotNull]
        public readonly string TextureName;

        [CanBeNull]
        public readonly PaintShopSource OutputMask;

        public readonly PreferredDdsFormat PreferredFormat;
        public readonly Size? ForceSize;

        public PaintShopDestination([NotNull] string textureName, PreferredDdsFormat preferredFormat,
                [CanBeNull] PaintShopSource outputMask = null, Size? forceSize = null) {
            TextureName = textureName;
            PreferredFormat = preferredFormat;
            OutputMask = outputMask;
            ForceSize = forceSize;
        }

        public PaintShopDestination([NotNull] string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            TextureName = ParseTextureName(name, out PreferredFormat);
        }

        public static string ParseTextureName(string name, out PreferredDdsFormat format) {
            var index = name.IndexOf(':');
            if (index == -1) {
                format = PreferredDdsFormat.AutoTransparency;
                return name;
            }

            format = ParseFormat(name.Substring(index + 1));
            return name.Substring(0, index);
        }

        public static PreferredDdsFormat ParseFormat(string format) {
            switch (format.Trim().ToLowerInvariant()) {
                case "dxt1":
                    return PreferredDdsFormat.DXT1;

                case "dxt":
                case "dxt5":
                    return PreferredDdsFormat.DXT5;

                case "l":
                case "lum":
                case "luminance":
                    return PreferredDdsFormat.Luminance;

                case "la":
                case "lumalpha":
                case "luminancealpha":
                    return PreferredDdsFormat.LuminanceTransparency;

                case "rgb565":
                case "rgb5650":
                case "565":
                case "5650":
                    return PreferredDdsFormat.RGB565;

                case "rgba4444":
                case "4444":
                    return PreferredDdsFormat.RGBA4444;

                case "rgba":
                case "rgba8888":
                case "8888":
                    return PreferredDdsFormat.NoCompressionTransparency;

                case "rgb":
                case "rgb888":
                case "rgba8880":
                case "888":
                case "8880":
                    return PreferredDdsFormat.NoCompression;

                case "h":
                case "hdr":
                    return PreferredDdsFormat.HDR;

                case "hdra":
                case "hdrauto":
                case "hauto":
                case "ha":
                case "autohdr":
                    return PreferredDdsFormat.AutoHDR;
            }

            return Enum.TryParse(format, true, out PreferredDdsFormat result) ?
                    result : PreferredDdsFormat.AutoTransparency;
        }

        public ValueAdjustment RedAdjustment = ValueAdjustment.Same,
                GreenAdjustment = ValueAdjustment.Same,
                BlueAdjustment = ValueAdjustment.Same,
                AlphaAdjustment = ValueAdjustment.Same;

        public bool AnyChannelAdjusted => RedAdjustment != ValueAdjustment.Same || GreenAdjustment != ValueAdjustment.Same ||
                BlueAdjustment != ValueAdjustment.Same || AlphaAdjustment != ValueAdjustment.Same;

        public PaintShopSourceChannel RedFrom = PaintShopSourceChannel.Red,
                GreenFrom = PaintShopSourceChannel.Green,
                BlueFrom = PaintShopSourceChannel.Blue,
                AlphaFrom = PaintShopSourceChannel.Alpha;

        public bool AnyChannelMapped => RedFrom != PaintShopSourceChannel.Red || GreenFrom != PaintShopSourceChannel.Green ||
                BlueFrom != PaintShopSourceChannel.Blue || AlphaFrom != PaintShopSourceChannel.Alpha;

        public PaintShopDestination InheritExtendedPropertiesFrom(PaintShopDestination baseDestination) {
            RedAdjustment = baseDestination.RedAdjustment;
            GreenAdjustment = baseDestination.GreenAdjustment;
            BlueAdjustment = baseDestination.BlueAdjustment;
            AlphaAdjustment = baseDestination.AlphaAdjustment;
            RedFrom = baseDestination.RedFrom;
            GreenFrom = baseDestination.GreenFrom;
            BlueFrom = baseDestination.BlueFrom;
            AlphaFrom = baseDestination.AlphaFrom;
            return this;
        }

        public override int GetHashCode() {
            return TextureName.GetHashCode();
        }

        public override bool Equals(object obj) {
            return TextureName.Equals((obj as PaintShopDestination)?.TextureName);
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

        public PaintShopSource() {
            UseInput = true;
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

        public PaintShopSource(PaintShopSource red, PaintShopSource green, PaintShopSource blue, PaintShopSource alpha) {
            RedChannelSource = red;
            GreenChannelSource = green;
            BlueChannelSource = blue;
            AlphaChannelSource = alpha;
        }

        public PaintShopSourceChannel RedFrom = PaintShopSourceChannel.Red,
                GreenFrom = PaintShopSourceChannel.Green,
                BlueFrom = PaintShopSourceChannel.Blue,
                AlphaFrom = PaintShopSourceChannel.Alpha;

        public bool AnyChannelMapped => RedFrom != PaintShopSourceChannel.Red || GreenFrom != PaintShopSourceChannel.Green ||
                BlueFrom != PaintShopSourceChannel.Blue || AlphaFrom != PaintShopSourceChannel.Alpha;

        [CanBeNull]
        public readonly PaintShopSource RedChannelSource,
                GreenChannelSource,
                BlueChannelSource,
                AlphaChannelSource;

        public bool DefinedByChannels => RedChannelSource != null || BlueChannelSource != null ||
                GreenChannelSource != null || AlphaChannelSource != null;

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

        public bool Custom => RequiresPreparation || AnyChannelMapped || AnyChannelAdjusted || DefinedByChannels || TextureRef != null;

        public PaintShopSource SetFrom([CanBeNull] PaintShopSourceParams baseParams) {
            if (baseParams == null) return this;

            foreach (var p in typeof(PaintShopSourceParams).GetProperties().Where(p => p.CanWrite)) {
                p.SetValue(this, p.GetValue(baseParams, null), null);
            }

            foreach (var p in typeof(PaintShopSourceParams).GetFields()) {
                p.SetValue(this, p.GetValue(baseParams));
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