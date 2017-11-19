using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using AcManager.Tools.Objects;

namespace AcManager.Controls {
    [ContentProperty(nameof(PreviewContent))]
    public class TrackBlock : Control {
        static TrackBlock() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TrackBlock), new FrameworkPropertyMetadata(typeof(TrackBlock)));
        }

        public static readonly DependencyProperty ShowPreviewProperty = DependencyProperty.Register(nameof(ShowPreview), typeof(bool),
                typeof(TrackBlock), new PropertyMetadata(true));

        public bool ShowPreview {
            get => GetValue(ShowPreviewProperty) as bool? == true;
            set => SetValue(ShowPreviewProperty, value);
        }

        public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(nameof(Track), typeof(TrackObjectBase),
                typeof(TrackBlock));

        public TrackObjectBase Track {
            get => (TrackObjectBase)GetValue(TrackProperty);
            set => SetValue(TrackProperty, value);
        }

        public static readonly DependencyProperty PreviewContentProperty = DependencyProperty.Register(nameof(PreviewContent), typeof(object),
                typeof(TrackBlock));

        public object PreviewContent {
            get => GetValue(PreviewContentProperty);
            set => SetValue(PreviewContentProperty, value);
        }
    }
}