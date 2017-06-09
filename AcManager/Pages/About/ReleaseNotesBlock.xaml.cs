using System;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.About;
using AcManager.Tools.Helpers;

namespace AcManager.Pages.About {
    public partial class ReleaseNotesBlock {
        public ReleaseNotesBlock() {
            InitializeComponent();

            /* TODO */
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null) {
                WeakEventManager<Window, EventArgs>.AddHandler(mainWindow, nameof(mainWindow.Activated), Handler);
            }
        }

        public static readonly DependencyProperty PieceProperty = DependencyProperty.Register(nameof(Piece), typeof(PieceOfInformation),
                typeof(ReleaseNotesBlock), new PropertyMetadata(OnPieceChanged));

        public PieceOfInformation Piece {
            get { return (PieceOfInformation)GetValue(PieceProperty); }
            set { SetValue(PieceProperty, value); }
        }

        private static void OnPieceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ReleaseNotesBlock)o).OnPieceChanged((PieceOfInformation)e.NewValue);
        }

        private void OnPieceChanged(PieceOfInformation newValue) {
            Root.DataContext = newValue;
            MarkAsRead(newValue).Forget();
        }

        private void Handler(object sender, EventArgs eventArgs) {
            MarkAsRead(Piece).Forget();
        }

        private async Task MarkAsRead(PieceOfInformation value) {
            if (value == null) return;
            await Task.Delay(1000);
            if (value != Piece) return;
            if (Application.Current?.MainWindow?.IsActive == true) {
                value.MarkAsRead();
            }
        }
    }
}
