using AcManager.Controls;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetSessions {
        public ServerPresetSessions() {
            InitializeComponent();
            this.AddSizeCondition(x => x.ActualWidth < 500 ? 1 : x.ActualWidth < 1200 ? 2 : 4)
                .Add(b => ColumnsGrid.Columns = b.Clamp(1, 2));

            //.Add(b => TimeGrid.Columns = b.Clamp(1, 2))
            //.Add(x => SessionsControl.FindChild<SpacingUniformGrid>("ColumnsGrid"), (grid, b) => grid.Columns = b);
        }
    }
}
