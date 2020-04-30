using FirstFloor.ModernUI.Windows.Controls;
using System;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Navigation {
    /// <summary>
    /// Defines the base navigation event arguments.
    /// </summary>
    public abstract class NavigationBaseEventArgs : EventArgs {
        /// <summary>
        /// Gets the frame that raised this event.
        /// </summary>
        public ModernFrame Frame { get; internal set; }

        /// <summary>
        /// Gets the source uri for the target being navigated to.
        /// </summary>
        [CanBeNull]
        public Uri Source { get; internal set; }
    }
}
