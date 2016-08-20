using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows {
    public class DragPreview : IDisposable {
        private readonly Visual _adornerVisual;
        private readonly AdornerLayer _layer;

        private DragAdorner _dragAdorner;
        private readonly Point _mouseDown;
        private readonly Point _delta;

        public DragPreview(Visual adornerVisual, UIElement list, UIElement item) {
            _adornerVisual = adornerVisual;
            _dragAdorner = new DragAdorner(list, item.RenderSize, new VisualBrush(item)) {
                Opacity = 0.9
            };

            _mouseDown = _adornerVisual.GetMousePosition();
            _delta = item.TranslatePoint(new Point(0, 0), list);

            _layer = AdornerLayer.GetAdornerLayer(_adornerVisual);
            _layer.Add(_dragAdorner);
        }

        private List<UIElement> _targets;

        public void SetTargets(IEnumerable<UIElement> targets) {
            Application.Current.MainWindow.AllowDrop = false;
            _targets = targets.ToList();
            foreach (var target in _targets) {
                target.DragEnter += Target_DragEnter;
                target.DragLeave += Target_DragLeave;
                target.DragOver += Target_DragOver;
            }
        }

        private void Target_DragEnter(object sender, DragEventArgs e) {
            Update();
        }

        private void Target_DragLeave(object sender, DragEventArgs e) {
            Hide();
        }

        private void Target_DragOver(object sender, DragEventArgs e) {
            Update();
        }

        public void SetTargets(DependencyObject parent) {
            SetTargets(parent.FindVisualChildren<ListBox>());
        }

        public void Update() {
            if (_dragAdorner == null) return;
            _dragAdorner.Visibility = Visibility.Visible;
            var point = _adornerVisual.GetMousePosition() - _mouseDown + _delta;
            _dragAdorner.SetOffsets(point.X, point.Y);
        }

        public void Hide() {
            if (_dragAdorner == null) return;
            _dragAdorner.Visibility = Visibility.Collapsed;
        }

        public void Dispose() {
            if (_dragAdorner != null) {
                _layer.Remove(_dragAdorner);
                _dragAdorner = null;
            }

            if (_targets != null) {
                Application.Current.MainWindow.AllowDrop = true;

                foreach (var target in _targets) {
                    target.DragEnter -= Target_DragEnter;
                    target.DragLeave -= Target_DragLeave;
                    target.DragOver -= Target_DragOver;
                }

                _targets = null;
            }
        }
    }
}