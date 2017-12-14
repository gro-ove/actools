using System;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class VirtualKeyCodeEventArgs : EventArgs {
        public readonly Keys Key;
        public bool Handled;
        public bool SkipMainEvent;

        public VirtualKeyCodeEventArgs(int virtualKeyCode) {
            Key = (Keys)virtualKeyCode;
        }
    }
}