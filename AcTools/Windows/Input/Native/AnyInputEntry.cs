using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace AcTools.Windows.Input.Native {
#pragma warning disable 649
    /// <summary>
    /// The combined/overlayed structure that includes Mouse, Keyboard and Hardware Input message data (see: http://msdn.microsoft.com/en-us/library/ms646270(VS.85).aspx)
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct AnyInputEntry {
        /// <summary>
        /// The <see cref="MountInputEntry"/> definition.
        /// </summary>
        [FieldOffset(0)]
        public MountInputEntry Mouse;

        /// <summary>
        /// The <see cref="KeyboardInputEntry"/> definition.
        /// </summary>
        [FieldOffset(0)]
        public KeyboardInputEntry Keyboard;

        /// <summary>
        /// The <see cref="HardwareInputEntry"/> definition.
        /// </summary>
        [FieldOffset(0)]
        public HardwareInputEntry Hardware;
    }
#pragma warning restore 649
}
