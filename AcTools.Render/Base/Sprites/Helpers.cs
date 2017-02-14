using System;
using System.Collections.Generic;

namespace AcTools.Render.Base.Sprites {
    /// <summary>
    /// This structure can be used in place of doubles that represent angles in radians. The conversion is performed implicitly.
    /// </summary>
    public struct Degrees {
        /// <summary>
        /// Gets or sets the angle of this object in degrees.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Constructs a new Degrees object.
        /// </summary>
        /// <param name="degrees">The angle to initialize this object with in degrees</param>
        public Degrees(double degrees) : this() {
            Value = degrees;
        }

        /// <summary>
        /// Converts a Degrees object to a double value that represents the same angle in radians.
        /// </summary>
        /// <param name="d">A Degrees object</param>
        /// <returns>A double value that represents the same angle in radians</returns>
        public static implicit operator double(Degrees d) {
            return d.Value * Math.PI / 180;
        }
    }

    /// <summary>
    /// This structure can be used in place of double that represent angles in radians. It does not perform any conversion, but 
    /// it is introduced as the dual to the Degrees struct.
    /// </summary>
    public struct Radians {
        /// <summary>
        /// Gets or sets the angle of this object in radians.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Constructs a new Radians object.
        /// </summary>
        /// <param name="rad">The angle to initialize this object with in radians</param>
        public Radians(double rad) : this() {
            Value = rad;
        }

        /// <summary>
        /// Converts a Radians object to a double value that represents the same angle in radians.
        /// </summary>
        /// <param name="d">A Radians object</param>
        /// <returns>A double value that represents the same angle in radians</returns>
        public static implicit operator double(Radians d) {
            return d.Value;
        }
    }

    public static class Helpers {
        public static int[] ConvertToCodePointArray(string text) {
            var codePoints = new List<int>();
            for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1) codePoints.Add(char.ConvertToUtf32(text, i));
            return codePoints.ToArray();
        }
    }
}
