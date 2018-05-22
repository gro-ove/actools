using System;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Win8Extension {
    public class ToastParameters {
        public string Title { get; }
        public string Message { get; }

        public ToastParameters([NotNull] string title, [NotNull] string message) {
            Title = title;
            Message = message;
        }

        public bool BoundToCallingApplication { get; set; }
        public string AppUserModelId { get; set; }
        public Uri IconUri { get; set; }
        public Action ClickCallback { get; set; }
    }
}