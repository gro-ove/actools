using System;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    public interface IDraggable {
        string DraggableFormat { get; }
    }

    public class DragPreview : IDisposable {
        private Window _mainWindow;
        private DragPopup _dragPopup;

        public DragPreview([NotNull] UIElement item) {
            if (item == null) throw new ArgumentNullException(nameof(item));

            _dragPopup = new DragPopup(item.RenderSize, new VisualBrush(item));
            _dragPopup.UpdatePosition();

            _mainWindow = Application.Current?.MainWindow;
            if (_mainWindow?.AllowDrop == true) {
                _mainWindow.AllowDrop = false;
            } else {
                _mainWindow = null;
            }
        }

        public void Dispose() {
            if (_dragPopup != null) {
                _dragPopup.Dispose();
                _dragPopup = null;
            }

            if (_mainWindow != null) {
                _mainWindow.AllowDrop = true;
                _mainWindow = null;
            }
        }
    }
}