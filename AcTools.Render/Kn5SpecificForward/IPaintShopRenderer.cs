using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using AcTools.Render.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public struct ValueAdjustment {
        public static readonly ValueAdjustment Same = new ValueAdjustment(0f, 1f);
        public static readonly ValueAdjustment One = new ValueAdjustment(1f, 0f);
        public static readonly ValueAdjustment Zero = new ValueAdjustment(0f, 0f);

        public float Add, Multiply;

        public ValueAdjustment(float add, float multiply) {
            Add = add;
            Multiply = multiply;
        }

        public ValueAdjustment(double add, double multiply) {
            Add = (float)add;
            Multiply = (float)multiply;
        }

        public bool Equals(ValueAdjustment other) {
            return Add.Equals(other.Add) && Multiply.Equals(other.Multiply);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ValueAdjustment adjustment && Equals(adjustment);
        }

        public override int GetHashCode() {
            unchecked {
                return (Add.GetHashCode() * 397) ^ Multiply.GetHashCode();
            }
        }

        public static bool operator ==(ValueAdjustment a, ValueAdjustment b) {
            return a.Equals(b);
        }

        public static bool operator !=(ValueAdjustment a, ValueAdjustment b) {
            return !a.Equals(b);
        }

        public bool CloseToFullRange() {
            return Multiply >= 0.7f || Add >= 0.3f;
        }

        public override string ToString() {
            return $"(×{Multiply}+{Add})";
        }
    }

    public abstract class PaintShopOverrideBase {
        public PaintShopDestination Destination;
    }

    public class PaintShopOverrideWithTexture : PaintShopOverrideBase {
        [CanBeNull]
        public PaintShopSource Source;
    }

    public class PaintShopOverrideWithColor : PaintShopOverrideBase {
        public Color Color;
        public int Size = 4;
        public double Flakes;
    }

    public class PaintShopOverridePattern : PaintShopOverrideBase {
        [CanBeNull]
        public PaintShopSource Ao, Pattern, Overlay, Underlay;

        [CanBeNull]
        public Color[] Colors;

        public int? SkinNumber;

        [CanBeNull]
        public IReadOnlyList<PaintShopPatternNumber> Numbers;

        [CanBeNull]
        public string SkinFlagFilename;

        [CanBeNull]
        public IReadOnlyList<PaintShopPatternFlag> Flags;

        [CanBeNull]
        public IReadOnlyDictionary<string, string> SkinLabels;

        [CanBeNull]
        public IReadOnlyList<PaintShopPatternLabel> Labels;
    }

    public class PaintShopOverrideMaps : PaintShopOverrideBase {
        public ValueAdjustment Reflection = ValueAdjustment.Same,
                Gloss = ValueAdjustment.Same,
                Specular = ValueAdjustment.Same;

        [CanBeNull]
        public PaintShopSource Source, Mask;
    }

    public class PaintShopOverrideTint : PaintShopOverrideBase {
        /// <summary>
        /// Several colors — for mask, in provided.
        /// </summary>
        [CanBeNull]
        public Color[] Colors;

        public ValueAdjustment Alpha = ValueAdjustment.Same;

        [CanBeNull]
        public PaintShopSource Source, Mask, Overlay;
    }

    public interface IPaintShopRenderer {
        bool Override([NotNull] PaintShopOverrideBase value);
        bool Reset([NotNull] string textureName);
        Task SaveAsync([NotNull] string location, [NotNull] PaintShopOverrideBase value);
        void SetCurrentSkinActive(bool active);
    }
}