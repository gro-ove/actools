using System;
using System.Windows.Forms;

namespace AcTools.Windows.Input {
    public class SneakyPeekyEventArgs : EventArgs {
        public readonly Keys SneakedPeeked;
        public bool Handled;
        public bool SkipMainEvent;

        public SneakyPeekyEventArgs(int virtualKeyCode) {
            SneakedPeeked = (Keys)virtualKeyCode;
        }
    }
}