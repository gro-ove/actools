using System;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class KeyboardEventArgs : EventArgs {
        public readonly Keys Key;
        public bool Handled;
        public bool SkipMainEvent;

        public KeyboardEventArgs(int keyCode) {
            Key = (Keys)keyCode;
        }
    }
}