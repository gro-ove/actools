using System;

namespace FirstFloor.ModernUI.Windows.Navigation {
    /// <summary>
    /// Provides data for the NavigationFailed event.
    /// </summary>
    public class NavigationFailedEventArgs : NavigationBaseEventArgs {
        /// <summary>
        /// Gets the error from the failed navigation.
        /// </summary>
        public Exception Error { get; internal set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the failure event has been handled.
        /// </summary>
        /// <remarks>
        /// When not handled, the error is displayed in the ModernFrame raising the NavigationFailed event.
        /// </remarks>
        public bool Handled { get; set; }
    }
}
