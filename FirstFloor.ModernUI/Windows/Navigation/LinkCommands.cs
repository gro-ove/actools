using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Navigation {
    public static class LinkCommands {
        public static RoutedUICommand NavigateLink { get; } = new RoutedUICommand(UiStrings.NavigateLink, "NavigateLink", typeof(LinkCommands));
    }
}
