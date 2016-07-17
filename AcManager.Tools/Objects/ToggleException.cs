using System;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ToggleException : Exception {
        internal ToggleException([LocalizationRequired] string message) : base(message) { }
    }
}
