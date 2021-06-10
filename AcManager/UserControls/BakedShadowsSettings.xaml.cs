using System.Windows.Input;
using AcManager.CustomShowroom;
using FirstFloor.ModernUI.Commands;

namespace AcManager.UserControls {
    public partial class BakedShadowsSettings {
        public BakedShadowsSettings() {
            InitializeComponent();

            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(() => (DataContext as BakedShadowsRendererViewModel)?.ShareCommand.Execute(null)),
                        new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });
        }
    }
}