using System;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DirectInputButtonEventArgs {
        public DirectInputButtonEventArgs([NotNull] DirectInputButton button) {
            if (button == null) throw new ArgumentNullException(nameof(button));

            Provider = button;
            IsPressed = button.Value;
        }
        
        [NotNull]
        public DirectInputButton Provider { get; }

        public bool IsPressed { get; }
    }
}