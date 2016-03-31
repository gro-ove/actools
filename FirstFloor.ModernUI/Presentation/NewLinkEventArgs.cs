using System;

namespace FirstFloor.ModernUI.Presentation {
    public class NewLinkEventArgs : EventArgs {
        public NewLinkEventArgs(string inputValue) {
            InputValue = inputValue;
        }

        public string InputValue { get; }
    }
}