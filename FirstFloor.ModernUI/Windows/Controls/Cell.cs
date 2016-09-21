using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class Cell : Panel {
        protected override Size MeasureOverride(Size constraint) {
            var width = 0d;
            var height = 0d;

            var children = InternalChildren;
            for (int i = 0, count = children.Count; i < count; ++i) {
                var child = children[i];
                child.Measure(constraint);

                var size = child.DesiredSize;
                if (size.Width > width) width = size.Width;
                if (size.Height > height) height = size.Height;
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            var rect = new Rect(arrangeBounds);

            var children = InternalChildren;
            for (int i = 0, count = children.Count; i < count; ++i) {
                children[i].Arrange(rect);
            }

            return arrangeBounds;
        }
    }
}