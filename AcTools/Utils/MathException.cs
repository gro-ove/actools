using System;

namespace AcTools.Utils {
    public class MathException : Exception {
        public MathException(string message)
                : base(message) { }
    }
}