using System;
using System.Drawing;
using System.Linq;
using System.Text;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopSource : PaintShopSourceParams {
        public static PaintShopSource InputSource => new PaintShopSource();
        public static PaintShopSource White => new PaintShopSource(System.Drawing.Color.White);
        public static PaintShopSource Transparent => new PaintShopSource(System.Drawing.Color.Transparent);

        public readonly bool UseInput;

        public readonly Color? Color;

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
                ByChannels;

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

        public PaintShopSource([NotNull] string name) {
            Name = name;
        }

        public PaintShopSource([NotNull] byte[] data) {
            Data = data;
        }

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
                var hashCode = UseInput ? -1 : Color?.GetHashCode() ?? Name?.GetHashCode() ?? Data?.GetHashCode() ?? 0;
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