using System;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class DraggableMovedEventArgs : EventArgs {
        public DraggableMovedEventArgs(string format, object draggable) {
            Format = format;
            Draggable = draggable;
        }

        public string Format { get; }

        public object Draggable { get; }
    }
}