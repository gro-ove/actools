using System.Windows;
using System.Windows.Markup;
using AcManager.Tools.Objects;

namespace AcManager.Controls.UserControls {
    [ContentProperty(nameof(PreviewContent))]
    public partial class TrackBlock {
        public TrackBlock() {
            InitializeComponent();
            InnerTrackBlockPanel.DataContext = this;
        }

        public static readonly DependencyProperty ShowPreviewProperty = DependencyProperty.Register(nameof(ShowPreview), typeof(bool),
                typeof(TrackBlock), new PropertyMetadata(true));

        public bool ShowPreview {
            get { return (bool)GetValue(ShowPreviewProperty); }
            set { SetValue(ShowPreviewProperty, value); }
        }

        public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(nameof(Track), typeof(TrackObjectBase),
                typeof(TrackBlock));

        public TrackObjectBase Track {
            get { return (TrackObjectBase)GetValue(TrackProperty); }
            set { SetValue(TrackProperty, value); }
        }

        public static readonly DependencyProperty PreviewContentProperty = DependencyProperty.Register(nameof(PreviewContent), typeof(object),
                typeof(TrackBlock));

        public object PreviewContent {
            get { return (object)GetValue(PreviewContentProperty); }
            set { SetValue(PreviewContentProperty, value); }
        }
    }
}
